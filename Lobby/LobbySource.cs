using WhatsDab.Chat;
using Thread = WhatsDab.Chat.Thread;

namespace WhatsDab.Lobby
{
    /// <summary>
    /// The real conversations: everyone in the Steam lobby, over the same lobby-chat mechanism the game already uses
    /// for its own control messages. No FishNet, and nothing from any other mod - a lobby is a lobby, whether it was
    /// opened from the main menu, by a gamemode, or by plain co-op.
    ///
    /// This class is the translation layer and holds no state of its own. The conversation store, the wire format,
    /// the transport and the member list are separate files because each of them is worth reading on its own; what
    /// this one does is turn "SteamIDs and envelopes" into the threads and messages the interface understands.
    /// </summary>
    internal sealed class LobbySource : IChatSource
    {
        /// <summary>How often the member list is rebuilt. Cheap, but not free, and nobody joins twice a second.</summary>
        private const float ContactRefreshSeconds = 5f;

        private readonly List<Thread> _threads = new List<Thread>();
        private int _threadsRevision = -1;
        private int _seq;
        private float _nextRefresh;
        private ulong _lobby;
        private Thread _arrival;

        internal LobbySource()
        {
            ChatTransport.Start(OnIncoming);
        }

        public string Self => "You";

        public int Revision => ChatStore.Revision;

        public bool Online => ChatTransport.InLobby;

        public int Peers => Contacts.All.Count;

        public string TypingIn => null;   // nothing on the wire says so; inventing it would be a lie on screen

        public IReadOnlyList<Thread> Threads
        {
            get
            {
                Rebuild();
                return _threads;
            }
        }

        public Thread Find(string id)
        {
            foreach (Thread t in Threads)
                if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;

            return null;
        }

        public bool Send(string threadId, string text)
        {
            if (!TryKey(threadId, out ulong key)) return false;

            int seq = ++_seq;
            if (ChatTransport.Send(key, seq, text) < 0) return false;

            // Shown immediately and marked pending; the flag clears when Steam echoes the message back, which is the
            // only confirmation there is that it left the machine.
            ChatStore.AddLocal(key, seq, text);
            return true;
        }

        public void MarkRead(string threadId)
        {
            if (TryKey(threadId, out ulong key)) ChatStore.MarkRead(key);
        }

        public int TotalUnread() => ChatStore.TotalUnread();

        public Thread TakeArrival()
        {
            Thread arrived = _arrival;
            _arrival = null;
            return arrived;
        }

        public void Tick()
        {
            ulong lobby = ChatTransport.InLobby ? CurrentLobby() : 0UL;

            // Leaving a lobby has to empty the store. Carrying a previous lobby's conversation into the next one
            // would show the player messages from people who are no longer there.
            if (lobby != _lobby)
            {
                _lobby = lobby;
                ChatStore.Clear();
                Contacts.Refresh(lobby);
                _nextRefresh = ContactRefreshSeconds;
                return;
            }

            if (lobby == 0UL) return;

            _nextRefresh -= UnityEngine.Time.deltaTime;
            if (_nextRefresh > 0f) return;

            _nextRefresh = ContactRefreshSeconds;
            Contacts.Refresh(lobby);
        }

        private static ulong CurrentLobby()
        {
            try
            {
                var lobby = Il2CppScheduleOne.DevUtilities.PersistentSingleton<Il2CppScheduleOne.Networking.Lobby>.Instance;
                return lobby != null && lobby.IsInLobby ? lobby.LobbyID : 0UL;
            }
            catch { return 0UL; }
        }

        /// <summary>
        /// A thread id as the interface uses it is a string; the store keys on a SteamID, with zero for the group.
        /// "everyone" is the group, anything else has to parse as an id - a thread for somebody who has left is not
        /// an error, it simply no longer exists.
        /// </summary>
        private static bool TryKey(string threadId, out ulong key)
        {
            key = ChatStore.GroupKey;
            if (string.IsNullOrWhiteSpace(threadId)) return false;
            if (string.Equals(threadId, "everyone", StringComparison.OrdinalIgnoreCase)) return true;

            return ulong.TryParse(threadId.Trim(), out key);
        }

        private void OnIncoming(ChatMessage message)
        {
            ulong active = ChatStore.NoThread;
            ChatStore.Receive(message, active);

            // Which conversation it landed in decides which one lights up. Worked out here rather than in the store,
            // because the store keys on SteamIDs and the rest of the app speaks thread ids.
            ulong self = ChatTransport.SelfId();
            ulong key = message.RecipientId == ChatStore.GroupKey ? ChatStore.GroupKey
                      : (message.SenderId == self ? message.RecipientId : message.SenderId);

            _threadsRevision = -1;
            _arrival = Find(key == ChatStore.GroupKey ? "everyone" : key.ToString());
        }

        /// <summary>
        /// Turn the store and the member list into the threads the interface reads. Rebuilt only when something
        /// changed - the page asks for this on every render, and a lobby of eight would otherwise rebuild eight
        /// conversations sixty times a second for nothing.
        /// </summary>
        private void Rebuild()
        {
            if (_threadsRevision == ChatStore.Revision) return;
            _threadsRevision = ChatStore.Revision;

            _threads.Clear();
            _threads.Add(BuildThread("everyone", "Everyone", true, ChatStore.GroupKey));

            foreach (Contact contact in Contacts.All)
                _threads.Add(BuildThread(contact.SteamId.ToString(), contact.Name, false, contact.SteamId));
        }

        private static Thread BuildThread(string id, string name, bool group, ulong key)
        {
            var thread = new Thread { Id = id, Name = name, Group = group, Unread = ChatStore.Unread(key) };

            foreach (ChatMessage m in ChatStore.Thread(key))
            {
                thread.Messages.Add(new Message
                {
                    From = m.Mine ? "You" : Contacts.NameOf(m.SenderId),
                    Text = m.Text,
                    At = DateTimeOffset.FromUnixTimeSeconds(m.UnixSeconds).ToLocalTime().DateTime,
                    Mine = m.Mine,
                    Pending = m.Pending,
                });
            }

            return thread;
        }
    }
}
