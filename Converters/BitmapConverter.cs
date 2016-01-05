/**
Copyright 2014-2015 Robert McNeel and Associates

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
using System.Globalization;
using Rhino.Geometry;
using Rhino.Render;
using Transform = ccl.Transform;
using System.Drawing;

namespace RhinoCycles
{
	public class BitmapConverter
	{
		static readonly internal Dictionary<uint, ByteBitmap> byte_images_new = new Dictionary<uint, ByteBitmap>();
		static readonly internal Dictionary<uint, FloatBitmap> float_images_new = new Dictionary<uint, FloatBitmap>();

		public static void ApplyGammaToTextures(float gamma)
		{
			foreach (var v in byte_images_new.Values)
			{
				v.ApplyGamma(gamma);
			}

			foreach (var v in float_images_new.Values)
			{
				v.ApplyGamma(gamma);
			}
		}

		/// <summary>
		/// Get material bitmap from texture evaluator
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="rm"></param>
		/// <param name="renderTexture"></param>
		/// <param name="channelName"></param>
		/// <param name="textureType"></param>
		internal static void MaterialBitmapFromEvaluator(ref CyclesShader shader, RenderMaterial rm, RenderTexture renderTexture, string channelName, RenderMaterial.StandardChildSlots textureType)
		{
			if (renderTexture == null || !rm.ChildSlotOn(channelName)) return;

			var rId = renderTexture.RenderHashWithoutLocalMapping;

			var rhinotfm = renderTexture.LocalMappingTransform;

			var projection_mode = renderTexture.GetProjectionMode();
			var env_projection_mode = renderTexture.GetInternalEnvironmentMappingMode();

			var texture_evaluator = renderTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping);
			int pheight;
			int pwidth;
			try
			{
				pheight = Convert.ToInt32(renderTexture.GetParameter("pixel-height"));
				pwidth = Convert.ToInt32(renderTexture.GetParameter("pixel-width"));
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

			var is_float = renderTexture.IsHdrCapable();

			if (is_float)
			{
				var img = RetrieveFloatsImg(rId, pwidth, pheight, texture_evaluator, false);
				img.ApplyGamma(shader.Gamma);
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.TexFloat = img.Data;
						shader.DiffuseTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.TexFloat = img.Data;
						shader.BumpTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.TexFloat = img.Data;
						shader.TransparencyTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
						shader.EnvironmentTexture.TexFloat = img.Data;
						shader.EnvironmentTexture.TexByte = null;
						break;
				}
			}
			else
			{
				var img = RetrieveBytesImg(rId, pwidth, pheight, texture_evaluator, false);
				img.ApplyGamma(shader.Gamma);
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.TexFloat = null;
						shader.DiffuseTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.TexFloat = null;
						shader.BumpTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.TexFloat = null;
						shader.TransparencyTexture.TexByte = img.Data;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
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
					shader.DiffuseTexture.ProjectionMode = projection_mode;
					shader.DiffuseTexture.EnvProjectionMode = env_projection_mode;
					shader.DiffuseTexture.Transform = t;
					shader.DiffuseTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.DiffuseTexture.IsLinear = renderTexture.IsLinear();
					break;
				case RenderMaterial.StandardChildSlots.Bump:
					shader.BumpTexture.TexWidth = pwidth;
					shader.BumpTexture.TexHeight = pheight;
					shader.BumpTexture.ProjectionMode = projection_mode;
					shader.BumpTexture.EnvProjectionMode = env_projection_mode;
					shader.BumpTexture.Transform = t;
					shader.BumpTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.BumpTexture.IsLinear = renderTexture.IsLinear();
					break;
				case RenderMaterial.StandardChildSlots.Transparency:
					shader.TransparencyTexture.TexWidth = pwidth;
					shader.TransparencyTexture.TexHeight = pheight;
					shader.TransparencyTexture.ProjectionMode = projection_mode;
					shader.TransparencyTexture.EnvProjectionMode = env_projection_mode;
					shader.TransparencyTexture.Transform = t;
					shader.TransparencyTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.TransparencyTexture.IsLinear = renderTexture.IsLinear();
					break;
				case RenderMaterial.StandardChildSlots.Environment:
					shader.EnvironmentTexture.TexWidth = pwidth;
					shader.EnvironmentTexture.TexHeight = pheight;
					// special texture, always set to Environment/Emap
					shader.EnvironmentTexture.ProjectionMode = TextureProjectionMode.EnvironmentMap;
					shader.EnvironmentTexture.EnvProjectionMode = TextureEnvironmentMappingMode.EnvironmentMap;
					shader.EnvironmentTexture.Transform = t;
					shader.EnvironmentTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.EnvironmentTexture.IsLinear = renderTexture.IsLinear();
					break;
			}
		}

		/// <summary>
		/// Get environment bitmap from texture evaluator
		/// </summary>
		/// <param name="rm"></param>
		/// <param name="teximg"></param>
		/// <param name="gamma"></param>
		public static void EnvironmentBitmapFromEvaluator(RenderEnvironment rm, CyclesTextureImage teximg, float gamma)
		{
			RenderTexture render_texture = null;

			if(rm!=null)
				render_texture = rm.FindChild("texture") as RenderTexture;

			if (render_texture == null)
			{
				teximg.TexByte = null;
				teximg.TexFloat = null;
				teximg.TexWidth = teximg.TexHeight = 0;
				teximg.Name = "";
				return;
			}

			var rId = render_texture.RenderHashWithoutLocalMapping;

			var rhinotfm = render_texture.LocalMappingTransform;

			using (
				var texture_evaluator = render_texture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
			{
				try
				{
					teximg.TexHeight = Convert.ToInt32(render_texture.GetParameter("pixel-height"));
					teximg.TexWidth = Convert.ToInt32(render_texture.GetParameter("pixel-width"));
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

				var is_float = render_texture.IsHdrCapable();

				teximg.IsLinear = render_texture.IsLinear();

				if (is_float)
				{
					var img = RetrieveFloatsImg(rId, teximg.TexWidth, teximg.TexHeight, texture_evaluator, true);
					img.ApplyGamma(gamma);
					teximg.TexFloat = img.Data;
					teximg.TexByte = null;
				}
				else
				{
					var img = RetrieveBytesImg(rId, teximg.TexWidth, teximg.TexHeight, texture_evaluator, true);
					img.ApplyGamma(gamma);
					teximg.TexByte = img.Data;
					teximg.TexFloat = null;
				}
				teximg.Name = rId.ToString(CultureInfo.InvariantCulture);

				teximg.Transform = t;
			}
		}

		public static byte[] ReadByteBitmapFromBitmap(int pwidth, int pheight, Bitmap bm)
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
			return upixel;
		}

		private static byte[] ReadByteBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnvironment)
		{
			var upixel = new byte[pwidth * pheight * 4];
			var zerovector = new Vector3d(0.0, 0.0, 0.0);

			for (var x = 0; x < pwidth; x++)
			{
				for (var y = 0; y < pheight; y++)
				{
					var fx = x / (float)pwidth;
					if (isEnvironment) fx += 0.5f;
					var fy = y / (float)pheight;

					// remember z can be !0.0 for volumetrics
					var col4_f = textureEvaluator.GetColor(new Point3d(fx, fy, 0.0), zerovector, zerovector);
					var offset = x * 4 + pwidth * y * 4;
					upixel[offset] = (byte)(col4_f.R * 255.0f);
					upixel[offset + 1] = (byte)(col4_f.G * 255.0f);
					upixel[offset + 2] = (byte)(col4_f.B * 255.0f);
					upixel[offset + 3] = (byte)(col4_f.A * 255.0f);
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
		/// <param name="gamma"></param>
		/// <returns></returns>
		private static float[] ReadFloatBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isEnvironment)
		{
			var fpixel = new float[pwidth*pheight*4];
			var zerovector = new Vector3d(0.0, 0.0, 0.0);

			for (var x = 0; x < pwidth; x++)
			{
				for (var y = 0; y < pheight; y++)
				{
					var fx = x/(float) pwidth;
					if (isEnvironment) fx += 0.5f;
					var fy = y/(float) pheight;

					// remember z can be !0.0 for volumetrics
					var col4_f = textureEvaluator.GetColor(new Point3d(fx, fy, 0.0), zerovector, zerovector);
					var offset = x*4 + pwidth*y*4;
					fpixel[offset] = col4_f.R;
					fpixel[offset + 1] = col4_f.G;
					fpixel[offset + 2] = col4_f.B;
					fpixel[offset + 3] = col4_f.A;
				}
			}
			return fpixel;
		}

		private static ByteBitmap RetrieveBytesImg(uint rId, int pwidth, int pheight, TextureEvaluator texture_evaluator, bool isEnv)
		{
			var read = byte_images_new.ContainsKey(rId);
			var img = read ? byte_images_new[rId] : new ByteBitmap(rId, ReadByteBitmapFromEvaluator(pwidth, pheight, texture_evaluator, isEnv), pwidth, pheight);
			if (!read)
			{
				byte_images_new[rId] = img;
			}

			return img;
		}

		private static FloatBitmap RetrieveFloatsImg(uint rId, int pwidth, int pheight, TextureEvaluator texture_evaluator, bool isEnv)
		{
			var read = float_images_new.ContainsKey(rId);
			var img = read ? float_images_new[rId] : new FloatBitmap(rId, ReadFloatBitmapFromEvaluator(pwidth, pheight, texture_evaluator, isEnv), pwidth, pheight);
			if (!read)
			{
				float_images_new[rId] = img;
			}

			return img;
		}

	}
}
