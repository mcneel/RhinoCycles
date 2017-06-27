using System;
using System.Drawing;
using Rhino.Geometry.Collections;

namespace RhinoCyclesCore.ExtensionMethods
{
	public static class NumericExtensions
	{
		public static bool FuzzyEquals(this double orig, double test)
		{
			var rc = false;
			rc = Math.Abs(orig - test) < 0.000001;
			return rc;
		}
		public static bool FuzzyEquals(this float orig, float test)
		{
			var rc = false;
			rc = Math.Abs(orig - test) < 0.000001;
			return rc;
		}
	}
	public static class SizeExtensions
	{
		public static void Deconstruct(this Size size, out int x, out int y)
		{
			x = size.Width;
			y = size.Height;
		}
	}

	public static class MeshVertexColorListExtensions
	{
		/// <summary>
		/// Copies all vertex colors to a linear array of float in rgb order
		/// </summary>
		/// <returns>The float array.</returns>
		public static float[] ToFloatArray(this MeshVertexColorList cl)
		{
			int count = cl.Count;
			var rc = new float[count * 3];
			int index = 0;
			foreach (var c in cl)
			{
				Rhino.Display.Color4f c4f = new Rhino.Display.Color4f(c);
				rc[index++] = c4f.R;
				rc[index++] = c4f.G;
				rc[index++] = c4f.B;
			}
			return rc;
		}

		/// <summary>
		/// Copies all vertex colors to a linear array of float in rgb order
		/// if cl.Count==count
		/// </summary>
		/// <returns>The float array, or null if cl.Count!=count</returns>
		public static float[] ToFloatArray(this MeshVertexColorList cl, int count)
		{
			if (count != cl.Count) return null;
			return cl.ToFloatArray();
		}
	}
}