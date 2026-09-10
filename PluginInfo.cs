/*
** Pixelyth-Menu - PluginInfo.cs
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

namespace Pixelyth
{
    public class PluginInfo
    {
        public const string GUID = "com.pixelcatt.gtag.pixelyth";
        public const string Name = "Pixelyth-Menu";
        public const string Description = "An Open-Source Mod Menu for Gorilla Tag with 2000+ Mods!";
        public const string BuildTimestamp = "2026-09-12@23-59-45";
        public const string Version = "9.0.0";

        public const string BaseDirectory = "Pixelyth-Menu";

        public const string ClientResourcePath = "Pixelyth.Resources.Client";
        public const string ServerResourcePath = "https://raw.githubusercontent.com/PixelCattt/Pixelyth-Menu/master/Resources/Server";

#if DEBUG
        public static bool BetaBuild = true;
#else
        public static bool BetaBuild = false;
#endif
    }
}
