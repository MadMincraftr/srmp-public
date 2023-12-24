using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRMultiplayer.Patches
{
    [HarmonyPatch(typeof(DiscordRpc), nameof(DiscordRpc.UpdatePresence))]
    static class ModifyDiscordRP
    {
        static void Prefix(DiscordRpc __instance, DiscordRpc.RichPresence presence)
        {

        }
    }
}
