namespace WhatsDab.Chat
{
    /// <summary>
    /// The scripted conversation: a contact starts typing shortly after you send, and answers shortly after that.
    ///
    /// Kept, and not only for the tests. It is what makes the app worth opening with no lobby running - the whole
    /// interface, the layouts, the unread counting and the notifications are all exercisable from a single-player
    /// save, which is the difference between a five-second check and a co-op session.
    /// </summary>
    internal sealed class DemoSource : IChatSource
    {
        // The demo's "other side": a contact starts typing shortly after you send, and answers shortly after that.
        private DateTime _confirmAt = DateTime.MaxValue;
        private DateTime _typingAt = DateTime.MaxValue;
        private DateTime _replyAt = DateTime.MaxValue;
        private string _replyTo;

        internal DemoSource()
        {
            ChatModel.Seed();

            // Constructing means "start over". A reply left scheduled from a previous instance would fire into the
            // fresh model and put a message into a conversation nobody sent one to.
            _confirmAt = _typingAt = _replyAt = DateTime.MaxValue;
            _replyTo = null;
        }

        public string Self => ChatModel.Self;

        public int Revision => ChatModel.Revision;

        public bool Online => ChatModel.Online;

        public int Peers => ChatModel.Peers;

        public IReadOnlyList<Thread> Threads => ChatModel.Threads;

        public Thread Find(string id) => ChatModel.Find(id);

        public string TypingIn => ChatModel.TypingIn;

        public void MarkRead(string threadId) => ChatModel.MarkRead(threadId);

        public int TotalUnread() => ChatModel.TotalUnread();

        public Thread TakeArrival()
        {
            Thread arrived = ChatModel.LastArrival;
            ChatModel.LastArrival = null;
            return arrived;
        }

        public bool Send(string threadId, string text)
        {
            Thread thread = ChatModel.Find(threadId);
            if (thread == null) return false;

            ChatModel.Send(thread.Id, text);

            // A real transport confirms when the wire says so; here a short delay stands in for the round trip, which
            // is what makes the pending tick visible at all. Scheduled against the CANONICAL id, so a differently
            // cased request still answers into the same conversation.
            _confirmAt = DateTime.Now.AddSeconds(0.9);
            _typingAt = DateTime.Now.AddSeconds(1.8);
            _replyAt = DateTime.Now.AddSeconds(4.0);
            _replyTo = thread.Id;
            return true;
        }

        public void Tick()
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
        }
    }
}
