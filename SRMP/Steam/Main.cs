using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace SRMultiplayer.Steam
{
    internal static class Main
    {
        public static bool FinishedSetup => m_finishedSetup && m_startedSetup;

        private static bool m_finishedSetup;
        private static bool m_startedSetup;

        public static void Init(GameObject srmpOBJ)
        {
            if (SteamAPI.Init())
            {
                m_startedSetup = true;
                Debug.Log("Starting steam invite system!");
                srmpOBJ.AddComponent<SRMPSteam>();
                m_finishedSetup = true;
            }
        }
    }
}
