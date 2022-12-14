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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Mobius.Terrain;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Mobius.Traits
{
	public class RemasterTerrainRendererInfo : TraitInfo, ITiledTerrainRendererInfo
	{
		public override object Create(ActorInitializer init) { return new RemasterTerrainRenderer(init.World); }

		bool ITiledTerrainRendererInfo.ValidateTileSprites(ITemplatedTerrainInfo terrainInfo, Action<string> onError)
		{
			var failed = false;
			var tileCache = new RemasterTileCache((RemasterTerrain)terrainInfo);
			foreach (var t in terrainInfo.Templates)
			{
				var templateInfo = (RemasterTerrainTemplateInfo)t.Value;
				foreach (var kv in templateInfo.Images)
				{
					for (var i = 0; i < kv.Value.Length; i++)
					{
						if (!tileCache.HasTileSprite(new TerrainTile(t.Key, (byte)kv.Key), i))
						{
							onError("\tTemplate `{0}` tile {1} references sprite `{2}` that does not exist.".F(t.Key, kv.Key, templateInfo.Images[i]));
							failed = true;
						}
					}
				}
			}

			return failed;
		}
	}

	public sealed class RemasterTerrainRenderer : IRenderTerrain, IWorldLoaded, INotifyActorDisposing, ITiledTerrainRenderer, ITick
	{
		readonly Map map;
		readonly RemasterTerrain terrainInfo;
		readonly RemasterTileCache tileCache;
		TerrainSpriteLayer[] spriteLayers;
		int frame;
		bool disposed;

		public RemasterTerrainRenderer(World world)
		{
			map = world.Map;

			terrainInfo = map.Rules.TerrainInfo as RemasterTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("TerrainRenderer is only compatible with the RemasterTerrain terrain parser.");

			tileCache = new RemasterTileCache(terrainInfo);
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
		{
			spriteLayers = new TerrainSpriteLayer[8];
			for (var i = 0; i < 8; i++)
				spriteLayers[i] = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);

			foreach (var cell in map.AllCells)
				UpdateCell(cell);

			map.Tiles.CellEntryChanged += UpdateCell;
			map.Height.CellEntryChanged += UpdateCell;
		}

		public void UpdateCell(CPos cell)
		{
			for (var i = 0; i < 8; i++)
				spriteLayers[i].Update(cell, tileCache.TileSprite(map.Tiles[cell], i), null);
		}

		int t;
		void ITick.Tick(Actor self)
		{
			t += 1;
			if (t > 2)
			{
				t = 0;
				frame = (frame + 1) % 8;
			}
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			spriteLayers[frame].Draw(wr.Viewport);
			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= UpdateCell;
			map.Height.CellEntryChanged -= UpdateCell;

			for (var i = 0; i < 8; i++)
				spriteLayers[i].Dispose();

			tileCache.Dispose();
			disposed = true;
		}

		Sprite ITiledTerrainRenderer.MissingTile { get { return tileCache.MissingTile; } }

		Sprite ITiledTerrainRenderer.TileSprite(TerrainTile r, int? variant)
		{
			return tileCache.TileSprite(r, variant ?? 0);
		}

		Rectangle ITiledTerrainRenderer.TemplateBounds(TerrainTemplateInfo template)
		{
			Rectangle? templateRect = null;
			var tileSize = map.Grid.TileSize;

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

					var sprite = tileCache.TileSprite(tile, 0);
					if (sprite == null)
						continue;

					var u = map.Grid.Type == MapGridType.Rectangular ? x : (x - y) / 2f;
					var v = map.Grid.Type == MapGridType.Rectangular ? y : (x + y) / 2f;

					var tl = new float2(u * tileSize.Width, (v - 0.5f * tileInfo.Height) * tileSize.Height) - 0.5f * sprite.Size;
					var rect = new Rectangle((int)(tl.X + sprite.Offset.X), (int)(tl.Y + sprite.Offset.Y), (int)sprite.Size.X, (int)sprite.Size.Y);
					templateRect = templateRect.HasValue ? Rectangle.Union(templateRect.Value, rect) : rect;
				}
			}

			return templateRect.HasValue ? templateRect.Value : Rectangle.Empty;
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderUIPreview(WorldRenderer wr, TerrainTemplateInfo t, int2 origin, float scale)
		{
			var template = t as RemasterTerrainTemplateInfo;
			if (template == null)
				yield break;

			var ts = map.Grid.TileSize;
			var gridType = map.Grid.Type;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)i++);
					var tileInfo = terrainInfo.GetTileInfo(tile);

					// Empty tile
					if (tileInfo == null)
						continue;

					var sprite = tileCache.TileSprite(tile, 0);
					if (sprite == null)
						continue;

					var u = gridType == MapGridType.Rectangular ? x : (x - y) / 2f;
					var v = gridType == MapGridType.Rectangular ? y : (x + y) / 2f;
					var offset = (scale * new float2(u * ts.Width, (v - 0.5f * tileInfo.Height) * ts.Height) - 0.5f * sprite.Size.XY).ToInt2();

					yield return new UISpriteRenderable(sprite, WPos.Zero, origin + offset, 0, null, scale);
				}
			}
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderPreview(WorldRenderer wr, TerrainTemplateInfo t, WPos origin)
		{
			var template = t as RemasterTerrainTemplateInfo;
			if (template == null)
				yield break;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)i++);
					var tileInfo = terrainInfo.GetTileInfo(tile);

					// Empty tile
					if (tileInfo == null)
						continue;

					var sprite = tileCache.TileSprite(tile, 0);
					if (sprite == null)
						continue;

					var offset = map.Offset(new CVec(x, y), tileInfo.Height);

					yield return new SpriteRenderable(sprite, origin, offset, 0, null, 1f, 1f, float3.Ones, TintModifiers.None, false);
				}
			}
		}
	}
}
