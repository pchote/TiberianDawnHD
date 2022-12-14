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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Mobius.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Create a color picker palette from another palette.")]
	class ColorPickerColorShiftInfo : TraitInfo
	{
		[PaletteReference]
		[FieldLoader.Require]
		[Desc("The name of the palette to base off.")]
		public readonly string BasePalette = null;

		[Desc("Hues between this and MaxHue will be shifted.")]
		public readonly float MinHue = 0.29f;

		[Desc("Hues between MinHue and this will be shifted.")]
		public readonly float MaxHue = 0.37f;

		[Desc("Hue reference for the color shift.")]
		public readonly float ReferenceHue = 0.33f;

		[Desc("Saturation reference for the color shift.")]
		public readonly float ReferenceSaturation = 0.925f;

		public override object Create(ActorInitializer init) { return new ColorPickerColorShift(init.World, this); }
	}

	class ColorPickerColorShift : ILoadsPalettes, ITickRender
	{
		readonly ColorPickerColorShiftInfo info;
		readonly ColorPickerManagerInfo colorManager;
		Color color;

		public ColorPickerColorShift(World world, ColorPickerColorShiftInfo info)
		{
			// All users need to use the same TraitInfo instance, chosen as the default mod rules
			colorManager = Game.ModData.DefaultRules.Actors[SystemActors.World].TraitInfo<ColorPickerManagerInfo>();
			this.info = info;
		}

		void ILoadsPalettes.LoadPalettes(WorldRenderer wr)
		{
			color = colorManager.Color;
			var (_, h, s, _) = color.ToAhsv();
			wr.SetPaletteColorShift(info.BasePalette, h - info.ReferenceHue, s - info.ReferenceSaturation, info.MinHue, info.MaxHue);
		}

		void ITickRender.TickRender(WorldRenderer wr, Actor self)
		{
			if (color == colorManager.Color)
				return;

			color = colorManager.Color;
			var (_, h, s, _) = color.ToAhsv();
			wr.SetPaletteColorShift(info.BasePalette, h - info.ReferenceHue, s - info.ReferenceSaturation, info.MinHue, info.MaxHue);
		}
	}
}
