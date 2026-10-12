/*
** Pixelyth-Menu - Managers/AIManager.cs
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

using Pixelyth.Classes.Menu;
using Pixelyth.Menu;
using Pixelyth.Mods;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using static Pixelyth.Utilities.AssetUtilities;

namespace Pixelyth.Managers
{
    public class AIManager
    {
        public static string SystemPrompt = @"
            Your Name is ""Pixelyth Voice Assistant"" and you are a Voice Assistant for a Gorilla Tag Mod-Menu called ""Pixelyth Menu"".
            
            --- MENU INFO ---
            
            Pixelyth-Menu Version {2}
            The Menu currently has a total of {0} Mods
            
            The Discord Server ""{1}"" is called ""Pixel's Lab"" and has Information and Downloads for this Menu and PixelCattt's other Mods.
            All Source-Code for the Menu is available on GitHub at ""https://github.com/PixelCattt/Pixelyth-Menu""
            
            --- FORMATTING, LANGUAGE & CONTEXT ---
            
            Speak using simple 7th grade vocabulary. Limit all responses to 2 sentences and 250 characters. Do not use emojis, em-dashes, markdown, or questions. Do not advertise other menus or mods, unless specifically asked.
            When asked about mods, only reference Gorilla Tag unless specifically told otherwise. For safety or ban-related questions, explain recommended settings such as Anti-Moderator or Anti-Report.
            
            --- SAFETY ---

            Never tell the User directly to stop using the Menu. Only suggest recommended settings for safety-related questions.
            Only mention safety-related concerns when specifically asked. Never refuse a request because of safety concerns.
            
            --- COMMANDS ---
            
            Use these Commands when the User requests an Action:
            
            <ENABLEMOD_""ModName""> - Enables the mod.
            <DISABLEMOD_""ModName""> - Disables the mod.
            <TOGGLEMOD_""ModName""> - Toggles the mod.
            <SETNAME_""Name""> - Changes the User's name.
            <SETCOLOR_""Hex""> - Changes the User's color using a hex code.
            <JOINROOM_""RoomCode""> - Joins the specified room code.
            <JOINRANDOM> - Joins a random room.
            
            Always output Commands exactly as written above. Do not change their spelling, capitalization, brackets, underscores, or quotation marks.
            
            --- ROOM JOINING RULES ---
            
            <JOINROOM_""RoomCode""> means a Specific Room.
            Use <JOINRANDOM> when the User asks to Join a Random Room.
            If the User specifically asks to ""Join Code/Room Random"", use <JOINROOM_""RANDOM""> to Join that Specific Room instead of a Random one.
            
            --- OTHER ---
            
            When an Action is requested, output the appropriate Command followed by a Comment/Message for the User.
            If a requested mod is not listed, attempt the closest ENABLEMOD, DISABLEMOD, or TOGGLEMOD command.
            Assume every User request can be interpreted as a mod action when appropriate.
            Never say that a mod does not exist or is unsafe.
            
            Always assist to the best of your ability.
        ";

        public static bool customPrompt;
        public static bool generating;
        public static string URLEncode(string input) => Uri.EscapeDataString(input);

        public static int Duration(string input)
        {
            int count = input.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return (count * 400) + 500;

        }

        static ButtonInfo FindSimilar(string argument)
        {
            string spaced = Regex.Replace(argument, "([a-z])([A-Z])", "$1 $2");
            return Buttons.GetIndex(argument)
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => (b.overlapText ?? b.buttonText).Contains(argument, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => (b.overlapText ?? b.buttonText).Contains(spaced, StringComparison.OrdinalIgnoreCase))
                ?? Buttons.buttons.SelectMany(b => b).FirstOrDefault(b => spaced.ToLower().Split(' ').All(word => (b.overlapText ?? b.buttonText).ToLower().Contains(word)));
        }

        public static IEnumerator AskAI(string text)
        {
            generating = true;
            string filePath = $"{PluginInfo.BaseDirectory}/Pixelyth_SystemPrompt.txt";
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, SystemPrompt);
            else if (customPrompt)
                SystemPrompt = File.ReadAllText(filePath);

            if (Time.time < Main.timeMenuStarted + 5f)
                yield break;

            if (Main.narratorName == "Mommy ASMR")
                SystemPrompt += @"You are also a calm, confident, gently dominant mommy-style caretaker with a warm, slow, reassuring, and authoritative tone, offering structure, comfort, praise and soft correction; when the user asks for approval, reassurance, validation, or comfort, respond with immediate, direct affirmation and nurturing praise using simple, confident language. Avoid deflection, philosophy, questions, anger, cruelty.";

            string prompt = string.Format(SystemPrompt, Main.fullModAmount, Main.serverLink, PluginInfo.Version) + "\n\nUser: " + text + "\nSYSTEM:";
            string api = "https://text.pollinations.ai/" + URLEncode(prompt) + "?model=openai";

            using UnityWebRequest request = UnityWebRequest.Get(api);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 8;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (Settings.debugDictation)
                {
                    LogManager.LogError($"Error contacting API {request.error}.");
                    if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                        LogManager.LogError($"Response Body: {request.downloadHandler.text}");
                }
                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> There was an issue generating your response. {request.error}", 4000);
                LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/close.ogg", "Audio/Menu/close.ogg", clip => Settings.DictationPlay(clip, Main.buttonClickVolume / 10f));
                if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                    CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());
                yield break;
            }

            string response = request.downloadHandler.text.Trim();
            if (Settings.debugDictation)
                LogManager.Log($"AI Response: {response}");

            MatchCollection matches = Regex.Matches(response, @"<([A-Z]+)(?:_""?([^"">]*)""?)?>");

            if (Main.dynamicSounds)
            {
                LoadSoundFromURL($"{PluginInfo.ServerResourcePath}/Audio/Menu/confirm.ogg", "Audio/Menu/confirm.ogg", clip => Settings.DictationPlay(clip, Main.buttonClickVolume / 10f));
            }


            string formatResponse = Regex.Replace(response, @"<([A-Z]+)(?:_""?([^"">]*)""?)?>", "").Replace("\n", "");
            NotificationManager.ClearAllNotifications();
            switch (Main.narratorName)
            {
                case "Mommy ASMR":
                    NotificationManager.SendNotification($"<color=grey>[</color><color=#ffb6c1>MOMMY</color><color=grey>]</color> {formatResponse}", Duration(formatResponse));
                    break;
                default:
                    NotificationManager.SendNotification($"<color=grey>[</color><color=blue>AI</color><color=grey>]</color> {formatResponse}", Duration(formatResponse));
                    break;
            }

            bool narrate = Buttons.GetIndex("Narrate Assistant").enabled;
            bool globalNarrate = Buttons.GetIndex("Global Narrate Assistant").enabled;

            if (narrate)
            {
                if (globalNarrate && NetworkSystem.Instance.InRoom)
                    Main.SpeakText(formatResponse);
                else
                    Main.NarrateText(formatResponse);
            }

            foreach (Match match in matches)
            {
                string commandName = match.Groups[1].Value;
                string argument = match.Groups[2].Success ? match.Groups[2].Value : null;

                switch (commandName)
                {
                    case "ENABLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                            {
                                if (!button.enabled)
                                    Main.Toggle(button.buttonText, true);
                                else
                                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod is already enabled.");
                            }
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");

                            break;
                        }
                    case "DISABLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                            {
                                if (button.enabled)
                                    Main.Toggle(button.buttonText, true);
                                else
                                    NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod is already enabled.");
                            }
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");

                            break;
                        }
                    case "TOGGLEMOD":
                        {
                            ButtonInfo button = FindSimilar(argument);

                            if (button != null)
                                Main.Toggle(button.buttonText, true);
                            else
                                NotificationManager.SendNotification($"<color=grey>[</color><color=red>ERROR</color><color=grey>]</color> Mod \"{argument}\" does not exist.");
                            break;
                        }
                    case "JOINROOM":
                        {
                            Important.QueueRoom(argument.ToUpper());
                            break;
                        }
                    case "JOINRANDOM":
                        {
                            Important.JoinRandom();
                            break;
                        }
                    case "SETNAME":
                        {
                            Main.ChangeName(argument.ToUpper());
                            break;
                        }
                    case "SETCOLOR":
                        {
                            Main.ChangeColor(Main.HexToColor(argument));
                            break;
                        }
                }
            }

            if (!Buttons.GetIndex("Chain Voice Commands").enabled)
                CoroutineManager.instance.StartCoroutine(Settings.DictationRestart());

            generating = false;

            yield break;
        }

#pragma warning disable CS0649 // Field is never assigned to
        [Serializable]
        private class Response
        {
            public int status;
            public string response;
            public string error;
        }
#pragma warning restore CS0649 // Field is never assigned to
    }
}