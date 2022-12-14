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

using OpenRA.Mods.Cnc;

namespace OpenRA.Mods.Mobius
{
	public sealed class RemasterLoadScreen : CncLoadScreen
	{
		public override bool BeforeLoad()
		{
			var remasterContent = ModData.Manifest.Get<RemasterModContent>();
			if (!remasterContent.TryMountPackages(ModData))
				return false;

			return base.BeforeLoad();
		}
	}
}
