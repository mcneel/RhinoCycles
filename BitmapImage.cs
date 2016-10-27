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
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using Rhino.Display;
using Rhino.Render;

namespace RhinoCyclesCore
{
	public class BitmapImage<T>
	{
		internal T[] Original;
		internal T[] Corrected;

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

		public BitmapImage(uint id, T[] data, int w, int h, bool linear)
		{
			Id = id;

			W = w;
			H = h;

			IsLinear = linear;

			var l = data.Length;
			Original = new T[l];
			Corrected = new T[l];
			data.CopyTo(Original, 0);
			data.CopyTo(Corrected, 0);
		}

		virtual public void ApplyGamma(float gamma)
		{
		}

		public T[] Data => Corrected;

		public void SaveBitmaps()
		{
			SavePixels(Original, $"{Id}_original");
			SavePixels(Corrected, $"{Id}_corrected");
		}

		protected virtual void SavePixels(T[] pixels, string name) {}
	}

	public class ByteBitmap : BitmapImage<byte>
	{

		public ByteBitmap(uint id, byte[] data, int w, int h, bool linear) : base(id, data, w, h, linear)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (!IsLinear && Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = Original.AsParallel().Select((b, i) => (i+1)%4 == 0 ? b : (byte) (Math.Pow(b/255.0f, gamma)*255.0f)).ToArray();
				conv.CopyTo(Corrected, 0);
			}
			else
			{
				Original.CopyTo(Corrected, 0);
			}
		}

		protected override void SavePixels(byte[] pixels, string name)
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
							ch.SetValue(x, y,
								Color4f.FromArgb(pixels[i + 3]/255.0f, pixels[i]/255.0f, pixels[i + 1]/255.0f, pixels[i + 2]/255.0f));
						}
					}
				}
				var tmpfhdr = $"{Environment.GetEnvironmentVariable("TEMP")}\\byte_{name}.exr";
				rw.SaveRenderImageAs(tmpfhdr, true);
			}
		}
	}

	public class FloatBitmap : BitmapImage<float>
	{
		public FloatBitmap(uint id, float[] data, int w, int h, bool linear) : base(id, data, w, h, linear)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (!IsLinear && Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = Original.AsParallel().Select((f, i) => (i+1)%4==0 ? f : (float)Math.Pow(f, gamma)).ToArray();
				conv.CopyTo(Corrected, 0);
			}
			else
			{
				Original.CopyTo(Corrected, 0);
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
				var tmpfhdr = $"{Environment.GetEnvironmentVariable("TEMP")}\\float_{name}.exr";
				rw.SaveRenderImageAs(tmpfhdr, true);
			}
		}

	}
}
