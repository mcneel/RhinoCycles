using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RhinoCycles
{
	/// <summary>
	/// Helper object to determine what shaders need update, what shaders need to be created
	/// </summary>
	public class CyclesObjectShader
	{
		/// <summary>
		/// Construct new helper object to track shaders for object <code>id</code>
		/// </summary>
		/// <param name="id"></param>
		public CyclesObjectShader(uint id)
		{
			Id = id;
		}

		/// <summary>
		/// Get the object Id
		/// </summary>
		public uint Id { get; private set; }

		/// <summary>
		/// Equality on Id
		/// </summary>
		/// <param name="obj"></param>
		/// <returns>True if object Ids are the same</returns>
		public override bool Equals(object obj)
		{
			var o = obj as CyclesObjectShader;

			return o != null && Id.Equals(o.Id);
		}

		/// <summary>
		/// Get hash code - based on Id
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		/// <summary>
		/// Get/set the shader hash for object in scene, if it already has one.
		/// </summary>
		public uint OldShaderHash { get; set; }

		/// <summary>
		/// Get/set the new shader hash for object in scene.
		/// </summary>
		public uint NewShaderHash { get; set; }
	}
}
