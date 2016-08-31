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

namespace RhinoCyclesCore
{
	public class BitmapImage<T>
	{
		internal T[] Original;
		internal T[] Corrected;

		protected int W;
		protected int H;

		public uint Id { get; }

		internal BitmapImage() { }

		protected static int ColorClamp(int ch)
		{
			if (ch < 0) return 0;
			return ch > 255 ? 255 : ch;
		}

		public BitmapImage(uint id, T[] data, int w, int h)
		{
			Id = id;

			W = w;
			H = h;

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

		public ByteBitmap(uint id, byte[] data, int w, int h) : base(id, data, w, h)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = Original.AsParallel().Select((b, i) => (i+1)%4 == 0 ? b : (byte) (Math.Pow(b/255.0f, gamma)*255.0f)).ToArray();
				conv.CopyTo(Corrected, 0);
			}
			else
			{
				Original.CopyTo(Corrected, 0);
			}
		}

		override protected void SavePixels(byte[] pixels, string name)
		{
			var bm = new Bitmap(W, H);
			for (var x = 0; x < W; x++)
			{
				for (var y = 0; y < H; y++)
				{
					var i = y * W * 4 + x * 4;
					var r = ColorClamp(pixels[i]);
					var g = ColorClamp(pixels[i + 1]);
					var b = ColorClamp(pixels[i + 2]);
					var a = ColorClamp(pixels[i + 3]);
					bm.SetPixel(x, y, Color.FromArgb(a, r, g, b));
				}
			}
			var tmpf = $"{Environment.GetEnvironmentVariable("TEMP")}\\byte_{name}.png";
			bm.Save(tmpf,  ImageFormat.Png);
		}
	}

	public class FloatBitmap : BitmapImage<float>
	{
		public FloatBitmap(uint id, float[] data, int w, int h) : base(id, data, w, h)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = Original.AsParallel().Select((f, i) => (i+1)%4==0 ? f : (float)Math.Pow(f, gamma)).ToArray();
				conv.CopyTo(Corrected, 0);
			}
			else
			{
				Original.CopyTo(Corrected, 0);
			}
		}

		override protected void SavePixels(float[] pixels, string name)
		{
			var bm = new Bitmap(W, H);
			for (var x = 0; x < W; x++)
			{
				for (var y = 0; y < H; y++)
				{
					var i = y * W * 4 + x * 4;
					var r = ColorClamp((int)(pixels[i] * 255.0f));
					var g = ColorClamp((int)(pixels[i + 1] * 255.0f));
					var b = ColorClamp((int)(pixels[i + 2] * 255.0f));
					var a = ColorClamp((int)(pixels[i + 3] * 255.0f));
					bm.SetPixel(x, y, Color.FromArgb(a, r, g, b));
				}
			}
			bm.Save($"{Environment.GetEnvironmentVariable("TEMP")}\\float_{name}.png",  ImageFormat.Png);
		}
		
	}
}
