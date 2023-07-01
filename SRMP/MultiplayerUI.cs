﻿using UnityEngine;
using System.Collections;
using SRMultiplayer;
using UnityEngine.SceneManagement;
using System;
using System.Text.RegularExpressions;
using SRMultiplayer.Networking;

public class MultiplayerUI : SRSingleton<MultiplayerUI>
{
    private Rect windowRect = new Rect(Screen.width - 300 - 20, 20, 300, 500);
    private Vector2 playersScroll = Vector2.zero;
    private string ipaddress = "localhost";
    private string port = "16500";
    private string servercode = "";

    //use internal name with getter and setter to allow the menu panel to remember the users last choice
    private int _menuOpen;
    private int menuOpen  //menu open hold the current menu state (0 colapsed, 1 minimized, 2 open)
    {
        get
        {
            return _menuOpen;
        }
        set
        {
            _menuOpen = value;

            //save users setting
            PlayerPrefs.SetInt("SRMP_Menu", menuOpen);
        }
    }


    private string username;
    private float lastCodeUse;
    private ConnectError error;
    private ConnectHelp help;
    private string errorMessage;



    public enum ConnectError
    {
        None,
        InvalidServerCode,
        ServerCodeTimeout,
        Kicked,
        Message
    }

    public enum ConnectHelp
    {
        None,
        ServerCode,
        Hosting,
    }

    public override void Awake()
    {
        base.Awake();

        Globals.Username = PlayerPrefs.GetString("SRMP_Username", "");
        ipaddress = PlayerPrefs.GetString("SRMP_IP", "localhost");
        port = PlayerPrefs.GetString("SRMP_Port", "16500");

        menuOpen = PlayerPrefs.GetInt("SRMP_Menu", 2); ; //start panel open by default

        username = Globals.Username;
    }

    private void Update()
    {
        if (lastCodeUse > 0f)
        {
            var prevTime = lastCodeUse;
            lastCodeUse -= Time.deltaTime;
            if (prevTime > 0f && lastCodeUse <= 0f)
            {
                error = ConnectError.ServerCodeTimeout;
            }
        }


        //this sets presskey senarios
        if (Input.GetKeyDown(KeyCode.F4))
        {
            //use the f4 key to swap between collapsed and minimized
            menuOpen = menuOpen == 2 ? 0 : 2;
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            //use the f3 key to set minimized or maximized
            menuOpen = menuOpen < 2 ? 2 : 1;
        }
    }

    private void OnGUI()
    {
        //verify on a window that the menu can be drawn on
        if ((!Levels.isMainMenu() && !Globals.IsMultiplayer && Globals.PauseState != PauseState.Pause)) return;
        if (Globals.IsMultiplayer && Globals.PauseState != PauseState.Pause) return;

        //if yes draw the window for the given state
        if (SceneManager.GetActiveScene().buildIndex >= 2)
        {
            //check to make sure the panel is taking less than 25% of the screen if possible
            float width = 300;
            if(Screen.width/ 4 < width) width = Screen.width/4;
            windowRect.width = width;

            //check to see if the windows needs to move due to it being off the screen from size change
            //also prevent the user from dragging it off the screen
            if (windowRect.x + 20 + windowRect.width > Screen.width) windowRect.x = Screen.width - windowRect.width - 20;
            if (windowRect.y + 20 + windowRect.height > Screen.height) windowRect.y = (Screen.height - windowRect.height - 20) >= 20 ? (Screen.height - windowRect.height - 20) : 20;
            if (windowRect.x < 20) windowRect.x = 20;
            if (windowRect.y < 20) windowRect.y = 20;

            

            //drawn in the window
            switch (menuOpen)
            {
                case 0: //collapsed
                    windowRect.height = 50;
                    windowRect = GUILayout.Window(1, windowRect, ClosedWindow, "SRMP v" + Globals.Version);
                    break;
                case 1: //minimized
                    windowRect.height = 100;
                    windowRect = GUILayout.Window(1, windowRect, MiniWindow, "SRMP v" + Globals.Version);
                    break;
                case 2: //open
                    windowRect.height = 500;
                    windowRect = GUILayout.Window(1, windowRect, MultiplayerWindow, "SRMP v" + Globals.Version);
                    break;
            }


        }
    }
    private void FunctionKeys()
    {
        if (menuOpen != 0)
        {
            GUILayout.Label("Press Button or Key To Change Style");
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(menuOpen == 1 ? "F3 - Full" : "F3 - Mini"))
        {
            menuOpen = menuOpen == 1 ? 2 : 1;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(menuOpen == 0 ? "F4 - Full" : "F4 - Colapsed"))
        {
            menuOpen = menuOpen == 0 ? 2 : 0;
        }
        GUILayout.EndHorizontal();
    }

    private void ClosedWindow(int id)
    {
        if (id != 1) return;
        //display f3 and f4 commands
        FunctionKeys();

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }
    private void MiniWindow(int id)
    {
        if (id != 1) return;

        //display f3 and f4 commands
        FunctionKeys();

        //show username and current connection status
        GUIStyle standard = new GUIStyle(GUI.skin.label);
        GUIStyle red = new GUIStyle(GUI.skin.label);
        red.normal.textColor = Color.red;


        if (string.IsNullOrWhiteSpace(Globals.Username))
        {
            GUILayout.Label("Username: Not Set", red);
        }
        else
        {
            GUILayout.Label("Username: " + Globals.Username);

            if (Globals.IsServer)
            {
                GUILayout.Label("Status: Host");
            }
            else if (Globals.IsClient)
            {

                GUIStyle green = new GUIStyle(GUI.skin.label);
                green.normal.textColor = Color.green;
                GUILayout.Label("Status: Client", green);
            }
            else
            {
                bool canHost = true;
                //check if user can host the session
                if (!int.TryParse(port, out int numport) || numport < 1000 || numport > 65000)
                {
                    canHost = false;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Status: Disconnected", red);

                GUILayout.FlexibleSpace();
                if (canHost)
                {
                    //only show host if not main menu
                    if (!Levels.isMainMenu())
                    {
                        if (GUILayout.Button("Host"))
                        {
                            NetworkServer.Instance.StartServer(numport);
                            SaveSettings();
                        }
                    }
                }
                GUILayout.EndHorizontal();
                if (!canHost)
                {
                    GUILayout.Label("Invalid Port Settings", red);
                }
            }

        }
        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    private void MultiplayerWindow(int id)
    {
        if (id != 1) return;

        //display f3 and f4 commands
        FunctionKeys();

        //now display standard 
        if (string.IsNullOrWhiteSpace(Globals.Username))
        {
            UsernameGUI();
        }
        else
        {
            if (Globals.IsServer)
            {
                ServerGUI();
            }
            else if (Globals.IsClient)
            {
                ClientGUI();
            }
            else
            {
                if (error != ConnectError.None)
                {
                    ErrorGUI();
                }
                else if (help != ConnectHelp.None)
                {
                    HelpGUI();
                }
                else
                {
                    if (lastCodeUse > 0f)
                    {
                        GUILayout.Label("Trying to connect with server code...");
                    }
                    else if (Levels.isMainMenu())
                    {
                        ConnectGUI();
                    }
                    else
                    {
                        HostGUI();
                    }
                }
            }
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 10000));
    }

    private void ServerGUI()
    {
        GUILayout.Label("You are the server");
        GUILayout.Space(20);

        GUILayout.Label("Server Code: " + Globals.ServerCode);
        GUILayout.Label("Players");
        playersScroll = GUILayout.BeginScrollView(playersScroll, GUI.skin.box);
        foreach (var player in Globals.Players.Values)
        {
            if (player.IsLocal) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(player.Username);
            if (GUILayout.Button("Kick"))
            {
                player.Connection.Disconnect("kicked");
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void ClientGUI()
    {
        GUILayout.Label("You are a client");
        GUILayout.Space(20);

        GUILayout.Label("Players");
        playersScroll = GUILayout.BeginScrollView(playersScroll, GUI.skin.box);
        foreach (var player in Globals.Players.Values)
        {
            if (player.IsLocal) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(player.Username);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void ConnectGUI()
    {
        GUILayout.Label("Username: " + Globals.Username);
        if (GUILayout.Button("Change Username"))
        {
            Globals.Username = "";
            return;
        }
        GUILayout.Space(20);
        if (GUILayout.Button("How do I host a game?"))
        {
            help = ConnectHelp.Hosting;
        }
        GUILayout.Space(20);

        if (SRSingleton<NetworkMasterServer>.Instance.Status == NetworkMasterServer.ConnectionStatus.Connected)
        {
            GUILayout.Label("Join with server code:");
            GUILayout.BeginHorizontal();
            servercode = GUILayout.TextField(servercode, 5, GUILayout.Width(80));
            servercode = servercode.Replace(" ", "").ToUpper();
            if (GUILayout.Button("Join"))
            {
                lastCodeUse = 5;
                SRSingleton<NetworkMasterServer>.Instance.JoinServer(servercode);
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("Server codes currently not available");
        }

        GUILayout.Space(20);

        GUILayout.Label("Join with IP Address:");
        GUILayout.BeginHorizontal();
        GUILayout.Label("IP Address", GUILayout.Width(80));
        ipaddress = GUILayout.TextField(ipaddress);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Port", GUILayout.Width(80));
        port = GUILayout.TextField(port);
        GUILayout.EndHorizontal();
        if (string.IsNullOrWhiteSpace(ipaddress))
        {
            GUILayout.Label("IPAddress invalid");
        }
        else if (!int.TryParse(port, out int numport) || numport < 1000 || numport > 65000)
        {
            GUILayout.Label("Invalid port, should be between 1000 and 65000");
        }
        else
        {
            if (GUILayout.Button("Connect"))
            {
                SRSingleton<NetworkClient>.Instance.Connect(ipaddress, numport, Globals.Username);
                SaveSettings();
            }
        }

        GUILayout.Space(20);
        GUILayout.Label("Games in the same Network:");
        if (GUILayout.Button("Refresh"))
        {
            SRSingleton<NetworkClient>.Instance.SendDiscoverMessage();
        }
        if (SRSingleton<NetworkClient>.Instance.LocalGames.Count == 0)
        {
            GUILayout.Label("No games found");
        }
        foreach (var game in SRSingleton<NetworkClient>.Instance.LocalGames)
        {
            if (GUILayout.Button(game.Name))
            {
                SRSingleton<NetworkClient>.Instance.Connect(game.IP, game.Port, Globals.Username);
            }
        }
    }

    private void HostGUI()
    {
        GUILayout.Label("Username: " + Globals.Username);
        if (GUILayout.Button("Change Username"))
        {
            Globals.Username = "";
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port", GUILayout.Width(80));
        port = GUILayout.TextField(port);
        GUILayout.EndHorizontal();

        if (!int.TryParse(port, out int numport) || numport < 1000 || numport > 65000)
        {
            GUILayout.Label("Invalid port");
        }
        else
        {
            if (GUILayout.Button("Host"))
            {
                NetworkServer.Instance.StartServer(numport);
                SaveSettings();
            }
        }
    }

    private void HelpGUI()
    {
        switch (help)
        {
            case ConnectHelp.ServerCode:
                {

                }
                break;
            case ConnectHelp.Hosting:
                {
                    GUILayout.Label("You can host a game by loading any Singleplayer save.");
                    GUILayout.Label("When the game is loaded, pause it and the HostUI should appear.");
                    GUILayout.Label("(Please make a backup of your Singleplayer save to avoid data loss on crashes or error)");

                    if (GUILayout.Button("Okay"))
                    {
                        help = ConnectHelp.None;
                    }
                }
                break;
        }
    }

    private void ErrorGUI()
    {
        switch (error)
        {
            case ConnectError.InvalidServerCode:
                {
                    GUILayout.Label("There is no server with this server code.");

                    if (GUILayout.Button("Okay"))
                    {
                        error = ConnectError.None;
                    }
                }
                break;
            case ConnectError.Kicked:
                {
                    GUILayout.Label("You got kicked from the game");

                    if (GUILayout.Button("Okay"))
                    {
                        error = ConnectError.None;
                    }
                }
                break;
            case ConnectError.Message:
                {
                    GUILayout.Label("Connection Error:");
                    GUILayout.Label(errorMessage);

                    if (GUILayout.Button("Okay"))
                    {
                        error = ConnectError.None;
                    }
                }
                break;
            case ConnectError.ServerCodeTimeout:
                {
                    GUILayout.Label("Connection via server code timed out.");
                    GUILayout.Label("Server codes do not have a 100% success chance.");
                    GUILayout.Label("You can try again, but if it keeps failing, you may have to port forward.");
                    GUILayout.Label("If you can't port forward, we recommend a service like 'Hamachi' or 'Radmin'");
                    GUILayout.Label("Please note: Hamachi and Radmin degrade the experience by quite a lot. Port forwarding is always better.");
                    GUILayout.Label("(We can not offer support for port forwarding)");

                    if (GUILayout.Button("Okay"))
                    {
                        error = ConnectError.None;
                    }
                }
                break;
        }
    }

    private void UsernameGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Username", GUILayout.Width(60));
        username = GUILayout.TextField(username, 30);
        GUILayout.EndHorizontal();

        if (string.IsNullOrWhiteSpace(username) || !new Regex(@"^[a-zA-Z_][\w]*$").IsMatch(username))
        {
            GUILayout.Label("Invalid username");
        }
        else
        {
            if (GUILayout.Button("Save Username"))
            {
                Globals.Username = username;
                SaveSettings();
                SRSingleton<NetworkMasterServer>.Instance.UpdateName(Globals.Username);
            }
        }
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetString("SRMP_Username", Globals.Username);
        PlayerPrefs.SetString("SRMP_IP", ipaddress);
        PlayerPrefs.GetString("SRMP_Port", port);
    }

    public void ConnectResponse(ConnectError connectError, string message = "")
    {
        lastCodeUse = 0f;
        error = connectError;
        errorMessage = message;
    }
}
