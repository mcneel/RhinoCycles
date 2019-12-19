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
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using Rhino.Display;
using Rhino.Render;

namespace RhinoCyclesCore
{
	public class BitmapImage<T> : IDisposable
	{
		internal T Original;
		internal T Corrected;

		protected int W;
		protected int H;

		public uint Id { get; }

		public bool IsLinear { get; }

		internal BitmapImage() { }

		protected static int ColorClamp(int ch)
		{
			if (ch < 0) return 0;
			return ch > 255 ? 255 : ch;
		}

		internal bool GammaApplied { get; set; }

		public BitmapImage(uint id, T data, int w, int h, bool linear)
		{
			Id = id;

			W = w;
			H = h;

			IsLinear = linear;

			Original = data;
		}

		virtual public void ApplyGamma(float gamma)
		{
		}

		public T Data => IsLinear || !GammaApplied ? Original : Corrected;

		public void SaveBitmaps()
		{
			SavePixels(Original, $"{Id}_original");
			if (!IsLinear && Corrected != null)
			{
				SavePixels(Corrected, $"{Id}_corrected");
			}
		}

		protected virtual void SavePixels(T pixels, string name) {}

		public void Dispose()
		{
		}
	}

	public class ByteArray : IDisposable
	{
		public ByteArray(IntPtr p, int size)
		{
			_p = p;
			_size = size;
		}

		public void Dispose()
		{
			TextureEvaluator.FreeByteArray(_p);
		}

		public IntPtr Data
		{
			get { return _p; }
		}

		public byte[] Convert()
		{
			var bytes = new byte[_size];
			for (int i=0;i<_size;i++)
			{
				bytes[i] = TextureEvaluator.GetByteArrayValue(_p, i);
			}
			return bytes;
		}

		IntPtr _p;
		int _size;
	}


	public class ByteBitmap : BitmapImage<ByteArray>
	{

		public ByteBitmap(uint id, ByteArray data, int w, int h, bool linear) : base(id, data, w, h, linear)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (!IsLinear && Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				/*var conv = Original.AsParallel().Select((b, i) => (i+1)%4 == 0 ? b : (byte) (Math.Pow(b/255.0f, gamma)*255.0f)).ToArray();
				if(Corrected == null)
					Corrected = new byte[Original.Length];
				conv.CopyTo(Corrected, 0);*/

				Corrected = new ByteArray(TextureEvaluator.Rdk_TextureEvaluator_NewByteArray_ApplyGamma(Original.Data, W, H, gamma), W*H*4);
				GammaApplied = true;
			}
			else
			{
				GammaApplied = false;
				//Corrected.Dispose();
				Corrected = null;
			}
		}

		protected override void SavePixels(ByteArray pixels, string name)
		{
			/*using (var rw = RenderWindow.Create(new Size(W, H)))
			{
				using (var ch = rw.OpenChannel(RenderWindow.StandardChannels.RGBA))
				{
					for (var x = 0; x < W; x++)
					{
						for (var y = 0; y < H; y++)
						{
							var i = y*W*4 + x*4;
							ch.SetValue(x, y,
								Color4f.FromArgb(pixels[i + 3]/255.0f, pixels[i]/255.0f, pixels[i + 1]/255.0f, pixels[i + 2]/255.0f));
						}
					}
				}
				var tmpfhdr = RenderEngine.TempPathForFile($"byte_{name}.exr");
				rw.SaveRenderImageAs(tmpfhdr, true);
			}*/
		}
	}

	public class FloatBitmap : BitmapImage<float[]>
	{
		public FloatBitmap(uint id, float[] data, int w, int h, bool linear) : base(id, data, w, h, linear)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (!IsLinear && Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = Original.AsParallel().Select((f, i) => (i+1)%4==0 ? f : (float)Math.Pow(f, gamma)).ToArray();
				if(Corrected == null)
					Corrected = new float[Original.Length];
				conv.CopyTo(Corrected, 0);
				GammaApplied = true;
			}
			else
			{
				Corrected = null;
				GammaApplied = false;
			}
		}

		protected override void SavePixels(float[] pixels, string name)
		{
			using (var rw = RenderWindow.Create(new Size(W, H)))
			{
				using (var ch = rw.OpenChannel(RenderWindow.StandardChannels.RGBA))
				{
					for (var x = 0; x < W; x++)
					{
						for (var y = 0; y < H; y++)
						{
							var i = y*W*4 + x*4;
							ch.SetValue(x, y, Color4f.FromArgb(pixels[i+3], pixels[i], pixels[i+1], pixels[i+2]));
						}
					}
				}
				var tmpfhdr = RenderEngine.TempPathForFile($"float_{ name}.exr");
				rw.SaveRenderImageAs(tmpfhdr, true);
			}
		}

	}
}
