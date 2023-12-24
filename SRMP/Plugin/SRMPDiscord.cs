using SRMultiplayer.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRMultiplayer.Plugin
{
    internal static class SRMPDiscord
    {
        public static int GetPlayerCount()
        {
            if (NetworkServer.Instance.Status == NetworkServer.ServerStatus.Running)
            {
                return NetworkServer.Instance.m_Server.ConnectionsCount;
            }
            else if (NetworkClient.Instance.Status == NetworkClient.ConnectionStatus.Connected)
            {
                return NetworkClient.Instance.playerCount;
            }
            else return 0;
        }
        internal static void InjectIntoRP(DiscordRpc.RichPresence rp)
        {
            rp.details += $"\nCurrently playing in multiplayer!\nLobby player count: {GetPlayerCount()}";
        }
    }
}
