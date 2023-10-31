/**
Copyright 2014-2021 Robert McNeel and Associates

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
		/// List of float arrays with UV coordinates. Stride 2.
		/// The first entry will be available in Cycles as "uvmap", the next
		/// "uvmap2" and so on.
		/// </summary>
		public List<float[]> Uvs { get; set; }
		/// <summary>
		/// Float array with vertex normal data. Stride 3.
		/// </summary>
		public float[] VertexNormals { get; set; }

		/// <summary>
		/// Float array with vertex color data or null. Stride 3.
		/// </summary>
		public float[] VertexColors { get; set; }

		public ccl.Transform OcsFrame { get; set; } = ccl.Transform.Identity();

		public void Clear()
		{
			this.Verts = null;
			this.Faces = null;
			this.Uvs = null;
			this.VertexNormals = null;
			this.VertexColors = null;
		}
	}
}
