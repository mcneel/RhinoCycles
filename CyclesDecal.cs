/**
Copyright 2014-2020 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using System.Collections.Generic;
using Rhino.Render;
using Rhino;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Helper class to hold decal information for one mesh instance
	/// </summary>
	public class CyclesDecal
	{
		public CyclesTextureImage Texture { get; set; } = null;
		public DecalMapping Mapping { get; set; } = DecalMapping.Planar;
		public DecalProjection Projection { get; set; } = DecalProjection.Both;
		public TextureMapping TextureMapping { get; set; } = null;

		public float Height { get; set; } = 1.0f;
		public float Radius { get; set; } = 1.0f;

		public float HorizontalSweepStart { get; set; } = 0.0f;
		public float HorizontalSweepEnd { get; set; } = 1.0f;
		public float VerticalSweepStart { get; set; } = 0.0f;
		public float VerticalSweepEnd { get; set; } = 1.0f;

		public ccl.Transform Transform { get; set; } = null;

		/// <summary>
		/// Decal origin
		/// </summary>
		public ccl.float4 Origin { get; set; } = new ccl.float4(0.0f);
		/// <summary>
		/// Decal across vector (planar)
		/// </summary>
		public ccl.float4 Across { get; set; } = new ccl.float4(0.0f);

		/// <summary>
		/// Decal up vector (planar)
		/// </summary>
		public ccl.float4 Up { get; set; } = new ccl.float4(0.0f);

		public uint CRC { get; set; } = 0;

		public float Transparency { get; set; } = 0.0f;

		static public uint CRCForList(List<CyclesDecal> decals)
		{
			uint decalsCRC = 0;
			if (decals != null)
			{
				decalsCRC = decals[0].CRC;
				if (decals.Count > 1)
				{
					for (int i = 1; i < decals.Count; i++)
					{
						decalsCRC = RhinoMath.CRC32(decalsCRC, decals[i].CRC);
					}
				}
			}
			return decalsCRC;
		}

	}

}