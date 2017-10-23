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

	public static class DisplayColor4fExtensions
	{
		public static T[] ToArray<T>(this Rhino.Display.Color4f cl)
		{
			var conv = new T[4];
			if (typeof(T) == typeof(float))
			{
				conv[0] = (T)((object)(cl.R));
				conv[1] = (T)((object)(cl.G));
				conv[2] = (T)((object)(cl.B));
				conv[3] = (T)((object)(cl.A));
			}
			else
			{
				conv[0] = (T)Convert.ChangeType((byte)(Math.Min(cl.R, 1.0f) * 255.0f), typeof(T));
				conv[1] = (T)Convert.ChangeType((byte)(Math.Min(cl.G, 1.0f) * 255.0f), typeof(T));
				conv[2] = (T)Convert.ChangeType((byte)(Math.Min(cl.B, 1.0f) * 255.0f), typeof(T));
				conv[3] = (T)Convert.ChangeType((byte)(Math.Min(cl.A, 1.0f) * 255.0f), typeof(T));
				//conv[0] = (T)((object)((byte)Math.Min(cl.R, 1.0f) * 255.0f));
				//conv[1] = (T)((object)((byte)Math.Min(cl.G, 1.0f) * 255.0f));
				//conv[2] = (T)((object)((byte)Math.Min(cl.B, 1.0f) * 255.0f));
				//conv[3] = (T)((object)((byte)Math.Min(cl.A, 1.0f) * 255.0f));
			}

			return conv;
		}

		public static ccl.float4 ToFloat4(this Rhino.Display.Color4f cl)
		{
			return RenderEngine.CreateFloat4(cl.R, cl.G, cl.B);
		}
	}
}