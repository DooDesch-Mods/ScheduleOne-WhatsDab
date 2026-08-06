using System;
using System.Collections.Generic;

namespace WhatsDab.Lobby
{
    /// <summary>
    /// In-memory conversation store (session-only by design: persisting logs would undermine the alias privacy story). One thread per peer SteamID, plus the group thread under key 0. Tracks unread counts and
    /// reconciles the local optimistic echo when Steam bounces the message back. Built so a later JSON persist
    /// would be a pure add-on.
    /// </summary>
    internal static class ChatStore
    {
        internal const ulong GroupKey = 0UL;

        /// <summary>Sentinel for "no thread is being viewed" (app closed or on the contact list). Never a real key
        /// (keys are the group key 0 or a SteamID), so with this active EVERY incoming message - lobby chat included
        /// - counts as unread and alerts.</summary>
        internal const ulong NoThread = ulong.MaxValue;

        private static readonly Dictionary<ulong, List<ChatMessage>> _threads = new Dictionary<ulong, List<ChatMessage>>();
        private static readonly Dictionary<ulong, int> _unread = new Dictionary<ulong, int>();

        /// <summary>Bumped whenever anything changes, so the UI can rebuild only on a real change.</summary>
        internal static int Revision { get; private set; }

        internal static IReadOnlyList<ChatMessage> Thread(ulong peerKey) =>
            _threads.TryGetValue(peerKey, out var list) ? list : Array.Empty<ChatMessage>();

        internal static int Unread(ulong peerKey) => _unread.TryGetValue(peerKey, out var n) ? n : 0;

        internal static int TotalUnread()
        {
            int t = 0;
            foreach (var kv in _unread) t += kv.Value;
            return t;
        }

        /// <summary>The thread key a message belongs to: the group key for group messages, otherwise the OTHER
        /// party (so my private message to X and X's reply share one thread keyed by X).</summary>
        private static ulong KeyFor(ChatMessage m, ulong self) =>
            m.RecipientId == GroupKey ? GroupKey : (m.SenderId == self ? m.RecipientId : m.SenderId);

        /// <summary>Optimistically append a locally-authored message (pending until Steam echoes it back).</summary>
        internal static ChatMessage AddLocal(ulong recipientKey, int seq, string text)
        {
            ulong self = ChatTransport.SelfId();
            var m = new ChatMessage
            {
                SenderId = self,
                RecipientId = recipientKey,
                Seq = seq,
                UnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Text = text,
                Mine = true,
                Pending = true,
            };
            List(recipientKey).Add(m);
            Revision++;
            return m;
        }

        /// <summary>Handle an incoming (or echoed) message from the transport.</summary>
        internal static void Receive(ChatMessage m, ulong activeThreadKey)
        {
            ulong self = ChatTransport.SelfId();
            ulong key = KeyFor(m, self);

            if (m.SenderId == self)
            {
                // Our own message bounced back: clear the pending flag on the matching optimistic entry.
                var list = List(key);
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].Mine && list[i].Pending && list[i].Seq == m.Seq) { list[i].Pending = false; Revision++; return; }
                // No local echo (e.g. reconnected): add it as sent.
                m.Mine = true; m.Pending = false;
                list.Add(m);
                Revision++;
                return;
            }

            List(key).Add(m);
            if (key != activeThreadKey) _unread[key] = Unread(key) + 1;
            Revision++;
        }

        internal static void MarkRead(ulong peerKey)
        {
            if (_unread.TryGetValue(peerKey, out var n) && n > 0) { _unread[peerKey] = 0; Revision++; }
        }

        internal static void Clear()
        {
            _threads.Clear();
            _unread.Clear();
            Revision++;
        }

#if DEBUG
        /// <summary>Test seam only: seed a group thread + one private thread + an unread count, for a screenshot.</summary>
        internal static void SeedForTest(ulong peerId)
        {
            Clear();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            void Msg(ulong key, ulong sender, ulong recipient, string text, bool mine)
                => List(key).Add(new ChatMessage { SenderId = sender, RecipientId = recipient, Text = text, Mine = mine, UnixSeconds = now });

            Msg(GroupKey, peerId, GroupKey, "hey, everyone synced up?", false);
            Msg(GroupKey, ChatTransport.SelfId(), GroupKey, "yep, just joined - looks good", true);
            Msg(GroupKey, peerId, GroupKey, "nice, meet at the RV", false);

            Msg(peerId, peerId, ChatTransport.SelfId(), "you got the money?", false);
            Msg(peerId, ChatTransport.SelfId(), peerId, "on my way", true);
            _unread[peerId] = 1;
            Revision++;
        }
#endif

        /// <summary>
        /// How much of a conversation is kept. A thread that grows without end makes every redraw more expensive than
        /// the last one - the page rebuilds the open thread from the whole list - so a spammer degrades the game for
        /// everyone in the lobby, not just for themselves. Two hundred is far more than anyone scrolls back through in
        /// a session, and the oldest lines go first.
        /// </summary>
        private const int MaxMessagesPerThread = 200;

        private static List<ChatMessage> List(ulong key)
        {
            if (!_threads.TryGetValue(key, out var list)) { list = new List<ChatMessage>(); _threads[key] = list; }
            if (list.Count > MaxMessagesPerThread) list.RemoveRange(0, list.Count - MaxMessagesPerThread);
            return list;
        }
    }
}
