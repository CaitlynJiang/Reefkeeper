using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Reefkeeper.Editor
{
    // Shared filename prompt; each platform supplies its own validated build action.
    public sealed class LdcBuildVersionWindow : EditorWindow
    {
        string platform;
        string version = "5";
        Action<string> build;

        public static string OutputPath(string platform, string version)
        {
            if (platform != "Swan" && platform != "Quest")
                throw new ArgumentException("Expected Swan or Quest.");
            if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1)
                throw new ArgumentException("Enter a positive whole version number, such as 5.");
            return $"Builds/LDC_{platform}_v{n}.apk";
        }

        public static void Open(string platform, Action<string> build)
        {
            var window = CreateInstance<LdcBuildVersionWindow>();
            window.titleContent = new GUIContent("Lucky Dive Co. — APK");
            window.platform = platform;
            window.build = build;
            window.version = EditorPrefs.GetString("LuckyDiveCo.BuildVersion." + Application.dataPath + "." + platform, "5");
            window.minSize = window.maxSize = new Vector2(420, 170);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Platform", platform);
            version = EditorGUILayout.TextField("Version number", version);
            string path = null;
            try { path = OutputPath(platform, version); }
            catch (ArgumentException) { }
            EditorGUILayout.HelpBox(path ?? "Enter a positive whole number, for example 5.",
                path == null ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.LabelField("This controls the APK filename only.");
            using (new EditorGUI.DisabledScope(path == null || build == null || EditorApplication.isCompiling || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Build APK"))
                {
                    if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace APK?", path + " already exists.", "Replace", "Cancel")) return;
                    string selected = version;
                    var action = build;
                    EditorPrefs.SetString("LuckyDiveCo.BuildVersion." + Application.dataPath + "." + platform, selected);
                    Close();
                    EditorApplication.delayCall += () => action(selected);
                }
            }
            if (GUILayout.Button("Cancel")) Close();
        }
    }
}
