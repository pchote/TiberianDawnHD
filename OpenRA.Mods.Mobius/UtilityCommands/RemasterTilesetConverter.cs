#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
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
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.Common.UpdateRules;

namespace OpenRA.Mods.Mobius.UtilityCommands
{
	class RemasterTilesetConverter : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--convert-tileset"; } }

		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length >= 3;
		}

		[Desc("FILENAME", "CONFIG.MEG", "TERRAIN-XML", "Convert a TD or RA tileset to remaster format.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var tileset = MiniYaml.FromFile(args[1], discardCommentsAndWhitespace: false);
			var templates = tileset.First(n => n.Key == "Templates");

			var mapping = new XmlDocument();
			using (var ffs = new FileStream(args[2], FileMode.Open))
			{
				var config = new MegV3Loader.MegFile(ffs, args[2]);

				// e.g. TD_TERRAIN_TEMPERATE.XML, TD_TERRAIN_DESERT.XML, TD_TERRAIN_WINTER.XML
				mapping.Load(config.GetStream("DATA\\XML\\TILESETS\\{0}".F(args[3])));
			}

			var rootTexturePath = mapping.SelectSingleNode("//RootTexturePath").InnerText.ToUpperInvariant();
			foreach (var template in templates.Value.Nodes)
			{
				var legacy = template.LastChildMatching("Images").Value.Value;
				var code = Path.GetFileNameWithoutExtension(legacy).ToUpperInvariant();

				var tileNodes = mapping.DocumentElement.SelectNodes("//Tile[Key/Name = '{0}']".F(code));

				if (tileNodes == null)
				{
					Console.WriteLine("No match for {0}!", code);
					continue;
				}

				template.RemoveNodes("Images");
				template.RemoveNodes("Frames");

				var imageNode = new MiniYamlNode("Images", "");
				template.AddNode(imageNode);

				foreach (var t in tileNodes)
				{
					var tileNode = (XmlNode)t;
					var index = tileNode.SelectSingleNode("Key/Shape").InnerText;
					var frames = new List<string>();
					foreach (var f in tileNode.SelectNodes("Value/Frames/Frame"))
						frames.Add("DATA\\ART\\TEXTURES\\SRGB\\{0}\\{1}".F(rootTexturePath, Path.ChangeExtension(((XmlNode)f).InnerText, ".DDS").ToUpperInvariant()));

					imageNode.AddNode(index, FieldSaver.FormatValue(frames));
				}
			}

			tileset.WriteToFile(args[1]);
		}
	}
}
