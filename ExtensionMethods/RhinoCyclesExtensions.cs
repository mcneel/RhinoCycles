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

	public static class CclTransformExtensions
	{
		public static float[] ToFloatArray(this ccl.Transform t)
		{
			var f = new float[16];
			f[0] = t.x.x;
			f[1] = t.x.y;
			f[2] = t.x.z;
			f[3] = t.x.w;

			f[4] = t.y.x;
			f[5] = t.y.y;
			f[6] = t.y.z;
			f[7] = t.y.w;

			f[8] = t.z.x;
			f[9] = t.z.y;
			f[10] = t.z.z;
			f[11] = t.z.w;

			f[12] = t.w.x;
			f[13] = t.w.y;
			f[14] = t.w.z;
			f[15] = t.w.w;

			return f;
		}
		public static Rhino.Geometry.Transform ToRhinoTransform(this ccl.Transform t)
		{
			Rhino.Geometry.Transform rt = new Rhino.Geometry.Transform();
			rt.M00 = t.x.x;
			rt.M01 = t.x.y;
			rt.M02 = t.x.z;
			rt.M03 = t.x.w;

			rt.M10 = t.y.x;
			rt.M11 = t.y.y;
			rt.M12 = t.y.z;
			rt.M13 = t.y.w;

			rt.M20 = t.z.x;
			rt.M21 = t.z.y;
			rt.M22 = t.z.z;
			rt.M23 = t.z.w;

			rt.M30 = t.w.x;
			rt.M31 = t.w.y;
			rt.M32 = t.w.z;
			rt.M33 = t.w.w;

			return rt;
		}

		/// <summary>
		/// Convert a Rhino.Geometry.Transform to ccl.Transform
		/// </summary>
		/// <param name="rt">Rhino.Geometry.Transform</param>
		/// <returns>ccl.Transform</returns>
		public static ccl.Transform ToCyclesTransform(this Rhino.Geometry.Transform rt)
		{
			var t = new ccl.Transform(
				(float) rt.M00, (float) rt.M01, (float) rt.M02, (float) rt.M03,
				(float) rt.M10, (float) rt.M11, (float) rt.M12, (float) rt.M13,
				(float) rt.M20, (float) rt.M21, (float) rt.M22, (float) rt.M23,
				(float) rt.M30, (float) rt.M31, (float) rt.M32, (float) rt.M33
				);

			return t;
		}
	}
}