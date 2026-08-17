using System.IO;
using UnityEngine;

namespace HttpGuard
{
    public class HttpGuardGui : MonoBehaviour
    {
        private const string HideFileName = "hide_gui.txt";

        private bool showMain = true;
        private bool showConfirm;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle confirmLabelStyle;

        private void Awake()
        {
            string hideFile = Path.Combine(Plugin.GuardFolderPath, HideFileName);
            if (File.Exists(hideFile))
            {
                showMain = false;
                enabled = false;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };

            confirmLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17
            };
        }

        private void OnGUI()
        {
            if (!showMain) return;

            EnsureStyles();

            if (showConfirm)
            {
                DrawConfirmWindow();
                return;
            }

            DrawMainWindow();
        }

        private void DrawMainWindow()
        {
            float w = 660f;
            float h = 430f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            GUI.Label(new Rect(x, y + 15, w, 40), "AtlasHttpGuard", titleStyle);

            GUI.Label(new Rect(x + 25, y + 75, w - 50, 250),
                "Trusted Mods:\n" +
                "Drop a mod .dll into the 'Trusted Mods' folder and HttpGuard will completely ignore it.\n" +
                "(You still need the mod in BepInEx/plugins for it to load.)\n\n" +
                "Trusted Links:\n" +
                "Add URLs or domains to TrustedLinks.txt to make HttpGuard automatically accept them.\n" +
                "One per line. Lines starting with '#' are comments.",
                labelStyle);

            if (GUI.Button(new Rect(x + 150, y + 350, 360, 45), "Open HttpGuard Folder", buttonStyle))
            {
                OpenGuardFolder();
            }

            if (GUI.Button(new Rect(x + 520, y + 350, 115, 45), "Close", buttonStyle))
            {
                showConfirm = true;
            }
        }

        private void DrawConfirmWindow()
        {
            float w = 520f;
            float h = 230f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            GUI.Label(new Rect(x + 20, y + 25, w - 40, 60),
                "Hide the HttpGuard info window forever?",
                confirmLabelStyle);

            if (GUI.Button(new Rect(x + 60, y + 130, 180, 55), "Close", buttonStyle))
            {
                showMain = false;
                showConfirm = false;
            }

            if (GUI.Button(new Rect(x + 280, y + 130, 180, 55), "Hide Forever", buttonStyle))
            {
                SaveHideForever();
                showMain = false;
                showConfirm = false;
                enabled = false;
            }
        }

        private void OpenGuardFolder()
        {
            try
            {
                System.Diagnostics.Process.Start(Plugin.GuardFolderPath);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to open folder: {ex.Message}");
            }
        }

        private void SaveHideForever()
        {
            try
            {
                Directory.CreateDirectory(Plugin.GuardFolderPath);
                File.WriteAllText(Path.Combine(Plugin.GuardFolderPath, HideFileName), "hidden");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to save hide setting: {ex.Message}");
            }
        }
    }
}
