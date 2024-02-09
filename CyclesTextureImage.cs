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
using Rhino.Display;
using Rhino.Render;
using Rhino.Runtime.InteropWrappers;
using RhinoCyclesCore.Converters;
using System;
using System.Collections.Generic;

namespace RhinoCyclesCore
{
	public class CyclesTextureImage : IDisposable
	{
		public Procedural Procedural { get; set; } = null;
		public bool HasProcedural => Procedural != null;
		public bool HasTextureImage => !string.IsNullOrEmpty(Filename);
		public List<CyclesTextureImage> TextureList = new List<CyclesTextureImage>();
		public bool HasFloatImage => TexFloat != null;
		public bool HasByteImage => TexByte != null;
		public StdVectorByte TexByte { get; set; }
		public StdVectorFloat TexFloat { get; set; }
		public int TexWidth;
		public int TexHeight;
		public string Name;
		public bool UseAlpha;
		public bool AlternateTiles;
		public bool UseColorMask;
		public Color4f ColorMask;
		public float ColorMaskSensitivity;
		private bool disposedValue;

		public string Filename { get; set; }

		public float UseAlphaAsFloat => UseAlpha ? 1.0f : 0.0f;
		public float Amount { get; set; }

		public bool IsLinear { get; set; }

		public bool IsNormalMap { get; set; } = false;

		/// <summary>
		/// transformations on texture space. Vectors used are:
		/// Transform.x = translate
		/// Transform.y = scale
		/// Transform.z = rotate
		/// </summary>
		public ccl.Transform Transform { get; set; }

		/// <summary>
		/// Strength of texture. Used in environment textures.
		/// </summary>
		public float Strength { get; set; }

		public float BumpDistance
		{
			get
			{
				if (Transform == null) return 0.0f;
				var td = Transform.x.x;
				if (td > 0.2f) return 0.05f;
				if (td < 0.06) return 2.0f;
				return td > 0.14 ? 0.1f : 0.75f;
			}
		}

		/* ADJUSTMENT SETTINGS */
		public bool AdjustNeeded {get; set; } = false;
		public bool AdjustGrayscale { get; set; } = false;
		public bool AdjustInvert {get; set; } = false;
		public bool AdjustClamp { get; set; } = false;
		public bool AdjustScaleToClamp { get; set; } = false;
		public float AdjustMultiplier { get; set; } = 1.0f;
		public float AdjustClampMin { get; set; } = 0.0f;
		public float AdjustClampMax { get; set; } = 1.0f;
		public float AdjustGain { get; set; } = 0.5f;
		public float AdjustGamma { get; set; } = 1.0f;
		public float AdjustSaturation { get; set; } = 1.0f;
		public float AdjustHueShift { get; set; } = 0.0f;
		public bool AdjustIsHdr { get; set; } = false;

		/* END ADJUSTMENT SETTINGS */

		public TextureProjectionMode ProjectionMode { get; set; }
		public TextureEnvironmentMappingMode EnvProjectionMode { get; set; }

		public int MappingChannel { get; set; } = 0;
		public string GetUvMapForChannel()
		{
			var chan = MappingChannel > 0 ? MappingChannel : 1;
			return $"uvmap{chan}";
		}

		public bool Repeat { get; set; }
		public float InvertRepeatAsFloat => Repeat ? 0.0f : 1.0f;
		public float RepeatAsFloat => Repeat ? 1.0f : 0.0f;

		public CyclesTextureImage()
		{
			Clear();
		}

		public void Clear()
		{
			Transform = ccl.Transform.Identity();
			TexByte = null;
			TexFloat = null;
			Procedural = null;
			TexWidth = 0;
			TexHeight = 0;
			Name = "";
			Filename = "";
			UseAlpha = false;
			Amount = 0.0f;
		}

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			System.Text.StringBuilder sb = new System.Text.StringBuilder($"{typeof(CyclesTextureImage).Name}:");
			var props = typeof(CyclesTextureImage).GetProperties();
			foreach (var prop in props)
			{
				sb.Append($"{prop.Name} := {prop.GetValue(this)}, ");
			}
			if (HasFloatImage)
			{
				var tf = TexFloat.ToArray();

				sb.Append($"{tf[0]}|{tf[1]}|{tf[2]}|{tf[3]}");
				var mid = tf.Length / 2;
				sb.Append($"{tf[mid]}|{tf[mid + 1]}|{tf[mid + 2]}|{tf[mid + 3]}");
				var end = tf.Length - 4;
				sb.Append($"{tf[end]}|{tf[end + 1]}|{tf[end + 2]}|{tf[end + 3]}");
			}
			return sb.ToString();
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					TexByte?.Dispose();
					TexFloat?.Dispose();
					Procedural?.Dispose();
				}

				Clear();

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
