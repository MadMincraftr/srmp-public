using SRMultiplayer.Networking;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SRMultiplayer.Steam
{
    public class SRMPSteam : MonoBehaviour
    {
        internal Callback<LobbyCreated_t> successfulHost;
        internal Callback<LobbyInvite_t> invited;
        internal Callback<GameLobbyJoinRequested_t> attemptJoin;
        internal Callback<LobbyEnter_t> join;
        public void Start()
        {
            if (Main.FinishedSetup)
            {
                successfulHost = Callback<LobbyCreated_t>.Create(CheckSteamHostSuccess);
                invited = Callback<LobbyInvite_t>.Create(DetectInvite);
                attemptJoin = Callback<GameLobbyJoinRequested_t>.Create(AttemptJoin);
                join = Callback<LobbyEnter_t>.Create(JoinLobby);
                join = Callback<LobbyEnter_t>.Create(JoinLobby);
            }
        }
        public void Update()
        {
            if (Main.FinishedSetup)
            {
                SteamAPI.RunCallbacks();
            }
        }

        public void HostSteamGame(ELobbyType type)
        {
            SteamMatchmaking.CreateLobby(type, 255);
        }

        public static async Task<string> GetPublicIP()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync("https://ipinfo.io");
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    int index = responseBody.IndexOf("ip\":", StringComparison.OrdinalIgnoreCase);
                    string publicIp = responseBody.Substring(index + 5, responseBody.IndexOf("\"", index + 5, StringComparison.OrdinalIgnoreCase) - (index + 5));

                    return publicIp.Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error getting public IP: " + ex.Message);
                    return null;
                }
            }
        }
        public static string GetIP()
        {
            string localIP = string.Empty;
            // Get the local machine's host name
            string hostName = Dns.GetHostName();

            // Get the IP addresses associated with the host
            IPAddress[] localIPs = Dns.GetHostAddresses(hostName);

            // Find the first IPv4 address (assuming you're looking for a normal, non-loopback address)
            foreach (IPAddress ip in localIPs)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }
        internal void CheckSteamHostSuccess(LobbyCreated_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "hostIP", GetIP());
                SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "hostPort", NetworkServer.Instance.Port.ToString());

                Debug.Log("Hosted game is now set up for steam invites!");
            }
        }

        internal void DetectInvite(LobbyInvite_t callback)
        {
            Debug.Log($"You have been invited to a game!");
        }

        internal void AttemptJoin(GameLobbyJoinRequested_t callback)
        {
            SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
        }
        internal void JoinLobby(LobbyEnter_t callback)
        {
            string name;
            if (string.IsNullOrEmpty(MultiplayerUI.Instance.Username))
                name = "NoName_Guest";
            else
                name = MultiplayerUI.Instance.Username;
            NetworkClient.Instance.Connect(SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "hostIP"), int.Parse(SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "hostPort")), name);
        }

    }
}
