/**
Copyright 2014-2017 Robert McNeel and Associates

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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Rhino.Geometry;
using Rhino.Render;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using Transform = ccl.Transform;
using Rhino.Runtime.InteropWrappers;

namespace RhinoCyclesCore.Converters
{
	public class BitmapConverter : IDisposable
	{
		readonly internal ConcurrentDictionary<uint, ByteBitmap> ByteImagesNew = new ConcurrentDictionary<uint, ByteBitmap>();
		readonly internal ConcurrentDictionary<uint, FloatBitmap> FloatImagesNew = new ConcurrentDictionary<uint, FloatBitmap>();

		readonly private object bytelocker = new object();
		readonly private object floatlocker = new object();
		private bool disposedValue;

		public void ReloadTextures(CyclesShader shader)
		{
			shader.ReloadTextures(ByteImagesNew, FloatImagesNew);
		}

		public void ClearTextureMemory()
		{
			ByteImagesNew.Clear();
			FloatImagesNew.Clear();
		}

		public void ApplyGammaToTextures(float gamma)
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
		public void MaterialBitmapFromEvaluator(ref ShaderBody shader, RenderTexture renderTexture, RenderMaterial.StandardChildSlots textureType)
		{
			if (renderTexture == null) return;

			var rId = renderTexture.RenderHashWithoutLocalMapping;

			var rhinotfm = renderTexture.LocalMappingTransform;
			var rotationvec = renderTexture.GetRotation();
			var repeatvec = renderTexture.GetRepeat();
			var offsetvec = renderTexture.GetOffset();

			Transform tt = new Transform(
				(float)offsetvec.X, (float)offsetvec.Y, (float)offsetvec.Z, 0.0f,
				(float)repeatvec.X, (float)repeatvec.Y, (float)repeatvec.Z, 0.0f,
				(float)rotationvec.X, (float)rotationvec.Y, (float)rotationvec.Z, 0.0f
			);

			var projectionMode = renderTexture.GetProjectionMode();
			var envProjectionMode = renderTexture.GetInternalEnvironmentMappingMode();
			var repeat = renderTexture.GetWrapType() == TextureWrapType.Repeating;

			using (var textureEvaluator = renderTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
			{
				SimulatedTexture st = textureEvaluator == null ? renderTexture.SimulatedTexture(RenderTexture.TextureGeneration.Disallow) : null;
				using (
					var actualEvaluator = textureEvaluator ?? RenderTexture.NewBitmapTexture(st, renderTexture.DocumentAssoc).CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
				{
					InternalMaterialBitmapFromEvaluator(shader, renderTexture, textureType, tt.ToRhinoTransform(), rId, actualEvaluator, projectionMode, envProjectionMode, repeat);

				}
			}
		}

		private void InternalMaterialBitmapFromEvaluator(ShaderBody shader, RenderTexture renderTexture,
			RenderMaterial.StandardChildSlots textureType, Rhino.Geometry.Transform rhinotfm, uint rId, TextureEvaluator actualEvaluator,
			TextureProjectionMode projectionMode, TextureEnvironmentMappingMode envProjectionMode, bool repeat)
		{
			bool canUse = actualEvaluator.Initialize();
			int pheight;
			int pwidth;
			try
			{
				renderTexture.PixelSize(out int u, out int v, out int w);
				pwidth = u;
				pheight = v;
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

			if(!canUse) { pheight = 1; pwidth = 1; }

			Transform t = rhinotfm.ToCyclesTransform();

			var isFloat = renderTexture.IsHdrCapable();
			var isLinear = renderTexture.IsLinear();
			var isImageBased = renderTexture.IsImageBased();
			var alternateob= renderTexture.GetParameter("mirror-alternate-tiles");
			var alternate = false;

			if (alternateob != null)
			{
				alternate = Convert.ToBoolean(alternateob);
			}

			if (isFloat)
			{
				var img = RetrieveFloatsImg(rId, pwidth, pheight, actualEvaluator, isLinear, isImageBased, canUse);
				if (textureType == RenderMaterial.StandardChildSlots.Diffuse
					|| textureType == RenderMaterial.StandardChildSlots.Environment)
				{
					img.ApplyGamma(shader.Gamma);
				}
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.IsLinear = isLinear;
						shader.DiffuseTexture.TexFloat = img.Data as SimpleArrayFloat;
						shader.DiffuseTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.IsLinear = isLinear;
						shader.BumpTexture.TexFloat = img.Data as SimpleArrayFloat;
						shader.BumpTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.IsLinear = isLinear;
						shader.TransparencyTexture.TexFloat = img.Data as SimpleArrayFloat;
						shader.TransparencyTexture.TexByte = null;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
						shader.EnvironmentTexture.IsLinear = isLinear;
						shader.EnvironmentTexture.TexFloat = img.Data as SimpleArrayFloat;
						shader.EnvironmentTexture.TexByte = null;
						break;
				}
			}
			else
			{
				var img = RetrieveBytesImg(rId, pwidth, pheight, actualEvaluator, isLinear, isImageBased, canUse);
				if (textureType == RenderMaterial.StandardChildSlots.Diffuse
					|| textureType == RenderMaterial.StandardChildSlots.Environment)
				{
					img.ApplyGamma(shader.Gamma);
				}
				switch (textureType)
				{
					case RenderMaterial.StandardChildSlots.Diffuse:
						shader.DiffuseTexture.IsLinear = isLinear;
						shader.DiffuseTexture.TexFloat = null;
						shader.DiffuseTexture.TexByte = img.Data as SimpleArrayByte;
						break;
					case RenderMaterial.StandardChildSlots.Bump:
						shader.BumpTexture.IsLinear = isLinear;
						shader.BumpTexture.TexFloat = null;
						shader.BumpTexture.TexByte = img.Data as SimpleArrayByte;
						break;
					case RenderMaterial.StandardChildSlots.Transparency:
						shader.TransparencyTexture.IsLinear = isLinear;
						shader.TransparencyTexture.TexFloat = null;
						shader.TransparencyTexture.TexByte = img.Data as SimpleArrayByte;
						break;
					case RenderMaterial.StandardChildSlots.Environment:
						shader.EnvironmentTexture.IsLinear = isLinear;
						shader.EnvironmentTexture.TexFloat = null;
						shader.EnvironmentTexture.TexByte = img.Data as SimpleArrayByte;
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
					shader.DiffuseTexture.AlternateTiles = alternate;
					break;
				case RenderMaterial.StandardChildSlots.Bump:
					shader.BumpTexture.TexWidth = pwidth;
					shader.BumpTexture.TexHeight = pheight;
					shader.BumpTexture.ProjectionMode = projectionMode;
					shader.BumpTexture.EnvProjectionMode = envProjectionMode;
					shader.BumpTexture.Transform = t;
					shader.BumpTexture.Repeat = repeat;
					shader.BumpTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.BumpTexture.AlternateTiles = alternate;
					break;
				case RenderMaterial.StandardChildSlots.Transparency:
					shader.TransparencyTexture.TexWidth = pwidth;
					shader.TransparencyTexture.TexHeight = pheight;
					shader.TransparencyTexture.ProjectionMode = projectionMode;
					shader.TransparencyTexture.EnvProjectionMode = envProjectionMode;
					shader.TransparencyTexture.Transform = t;
					shader.TransparencyTexture.Repeat = repeat;
					shader.TransparencyTexture.Name = rId.ToString(CultureInfo.InvariantCulture);
					shader.TransparencyTexture.AlternateTiles = alternate;
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
					shader.EnvironmentTexture.AlternateTiles = alternate;
					break;
			}
		}

		private TextureEnvironmentMappingMode get_environment_mapping(RenderEnvironment rm, RenderTexture renderTexture)
		{
			var s = rm.GetParameter("background-projection") as IConvertible;
			string proj = "";
			if (s == null) {
				SimulatedEnvironment simenv = rm.SimulateEnvironment(true);
				proj = SimulatedEnvironment.StringFromProjection(simenv.BackgroundProjection);
			}
			else {
				proj = Convert.ToString(s, CultureInfo.InvariantCulture);
			}

			switch (proj) {
			case "automatic":
					return renderTexture.GetEnvironmentMappingMode();
			case "box":
					return TextureEnvironmentMappingMode.Box;
			case "cubemap":
					return TextureEnvironmentMappingMode.Cube;
			case "emap":
					return TextureEnvironmentMappingMode.EnvironmentMap;
			case "horizontal-cross-cubemap":
					return TextureEnvironmentMappingMode.HorizontalCrossCube;
			case "vertical-cross-cubemap":
					return TextureEnvironmentMappingMode.VerticalCrossCube;
			case "hemispherical":
					return TextureEnvironmentMappingMode.Hemispherical;
			case "lightprobe":
					return TextureEnvironmentMappingMode.LightProbe;
			case "spherical":
					return TextureEnvironmentMappingMode.Spherical;
				default: // default (non existing planar)
					return (TextureEnvironmentMappingMode)4;
				}
		}

		/// <summary>
		/// Get environment bitmap from texture evaluator
		/// </summary>
		/// <param name="rm"></param>
		/// <param name="teximg"></param>
		/// <param name="gamma"></param>
		/// <param name="floatAsByte"></param>
		public void EnvironmentBitmapFromEvaluator(RenderEnvironment rm, CyclesTextureImage teximg, float gamma)
		{
			RenderTexture renderTexture = null;

			if (rm != null)
				renderTexture = rm.FindChild("texture") as RenderTexture;

			if (renderTexture == null)
			{
				teximg.TexByte = null;
				teximg.TexFloat = null;
				teximg.TexWidth = teximg.TexHeight = 0;
				teximg.Name = "";
				return;
			}

			var projection = get_environment_mapping(rm, renderTexture);
			var rhinotfm = renderTexture.LocalMappingTransform;
			var guid = renderTexture.TypeId;
			var nm = renderTexture.TypeName;
			var rot = renderTexture.GetRotation();
			var rep = renderTexture.GetRepeat();
			var tra = renderTexture.GetOffset();
			var rId = renderTexture.RenderHashExclude(TextureRenderHashFlags.ExcludeLocalMapping, "azimuth;altitude;multiplier;rdk-texture-repeat;rdk-texture-offset;rdk-texture-rotation;rdk-texture-adjust-multiplier;intensity");
			var azimob = renderTexture.GetParameter("azimuth");
			var altob = renderTexture.GetParameter("altitude");
			var multob = renderTexture.GetParameter("multiplier");
			var intensityob = renderTexture.GetParameter("intensity");
			var multadjob = renderTexture.GetParameter("rdk-texture-adjust-multiplier");
			var mult = 1.0f;
			var multadj = 1.0f;
			var azi = 0.0f;
			var alti = 0.0f;
			var intensity = 1.0f;

			if (multob != null)
			{
				mult = (float)Convert.ToDouble(multob);
			}
			if (multadjob != null)
			{
				multadj = (float)Convert.ToDouble(multadjob);
			}
			if (azimob != null) {
				azi = (float)Convert.ToDouble(azimob);
			}
			if (altob != null) {
				alti = (float)Convert.ToDouble(altob);
			}
			if (intensityob != null) {
				intensity = (float)Convert.ToDouble(intensityob);
			}

			var restore =
				!multadj.FuzzyEquals(1.0f)
				|| !mult.FuzzyEquals(1.0f)
				|| !azi.FuzzyEquals(0.0f)
				|| !alti.FuzzyEquals(0.0f)
				|| !intensity.FuzzyEquals(1.0f);

			if (restore)
			{
				renderTexture.BeginChange(RenderContent.ChangeContexts.Ignore);
				renderTexture.SetParameter("rdk-texture-adjust-multiplier", 1.0);
				renderTexture.SetParameter("azimuth", 0.0);
				renderTexture.SetParameter("altitude", 0.0);
				renderTexture.SetParameter("intensity", 1.0);
				renderTexture.SetParameter("multiplier", 1.0);
				renderTexture.EndChange();
			}

			using (var textureEvaluator =
				renderTexture.CreateEvaluator(
					RenderTexture.TextureEvaluatorFlags.DisableLocalMapping |
					RenderTexture.TextureEvaluatorFlags.DisableProjectionChange |
					RenderTexture.TextureEvaluatorFlags.DisableFiltering
					))
			{
				if (textureEvaluator == null)
				{
					teximg.TexByte = null;
					teximg.TexFloat = null;
					teximg.TexWidth = teximg.TexHeight = 0;
					teximg.Name = "";
					return;
				}
				bool canUse = textureEvaluator.Initialize();
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
				if(!canUse) { teximg.TexHeight = teximg.TexWidth = 1; }

				var isFloat = renderTexture.IsHdrCapable();

				var isLinear = teximg.IsLinear = renderTexture.IsLinear();
				var isImageBased = renderTexture.IsImageBased();

				if (isFloat)
				{
					var img = RetrieveFloatsImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, isLinear, isImageBased, canUse);
					img.ApplyGamma(gamma);
					teximg.TexFloat = img.Data as SimpleArrayFloat;
					teximg.TexByte = null;
				}
				else
				{
					var img = RetrieveBytesImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, isLinear, isImageBased, canUse);
					img.ApplyGamma(gamma);
					teximg.TexByte = img.Data as SimpleArrayByte;
					teximg.TexFloat = null;
				}
				teximg.Name = rId.ToString(CultureInfo.InvariantCulture);

			}
			if (restore)
			{
				renderTexture.BeginChange(RenderContent.ChangeContexts.Ignore);
				renderTexture.SetParameter("rdk-texture-adjust-multiplier", (double)multadj);
				renderTexture.SetParameter("azimuth", (double)azi);
				renderTexture.SetParameter("altitude", (double)alti);
				renderTexture.SetParameter("intensity", (double)intensity);
				renderTexture.SetParameter("multiplier", (double)mult);
				renderTexture.EndChange();
			}
			teximg.EnvProjectionMode = projection;
			teximg.ProjectionMode = TextureProjectionMode.EnvironmentMap;

			if ((int)projection != 4) {

				rhinotfm.M00 = tra.X;
				rhinotfm.M01 = tra.Y;
				rhinotfm.M02 = tra.Z;

				rhinotfm.M10 = rep.X;
				rhinotfm.M11 = rep.Y;
				rhinotfm.M12 = rep.Z;

				rhinotfm.M20 = alti;
				rhinotfm.M21 = 0; // alti;
				rhinotfm.M22 = azi + (-Rhino.RhinoMath.ToRadians(rot.Z));
			} else
			{
				rhinotfm.M00 = tra.X;
				rhinotfm.M01 = tra.Y;
				rhinotfm.M02 = tra.Z;

				rhinotfm.M10 = rep.X;
				rhinotfm.M11 = rep.Y;
				rhinotfm.M12 = rep.Z;

				rhinotfm.M20 = Rhino.RhinoMath.ToRadians(rot.X);
				rhinotfm.M21 = Rhino.RhinoMath.ToRadians(rot.Y);
				rhinotfm.M22 = Rhino.RhinoMath.ToRadians(rot.Z);
			}

			Transform t = new Transform(
				rhinotfm.ToFloatArray(true)
				);
			teximg.Transform = t;
			teximg.Strength = mult * multadj;
		}



		class MyDumbBitmapByteList : IEnumerable<byte>
		{
			Bitmap _bitmap;
			public MyDumbBitmapByteList(Bitmap bm)
			{
				_bitmap = bm;
			}
			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}
			IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}
			IEnumerator<byte> GetEnumeratorImpl()
			{
				int width = _bitmap.Width;
				int height = _bitmap.Height;

				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						Color px = _bitmap.GetPixel(x, height - 1 - y);

						yield return px.R;
						yield return px.G;
						yield return px.B;
						yield return px.A;
					}
				}
			}
		};

	public ByteBitmap ReadByteBitmapFromBitmap(uint id, int pwidth, int pheight, Bitmap bm)
	{
		var read = ByteImagesNew.ContainsKey(id);
		var img = read ? ByteImagesNew[id] : new ByteBitmap(id, new SimpleArrayByte(new MyDumbBitmapByteList(bm)), pwidth, pheight, false);
		if (!read)
		{
			if (RcCore.It.AllSettings.SaveDebugImages) img.SaveBitmaps();
			lock (bytelocker)
			{
				ByteImagesNew[id] = img;
			}
		}
		return img;
	}


		class EvaluatorToByteList : IEnumerable<byte>
		{
			TextureEvaluator _eval;
			int _width, _height;
			bool _image_based;
			public EvaluatorToByteList(TextureEvaluator eval, int width, int height, bool image_based)
			{
				_eval = eval;
				_width = width;
				_height = height;
				_image_based = image_based;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}

			IEnumerator<byte> IEnumerable<byte>.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}

			IEnumerator<byte> GetEnumeratorImpl()
			{
				var halfpixelU = 0.5 / _width;
				var halfpixelV = 0.5 / _height;
				var duvw = new Vector3d(halfpixelU, halfpixelV, 0.0);

				if (_image_based)
				{
					duvw.X = duvw.Y = duvw.Z = 0.0;
				}

				var pt = new Point3d();
				var col4F = new Rhino.Display.Color4f();

				for (var y = 0; y < _height; y++)
				{
					for (var x = 0; x < _width; x++)
					{
						var fx = x / (float)_width + halfpixelU;
						var fy = y / (float)_height + halfpixelV;

						pt.X = fx;
						pt.Y = fy;
						pt.Z = 0.0;

						// remember z can be !0.0 for volumetrics
						_eval.GetColor(pt, duvw, duvw, ref col4F);

						yield return (byte)(col4F.R * 255.0);
						yield return (byte)(col4F.G * 255.0);
						yield return (byte)(col4F.B * 255.0);
						yield return (byte)(col4F.A * 255.0);
					}
				}
			}
	};


	private SimpleArrayByte ReadByteBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse)
	{
	  if (!canUse)
	  {
			Rhino.Display.Color4f c4f = new Rhino.Display.Color4f(1.0f, 1.0f, 1.0f, 1.0f);

			byte[] conv = new byte[4];
			c4f.ToArray(ref conv);

			return new SimpleArrayByte(conv);
	  }

		var bytes = textureEvaluator.WriteToByteArray(pwidth, pheight);
		if (null != bytes)
		{
			return bytes;
		}

		//Otherwise, we do this the slow way.
		return new SimpleArrayByte(new EvaluatorToByteList(textureEvaluator, pwidth, pheight, isImageBased));
	}





		class EvaluatorToFloatList : IEnumerable<float>
		{
			TextureEvaluator _eval;
			int _width, _height;
			bool _image_based;
			public EvaluatorToFloatList(TextureEvaluator eval, int width, int height, bool image_based)
			{
				_eval = eval;
				_width = width;
				_height = height;
				_image_based = image_based;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}

			IEnumerator<float> IEnumerable<float>.GetEnumerator()
			{
				return GetEnumeratorImpl();
			}

			IEnumerator<float> GetEnumeratorImpl()
			{
				var halfpixelU = 0.5 / _width;
				var halfpixelV = 0.5 / _height;
				var duvw = new Vector3d(halfpixelU, halfpixelV, 0.0);

				if (_image_based)
				{
					duvw.X = duvw.Y = duvw.Z = 0.0;
				}

				var pt = new Point3d();
				var col4F = new Rhino.Display.Color4f();

				for (var y = 0; y < _height; y++)
				{
					for (var x = 0; x < _width; x++)
					{
						var fx = x / (float)_width + halfpixelU;
						var fy = y / (float)_height + halfpixelV;

						pt.X = fx;
						pt.Y = fy;
						pt.Z = 0.0;

						// remember z can be !0.0 for volumetrics
						_eval.GetColor(pt, duvw, duvw, ref col4F);

						yield return col4F.R;
						yield return col4F.G;
						yield return col4F.B;
						yield return col4F.A;
					}
				}
			}
		};

		private SimpleArrayFloat ReadFloatBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse)
	{
			if (!canUse)
			{
				Rhino.Display.Color4f c4f = new Rhino.Display.Color4f(1.0f, 1.0f, 1.0f, 1.0f);

				float[] conv = new float[4];
				c4f.ToArray(ref conv);

				return new SimpleArrayFloat(conv);
			}

			var floats = textureEvaluator.WriteToFloatArray(pwidth, pheight);
			if (null != floats)
			{
				return floats;
			}

			//Otherwise, we do this the slow way.
			return new SimpleArrayFloat(new EvaluatorToFloatList(textureEvaluator, pwidth, pheight, isImageBased));
		}

	public ByteBitmap RetrieveBytesImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isLinear, bool isImageBased, bool canUse)
		{
			var read = ByteImagesNew.ContainsKey(rId);
			var img = read ? ByteImagesNew[rId] : new ByteBitmap(rId, ReadByteBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isImageBased, canUse), pwidth, pheight, isLinear);
			if (!read)
			{
				if(RcCore.It.AllSettings.SaveDebugImages) img.SaveBitmaps();
				lock (bytelocker)
				{
					ByteImagesNew[rId] = img;
				}
			}

			return img;
		}

		public FloatBitmap RetrieveFloatsImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isLinear, bool isImageBased, bool canUse)
		{
			var read = FloatImagesNew.ContainsKey(rId);
			var img = read ? FloatImagesNew[rId] : new FloatBitmap(rId, ReadFloatBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isImageBased, canUse), pwidth, pheight, isLinear);
			if (!read)
			{
				if(RcCore.It.AllSettings.SaveDebugImages) img.SaveBitmaps();
				lock (floatlocker)
				{
					FloatImagesNew[rId] = img;
				}
			}

			return img;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					ClearTextureMemory();
					// TODO: dispose managed state (managed objects)
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~BitmapConverter()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
