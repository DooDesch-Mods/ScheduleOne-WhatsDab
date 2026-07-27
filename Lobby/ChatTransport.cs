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
        private static Action<ChatMessage> _onMessage;
        private static long _lastSendTicks;

        internal static void Start(Action<ChatMessage> onMessage)
        {
            _onMessage = onMessage;
            if (_callback != null) return;
            try { _callback = Callback<LobbyChatMsg_t>.Create((Callback<LobbyChatMsg_t>.DispatchDelegate)OnChatMsg); }
            catch (Exception e) { Core.Log?.Error("[WhatsDab] chat callback registration failed: " + e); }
        }

        internal static ulong SelfId()
        {
            try { return SteamUser.GetSteamID().m_SteamID; } catch { return 0UL; }
        }

        private static ulong CurrentLobby()
        {
            // Fully qualified: this mod's own namespace is called Lobby too, and the short name resolves to it.
            try { var l = PersistentSingleton<Il2CppScheduleOne.Networking.Lobby>.Instance; return l != null && l.IsInLobby ? l.LobbyID : 0UL; }
            catch { return 0UL; }
        }

        internal static bool InLobby => CurrentLobby() != 0UL;

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
            catch (Exception e) { Core.Log?.Warning("[WhatsDab] send failed: " + e.Message); return -1; }
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
            catch (Exception e) { Core.Log?.Warning("[WhatsDab] receive failed: " + e.Message); }
        }
    }
}
