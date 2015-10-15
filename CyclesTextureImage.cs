using Rhino.Render;

namespace RhinoCycles
{
	public class CyclesTextureImage
	{
		public bool HasTextureImage { get { return TexByte != null || TexFloat != null; } }
		public bool HasFloatImage { get { return TexFloat != null; } }
		public bool HasByteImage { get { return TexByte != null; } }
		public byte[] TexByte { get; set; }
		public float[] TexFloat { get; set; }
		public int TexWidth;
		public int TexHeight;
		public string Name;
		public bool UseAlpha;
		public float UseAlphaAsFloat { get { return UseAlpha ? 1.0f : 0.0f; } }
		public float Amount { get; set; }

		public bool IsLinear { get; set; }

		/// <summary>
		/// transformations on texture space. Vectors used are:
		/// Transform.x = translate
		/// Transform.y = scale
		/// Transform.z = rotate
		/// </summary>
		public ccl.Transform Transform { get; set; }

		public TextureProjectionMode ProjectionMode { get; set; }
		public TextureEnvironmentMappingMode EnvProjectionMode { get; set; }

		public CyclesTextureImage()
		{
			Clear();
		}

		public void Clear()
		{
			Transform = ccl.Transform.Identity();
			TexByte = null;
			TexFloat = null;
			TexWidth = 0;
			TexHeight = 0;
			Name = "";
			UseAlpha = false;
			Amount = 0.0f;
		}
	}
}
