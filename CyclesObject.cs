using System;
using ccl;

namespace RhinoCycles
{
	/// <summary>
	/// Simple wrapper object to hold mesh related data to be pushed to Cycles.
	/// </summary>
	public class CyclesObject
	{
		public CyclesObject()
		{
			Visible = true;
		}

		public ccl.Object cob { get; set; }

		/// <summary>
		/// Id of InstanceAncestry
		/// </summary>
		public uint obid;

		/// <summary>
		/// Guid of the mesh this object references
		/// </summary>
		public Tuple<Guid, int> meshid;

		/// <summary>
		/// The transformation matrix for this object.
		/// </summary>
		public Transform Transform { get; set; }

		/// <summary>
		/// Material CRC (RenderHash)
		/// </summary>
		public uint matid { get; set; }

		/// <summary>
		/// Visibility toggle.
		/// </summary>
		public bool Visible { get; set; }
	}
}
