using System.Globalization;

namespace WhatsDab.Chat
{
    internal sealed class Message
    {
        internal string From = "";
        internal string Text = "";
        internal DateTime At;
        internal bool Mine;
        internal bool Pending;
    }

    internal sealed class Thread
    {
        internal string Id = "";
        internal string Name = "";
        internal bool Group;
        internal int Unread;
        internal readonly List<Message> Messages = new List<Message>();
    }

    /// <summary>
    /// The example's chat state. Entirely in memory and entirely local - a demo of the shape a real mod's model
    /// takes, not a network client. Swapping this for a live transport (Side Hustle's messenger sends over Steam
    /// lobby chat) touches only this file: the page talks to <see cref="ChatBackend"/>, never to a data structure.
    /// </summary>
    internal static class ChatModel
    {
        internal const string GroupId = "everyone";

        private static readonly string[] _replies =
        {
            "on my way",
            "give me two minutes, dealing with a customer",
            "did you restock the west van?",
            "cops were on Marina Ave, take the back road",
            "that batch came out at 82%, best yet",
            "no way. seriously?",
            "sending the cash over now",
            "can you cover the motel drop tonight?",
        };

        private static readonly List<Thread> _threads = new List<Thread>();
        private static int _replyIndex;

        internal static string Self { get; private set; } = "You";

        internal static IReadOnlyList<Thread> Threads => _threads;

        /// <summary>Bumped on every change, so the page can ignore an event that tells it nothing new.</summary>
        internal static int Revision { get; private set; }

        /// <summary>
        /// Whether there is anyone to talk to at all. A chat app with no transport behind it is not an empty chat app,
        /// it is a different screen - so this is state the page has to be able to read, not something it infers from
        /// an empty thread list.
        ///
        /// The demo has no transport and is therefore always online. A real one reports whether the local player is in
        /// a lobby.
        /// </summary>
        internal static bool Online { get; private set; } = true;

        /// <summary>How many other people are reachable. One thread per person, so this is the thread list minus the
        /// group - but the page is told the number rather than counting rows, because "nobody else is here" is a fact
        /// about the lobby and not about how many rows survived a search filter.</summary>
        internal static int Peers
        {
            get
            {
                int peers = 0;
                foreach (Thread thread in _threads) if (!thread.Group) peers++;
                return peers;
            }
        }

        internal static void SetOnline(bool online)
        {
            if (Online == online) return;

            Online = online;
            Revision++;
        }

        /// <summary>Thread whose contact is currently "typing", or null. Purely cosmetic and purely local.</summary>
        internal static string TypingIn { get; private set; }

        internal static Thread Find(string id)
        {
            foreach (Thread t in _threads)
                if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        internal static void Seed()
        {
            _threads.Clear();

            // A seeded model is a FRESH model: transient state and the scripted reply cursor go with the data, or a
            // reload leaves a typing indicator pointing at a thread that no longer exists. Connectivity is part of
            // that - seeding four conversations while the model still claims to be offline would draw the offline
            // screen over data that is demonstrably there.
            TypingIn = null;
            _replyIndex = 0;
            Online = true;

            var everyone = new Thread { Id = GroupId, Name = "Everyone", Group = true };
            Add(everyone, "Mick", "anyone seen my van keys", -46);
            Add(everyone, "Jessi", "they were in the ignition. again.", -44);
            Add(everyone, "Benji", "we are meeting at the docks at nine", -12);
            everyone.Unread = 1;
            _threads.Add(everyone);

            var mick = new Thread { Id = "mick", Name = "Mick Lubbin" };
            Add(mick, "Mick Lubbin", "got the package?", -180);
            Add(mick, Self, "yeah, dropped it off an hour ago", -176, mine: true);
            Add(mick, "Mick Lubbin", "you are a legend", -175);
            _threads.Add(mick);

            var jessi = new Thread { Id = "jessi", Name = "Jessi Waters", Unread = 2 };
            Add(jessi, "Jessi Waters", "that new strain is unreal", -9);
            Add(jessi, "Jessi Waters", "customers are asking for it by name now", -8);
            _threads.Add(jessi);

            var benji = new Thread { Id = "benji", Name = "Benji Coleman" };
            Add(benji, Self, "warehouse is full, we need a second one", -320, mine: true);
            Add(benji, "Benji Coleman", "i know a guy who knows a guy", -318);
            _threads.Add(benji);

            Revision++;
        }

        internal static void Send(string threadId, string text)
        {
            Thread thread = Find(threadId);
            if (thread == null || string.IsNullOrWhiteSpace(text)) return;

            thread.Messages.Add(new Message
            {
                From = Self,
                Text = text.Trim(),
                At = DateTime.Now,
                Mine = true,
                Pending = true,
            });

            Revision++;
        }

        /// <summary>The optimistic echo turns into a delivered message, exactly as a real transport would confirm it.</summary>
        internal static bool ConfirmPending()
        {
            bool changed = false;
            foreach (Thread thread in _threads)
                foreach (Message message in thread.Messages)
                    if (message.Pending) { message.Pending = false; changed = true; }

            if (changed) Revision++;
            return changed;
        }

        /// <summary>
        /// Mark a contact as typing. The id is canonicalised through <see cref="Find"/> first: every other lookup is
        /// case-insensitive, and storing the caller's spelling would make `SetTyping("JESSI")` invisible to a
        /// conversation that compares against `"jessi"`.
        /// </summary>
        internal static void SetTyping(string threadId)
        {
            string canonical = Find(threadId)?.Id;
            if (TypingIn == canonical) return;

            TypingIn = canonical;
            Revision++;
        }

        internal static void Reply(string threadId)
        {
            // Validate BEFORE touching anything: a reply scheduled for a thread that has since gone away must be a
            // true no-op, not a silent clearing of an indicator that belongs to a different conversation.
            Thread thread = Find(threadId);
            if (thread == null) return;

            TypingIn = null;

            string author = thread.Group ? "Benji Coleman" : thread.Name;
            thread.Messages.Add(new Message
            {
                From = author,
                Text = _replies[_replyIndex++ % _replies.Length],
                At = DateTime.Now,
            });

            thread.Unread++;
            Revision++;
        }

        internal static void MarkRead(string threadId)
        {
            Thread thread = Find(threadId);
            if (thread == null || thread.Unread == 0) return;

            thread.Unread = 0;
            Revision++;
        }

        internal static int TotalUnread()
        {
            int total = 0;
            foreach (Thread thread in _threads) total += thread.Unread;
            return total;
        }

        internal static string Clock(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

        internal static string DayLabel(DateTime at) => DayLabel(at, DateTime.Now);

        /// <summary>The reference day is a parameter so the boundary cases are testable; reading the clock inside
        /// would make "Today" untestable and flaky across midnight.</summary>
        internal static string DayLabel(DateTime at, DateTime now)
        {
            DateTime today = now.Date;
            if (at.Date == today) return "Today";
            if (at.Date == today.AddDays(-1)) return "Yesterday";
            return at.ToString("d MMMM", CultureInfo.InvariantCulture);
        }

        private static void Add(Thread thread, string from, string text, int minutesAgo, bool mine = false)
        {
            thread.Messages.Add(new Message
            {
                From = from,
                Text = text,
                At = DateTime.Now.AddMinutes(minutesAgo),
                Mine = mine,
            });
        }
    }
}
