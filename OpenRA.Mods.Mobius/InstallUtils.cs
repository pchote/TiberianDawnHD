#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.Mobius
{
	public static class InstallUtils
	{
		static Dictionary<string, string> ParseKeyValuesManifest(string path)
		{
			var kvRegex = new Regex("^\\s*\"(?<key>[^\"]*)\"\\s*\"(?<value>[^\"]*)\"\\s*$");
			var ret = new Dictionary<string, string>();
			using (var s = new FileStream(path, FileMode.Open))
			{
				foreach (var line in s.ReadAllLines())
				{
					var match = kvRegex.Match(line);
					if (match.Success)
						ret[match.Groups["key"].Value] = match.Groups["value"].Value;
				}
			}

			return ret;
		}

		static IEnumerable<string> SteamDirectory()
		{
			var candidatePaths = new List<string>();
			switch (Platform.CurrentPlatform)
			{
				case PlatformType.Windows:
				{
					var path = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
					if (path != null)
						candidatePaths.Add(path);

					path = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null) as string;
					if (path != null)
						candidatePaths.Add(path);

					break;
				}

				case PlatformType.OSX:
				{
					candidatePaths.Add(Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
						"Library", "Application Support", "Steam"));

					break;
				}

				default:
				{
					candidatePaths.Add(Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
						".steam", "root"));

					break;
				}
			}

			foreach (var libraryPath in candidatePaths)
			{
				if (!Directory.Exists(libraryPath))
					continue;

				yield return libraryPath;

				// Steam stores additional paths in incrementing keys ("1", "2", etc) in libraryfolders.vdf
				var libraryFoldersPath = Path.Combine(libraryPath, "steamapps", "libraryfolders.vdf");
				if (File.Exists(libraryFoldersPath))
				{
					var data = ParseKeyValuesManifest(libraryFoldersPath);
					for (var i = 1; ; i++)
					{
						if (!data.TryGetValue(i.ToString(), out var path))
							break;

						yield return path;
					}
				}
			}
		}

		public static string FindSteamInstallation(int steamAppID)
		{
			var manifestName = "appmanifest_{0}.acf".F(steamAppID);
			foreach (var steamDirectory in SteamDirectory())
			{
				var manifestPath = Path.Combine(steamDirectory, "steamapps", manifestName);
				if (!File.Exists(manifestPath))
					continue;

				var data = ParseKeyValuesManifest(manifestPath);
				if (!data.TryGetValue("StateFlags", out var stateFlags) || stateFlags != "4")
					continue;

				if (!data.TryGetValue("installdir", out var installDir))
					continue;

				return Path.Combine(steamDirectory, "steamapps", "common", installDir);
			}

			return null;
		}
	}
}
