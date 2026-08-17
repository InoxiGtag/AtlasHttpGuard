using AtlasHttpGuard;
using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HttpGuard
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        private const string ListUrl = "https://raw.githubusercontent.com/InoxiGtag/AtlasInfo-ForDevs/refs/heads/main/AtlasLinksTrusted";

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

        private void Start()
        {
            try
            {
                Debug.Log("[HttpGuard] Initializing patches...");
                instance = this;
                HarmonyPatches.ApplyHarmonyPatches();

                _ = LoadAllowlistAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HttpGuard] Failed to initialize: {ex.Message}");
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

        public static bool CheckRequest(string url)
        {
            Debug.Log($"[HttpGuard] CHECKING -> {url}");

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
