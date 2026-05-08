using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace elian.Warden.Editor
{
    internal static class Warden
    {

        private static readonly string[] s_ScriptExtensions = { ".cs", ".dll" };

        internal static object[] s_AllItems = null;
        internal static object[] s_ScriptItems = null;
        internal static bool s_Analyzed = false;

        private static GUIStyle WarningStyle;
        private static GUIStyle HeaderStyle;
        private static GUIStyle BodyStyle;
        private static GUIStyle ItemStyle;

        internal static readonly System.Type t_PackageImport =
            typeof(AssetDatabase).Assembly.GetType("UnityEditor.PackageImport");
        internal static readonly System.Type t_ImportPackageItem =
            typeof(AssetDatabase).Assembly.GetType("UnityEditor.ImportPackageItem");

        internal static readonly FieldInfo f_isFolder =
            t_ImportPackageItem?.GetField("isFolder", BindingFlags.Public | BindingFlags.Instance);
        internal static readonly FieldInfo f_destinationAssetPath =
            t_ImportPackageItem?.GetField("destinationAssetPath", BindingFlags.Public | BindingFlags.Instance);
        internal static readonly FieldInfo f_sourceFolder =
            t_ImportPackageItem?.GetField("sourceFolder", BindingFlags.Public | BindingFlags.Instance);
        internal static readonly FieldInfo f_enabledStatus =
            t_ImportPackageItem?.GetField("enabledStatus", BindingFlags.Public | BindingFlags.Instance);

        [HarmonyPatch]
        internal static class Patch_Init
        {
            [HarmonyTargetMethod]
            static MethodBase Target() =>
                AccessTools.Method(t_PackageImport, "Init");

            [HarmonyPostfix]
            internal static void Postfix(object __instance, object[] items)
            {
                s_Analyzed = false;
                s_AllItems = null;
                s_ScriptItems = null;

                if (items == null || items.Length == 0)
                    return;

                s_AllItems = items;
                s_ScriptItems = items
                    .Where(item =>
                    {
                        var isFolder = (bool)(f_isFolder?.GetValue(item) ?? false);
                        if (isFolder) return false;
                        var path = (string)(f_destinationAssetPath?.GetValue(item) ?? "");
                        return s_ScriptExtensions.Any(ext =>
                            path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase));
                    })
                    .ToArray();

                s_Analyzed = true;
                Debug.Log($"[Warden] Analysis done. Script files found: {s_ScriptItems.Length}");
            }
        }

        [HarmonyPatch]
        internal static class Patch_OnGUI
        {
            [HarmonyTargetMethod]
            static MethodBase Target() =>
                AccessTools.Method(t_PackageImport, "OnGUI");

            [HarmonyPrefix]
            static void Prefix(object __instance)
            {
                if (!s_Analyzed)
                {
                    var itemsProp = t_PackageImport?.GetProperty("packageItems",
                        BindingFlags.Public | BindingFlags.Instance);
                    var items = itemsProp?.GetValue(__instance) as object[];
                    if (items != null)
                        Patch_Init.Postfix(__instance, items);
                }

                if (s_ScriptItems == null || s_ScriptItems.Length == 0)
                    return;

                DrawWarningBanner();
            }
        }

        internal static void DrawWarningBanner()
        {
            EnsureStyles();

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.75f, 0.1f, 1f);
            GUILayout.BeginVertical(WarningStyle);
            GUI.backgroundColor = prevBg;

            GUILayout.BeginHorizontal();
            GUILayout.Label("⚠  Script Warning", HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(
                $"{s_ScriptItems.Length} script file{(s_ScriptItems.Length == 1 ? "" : "s")} detected",
                EditorStyles.miniLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(2);
            GUILayout.Label(
                "This package contains scripts. Only import from sources you trust. Scripts can execute arbitrary code on import.",
                BodyStyle);

            GUILayout.Space(4);
            foreach (var item in s_ScriptItems.Take(6))
            {
                var path = (string)f_destinationAssetPath.GetValue(item);
                GUILayout.Label($"  - {path}", ItemStyle);
            }

            if (s_ScriptItems.Length > 6)
                GUILayout.Label($"  … and {s_ScriptItems.Length - 6} more", ItemStyle);

            GUILayout.Space(6);
            if (GUILayout.Button("Export For Manual Review", GUILayout.Width(200)))
            {
                var dest = EditorUtility.OpenFolderPanel("Save scripts to…", "", "");
                if (!string.IsNullOrEmpty(dest))
                    ExportItems(s_ScriptItems, dest);
            }

            GUILayout.Space(4);
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private static void ExportItems(object[] items, string destFolder)
        {
            int count = 0;
            foreach (var item in items)
            {
                var sourceFolder = (string)(f_sourceFolder?.GetValue(item) ?? "");
                var destPath = (string)(f_destinationAssetPath?.GetValue(item) ?? "");

                if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(destPath))
                    continue;

                var sourcePath = Path.Combine(sourceFolder, "asset");
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[Warden] Source file not found: {sourcePath}");
                    continue;
                }

                var outputPath = Path.Combine(destFolder, destPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.Copy(sourcePath, outputPath, overwrite: true);
                Debug.Log($"[Warden] Exported: {outputPath}");
                count++;
            }

            Debug.Log($"[Warden] Exported {count} script(s) to {destFolder}");
            EditorUtility.RevealInFinder(destFolder);
        }

        private static void EnsureStyles()
        {
            if (WarningStyle != null) return;

            WarningStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(6, 6, 4, 0),
            };

            HeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.55f, 0.35f, 0f) },
                fontSize = 12,
            };

            BodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 11,
            };

            ItemStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                wordWrap = true,
            };
        }
    }
}
