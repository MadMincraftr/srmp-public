using Lidgren.Network;
using SRMultiplayer.Packets;
using SRMultiplayer.Steam;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SRMultiplayer.Networking
{
    public class NetworkServer : MonoBehaviour
    {
        public static NetworkServer Instance { get; private set; }
        public NetServer m_Server;
        public NetServer m_DiscoverServer;

        public int Port;

        public enum ServerStatus
        {
            Stopped,
            Running
        }
        public ServerStatus Status;
        public NetPeerStatistics Statistics { get { return m_Server.Statistics; } }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void StartServer(int port, MultiplayerUI.SteamHostMode steamMode = MultiplayerUI.SteamHostMode.NoSteam)
        {
            Port = port;
            Status = ServerStatus.Running;

            NetPeerConfiguration config = new NetPeerConfiguration("srmp");
            config.Port = port;
            config.MaximumConnections = 250;
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);

            m_Server = new NetServer(config);
            m_Server.Start();

            NetPeerConfiguration discoverConfig = new NetPeerConfiguration("discover");
            discoverConfig.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            discoverConfig.MaximumConnections = 0;
            discoverConfig.Port = 6996;

            m_DiscoverServer = new NetServer(discoverConfig);
            m_DiscoverServer.Start();

            Globals.DisableAchievements = true;
            Globals.PartyID = Guid.NewGuid();

            byte id = 1;
            while (id < 255 && Globals.Players.Values.Any(p => p.ID == id))
                id++;

            Globals.LocalID = id;
            Globals.LocalPlayer = SRSingleton<SceneContext>.Instance.Player.AddComponent<NetworkPlayer>();
            Globals.LocalPlayer.ID = id;
            Globals.LocalPlayer.Username = Globals.Username;
            Globals.LocalPlayer.HasLoaded = true;
            Globals.LocalPlayer.Spawn();
            Globals.Players.Add(id, Globals.LocalPlayer);
            Globals.ClientLoaded = true;

            Directory.CreateDirectory(Path.Combine(SRMP.ModDataPath, SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName()));

            foreach(var netRegion in Globals.Regions.Values)
            {
                if(netRegion.Region.root.activeInHierarchy)
                {
                    netRegion.AddPlayer(Globals.LocalPlayer);
                    netRegion.TakeOwnership();
                }
            }
            foreach(var actor in SRSingleton<SceneContext>.Instance.GameModel.AllActors().Values)
            {
                if (actor.ident != Identifiable.Id.NONE && actor.ident != Identifiable.Id.PLAYER && !Identifiable.SCENE_OBJECTS.Contains(actor.ident))
                {
                    var netActor = actor.transform.gameObject.AddComponent<NetworkActor>();
                    netActor.ID = Utils.GetRandomActorID();
                    netActor.Ident = (ushort)actor.ident;
                    netActor.RegionSet = (byte)actor.currRegionSetId;
                    if (actor.transform.gameObject.activeInHierarchy)
                    {
                        netActor.Owner = Globals.LocalID;
                    }

                    Globals.Actors.Add(netActor.ID, netActor);
                }
            }
            foreach(var landPlot in SRSingleton<SceneContext>.Instance.GameModel.AllLandPlots().Values)
            {
                var netLandPlot = landPlot.gameObj.GetComponent<NetworkLandplot>();
                netLandPlot.Plot = landPlot.gameObj.GetComponentInChildren<LandPlot>(true);
            }
            foreach (TutorialDirector.Id tut in (TutorialDirector.Id[])Enum.GetValues(typeof(TutorialDirector.Id)))
            {
                SRSingleton<SceneContext>.Instance.TutorialDirector.tutModel.completedIds.Add(tut);
            }

            if (Steam.Main.FinishedSetup)
            {
                SRMPSteam.Instance.HostSteamGame(MultiplayerUI.Instance.SteamHostModeToLobbyType[steamMode]);
            }
            NetworkMasterServer.Instance.CreateServer(port);
        }

        public void Disconnect()
        {
            Status = ServerStatus.Stopped;

            m_Server?.Shutdown("goodbye");
            m_DiscoverServer?.Shutdown("goodbye");

            if (Main.FinishedSetup)
            {
                SteamMatchmaking.LeaveLobby(SRMPSteam.Instance.currLobbyID);
                SRMPSteam.Instance.isHost = false;
            }

            SRSingleton<NetworkMasterServer>.Instance.DeleteServer();
        }

        private void Update()
        {
            NetIncomingMessage dim;
            while ((dim = m_DiscoverServer?.ReadMessage()) != null)
            {
                switch (dim.MessageType)
                {
                    case NetIncomingMessageType.DiscoveryRequest:
                        {
                            NetOutgoingMessage response = m_DiscoverServer.CreateMessage();
                            response.Write("Game of " + Globals.Username);
                            response.Write(Port);

                            // Send the response to the sender of the request
                            m_DiscoverServer.SendDiscoveryResponse(response, dim.SenderEndPoint);
                        }
                        break;
                }
            }

            NetIncomingMessage im;
            while ((im = m_Server?.ReadMessage()) != null)
            {
                switch (im.MessageType)
                {
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.ErrorMessage:
                    case NetIncomingMessageType.WarningMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        {
                            string text = im.ReadString();
                            //SRMP.Log(text);
                        }
                        break;
                    case NetIncomingMessageType.NatIntroductionSuccess:
                        {
                            string token = im.ReadString();
                            SRMP.Log("[NetworkServer] Nat introduction success to " + im.SenderEndPoint + " token is: " + token);
                        }
                        break;
                    case NetIncomingMessageType.ConnectionApproval:
                        {
                            int version = im.ReadInt32();
                            Guid uuid = new Guid(im.ReadBytes(16));
                            string username = im.ReadString();

                            SRMP.Log($"{username} ({uuid} {im.SenderConnection.RemoteEndPoint.Address.ToString()}) connects with version {version}");

                            if (version != Globals.Version)
                            {
                                im.SenderConnection.Deny($"Version mismatch. Required version: {Globals.Version}");
                                break;
                            }
                            Regex rg = new Regex(@"^[a-zA-Z_][\w]*$");
                            if (!rg.IsMatch(username))
                            {
                                im.SenderConnection.Deny("Username invalid. Only A-Z, 0-9 and _ is allowed");
                                break;
                            }
                            if (!im.SenderConnection.RemoteEndPoint.Address.ToString().Equals("127.0.0.1") && Globals.Players.Values.Any(p => p.UUID == uuid))
                            {
                                im.SenderConnection.Deny("Someone with that UUID already online");
                                break;
                            }

                            List<string> mods = new List<string>();
                            int modsCount = im.ReadInt32();
                            for (int i = 0; i < modsCount; i++)
                            {
                                mods.Add(im.ReadString());
                            }
                            bool successMods = true;
                            int successModsCount = 0;
                            foreach (var mod in mods)
                            {
                                if (!Globals.Mods.Contains(mod) && !Globals.UserData.IgnoredMods.Contains(mod))
                                {
                                    successMods = false;
                                    break;
                                }
                                else if(!Globals.UserData.IgnoredMods.Contains(mod))
                                {
                                    successModsCount++;
                                }
                            }
                            if (!successMods || successModsCount != Globals.Mods.Count)
                            {
                                var missingMods = new List<string>();
                                foreach (var m in Globals.Mods)
                                {
                                    if (!mods.Contains(m))
                                    {
                                        missingMods.Add(m);
                                    }
                                }
                                var clientMods = new List<string>();
                                foreach (var m in mods)
                                {
                                    if (!Globals.Mods.Contains(m))
                                    {
                                        clientMods.Add(m);
                                    }
                                }
                                string msg = "Mods mismatch.\n";
                                if (missingMods.Count > 0) msg += "You are missing following mods: " + String.Join(", ", missingMods.ToArray()) + "\n";
                                if (clientMods.Count > 0) msg += "The Server is missing following mods: " + String.Join(", ", clientMods.ToArray()) + "\n";
                                im.SenderConnection.Deny(msg);
                                break;
                            }

                            List<DLCPackage.Id> dlcs = new List<DLCPackage.Id>();
                            int dlcCount = im.ReadInt32();
                            for(int i = 0; i < dlcCount; i++)
                            {
                                dlcs.Add((DLCPackage.Id)im.ReadByte());
                            }
                            if (Globals.UserData.CheckDLC)
                            {
                                if (SRSingleton<GameContext>.Instance.DLCDirector.Installed.Count() != dlcs.Count)
                                {
                                    im.SenderConnection.Deny("DLC mismatch. You need following DLCs: " + String.Join(", ", SRSingleton<GameContext>.Instance.DLCDirector.Installed) + "\nYou got: " + String.Join(", ", dlcs.ToArray()));
                                    break;
                                }
                                bool successDLCs = true;
                                int successDLCsCount = 0;
                                foreach (var dlc in dlcs)
                                {
                                    if (!SRSingleton<GameContext>.Instance.DLCDirector.Installed.Contains(dlc))
                                    {
                                        successDLCs = false;
                                        break;
                                    }
                                    else
                                    {
                                        successDLCsCount++;
                                    }
                                }
                                if (!successDLCs || successDLCsCount != SRSingleton<GameContext>.Instance.DLCDirector.Installed.Count())
                                {
                                    im.SenderConnection.Deny("DLC mismatch. You need following DLCs: " + String.Join(", ", SRSingleton<GameContext>.Instance.DLCDirector.Installed) + "\nYou got: " + String.Join(", ", dlcs.ToArray()));
                                    break;
                                }
                            }

                            byte id = 1;
                            while (id < 255 && Globals.Players.Values.Any(p => p.ID == id))
                                id++;

                            var playerObj = new GameObject(username + "(" + id + ")");
                            var player = playerObj.AddComponent<NetworkPlayer>();

                            player.Connection = im.SenderConnection;
                            player.UUID = uuid;
                            player.ID = id;
                            player.Username = username;
                            player.Mods = mods;
                            player.DLCs = dlcs;

                            Globals.Players.Add(player.ID, player);
                            NetOutgoingMessage hail = CreateMessage();
                            hail.Write(id);
                            hail.Write(Globals.Players.Count);
                            foreach (var p in Globals.Players.Values.ToList())
                            {
                                hail.Write(p.ID);
                                hail.Write(p.Username);
                                hail.Write(p.HasLoaded);
                            }
                            hail.Write(Globals.PartyID.ToByteArray());
                            hail.Write((byte)SRSingleton<SceneContext>.Instance.GameModel.currGameMode);
                            hail.Write(SRSingleton<GameContext>.Instance.AutoSaveDirector.SavedGame.GetName());
                            im.SenderConnection.Approve(hail);
                        }
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        {
                            NetConnectionStatus status = (NetConnectionStatus)im.ReadByte();
                            string reason = im.ReadString();
                            //SRMP.Log(NetUtility.ToHexString(im.SenderConnection.RemoteUniqueIdentifier) + " " + status + ": " + reason);

                            var player = Globals.Players.Values.FirstOrDefault(p => p.Connection != null && p.Connection.RemoteUniqueIdentifier == im.SenderConnection.RemoteUniqueIdentifier);
                            if (player != null)
                            {
                                if (status == NetConnectionStatus.Connected)
                                {
                                    SRMP.Log($"Player connected: {player} with following mods: {string.Join(", ", player.Mods)} and these DLCs: {String.Join(", ", player.DLCs)}");
                                    new PacketPlayerJoined()
                                    {
                                        ID = player.ID,
                                        Username = player.Username
                                    }.SendToAllExcept(player);
                                }
                                else if (status == NetConnectionStatus.Disconnected)
                                {
                                    if (player.HasLoaded)
                                    {
                                        player.Save();
                                    }
                                    if (player.gameObject != null)
                                    {
                                        Destroy(player.gameObject);
                                    }
                                    Globals.Players.Remove(player.ID);

                                    player.OnLeft();
                                    SRMP.Log($"Player disconnected: {player}");
                                    SendToAll(new PacketPlayerLeft()
                                    {
                                        ID = player.ID
                                    });
                                }
                            }
                        }
                        break;
                    case NetIncomingMessageType.Data:
                        {
                            Globals.HandlePacket = true;
                            PacketType type = (PacketType)im.ReadUInt16();

                            var player = Globals.Players.Values.FirstOrDefault(p => p.Connection != null && p.Connection.RemoteUniqueIdentifier == im.SenderConnection.RemoteUniqueIdentifier);
                            if (player != null)
                            {
                                try
                                {
                                    NetworkHandlerServer.HandlePacket(type, im, player);
                                }
                                catch (Exception ex)
                                {
                                    SRMP.Log($"[SERVER] Could not handle packet {type}\n{ex}");
                                }
                            }
                            Globals.HandlePacket = false;
                        }
                        break;
                    default:
                        SRMP.Log("Unhandled type: " + im.MessageType + " " + im.LengthBytes + " bytes " + im.DeliveryMethod + "|" + im.SequenceChannel);
                        break;
                }
                m_Server.Recycle(im);
            }
        }

        public void SendToAll(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequence = 0)
        {
            var om = CreateMessage();
            packet.Serialize(om);

            m_Server?.SendToAll(om, method, sequence);
        }

        public void SendToAll(NetOutgoingMessage packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequence = 0)
        {
            m_Server?.SendToAll(packet, method, sequence);
        }

        public void SendToAll(NetOutgoingMessage packet, NetworkPlayer player, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequence = 0)
        {
            m_Server?.SendToAll(packet, player.Connection, method, sequence);
        }

        internal void SendTo(Packet packet, List<NetConnection> cons, NetDeliveryMethod method, int sequence)
        {
            if (cons.Count > 0)
            {
                var om = CreateMessage();
                packet.Serialize(om);

                m_Server?.SendMessage(om, cons, method, sequence);
            }
        }

        public void SendToAllExcept(Packet packet, NetworkPlayer player, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequence = 0)
        {
            var om = CreateMessage();
            packet.Serialize(om);

            List<NetConnection> cons = new List<NetConnection>();
            foreach (var p in Globals.Players.Values.ToList())
            {
                if (p.ID != player.ID)
                {
                    cons.Add(p.Connection);
                }
            }
            if (cons.Count > 0)
            {
                m_Server?.SendMessage(om, cons, method, sequence);
            }
        }

        public void Send(NetConnection connection, IPacket packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered, int sequence = 0)
        {
            var om = CreateMessage();
            packet.Serialize(om);

            connection.SendMessage(om, method, sequence);
        }

        public void SendUnconnected(NetOutgoingMessage om, IPEndPoint endPoint)
        {
            m_Server?.SendUnconnectedMessage(om, endPoint);
        }

        public NetOutgoingMessage CreateMessage()
        {
            return m_Server?.CreateMessage();
        }
    }
}
