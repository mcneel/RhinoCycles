using System;

namespace RhinoCycles
{
	/// <summary>
	/// Simple wrapper object to hold mesh related data to be pushed to Cycles.
	/// </summary>
	public class CyclesMesh
	{
		/// <summary>
		/// Mesh Guid and mesh index
		/// </summary>
		public Tuple<Guid, int> MeshId;
		/// <summary>
		/// Material renderhash
		/// </summary>
		public uint matid;
		public float[] verts;
		public int[] faces;
		public float[] uvs;
		public float[] vertex_normals;
	}
}
