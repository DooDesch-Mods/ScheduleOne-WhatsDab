using Sideload.Api;

namespace WhatsDab.Chat
{
    /// <summary>
    /// Everything the page can ask for, and everything the mod pushes at it. This is the entire seam between C# and
    /// the interface: six calls in, one event out, JSON strings both ways.
    ///
    /// Nothing here knows what the app looks like. That is the property worth having - the layout can be rewritten in
    /// app.css without recompiling, and this file would not notice. It also means every user-facing WORD lives in the
    /// page: this side reports that there are no peers, never that "you're the only one here so far".
    /// </summary>
    internal static class ChatBackend
    {
        /// <summary>
        /// Longest message the wire may carry. The compose field declares the same number as its maxlength, so a
        /// player cannot type past it; this is the guard for everything else, because the web bundle can be replaced
        /// wholesale from the Mods folder and a handler that trusts its caller is not a guard at all.
        /// </summary>
        internal const int MaxMessageChars = 500;

        private static AppHandle _app;
        private static IChatSource _source;
        private static int _pushedRevision = -1;

        /// <summary>Where the conversations come from. The real one carries messages between players; the demo makes
        /// the app worth opening with no lobby running. Both answer exactly the calls below.</summary>
        internal static IChatSource Source => _source;

        /// <summary>
        /// Point the handlers at a source. Separate from <see cref="Install"/> because the headless tests exercise
        /// the handlers directly and have no app to register them on - the seam under test is the source, not the
        /// registration.
        /// </summary>
        internal static void UseSource(IChatSource source)
        {
            _source = source;
            _pushedRevision = -1;
        }

        internal static void Install(AppHandle app, IChatSource source)
        {
            _app = app;
            UseSource(source);

            app.OnCall("chat.threads", _ => Threads())
               .OnCall("chat.thread", Conversation)
               .OnCall("chat.send", Send)
               .OnCall("chat.read", id => { _source.MarkRead(id); return "ok"; })
               .OnCall("chat.self", _ => _source.Self)
               .OnCall("chat.status", _ => Status());
        }

        /// <summary>Called once a frame by the mod. Advances the source and tells the page when to refresh - on a
        /// revision change only, so an idle app costs one integer comparison.</summary>
        internal static void Tick()
        {
            if (_source == null) return;
            _source.Tick();

            if (_source.Revision == _pushedRevision) return;
            _pushedRevision = _source.Revision;

            // One event, no payload worth the name: the page decides what it needs and asks for it. Pushing the whole
            // state would mean serialising every thread on every keystroke-sized change.
            int unread = _source.TotalUnread();
            _app?.Emit("chat.changed", unread.ToString());

            // The badge is the count a player sees without opening anything, so it follows the same one number the
            // app's own header shows. Set on every change rather than tracked: the framework remembers it across a
            // phone rebuild, and re-setting the same value costs nothing.
            _app?.Badge(unread);

            NotifyIfUnseen();
        }

        /// <summary>
        /// Interrupt the player only for a message they are not already watching arrive. WhatsDab being on screen is
        /// enough to stay quiet: they are looking at the app, so the thread list showing a new row is the whole
        /// notification. With the phone in their pocket, nothing else would tell them.
        /// </summary>
        private static void NotifyIfUnseen()
        {
            Thread arrived = _source.TakeArrival();
            if (arrived == null) return;

            if (_app == null || _app.IsOnScreen) return;

            Message last = arrived.Messages.Count > 0 ? arrived.Messages[arrived.Messages.Count - 1] : null;
            if (last == null || last.Mine) return;

            // The sender leads, because in a group the thread name says nothing about who wrote.
            _app.Notify(arrived.Group ? $"{last.From} - {arrived.Name}" : last.From, last.Text);
        }

        /// <summary>
        /// Whether chatting is possible at all, and with how many people.
        ///
        /// A real transport must answer <c>online: false</c> whenever the local player is not in a lobby - that is the
        /// whole difference between "no messages yet" and "there is nobody to message", and only the transport knows
        /// which one it is. <c>peers</c> is the number of OTHER lobby members, excluding the local player, so a
        /// freshly hosted lobby reports zero.
        /// </summary>
        internal static string Status() =>
            Json.Object()
                .Add("online", _source.Online)
                .Add("peers", _source.Peers)
                .Close();

        internal static string Threads()
        {
            Json list = Json.Array();

            foreach (Thread thread in _source.Threads)
            {
                Message last = thread.Messages.Count > 0 ? thread.Messages[thread.Messages.Count - 1] : null;

                list.Item(Json.Object()
                    .Add("id", thread.Id)
                    .Add("name", thread.Name)
                    .Add("group", thread.Group)
                    .Add("unread", thread.Unread)
                    // Whether someone is typing belongs in the list too, not just in the open conversation: it is the
                    // reason to switch threads, and you cannot see it from inside a different one.
                    .Add("typing", string.Equals(_source.TypingIn, thread.Id, StringComparison.OrdinalIgnoreCase))
                    // Empty means empty. What a conversation nobody has spoken in should SAY is a different question
                    // for a group than for one person, and the answer is a sentence - so the page owns it and this
                    // side just declines to invent one.
                    .Add("preview", last == null ? ""
                                                : (last.Mine ? "You: " : thread.Group ? last.From + ": " : "") + last.Text)
                    .Add("time", last == null ? "" : ChatModel.Clock(last.At)));
            }

            return list.Close();
        }

        internal static string Conversation(string id)
        {
            Thread thread = _source.Find(id);
            if (thread == null) return Json.Object().Add("id", "").Add("name", "").Close();

            Json messages = Json.Array();
            string previousDay = null;

            foreach (Message message in thread.Messages)
            {
                // The day separator is a property of the SEQUENCE, so it belongs here rather than in the page: the
                // page would have to re-derive it from timestamps it does not otherwise care about.
                string day = ChatModel.DayLabel(message.At);
                if (day != previousDay)
                {
                    messages.Item(Json.Object().Add("kind", "day").Add("text", day));
                    previousDay = day;
                }

                messages.Item(Json.Object()
                    .Add("kind", "message")
                    .Add("from", message.From)
                    .Add("text", message.Text)
                    .Add("time", ChatModel.Clock(message.At))
                    .Add("mine", message.Mine)
                    .Add("pending", message.Pending));
            }

            return Json.Object()
                .Add("id", thread.Id)
                .Add("name", thread.Name)
                .Add("group", thread.Group)
                .Add("typing", string.Equals(_source.TypingIn, thread.Id, StringComparison.OrdinalIgnoreCase))
                .Add("messages", messages)
                .Close();
        }

        /// <summary>
        /// The page sends <c>id\ntext</c>. A newline is the one character a message may not contain, so the inbound
        /// direction needs no JSON - but that only holds if a second newline is REJECTED rather than folded into the
        /// text, and if the whole request is refused before anything is scheduled.
        /// </summary>
        internal static string Send(string argument)
        {
            // Nothing goes out without a transport. The page hides the compose bar behind the offline screen, so this
            // is unreachable from the shipped interface - which is exactly why it belongs here: the interface is a
            // folder of web files anybody can replace, and a handler that relies on its caller having hidden a button
            // is relying on the one part of the app that is not under its control.
            if (!_source.Online) return "error";

            string raw = argument ?? "";

            int split = raw.IndexOf('\n');
            if (split <= 0) return "error";                      // no id, or no separator at all

            string id = raw.Substring(0, split);
            string text = raw.Substring(split + 1);

            if (text.IndexOf('\n') >= 0) return "error";         // the wire format cannot carry it
            if (string.IsNullOrWhiteSpace(text)) return "error";

            // Refused rather than truncated. Cutting a message in half and sending it anyway loses words the player
            // wrote and cannot get back; refusing leaves the text in the compose field, where they can still edit it.
            if (text.Trim().Length > MaxMessageChars) return "error";

            Thread thread = _source.Find(id);
            if (thread == null) return "error";

            // The CANONICAL id, so a differently cased request still lands in the same conversation. A refusal here
            // is the transport saying no - rate limited, or the lobby gone since the page last asked - and the page
            // keeps the words in the field rather than losing them.
            return _source.Send(thread.Id, text) ? "ok" : "error";
        }
    }
}
