/**
Copyright 2014-2021 Robert McNeel and Associates

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
			foreach(ByteBitmap byteBitmap in ByteImagesNew.Values)
			{
				byteBitmap.Dispose();
			}
			ByteImagesNew.Clear();
			foreach(FloatBitmap floatBitmap in FloatImagesNew.Values)
			{
				floatBitmap.Dispose();
			}
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

		private TextureEnvironmentMappingMode get_environment_mapping(RenderEnvironment rm, RenderTexture renderTexture)
		{
			var s = rm.GetParameter("background-projection") as IConvertible;
			string proj = "";
			if (s == null)
			{
				SimulatedEnvironment simenv = rm.SimulateEnvironment(true);
				proj = SimulatedEnvironment.StringFromProjection(simenv.BackgroundProjection);
			}
			else
			{
				proj = Convert.ToString(s, CultureInfo.InvariantCulture);
			}

			switch (proj)
			{
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
			if (azimob != null)
			{
				azi = (float)Convert.ToDouble(azimob);
			}
			if (altob != null)
			{
				alti = (float)Convert.ToDouble(altob);
			}
			if (intensityob != null)
			{
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
				if (!canUse) { teximg.TexHeight = teximg.TexWidth = 1; }

				var isFloat = renderTexture.IsHdrCapable();

				var isLinear = teximg.IsLinear = renderTexture.IsLinear();
				var isImageBased = renderTexture.IsImageBased();

				if (isFloat)
				{
					var img = RetrieveFloatsImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, isLinear, isImageBased, canUse, false, projection == (TextureEnvironmentMappingMode)4); ;
					img.ApplyGamma(gamma);
					teximg.TexFloat = img.Data as SimpleArrayFloat;
					teximg.TexByte = null;
				}
				else
				{
					var img = RetrieveBytesImg(rId, teximg.TexWidth, teximg.TexHeight, textureEvaluator, isLinear, isImageBased, canUse, false, projection == (TextureEnvironmentMappingMode)4);
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

			if ((int)projection != 4)
			{

				rhinotfm.M00 = tra.X;
				rhinotfm.M01 = tra.Y;
				rhinotfm.M02 = tra.Z;

				rhinotfm.M10 = rep.X;
				rhinotfm.M11 = rep.Y;
				rhinotfm.M12 = 1.0f; // rep.Z;

				rhinotfm.M20 = alti;
				rhinotfm.M21 = 0; // alti;
				rhinotfm.M22 = azi + (-Rhino.RhinoMath.ToRadians(rot.Z));
			}
			else
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
			bool _flip;
			public MyDumbBitmapByteList(Bitmap bm, bool flip)
			{
				_bitmap = bm;
				_flip = flip;
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

				if(_flip) {
					for (var y = 0; y < height; y++)
					{
						for (var x = 0; x < width; x++)
						{
							Color px = _bitmap.GetPixel(x, y);

							yield return px.R;
							yield return px.G;
							yield return px.B;
							yield return px.A;
						}
					}
				} else
				{
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
			}
		};

		public ByteBitmap ReadByteBitmapFromBitmap(uint id, int pwidth, int pheight, Bitmap bm, bool flip)
		{
			var read = ByteImagesNew.ContainsKey(id);
			var img = read ? ByteImagesNew[id] : new ByteBitmap(id, new SimpleArrayByte(new MyDumbBitmapByteList(bm, flip)), pwidth, pheight, false);
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
			bool _flip;
			public EvaluatorToByteList(TextureEvaluator eval, int width, int height, bool image_based, bool flip)
			{
				_eval = eval;
				_width = width;
				_height = height;
				_image_based = image_based;
				_flip = flip;
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

				if (_flip)
				{
					for (var y = _height; y >= 0; y--)
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
				else
				{
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
			}
		};


		private SimpleArrayByte ReadByteBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			if (!hasTransparentColor && !flip)
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
			}

			//Otherwise, we do this the slow way.
			return new SimpleArrayByte(new EvaluatorToByteList(textureEvaluator, pwidth, pheight, isImageBased, flip));
		}





		class EvaluatorToFloatList : IEnumerable<float>
		{
			TextureEvaluator _eval;
			int _width, _height;
			bool _image_based;
			bool _flip;
			public EvaluatorToFloatList(TextureEvaluator eval, int width, int height, bool image_based, bool flip)
			{
				_eval = eval;
				_width = width;
				_height = height;
				_image_based = image_based;
				_flip = flip;
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

				if (_flip)
				{
					for (var y = _height; y >= 0; y--)
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
				else
				{
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
			}
		};

		private SimpleArrayFloat ReadFloatBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			if (!hasTransparentColor && !flip)
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
			}

			//Otherwise, we do this the slow way.
			return new SimpleArrayFloat(new EvaluatorToFloatList(textureEvaluator, pwidth, pheight, isImageBased, flip));
		}

		public ByteBitmap RetrieveBytesImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isLinear, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			var read = ByteImagesNew.ContainsKey(rId);
			var img = read ? ByteImagesNew[rId] : new ByteBitmap(rId, ReadByteBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isImageBased, canUse, hasTransparentColor, flip), pwidth, pheight, isLinear);
			if (!read)
			{
				if (RcCore.It.AllSettings.SaveDebugImages) img.SaveBitmaps();
				lock (bytelocker)
				{
					ByteImagesNew[rId] = img;
				}
			}

			return img;
		}

		public FloatBitmap RetrieveFloatsImg(uint rId, int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isLinear, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			var read = FloatImagesNew.ContainsKey(rId);
			var img = read ? FloatImagesNew[rId] : new FloatBitmap(rId, ReadFloatBitmapFromEvaluator(pwidth, pheight, textureEvaluator, isImageBased, canUse, hasTransparentColor, flip), pwidth, pheight, isLinear);
			if (!read)
			{
				if (RcCore.It.AllSettings.SaveDebugImages) img.SaveBitmaps();
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
