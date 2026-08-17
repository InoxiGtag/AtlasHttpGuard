using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HttpGuard;

[BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
public class Plugin : BaseUnityPlugin
{
    private const string ListUrl = "https://raw.githubusercontent.com/InoxiGtag/AtlasInfo-ForDevs/refs/heads/main/AtlasLinksTrusted";
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase);

    private async void Start()
    {
        new Harmony("com.inoxi.gtag.httpguard").PatchAll();
        await LoadAllowlistAsync();
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
