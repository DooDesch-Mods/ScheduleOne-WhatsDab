namespace WhatsDab.Chat
{
    /// <summary>
    /// Where conversations come from. Two implementations answer this: the scripted demo, and the real one that
    /// carries messages between players over the lobby.
    ///
    /// It exists because of a hard constraint rather than a taste for abstraction. The headless test suite compiles
    /// <c>Chat/</c> WITHOUT any Unity or Steam reference - that is what makes it run in a second and what catches an
    /// accidental engine dependency before a game launch. So the real transport cannot live in this folder, and this
    /// interface is the seam it reaches the rest of the app through.
    ///
    /// Everything here is called on the Unity main thread.
    /// </summary>
    internal interface IChatSource
    {
        /// <summary>The local player's display name, as it should read above their own messages.</summary>
        string Self { get; }

        /// <summary>Bumped whenever anything at all changed, so the app can push one event instead of polling.</summary>
        int Revision { get; }

        /// <summary>Whether chatting is possible at all. False means "there is nobody to message", which is a
        /// different screen from "no messages yet".</summary>
        bool Online { get; }

        /// <summary>How many OTHER people are reachable. Zero in a lobby of one.</summary>
        int Peers { get; }

        /// <summary>Every conversation, group first.</summary>
        IReadOnlyList<Thread> Threads { get; }

        /// <summary>The thread whose id this is, or null.</summary>
        Thread Find(string id);

        /// <summary>Who is typing right now, or null. Only one at a time, which is all the interface shows.</summary>
        string TypingIn { get; }

        /// <summary>
        /// Send a message. Returns false when it was refused - no transport, rate limited, or a text the wire cannot
        /// carry - and the app then leaves the words in the field rather than losing them.
        /// </summary>
        bool Send(string threadId, string text);

        /// <summary>The player is looking at this conversation, so it has no unread messages.</summary>
        void MarkRead(string threadId);

        /// <summary>Unread across every conversation - the number on the app icon and in the header.</summary>
        int TotalUnread();

        /// <summary>
        /// The conversation a message most recently landed in, so the mod can decide whether it is worth a
        /// notification. Reading it clears it: it is a one-shot signal, not a state.
        /// </summary>
        Thread TakeArrival();

        /// <summary>Advance whatever the source needs advancing. Called once a frame.</summary>
        void Tick();
    }
}
