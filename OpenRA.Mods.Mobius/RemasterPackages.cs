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

using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Mobius
{
	public sealed class RemasterModContent : IGlobalModData
	{
		[FieldLoader.Require]
		public readonly string RemasterDataMount = null;
		public readonly string InstallPromptMod = "cnccontent";
		public readonly Dictionary<string, string> Packages;
		public readonly int SteamAppID = 1213210;
		public readonly string OriginRegistryKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Petroglyph\CnCRemastered";
		public readonly string OriginRegistryValue = "Install Dir";

		public RemasterModContent(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
		}

		string FindOriginInstallation()
		{
			if (Platform.CurrentPlatform == PlatformType.Windows)
			{
				var path = Microsoft.Win32.Registry.GetValue(OriginRegistryKey, OriginRegistryValue, null) as string;
				if (Directory.Exists(path))
					return path;
			}

			return null;
		}

		public bool TryMountPackagesInner(ModData modData)
		{
			var remasterDataPath = InstallUtils.FindSteamInstallation(SteamAppID) ?? FindOriginInstallation();
			if (remasterDataPath == null)
				return false;

			modData.ModFiles.Mount(Path.Combine(remasterDataPath, "Data"), RemasterDataMount);
			foreach (var kv in Packages)
			{
				var package = modData.ModFiles.OpenPackage(kv.Key);
				if (package == null)
					return false;

				modData.ModFiles.Mount(package, kv.Value);
			}

			return true;
		}

		public bool TryMountPackages(ModData modData)
		{
			if (!TryMountPackagesInner(modData))
			{
				Game.InitializeMod(InstallPromptMod, new Arguments());
				return false;
			}

			return true;
		}
	}
}
