/**
Copyright 2014-2024 Robert McNeel and Associates

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

using Rhino;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Render.Fields;
using Rhino.Runtime.InteropWrappers;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Transform = ccl.Transform;

namespace RhinoCyclesCore.Converters
{
	public class BitmapConverter : IDisposable
	{
		readonly internal ConcurrentDictionary<uint, ByteBitmap> ByteImagesNew = new ConcurrentDictionary<uint, ByteBitmap>();
		readonly internal ConcurrentDictionary<uint, FloatBitmap> FloatImagesNew = new ConcurrentDictionary<uint, FloatBitmap>();

		readonly private object bytelocker = new object();
		readonly private object floatlocker = new object();
		private bool disposedValue;

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

		List<Guid> _specialIds = new List<Guid> {
			new Guid("6A4D9BEE-5B02-4BB6-9764-5B407240731A"), // HDRLS Environment
			new Guid("ABC95D68-BD66-4EB5-A72A-E3FA6C58CCC3"), // HDRLS Texture
			new Guid("f28c2d86-0466-40d4-89ee-a54b6c5e9288"), // High Dynamic Range bitmap
		};

		/// <summary>
		/// Get environment bitmap from texture evaluator
		/// </summary>
		/// <param name="rm"></param>
		/// <param name="teximg"></param>
		/// <param name="gamma"></param>
		public void EnvironmentBitmapFromEvaluator(RenderEnvironment rm, CyclesTextureImage teximg, float gamma, uint docsrn = 0, string simfilename = "")
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

			if(!(_specialIds.Contains(renderTexture.TypeId) || _specialIds.Contains(rm.TypeId)))
			{
				simfilename = "";
			}
			else {
				teximg.IsLinear = true;
			}


			if (/*renderTexture.IsImageBased() ||*/ simfilename.Length > 0)
			{
				/*Field tf = renderTexture.Fields.GetField("filename");
				var fs = "";
				if(tf != null) {
					var ofs = tf.GetValue<string>();
					RhinoDoc doc = rm.DocumentAssoc;
					fs = Rhino.Render.Utilities.FindFile(doc, ofs, true);
				}
				*/
				//fs = string.IsNullOrEmpty(fs) ? simfilename : fs;

				teximg.Filename = string.IsNullOrEmpty(simfilename) ? null : simfilename;
			} else {
				Utilities.HandleRenderTexture(renderTexture, teximg, false, false, this, docsrn, gamma, false, true);
			}


			var projection = get_environment_mapping(rm, renderTexture);
			var rhinotfm = renderTexture.LocalMappingTransform;
			var guid = renderTexture.TypeId;
			var nm = renderTexture.TypeName;
			var rot = renderTexture.GetRotation();
			var rep = renderTexture.GetRepeat();
			var tra = renderTexture.GetOffset();

			teximg.ForImageTextureNode = false;
			if(teximg.HasProcedural && teximg.Procedural is BitmapTextureProcedural bmp)
			{
				// if wallpaper then don't use as environment texture node texture, but
				// regular image texture node with texco.window as mapping
				bmp.IsForEnvironment = (int)projection != 4;
				bmp.IsForWallpaper = (int)projection == 4;
			}

			// JohnC: I had to change this to also exclude linear workflow because when I changed from using
			// the incorrect TextureRenderHashFlags to the correct CrcRenderHashFlags, an assert started firing
			// because we are not on the main thread.
			var flags = CrcRenderHashFlags.ExcludeLocalMapping | CrcRenderHashFlags.ExcludeDocumentEffects;
			var rId = renderTexture.RenderHashExclude(flags, "azimuth;altitude;multiplier;rdk-texture-repeat;rdk-texture-offset;rdk-texture-rotation;rdk-texture-adjust-multiplier;intensity");

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

				rhinotfm.M20 = -alti;
				rhinotfm.M21 = -Rhino.RhinoMath.ToRadians(rot.Z);
				rhinotfm.M22 = -(azi + RhinoMath.ToRadians(180));
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
			var img = read ? ByteImagesNew[id] : new ByteBitmap(id, new StdVectorByte(new MyDumbBitmapByteList(bm, flip)), pwidth, pheight, false);
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


		private StdVectorByte ReadByteBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			if (!hasTransparentColor && !flip)
			{
				if (!canUse)
				{
					Rhino.Display.Color4f c4f = new Rhino.Display.Color4f(1.0f, 1.0f, 1.0f, 1.0f);

					byte[] conv = new byte[4];
					c4f.ToArray(ref conv);

					return new StdVectorByte(conv);
				}

				var bytes = textureEvaluator.WriteToByteArray2(pwidth, pheight);
				if (null != bytes)
				{
					return bytes;
				}
			}

			//Otherwise, we do this the slow way.
			return new StdVectorByte(new EvaluatorToByteList(textureEvaluator, pwidth, pheight, isImageBased, flip));
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

		private StdVectorFloat ReadFloatBitmapFromEvaluator(int pwidth, int pheight, TextureEvaluator textureEvaluator, bool isImageBased, bool canUse, bool hasTransparentColor, bool flip)
		{
			if (!hasTransparentColor && !flip)
			{
				if (!canUse)
				{
					Rhino.Display.Color4f c4f = new Rhino.Display.Color4f(1.0f, 1.0f, 1.0f, 1.0f);

					float[] conv = new float[4];
					c4f.ToArray(ref conv);

					return new StdVectorFloat(conv);
				}

				var floats = textureEvaluator.WriteToFloatArray2(pwidth, pheight);
				if (null != floats)
				{
					return floats;
				}
			}

			//Otherwise, we do this the slow way.
			return new StdVectorFloat(new EvaluatorToFloatList(textureEvaluator, pwidth, pheight, isImageBased, flip));
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
