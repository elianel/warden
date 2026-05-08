using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace elian.Warden.Editor
{
    [InitializeOnLoad]
    public static class HarmonyPatcher
    {
        private static Harmony harmony;
        static HarmonyPatcher()
        {
            harmony = new Harmony("elianel.Warden");
            harmony.PatchAll();
            //Debug.Log("[Warden] Harmony.PatchAll");
        }
    }
}
