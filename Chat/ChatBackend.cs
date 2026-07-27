using Sideload.Api;

namespace WhatsDab.Chat
{
    /// <summary>
    /// Everything the page can ask for, and everything the mod pushes at it. This is the entire seam between C# and
    /// the interface: five calls in, two events out, JSON strings both ways.
    ///
    /// Nothing here knows what the app looks like. That is the property worth having - the layout can be rewritten in
    /// app.css without recompiling, and this file would not notice.
    /// </summary>
    internal static class ChatBackend
    {
        private static AppHandle _app;
        private static int _pushedRevision = -1;

        // The demo's "other side": a contact starts typing shortly after you send, and answers shortly after that.
        private static DateTime _confirmAt = DateTime.MaxValue;
        private static DateTime _typingAt = DateTime.MaxValue;
        private static DateTime _replyAt = DateTime.MaxValue;
        private static string _replyTo;

        internal static void Install(AppHandle app)
        {
            _app = app;
            ChatModel.Seed();

            // Install means "start over". A reply left scheduled from a previous install would fire into the fresh
            // model and put a message into a conversation nobody sent one to.
            _confirmAt = _typingAt = _replyAt = DateTime.MaxValue;
            _replyTo = null;
            _pushedRevision = -1;

            app.OnCall("chat.threads", _ => Threads())
               .OnCall("chat.thread", Conversation)
               .OnCall("chat.send", Send)
               .OnCall("chat.read", id => { ChatModel.MarkRead(id); return "ok"; })
               .OnCall("chat.self", _ => ChatModel.Self);
        }

        /// <summary>Called once a frame by the mod. Drives the scripted replies and tells the page when to refresh -
        /// on a revision change only, so an idle app costs one integer comparison.</summary>
        internal static void Tick()
        {
            DateTime now = DateTime.Now;

            if (now >= _confirmAt)
            {
                _confirmAt = DateTime.MaxValue;
                ChatModel.ConfirmPending();
            }

            if (now >= _typingAt)
            {
                _typingAt = DateTime.MaxValue;
                ChatModel.SetTyping(_replyTo);
            }

            if (now >= _replyAt)
            {
                _replyAt = DateTime.MaxValue;
                ChatModel.Reply(_replyTo);
            }

            if (ChatModel.Revision == _pushedRevision) return;
            _pushedRevision = ChatModel.Revision;

            // One event, no payload worth the name: the page decides what it needs and asks for it. Pushing the whole
            // state would mean serialising every thread on every keystroke-sized change.
            _app?.Emit("chat.changed", ChatModel.TotalUnread().ToString());
        }

        internal static string Threads()
        {
            Json list = Json.Array();

            foreach (Thread thread in ChatModel.Threads)
            {
                Message last = thread.Messages.Count > 0 ? thread.Messages[thread.Messages.Count - 1] : null;

                list.Item(Json.Object()
                    .Add("id", thread.Id)
                    .Add("name", thread.Name)
                    .Add("group", thread.Group)
                    .Add("unread", thread.Unread)
                    // Whether someone is typing belongs in the list too, not just in the open conversation: it is the
                    // reason to switch threads, and you cannot see it from inside a different one.
                    .Add("typing", string.Equals(ChatModel.TypingIn, thread.Id, StringComparison.OrdinalIgnoreCase))
                    .Add("preview", last == null ? "No messages yet"
                                                : (last.Mine ? "You: " : thread.Group ? last.From + ": " : "") + last.Text)
                    .Add("time", last == null ? "" : ChatModel.Clock(last.At)));
            }

            return list.Close();
        }

        internal static string Conversation(string id)
        {
            Thread thread = ChatModel.Find(id);
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
                .Add("typing", string.Equals(ChatModel.TypingIn, thread.Id, StringComparison.OrdinalIgnoreCase))
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
            string raw = argument ?? "";

            int split = raw.IndexOf('\n');
            if (split <= 0) return "error";                      // no id, or no separator at all

            string id = raw.Substring(0, split);
            string text = raw.Substring(split + 1);

            if (text.IndexOf('\n') >= 0) return "error";         // the wire format cannot carry it
            if (string.IsNullOrWhiteSpace(text)) return "error";

            Thread thread = ChatModel.Find(id);
            if (thread == null) return "error";

            ChatModel.Send(thread.Id, text);

            // A real transport confirms when the wire says so; here a short delay stands in for the round trip, which
            // is what makes the pending tick visible at all. Scheduled against the CANONICAL id, so a differently
            // cased request still answers into the same conversation.
            _confirmAt = DateTime.Now.AddSeconds(0.9);
            _typingAt = DateTime.Now.AddSeconds(1.8);
            _replyAt = DateTime.Now.AddSeconds(4.0);
            _replyTo = thread.Id;

            return "ok";
        }
    }
}
