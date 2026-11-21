using Pixelyth.Managers;
using GorillaNetworking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Pixelyth.Menu.Main;

namespace Pixelyth.Mods
{
    public static class Watchdog
    {
        private static readonly HashSet<string> watchlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;
        private static float nextCheck;
        private static string FilePath => Path.Combine(PluginInfo.BaseDirectory, "Watchlist.txt");

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (File.Exists(FilePath))
                    foreach (string raw in File.ReadAllLines(FilePath))
                    {
                        string value = raw.Trim();
                        if (value.Length > 0 && !value.StartsWith("#")) watchlist.Add(value);
                    }
            }
            catch (Exception ex) { LogManager.LogError($"Failed to load Watchdog list: {ex.Message}"); }
        }

        private static void Save()
        {
            try { File.WriteAllLines(FilePath, watchlist.OrderBy(value => value)); }
            catch (Exception ex) { LogManager.LogError($"Failed to save Watchdog list: {ex.Message}"); }
        }

        public static void WatchdogMarkAll()
        {
            EnsureLoaded();
            if (!NetworkSystem.Instance.InRoom) return;
            foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
            {
                if (!string.IsNullOrWhiteSpace(player.UserId)) watchlist.Add(player.UserId);
                else if (!string.IsNullOrWhiteSpace(player.NickName)) watchlist.Add(player.NickName);
            }
            Save();
            NotificationManager.SendNotification($"[WATCHDOG] Added room players to watchlist. Total: {watchlist.Count}.");
        }

        public static void WatchdogClear()
        {
            EnsureLoaded();
            watchlist.Clear();
            Save();
            NotificationManager.SendNotification("[WATCHDOG] Watchlist cleared.");
        }

        public static void WatchdogAutoLeave()
        {
            EnsureLoaded();
            if (!NetworkSystem.Instance.InRoom || watchlist.Count == 0 || Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;

            foreach (NetPlayer player in NetworkSystem.Instance.PlayerListOthers)
            {
                bool flagged = (!string.IsNullOrEmpty(player.UserId) && watchlist.Contains(player.UserId))
                    || (!string.IsNullOrEmpty(player.NickName) && watchlist.Contains(player.NickName));
                if (!flagged) continue;

                string name = string.IsNullOrEmpty(player.NickName) ? player.UserId : player.NickName;
                NetworkSystem.Instance.ReturnToSinglePlayer();
                RPCProtection();
                NotificationManager.SendNotification($"[WATCHDOG] {name} is on your watchlist; disconnected.");
                return;
            }
        }
    }
}
