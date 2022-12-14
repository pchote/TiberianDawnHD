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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Mobius.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Add color shifts to player palettes. Use to add RGBA compatibility to PlayerColorPalette.")]
	public class PlayerColorShiftInfo : TraitInfo
	{
		[PaletteReference(true)]
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

		public override object Create(ActorInitializer init) { return new PlayerColorShift(this); }
	}

	public class PlayerColorShift : ILoadsPlayerPalettes
	{
		readonly PlayerColorShiftInfo info;

		public PlayerColorShift(PlayerColorShiftInfo info)
		{
			this.info = info;
		}

		public void LoadPlayerPalettes(WorldRenderer wr, string playerName, Color color, bool replaceExisting)
		{
			var (_, h, s, _) = color.ToAhsv();
			wr.SetPaletteColorShift(info.BasePalette + playerName, h - info.ReferenceHue, s - info.ReferenceSaturation, info.MinHue, info.MaxHue);
		}
	}
}
