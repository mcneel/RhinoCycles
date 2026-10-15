/**
Copyright 2014-2024 Robert McNeel and Associates

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
using RhinoCyclesCore.ExtensionMethods;
using System;
using System.Collections.Generic;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Simple wrapper object to hold mesh related data to be pushed to Cycles.
	/// </summary>
	public class CyclesMesh
	{
		/// <summary>
		/// Mesh Guid and mesh index
		/// </summary>
		public Tuple<Guid, int> MeshId { get; set; }
		/// <summary>
		/// Material renderhash
		/// </summary>
		public uint MatId { get; set; }
		/// <summary>
		/// float array with vertex data. Stride 3.
		/// </summary>
		public float[] Verts { get; set; }
		/// <summary>
		/// int array with face indices. Indices are into Verts
		/// </summary>
		public int[] Faces { get; set; }
		/// <summary>
		/// List of tuples of channel indices and float arrays with UV coordinates. Stride 2.
		/// </summary>
		public List<Tuple<int, float[]>> Uvs { get; set; }
		/// <summary>
		/// Float array with vertex normal data. Stride 3.
		/// </summary>
		public float[] VertexNormals { get; set; }

		/// <summary>
		/// Float array with vertex color data or null. Stride 3.
		/// </summary>
		public float[] VertexColors { get; set; }

		/// <summary>
		/// True when the source mesh is closed and should be treated as solid for decals.
		/// </summary>
		public bool IsSolid { get; set; }

		public ccl.Transform OcsFrame { get; set; } = ccl.Transform.Identity();

		/// <summary>
		/// Planar, spherical or cylindrical mapping of one of the mesh's mapping channels, null if none.
		/// </summary>
		public PrimitiveMapping PrimitiveMapping { get; set; }

		public void Clear()
		{
			this.Verts = null;
			this.Faces = null;
			this.Uvs = null;
			this.VertexNormals = null;
			this.VertexColors = null;
		}
	}

	/// <summary>
	/// A planar, spherical or cylindrical mapping that the Cycles kernel evaluates per
	/// shading point, like the display does.
	/// </summary>
	public class PrimitiveMapping
	{
		/// <summary>
		/// ON_TextureMapping::TYPE value.
		/// </summary>
		public int Type { get; private set; }
		public int Channel { get; private set; }
		public bool Capped { get; private set; }
		public bool Divided { get; private set; }
		/// <summary>
		/// Object space to mapping primitive space.
		/// </summary>
		public ccl.Transform P { get; private set; }
		public ccl.Transform N { get; private set; }
		public ccl.Transform Uvw { get; private set; }

		/// <summary>
		/// Set <paramref name="mapping"/> on <paramref name="ob"/>, or clear it if null.
		/// </summary>
		public static void Apply(ccl.Object ob, PrimitiveMapping mapping)
		{
			if (mapping == null)
				ob.ClearPrimitiveMapping();
			else
				ob.SetPrimitiveMapping(mapping.Type, mapping.Channel, mapping.Capped, mapping.Divided, mapping.P, mapping.N, mapping.Uvw);
		}

		/// <summary>
		/// Null unless <paramref name="mc"/> has a planar, spherical or cylindrical mapping.
		/// </summary>
		public static PrimitiveMapping FromMappingChannel(Rhino.Render.ChangeQueue.MappingChannel mc)
		{
			var mapping = mc.Mapping;
			var type = mapping.MappingType;
			if (type != Rhino.Render.TextureMappingType.PlaneMapping &&
				type != Rhino.Render.TextureMappingType.CylinderMapping &&
				type != Rhino.Render.TextureMappingType.SphereMapping)
				return null;

			// The mesh UVs evaluate the mapping at Local^-1 * P, see ON_Xform::GetMappingXforms.
			var p = Rhino.Geometry.Transform.Identity;
			var n = Rhino.Geometry.Transform.Identity;
			var local = mc.Local;
			if (!local.IsZero && !local.IsIdentity && local.TryGetInverse(out var localInverse))
			{
				p = localInverse;
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						n[i, j] = local[j, i];
					}
				}
			}

			return new PrimitiveMapping
			{
				Type = (int)type,
				Channel = mc.Channel == 0 ? 1 : mc.Channel,
				Capped = mapping.Capped,
				Divided = mapping.TextureSpace == Rhino.Render.TextureSpace.Divided,
				P = (mapping.PrimitiveTransform * p).ToCyclesTransform(),
				N = (mapping.NormalTransform * n).ToCyclesTransform(),
				Uvw = mapping.UvwTransform.ToCyclesTransform(),
			};
		}
	}
}
