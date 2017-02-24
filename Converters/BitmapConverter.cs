/**
Copyright 2014-2016 Robert McNeel and Associates

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
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using Rhino.Geometry;
using Rhino.Render;
using RhinoCyclesCore.Core;
using Transform = ccl.Transform;

namespace RhinoCyclesCore.Converters
{
	public class BitmapConverter
	{
		static readonly internal Dictionary<uint, ByteBitmap> ByteImagesNew = new Dictionary<uint, ByteBitmap>();
		static readonly internal Dictionary<uint, FloatBitmap> FloatImagesNew = new Dictionary<uint, FloatBitmap>();

		static readonly private object bytelocker = new object();
		static readonly private object floatlocker = new object();

		public static void ApplyGammaToTextures(float gamma)
		{
			lock (bytelocker)
			{
				foreach (var v in ByteImagesNew.Values)
				{
					v.ApplyGamma(gamma);
				}
			}

			lock (floatlocker)
			{
				foreach (var v in FloatImagesNew.Values)
				{
					v.ApplyGamma(gamma);
				}
			}
		}

		/// <summary>
		/// Get material bitmap from texture evaluator
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="rm"></param>
		/// <param name="renderTexture"></param>
		/// <param name="textureType"></param>
		internal static void MaterialBitmapFromEvaluator(ref ShaderBody shader, RenderMaterial rm, RenderTexture renderTexture, RenderMaterial.StandardChildSlots textureType)
		{
			if (renderTexture == null) return;

			var rId = renderTexture.RenderHashWithoutLocalMapping;

			var rhinotfm = renderTexture.LocalMappingTransform;

			var projectionMode = renderTexture.GetProjectionMode();
			var envProjectionMode = renderTexture.GetInternalEnvironmentMappingMode();
			var repeat = renderTexture.GetWrapType() == TextureWrapType.Repeating;

			using (var textureEvaluator = renderTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
			{
				SimulatedTexture st = textureEvaluator == null ? renderTexture.SimulatedTexture(RenderTexture.TextureGeneration.Disallow) : null;
				using (
					var actualEvaluator = textureEvaluator ?? RenderTexture.NewBitmapTexture(st).CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
				{
					InternalMaterialBitmapFromEvaluator(shader, renderTexture, textureType, rhinotfm, rId, actualEvaluator, projectionMode, envProjectionMode, repeat);

				}
			}
		}

		private static void InternalMaterialBitmapFromEvaluator(ShaderBody shader, RenderTexture renderTexture,
			RenderMaterial.StandardChildSlots textureType, Rhino.Geometry.Transform rhinotfm, uint rId, TextureEvaluator actualEvaluator,
			TextureProjectionMode projectionMode, TextureEnvironmentMappingMode envProjectionMode, bool repeat)
		{
			int pheight;
			int pwidth;
			try
			{
				int u, v, w;
				renderTexture.PixelSize(out u, out v, out w);
				pheight = u;
				pwidth = v;
			}
			catch (Exception)
			{
				pheight = 1024;
				pwidth = 1024;
			}

			if (pheight == 0 || pwidth == 0)
			{
				pheight = 1024;
				pwidth = 1024;
			}

			Transform t = new Transform(
				rhinotfm.ToFloatArray(true)
				);

			var isFloat = renderTexture.IsHdrCapable();
			var isLinear = renderTexture.IsLinear();

			if (isFloat)
			{
				var img = RetrieveFloatsImg(rId, pwidth, pheight, actualEvaluator, false, false, isLinear);
				img.ApplyGamma(shader.Gamma);
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.IsLinear = isLinear;
						shader.DiffuseTexture.TexFloat = img.Data;
						shader.DiffuseTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.IsLinear = isLinear;
						shader.BumpTexture.TexFloat = img.Data;
						shader.BumpTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.IsLinear = isLinear;
						shader.TransparencyTexture.TexFloat = img.Data;
						shader.TransparencyTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
						shader.EnvironmentTexture.IsLinear = isLinear;
						shader.EnvironmentTexture.TexFloat = img.Data;
						shader.EnvironmentTexture.TexByte = null;
						break;
				}
			}
			else
			{
				var img = RetrieveBytesImg(rId, pwidth, pheight, actualEvaluator, false, false, isLinear);
				img.ApplyGamma(shader.Gamma);
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.IsLinear = isLinear;
						shader.DiffuseTexture.TexFloat = null;
						shader.DiffuseTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.IsLinear = isLinear;
						shader.BumpTexture.TexFloat = null;
						shader.BumpTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.IsLinear = isLinear;
						shader.TransparencyTexture.TexFloat = null;
						shader.TransparencyTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
						shader.EnvironmentTexture.IsLinear = isLinear;
						shader.EnvironmentTexture.TexFloat = null;
						shader.EnvironmentTexture.TexByte = img.Data;
						break;
				}
			}
			switch (textureType)
			{
				case RenderMaterial.StandardChildSlots.Diffuse:
					shader.DiffuseTexture.TexWidth = pwidth;
					shader.DiffuseTexture.TexHeight = pheight;
					shader.DiffuseTexture.ProjectionMode = projectionMode;
					shader.DiffuseTexture.EnvProjectionMode = envProjectionMode;
					shader.DiffuseTexture.Transform = t;
					shader.DiffuseTexture.Repeat = repeat;
					shader.DiffuseTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					break;
				case RenderMaterial.StandardChildSlots.Bump:
					shader.BumpTexture.TexWidth = pwidth;
					shader.BumpTexture.TexHeight = pheight;
					shader.BumpTexture.ProjectionMode = projectionMode;
					shader.BumpTexture.EnvProjectionMode = envProjectionMode;
					shader.BumpTexture.Transform = t;
					shader.BumpTexture.Repeat = repeat;
					shader.BumpTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					break;
				case RenderMaterial.StandardChildSlots.Transparency:
					shader.TransparencyTexture.TexWidth = pwidth;
					shader.TransparencyTexture.TexHeight = pheight;
					shader.TransparencyTexture.ProjectionMode = projectionMode;
					shader.TransparencyTexture.EnvProjectionMode = envProjectionMode;
					shader.TransparencyTexture.Transform = t;
					shader.TransparencyTexture.Repeat = repeat;
					shader.TransparencyTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					break;
				case RenderMaterial.StandardChildSlots.Environment:
					shader.EnvironmentTexture.TexWidth = pwidth;
					shader.EnvironmentTexture.TexHeight = pheight;
					// special texture, always set to Environment/Emap
					shader.EnvironmentTexture.ProjectionMode = TextureProjectionMode.EnvironmentMap;
					shader.EnvironmentTexture.EnvProjectionMode = TextureEnvironmentMappingMode.EnvironmentMap;
					shader.EnvironmentTexture.Transform = t;
					shader.EnvironmentTexture.Repeat = repeat;
					shader.EnvironmentTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					break;
			}
		}

		/// <summary>
		/// Get environment bitmap from texture evaluator
		/// </summary>
		/// <param name="rm"></param>
		/// <param name="teximg"></param>
		/// <param name="gamma"></param>
		/// <param name="floatAsByte"></param>
		/// <param name="planarProjection"></param>
		public static void EnvironmentBitmapFromEvaluator(RenderEnvironment rm, CyclesTextureImage teximg, float gamma, bool floatAsByte, bool planarProjection)
		{
			RenderTexture renderTexture = null;

			if(rm!=null)
				renderTexture = rm.FindChild("texture") as RenderTexture;

			if (renderTexture == null)
			{
				teximg.TexByte = null;
				teximg.TexFloat = null;
				teximg.TexWidth = teximg.TexHeight = 0;
				teximg.Name = "";
				return;
			}

			var rId = renderTexture.RenderHashWithoutLocalMapping;

			var rhinotfm = renderTexture.LocalMappingTransform;

			using (var textureEvaluator = renderTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
			{
				if (textureEvaluator == null)
				{
					teximg.TexByte = null;
					teximg.TexFloat = null;
					teximg.TexWidth = teximg.TexHeight = 0;
					teximg.Name = "";
					return;
				}
				try
				{
					int u, v, w;
					renderTexture.PixelSize(out u, out v, out w);
					teximg.TexWidth = u;
					teximg.TexHeight = v;
				}
				catch (Exception)
				{
					teximg.TexHeight = teximg.TexWidth = 1024;
				}

				if (teximg.TexHeight == 0 || teximg.TexWidth == 0)
				{
					teximg.TexHeight = teximg.TexWidth = 1024;
				}

				Transform t = new Transform(
					rhinotfm.ToFloatArray(true)
					);

				var isFloat = renderTexture.IsHdrCapable();

				var isLinear = teximg.IsLinear = renderTexture.IsLinear();

				if (isFloat && !floatAsByte)
				{
					var img = RetrieveFloatsImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, true, planarProjection, isLinear);
					img.ApplyGamma(gamma);
					teximg.TexFloat = img.Data;
					teximg.TexByte = null;
				}
				else
				{
					var img = RetrieveBytesImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, true, planarProjection, isLinear);
					img.ApplyGamma(gamma);
					teximg.TexByte = img.Data;
					teximg.TexFloat = null;
				}
				teximg.Name = rId.ToString(CultureInfo.InvariantCulture);

				teximg.Transform = t;
			}
		}

		public static ByteBitmap ReadByteBitmapFromBitmap(uint id, int pwidth, int pheight, Bitmap bm)
		{
			var upixel = new byte[pwidth * pheight * 4];

			for (var x = 0; x < pwidth; x++)
			{
				for (var y = 0; y < pheight; y++)
				{
					Color px = bm.GetPixel(x, y);
					var offset = x * 4 + pwidth * y * 4;
					upixel[offset] = px.R;
					upixel[offset + 1] = px.G;
					upixel[offset + 2] = px.B;
					upixel[offset + 3] = px.A;
				}
			}
			return new ByteBitmap(id, upixel, pwidth, pheight, false);
		}

		private static byte[] ReadByteBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnvironment, bool planarProjection)
		{
			var upixel = new byte[pwidth * pheight * 4];
			var halfpixelU = 0.5 / pwidth;
			var halfpixelV = 0.5 / pheight;
			var duvw = new Vector3d(halfpixelU, halfpixelV, 0.0);

			for (var x = 0; x < pwidth; x++)
			{
				for (var y = 0; y < pheight; y++)
				{
					var fx = x / (float)pwidth + halfpixelU;
					if (isEnvironment && !planarProjection) fx += 0.5f;
					var fy = y / (float)pheight + halfpixelV;
					if (planarProjection) fy = 1.0f - fy;

					// remember z can be !0.0 for volumetrics
					var col4F = textureEvaluator.GetColor(new Point3d(fx, fy, 0.0), duvw, duvw);
					var offset = x * 4 + pwidth * y * 4;
					upixel[offset] = (byte)(Math.Min(col4F.R, 1.0f) * 255.0f);
					upixel[offset + 1] = (byte)(Math.Min(col4F.G, 1.0f) * 255.0f);
					upixel[offset + 2] = (byte)(Math.Min(col4F.B, 1.0f) * 255.0f);
					upixel[offset + 3] = (byte)(Math.Min(col4F.A, 1.0f) * 255.0f);
				}
			}
			return upixel;
		}

		/// <summary>
		/// Read image as float array from texture evaluator.
		/// </summary>
		/// <param name="pwidth"></param>
		/// <param name="pheight"></param>
		/// <param name="textureEvaluator"></param>
		/// <param name="isEnvironment"></param>
		/// <param name="planarProjection"></param>
		/// <returns></returns>
		private static float[] ReadFloatBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnvironment, bool planarProjection)
		{
			var fpixel = new float[pwidth*pheight*4];
			var halfpixelU = 0.5 / pwidth;
			var halfpixelV = 0.5 / pheight;
			var duvw = new Vector3d(halfpixelU, halfpixelV, 0.0);

			for (var x = 0; x < pwidth; x++)
			{
				for (var y = 0; y < pheight; y++)
				{
					var fx = x/(float) pwidth + halfpixelU;
					if (isEnvironment && !planarProjection) fx += 0.5f;
					var fy = y/(float) pheight + halfpixelV;
					if (planarProjection) fy = 1.0f - fy;

					// remember z can be !0.0 for volumetrics
					var col4F = textureEvaluator.GetColor(new Point3d(fx, fy, 0.0), duvw, duvw);
					var offset = x*4 + pwidth*y*4;
					fpixel[offset] = col4F.R;
					fpixel[offset + 1] = col4F.G;
					fpixel[offset + 2] = col4F.B;
					fpixel[offset + 3] = col4F.A;
				}
			}
			return fpixel;
		}

		private static ByteBitmap RetrieveBytesImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnv, bool planarProjection, bool isLinear)
		{
			var read = ByteImagesNew.ContainsKey(rId);
			var img = read ? ByteImagesNew[rId] : new ByteBitmap(rId, ReadByteBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isEnv, planarProjection), pwidth, pheight, isLinear);
			if (!read)
			{
				if(RcCore.It.EngineSettings.SaveDebugImages) img.SaveBitmaps();
				lock (bytelocker)
				{
					ByteImagesNew[rId] = img;
				}
			}

			return img;
		}

		private static FloatBitmap RetrieveFloatsImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnv, bool planarProjection, bool isLinear)
		{
			var read = FloatImagesNew.ContainsKey(rId);
			var img = read ? FloatImagesNew[rId] : new FloatBitmap(rId, ReadFloatBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isEnv, planarProjection), pwidth, pheight, isLinear);
			if (!read)
			{
				if(RcCore.It.EngineSettings.SaveDebugImages) img.SaveBitmaps();
				lock (floatlocker)
				{
					FloatImagesNew[rId] = img;
				}
			}

			return img;
		}

	}
}
