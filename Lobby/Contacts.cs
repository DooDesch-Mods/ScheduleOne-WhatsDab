using System;
using System.Collections.Generic;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppSteamworks;

namespace WhatsDab.Lobby
{
    /// <summary>One chat contact = a fellow lobby member.</summary>
    internal sealed class Contact
    {
        public ulong SteamId;
        public string Name = "";
    }

    /// <summary>
    /// The lobby's members as chat contacts. Names are alias-first: the in-game <c>Player.PlayerName</c> (which
    /// another mod may have overridden for privacy) matched by <c>PlayerCode</c> = SteamID, then
    /// the Steam friend/persona name, then a short id. Refreshed on a poll (cheap); the local player is excluded.
    /// </summary>
    internal static class Contacts
    {
        private static readonly List<Contact> _list = new List<Contact>();

        internal static IReadOnlyList<Contact> All => _list;

#if DEBUG
        /// <summary>Test seam only: seed fake contacts for a screenshot (no lobby needed).</summary>
        internal static void SeedForTest(params (ulong Id, string Name)[] contacts)
        {
            _list.Clear();
            foreach (var c in contacts) _list.Add(new Contact { SteamId = c.Id, Name = c.Name });
        }
#endif

        internal static string NameOf(ulong steamId)
        {
            foreach (var c in _list) if (c.SteamId == steamId) return c.Name;
            return ResolveName(steamId);
        }

        internal static void Refresh(ulong lobbyId)
        {
            _list.Clear();
            if (lobbyId == 0UL) return;
            ulong self = ChatTransport.SelfId();
            try
            {
                var lobby = new CSteamID(lobbyId);
                int n = SteamMatchmaking.GetNumLobbyMembers(lobby);
                for (int i = 0; i < n; i++)
                {
                    CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                    if (m.m_SteamID == 0UL || m.m_SteamID == self) continue;
                    _list.Add(new Contact { SteamId = m.m_SteamID, Name = ResolveName(m.m_SteamID) });
                }
            }
            catch (Exception e) { Core.Log?.Warning("[WhatsDab] contact refresh failed: " + e.Message); }
        }

        private static string ResolveName(ulong steamId)
        {
            // In-game player name (honours any alias another mod set) by matching the replicated PlayerCode.
            try
            {
                string code = steamId.ToString();
                var players = Player.PlayerList;
                if (players != null)
                    for (int i = 0; i < players.Count; i++)
                    {
                        var p = players[i];
                        if (p == null) continue;
                        string pc = null;
                        try { pc = p.PlayerCode; } catch { }
                        if (pc == code)
                        {
                            string name = null;
                            try { name = p.PlayerName; } catch { }
                            if (!string.IsNullOrEmpty(name)) return name;
                        }
                    }
            }
            catch { }
            // Steam persona (works for friends; GBE returns a name in the test rig).
            try
            {
                string persona = SteamFriends.GetFriendPersonaName(new CSteamID(steamId));
                if (!string.IsNullOrEmpty(persona) && persona != "[unknown]") return persona;
            }
            catch { }
            string s = steamId.ToString();
            return "Player " + (s.Length > 4 ? s.Substring(s.Length - 4) : s);
        }
    }
}
