using AtlasHttpGuard;
using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HttpGuard
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        private const string ListUrl = "https://raw.githubusercontent.com/InoxiGtag/AtlasInfo-ForDevs/refs/heads/main/AtlasLinksTrusted";

        private const string TrustedModsFolderName = "Trusted Mods";
        private const string TrustedLinksFolderName = "Trusted Links";
        private const string TrustedLinksFileName = "TrustedLinks.txt";

        public static Plugin instance;

        private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
          // HttpGuard / Atlas resources
          "seralyth.software",

          // Gorilla Tag official services
          "gtag-cf.com",
          "mmr-prod.gtag-cf.com",
          "auth-prod.gtag-cf.com",
          "temp-prod.gtag-cf.com",
          "hppromo-prod.gtag-cf.com",
          "iap.gtag-cf.com",
          "kid-prod.gtag-cf.com",
          "prog-prod.gtag-cf.com",
          "title-data.gtag-cf.com",
          "voting-prod.gtag-cf.com",
          "aa-mothership.com",
          "mothership.gg",
          "modapi.io",
          "mod.io",
          "gtagmods.com",
          "playfabapi.com",
          "photonengine.com",
          "steamcommunity.com",
          "steamstatic.com",
          "thumb.modcdn.io",
        };

        private static readonly HashSet<string> TrustedModNames = new(StringComparer.OrdinalIgnoreCase);

        public static string GuardFolderPath => Path.Combine(Paths.GameRootPath, "AtlasHttpGuard");
        private static string TrustedModsPath => Path.Combine(GuardFolderPath, TrustedModsFolderName);
        private static string TrustedLinksPath => Path.Combine(GuardFolderPath, TrustedLinksFolderName);
        private static string TrustedLinksFilePath => Path.Combine(TrustedLinksPath, TrustedLinksFileName);

        private void Start()
        {
            try
            {
                SetupFolders();
                LoadTrustedMods();
                LoadTrustedLinks();

                Debug.Log("[HttpGuard] Initializing patches...");
                instance = this;
                HarmonyPatches.ApplyHarmonyPatches();

                gameObject.AddComponent<HttpGuardGui>();

                _ = LoadAllowlistAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to initialize: {ex.Message}");
            }
        }

        private static void SetupFolders()
        {
            try
            {
                Directory.CreateDirectory(GuardFolderPath);
                Directory.CreateDirectory(TrustedModsPath);
                Directory.CreateDirectory(TrustedLinksPath);

                string modsReadme = Path.Combine(TrustedModsPath, "READ_ME.txt");
                if (!File.Exists(modsReadme))
                {
                    File.WriteAllText(modsReadme,
                        "Any .dll mod you put in this 'Trusted Mods' folder will automatically NOT be checked by AtlasHttpGuard at all - " +
                        "AtlasHttpGuard will simply ignore it." + Environment.NewLine + Environment.NewLine +
                        "You still need to put the mod in your BepInEx/plugins folder as well for it to actually load.");
                }

                string linksReadme = Path.Combine(TrustedLinksPath, "READ_ME.txt");
                if (!File.Exists(linksReadme))
                {
                    File.WriteAllText(linksReadme,
                        "AtlasHttpGuard will automatically accept/ignore every link (URL or domain) you add to the " +
                        "TrustedLinks.txt file in this same folder." + Environment.NewLine + Environment.NewLine +
                        "Put one URL or domain per line. Lines starting with '#' are treated as comments.");
                }

                if (!File.Exists(TrustedLinksFilePath))
                {
                    File.WriteAllText(TrustedLinksFilePath,
                        "# Add one URL or domain per line below to make AtlasHttpGuard automatically accept/ignore it." + Environment.NewLine +
                        "# Example:" + Environment.NewLine +
                        "# https://example.com/api" + Environment.NewLine +
                        "# api.example.com");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to create folders/files: {ex.Message}");
            }
        }

        private static void LoadTrustedMods()
        {
            try
            {
                if (!Directory.Exists(TrustedModsPath)) return;

                foreach (var file in Directory.GetFiles(TrustedModsPath, "*.dll"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name))
                        TrustedModNames.Add(name);
                }

                if (TrustedModNames.Count > 0)
                    Debug.Log($"[HttpGuard] {TrustedModNames.Count} trusted mod(s) registered.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to load trusted mods: {ex.Message}");
            }
        }

        private static void LoadTrustedLinks()
        {
            try
            {
                if (!File.Exists(TrustedLinksFilePath)) return;

                foreach (var line in File.ReadAllLines(TrustedLinksFilePath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                    string host = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.Host : trimmed;
                    if (!string.IsNullOrEmpty(host))
                        AllowedHosts.Add(host);
                }

                Debug.Log($"[HttpGuard] TrustedLinks.txt loaded ({AllowedHosts.Count} total allowed domain(s)).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to load TrustedLinks.txt: {ex.Message}");
            }
        }

        private async Task LoadAllowlistAsync()
        {
            Debug.Log("[HttpGuard] Loading trusted links list...");
            try
            {
                using var wc = new WebClient();
                string content = await wc.DownloadStringTaskAsync(ListUrl);

                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                    string host = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ? uri.Host : trimmed;
                    AllowedHosts.Add(host);
                }

                AllowedHosts.Add("raw.githubusercontent.com");
                Debug.Log($"[HttpGuard] Allowlist LOADED with {AllowedHosts.Count} allowed domain(s).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to load allowlist: {ex.Message}");
            }
        }

        private static bool IsCallerTrusted()
        {
            try
            {
                var stack = new System.Diagnostics.StackTrace(false);
                foreach (var frame in stack.GetFrames())
                {
                    var method = frame.GetMethod();
                    var assembly = method?.DeclaringType?.Assembly;
                    if (assembly == null) continue;
                    if (assembly == typeof(Plugin).Assembly) continue;
                    if (TrustedModNames.Contains(assembly.GetName().Name)) return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool CheckRequest(string url)
        {
            Debug.Log($"[HttpGuard] CHECKING -> {url}");

            if (IsCallerTrusted())
            {
                Debug.Log($"[HttpGuard] ACCEPTED (trusted mod) -> {url}");
                return true;
            }

            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Debug.LogWarning($"[HttpGuard] REJECTED (Invalid URL) -> {url}");
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeFile)
            {
                Debug.Log($"[HttpGuard] ACCEPTED (local file) -> {url}");
                return true;
            }

            string host = uri.Host;
            bool isAllowed = AllowedHosts.Contains(host) || AllowedHosts.Any(a => host.EndsWith("." + a, StringComparison.OrdinalIgnoreCase));

            if (isAllowed)
                Debug.Log($"[HttpGuard] ACCEPTED -> {url}");
            else
                Debug.LogWarning($"[HttpGuard] REJECTED -> {url}");

            return isAllowed;
        }

        [HarmonyPatch(typeof(HttpWebRequest), nameof(HttpWebRequest.GetResponse))]
        private static class Patch_HttpWebRequest
        {
            private static bool Prefix(HttpWebRequest __instance)
            {
                if (!CheckRequest(__instance.RequestUri.AbsoluteUri))
                    throw new WebException($"HTTP request blocked by HttpGuard: {__instance.RequestUri}", WebExceptionStatus.RequestCanceled);

                return true;
            }
        }

        [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.SendWebRequest))]
        private static class Patch_UnityWebRequest
        {
            private static bool Prefix(UnityWebRequest __instance)
            {
                if (!CheckRequest(__instance.url))
                {
                    __instance.Abort();
                    return false;
                }

                return true;
            }
        }
    }
}
