using System;
using System.Collections.Generic;
using System.Drawing;
using ccl.ShaderNodes.Sockets;
using Rhino.Geometry.Collections;
using Rhino.Render;

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

	public static class RenderTextureExtensions
	{
		public static bool IsBitmapTexture(this RenderTexture tex)
		{
			return tex.TypeId.Equals(ContentUuids.BitmapTextureType)
						|| tex.TypeId.Equals(ContentUuids.SimpleBitmapTextureType)
						|| tex.TypeId.Equals(ContentUuids.HDRTextureType)
						|| tex.TypeId.Equals(ContentUuids.HatchBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.CrossHatchBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.GritBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.LeatherBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.DotBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.WoodBumpTexture)
						|| tex.TypeId.Equals(ContentUuids.SpeckleBumpTexture)
						;
		}
	}

	public static class ISocketExtenions
	{
	public static List<ISocket> ToList(this ISocket sock)
	{
		List<ISocket> lst = new List<ISocket> {
			sock
		};
			return lst;
	}
	}

	public static class DisplayColor4fExtensions
	{
	public static void ToArray(this Rhino.Display.Color4f cl, ref byte[] conv)
	{
	  conv[0] = (byte)(Math.Min(cl.R, 1.0f) * 255.0f);
	  conv[1] = (byte)(Math.Min(cl.G, 1.0f) * 255.0f);
	  conv[2] = (byte)(Math.Min(cl.B, 1.0f) * 255.0f);
	  conv[3] = (byte)(Math.Min(cl.A, 1.0f) * 255.0f);
	}

	public static void ToArray(this Rhino.Display.Color4f cl, ref float[] conv)
	{
	  conv[0] = cl.R;
	  conv[1] = cl.G;
	  conv[2] = cl.B;
	  conv[3] = cl.A;
	}

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

		public static float LargestComponent(this Rhino.Display.Color4f cl)
		{
			if (cl.R > cl.G && cl.R > cl.B) return cl.R;

			if (cl.G > cl.R && cl.G > cl.B) return cl.G;

			return cl.B;
		}

		public static ccl.float4 ToFloat4(this Rhino.Display.Color4f cl)
		{
			return RenderEngine.CreateFloat4(cl.R, cl.G, cl.B, cl.A);
		}

		public static Rhino.Display.Color4f ToColor4f(this ccl.float4 cl)
		{
			return new Rhino.Display.Color4f(cl.x, cl.y, cl.z, cl.w);
		}


		/// <summary>
		/// Apply gamma
		/// </summary>
		/// <param name="cl">Color whose components to raise to the power of</param>
		/// <param name="gamma">power to raise to</param>
		/// <returns>Color with components raised to the power of gamma</returns>
		public static Rhino.Display.Color4f ApplyGamma(this Rhino.Display.Color4f cl, float gamma) {
			if (Math.Abs(1.0f - gamma) > float.Epsilon)
			{
				return new Rhino.Display.Color4f((float) Math.Pow(cl.R, gamma), (float) Math.Pow(cl.G, gamma), (float) Math.Pow(cl.B, gamma), cl.A);
			}
			return cl;
		}
	}
	public static class Point3dExtensions
	{
		/// <summary>
		/// Get a ccl.float4 representation from a Point3d. The w property will
		/// be set to 0
		/// </summary>
		/// <param name="point">The Point3d to cast</param>
		/// <returns>ccl.float4</returns>
		public static ccl.float4 ToFloat4(this Rhino.Geometry.Point3d point)
		{
			var f = new ccl.float4((float)point.X, (float)point.Y, (float)point.Z);
			return f;
		}
	}

	public static class Vector3dExtensions
	{
		/// <summary>
		/// Get a ccl.float4 representation from a Vector3d. The w property will
		/// be set to 0
		/// </summary>
		/// <param name="vector">The Vector3d to cast</param>
		/// <returns>ccl.float4</returns>
		public static ccl.float4 ToFloat4(this Rhino.Geometry.Vector3d vector)
		{
			var f = new ccl.float4((float)vector.X, (float)vector.Y, (float)vector.Z);
			return f;
		}
	}

	public static class CclTransformExtensions
	{
		public static float[] ToFloatArray(this ccl.Transform t)
		{
			var f = new float[12];
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

			rt.M30 = 0.0f;
			rt.M31 = 0.0f;
			rt.M32 = 0.0f;
			rt.M33 = 0.0f;

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
				(float) rt.M20, (float) rt.M21, (float) rt.M22, (float) rt.M23
				);

			return t;
		}

		/// <summary>
		/// Calculate the CRC for the transform using the given remainder.
		/// </summary>
		public static uint TransformCrc(this Rhino.Geometry.Transform rt, uint remainder)
		{
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M00);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M01);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M02);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M03);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M10);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M11);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M12);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M13);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M20);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M21);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M22);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M23);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M30);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M31);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M32);
			remainder = Rhino.RhinoMath.CRC32(remainder, rt.M33);
			return remainder;
		}

		/// <summary>
		/// Extract scale vector from ccl.Transform
		/// </summary>
		/// <param name="t">ccl.Transform to extract scale vector from</param>
		/// <returns>ccl.float4 that is the scale vector for this transform</returns>
		public static ccl.float4 ScaleVector(this ccl.Transform t)
		{
				ccl.float4 sx = new ccl.float4(t.x.x,t.y.x,t.z.x);
				ccl.float4 sy = new ccl.float4(t.x.y,t.y.y,t.z.y);
				ccl.float4 sz = new ccl.float4(t.x.z,t.y.z,t.z.z);
				return new ccl.float4(sx.Length(), sy.Length(), sz.Length());
		}

		/// <summary>
		/// Extract translate vector from ccl.Transform
		/// </summary>
		/// <param name="t">ccl.Transform to extract translate vector from</param>
		/// <returns>ccl.float4 that is the translate vector for this transform</returns>
		public static ccl.float4 TranslateVector(this ccl.Transform t)
		{
			return new ccl.float4(t.x.w, t.y.w, t.z.w);
		}
	}
}