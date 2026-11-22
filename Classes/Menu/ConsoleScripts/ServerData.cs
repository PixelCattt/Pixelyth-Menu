/*
** Pixelyth-Menu - Classes/Menu/ConsoleScripts/ServerData.cs
** An Open-Source Mod Menu for Gorilla Tag with 2000+ Mods!
**
** Copyright (C) 2026 - PixelCatt
** https://github.com/PixelCattt/Pixelyth-Menu
**
** This program is free software: you can redistribute it and/or modify
** it under the terms of the GNU General Public License as published by
** the Free Software Foundation, either version 3 of the License, or
** (at your option) any later version.
**
** This program is distributed in the hope that it will be useful,
** but WITHOUT ANY WARRANTY; without even the implied warranty of
** MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
** See the GNU General Public License for more details.
**
** You should have received a copy of the GNU General Public License
** along with this program.
** If not, see <https://www.gnu.org/licenses/>.
*/

using Photon.Pun;
using Photon.Realtime;
using Pixelyth.Managers;
using Pixelyth.Menu;
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;
using GorillaNetworking;
using System.IO;

namespace Pixelyth.Classes.Menu.ConsoleScripts
{
    public class ServerData : MonoBehaviour
    {
        #region Configuration
        // The Enpoint used for all API Requests.
        public const string ServerEndpoint = "https://gtag.pixelcatt.dev";

        // The URL used for Downloading Console Assets.
        public const string AssetURL = "https://raw.githubusercontent.com/PixelCattt/Console/refs/heads/master/ServerData";

        // The Dictionary used to assign the admins only seen in your Mod.
        public static readonly Dictionary<string, string> LocalAdmins = new Dictionary<string, string>()
        {
            // { "Placeholder Admin UserID", "Placeholder Admin Name" },
        };
        #endregion

        #region Server Data Code
        private static ServerData instance;

        private static readonly List<string> DetectedModsLabelled = new List<string>();

        private static int LoadAttempts;

        private static float DataLoadTime = -1f;
        private static float ReloadTime = -1f;

        public static bool OutdatedVersion;

        private static bool GivenAdminMods;
        private static bool loggedWaitingForConnection;

        private static string LastPollAnswered;

        private static string CurrentPoll = "What goes well with Cheeseburgers?";
        private static string OptionA = "Fries";
        private static string OptionB = "Chips";

        public void Awake()
        {
            instance = this;
            DataLoadTime = Time.time + 5f;
            DataDebug($"ServerData.Awake on '{gameObject.name}' (active={gameObject.activeInHierarchy}, enabled={enabled}); first request scheduled in 5 seconds. Endpoint={ServerEndpoint}.");

            NetworkSystem.Instance.OnJoinedRoomEvent += OnJoinRoom;

            NetworkSystem.Instance.OnPlayerJoined += UpdatePlayerCount;
            NetworkSystem.Instance.OnPlayerLeft += UpdatePlayerCount;

            if (File.Exists($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt"))
                LastPollAnswered = File.ReadAllText($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt");
        }

        public void Update()
        {
            bool connectedToMaster = GorillaComputer.instance != null && GorillaComputer.instance.isConnectedToMaster;
            if (DataLoadTime > 0f && Time.time > DataLoadTime && !connectedToMaster && !loggedWaitingForConnection)
            {
                loggedWaitingForConnection = true;
                DataDebug($"Initial server-data request is waiting for GorillaComputer connection. connected={connectedToMaster}, time={Time.time:F1}, scheduled={DataLoadTime:F1}.");
            }

            if (DataLoadTime > 0f && Time.time > DataLoadTime && connectedToMaster)
            {
                DataLoadTime = Time.time + 5f;
                loggedWaitingForConnection = false;

                LoadAttempts++;
                if (LoadAttempts >= 3)
                {
                    DataDebug($"Initial refresh attempt limit reached at counter={LoadAttempts}; disabling initial retry timer. The 30-second refresh timer may still retry later.");
                    Console.Log("Server Data could not be loaded");
                    DataLoadTime = -1f;
                    return;
                }

                DataDebug($"Starting initial refresh attempt {LoadAttempts}/2 at t={Time.time:F1}.");
                Console.Log("Attempting to load Web Data");
                instance.StartCoroutine(RefreshServerData());
            }

            if (ReloadTime > 0f)
            {
                if (Time.time > ReloadTime)
                {
                    ReloadTime = Time.time + 30f;
                    DataDebug($"Starting scheduled 30-second refresh at t={Time.time:F1}.");
                    instance.StartCoroutine(RefreshServerData());
                }
            }
            else
            {
                if (GorillaComputer.instance.isConnectedToMaster)
                    ReloadTime = Time.time + 5f;
            }

            if (!(Time.time > DataSyncDelay) && NetworkSystem.Instance.InRoom) return;
            if (NetworkSystem.Instance.InRoom && PhotonNetwork.PlayerList.Length != PlayerCount)
            {
                instance.StartCoroutine(PlayerDataSync(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CloudRegion));
                NetworkSystem.Instance.PlayerListOthers.ForEach(p => ShouldWeReport(p.GetPlayerRef()));
            }

            PlayerCount = NetworkSystem.Instance.InRoom ? PhotonNetwork.PlayerList.Length : -1;
        }

        private IEnumerator RefreshServerData()
        {
            DataDebug("RefreshServerData entered.");
            yield return LoadServerData();
            DataDebug("Server-data stage finished; starting report-data stage.");
            yield return LoadReportData();
            DataDebug("Report-data stage finished; starting poll-data stage.");
            yield return LoadPollData();
            DataDebug("RefreshServerData completed all stages.");
        }

        private static void DataDebug(string message) => LogManager.Log($"[ServerData Debug] {message}");

        public static void OnJoinRoom()
        {
            instance.StartCoroutine(TelemetryRequest(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.NickName, PhotonNetwork.CloudRegion, PhotonNetwork.LocalPlayer.UserId, PhotonNetwork.CurrentRoom.IsVisible, PhotonNetwork.PlayerList.Length, NetworkSystem.Instance.GameModeString));
            NetworkSystem.Instance.PlayerListOthers.ForEach(p => ShouldWeReport(p.GetPlayerRef()));
        }

        public static int PlayerCount;
        public static void UpdatePlayerCount(NetPlayer Player)
        {
            NetworkSystem.Instance.PlayerListOthers.ForEach(p => ShouldWeReport(p.GetPlayerRef()));
            PlayerCount = -1;
        }

        public static void ShouldWeReport(Player player)
        {
            if (!reportData.TryGetValue(player.UserId, out var entry))
                return;

            if (Administrators.ContainsKey(player.UserId))
                return;

            lock (entry.ReportedIn)
            {
                if (!entry.ReportedIn.Add(NetworkSystem.Instance.RoomName))
                    return;

                GorillaPlayerScoreboardLine.ReportPlayer(
                    player.UserId,
                    entry.ReportType,
                    player.NickName
                );

                if (Administrators.ContainsKey(PhotonNetwork.LocalPlayer.UserId))
                {
                    Console.SendNotification(
                        $"<color=grey>[</color><color=purple>ARS</color><color=grey>]</color> Player {player.NickName} (also known as {entry.Name}) has been reported for {entry.Reason}.",
                        10000
                    );
                }
            }
        }

        public static bool IsPlayerSteam(VRRig Player)
        {
            string concat = Player._playerOwnedCosmetics.Concat();
            int customPropsCount = Player.Creator.GetPlayerRef().CustomProperties.Count;

            return concat.Contains("S. FIRST LOGIN") ? true : concat.Contains("FIRST LOGIN") || customPropsCount >= 2;
        }

        public static string CleanString(string input, int maxLength = 12)
        {
            input = new string(Array.FindAll(input.ToCharArray(), Utils.IsASCIILetterOrDigit));

            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();
            return input;
        }

        public static string NoASCIIStringCheck(string input, int maxLength = 12)
        {
            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();
            return input;
        }

        public static int VersionToNumber(string version)
        {
            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return -1; // Version must be in 'major.minor.patch' format

            return int.Parse(parts[0]) * 100 + int.Parse(parts[1]) * 10 + int.Parse(parts[2]);
        }

        public static IEnumerator TelemetryRequest(string directory, string identity, string region, string userid, bool isPrivate, int playerCount, string gameMode)
        {
            UnityWebRequest request = new UnityWebRequest($"{ServerEndpoint}/telemetry", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                directory = CleanString(directory),
                identity = CleanString(identity),
                region = CleanString(region, 3),
                userid = CleanString(userid, 20),
                isPrivate,
                playerCount,
                gameMode = CleanString(gameMode, 128),
                consoleVersion = Console.ConsoleVersion,
                menuName = Console.ModName,
                menuVersion = Console.ModVersion
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }

        public static IEnumerator ReportFailureMessage(string error)
        {
            List<string> enabledMods = new List<string>();

            int categoryIndex = 0;
            foreach (ButtonInfo[] category in Buttons.buttons)
            {
                enabledMods.AddRange(from button in category where button.enabled && !Buttons.categoryNames[categoryIndex].Contains("Settings") select NoASCIIStringCheck(Main.NoRichtextTags(button.overlapText ?? button.buttonText), 128));

                categoryIndex++;
            }

            AchievementManager.UnlockAchievement(new AchievementManager.Achievement
            {
                name = "Purgatory",
                description = "Get banned with the menu.",
                icon = "Images/Achievements/banned.png"
            });

            UnityWebRequest request = new UnityWebRequest(ServerEndpoint + "/reportban", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                error = NoASCIIStringCheck(error, 512),
                version = PluginInfo.Version,
                data = enabledMods
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }

        private static bool ShownPrompt;

        public static readonly Dictionary<string, string> Administrators = new Dictionary<string, string>();
        public static readonly List<string> SuperAdministrators = new List<string>();
        public static readonly List<string> Owners = new List<string>();
        public static IEnumerator LoadServerData()
        {
            using (UnityWebRequest request = UnityWebRequest.Get($"{ServerEndpoint}/serverdata"))
            {
                request.timeout = 15;
                float requestStartedAt = Time.realtimeSinceStartup;
                DataDebug($"GET /serverdata starting (timeout={request.timeout}s).");
                yield return request.SendWebRequest();
                DataDebug($"GET /serverdata completed after {Time.realtimeSinceStartup - requestStartedAt:F2}s: result={request.result}, status={request.responseCode}, error='{request.error}', bytes={request.downloadedBytes}.");

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DataDebug($"GET /serverdata failed. Response body length={request.downloadHandler?.text?.Length ?? 0}.");
                    Console.Log($"Failed to load server data:\nError: {request.error}\nResult: {request.result}\nResponse Code: {request.responseCode}\nBody (if any): {request.downloadHandler?.text}");
                    if (CustomBoardManager.motdTemplate == "\n\n<align=\"center\"><b>Loading...</b></align>")
                    {
                        CustomBoardManager.motdTemplate = "\n\n<align=\"center\"><b>Server data unavailable</b></align>";
                        CustomBoardManager.motdTextDirty = true;
                    }
                    yield break;
                }

                string json = request.downloadHandler.text;
                DataDebug($"GET /serverdata returned a body with {json?.Length ?? 0} characters.");
                DataLoadTime = -1f;

                JObject data = null;
                try
                {
                    data = JObject.Parse(json);
                    DataDebug($"Server-data JSON parsed. Keys: {string.Join(", ", data.Properties().Select(property => property.Name))}.");
                }
                catch (Exception exception)
                {
                    DataDebug($"Server-data JSON parse threw {exception.GetType().Name}: {exception.Message}. Body preview: '{(json ?? string.Empty).Substring(0, Math.Min(json?.Length ?? 0, 240))}'.");
                    Console.Log($"Failed to parse server data: {exception.Message}");
                }

                if (data == null)
                {
                    Console.Log($"Failed to parse server data: JSON Response is empty or null");
                    yield break;
                }

                Console.Log($"Loaded server data from {ServerEndpoint}/serverdata (HTTP {request.responseCode}).");

                Main.serverLink = (string)data["discord-invite"];
                string receivedMotd = (string)data["motd"];
                DataDebug($"Server-data fields read: discordInvitePresent={!string.IsNullOrEmpty(Main.serverLink)}, motdPresent={!string.IsNullOrEmpty(receivedMotd)}, motdLength={receivedMotd?.Length ?? 0}.");
                if (string.IsNullOrEmpty(receivedMotd))
                    DataDebug("WARNING: server-data JSON had no usable 'motd' string; board template remains unchanged.");
                if (CustomBoardManager.motdTemplate != receivedMotd && !string.IsNullOrEmpty(receivedMotd))
                {
                    int previousMotdLength = CustomBoardManager.motdTemplate?.Length ?? 0;
                    CustomBoardManager.motdTextDirty = true;
                    CustomBoardManager.motdTemplate = receivedMotd;
                    DataDebug($"MOTD template replaced; dirty=true, previousLength={previousMotdLength}, newLength={receivedMotd.Length}.");
                }
                else
                    DataDebug($"MOTD template unchanged; dirty={CustomBoardManager.motdTextDirty}, currentLength={CustomBoardManager.motdTemplate?.Length ?? 0}.");

                // Version Check
                string minimumVersion = (string)data["min-menu-version"];
                string version = (string)data["menu-version"];
                DataDebug($"Menu version fields: local={PluginInfo.Version}, minimum={minimumVersion}, remote={version}.");

                if (VersionToNumber(PluginInfo.Version) < VersionToNumber(minimumVersion))
                {
                    if (!OutdatedVersion)
                    {
                        OutdatedVersion = true;
                        Console.Log("Version is severely outdated");
                        GorillaComputer.instance.GeneralFailureMessage("Please update your menu. For safety purposes, you have been blocked from joining rooms.");
                        if (NetworkSystem.Instance.InRoom)
                            NetworkSystem.Instance.ReturnToSinglePlayer();
                        Console.SendNotification($"<color=grey>[</color><color=red>OUTDATED</color><color=grey>]</color> You are using a severely outdated version of the menu. Please update your menu if available. For safety purposes, you have been blocked from joining rooms.", 10000);
                        Main.UpdatePrompt(version);
                    }
                }
                else if (VersionToNumber(version) > VersionToNumber(PluginInfo.Version))
                {
                    if (!OutdatedVersion)
                    {
                        OutdatedVersion = true;
                        Console.Log("Version is outdated");
                        Console.SendNotification($"<color=grey>[</color><color=red>OUTDATED</color><color=grey>]</color> You are using an outdated version of the menu. Please update to version {version}.", 10000);
                        Main.UpdatePrompt(version);
                        ShownPrompt = true;
                    }
                }

                string minConsoleVersion = (string)data["min-console-version"];
                int installedConsoleVersion = VersionToNumber(Console.ConsoleVersion);
                int requiredConsoleVersion = VersionToNumber(minConsoleVersion);
                DataDebug($"Console version gate: installed={Console.ConsoleVersion} ({installedConsoleVersion}), required={minConsoleVersion} ({requiredConsoleVersion}), willLoadAdmins={installedConsoleVersion >= requiredConsoleVersion}.");
                Console.Log($"Console version gate: installed={Console.ConsoleVersion}, required={minConsoleVersion}, parsed installed={installedConsoleVersion}, parsed required={requiredConsoleVersion}");
                if (installedConsoleVersion >= requiredConsoleVersion)
                    yield return LoadAdminData();
                else
                    Console.Log("Extremely outdated Version of Console, not loading Administrators");

                // Detected Mods
                JArray detectedMods = (JArray)data["detected-mods"];
                DataDebug($"Detected-mod list: {(detectedMods == null ? "missing/null" : detectedMods.Count.ToString())} item(s).");
                if (detectedMods == null || detectedMods.Count() == 0)
                    yield break;

                foreach (var detectedMod in detectedMods)
                {
                    string detectedModName = detectedMod.ToString();
                    if (DetectedModsLabelled.Contains(detectedModName)) continue;
                    ButtonInfo button = Buttons.GetIndex(detectedModName);
                    if (button != null)
                    {
                        string overlapText = button.overlapText ?? button.buttonText;

                        button.overlapText = overlapText + " <color=grey>[</color><color=red>Disabled</color><color=grey>]</color>";
                        if (!Administrators.TryGetValue(PhotonNetwork.LocalPlayer.UserId ?? PlayFabAuthenticator.instance.GetPlayFabPlayerId(), out _))
                        {
                            button.isTogglable = false;
                            button.SetEnabled(false);

                            button.method = delegate { Console.SendNotification("<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> This mod is currently disabled, as it is detected."); };
                            button.enableMethod = button.method;
                            button.disableMethod = button.method;
                        }
                    }
                    DetectedModsLabelled.Add(detectedModName);
                }
            }

            yield return null;
        }

        public static IEnumerator LoadAdminData()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{ServerEndpoint}/admindata");
            request.timeout = 15;
            float requestStartedAt = Time.realtimeSinceStartup;
            DataDebug($"GET /admindata starting (timeout={request.timeout}s).");

            yield return request.SendWebRequest();
            DataDebug($"GET /admindata completed after {Time.realtimeSinceStartup - requestStartedAt:F2}s: result={request.result}, status={request.responseCode}, error='{request.error}', bytes={request.downloadedBytes}.");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Console.Log($"Failed to load admin data:\nError: {request.error}\nResult: {request.result}\nResponse Code: {request.responseCode}");
                yield break;
            }

            string json = request.downloadHandler.text;
            DataDebug($"GET /admindata returned a body with {json?.Length ?? 0} characters.");
            JObject adminData = null;
            try
            {
                adminData = JObject.Parse(json);
                DataDebug($"Admin-data JSON parsed. Keys: {string.Join(", ", adminData.Properties().Select(property => property.Name))}.");
            }
            catch (Exception exception)
            {
                DataDebug($"Admin-data JSON parse threw {exception.GetType().Name}: {exception.Message}.");
                Console.Log($"Failed to parse admin data: {exception.Message}");
            }

            if (adminData == null)
            {
                Console.Log("Failed to parse admin data: response was empty or null.");
                yield break;
            }

            Console.Log($"Loaded admin data from {ServerEndpoint}/admindata (HTTP {request.responseCode}).");

            // Admin Dictionary
            Administrators.Clear();

            JArray admins = adminData["admins"] as JArray ?? new JArray();
            DataDebug($"Admin records received: {admins.Count}; starting dictionary build.");
            foreach (var admin in admins)
            {
                string name = admin?["name"]?.ToString();
                string userId = admin?["user-id"]?.ToString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(userId))
                {
                    DataDebug("Skipped admin entry because name or user-id was empty.");
                    continue;
                }
                Administrators[userId] = name;
            }

            // Super Admin Dictionary
            SuperAdministrators.Clear();

            JArray superAdmins = adminData["super-admins"] as JArray ?? new JArray();
            DataDebug($"Super-admin records received: {superAdmins.Count}.");
            foreach (var superAdmin in superAdmins)
                SuperAdministrators.Add(superAdmin.ToString());

            // Owner Dictionary
            Owners.Clear();

            JArray owners = adminData["owners"] as JArray ?? new JArray();
            DataDebug($"Owner records received: {owners.Count}.");
            foreach (var owner in owners)
                Owners.Add(owner.ToString());

            // Add Local Admins
            foreach (var localAdmin in LocalAdmins)
            {
                Administrators[localAdmin.Key] = localAdmin.Value;

                SuperAdministrators.Add(localAdmin.Value);

                Owners.Add(localAdmin.Value);
            }

            string localUserId = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.UserId : null;
            DataDebug($"Admin dictionaries built: admins={Administrators.Count}, superAdmins={SuperAdministrators.Count}, owners={Owners.Count}; localUserIdPresent={!string.IsNullOrEmpty(localUserId)}, recognized={(!string.IsNullOrEmpty(localUserId) && Administrators.ContainsKey(localUserId))}, idSuffix={(localUserId?.Length >= 4 ? localUserId.Substring(localUserId.Length - 4) : "n/a")}.");
            Console.Log($"Admin data loaded: admins={Administrators.Count}, super-admins={SuperAdministrators.Count}, owners={Owners.Count}, local user id present={!string.IsNullOrEmpty(localUserId)}, local user recognized={(!string.IsNullOrEmpty(localUserId) && Administrators.ContainsKey(localUserId))}.");

            // Spawn Admin-Panel
            if (!GivenAdminMods && !string.IsNullOrEmpty(localUserId) && Administrators.TryGetValue(localUserId, out var administrator))
            {
                GivenAdminMods = true;
                Console.SetupAdminPanel(administrator);
                Console.Log($"Admin panel enabled for {administrator}.");
                DataDebug($"Admin panel setup completed for '{administrator}'.");
            }
            else
                DataDebug($"Admin panel not set up: alreadyGiven={GivenAdminMods}, localIdPresent={!string.IsNullOrEmpty(localUserId)}, idRecognized={(!string.IsNullOrEmpty(localUserId) && Administrators.ContainsKey(localUserId))}.");
        }

        public static readonly Dictionary<string, ReportEntry> reportData = new Dictionary<string, ReportEntry>();
        public class ReportEntry
        {
            public string Name;
            public string UserID;
            public GorillaPlayerLineButton.ButtonType ReportType;
            public string Reason;
            public HashSet<string> ReportedIn = new HashSet<string>();
        }

        private IEnumerator LoadReportData()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{ServerEndpoint}/reportdata");
            request.timeout = 15;
            float requestStartedAt = Time.realtimeSinceStartup;
            DataDebug($"GET /reportdata starting (timeout={request.timeout}s).");

            yield return request.SendWebRequest();
            DataDebug($"GET /reportdata completed after {Time.realtimeSinceStartup - requestStartedAt:F2}s: result={request.result}, status={request.responseCode}, error='{request.error}', bytes={request.downloadedBytes}.");

            if (request.result != UnityWebRequest.Result.Success)
            {
                DataDebug("Report-data stage stopped because the request failed.");
                yield break;
            }

            try
            {
                reportData.Clear();

                JObject json = JObject.Parse(request.downloadHandler.text);
                DataDebug($"Report-data JSON parsed; bodyLength={request.downloadHandler.text.Length}.");
                if (json == null)
                    yield break;

                JArray reports = (JArray)json["reports"];
                DataDebug($"Report-data array contains {(reports == null ? "null" : reports.Count.ToString())} records.");
                if (reports == null || reports.Count() == 0)
                    yield break;

                foreach (JToken report in reports)
                {
                    string name = report["name"]?.ToString() ?? "Unknown";
                    string userId = report["user-id"]?.ToString() ?? "Unknown";

                    if (!(report["report-type"]?.Value<int>() >= 1 && report["report-type"]?.Value<int>() <= 3))
                        continue;

                    ReportEntry entry = new ReportEntry
                    {
                        Name = name,
                        UserID = userId,
                        ReportType = report["report-type"]?.Value<int>() switch
                        {
                            1 => GorillaPlayerLineButton.ButtonType.Cheating,
                            2 => GorillaPlayerLineButton.ButtonType.Toxicity,
                            3 => GorillaPlayerLineButton.ButtonType.HateSpeech,
                            _ => GorillaPlayerLineButton.ButtonType.Cancel
                        },
                        Reason = report["reason"]?.ToString() ?? "NONE",
                    };

                    if (reportData.TryGetValue(userId, out var existing))
                        entry.ReportedIn = existing.ReportedIn;

                    reportData[userId] = entry;
                }

                if (NetworkSystem.Instance.InRoom)
                    NetworkSystem.Instance.PlayerListOthers.ForEach(p => ShouldWeReport(p.GetPlayerRef()));
                DataDebug($"Report-data processing completed; loaded {reportData.Count} records.");
            }
            catch (Exception exception)
            {
                DataDebug($"Report-data parsing/processing failed: {exception}.");
            }
        }

        private IEnumerator LoadPollData()
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{ServerEndpoint}/polldata");
            request.timeout = 15;
            float requestStartedAt = Time.realtimeSinceStartup;
            DataDebug($"GET /polldata starting (timeout={request.timeout}s).");

            yield return request.SendWebRequest();
            DataDebug($"GET /polldata completed after {Time.realtimeSinceStartup - requestStartedAt:F2}s: result={request.result}, status={request.responseCode}, error='{request.error}', bytes={request.downloadedBytes}.");

            if (request.result != UnityWebRequest.Result.Success)
            {
                DataDebug("Poll-data stage stopped because the request failed.");
                yield break;
            }

            string json = request.downloadHandler.text;
            JObject pollData;
            Exception parseException = null;
            try
            {
                pollData = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                parseException = exception;
                pollData = null;
            }
            if (parseException != null)
            {
                DataDebug($"Poll-data JSON parse failed: {parseException}.");
                yield break;
            }

            CurrentPoll = (string)pollData["text"];
            OptionA = (string)pollData["option-a"];
            OptionB = (string)pollData["option-b"];
            DataDebug($"Poll data applied: questionPresent={!string.IsNullOrEmpty(CurrentPoll)}, optionA present={!string.IsNullOrEmpty(OptionA)}, optionB present={!string.IsNullOrEmpty(OptionB)}.");

            if (!Bootstrapper.FirstLaunch && LastPollAnswered != CurrentPoll)
            {
                if (!ShownPrompt)
                {
                    Main.Prompt(CurrentPoll, () => CoroutineManager.instance.StartCoroutine(SendVote("a-votes")), () => CoroutineManager.instance.StartCoroutine(SendVote("b-votes")), OptionA, OptionB);
                    Console.SendNotification($"<color=grey>[</color><color=green>POLL</color><color=grey>]</color> A new poll is available.", 10000);
                }

                LastPollAnswered = CurrentPoll;
                File.WriteAllText($"{PluginInfo.BaseDirectory}/LastPollAnswered.txt", CurrentPoll);
            }
        }

        public static IEnumerator SendVote(string category)
        {
            UnityWebRequest request = new UnityWebRequest($"{ServerEndpoint}/sendvote", "POST");

            string json = JsonConvert.SerializeObject(new { option = category });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) yield break;
            try
            {
                string responseText = request.downloadHandler.text;
                Dictionary<string, object> responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                int avotes = Convert.ToInt32(responseJson["a-votes"]);
                int bvotes = Convert.ToInt32(responseJson["b-votes"]);

                int total = avotes + bvotes;

                string result;
                if (total > 0)
                {
                    double aPercent = (double)avotes / total * 100;
                    double bPercent = (double)bvotes / total * 100;

                    result = $"Total Votes: {total}\n{OptionA}: {aPercent:F2}%\n{OptionB}: {bPercent:F2}%";
                }
                else
                    result = "No votes yet.";

                Main.PromptSingle(result);
            }
            catch { }
        }

        private static float DataSyncDelay;
        public static IEnumerator PlayerDataSync(string directory, string region)
        {
            DataSyncDelay = Time.time + 5f;
            yield return new WaitForSeconds(5f);

            if (!NetworkSystem.Instance.InRoom)
                yield break;

            Dictionary<string, Dictionary<string, string>> data = new Dictionary<string, Dictionary<string, string>>();

            foreach (Player identification in PhotonNetwork.PlayerList)
            {
                VRRig rig = Console.GetVRRigFromPlayer(identification) ?? VRRig.LocalRig;
                data.Add(identification.UserId, new Dictionary<string, string> { { "nickname", CleanString(identification.NickName) }, { "cosmetics", rig._playerOwnedCosmetics.Concat() }, { "color", $"{Math.Round(rig.playerColor.r * 255)} {Math.Round(rig.playerColor.g * 255)} {Math.Round(rig.playerColor.b * 255)}" }, { "platform", IsPlayerSteam(rig) ? "STEAM" : "QUEST" } });
            }

            UnityWebRequest request = new UnityWebRequest(ServerEndpoint + "/syncdata", "POST");

            string json = JsonConvert.SerializeObject(new
            {
                directory = CleanString(directory),
                region = CleanString(region, 3),
                data
            });

            byte[] raw = Encoding.UTF8.GetBytes(json);

            request.uploadHandler = new UploadHandlerRaw(raw);
            request.SetRequestHeader("Content-Type", "application/json");

            request.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();
        }
        #endregion
    }
}
