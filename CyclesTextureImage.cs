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
