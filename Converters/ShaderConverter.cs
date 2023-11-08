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
using System.Collections.Generic;
using System.Linq;
using ccl;
using ccl.ShaderNodes;
using ccl.ShaderNodes.Sockets;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using Light = Rhino.Render.ChangeQueue.Light;

namespace RhinoCyclesCore.Converters
{
	public abstract class Procedural : IDisposable
	{
		public static Procedural CreateProceduralFromChild(RenderTexture render_texture, string child_name, List<CyclesTextureImage> texture_list, BitmapConverter _bitmapConverter, uint docsrn, float gamma, bool is_color)
		{
			Procedural procedural = null;

			if (render_texture.ChildSlotOn(child_name) && render_texture.ChildSlotAmount(child_name) > 0.01)
			{
				var render_texture_child = (RenderTexture)render_texture.FindChild(child_name);

				// Recursive call
				procedural = CreateProcedural(render_texture_child, texture_list, _bitmapConverter, docsrn, gamma, is_color);
			}

			return procedural;
		}

		private static bool ShouldSimulate(RenderTexture rt) {
			Guid type_id = rt.TypeId;
			return type_id == ContentUuids.ResampleTextureType || type_id == ContentUuids.AdvancedDotTextureType || type_id == ContentUuids.GritBumpTexture || type_id == ContentUuids.DotBumpTexture || type_id == ContentUuids.WoodBumpTexture || type_id == ContentUuids.HatchBumpTexture || type_id == ContentUuids.LeatherBumpTexture || type_id == ContentUuids.SpeckleBumpTexture || type_id == ContentUuids.CrossHatchBumpTexture;
		}

		public static Procedural CreateProcedural(RenderTexture render_texture, List<CyclesTextureImage> texture_list, BitmapConverter bitmap_converter, uint docsrn, float gamma, bool is_color)
		{
			if (render_texture == null)
				return null;

			Procedural procedural = null;

			Guid type_id = render_texture.TypeId;

			if (type_id == ContentUuids.Texture2DCheckerTextureType)
			{
				procedural = new CheckerTextureProcedural(render_texture, true, is_color);
			}
			else if (type_id == ContentUuids.Texture3DCheckerTextureType)
			{
				procedural = new CheckerTextureProcedural(render_texture, false, is_color);
			}
			else if (type_id == ContentUuids.NoiseTextureType)
			{
				procedural = new NoiseTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.WavesTextureType)
			{
				procedural = new WavesTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.PerturbingTextureType)
			{
				procedural = new PerturbingTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.WoodTextureType)
			{
				procedural = new WoodTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.AddTextureType)
			{
				procedural = new AddTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.MultiplyTextureType)
			{
				procedural = new MultiplicationTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.BlendTextureType)
			{
				procedural = new BlendTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.GradientTextureType)
			{
				procedural = new GradientTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.ExposureTextureType)
			{
				procedural = new ExposureTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.FBmTextureType)
			{
				procedural = new FbmTextureProcedural(render_texture, false, is_color);
			}
			else if (type_id == ContentUuids.TurbulenceTextureType)
			{
				procedural = new FbmTextureProcedural(render_texture, true, is_color);
			}
			else if (type_id == ContentUuids.GraniteTextureType)
			{
				procedural = new GraniteTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.GridTextureType)
			{
				procedural = new GridTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.ProjectionChangerTextureType)
			{
				procedural = new ProjectionChangerTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.MarbleTextureType)
			{
				procedural = new MarbleTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.MaskTextureType)
			{
				procedural = new MaskTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.PerlinMarbleTextureType)
			{
				procedural = new PerlinMarbleTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.PhysicalSkyTextureType)
			{
				procedural = new PhysicalSkyTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.SingleColorTextureType)
			{
				procedural = new SingleColorTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.StuccoTextureType)
			{
				procedural = new StuccoTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.TextureAdjustmentTextureType)
			{
				procedural = new TextureAdjustmentTextureProcedural(render_texture, is_color);
			}
			else if (type_id == ContentUuids.TileTextureType)
			{
				procedural = new TileTextureProcedural(render_texture, is_color);
			}
			/* TODO: re-enable this once dot texture is natively supported. Until then handle as bitmap texture
			 else if (type_id == ContentUuids.AdvancedDotTextureType)
			{
				procedural = new DotsTextureProcedural(render_texture);
			}*/
			else if (render_texture.IsBitmapTexture() || ShouldSimulate(render_texture))
			{
				CyclesTextureImage cycles_texture = new CyclesTextureImage();
				texture_list.Add(cycles_texture);
				procedural = new BitmapTextureProcedural(render_texture, cycles_texture, bitmap_converter, docsrn, gamma, ShouldSimulate(render_texture), is_color);
			}
			else if (type_id == ContentUuids.HDRTextureType)
			{
				CyclesTextureImage cycles_texture = new CyclesTextureImage();
				texture_list.Add(cycles_texture);
				procedural = new HighDynamicRangeTextureProcedural(render_texture, cycles_texture, bitmap_converter, docsrn, is_color);
			}
			/* TODO: re-enable this once resample texture is natively supported. Until then handle as bitmap texture
			else if (type_id == ContentUuids.ResampleTextureType)
			{
				CyclesTextureImage cycles_texture = new CyclesTextureImage();
				texture_list.Add(cycles_texture);
				procedural = new ResampleTextureProcedural(render_texture, cycles_texture, bitmap_converter);
			}*/

			if (procedural is OneColorProcedural one_color)
			{
				one_color.Child = CreateProceduralFromChild(render_texture, "color-one", texture_list, bitmap_converter, docsrn, gamma, is_color);
			}

			if (procedural is TwoColorProcedural two_color)
			{
				two_color.Child1 = CreateProceduralFromChild(render_texture, "color-one", texture_list, bitmap_converter, docsrn, gamma, is_color);
				two_color.Child2 = CreateProceduralFromChild(render_texture, "color-two", texture_list, bitmap_converter, docsrn, gamma, is_color);

				if(two_color.SwapColors)
				{
					(two_color.Color1, two_color.Color2) = (two_color.Color2, two_color.Color1);
					(two_color.Amount1, two_color.Amount2) = (two_color.Amount2, two_color.Amount1);
					(two_color.Child1, two_color.Child2) = (two_color.Child2, two_color.Child1);
				}
			}

			if(procedural is WavesTextureProcedural waves_texture)
			{
				RenderTexture wave_width_child = (RenderTexture)render_texture.FindChild("wave-width-tex");
				if(wave_width_child != null)
				{
					waves_texture.WaveWidthChild = CreateProcedural(wave_width_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if(procedural is PerturbingTextureProcedural perturbing_texture)
			{
				RenderTexture perturbing_source_child = (RenderTexture)render_texture.FindChild("source");
				if (perturbing_source_child != null)
				{
					perturbing_texture.SourceChild = CreateProcedural(perturbing_source_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}

				RenderTexture perturbing_perturb_child = (RenderTexture)render_texture.FindChild("perturb");
				if (perturbing_perturb_child != null)
				{
					perturbing_texture.PerturbChild = CreateProcedural(perturbing_perturb_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if(procedural is BlendTextureProcedural blend_texture)
			{
				RenderTexture blend_child = (RenderTexture)render_texture.FindChild("blend-texture");
				if (blend_child != null)
				{
					blend_texture.BlendChild = CreateProcedural(blend_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if(procedural is ExposureTextureProcedural exposure_texture)
			{
				RenderTexture exposure_child = (RenderTexture)render_texture.FindChild("input-texture");
				if (exposure_child != null)
				{
					exposure_texture.ExposureChild = CreateProcedural(exposure_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if (procedural is ProjectionChangerTextureProcedural projection_changer_texture)
			{
				RenderTexture projection_changer_child = (RenderTexture)render_texture.FindChild("input-texture");
				if (projection_changer_child != null)
				{
					projection_changer_texture.ProjectionChangerChild = CreateProcedural(projection_changer_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if (procedural is MaskTextureProcedural mask_texture)
			{
				RenderTexture mask_child = (RenderTexture)render_texture.FindChild("source-texture");
				if (mask_texture != null)
				{
					mask_texture.MaskChild = CreateProcedural(mask_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			if(procedural is TextureAdjustmentTextureProcedural texture_adjustment_texture)
			{
				RenderTexture texture_adjustment_child = (RenderTexture)render_texture.FindChild("input-texture");
				if (texture_adjustment_child != null)
				{
					texture_adjustment_texture.TextureAdjustmentChild = CreateProcedural(texture_adjustment_child, texture_list, bitmap_converter, docsrn, gamma, is_color); // Recursive call
				}
			}

			return procedural;
		}

		protected static ccl.Transform ToCyclesTransform(Rhino.Geometry.Transform transform)
		{
			return new ccl.Transform(
				(float)transform[0, 0], (float)transform[0, 1], (float)transform[0, 2], (float)transform[0, 3],
				(float)transform[1, 0], (float)transform[1, 1], (float)transform[1, 2], (float)transform[1, 3],
				(float)transform[2, 0], (float)transform[2, 1], (float)transform[2, 2], (float)transform[2, 3]);
		}

		public bool IsColor
		{
			get;
			private set;
		} = false;

		public bool IsBitmapTexture
		{
			get;
			private set;
		} = false;

		public TextureProjectionMode ProjectionMode
		{
			get;
			private set;
		}

		public TextureEnvironmentMappingMode EnvironmentMappingMode
		{
			get;
			private set;
		}

		public Procedural(RenderTexture render_texture, bool is_color)
		{
			if (render_texture != null)
			{
				IsColor = is_color;
				IsBitmapTexture = render_texture.IsBitmapTexture();
				MappingTransform = ToCyclesTransform(render_texture.LocalMappingTransform);
				ProjectionMode = render_texture.GetProjectionMode();
				EnvironmentMappingMode = render_texture.GetInternalEnvironmentMappingMode();

				var rtf = render_texture.Fields;

				if (rtf.TryGetValue("rdk-texture-adjust-grayscale", out bool grayscale))
					AdjustGrayscale = grayscale;

				if (rtf.TryGetValue("rdk-texture-adjust-invert", out bool invert))
					AdjustInvert = invert;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp", out bool clamp))
					AdjustClamp = clamp;

				if (rtf.TryGetValue("rdk-texture-adjust-scale-to-clamp", out bool scale_to_clamp))
					AdjustScaleToClamp = scale_to_clamp;

				if (rtf.TryGetValue("rdk-texture-adjust-multiplier", out double multiplier))
					AdjustMultiplier = (float)multiplier;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp-min", out double clamp_min))
					AdjustClampMin = (float)clamp_min;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp-max", out double clamp_max))
					AdjustClampMax = (float)clamp_max;

				if (rtf.TryGetValue("rdk-texture-adjust-gain", out double gain))
					AdjustGain = (float)gain;

				if (rtf.TryGetValue("rdk-texture-adjust-gamma", out double gamma))
					AdjustGamma = (float)gamma;

				if (rtf.TryGetValue("rdk-texture-adjust-saturation", out double saturation))
					AdjustSaturation = (float)saturation;

				if (rtf.TryGetValue("rdk-texture-adjust-hue-shift", out double hue_shift))
					AdjustHueShift = (float)hue_shift;

				AdjustIsHdr = render_texture.IsHdrCapable();

				if (AdjustClamp || AdjustScaleToClamp || AdjustInvert || AdjustGrayscale)
				{
					AdjustNeeded = true;
				}
				else if (AdjustGain != 0.5f ||
					AdjustGamma != 1.0f ||
					AdjustMultiplier != 1.0f ||
					AdjustClampMin != 0.0f ||
					AdjustClampMax != 1.0f ||
					AdjustHueShift != 0.0f ||
					AdjustSaturation != 1.0f)
				{
					AdjustNeeded = true;
				}
			}
		}

		protected virtual void Dispose(bool disposing)
		{

		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		public void CreateAndConnectAdjustmentNode(Shader shader, ISocket color_output, ColorSocket parent_color_input)
		{
			if (!AdjustNeeded)
			{
				color_output.Connect(parent_color_input);
			} else
			{
				var texture_adjustment_node = new TextureAdjustmentTextureProceduralNode(shader);

				texture_adjustment_node.Grayscale = AdjustGrayscale;
				texture_adjustment_node.Invert = AdjustInvert;
				texture_adjustment_node.Clamp = AdjustClamp;
				texture_adjustment_node.ScaleToClamp = AdjustScaleToClamp;
				texture_adjustment_node.Multiplier = AdjustMultiplier;
				texture_adjustment_node.ClampMin = AdjustClampMin;
				texture_adjustment_node.ClampMax = AdjustClampMax;
				texture_adjustment_node.Gain = AdjustGain;
				texture_adjustment_node.Gamma = AdjustGamma;
				texture_adjustment_node.Saturation = AdjustSaturation;
				texture_adjustment_node.HueShift = AdjustHueShift;
				texture_adjustment_node.IsHdr = AdjustIsHdr;

				color_output.Connect(texture_adjustment_node.ins.Color);
				texture_adjustment_node.outs.Color.Connect(parent_color_input);
			}
		}

		public void ConnectAlphaNode(FloatSocket alpha_output, List<ISocket> parent_alpha_input)
		{
			if (parent_alpha_input == null) return;
			foreach (var _parent_alpha_input in parent_alpha_input)
			{
				if (_parent_alpha_input != null)
				{
					alpha_output.Connect(_parent_alpha_input);
				}
			}
		}

		public abstract void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData);
		protected ccl.Transform MappingTransform { get; set; } = ccl.Transform.Identity();

		public bool AdjustGrayscale { get; set; }
		public bool AdjustInvert { get; set; }
		public bool AdjustClamp { get; set; }
		public bool AdjustScaleToClamp { get; set; }
		public float AdjustMultiplier { get; set; }
		public float AdjustClampMin { get; set; }
		public float AdjustClampMax { get; set; }
		public float AdjustGain { get; set; }
		public float AdjustGamma { get; set; }
		public float AdjustSaturation { get; set; }
		public float AdjustHueShift { get; set; }
		public bool AdjustIsHdr { get; set; }
		public bool AdjustNeeded { get; set; }

		public uint Id { get; set; } = 0;
	}

	public abstract class OneColorProcedural : Procedural
	{
		public OneColorProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			Color = render_texture.Fields.TryGetValue("color-one", out Color4f color1) ? color1 : Color4f.Black;
			Amount = render_texture.Fields.TryGetValue("texture-amount-one", out double texture_amount1) ? (float)texture_amount1 : 1.0f;
		}

		protected void ConnectChildNode(Shader shader, VectorSocket uvw_output, ColorSocket color_input, List<ISocket> alpha_input, bool IsData)
		{
			if (Child != null)
			{
				if (Amount < 1.0f)
				{
					var mixer = new MixNode(shader);

					mixer.ins.Fac.Value = Amount;
					mixer.ins.Color1.Value = Color.ToFloat4();

					// Recursive call
					Child.CreateAndConnectProceduralNode(shader, uvw_output, mixer.ins.Color2, alpha_input, IsData);

					mixer.outs.Color.Connect(color_input);
				}
				else
				{
					// Recursive call
					Child.CreateAndConnectProceduralNode(shader, uvw_output, color_input, alpha_input, IsData);
				}
			}
			else
			{
				color_input.Value = Color.ToFloat4();
			}
		}

		public Color4f Color { get; set; }
		public float Amount { get; set; }

		public Procedural Child { get; set; } = null;
	}

	public abstract class TwoColorProcedural : Procedural
	{
		public TwoColorProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			Color1 = render_texture.Fields.TryGetValue("color-one", out Color4f color1) ? color1 : Color4f.Black;
			Color2 = render_texture.Fields.TryGetValue("color-two", out Color4f color2) ? color2 : Color4f.White;
			Amount1 = render_texture.Fields.TryGetValue("texture-amount-one", out double texture_amount1) ? (float)texture_amount1 : 1.0f;
			Amount2 = render_texture.Fields.TryGetValue("texture-amount-two", out double texture_amount2) ? (float)texture_amount2 : 1.0f;
			SwapColors = render_texture.Fields.TryGetValue("swap-colors", out bool swap_colors) ? swap_colors : false;
		}

		protected void ConnectChildNodes(Shader shader, VectorSocket uvw_output, ColorSocket color1_input, ColorSocket color2_input, bool IsData)
		{
			ConnectChildNodes(shader, uvw_output, color1_input, null, color2_input, null, IsData);
		}

			protected void ConnectChildNodes(Shader shader, VectorSocket uvw_output, ColorSocket color1_input, FloatSocket alpha1_input, ColorSocket color2_input, FloatSocket alpha2_input, bool IsData)
		{
			if (Child1 != null)
			{
				if(Amount1 < 1.0f)
				{
					//var mult = new MathNode();
					//mult.Operation = MathNode.Operations.Multiply;
					//mult.ins.Value1.Value = Amount1;
					//mult.ins.Value2 =

					var mixer = new MixNode(shader);

					mixer.ins.Fac.Value = Amount1;
					mixer.ins.Color1.Value = Color1.ToFloat4();

					// Recursive call
					Child1.CreateAndConnectProceduralNode(shader, uvw_output, mixer.ins.Color2, alpha1_input?.ToList() ?? null, IsData);

					mixer.outs.Color.Connect(color1_input);
				}
				else
				{
					// Recursive call
					Child1.CreateAndConnectProceduralNode(shader, uvw_output, color1_input, alpha1_input?.ToList() ?? null, IsData);
				}
			}
			else
			{
				float4 f4 = Color1.ToFloat4();
				color1_input.Value = f4;
				if(alpha1_input != null)
					alpha1_input.Value = f4.w;
			}

			if (Child2 != null)
			{
				if (Amount2 < 1.0f)
				{
					var mixer = new MixNode(shader);

					mixer.ins.Fac.Value = Amount2;
					mixer.ins.Color1.Value = Color2.ToFloat4();

					// Recursive call
					Child2.CreateAndConnectProceduralNode(shader, uvw_output, mixer.ins.Color2, alpha2_input?.ToList() ?? null, IsData);

					mixer.outs.Color.Connect(color2_input);
				}
				else
				{
					// Recursive call
					Child2.CreateAndConnectProceduralNode(shader, uvw_output, color2_input, alpha2_input?.ToList() ?? null, IsData);
				}
			}
			else
			{
				float4 f4 = Color2.ToFloat4();
				color2_input.Value = f4;
				if (alpha2_input != null)
					alpha2_input.Value = f4.w;
			}
		}

		public Color4f Color1 { get; set; }
		public Color4f Color2 { get; set; }
		public float Amount1 { get; set; }
		public float Amount2 { get; set; }
		public bool SwapColors { get; set; }

		public Procedural Child1 { get; set; } = null;
		public Procedural Child2 { get; set; } = null;
	}

	public class CheckerTextureProcedural : TwoColorProcedural
	{
		public CheckerTextureProcedural(RenderTexture render_texture, bool is_2d, bool is_color)
			: base(render_texture, is_color)
		{
			MappingTransform *= ccl.Transform.Scale(2.0f, 2.0f, 2.0f);
			MappingTransform.x.w *= 2.0f;
			MappingTransform.y.w *= 2.0f;
			MappingTransform.z.w *= 2.0f;

			if(is_2d)
			{
				MappingTransform.z.z = 0.0f;
			}

			if (render_texture.Fields.TryGetValue("remap-textures", out bool remap_textures))
				RemapTextures = remap_textures;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var node = new CheckerTextureProceduralNode(shader);

			var transform_node = new MatrixMathNode(shader)
			{
				Transform = new ccl.Transform(MappingTransform)
			};

			uvw_output.Connect(transform_node.ins.Vector);

			// Recursive call
			ConnectChildNodes(shader, RemapTextures ? transform_node.outs.Vector : uvw_output, node.ins.Color1, node.ins.Alpha1, node.ins.Color2, node.ins.Alpha2, IsData);

			transform_node.outs.Vector.Connect(node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, node.outs.Color, parent_color_input);
			ConnectAlphaNode(node.outs.Alpha, parent_alpha_input);
		}

		public bool RemapTextures { get; set; } = true;
	}

	public class NoiseTextureProcedural : TwoColorProcedural
	{
		public NoiseTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("noise-type", out string noise_type))
				NoiseType = StringToNoiseType(noise_type);

			if (rtf.TryGetValue("spectral-synthesis-type", out string spec_synth_type))
				SpecSynthType = StringToSpecSynthType(spec_synth_type);

			if (rtf.TryGetValue("octave-count", out int octave_count))
				OctaveCount = octave_count;

			if (rtf.TryGetValue("frequency-multiplier", out double frequency_multiplier))
				FrequencyMultiplier = (float)frequency_multiplier;

			if (rtf.TryGetValue("amplitude-multiplier", out double amplitude_multiplier))
				AmplitudeMultiplier = (float)amplitude_multiplier;

			if (rtf.TryGetValue("clamp-min", out double clamp_min))
				ClampMin = (float)clamp_min;

			if (rtf.TryGetValue("clamp-max", out double clamp_max))
				ClampMax = (float)clamp_max;

			if (rtf.TryGetValue("scale-to-clamp", out bool scale_to_clamp))
				ScaleToClamp = scale_to_clamp;

			if (rtf.TryGetValue("inverse", out bool inverse))
				Inverse = inverse;

			if (rtf.TryGetValue("gain", out double gain))
				Gain = (float)gain;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader)
			{
				Transform = new ccl.Transform(MappingTransform)
			};

			var node = new NoiseTextureProceduralNode(shader);

			node.NoiseType = NoiseType;
			node.SpecSynthType = SpecSynthType;
			node.OctaveCount = OctaveCount;
			node.FrequencyMultiplier = FrequencyMultiplier;
			node.AmplitudeMultiplier = AmplitudeMultiplier;
			node.ClampMin = ClampMin;
			node.ClampMax = ClampMax;
			node.ScaleToClamp = ScaleToClamp;
			node.Inverse = Inverse;
			node.Gain = Gain;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, node.ins.Color1, node.ins.Alpha1, node.ins.Color2, node.ins.Alpha2, IsData);

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, node.outs.Color, parent_color_input);
			ConnectAlphaNode(node.outs.Alpha, parent_alpha_input);
		}

		public NoiseTextureProceduralNode.NoiseTypes NoiseType { get; set; } = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
		public NoiseTextureProceduralNode.SpecSynthTypes SpecSynthType { get; set; } = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
		public int OctaveCount { get; set; } = 3;
		public float FrequencyMultiplier { get; set; } = 2.0f;
		public float AmplitudeMultiplier { get; set; } = 0.5f;
		public float ClampMin { get; set; } = -1.0f;
		public float ClampMax { get; set; } = 1.0f;
		public bool ScaleToClamp { get; set; } = false;
		public bool Inverse { get; set; } = false;
		public float Gain { get; set; } = 0.5f;

		private static NoiseTextureProceduralNode.NoiseTypes StringToNoiseType(string enum_string)
		{
			switch (enum_string)
			{
				case "perlin": return NoiseTextureProceduralNode.NoiseTypes.PERLIN;
				case "valuenoise": return NoiseTextureProceduralNode.NoiseTypes.VALUE_NOISE;
				case "perlin_plus_value": return NoiseTextureProceduralNode.NoiseTypes.PERLIN_PLUS_VALUE;
				case "simplex": return NoiseTextureProceduralNode.NoiseTypes.SIMPLEX;
				case "sparseconvolution": return NoiseTextureProceduralNode.NoiseTypes.SPARSE_CONVOLUTION;
				case "latticeconvolution": return NoiseTextureProceduralNode.NoiseTypes.LATTICE_CONVOLUTION;
				case "wardshermite": return NoiseTextureProceduralNode.NoiseTypes.WARDS_HERMITE;
				case "aaltonen": return NoiseTextureProceduralNode.NoiseTypes.AALTONEN;
				default: return NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			}
		}

		private static NoiseTextureProceduralNode.SpecSynthTypes StringToSpecSynthType(string enum_string)
		{
			switch (enum_string)
			{
				case "fractalsum": return NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
				case "turbulence": return NoiseTextureProceduralNode.SpecSynthTypes.TURBULENCE;
				default: return NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			}
		}
	}

	public class WavesTextureProcedural : TwoColorProcedural
	{
		public WavesTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("wave-type", out int wave_type))
				WaveType = (WavesTextureProceduralNode.WaveTypes)wave_type;

			if (rtf.TryGetValue("wave-width", out double wave_width))
				WaveWidth = (float)wave_width;

			if (rtf.TryGetValue("wave-width-tex-on", out bool wave_width_texture_on))
				WaveWidthTextureOn = wave_width_texture_on;

			if (rtf.TryGetValue("contrast1", out double contrast1))
				Contrast1 = (float)contrast1;

			if (rtf.TryGetValue("contrast2", out double contrast2))
				Contrast2 = (float)contrast2;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader)
			{
				Transform = new ccl.Transform(MappingTransform)
			};

			var waves_node = new WavesTextureProceduralNode(shader);

			waves_node.WaveType = WaveType;
			waves_node.WaveWidth = WaveWidth;
			waves_node.WaveWidthTextureOn = WaveWidthTextureOn;
			waves_node.Contrast1 = Contrast1;
			waves_node.Contrast2 = Contrast2;

			var waves_width_node = new WavesWidthTextureProceduralNode(shader);

			waves_width_node.WaveType = WaveType;

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(waves_width_node.ins.UVW);

			// Recursive call
			ConnectChildNodes(shader, uvw_output, waves_node.ins.Color1, waves_node.ins.Alpha1, waves_node.ins.Color2, waves_node.ins.Alpha2, IsData);

			if (WaveWidthChild != null)
			{
				WaveWidthChild.CreateAndConnectProceduralNode(shader, waves_width_node.outs.UVW, waves_node.ins.Color3, null, IsData); // Recursive call
			}

			transform_node.outs.Vector.Connect(waves_node.ins.UVW);
			waves_node.outs.Color.Connect(parent_color_input);

			ConnectAlphaNode(waves_node.outs.Alpha, parent_alpha_input);
		}

		public WavesTextureProceduralNode.WaveTypes WaveType { get; set; } = WavesTextureProceduralNode.WaveTypes.LINEAR;
		public float WaveWidth { get; set; } = 0.5f;
		public bool WaveWidthTextureOn { get; set; } = false;
		public float Contrast1 { get; set; } = 1.0f;
		public float Contrast2 { get; set; } = 0.5f;
		public Procedural WaveWidthChild { get; set; } = null;
	}

	public class PerturbingTextureProcedural : Procedural
	{
		public PerturbingTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("amount", out double amount))
				Amount = (float)amount;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader)
			{
				Transform = new ccl.Transform(MappingTransform)
			};

			var perturbing_part1_node = new PerturbingPart1TextureProceduralNode(shader);

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(perturbing_part1_node.ins.UVW);

			var perturbing_part2_node = new PerturbingPart2TextureProceduralNode(shader);

			perturbing_part2_node.Amount = Amount;

			perturbing_part1_node.outs.UVW1.Connect(perturbing_part2_node.ins.UVW);

			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW1, perturbing_part2_node.ins.Color1, null, IsData);
			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW2, perturbing_part2_node.ins.Color2, null, IsData);
			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW3, perturbing_part2_node.ins.Color3, null, IsData);

			SourceChild?.CreateAndConnectProceduralNode(shader, perturbing_part2_node.outs.PerturbedUVW, parent_color_input, parent_alpha_input, IsData);
		}

		public float Amount { get; set; } = 0.1f;
		public Procedural SourceChild { get; set; } = null;
		public Procedural PerturbChild { get; set; } = null;
	}

	public class WoodTextureProcedural : TwoColorProcedural
	{
		public WoodTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("grain-thickness", out double gain_thickness))
				GrainThickness = (float)gain_thickness;
			if (rtf.TryGetValue("radial-noise", out double radial_noise))
				RadialNoise = (float)radial_noise;
			if (rtf.TryGetValue("axial-noise", out double axial_noise))
				AxialNoise = (float)axial_noise;
			if (rtf.TryGetValue("blur-1", out double blur1))
				Blur1 = (float)blur1;
			if (rtf.TryGetValue("blur-2", out double blur2))
				Blur2 = (float)blur2;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			NoiseTextureProceduralNode noise1 = new NoiseTextureProceduralNode(shader);
			noise1.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise1.OctaveCount = 2;
			noise1.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise1.FrequencyMultiplier = 2.17f;
			noise1.AmplitudeMultiplier = 0.5f;
			noise1.ClampMin = -1.0f;
			noise1.ClampMax = 1.0f;
			noise1.ScaleToClamp = false;
			noise1.Inverse = false;
			noise1.Gain = 0.5f;

			var noise1_transform_node = new MatrixMathNode(shader)
			{
				Transform = ccl.Transform.Scale(1.0f, 1.0f, AxialNoise)
			};

			NoiseTextureProceduralNode noise2 = new NoiseTextureProceduralNode(shader);
			noise2.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise2.OctaveCount = 2;
			noise2.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise2.FrequencyMultiplier = 2.17f;
			noise2.AmplitudeMultiplier = 0.5f;
			noise2.ClampMin = -1.0f;
			noise2.ClampMax = 1.0f;
			noise2.ScaleToClamp = false;
			noise2.Inverse = false;
			noise2.Gain = 0.5f;

			var noise2_transform_node = new MatrixMathNode(shader)
			{
				Transform = ccl.Transform.Scale(1.0f, 1.0f, AxialNoise)
			};

			NoiseTextureProceduralNode noise3 = new NoiseTextureProceduralNode(shader);
			noise3.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise3.OctaveCount = 2;
			noise3.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise3.FrequencyMultiplier = 2.17f;
			noise3.AmplitudeMultiplier = 0.5f;
			noise3.ClampMin = -1.0f;
			noise3.ClampMax = 1.0f;
			noise3.ScaleToClamp = false;
			noise3.Inverse = false;
			noise3.Gain = 0.5f;

			var noise3_transform_node = new MatrixMathNode(shader)
			{
				Transform = ccl.Transform.Scale(1.0f, 1.0f, AxialNoise)
			};

			WavesTextureProceduralNode waves = new WavesTextureProceduralNode(shader);
			waves.WaveType = WavesTextureProceduralNode.WaveTypes.RADIAL;
			waves.WaveWidth = GrainThickness;
			waves.Contrast1 = 1.0f - Blur1;
			waves.Contrast2 = 1.0f - Blur2;
			waves.WaveWidthTextureOn = false;
			waves.ins.Color1.Value = Color1.ToFloat4();
			waves.ins.Alpha1.Value = Color1.ToFloat4().w;
			//waves.TextureAmount1 = TextureAmount1; // TODO
			waves.ins.Color2.Value = Color2.ToFloat4();
			waves.ins.Alpha2.Value = Color2.ToFloat4().w;
			//waves.TextureAmount2 = TextureAmount2; // TODO

			Child1?.CreateAndConnectProceduralNode(shader, uvw_output, waves.ins.Color1, waves.ins.Alpha1.ToList(), IsData);
			Child2?.CreateAndConnectProceduralNode(shader, uvw_output, waves.ins.Color2, waves.ins.Alpha2.ToList(), IsData);

			var perturbing1_transform_node = new MatrixMathNode(shader)
			{
				Transform = new ccl.Transform(MappingTransform)
			};

			PerturbingPart1TextureProceduralNode perturbing1 = new PerturbingPart1TextureProceduralNode(shader);
			PerturbingPart2TextureProceduralNode perturbing2 = new PerturbingPart2TextureProceduralNode(shader);
			perturbing2.Amount = RadialNoise;

			uvw_output.Connect(perturbing1_transform_node.ins.Vector);
			perturbing1_transform_node.outs.Vector.Connect(perturbing1.ins.UVW);
			perturbing1.outs.UVW1.Connect(noise1_transform_node.ins.Vector);
			perturbing1.outs.UVW2.Connect(noise2_transform_node.ins.Vector);
			perturbing1.outs.UVW3.Connect(noise3_transform_node.ins.Vector);

			noise1_transform_node.outs.Vector.Connect(noise1.ins.UVW);
			noise2_transform_node.outs.Vector.Connect(noise2.ins.UVW);
			noise3_transform_node.outs.Vector.Connect(noise3.ins.UVW);

			perturbing1.outs.UVW1.Connect(perturbing2.ins.UVW);
			noise1.outs.Color.Connect(perturbing2.ins.Color1);
			noise2.outs.Color.Connect(perturbing2.ins.Color2);
			noise3.outs.Color.Connect(perturbing2.ins.Color3);

			perturbing2.outs.PerturbedUVW.Connect(waves.ins.UVW);
			waves.outs.Color.Connect(parent_color_input);
			ConnectAlphaNode(waves.outs.Alpha, parent_alpha_input);
		}

		public float GrainThickness { get; set; } = 0.0f;
		public float RadialNoise { get; set; } = 0.0f;
		public float AxialNoise { get; set; } = 0.0f;
		public float Blur1 { get; set; } = 0.0f;
		public float Blur2 { get; set; } = 0.0f;
	}

	public class BitmapTextureProcedural : Procedural
	{
		public BitmapTextureProcedural(RenderTexture render_texture, CyclesTextureImage cycles_texture, BitmapConverter bitmap_converter, uint docsrn, float gamma, bool should_simulate, bool is_color) : base(render_texture, is_color)
		{
			CyclesTexture = cycles_texture;
			BitmapConverter = bitmap_converter;
			Gamma = gamma;
			Utilities.HandleRenderTexture(render_texture, cycles_texture, false, true, bitmap_converter, docsrn, gamma, should_simulate, is_color);

			Repeat = cycles_texture.Repeat;

			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("filter", out bool filter))
				Filter = filter;

			if (rtf.TryGetValue("mirror-alternate-tiles", out bool alternate_tiles))
				AlternateTiles = alternate_tiles;

			if (rtf.TryGetValue("use-alpha-channel", out bool use_alpha))
				UseAlpha = use_alpha;

			if (rtf.TryGetValue("alpha-transparency", out bool use_alpha_transp))
				UseAlpha |= use_alpha_transp;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader, "matrix math node");

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var image_texture_node = new ImageTextureNode(shader, "image texture node");

			if (CyclesTexture.HasTextureImage)
			{
				image_texture_node.ins.Filename.Value = CyclesTexture.Filename;
			}

			image_texture_node.UseAlpha = UseAlpha;
			if(IsData)
			{
				image_texture_node.ColorSpace = TextureNode.TextureColorSpace.None;
			}
			else
			{
				image_texture_node.ColorSpace = TextureNode.TextureColorSpace.Color;
			}
			image_texture_node.AlternateTiles = AlternateTiles;
			image_texture_node.Interpolation = Filter ? InterpolationType.Cubic : InterpolationType.Closest;
			image_texture_node.Extension = Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(image_texture_node.ins.Vector);
			CreateAndConnectAdjustmentNode(shader, image_texture_node.outs.Color, parent_color_input);
			if (CyclesTexture.UseColorMask) {

				var sep_img_col = new SeparateRgbNode(shader, "separate image color");
				var sep_mask_col = new SeparateRgbNode(shader, "separate mask color");

				var comp_r = new MathNode(shader, "compare r channels")
				{
					Operation = MathNode.Operations.Compare
				};
				comp_r.ins.Value3.Value = CyclesTexture.ColorMaskSensitivity;

				var comp_g = new MathNode(shader, "compare g channels")
				{
					Operation = MathNode.Operations.Compare
				};
				comp_g.ins.Value3.Value = CyclesTexture.ColorMaskSensitivity;

				var comp_b = new MathNode(shader, "compare b channels")
				{
					Operation = MathNode.Operations.Compare
				};
				comp_b.ins.Value3.Value = CyclesTexture.ColorMaskSensitivity;

				var comp_comps = new MathNode(shader, "compare comps sum")
				{
					Operation = MathNode.Operations.Compare
				};
				comp_comps.ins.Value3.Value = 0.0001f;
				comp_comps.ins.Value1.Value = 3.0f;

				var add_comp_rg = new MathAdd(shader, "add r and g comps");
				var add_comp_b = new MathAdd(shader, "add b comp");

				var invert_comp = new MathSubtract(shader, "invert_comp");
				invert_comp.ins.Value1.Value = 1.0f;
				var adjust_img_alpha = new MathMultiply(shader, "adjust_img_alpha");

				image_texture_node.outs.Color.Connect(sep_img_col.ins.Image);
				sep_mask_col.ins.Image.Value = CyclesTexture.ColorMask.ToFloat4();

				sep_img_col.outs.R.Connect(comp_r.ins.Value1);
				sep_mask_col.outs.R.Connect(comp_r.ins.Value2);

				sep_img_col.outs.G.Connect(comp_g.ins.Value1);
				sep_mask_col.outs.G.Connect(comp_g.ins.Value2);

				sep_img_col.outs.B.Connect(comp_b.ins.Value1);
				sep_mask_col.outs.B.Connect(comp_b.ins.Value2);

				comp_r.outs.Value.Connect(add_comp_rg.ins.Value1);
				comp_g.outs.Value.Connect(add_comp_rg.ins.Value2);

				add_comp_rg.outs.Value.Connect(add_comp_b.ins.Value1);
				comp_b.outs.Value.Connect(add_comp_b.ins.Value2);


				add_comp_b.outs.Value.Connect(comp_comps.ins.Value2);

				comp_comps.outs.Value.Connect(invert_comp.ins.Value2);

				image_texture_node.outs.Alpha.Connect(adjust_img_alpha.ins.Value1);
				invert_comp.outs.Value.Connect(adjust_img_alpha.ins.Value2);

				ConnectAlphaNode(adjust_img_alpha.outs.Value, parent_alpha_input);
			} else {
				ConnectAlphaNode(image_texture_node.outs.Alpha, parent_alpha_input);
			}
		}

		public CyclesTextureImage CyclesTexture { get; set; } = null;
		public BitmapConverter BitmapConverter { get; set; } = null;
		public bool UseAlpha { get; set; } = true;
		public bool AlternateTiles { get; set; } = false;
		public bool Filter { get; set; } = true;
		public float Gamma { get; set; } = 1.0f;
		public bool Repeat { get; set; } = true;
	}

	public class AddTextureProcedural : TwoColorProcedural
	{
		public AddTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var mix_node = new MixNode(shader);

			mix_node.BlendType = MixNode.BlendTypes.Add;
			mix_node.ins.Fac.Value = 1.0f;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, mix_node.ins.Color1, mix_node.ins.Color2, IsData);

			mix_node.outs.Color.Connect(parent_color_input);
		}
	}

	public class MultiplicationTextureProcedural : TwoColorProcedural
	{
		public MultiplicationTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var mix_node = new MixNode(shader);

			mix_node.BlendType = MixNode.BlendTypes.Multiply;
			mix_node.ins.Fac.Value = 1.0f;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, mix_node.ins.Color1, mix_node.ins.Color2, IsData);

			mix_node.outs.Color.Connect(parent_color_input);
		}
	}

	public class GradientTextureProcedural : TwoColorProcedural
	{
		public GradientTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("gradient-type", out int gradient_type))
				GradientType = (GradientTextureProceduralNode.GradientTypes)gradient_type;

			if (rtf.TryGetValue("flip-alternate", out bool flip_alternate))
				FlipAlternate = flip_alternate;

			if (rtf.TryGetValue("custom-curve", out bool use_custom_curve))
				UseCustomCurve = use_custom_curve;

			// TODO:
			//if (rtf.TryGetValue("point-width", out int point_width))
			//	PointWidth = point_width;

			//if (rtf.TryGetValue("point-height", out int point_height))
			//	PointHeight = point_height;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var gradient_node = new GradientTextureProceduralNode(shader);

			gradient_node.GradientType = GradientType;
			gradient_node.FlipAlternate = FlipAlternate;
			gradient_node.UseCustomCurve = UseCustomCurve;
			gradient_node.PointWidth = PointWidth;
			gradient_node.PointHeight = PointHeight;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, gradient_node.ins.Color1, gradient_node.ins.Alpha1, gradient_node.ins.Color2, gradient_node.ins.Alpha2, IsData);

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(gradient_node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, gradient_node.outs.Color, parent_color_input);
			ConnectAlphaNode(gradient_node.outs.Alpha, parent_alpha_input);
		}

		public GradientTextureProceduralNode.GradientTypes GradientType { get; set; }
		public bool FlipAlternate { get; set; }
		public bool UseCustomCurve { get; set; }
		public int PointWidth { get; set; }
		public int PointHeight { get; set; }
	}

	public class BlendTextureProcedural : TwoColorProcedural
	{
		public BlendTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("texture-on", out bool texture_on))
				UseBlendColor = texture_on;

			if (rtf.TryGetValue("blend-factor", out double blend_factor))
				BlendFactor = (float)blend_factor;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var blend_node = new BlendTextureProceduralNode(shader);

			blend_node.UseBlendColor = UseBlendColor;
			blend_node.BlendFactor = BlendFactor;

			uvw_output.Connect(transform_node.ins.Vector);

			// Recursive call
			ConnectChildNodes(shader, transform_node.outs.Vector, blend_node.ins.Color1, blend_node.ins.Alpha1, blend_node.ins.Color2, blend_node.ins.Alpha2, IsData);

			// Recursive call
			BlendChild?.CreateAndConnectProceduralNode(shader, transform_node.outs.Vector, blend_node.ins.BlendColor, null, IsData);

			CreateAndConnectAdjustmentNode(shader, blend_node.outs.Color, parent_color_input);
			ConnectAlphaNode(blend_node.outs.Alpha, parent_alpha_input);
		}

		public bool UseBlendColor { get; set; }
		public float BlendFactor { get; set; }
		public Procedural BlendChild { get; set; } = null;
	}

	public class ExposureTextureProcedural : Procedural
	{
		public ExposureTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("exposure", out double exposure))
				Exposure = (float)exposure;

			if (rtf.TryGetValue("multiplier", out double multiplier))
				Multiplier = (float)multiplier;

			object world_luminance = render_texture.GetParameter("world-luminance");
			WorldLuminance = (float)Convert.ToDouble(world_luminance);

			object max_luminance = render_texture.GetParameter("max-luminance");
			MaxLuminance = (float)Convert.ToDouble(max_luminance);
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var exposure_node = new ExposureTextureProceduralNode(shader);

			exposure_node.Exposure = Exposure;
			exposure_node.Multiplier = Multiplier;
			exposure_node.WorldLuminance = WorldLuminance;
			exposure_node.MaxLuminance = MaxLuminance;

			uvw_output.Connect(transform_node.ins.Vector);

			// Recursive call
			ExposureChild?.CreateAndConnectProceduralNode(shader, transform_node.outs.Vector, exposure_node.ins.Color, parent_alpha_input, IsData);

			exposure_node.outs.Color.Connect(parent_color_input);
		}

		public float Exposure { get; set; }
		public float Multiplier { get; set; }
		public float WorldLuminance { get; set; }
		public float MaxLuminance { get; set; }
		public Procedural ExposureChild { get; set; }
	}

	public class FbmTextureProcedural : TwoColorProcedural
	{
		public FbmTextureProcedural(RenderTexture render_texture, bool is_turbulent, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			IsTurbulent = is_turbulent;

			if (rtf.TryGetValue("max-octaves", out int max_octaves))
				MaxOctaves = max_octaves;

			if (rtf.TryGetValue("gain", out double gain))
				Gain = (float)gain;

			if (rtf.TryGetValue("roughness", out double roughness))
				Roughness = (float)roughness;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var fbm_node = new FbmTextureProceduralNode(shader);

			fbm_node.IsTurbulent = IsTurbulent;
			fbm_node.MaxOctaves = MaxOctaves;
			fbm_node.Gain = Gain;
			fbm_node.Roughness = Roughness;

			uvw_output.Connect(transform_node.ins.Vector);

			// Recursive call
			ConnectChildNodes(shader, uvw_output, fbm_node.ins.Color1, fbm_node.ins.Alpha1, fbm_node.ins.Color2, fbm_node.ins.Alpha2, IsData);

			transform_node.outs.Vector.Connect(fbm_node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, fbm_node.outs.Color, parent_color_input);
			ConnectAlphaNode(fbm_node.outs.Alpha, parent_alpha_input);
		}

		public bool IsTurbulent { get; set; }
		public int MaxOctaves { get; set; }
		public float Gain { get; set; }
		public float Roughness { get; set; }
	}

	public class GraniteTextureProcedural : TwoColorProcedural
	{
		public GraniteTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("spot-size", out double spot_size))
				SpotSize = spot_size;

			if (rtf.TryGetValue("blending", out double blending))
				Blending = blending;

			if (rtf.TryGetValue("size", out double size))
				Size = size;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var noise_node = new NoiseTextureProceduralNode(shader);

			noise_node.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise_node.OctaveCount = 2;
			noise_node.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise_node.FrequencyMultiplier = 2.17f;
			noise_node.AmplitudeMultiplier = 0.4f;

			double spot = (2.0 * SpotSize - 1.0);
			spot *= spot;
			if (SpotSize < 0.5)
				spot = -spot;

			double clampMin = Math.Min(+0.9, Math.Max(-0.9, spot - 0.5 * Math.Max(0.01, Blending * 0.5)));
			double clampMax = Math.Max(-0.9, Math.Min(+0.9, spot + 0.5 * Math.Max(0.01, Blending * 0.5)));
			if (clampMin >= clampMax)
			{
				clampMin = (clampMin + clampMax) / 2.0 - 0.01;
				clampMax = (clampMin + clampMax) / 2.0 + 0.01;
			}

			noise_node.ClampMin = (float)clampMin;
			noise_node.ClampMax = (float)clampMax;
			noise_node.ScaleToClamp = true;
			noise_node.Inverse = true;

			float scale_size = (float)(8.0 / Size);

			var noise_transform_node = new MatrixMathNode(shader)
			{
				Transform = ccl.Transform.Scale(scale_size, scale_size, scale_size)
			};

			var blend_transform_node = new MatrixMathNode(shader);

			blend_transform_node.Transform = new ccl.Transform(MappingTransform);

			var blend_node = new BlendTextureProceduralNode(shader);

			blend_node.BlendFactor = 0.5f;
			blend_node.UseBlendColor = true;

			uvw_output.Connect(blend_transform_node.ins.Vector);
			blend_transform_node.outs.Vector.Connect(blend_node.ins.UVW);
			blend_transform_node.outs.Vector.Connect(noise_transform_node.ins.Vector);
			noise_transform_node.outs.Vector.Connect(noise_node.ins.UVW);
			noise_node.outs.Color.Connect(blend_node.ins.BlendColor);

			ConnectChildNodes(shader, blend_transform_node.outs.Vector, blend_node.ins.Color1, blend_node.ins.Alpha1, blend_node.ins.Color2, blend_node.ins.Alpha2, IsData);

			CreateAndConnectAdjustmentNode(shader, blend_node.outs.Color, parent_color_input);
			ConnectAlphaNode(blend_node.outs.Alpha, parent_alpha_input);
		}

		public double SpotSize { get; set; }
		public double Blending { get; set; }
		public double Size { get; set; }
	}

	public class GridTextureProcedural : TwoColorProcedural
	{
		public GridTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("cells", out int cells))
				Cells = cells;

			if (rtf.TryGetValue("font-thickness", out double font_thickness))
				FontThickness = (float)font_thickness;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var grid_node = new GridTextureProceduralNode(shader);

			grid_node.Cells = Cells;
			grid_node.FontThickness = FontThickness;

			uvw_output.Connect(transform_node.ins.Vector);

			// Recursive call
			ConnectChildNodes(shader, uvw_output, grid_node.ins.Color1, grid_node.ins.Alpha1, grid_node.ins.Color2, grid_node.ins.Alpha2, IsData);

			transform_node.outs.Vector.Connect(grid_node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, grid_node.outs.Color, parent_color_input);
			ConnectAlphaNode(grid_node.outs.Alpha, parent_alpha_input);
		}

		public int Cells { get; set; }
		public float FontThickness { get; set; }
	}

	public class ProjectionChangerTextureProcedural : Procedural
	{
		public ProjectionChangerTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("input-projection", out string input_projection_type))
				InputProjectionType = StringToProjectionType(input_projection_type);

			if (rtf.TryGetValue("output-projection", out string output_projection_type))
				OutputProjectionType = StringToProjectionType(output_projection_type);

			if (rtf.TryGetValue("azimuth", out double azimuth))
				Azimuth = (float)azimuth;

			if (rtf.TryGetValue("altitude", out double altitude))
				Altitude = (float)altitude;
		}

		private static ProjectionChangerTextureProceduralNode.ProjectionTypes StringToProjectionType(string enum_string)
		{
			switch (enum_string)
			{
				case "planar": return ProjectionChangerTextureProceduralNode.ProjectionTypes.PLANAR;
				case "light-probe": return ProjectionChangerTextureProceduralNode.ProjectionTypes.LIGHTPROBE;
				case "equirect": return ProjectionChangerTextureProceduralNode.ProjectionTypes.EQUIRECT;
				case "cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.CUBEMAP;
				case "vertical-cross-cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.VERTICAL_CROSS_CUBEMAP;
				case "horizontal-cross-cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.HORIZONTAL_CROSS_CUBEMAP;
				case "emap": return ProjectionChangerTextureProceduralNode.ProjectionTypes.EMAP;
				case "same-as-input": return ProjectionChangerTextureProceduralNode.ProjectionTypes.SAME_AS_INPUT;
				case "hemispherical": return ProjectionChangerTextureProceduralNode.ProjectionTypes.HEMISPHERICAL;
				default:
					{
						System.Diagnostics.Debug.Assert(false);
						return ProjectionChangerTextureProceduralNode.ProjectionTypes.EQUIRECT;
					}
			}
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var projection_changer_node = new ProjectionChangerTextureProceduralNode(shader);

			projection_changer_node.InputProjectionType = InputProjectionType;
			projection_changer_node.OutputProjectionType = OutputProjectionType;
			projection_changer_node.Altitude = Altitude;
			projection_changer_node.Azimuth = Azimuth;

			uvw_output.Connect(transform_node.ins.Vector);

			transform_node.outs.Vector.Connect(projection_changer_node.ins.UVW);

			// Recursive call
			ProjectionChangerChild?.CreateAndConnectProceduralNode(shader, projection_changer_node.outs.OutputUVW, parent_color_input, parent_alpha_input, IsData);
		}

		public ProjectionChangerTextureProceduralNode.ProjectionTypes InputProjectionType;
		public ProjectionChangerTextureProceduralNode.ProjectionTypes OutputProjectionType;
		public float Azimuth { get; set; }
		public float Altitude { get; set; }
		public Procedural ProjectionChangerChild { get; set; } = null;
	}

	public class HighDynamicRangeTextureProcedural : Procedural
	{
		public HighDynamicRangeTextureProcedural(RenderTexture render_texture, CyclesTextureImage cycles_texture, BitmapConverter bitmap_converter, uint docsrn, bool is_color) : base(render_texture, is_color)
		{
			CyclesTexture = cycles_texture;
			BitmapConverter = bitmap_converter;
			Utilities.HandleRenderTexture(render_texture, cycles_texture, false, IsBitmapTexture, bitmap_converter, docsrn, 1.0f, false, is_color);

			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("input-projection", out string input_projection_type))
				InputProjectionType = StringToProjectionType(input_projection_type);

			if (rtf.TryGetValue("output-projection", out string output_projection_type))
				OutputProjectionType = StringToProjectionType(output_projection_type);

			if (rtf.TryGetValue("azimuth", out double azimuth))
				Azimuth = (float)azimuth;

			if (rtf.TryGetValue("altitude", out double altitude))
				Altitude = (float)altitude;

			if (rtf.TryGetValue("filter", out bool filter))
				Filter = filter;

			if (rtf.TryGetValue("multiplier", out double multiplier))
				Multiplier = (float)multiplier;
		}

		private static ProjectionChangerTextureProceduralNode.ProjectionTypes StringToProjectionType(string enum_string)
		{
			switch (enum_string)
			{
				case "planar": return ProjectionChangerTextureProceduralNode.ProjectionTypes.PLANAR;
				case "light-probe": return ProjectionChangerTextureProceduralNode.ProjectionTypes.LIGHTPROBE;
				case "equirect": return ProjectionChangerTextureProceduralNode.ProjectionTypes.EQUIRECT;
				case "cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.CUBEMAP;
				case "vertical-cross-cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.VERTICAL_CROSS_CUBEMAP;
				case "horizontal-cross-cube-map": return ProjectionChangerTextureProceduralNode.ProjectionTypes.HORIZONTAL_CROSS_CUBEMAP;
				case "emap": return ProjectionChangerTextureProceduralNode.ProjectionTypes.EMAP;
				case "same-as-input": return ProjectionChangerTextureProceduralNode.ProjectionTypes.SAME_AS_INPUT;
				case "hemispherical": return ProjectionChangerTextureProceduralNode.ProjectionTypes.HEMISPHERICAL;
				default:
					{
						System.Diagnostics.Debug.Assert(false);
						return ProjectionChangerTextureProceduralNode.ProjectionTypes.EQUIRECT;
					}
			}
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Transform = new ccl.Transform(MappingTransform);

			var projection_changer_node = new ProjectionChangerTextureProceduralNode(shader);

			projection_changer_node.InputProjectionType = InputProjectionType;
			projection_changer_node.OutputProjectionType = OutputProjectionType;
			projection_changer_node.Altitude = Altitude;
			projection_changer_node.Azimuth = Azimuth;

			var image_texture_node = new ImageTextureNode(shader);

			if (CyclesTexture.HasTextureImage)
			{
				image_texture_node.ins.Filename.Value = CyclesTexture.Filename;
			}
			if(IsData)
			{
				image_texture_node.ColorSpace = TextureNode.TextureColorSpace.None;
			}
			else
			{
				image_texture_node.ColorSpace = TextureNode.TextureColorSpace.Color;
			}

			image_texture_node.UseAlpha = false;
			image_texture_node.AlternateTiles = false;
			image_texture_node.Interpolation = Filter ? InterpolationType.Cubic : InterpolationType.Closest;

			var multiplier_node = new MathNode(shader);
			multiplier_node.Operation = MathNode.Operations.Multiply;
			multiplier_node.ins.Value2.Value = Multiplier;

			uvw_output.Connect(transform_node.ins.Vector);

			transform_node.outs.Vector.Connect(projection_changer_node.ins.UVW);
			projection_changer_node.outs.OutputUVW.Connect(image_texture_node.ins.Vector);
			image_texture_node.outs.Color.Connect(multiplier_node.ins.Value1);

			CreateAndConnectAdjustmentNode(shader, multiplier_node.outs.Value, parent_color_input);
		}

		public CyclesTextureImage CyclesTexture { get; set; } = null;
		public BitmapConverter BitmapConverter { get; set; } = null;
		public ProjectionChangerTextureProceduralNode.ProjectionTypes InputProjectionType;
		public ProjectionChangerTextureProceduralNode.ProjectionTypes OutputProjectionType;
		public float Azimuth { get; set; }
		public float Altitude { get; set; }
		public bool Filter { get; set; }
		public float Multiplier { get; set; }
	}

	public class MarbleTextureProcedural : TwoColorProcedural
	{
		public MarbleTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("size", out double size))
				Size = (float)size;
			if (rtf.TryGetValue("vein-width", out double vein_width))
				VeinWidth = (float)vein_width;
			if (rtf.TryGetValue("blue", out double blur))
				Blur = (float)blur;
			if (rtf.TryGetValue("noise", out double noise))
				Noise = (float)noise;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			NoiseTextureProceduralNode noise1 = new NoiseTextureProceduralNode(shader);
			noise1.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise1.OctaveCount = 5;
			noise1.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise1.FrequencyMultiplier = 2.17f;
			noise1.AmplitudeMultiplier = 0.5f;
			noise1.ClampMin = -1.0f;
			noise1.ClampMax = 1.0f;
			noise1.ScaleToClamp = false;
			noise1.Inverse = false;
			noise1.Gain = 0.5f;

			NoiseTextureProceduralNode noise2 = new NoiseTextureProceduralNode(shader);
			noise2.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise2.OctaveCount = 5;
			noise2.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise2.FrequencyMultiplier = 2.17f;
			noise2.AmplitudeMultiplier = 0.5f;
			noise2.ClampMin = -1.0f;
			noise2.ClampMax = 1.0f;
			noise2.ScaleToClamp = false;
			noise2.Inverse = false;
			noise2.Gain = 0.5f;

			NoiseTextureProceduralNode noise3 = new NoiseTextureProceduralNode(shader);
			noise3.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise3.OctaveCount = 5;
			noise3.SpecSynthType = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			noise3.FrequencyMultiplier = 2.17f;
			noise3.AmplitudeMultiplier = 0.5f;
			noise3.ClampMin = -1.0f;
			noise3.ClampMax = 1.0f;
			noise3.ScaleToClamp = false;
			noise3.Inverse = false;
			noise3.Gain = 0.5f;

			var noise_transform1 = new MatrixMathNode(shader);
			var noise_transform2 = new MatrixMathNode(shader);
			var noise_transform3 = new MatrixMathNode(shader);

			noise_transform1.Transform = ccl.Transform.Scale(4.0f / Size, 4.0f / Size, 4.0f / Size);
			noise_transform2.Transform = ccl.Transform.Scale(4.0f / Size, 4.0f / Size, 4.0f / Size);
			noise_transform3.Transform = ccl.Transform.Scale(4.0f / Size, 4.0f / Size, 4.0f / Size);

			WavesTextureProceduralNode waves = new WavesTextureProceduralNode(shader);
			waves.WaveType = WavesTextureProceduralNode.WaveTypes.LINEAR;
			waves.WaveWidth = VeinWidth;
			waves.Contrast1 = 1.0f - Blur;
			waves.Contrast2 = 1.0f - Blur;
			waves.WaveWidthTextureOn = false;
			waves.ins.Color1.Value = Color1.ToFloat4();
			waves.ins.Alpha1.Value = Color1.ToFloat4().w;
			//waves.TextureAmount1 = TextureAmount1; // TODO
			waves.ins.Color2.Value = Color2.ToFloat4();
			waves.ins.Alpha2.Value = Color2.ToFloat4().w;
			//waves.TextureAmount2 = TextureAmount2; // TODO

			var waves_transform = new MatrixMathNode(shader);
			waves_transform.Transform = ccl.Transform.Scale(4.0f / Size, 4.0f / Size, 4.0f / Size);

			var perturbing_transform = new MatrixMathNode(shader);
			perturbing_transform.Transform = new ccl.Transform(MappingTransform);

			PerturbingPart1TextureProceduralNode perturbing1 = new PerturbingPart1TextureProceduralNode(shader);
			PerturbingPart2TextureProceduralNode perturbing2 = new PerturbingPart2TextureProceduralNode(shader);
			perturbing2.Amount = 0.1f * Noise;

			uvw_output.Connect(perturbing_transform.ins.Vector);
			perturbing_transform.outs.Vector.Connect(perturbing1.ins.UVW);
			perturbing_transform.outs.Vector.Connect(perturbing2.ins.UVW);

			uvw_output.Connect(noise_transform1.ins.Vector);
			uvw_output.Connect(noise_transform2.ins.Vector);
			uvw_output.Connect(noise_transform3.ins.Vector);

			perturbing1.outs.UVW1.Connect(noise_transform1.ins.Vector);
			noise_transform1.outs.Vector.Connect(noise1.ins.UVW);

			perturbing1.outs.UVW2.Connect(noise_transform2.ins.Vector);
			noise_transform2.outs.Vector.Connect(noise2.ins.UVW);

			perturbing1.outs.UVW3.Connect(noise_transform3.ins.Vector);
			noise_transform3.outs.Vector.Connect(noise3.ins.UVW);

			noise1.outs.Color.Connect(perturbing2.ins.Color1);
			noise2.outs.Color.Connect(perturbing2.ins.Color2);
			noise3.outs.Color.Connect(perturbing2.ins.Color3);

			Child1?.CreateAndConnectProceduralNode(shader, perturbing2.outs.PerturbedUVW, waves.ins.Color1, waves.ins.Alpha1.ToList(), IsData);
			Child2?.CreateAndConnectProceduralNode(shader, perturbing2.outs.PerturbedUVW, waves.ins.Color2, waves.ins.Alpha2.ToList(), IsData);

			perturbing2.outs.PerturbedUVW.Connect(waves_transform.ins.Vector);
			waves_transform.outs.Vector.Connect(waves.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, waves.outs.Color, parent_color_input);
			ConnectAlphaNode(waves.outs.Alpha, parent_alpha_input);
		}

		public float Size { get; set; } = 0.0f;
		public float VeinWidth { get; set; } = 0.0f;
		public float Blur { get; set; } = 0.0f;
		public float Noise { get; set; } = 0.0f;
	}

	public class MaskTextureProcedural : Procedural
	{
		public MaskTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("mask-type", out string mask_type))
				MaskType = StringToProjectionType(mask_type);
		}

		private static MaskTextureProceduralNode.MaskTypes StringToProjectionType(string enum_string)
		{
			switch (enum_string)
			{
				case "luminance": return MaskTextureProceduralNode.MaskTypes.LUMINANCE;
				case "red": return MaskTextureProceduralNode.MaskTypes.RED;
				case "green": return MaskTextureProceduralNode.MaskTypes.GREEN;
				case "blue": return MaskTextureProceduralNode.MaskTypes.BLUE;
				case "alpha": return MaskTextureProceduralNode.MaskTypes.ALPHA;
				default:
					{
						System.Diagnostics.Debug.Assert(false);
						return MaskTextureProceduralNode.MaskTypes.LUMINANCE;
					}
			}
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var mask_node = new MaskTextureProceduralNode(shader);

			mask_node.MaskType = MaskType;

			// Recursive call
			MaskChild?.CreateAndConnectProceduralNode(shader, uvw_output, mask_node.ins.Color, mask_node.ins.Alpha.ToList(), IsData);
			mask_node.ins.Alpha.Value = 1.0f;

			mask_node.outs.Color.Connect(parent_color_input);
			ConnectAlphaNode(mask_node.outs.Alpha, parent_alpha_input);
		}

		public MaskTextureProceduralNode.MaskTypes MaskType;
		public Procedural MaskChild { get; set; }
	}

	public class PerlinMarbleTextureProcedural : TwoColorProcedural
	{
		public PerlinMarbleTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("levels", out int levels))
				Levels = levels;

			if (rtf.TryGetValue("noise", out double noise))
				Noise = (float)noise;

			if (rtf.TryGetValue("blur", out double blur))
				Blur = (float)blur;

			if (rtf.TryGetValue("size", out double size))
				Size = (float)size;

			if (rtf.TryGetValue("color-1-saturation", out double color1_saturation))
				Color1Saturation = (float)color1_saturation;

			if (rtf.TryGetValue("color-2-saturation", out double color2_saturation))
				Color2Saturation = (float)color2_saturation;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var perlin_marble_node = new PerlinMarbleTextureProceduralNode(shader);

			perlin_marble_node.Levels = Levels;
			perlin_marble_node.Noise = Noise;
			perlin_marble_node.Blur = Blur;
			perlin_marble_node.Size = Size;
			perlin_marble_node.Color1Saturation = Color1Saturation;
			perlin_marble_node.Color2Saturation = Color2Saturation;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, perlin_marble_node.ins.Color1, perlin_marble_node.ins.Color2, IsData);

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(perlin_marble_node.ins.UVW);

			CreateAndConnectAdjustmentNode(shader, perlin_marble_node.outs.Color, parent_color_input);
			ConnectAlphaNode(perlin_marble_node.outs.Alpha, parent_alpha_input);
		}

		public int Levels { get; set; }
		public float Noise { get; set; }
		public float Blur { get; set; }
		public float Size { get; set; }
		public float Color1Saturation { get; set; }
		public float Color2Saturation { get; set; }
	}

	public class PhysicalSkyTextureProcedural : Procedural
	{
		public PhysicalSkyTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var sun_direction = render_texture.GetParameter("physical-sky-sun-direction") as IConvertible;
			SunDirection = ConvertibleExtensions.ToVector3d(sun_direction);

			var atmospheric_density = render_texture.GetParameter("physical-sky-atmospheric-density");
			AtmosphericDensity = (float)Convert.ToDouble(atmospheric_density);

			var rayleigh_scattering = render_texture.GetParameter("physical-sky-rayleigh-scattering");
			RayleighScattering = (float)Convert.ToDouble(rayleigh_scattering);

			var mie_scattering = render_texture.GetParameter("physical-sky-mie-scattering");
			MieScattering = (float)Convert.ToDouble(mie_scattering);

			var show_sun = render_texture.GetParameter("physical-sky-show-sun");
			ShowSun = Convert.ToBoolean(show_sun);

			var sun_brightness = render_texture.GetParameter("physical-sky-sun-brightness");
			SunBrightness = (float)Convert.ToDouble(sun_brightness);

			var sun_size = render_texture.GetParameter("physical-sky-sun-size");
			SunSize = (float)Convert.ToDouble(sun_size);

			var sun_color = render_texture.GetParameter("physical-sky-sun-color") as IConvertible;
			SunColor = ConvertibleExtensions.ToVector3d(sun_color);

			var inverse_wavelengths = render_texture.GetParameter("physical-sky-inverse-wavelengths") as IConvertible;
			InverseWavelengths = ConvertibleExtensions.ToVector3d(inverse_wavelengths);

			var exposure = render_texture.GetParameter("physical-sky-exposure");
			Exposure = (float)Convert.ToDouble(exposure);
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var physical_sky_node = new PhysicalSkyTextureProceduralNode(shader);

			physical_sky_node.SunDirectionX = (float)SunDirection.X;
			physical_sky_node.SunDirectionY = (float)SunDirection.Y;
			physical_sky_node.SunDirectionZ = (float)SunDirection.Z;
			physical_sky_node.AtmosphericDensity = AtmosphericDensity;
			physical_sky_node.RayleighScattering = RayleighScattering;
			physical_sky_node.MieScattering = MieScattering;
			physical_sky_node.ShowSun = ShowSun;
			physical_sky_node.SunBrightness = SunBrightness;
			physical_sky_node.SunSize = SunSize;
			physical_sky_node.SunColorRed = (float)SunColor.X;
			physical_sky_node.SunColorGreen = (float)SunColor.Y;
			physical_sky_node.SunColorBlue = (float)SunColor.Z;
			physical_sky_node.InverseWavelengthsX = (float)InverseWavelengths.X;
			physical_sky_node.InverseWavelengthsY = (float)InverseWavelengths.Y;
			physical_sky_node.InverseWavelengthsZ = (float)InverseWavelengths.Z;
			physical_sky_node.Exposure = Exposure;

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(physical_sky_node.ins.UVW);

			physical_sky_node.outs.Color.Connect(parent_color_input);
		}

		public Vector3d SunDirection { get; set; }
		public float AtmosphericDensity { get; set; }
		public float RayleighScattering { get; set; }
		public float MieScattering { get; set; }
		public bool ShowSun { get; set; }
		public float SunBrightness { get; set; }
		public float SunSize { get; set; }
		public Vector3d SunColor { get; set; }
		public Vector3d InverseWavelengths { get; set; }
		public float Exposure { get; set; }
	}

	public class ResampleTextureProcedural : Procedural
	{
		public ResampleTextureProcedural(RenderTexture render_texture, CyclesTextureImage cycles_texture, BitmapConverter bitmap_converter, uint docsrn, bool is_color) : base(render_texture, is_color)
		{
			CyclesTexture = cycles_texture;
			BitmapConverter = bitmap_converter;
			Utilities.HandleRenderTexture(render_texture, cycles_texture, false, IsBitmapTexture, bitmap_converter, docsrn, 1.0f, false, is_color);

			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("interpolate", out bool interpolate))
				Interpolate = interpolate;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			transform_node.Operation = MatrixMathNode.Operations.Point;
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var image_texture_node = new ImageTextureNode(shader);

			if (CyclesTexture.HasTextureImage)
			{
				if (CyclesTexture.HasByteImage)
				{
					image_texture_node.ByteImagePtr = CyclesTexture.TexByte.Memory();
				}
				else if (CyclesTexture.HasFloatImage)
				{
					image_texture_node.FloatImagePtr = CyclesTexture.TexFloat.Memory();
				}
				image_texture_node.Filename = CyclesTexture.Name;
				image_texture_node.Width = (uint)CyclesTexture.TexWidth;
				image_texture_node.Height = (uint)CyclesTexture.TexHeight;
			}

			image_texture_node.UseAlpha = false;
			image_texture_node.AlternateTiles = false;
			image_texture_node.Interpolation = Interpolate ? InterpolationType.Cubic : InterpolationType.Closest;

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(image_texture_node.ins.Vector);
			image_texture_node.outs.Color.Connect(parent_color_input);
		}

		public CyclesTextureImage CyclesTexture { get; set; } = null;
		public BitmapConverter BitmapConverter { get; set; } = null;
		public bool Interpolate { get; set; } = true;
	}

	public class SingleColorTextureProcedural : OneColorProcedural
	{
		public SingleColorTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("use-object-color", out bool use_object_color))
				UseObjectColor = use_object_color;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			// Color adjustments don't work here because of the way this node is set up.
			// TODO: Create an actual node here so that we can connect the adjust node
			// between the new node and the parent color node. Maybe.

			if (UseObjectColor)
			{
				parent_color_input.Value = new float4(0.5f, 0.5f, 0.5f, 1.0f);
				foreach (var _parent_alpha_input in parent_alpha_input)
				{
					if (_parent_alpha_input is FloatSocket fs)
					{
						fs.Value = 1.0f;
					}
				}
			}
			else
			{
				// Recursive call
				ConnectChildNode(shader, uvw_output, parent_color_input, parent_alpha_input, IsData);
			}
		}

		public bool UseObjectColor { get; set; }
	}

	public class StuccoTextureProcedural : TwoColorProcedural
	{
		public StuccoTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("size", out double size))
				Size = (float)size;
			if (rtf.TryGetValue("thickness", out double thickness))
				Thickness = (float)thickness;
			if (rtf.TryGetValue("threshold", out double threshold))
				Threshold = (float)threshold;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			NoiseTextureProceduralNode noise = new NoiseTextureProceduralNode(shader);
			noise.NoiseType = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
			noise.OctaveCount = 2;
			noise.FrequencyMultiplier = 1.0f + Thickness;
			noise.AmplitudeMultiplier = 1.0f;
			noise.ClampMin = 0.6f * Threshold;
			noise.ClampMax = 0.6f;
			noise.ScaleToClamp = true;
			noise.Inverse = false;
			noise.Gain = 0.5f;


			var noise_transform = new MatrixMathNode(shader);

			noise_transform.Transform = ccl.Transform.Scale(8.0f / Size, 8.0f / Size, 8.0f / Size);

			BlendTextureProceduralNode blend = new BlendTextureProceduralNode(shader);

			blend.UseBlendColor = true;
			blend.ins.Color1.Value = Color1.ToFloat4();
			blend.ins.Alpha1.Value = Color1.ToFloat4().w;
			//pBlendTexture->SetTextureOn1(TextureOn1()); // TODO
			//pBlendTexture->SetTextureAmount1(TextureAmount1()); // TODO
			blend.ins.Color2.Value = Color2.ToFloat4();
			blend.ins.Alpha2.Value = Color2.ToFloat4().w;
			//pBlendTexture->SetTextureOn2(TextureOn2()); // TODO
			//pBlendTexture->SetTextureAmount2(TextureAmount2()); // TODO

			var blend_transform = new MatrixMathNode(shader);
			blend_transform.Transform = new ccl.Transform(MappingTransform);

			Child1?.CreateAndConnectProceduralNode(shader, blend_transform.outs.Vector, blend.ins.Color1, blend.ins.Alpha1.ToList(), IsData);
			Child2?.CreateAndConnectProceduralNode(shader, blend_transform.outs.Vector, blend.ins.Color2, blend.ins.Alpha2.ToList(), IsData);

			uvw_output.Connect(noise_transform.ins.Vector);
			uvw_output.Connect(blend_transform.ins.Vector);

			noise_transform.outs.Vector.Connect(noise.ins.UVW);
			blend_transform.outs.Vector.Connect(blend.ins.UVW);

			noise.outs.Color.Connect(blend.ins.BlendColor);

			CreateAndConnectAdjustmentNode(shader, blend.outs.Color, parent_color_input);
			ConnectAlphaNode(blend.outs.Alpha, parent_alpha_input);
		}

		public float Size { get; set; } = 0.0f;
		public float Thickness { get; set; } = 0.0f;
		public float Threshold { get; set; } = 0.0f;
	}

	public class TextureAdjustmentTextureProcedural : Procedural
	{
		public TextureAdjustmentTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("flip-horizontally", out bool flip_horizontal))
				FlipHorizontal = flip_horizontal;

			if (rtf.TryGetValue("flip-vertically", out bool flip_vertical))
				FlipVertical = flip_vertical;

			if (rtf.TryGetValue("grayscale", out bool grayscale))
				AdjustGrayscale = grayscale;

			if (rtf.TryGetValue("invert", out bool invert))
				AdjustInvert = invert;

			if (rtf.TryGetValue("clamp", out bool clamp))
				AdjustClamp = clamp;

			if (rtf.TryGetValue("scale-to-clamp", out bool scale_to_clamp))
				AdjustScaleToClamp = scale_to_clamp;

			if (rtf.TryGetValue("multiplier", out double multiplier))
				AdjustMultiplier = (float)multiplier;

			if (rtf.TryGetValue("clamp-min", out double clamp_min))
				AdjustClampMin = (float)clamp_min;

			if (rtf.TryGetValue("clamp-max", out double clamp_max))
				AdjustClampMax = (float)clamp_max;

			if (rtf.TryGetValue("gain", out double gain))
				AdjustGain = (float)gain;

			if (rtf.TryGetValue("gamma", out double gamma))
				AdjustGamma = (float)gamma;

			if (rtf.TryGetValue("saturation", out double saturation))
				AdjustSaturation = (float)saturation;

			if (rtf.TryGetValue("hue-shift", out double hue_shift))
				AdjustHueShift = (float)hue_shift;

			AdjustIsHdr = render_texture.IsHdrCapable();
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);

			float dx = FlipHorizontal ? -1.0f : 1.0f;
			float dy = FlipVertical ? -1.0f : 1.0f;
			float tx = FlipHorizontal ? 1.0f : 0.0f;
			float ty = FlipVertical ? 1.0f : 0.0f;
			var diagonal_transform = ccl.Transform.Identity();
			diagonal_transform[0][0] = dx;
			diagonal_transform[1][1] = dy;

			transform_node.Transform = ccl.Transform.Translate(tx, ty, 0.0f) * diagonal_transform * MappingTransform;

			var texture_adjustment_node = new TextureAdjustmentTextureProceduralNode(shader);

			texture_adjustment_node.Grayscale = AdjustGrayscale;
			texture_adjustment_node.Invert = AdjustInvert;
			texture_adjustment_node.Clamp = AdjustClamp;
			texture_adjustment_node.ScaleToClamp = AdjustScaleToClamp;
			texture_adjustment_node.Multiplier = AdjustMultiplier;
			texture_adjustment_node.ClampMin = AdjustClampMin;
			texture_adjustment_node.ClampMax = AdjustClampMax;
			texture_adjustment_node.Gain = AdjustGain;
			texture_adjustment_node.Gamma = AdjustGamma;
			texture_adjustment_node.Saturation = AdjustSaturation;
			texture_adjustment_node.HueShift = AdjustHueShift;
			texture_adjustment_node.IsHdr = AdjustIsHdr;

			uvw_output.Connect(transform_node.ins.Vector);

			TextureAdjustmentChild?.CreateAndConnectProceduralNode(shader, transform_node.outs.Vector, texture_adjustment_node.ins.Color, parent_alpha_input, IsData);

			texture_adjustment_node.outs.Color.Connect(parent_color_input);
		}

		public bool FlipHorizontal { get; set; }
		public bool FlipVertical { get; set; }
		public Procedural TextureAdjustmentChild { get; set; }
	}

	public class TileTextureProcedural : TwoColorProcedural
	{
		public TileTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("tile-type", out string tile_type))
				TileType = StringToTileType(tile_type);

			rtf.TryGetValue("phase-x", out double phase_x);
			rtf.TryGetValue("phase-y", out double phase_y);
			rtf.TryGetValue("phase-z", out double phase_z);

			Phase = new Vector3d(phase_x, phase_y, phase_z);

			rtf.TryGetValue("width-x", out double width_x);
			rtf.TryGetValue("width-y", out double width_y);
			rtf.TryGetValue("width-z", out double width_z);

			JoinWidth = new Vector3d(width_x, width_y, width_z);
		}

		private static TileTextureProceduralNode.TileTypes StringToTileType(string enum_string)
		{
			switch (enum_string)
			{
				case "3d-rectangular": return TileTextureProceduralNode.TileTypes.RECTANGULAR_3D;
				case "2d-rectangular": return TileTextureProceduralNode.TileTypes.RECTANGULAR_2D;
				case "2d_hexagonal": return TileTextureProceduralNode.TileTypes.HEXAGONAL_2D;
				case "2d-triangular": return TileTextureProceduralNode.TileTypes.TRIANGULAR_2D;
				case "2d_octagonal": return TileTextureProceduralNode.TileTypes.OCTAGONAL_2D;
				default:
					{
						System.Diagnostics.Debug.Assert(false);
						return TileTextureProceduralNode.TileTypes.RECTANGULAR_3D;
					}
			}
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			var transform_node = new MatrixMathNode(shader);
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var tile_node = new TileTextureProceduralNode(shader);

			tile_node.TileType = TileType;
			tile_node.PhaseX = (float)Phase.X;
			tile_node.PhaseY = (float)Phase.Y;
			tile_node.PhaseZ = (float)Phase.Z;
			tile_node.JoinWidthX = (float)JoinWidth.X;
			tile_node.JoinWidthY = (float)JoinWidth.Y;
			tile_node.JoinWidthZ = (float)JoinWidth.Z;

			uvw_output.Connect(transform_node.ins.Vector);

			ConnectChildNodes(shader, uvw_output, tile_node.ins.Color1, tile_node.ins.Alpha1, tile_node.ins.Color2, tile_node.ins.Alpha2, IsData);

			transform_node.outs.Vector.Connect(tile_node.ins.UVW);

			tile_node.outs.Color.Connect(parent_color_input);
			ConnectAlphaNode(tile_node.outs.Alpha, parent_alpha_input);
		}

		public TileTextureProceduralNode.TileTypes TileType { get; set; }
		public Vector3d Phase { get; set; }
		public Vector3d JoinWidth { get; set; }
	}

	public class DotsTextureProcedural : TwoColorProcedural
	{
		public DotsTextureProcedural(RenderTexture render_texture, bool is_color) : base(render_texture, is_color)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("sample-area-size", out int sample_area_size))
				SampleAreaSize = sample_area_size;

			if (rtf.TryGetValue("rings", out bool rings))
				Rings = rings;

			if (rtf.TryGetValue("ring-radius", out double ring_radius))
				RingRadius = (float)ring_radius;

			if (rtf.TryGetValue("fall-off-type", out int falloff_type))
				FalloffType = (DotsTextureProceduralNode.FalloffTypes)falloff_type;

			if (rtf.TryGetValue("composition", out int composition_type))
				CompositionType = (DotsTextureProceduralNode.CompositionTypes)composition_type;
		}

		public override void CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input, List<ISocket> parent_alpha_input, bool IsData)
		{
			//var transform_node = new MatrixMathNode(shader);
			//transform_node.Transform = new ccl.Transform(MappingTransform);

			//var dots_node = new DotsTextureProceduralNode();

			//dots_node.DataCount = DataCount;
			//dots_node.TreeNodeCount = TreeNodeCount;
			//dots_node.SampleAreaSize = SampleAreaSize;
			//dots_node.Rings = Rings;
			//dots_node.RingRadius = RingRadius;
			//dots_node.FalloffType = FalloffType;
			//dots_node.CompositionType = CompositionType;

			//uvw_output.Connect(transform_node.ins.Vector);

			//ConnectChildNodes(shader, uvw_output, dots_node.ins.Color1, dots_node.ins.Color2, IsData);

			//transform_node.outs.Vector.Connect(dots_node.ins.UVW);

			//dots_node.outs.Color.Connect(parent_color_input);
		}

		public int DataCount { get; set; }
		public int TreeNodeCount { get; set; }
		public float SampleAreaSize { get; set; }
		public bool Rings { get; set; }
		public float RingRadius { get; set; }
		public DotsTextureProceduralNode.FalloffTypes FalloffType { get; set; }
		public DotsTextureProceduralNode.CompositionTypes CompositionType { get; set; }
	}

	public class ShaderConverter
	{
		private Guid realtimDisplaMaterialId = new Guid("e6cd1973-b739-496e-ab69-32957fa48492");

		/// <summary>
		/// Create a CyclesShader based on given Material m
		/// </summary>
		/// <param name="rm">Material to convert to CyclesShader</param>
		/// <param name="lw">LinearWorkflow data for this shader (gamma)</param>
		/// <param name="decals">Decals to integrate into the shader</param>
		/// <returns>The CyclesShader</returns>
		public CyclesShader RecordDataToSetupCyclesShader(RenderMaterial rm, LinearWorkflow lw, uint mid, BitmapConverter bitmapConverter, List<CyclesDecal> decals, uint docsrn)
		{
			var shader = new CyclesShader(mid, bitmapConverter, docsrn)
			{
				Type = CyclesShader.Shader.Diffuse,
				Decals = decals,
			};

			if (rm.TypeId.Equals(realtimDisplaMaterialId))
			{
				if (rm.FindChild("front") is RenderMaterial front)
				{
					shader.RecordDataForFrontShader(front, lw.PreProcessGamma);
				}
				if (rm.FindChild("back") is RenderMaterial back)
				{
					shader.RecordDataForBackShader(back, lw.PreProcessGamma);
				}
				/* Now ensure we have a valid front part of the shader. When a
				 * double-sided material is added without having a front material
				 * set this can be necessary. */
				if (shader.Front == null)
				{
					using (RenderMaterial defrm = RenderMaterial.CreateBasicMaterial(null, null))
					{
						shader.RecordDataForFrontShader(defrm, lw.PreProcessGamma);
					}
				}
			}
			else
			{
				shader.RecordDataForFrontShader(rm, lw.PreProcessGamma);
			}

			return shader;
		}

		/// <summary>
		/// Convert a Rhino.Render.ChangeQueue.Light to a CyclesLight
		/// </summary>
		/// <param name="changequeue"></param>
		/// <param name="light"></param>
		/// <param name="view"></param>
		/// <param name="gamma"></param>
		/// <returns></returns>
		internal CyclesLight ConvertLight(ChangeQueue changequeue, Light light, ViewInfo view, float gamma)
		{
			if (changequeue != null && view != null)
			{
				if (light.Data.LightStyle == LightStyle.CameraDirectional)
				{
					ChangeQueue.ConvertCameraBasedLightToWorld(changequeue, light, view);
				}
			}
			var cl = ConvertLight(light.Data, gamma);
			cl.Id = light.Id;

			if (light.ChangeType == Light.Event.Deleted)
			{
				cl.Strength = 0;
			}

			return cl;
		}

		/// <summary>
		/// Convert a Rhino light into a <c>CyclesLight</c>.
		/// </summary>
		/// <param name="lg">The Rhino light to convert</param>
		/// <param name="gamma"></param>
		/// <returns><c>CyclesLight</c></returns>
		internal CyclesLight ConvertLight(Rhino.Geometry.Light lg, float gamma)
		{
			var enabled = lg.IsEnabled ? 1.0f : 0.0f;

			var spotangle = 0.0;
			var smooth = 0.0;
			var size = 0.0f;
			var angle = 0.009180f;
			var strength = (float)(lg.Intensity * RcCore.It.AllSettings.PointLightFactor);
			var axisu = new float4(0.0f);
			var axisv = new float4(0.0f);
			var useMis = true;
			var sizeU = 0.0f;
			var sizeV = 0.0f;

			CyclesLightFalloff lfalloff;
			switch (lg.AttenuationType) {
				case Rhino.Geometry.Light.Attenuation.Constant:
					lfalloff = CyclesLightFalloff.Constant;
					break;
				case Rhino.Geometry.Light.Attenuation.Linear:
					lfalloff = CyclesLightFalloff.Linear;
					break;
				default:
					lfalloff = CyclesLightFalloff.Quadratic;
					break;
			}

			var co = RenderEngine.CreateFloat4(lg.Location.X, lg.Location.Y, lg.Location.Z);
			var dir = RenderEngine.CreateFloat4(lg.Direction.X, lg.Direction.Y, lg.Direction.Z);
			var color = RenderEngine.CreateFloat4(lg.Diffuse.R, lg.Diffuse.G, lg.Diffuse.B, lg.Diffuse.A);

			var sizeterm= 1.0f - (float)lg.ShadowIntensity;
			size = sizeterm*sizeterm*sizeterm * 100.0f; // / 100.f;

			var lt = LightType.Point;
			if (lg.IsDirectionalLight)
			{
				lt = LightType.Distant;
				strength = (float)(lg.Intensity * RcCore.It.AllSettings.SunLightFactor);
				angle = Math.Max(sizeterm * sizeterm * sizeterm * 1.5f, 0.009180f);
				//size = 0.01f;
			}
			else if (lg.IsSpotLight)
			{
				lt = LightType.Spot;
				spotangle = lg.SpotAngleRadians * 2;
				smooth = 1.0 / Math.Max(lg.HotSpot, 0.001f) - 1.0;
				strength = (float)(lg.Intensity * RcCore.It.AllSettings.SpotLightFactor);

				var light_z = lg.Direction; light_z.Unitize();
				var light_x = lg.PerpendicularDirection;
				var light_y = Vector3d.CrossProduct(light_z, light_x);

				// We overwrite 'dir' because it needs to be of same length as axisu and axisv.
				axisu = RenderEngine.CreateFloat4(light_x.X, light_x.Y, light_x.Z);
				axisv = RenderEngine.CreateFloat4(light_y.X, light_y.Y, light_y.Z);
				dir   = RenderEngine.CreateFloat4(light_z.X, light_z.Y, light_z.Z);
			}
			else if (lg.IsRectangularLight)
			{
				lt = LightType.Area;

				strength = (float)(lg.Intensity * RcCore.It.AllSettings.AreaLightFactor);

				var width = lg.Width;
				var length = lg.Length;

				sizeU = (float)width.Length;
				sizeV = (float)length.Length;

				size = 1.0f + size/10.0f;// - (float)lg.ShadowIntensity / 100.f;

				var rectLoc = lg.Location + (lg.Width * 0.5) + (lg.Length * 0.5);

				co = RenderEngine.CreateFloat4(rectLoc.X, rectLoc.Y, rectLoc.Z);

				width.Unitize();
				length.Unitize();

				axisu = RenderEngine.CreateFloat4(width.X, width.Y, width.Z);
				axisv = RenderEngine.CreateFloat4(length.X, length.Y, length.Z);

				useMis = true;
			}
			else if (lg.IsLinearLight)
			{
				throw new Exception("Linear light handled in wrong place. Contact developer nathan@mcneel.com");
			}

			strength *= enabled;

			var clight = new CyclesLight
				{
					Type = lt,
					Co = co,
					Dir = dir,
					DiffuseColor = color,
					Size = size,
					Angle = angle,

					SizeU = sizeU,
					SizeV = sizeV,

					AxisU = axisu,
					AxisV = axisv,

					UseMis = useMis,

					SpotAngle = (float)spotangle,
					SpotSmooth = (float)smooth,

					Strength = strength,

					Falloff = lfalloff,

					CastShadow = lg.ShadowIntensity > 0.0,

					Gamma = gamma,

					Id = lg.Id
				};

			return clight;
		}
	}
}
