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
using System.Globalization;
using System.Linq;
using ccl;
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
	public class ShaderConverter
	{

		private Guid realtimDisplaMaterialId = new Guid("e6cd1973-b739-496e-ab69-32957fa48492");

		protected void RecurseIntoChild(RenderTexture render_texture, string child_name, TextureType texture_type, Dictionary<TextureType, Tuple<float4, float4>> procedurals)
		{
			if (render_texture.ChildSlotOn(child_name) && render_texture.ChildSlotAmount(child_name) > 0.1)
			{
				var render_texture_child = (RenderTexture)render_texture.FindChild(child_name);

				// Recursive call
				GetProceduralData(render_texture_child, texture_type, procedurals);
			}
		}

		protected void GetProceduralData(RenderTexture render_texture, TextureType texture_type, Dictionary<TextureType, Tuple<float4, float4>> procedurals)
		{
			if (render_texture.TypeName.Equals("2D Checker Texture"))
			{
				render_texture.Fields.TryGetValue<bool>("swap-colors", out bool swap_colors);

				var child_name1 = swap_colors ? "color-two" : "color-one";
				var child_name2 = swap_colors ? "color-one" : "color-two";

				if (!render_texture.Fields.TryGetValue<Color4f>(child_name1, out Color4f color1))
				{
					color1 = swap_colors ? Color4f.White : Color4f.Black;
				}

				if (!render_texture.Fields.TryGetValue<Color4f>(child_name2, out Color4f color2))
				{
					color2 = swap_colors ? Color4f.Black : Color4f.White;
				}

				var texture_amount_string1 = swap_colors ? "texture-amount-two" : "texture-amount-one";
				var texture_amount_string2 = swap_colors ? "texture-amount-one" : "texture-amount-two";
				if (!render_texture.Fields.TryGetValue<double>(texture_amount_string1, out double texture_amount1))
				{
					texture_amount1 = 1.0;
				}

				if (!render_texture.Fields.TryGetValue<double>(texture_amount_string2, out double texture_amount2))
				{
					texture_amount2 = 1.0;
				}

				RecurseIntoChild(render_texture, child_name1, texture_type, procedurals);
				RecurseIntoChild(render_texture, child_name2, texture_type, procedurals);

				procedurals.Add(texture_type, new Tuple<float4, float4>(color1.ToFloat4(), color2.ToFloat4()));
			}
		}

		protected Dictionary<TextureType, Tuple<float4, float4>> ProcessProcedurals(RenderMaterial rm)
		{
			var procedurals = new Dictionary<TextureType, Tuple<float4, float4>>();

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
					GetProceduralData(render_texture, texture_type, procedurals);
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
