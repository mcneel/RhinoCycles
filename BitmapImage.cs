using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RhinoCycles
{
	public class BitmapImage<T>
	{
		internal T[] original;
		internal T[] corrected;

		public uint Id { get; private set; }

		internal BitmapImage() { } 

		public BitmapImage(uint id, T[] data)
		{
			Id = id;

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

		public ByteBitmap(uint id, byte[] data) : base(id, data)
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
		public FloatBitmap(uint id, float[] data) : base(id, data)
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
