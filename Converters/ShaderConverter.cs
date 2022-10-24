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
		public static Procedural CreateProceduralFromChild(RenderTexture render_texture, string child_name, ccl.Transform transform, List<CyclesTextureImage> texture_list, BitmapConverter _bitmapConverter)
		{
			Procedural procedural = null;

			if (render_texture.ChildSlotOn(child_name) && render_texture.ChildSlotAmount(child_name) > 0.1)
			{
				var render_texture_child = (RenderTexture)render_texture.FindChild(child_name);

				// Recursive call
				procedural = CreateProcedural(render_texture_child, transform, texture_list, _bitmapConverter);
			}

			return procedural;
		}

		public static Procedural CreateProcedural(RenderTexture render_texture, ccl.Transform transform, List<CyclesTextureImage> texture_list, BitmapConverter bitmap_converter)
		{
			if (render_texture == null)
				return null;

			Procedural procedural = null;

			if (render_texture.TypeName.Equals("2D Checker Texture"))
			{
				procedural = new CheckerTexture2dProcedural(render_texture, transform);
			}
			else if (render_texture.TypeName.Equals("Noise Texture"))
			{
				procedural = new NoiseTextureProcedural(render_texture, transform);
			}
			else if (render_texture.TypeName.Equals("Waves Texture"))
			{
				procedural = new WavesTextureProcedural(render_texture, transform);
			}
			else if (render_texture.TypeName.Equals("Perturbing Texture"))
			{
				procedural = new PerturbingTextureProcedural(render_texture, transform);
			}
			else if (render_texture.TypeName.Equals("Wood Texture"))
			{
				procedural = new WoodTextureProcedural(render_texture, transform);
			}
			else if( render_texture.TypeName.Equals("Add Texture"))
			{
				procedural = new AddTextureProcedural(render_texture, transform);
			}
			else if( render_texture.TypeName.Equals("Blend Texture"))
			{
				procedural = new BlendTextureProcedural(render_texture, transform);
			}
			else if (render_texture.TypeName.Equals("Gradient Texture"))
			{
				procedural = new GradientTextureProcedural(render_texture, transform);
			}
			else if( render_texture.TypeName.Equals("Bitmap Texture") || render_texture.TypeName.Equals("Simple Bitmap Texture"))
			{
				CyclesTextureImage cycles_texture = new CyclesTextureImage();
				texture_list.Add(cycles_texture);
				procedural = new BitmapTextureProcedural(render_texture, transform, cycles_texture, bitmap_converter);
			}

			ccl.Transform child_transform = procedural != null ? procedural.GetChildTransform() : ccl.Transform.Identity();

			if (procedural is TwoColorProcedural two_color)
			{
				two_color.Child1 = CreateProceduralFromChild(render_texture, "color-one", child_transform, texture_list, bitmap_converter);
				two_color.Child2 = CreateProceduralFromChild(render_texture, "color-two", child_transform, texture_list, bitmap_converter);

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
					waves_texture.WaveWidthChild = CreateProcedural(wave_width_child, child_transform, texture_list, bitmap_converter); // Recursive call
				}
			}

			if(procedural is PerturbingTextureProcedural perturbing_texture)
			{
				RenderTexture perturbing_source_child = (RenderTexture)render_texture.FindChild("source");
				if (perturbing_source_child != null)
				{
					perturbing_texture.SourceChild = CreateProcedural(perturbing_source_child, child_transform, texture_list, bitmap_converter); // Recursive call
				}

				RenderTexture perturbing_perturb_child = (RenderTexture)render_texture.FindChild("perturb");
				if (perturbing_perturb_child != null)
				{
					perturbing_texture.PerturbChild = CreateProcedural(perturbing_perturb_child, child_transform, texture_list, bitmap_converter); // Recursive call
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

		public Procedural(RenderTexture render_texture, ccl.Transform transform)
		{
			if(render_texture != null)
			{
				InputTransform = new ccl.Transform(transform);
				MappingTransform = ToCyclesTransform(render_texture.LocalMappingTransform) * InputTransform;
			}
		}

		protected static void ConnectInputOutputNodes(VectorSocket uvw_output, ColorSocket parent_color_input, ColorSocket node_color_output, VectorSocket node_uvw_input)
		{
			if (parent_color_input != null && node_color_output != null)
				node_color_output.Connect(parent_color_input);

			uvw_output.Connect(node_uvw_input);
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

		public abstract ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input);
		protected ccl.Transform InputTransform { get; set; } = ccl.Transform.Identity();
		protected ccl.Transform MappingTransform { get; set; } = ccl.Transform.Identity();

		public uint Id { get; set; } = 0;

		protected virtual ccl.Transform GetChildTransform() { return InputTransform; }
	}

	public abstract class TwoColorProcedural : Procedural
	{
		public TwoColorProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
		{
			Color1 = render_texture.Fields.TryGetValue("color-one", out Color4f color1) ? color1 : Color4f.Black;
			Color2 = render_texture.Fields.TryGetValue("color-two", out Color4f color2) ? color2 : Color4f.White;
			Amount1 = render_texture.Fields.TryGetValue("texture-amount-one", out double texture_amount1) ? (float)texture_amount1 : 1.0f;
			Amount2 = render_texture.Fields.TryGetValue("texture-amount-two", out double texture_amount2) ? (float)texture_amount2 : 1.0f;
			SwapColors = render_texture.Fields.TryGetValue("swap-colors", out bool swap_colors) ? swap_colors : false;
		}

		protected void ConnectChildNodes(Shader shader, VectorSocket uvw_output, ColorSocket color1_input, ColorSocket color2_input)
		{
			if (Child1 != null)
			{
				// Recursive call
				Child1.CreateAndConnectProceduralNode(shader, uvw_output, color1_input);
			}
			else
			{
				color1_input.Value = Color1.ToFloat4();
			}

			if (Child2 != null)
			{
				// Recursive call
				Child2.CreateAndConnectProceduralNode(shader, uvw_output, color2_input);
			}
			else
			{
				color2_input.Value = Color2.ToFloat4();
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

	public class CheckerTexture2dProcedural : TwoColorProcedural
	{
		public CheckerTexture2dProcedural(RenderTexture render_texture, ccl.Transform transform)
			: base(render_texture, transform)
		{
			MappingTransform *= ccl.Transform.Scale(2.0f, 2.0f, 2.0f);
			MappingTransform.x.w *= 2.0f;
			MappingTransform.y.w *= 2.0f;
			MappingTransform.z.w *= 2.0f;

			if (render_texture.Fields.TryGetValue("remap-textures", out bool remap_textures))
				RemapTextures = remap_textures;
		}

		protected override ccl.Transform GetChildTransform()
		{
			return RemapTextures ? new ccl.Transform(MappingTransform) : new ccl.Transform(InputTransform);
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var node = new CheckerTexture2dProceduralNode();
			shader.AddNode(node);

			node.UvwTransform = new ccl.Transform(MappingTransform);

			// Recursive call
			ConnectChildNodes(shader, uvw_output, node.ins.Color1, node.ins.Color2);

			ConnectInputOutputNodes(uvw_output, parent_color_input, node.outs.Color, node.ins.UVW);

			return node;
		}

		public bool RemapTextures { get; set; } = true;
	}

	public class NoiseTextureProcedural : TwoColorProcedural
	{
		public NoiseTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
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

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var node = new NoiseTextureProceduralNode();
			shader.AddNode(node);

			node.UvwTransform = new ccl.Transform(MappingTransform);
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
			ConnectChildNodes(shader, uvw_output, node.ins.Color1, node.ins.Color2);

			ConnectInputOutputNodes(uvw_output, parent_color_input, node.outs.Color, node.ins.UVW);

			return node;
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
		public WavesTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
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

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var waves_node = new WavesTextureProceduralNode();
			shader.AddNode(waves_node);

			waves_node.UvwTransform = new ccl.Transform(MappingTransform);
			waves_node.WaveType = WaveType;
			waves_node.WaveWidth = WaveWidth;
			waves_node.WaveWidthTextureOn = WaveWidthTextureOn;
			waves_node.Contrast1 = Contrast1;
			waves_node.Contrast2 = Contrast2;

			var waves_width_node = new WavesWidthTextureProceduralNode();
			shader.AddNode(waves_width_node);

			waves_width_node.UvwTransform = new ccl.Transform(MappingTransform);
			waves_width_node.WaveType = WaveType;

			uvw_output.Connect(waves_width_node.ins.UVW);

			// Recursive call
			ConnectChildNodes(shader, uvw_output, waves_node.ins.Color1, waves_node.ins.Color2);

			if (WaveWidthChild != null)
			{
				WaveWidthChild.CreateAndConnectProceduralNode(shader, waves_width_node.outs.UVW, waves_node.ins.Color3); // Recursive call
			}

			ConnectInputOutputNodes(uvw_output, parent_color_input, waves_node.outs.Color, waves_node.ins.UVW);

			return waves_node;
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
		public PerturbingTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("amount", out double amount))
				Amount = (float)amount;
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var perturbing_part1_node = new PerturbingPart1TextureProceduralNode();
			shader.AddNode(perturbing_part1_node);

			perturbing_part1_node.UvwTransform = new ccl.Transform(MappingTransform);

			uvw_output.Connect(perturbing_part1_node.ins.UVW);

			var perturbing_part2_node = new PerturbingPart2TextureProceduralNode();
			shader.AddNode(perturbing_part2_node);

			perturbing_part2_node.Amount = Amount;

			perturbing_part1_node.outs.UVW1.Connect(perturbing_part2_node.ins.UVW);

			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW1, perturbing_part2_node.ins.Color1);
			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW2, perturbing_part2_node.ins.Color2);
			PerturbChild?.CreateAndConnectProceduralNode(shader, perturbing_part1_node.outs.UVW3, perturbing_part2_node.ins.Color3);

			var output_node = SourceChild?.CreateAndConnectProceduralNode(shader, perturbing_part2_node.outs.PerturbedUVW, parent_color_input);

			return output_node;
		}

		public float Amount { get; set; } = 0.1f;
		public Procedural SourceChild { get; set; } = null;
		public Procedural PerturbChild { get; set; } = null;
	}

	public class WoodTextureProcedural : TwoColorProcedural
	{
		public WoodTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
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

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			NoiseTextureProceduralNode noise1 = new NoiseTextureProceduralNode();
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
			noise1.UvwTransform *= ccl.Transform.Scale(1.0f, 1.0f, AxialNoise);

			NoiseTextureProceduralNode noise2 = new NoiseTextureProceduralNode();
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
			noise2.UvwTransform *= ccl.Transform.Scale(1.0f, 1.0f, AxialNoise);

			NoiseTextureProceduralNode noise3 = new NoiseTextureProceduralNode();
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
			noise3.UvwTransform *= ccl.Transform.Scale(1.0f, 1.0f, AxialNoise);

			shader.AddNode(noise1);
			shader.AddNode(noise2);
			shader.AddNode(noise3);

			WavesTextureProceduralNode waves = new WavesTextureProceduralNode();
			waves.WaveType = WavesTextureProceduralNode.WaveTypes.RADIAL;
			waves.WaveWidth = GrainThickness;
			waves.Contrast1 = 1.0f - Blur1;
			waves.Contrast2 = 1.0f - Blur2;
			waves.WaveWidthTextureOn = false;
			waves.ins.Color1.Value = Color1.ToFloat4();
			//waves.TextureAmount1 = TextureAmount1; // TODO
			waves.ins.Color2.Value = Color2.ToFloat4();
			//waves.TextureAmount2 = TextureAmount2; // TODO

			Child1?.CreateAndConnectProceduralNode(shader, uvw_output, waves.ins.Color1);
			Child2?.CreateAndConnectProceduralNode(shader, uvw_output, waves.ins.Color2);

			shader.AddNode(waves);

			PerturbingPart1TextureProceduralNode perturbing1 = new PerturbingPart1TextureProceduralNode();
			perturbing1.UvwTransform = new ccl.Transform(MappingTransform);
			//perturbing1.Repeat = Repeat; // TODO? Or is this being handled in the line above?
			//perturbing1.Offset = Offset; // TODO?
			//perturbing1.Rotation = Rotation; // TODO?

			PerturbingPart2TextureProceduralNode perturbing2 = new PerturbingPart2TextureProceduralNode();
			perturbing2.Amount = RadialNoise;

			shader.AddNode(perturbing1);
			shader.AddNode(perturbing2);

			uvw_output.Connect(perturbing1.ins.UVW);
			perturbing1.outs.UVW1.Connect(noise1.ins.UVW);
			perturbing1.outs.UVW2.Connect(noise2.ins.UVW);
			perturbing1.outs.UVW3.Connect(noise3.ins.UVW);

			perturbing1.outs.UVW1.Connect(perturbing2.ins.UVW);
			noise1.outs.Color.Connect(perturbing2.ins.Color1);
			noise2.outs.Color.Connect(perturbing2.ins.Color2);
			noise3.outs.Color.Connect(perturbing2.ins.Color3);

			perturbing2.outs.PerturbedUVW.Connect(waves.ins.UVW);
			waves.outs.Color.Connect(parent_color_input);

			return waves;
		}

		public float GrainThickness { get; set; } = 0.0f;
		public float RadialNoise { get; set; } = 0.0f;
		public float AxialNoise { get; set; } = 0.0f;
		public float Blur1 { get; set; } = 0.0f;
		public float Blur2 { get; set; } = 0.0f;
	}

	public class BitmapTextureProcedural : Procedural
	{
		public BitmapTextureProcedural(RenderTexture render_texture, ccl.Transform transform, CyclesTextureImage cycles_texture, BitmapConverter bitmap_converter) : base(render_texture, transform)
		{
			CyclesTexture = cycles_texture;
			BitmapConverter = bitmap_converter;
			Utilities.HandleRenderTexture(render_texture, cycles_texture, false, bitmap_converter, 1.0f);

			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("filter", out bool filter))
				Filter = filter;

			if (rtf.TryGetValue("mirror-alternate-tiles", out bool alternate_tiles))
				AlternateTiles = alternate_tiles;

			if (rtf.TryGetValue("use-alpha-channel", out bool use_alpha))
				UseAlpha = use_alpha;
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var transform_node = new MatrixMathNode();
			shader.AddNode(transform_node);

			transform_node.Operation = MatrixMathNode.Operations.Point;
			transform_node.Transform = new ccl.Transform(MappingTransform);

			var image_texture_node = new ImageTextureNode();
			shader.AddNode(image_texture_node);

			// This might be pointless, because we don't support setting projection modes on child textures.
			// If this bitmap texture is not a child texture, then it is not a procedural, so this code won't be executed.
			//RenderEngine.SetProjectionModeSimple(CyclesTexture, transform_node.ins.Vector, texture_coordinate_node);

			if (CyclesTexture.HasTextureImage)
			{
				if (CyclesTexture.HasByteImage)
				{
					image_texture_node.ByteImagePtr = CyclesTexture.TexByte.Array();
				}
				else if (CyclesTexture.HasFloatImage)
				{
					image_texture_node.FloatImagePtr = CyclesTexture.TexFloat.Array();
				}
				image_texture_node.Filename = CyclesTexture.Name;
				image_texture_node.Width = (uint)CyclesTexture.TexWidth;
				image_texture_node.Height = (uint)CyclesTexture.TexHeight;
			}

			image_texture_node.UseAlpha = UseAlpha;
			image_texture_node.AlternateTiles = AlternateTiles;
			image_texture_node.Interpolation = Filter ? InterpolationType.Cubic : InterpolationType.Closest;

			uvw_output.Connect(transform_node.ins.Vector);
			transform_node.outs.Vector.Connect(image_texture_node.ins.Vector);
			image_texture_node.outs.Color.Connect(parent_color_input);

			return image_texture_node;
		}

		public CyclesTextureImage CyclesTexture { get; set; } = null;
		public BitmapConverter BitmapConverter { get; set; } = null;
		public bool UseAlpha { get; set; } = true;
		public bool AlternateTiles { get; set; } = false;
		public bool Filter { get; set; } = true;
	}

	public class AddTextureProcedural : TwoColorProcedural
	{
		public AddTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
		{
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var mix_node = new MixNode();
			shader.AddNode(mix_node);

			mix_node.BlendType = MixNode.BlendTypes.Add;
			mix_node.ins.Fac.Value = 1.0f;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, mix_node.ins.Color1, mix_node.ins.Color2);

			mix_node.outs.Color.Connect(parent_color_input);

			return mix_node;
		}
	}

	public class BlendTextureProcedural : TwoColorProcedural
	{
		public BlendTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
		{
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var mix_node = new MixNode();
			shader.AddNode(mix_node);

			mix_node.BlendType = MixNode.BlendTypes.Blend;
			mix_node.ins.Fac.Value = 0.5f;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, mix_node.ins.Color1, mix_node.ins.Color2);

			mix_node.outs.Color.Connect(parent_color_input);

			return mix_node;
		}
	}

	public class GradientTextureProcedural : TwoColorProcedural
	{
		public GradientTextureProcedural(RenderTexture render_texture, ccl.Transform transform) : base(render_texture, transform)
		{
			var rtf = render_texture.Fields;

			if (rtf.TryGetValue("gradient-type", out int gradient_type))
				GradientType = (GradientTextureProceduralNode.GradientTypes)gradient_type;

			if (rtf.TryGetValue("flip-alternate", out bool flip_alternate))
				FlipAlternate = flip_alternate;

			if (rtf.TryGetValue("custom-curve", out bool use_custom_curve))
				UseCustomCurve = use_custom_curve;

			//if (rtf.TryGetValue("point-width", out int point_width))
			//	PointWidth = point_width;

			//if (rtf.TryGetValue("point-height", out int point_height))
			//	PointHeight = point_height;
		}

		public override ShaderNode CreateAndConnectProceduralNode(Shader shader, VectorSocket uvw_output, ColorSocket parent_color_input)
		{
			var gradient_node = new GradientTextureProceduralNode();
			shader.AddNode(gradient_node);

			gradient_node.UvwTransform = new ccl.Transform(MappingTransform);
			gradient_node.GradientType = GradientType;
			gradient_node.FlipAlternate = FlipAlternate;
			gradient_node.UseCustomCurve = UseCustomCurve;
			gradient_node.PointWidth = PointWidth;
			gradient_node.PointHeight = PointHeight;

			// Recursive call
			ConnectChildNodes(shader, uvw_output, gradient_node.ins.Color1, gradient_node.ins.Color2);

			uvw_output.Connect(gradient_node.ins.UVW);
			gradient_node.outs.Color.Connect(parent_color_input);

			return gradient_node;
		}

		public GradientTextureProceduralNode.GradientTypes GradientType { get; set; }
		public bool FlipAlternate { get; set; }
		public bool UseCustomCurve { get; set; }
		public int PointWidth { get; set; }
		public int PointHeight { get; set; }
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
		public CyclesShader CreateCyclesShader(RenderMaterial rm, LinearWorkflow lw, uint mid, BitmapConverter bitmapConverter, List<CyclesDecal> decals)
		{
			var shader = new CyclesShader(mid, bitmapConverter)
			{
				Type = CyclesShader.Shader.Diffuse,
				Decals = decals,
			};

			if (rm.TypeId.Equals(realtimDisplaMaterialId))
			{
				if (rm.FindChild("front") is RenderMaterial front)
				{
					shader.CreateFrontShader(front, lw.PreProcessGamma);
				}
				if (rm.FindChild("back") is RenderMaterial back)
				{
					shader.CreateBackShader(back, lw.PreProcessGamma);
				}
				/* Now ensure we have a valid front part of the shader. When a
				 * double-sided material is added without having a front material
				 * set this can be necessary. */
				if (shader.Front == null)
				{
					using (RenderMaterial defrm = RenderMaterial.CreateBasicMaterial(null, null))
					{
						shader.CreateFrontShader(defrm, lw.PreProcessGamma);
					}
				}
			}
			else
			{
				shader.CreateFrontShader(rm, lw.PreProcessGamma);
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
