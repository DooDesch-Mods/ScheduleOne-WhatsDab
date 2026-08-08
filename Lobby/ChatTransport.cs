using System;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Networking;
using Il2CppSteamworks;

namespace WhatsDab.Lobby
{
    /// <summary>
    /// Steam lobby chat transport for the Messenger. Sends/receives through the SAME mechanism the vanilla game
    /// uses for its own lobby control messages, so it works in any lobby the player is part of (gamemode,
    /// published vanilla, plain co-op) with no FishNet coupling. 1:1 is filter-level privacy - the bytes reach
    /// every member, but non-recipients drop them - which is fine for game chat (documented). A basic rate limit
    /// guards against floods. The Callback is held in a static field (a GC'd Callback silently stops firing).
    /// </summary>
    internal static class ChatTransport
    {
        private const int MinSendIntervalMs = 300;
        private static Callback<LobbyChatMsg_t> _callback;
        private static Callback<LobbyEnter_t> _enterCallback;
        private static Callback<LobbyCreated_t> _createdCallback;
        private static Action<ChatMessage> _onMessage;
        private static long _lastSendTicks;
        private static ulong _seenLobby;
        private static bool _loggedDeadProperty;

        internal static void Start(Action<ChatMessage> onMessage)
        {
            _onMessage = onMessage;
            if (_callback != null) return;
            try { _callback = Callback<LobbyChatMsg_t>.Create((Callback<LobbyChatMsg_t>.DispatchDelegate)OnChatMsg); }
            catch (Exception e) { Core.Log?.Error("chat callback registration failed: " + e); }
            // Our own record of which lobby we are in, so the transport does not depend on any single vanilla member
            // staying alive across game updates - which is exactly how it broke once (see CurrentLobby).
            try
            {
                _enterCallback = Callback<LobbyEnter_t>.Create((Callback<LobbyEnter_t>.DispatchDelegate)OnLobbyEnter);
                _createdCallback = Callback<LobbyCreated_t>.Create((Callback<LobbyCreated_t>.DispatchDelegate)OnLobbyCreated);
            }
            catch (Exception e) { Core.Log?.Warning("lobby callbacks failed: " + e.Message); }
        }

        private static void OnLobbyEnter(LobbyEnter_t e)
        {
            if (e.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                _seenLobby = e.m_ulSteamIDLobby;
        }

        private static void OnLobbyCreated(LobbyCreated_t e)
        {
            if (e.m_eResult == EResult.k_EResultOK) _seenLobby = e.m_ulSteamIDLobby;
        }

        internal static ulong SelfId()
        {
            try { return SteamUser.GetSteamID().m_SteamID; } catch { return 0UL; }
        }

        /// <summary>
        /// The lobby to send into, or 0 when there is none.
        ///
        /// NOT <c>Lobby.LobbyID</c>. That property still exists and still compiles, but since 0.4.6 nothing assigns it:
        /// the class was refactored onto an <c>ILobbyService</c> and the property was left behind (0.4.5f2 set it in the
        /// class's own Steam callbacks). It therefore read 0 in every lobby, and the messenger stopped sending without a
        /// single error - the send path returns before it reaches its try block.
        ///
        /// The id now comes from where the game actually keeps it, <c>SteamLobbyService._lobbyID</c>, with our own
        /// callback record as the fallback so the next refactor of that service cannot silently repeat this.
        /// </summary>
        private static ulong CurrentLobby()
        {
            ulong fromGame = FromLobbyService();
            if (fromGame != 0UL) return fromGame;
            if (_seenLobby != 0UL && !_loggedDeadProperty)
            {
                _loggedDeadProperty = true;
                Core.Log?.Warning("the game's lobby service gave no id; using our own record " + _seenLobby +
                                  " (SteamLobbyService may have changed again).");
            }
            return _seenLobby;
        }

        private static ulong FromLobbyService()
        {
            // Fully qualified: this mod's own namespace is called Lobby too, and the short name resolves to it.
            try
            {
                var l = PersistentSingleton<Il2CppScheduleOne.Networking.Lobby>.Instance;
                if (l == null || !l.IsInLobby) return 0UL;
                var svc = l._lobbyService;   // interop exposes the private field; IsInLobby above already reads it
                if (svc == null) return 0UL;
                // TryCast, never `as`: a managed cast does not see the Il2Cpp type and returns null.
                var steam = svc.TryCast<SteamLobbyService>();
                return steam != null ? steam._lobbyID : 0UL;
            }
            catch { return 0UL; }
        }

        internal static bool InLobby => CurrentLobby() != 0UL;

        /// <summary>The lobby everything in this mod must agree on. Public because LobbySource used to run its own
        /// copy of the lookup - and kept the broken one after this file was fixed, which left the contact list empty
        /// and took every private conversation with it while the group chat worked.</summary>
        internal static ulong LobbyId => CurrentLobby();

        /// <summary>Send a message (recipient 0 = group). Returns the seq used, or -1 when it could not send.</summary>
        internal static int Send(ulong recipientId, int seq, string text)
        {
            ulong lobby = CurrentLobby();
            if (lobby == 0UL) return -1;
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastSendTicks < MinSendIntervalMs * TimeSpan.TicksPerMillisecond) return -1;
            _lastSendTicks = now;
            try
            {
                long unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                byte[] bytes = ChatEnvelope.Encode(recipientId, seq, unix, text);
                bool ok = SteamMatchmaking.SendLobbyChatMsg(new CSteamID(lobby), bytes, bytes.Length);
                return ok ? seq : -1;
            }
            catch (Exception e) { Core.Log?.Warning("send failed: " + e.Message); return -1; }
        }

        private static void OnChatMsg(LobbyChatMsg_t msg)
        {
            try
            {
                var lobby = new CSteamID(msg.m_ulSteamIDLobby);
                // The buffer MUST be an Il2CppStructArray: a managed byte[] gets copied at the interop boundary,
                // so Steam would fill a throwaway copy and the managed array stays empty.
                var buf = new Il2CppStructArray<byte>(4096);
                int len = SteamMatchmaking.GetLobbyChatEntry(lobby, (int)msg.m_iChatID,
                    out CSteamID sender, buf, (int)buf.Length, out _);
                int n = Math.Max(0, Math.Min(len, (int)buf.Length));
                if (n == 0) return;
                var managed = new byte[n];
                for (int i = 0; i < n; i++) managed[i] = buf[i];
                string raw = Encoding.ASCII.GetString(managed).TrimEnd('\0');
                if (!ChatEnvelope.IsOurs(raw)) return;   // a vanilla control message - leave it alone

                var decoded = ChatEnvelope.Decode(raw, sender.m_SteamID);
                if (decoded == null) return;
                // Drop 1:1 messages not addressed to us (filter-level privacy).
                if (decoded.RecipientId != 0UL && decoded.RecipientId != SelfId() && decoded.SenderId != SelfId()) return;
                _onMessage?.Invoke(decoded);
            }
            catch (Exception e) { Core.Log?.Warning("receive failed: " + e.Message); }
        }
    }
}
