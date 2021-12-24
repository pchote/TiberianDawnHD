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
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.Common.SpriteLoaders;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Mods.Mobius.Terrain
{
	public sealed class RemasterTileCache : IDisposable
	{
		static readonly int[] ChannelsBGRA = { 0, 1, 2, 3 };
		static readonly int[] ChannelsRGBA = { 2, 1, 0, 3 };

		readonly Dictionary<ushort, Dictionary<int, Sprite[]>> templates = new Dictionary<ushort, Dictionary<int, Sprite[]>>();
		SheetBuilder sheetBuilder;
		readonly Sprite missingTile;
		readonly RemasterTerrain terrainInfo;

		public RemasterTileCache(RemasterTerrain terrainInfo)
		{
			this.terrainInfo = terrainInfo;

			// HACK: Reduce the margin so we can fit DESERT into 4 sheets until we can find more memory savings somewhere else!
			sheetBuilder = new SheetBuilder(SheetType.BGRA, terrainInfo.SheetSize, margin: 0);
			missingTile = sheetBuilder.Add(new byte[4], SpriteFrameType.Bgra32, new Size(1, 1));

			foreach (var t in terrainInfo.Templates)
			{
				var templateInfo = (RemasterTerrainTemplateInfo)t.Value;
				var sprites = new Dictionary<int, Sprite[]>();
				templates.Add(t.Value.Id, sprites);

				foreach (var kv in templateInfo.Images)
				{
					var tileSprites = new List<Sprite>();
					foreach (var f in kv.Value)
					{
						var frames = FrameLoader.GetFrames(Game.ModData.DefaultFileSystem, f,
							Game.ModData.SpriteLoaders, out var allMetadata);

						var frame = frames[0];
						if (frame.Type == SpriteFrameType.Indexed8)
						{
							// Composite frames are assembled at runtime from one or more source images
							var metadata = allMetadata.GetOrDefault<PngSheetMetadata>();
							if (metadata?.Metadata?.TryGetValue("SourceFilename[0]", out _) ?? false)
							{
								var data = new byte[frame.Size.Width * frame.Size.Height * 4];
								for (var i = 0; i < frames.Length; i++)
								{
									if (!metadata.Metadata.TryGetValue($"SourceFilename[{i}]", out var sf))
									{
										// TODO: throw exception
										Console.WriteLine($"SourceFilename[{i}] not found");
										break;
									}

									var overlay = FrameLoader.GetFrames(Game.ModData.DefaultFileSystem, sf,
										Game.ModData.SpriteLoaders, out _)[0];

									if (overlay.Type != SpriteFrameType.Bgra32 && overlay.Type != SpriteFrameType.Rgba32)
									{
										// TODO: throw exception
										Console.WriteLine($"SourceFilename[{i}] unsupported");
										break;
									}

									var channels = overlay.Type == SpriteFrameType.Bgra32 ? ChannelsBGRA : ChannelsRGBA;

									for (var y = 0; y < frame.Size.Height; y++)
									{
										var o = 4 * y * frame.Size.Width;
										for (var x = 0; x < frame.Size.Width; x++)
										{
											var maskAlpha = frames[i].Data[y * frame.Size.Width + x];
											for (var j = 0; j < 4; j++)
											{
												// Note: we want to pre-multiply the colour channels by the alpha channel,
												// but not the alpha channel itself. The simplest way to do this is to
												// always include the overlay alpha in the alpha component, and
												// special-case the alpha's channel value instead.
												var overlayAlpha = overlay.Data[o + 4 * x + 3] * maskAlpha;
												var overlayChannel = j < 3 ? overlay.Data[o + 4 * x + channels[j]] : 255;

												// Base channels have already been pre-multiplied by alpha
												var baseAlpha = 65205 - overlayAlpha;
												var baseChannel = data[o + 4 * x + j];

												// Apply mask and pre-multiply alpha
												data[o + 4 * x + j] = (byte)((overlayChannel * overlayAlpha + baseChannel * baseAlpha) / 65205);
											}
										}
									}
								}

								var sc = sheetBuilder.Allocate(frame.Size, 1f, frame.Offset);
								Util.FastCopyIntoChannel(sc, data, SpriteFrameType.Bgra32);
								tileSprites.Add(sc);

								continue;
							}

							// Indexed sprites are otherwise not supported
							tileSprites.Add(missingTile);
							continue;
						}

						var s = sheetBuilder.Allocate(frame.Size, 1f, frame.Offset);
						Util.FastCopyIntoChannel(s, frame.Data, frame.Type);
						tileSprites.Add(s);
					}

					sprites[kv.Key] = tileSprites.ToArray();
				}
			}

			sheetBuilder.Current.ReleaseBuffer();

			Console.WriteLine("Terrain has {0} sheets", sheetBuilder.AllSheets.Count());
		}

		public bool HasTileSprite(TerrainTile r, int frame)
		{
			return TileSprite(r, frame) != missingTile;
		}

		public Sprite TileSprite(TerrainTile r, int frame)
		{
			if (!templates.TryGetValue(r.Type, out var template))
				return missingTile;

			if (!template.TryGetValue(r.Index, out var sprites))
				return missingTile;

			return sprites[frame % sprites.Length];
		}

		public Rectangle TemplateBounds(TerrainTemplateInfo template, Size tileSize, MapGridType mapGrid)
		{
			Rectangle? templateRect = null;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)(i++));
					var tileInfo = terrainInfo.GetTileInfo(tile);

					// Empty tile
					if (tileInfo == null)
						continue;

					var sprite = TileSprite(tile, 0);
					var u = mapGrid == MapGridType.Rectangular ? x : (x - y) / 2f;
					var v = mapGrid == MapGridType.Rectangular ? y : (x + y) / 2f;

					var tl = new float2(u * tileSize.Width, (v - 0.5f * tileInfo.Height) * tileSize.Height) - 0.5f * sprite.Size;
					var rect = new Rectangle((int)(tl.X + sprite.Offset.X), (int)(tl.Y + sprite.Offset.Y), (int)sprite.Size.X, (int)sprite.Size.Y);
					templateRect = templateRect.HasValue ? Rectangle.Union(templateRect.Value, rect) : rect;
				}
			}

			return templateRect.HasValue ? templateRect.Value : Rectangle.Empty;
		}

		public Sprite MissingTile { get { return missingTile; } }

		public void Dispose()
		{
			sheetBuilder.Dispose();
		}
	}
}
