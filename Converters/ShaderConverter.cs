﻿/**
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
using System.Globalization;
using System.Linq;
using ccl;
using ccl.ShaderNodes;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using RhinoCyclesCore.Core;
using Light = Rhino.Render.ChangeQueue.Light;

namespace RhinoCyclesCore.Converters
{
	public abstract class Procedural
	{
		public ccl.Transform MappingTransform { get; set; } = ccl.Transform.Identity();
	}

	public abstract class TwoColorProcedural : Procedural
	{
		public Color4f Color1 { get; set; } = Color4f.Black;
		public Color4f Color2 { get; set; } = Color4f.White;
		public double Amount1 { get; set; } = 1.0;
		public double Amount2 { get; set; } = 1.0;
		public Procedural Child1 { get; set; } = null;
		public Procedural Child2 { get; set; } = null;
	}

	public class CheckerTexture2dProcedural : TwoColorProcedural
	{
		public bool RemapTextures { get; set; } = true;
	}

	public class NoiseTextureProcedural : TwoColorProcedural
	{
		public NoiseTextureProceduralNode.NoiseTypes NoiseType { get; set; } = NoiseTextureProceduralNode.NoiseTypes.PERLIN;
		public NoiseTextureProceduralNode.SpecSynthTypes SpecSynthType { get; set; } = NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
		public int OctaveCount { get; set; } = 0;
		public float FrequencyMultiplier { get; set; } = 1.0f;
		public float AmplitudeMultiplier { get; set; } = 1.0f;
		public float ClampMin { get; set; } = 0.0f;
		public float ClampMax { get; set; } = 1.0f;
		public bool ScaleToClamp { get; set; } = false;
		public bool Inverse { get; set; } = false;
		public float Gain { get; set; } = 1.0f;
	}

	public class ShaderConverter
	{

		private Guid realtimDisplaMaterialId = new Guid("e6cd1973-b739-496e-ab69-32957fa48492");

		protected Procedural RecurseIntoChild(RenderTexture render_texture, string child_name, ccl.Transform transform)
		{
			Procedural procedural = null;

			if (render_texture.ChildSlotOn(child_name) && render_texture.ChildSlotAmount(child_name) > 0.1)
			{
				var render_texture_child = (RenderTexture)render_texture.FindChild(child_name);

				// Recursive call
				procedural = GetProceduralData(render_texture_child, transform);
			}

			return procedural;
		}

		protected ccl.Transform ToCyclesTransform(Rhino.Geometry.Transform transform)
		{
			return new ccl.Transform(
				(float)transform[0, 0], (float)transform[0, 1], (float)transform[0, 2], (float)transform[0, 3],
				(float)transform[1, 0], (float)transform[1, 1], (float)transform[1, 2], (float)transform[1, 3],
				(float)transform[2, 0], (float)transform[2, 1], (float)transform[2, 2], (float)transform[2, 3]);
		}

		protected NoiseTextureProceduralNode.NoiseTypes StringToNoiseType(string enum_string)
		{
			switch(enum_string)
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

		protected NoiseTextureProceduralNode.SpecSynthTypes StringToSpecSynthType(string enum_string)
		{
			switch (enum_string)
			{
				case "fractalsum": return NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
				case "turbulence": return NoiseTextureProceduralNode.SpecSynthTypes.TURBULENCE;
				default: return NoiseTextureProceduralNode.SpecSynthTypes.FRACTAL_SUM;
			}
		}

		protected Procedural GetProceduralData(RenderTexture render_texture, ccl.Transform transform)
		{
			if (render_texture == null)
				return null;

			if (render_texture.TypeName.Equals("2D Checker Texture"))
			{
				var procedural = new CheckerTexture2dProcedural();

				procedural.MappingTransform = transform;
				procedural.MappingTransform *= ToCyclesTransform(render_texture.LocalMappingTransform);
				procedural.MappingTransform *= ccl.Transform.Scale(2.0f, 2.0f, 2.0f);

				if (render_texture.Fields.TryGetValue("color-one", out Color4f color1))
					procedural.Color1 = color1;

				if (render_texture.Fields.TryGetValue("color-two", out Color4f color2))
					procedural.Color2 = color2;

				if (render_texture.Fields.TryGetValue("texture-amount-one", out double texture_amount1))
					procedural.Amount1 = texture_amount1;

				if (render_texture.Fields.TryGetValue("texture-amount-two", out double texture_amount2))
					procedural.Amount2 = texture_amount2;

				if (render_texture.Fields.TryGetValue("remap-textures", out bool remap_textures))
					procedural.RemapTextures = remap_textures;

				var child_transform = procedural.RemapTextures ? procedural.MappingTransform : transform;
				procedural.Child1 = RecurseIntoChild(render_texture, "color-one", child_transform);
				procedural.Child2 = RecurseIntoChild(render_texture, "color-two", child_transform);

				if (render_texture.Fields.TryGetValue("swap-colors", out bool swap_colors) && swap_colors)
				{
					(procedural.Color1, procedural.Color2) = (procedural.Color2, procedural.Color1);
					(procedural.Amount1, procedural.Amount2) = (procedural.Amount2, procedural.Amount1);
					(procedural.Child1, procedural.Child2) = (procedural.Child2, procedural.Child1);
				}

				return procedural;
			}

			if (render_texture.TypeName.Equals("Noise Texture"))
			{
				var procedural = new NoiseTextureProcedural();

				procedural.MappingTransform = transform;
				procedural.MappingTransform *= ToCyclesTransform(render_texture.LocalMappingTransform);

				if (render_texture.Fields.TryGetValue("color-one", out Color4f color1))
					procedural.Color1 = color1;

				if (render_texture.Fields.TryGetValue("color-two", out Color4f color2))
					procedural.Color2 = color2;

				if (render_texture.Fields.TryGetValue("texture-amount-one", out double texture_amount1))
					procedural.Amount1 = texture_amount1;

				if (render_texture.Fields.TryGetValue("texture-amount-two", out double texture_amount2))
					procedural.Amount2 = texture_amount2;

				if (render_texture.Fields.TryGetValue("noise-type", out string noise_type))
					procedural.NoiseType = StringToNoiseType(noise_type);

				if (render_texture.Fields.TryGetValue("spectral-synthesis-type", out string spec_synth_type))
					procedural.SpecSynthType = StringToSpecSynthType(spec_synth_type);

				if (render_texture.Fields.TryGetValue("octave-count", out int octave_count))
					procedural.OctaveCount = octave_count;

				if (render_texture.Fields.TryGetValue("frequency-multiplier", out double frequency_multiplier))
					procedural.FrequencyMultiplier = (float)frequency_multiplier;

				if (render_texture.Fields.TryGetValue("amplitude-multiplier", out double amplitude_multiplier))
					procedural.AmplitudeMultiplier = (float)amplitude_multiplier;

				if (render_texture.Fields.TryGetValue("clamp-min", out double clamp_min))
					procedural.ClampMin = (float)clamp_min;

				if (render_texture.Fields.TryGetValue("clamp-max", out double clamp_max))
					procedural.ClampMax = (float)clamp_max;

				if (render_texture.Fields.TryGetValue("scale-to-clamp", out bool scale_to_clamp))
					procedural.ScaleToClamp = scale_to_clamp;

				if (render_texture.Fields.TryGetValue("inverse", out bool inverse))
					procedural.Inverse = inverse;

				if (render_texture.Fields.TryGetValue("gain", out double gain))
					procedural.Gain = (float)gain;

				procedural.Child1 = RecurseIntoChild(render_texture, "color-one", transform);
				procedural.Child2 = RecurseIntoChild(render_texture, "color-two", transform);

				if (render_texture.Fields.TryGetValue("swap-colors", out bool swap_colors) && swap_colors)
				{
					(procedural.Color1, procedural.Color2) = (procedural.Color2, procedural.Color1);
					(procedural.Amount1, procedural.Amount2) = (procedural.Amount2, procedural.Amount1);
					(procedural.Child1, procedural.Child2) = (procedural.Child2, procedural.Child1);
				}

				return procedural;
			}

			return null;
		}

		protected Dictionary<TextureType, Procedural> ProcessProcedurals(RenderMaterial rm)
		{
			var procedurals = new Dictionary<TextureType, Procedural>();

			foreach (var child_slot in Enum.GetValues(typeof(RenderMaterial.StandardChildSlots)).Cast<RenderMaterial.StandardChildSlots>())
			{
				if (child_slot == RenderMaterial.StandardChildSlots.None ||
					child_slot == RenderMaterial.StandardChildSlots.Environment)
					continue;

				var texture_type = ConvertChildSlotToTextureType(child_slot);

				if (procedurals.ContainsKey(texture_type))
					continue;

				RenderTexture render_texture = rm.GetTextureFromUsage(child_slot);

				if (render_texture != null && child_slot == RenderMaterial.StandardChildSlots.PbrBaseColor)
				{
					var procedural = GetProceduralData(render_texture, ccl.Transform.Identity());

					if(procedural != null)
						procedurals.Add(texture_type, procedural);
				}
			}

			return procedurals;
		}

		/// <summary>
		/// Create a CyclesShader based on given Material m
		/// </summary>
		/// <param name="rm">Material to convert to CyclesShader</param>
		/// <param name="lw">LinearWorkflow data for this shader (gamma)</param>
		/// <param name="decals">Decals to integrate into the shader</param>
		/// <returns>The CyclesShader</returns>
		public CyclesShader CreateCyclesShader(RenderMaterial rm, LinearWorkflow lw, uint mid, BitmapConverter bitmapConverter, List<CyclesDecal> decals)
		{
			var procedurals = ProcessProcedurals(rm);

			var shader = new CyclesShader(mid, bitmapConverter)
			{
				Type = CyclesShader.Shader.Diffuse,
				Decals = decals,
				Procedurals = procedurals,
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

		protected TextureType ConvertChildSlotToTextureType(RenderMaterial.StandardChildSlots child_slot)
		{
			switch (child_slot)
			{
				case RenderMaterial.StandardChildSlots.None:
					return TextureType.None;
				case RenderMaterial.StandardChildSlots.PbrBaseColor:
					return TextureType.PBR_BaseColor;
				case RenderMaterial.StandardChildSlots.PbrOpacity:
					return TextureType.Opacity;
				case RenderMaterial.StandardChildSlots.Bump:
					return TextureType.Bump;
				case RenderMaterial.StandardChildSlots.Environment:
					return TextureType.Emap;
				case RenderMaterial.StandardChildSlots.PbrSubsurface:
					return TextureType.PBR_Subsurface;
				case RenderMaterial.StandardChildSlots.PbrSubSurfaceScattering:
					return TextureType.PBR_SubsurfaceScattering;
				case RenderMaterial.StandardChildSlots.PbrSubsurfaceScatteringRadius:
					return TextureType.PBR_SubsurfaceScatteringRadius;
				case RenderMaterial.StandardChildSlots.PbrMetallic:
					return TextureType.PBR_Metallic;
				case RenderMaterial.StandardChildSlots.PbrSpecular:
					return TextureType.PBR_Specular;
				case RenderMaterial.StandardChildSlots.PbrSpecularTint:
					return TextureType.PBR_SpecularTint;
				case RenderMaterial.StandardChildSlots.PbrRoughness:
					return TextureType.PBR_Roughness;
				case RenderMaterial.StandardChildSlots.PbrAnisotropic:
					return TextureType.PBR_Anisotropic;
				case RenderMaterial.StandardChildSlots.PbrAnisotropicRotation:
					return TextureType.PBR_Anisotropic_Rotation;
				case RenderMaterial.StandardChildSlots.PbrSheen:
					return TextureType.PBR_Sheen;
				case RenderMaterial.StandardChildSlots.PbrSheenTint:
					return TextureType.PBR_SheenTint;
				case RenderMaterial.StandardChildSlots.PbrClearcoat:
					return TextureType.PBR_Clearcoat;
				case RenderMaterial.StandardChildSlots.PbrClearcoatRoughness:
					return TextureType.PBR_ClearcoatRoughness;
				case RenderMaterial.StandardChildSlots.PbrOpacityIor:
					return TextureType.PBR_OpacityIor;
				case RenderMaterial.StandardChildSlots.PbrOpacityRoughness:
					return TextureType.PBR_OpacityRoughness;
				case RenderMaterial.StandardChildSlots.PbrEmission:
					return TextureType.PBR_Emission;
				case RenderMaterial.StandardChildSlots.PbrAmbientOcclusion:
					return TextureType.PBR_AmbientOcclusion;
				case RenderMaterial.StandardChildSlots.PbrDisplacement:
					return TextureType.PBR_Displacement;
				case RenderMaterial.StandardChildSlots.PbrClearcoatBump:
					return TextureType.PBR_ClearcoatBump;
				case RenderMaterial.StandardChildSlots.PbrAlpha:
					return TextureType.PBR_Alpha;
				default:
					{
						System.Diagnostics.Debug.Assert(false);
						return TextureType.None;
					}
			}
		}
	}
}
