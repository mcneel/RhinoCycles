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
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace RhinoCycles
{
	public class BitmapImage<T>
	{
		internal T[] original;
		internal T[] corrected;

		protected int w;
		protected int h;

		public uint Id { get; private set; }

		internal BitmapImage() { }

		protected static int ColorClamp(int ch)
		{
			if (ch < 0) return 0;
			return ch > 255 ? 255 : ch;
		}

		public BitmapImage(uint id, T[] data, int _w, int _h)
		{
			Id = id;

			w = _w;
			h = _h;

			var l = data.Length;
			original = new T[l];
			corrected = new T[l];
			data.CopyTo(original, 0);
			data.CopyTo(corrected, 0);
		}

		virtual public void ApplyGamma(float gamma)
		{
		}

		public T[] Data { get { return corrected; } }
	}

	public class ByteBitmap : BitmapImage<byte>
	{

		public ByteBitmap(uint id, byte[] data, int w, int h) : base(id, data, w, h)
		{ }

		override public void ApplyGamma(float gamma)
		{
			if (Math.Abs(gamma - 1.0f) > float.Epsilon)
			{
				var conv = original.AsParallel().Select((b, i) => (i+1)%4 == 0 ? b : (byte) (Math.Pow(b/255.0f, gamma)*255.0f)).ToArray();
				conv.CopyTo(corrected, 0);
			}
			else
			{
				original.CopyTo(corrected, 0);
			}
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
				var conv = original.AsParallel().Select((f, i) => (i+1)%4==0 ? f : (float)Math.Pow(f, gamma)).ToArray();
				conv.CopyTo(corrected, 0);
			}
			else
			{
				original.CopyTo(corrected, 0);
			}
		}
		
	}
}
