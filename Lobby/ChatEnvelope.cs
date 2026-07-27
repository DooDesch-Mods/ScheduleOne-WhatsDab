using System;
using System.Text;

namespace WhatsDab.Lobby
{
    /// <summary>One decoded chat message off the wire. The sender is ALWAYS the Steam-authenticated id from the
    /// lobby-chat callback, never a field in the payload (spoof-proof).</summary>
    internal sealed class ChatMessage
    {
        public ulong SenderId;
        public ulong RecipientId;   // 0 = group (everyone); otherwise a specific member's SteamID (1:1)
        public int Seq;
        public long UnixSeconds;
        public string Text = "";
        public bool Mine;           // authored locally (optimistic echo, reconciled when it comes back)
        public bool Pending;        // sent, not yet echoed back by Steam
    }

    /// <summary>
    /// The wire format for WhatsDab over Steam lobby chat: an ASCII envelope that can never collide
    /// with the vanilla lobby control strings ("ready"/"load_tutorial"/"host_loading") the game reacts to, and
    /// carries the text as base64(UTF-8) so umlauts survive. The prefix is WhatsDab's own, so an older mod
    /// speaking a different dialect in the same lobby is ignored rather than misread. Recipient "*" = group, else the member SteamID.
    /// Format: <c>WD1|MSG|&lt;recipient&gt;|&lt;seq&gt;|&lt;unix&gt;|&lt;base64&gt;</c>.
    /// </summary>
    internal static class ChatEnvelope
    {
        private const string Prefix = "WD1|MSG|";
        internal const int MaxTextChars = 500;

        internal static bool IsOurs(string raw) => raw != null && raw.StartsWith(Prefix, StringComparison.Ordinal);

        internal static byte[] Encode(ulong recipientId, int seq, long unix, string text)
        {
            string t = text ?? "";
            if (t.Length > MaxTextChars) t = t.Substring(0, MaxTextChars);
            string recipient = recipientId == 0 ? "*" : recipientId.ToString();
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(t));
            string envelope = Prefix + recipient + "|" + seq + "|" + unix + "|" + b64 + "\0";
            return Encoding.ASCII.GetBytes(envelope);
        }

        /// <summary>Parse a received envelope; null when it is not ours or is malformed. <paramref name="senderId"/>
        /// comes from the Steam callback, not the payload.</summary>
        internal static ChatMessage Decode(string raw, ulong senderId)
        {
            if (!IsOurs(raw)) return null;
            try
            {
                var parts = raw.Split('|');
                if (parts.Length != 6) return null;
                var msg = new ChatMessage { SenderId = senderId };
                msg.RecipientId = parts[2] == "*" ? 0UL : (ulong.TryParse(parts[2], out var r) ? r : 0UL);
                int.TryParse(parts[3], out msg.Seq);
                long.TryParse(parts[4], out msg.UnixSeconds);
                msg.Text = Encoding.UTF8.GetString(Convert.FromBase64String(parts[5]));
                return msg;
            }
            catch { return null; }
        }
    }
}
