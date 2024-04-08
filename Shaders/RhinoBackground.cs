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

using ccl;
using ccl.ShaderNodes;
using ccl.ShaderNodes.Sockets;
using Rhino.Display;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using System;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoBackground : RhinoShader
	{

		public RhinoBackground(Session client, CyclesBackground intermediate, Shader existing) : this(client, intermediate, existing, "background", true)
		{
		}

		public RhinoBackground(Session client, CyclesBackground intermediate, Shader existing, string name, bool recreate) : base(client, intermediate, name, existing, recreate)
		{
		}

		private bool _IsBitmapTextureProcedural(CyclesTextureImage img)
		{
			return img != null && img.HasProcedural && img.Procedural is BitmapTextureProcedural;
		}

		private VectorSocket _GetTexcoordSocket(CyclesTextureImage img, RhinoTextureCoordinateNode texco)
		{
			switch(img.EnvProjectionMode)
			{
				case Rhino.Render.TextureEnvironmentMappingMode.Hemispherical:
					return texco.outs.EnvHemispherical;
				case Rhino.Render.TextureEnvironmentMappingMode.Box:
					return texco.outs.EnvBox;
				case Rhino.Render.TextureEnvironmentMappingMode.Cube:
					return texco.outs.EnvCubemap;
				case Rhino.Render.TextureEnvironmentMappingMode.HorizontalCrossCube:
					return texco.outs.EnvCubemapHorizontalCross;
				case Rhino.Render.TextureEnvironmentMappingMode.VerticalCrossCube:
					return texco.outs.EnvCubemapVerticalCross;
				case Rhino.Render.TextureEnvironmentMappingMode.LightProbe:
					return texco.outs.EnvLightProbe;
				case Rhino.Render.TextureEnvironmentMappingMode.EnvironmentMap:
					return texco.outs.EnvEmap;
				case Rhino.Render.TextureEnvironmentMappingMode.Spherical:
				case Rhino.Render.TextureEnvironmentMappingMode.Automatic:
					return texco.outs.EnvSpherical;
				default: // non-existing planar environment projection, value 4
					return texco.outs.Window;

			}
		}


		public override Shader GetShader()
		{
			if(RcCore.It.AllSettings.DebugSimpleShaders)
			{
				var texco = new RhinoTextureCoordinateNode(m_shader, "texcoord");
				RhinoAzimuthAltitudeTransformNode bgAzimuthAltitudeTransformNode = new RhinoAzimuthAltitudeTransformNode(m_shader, "bgAzimuthAltitudeTransform");
				var bgenv = new EnvironmentTextureNode(m_shader, "bg_env_texture");
				var mixcol = new MixNode(m_shader, "color_mixer");
				var mixbgs = new MixClosureNode(m_shader, "mix_bgs");
				RenderEngine.SetTextureImage(bgenv, m_original_background.BgTexture);
				//_SetEnvironmentProjection(m_original_background.BgTexture, bgenv);
				bgenv.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				var bg = new BackgroundNode(m_shader, "debug_bg_node");
				var bg2 = new BackgroundNode(m_shader, "debug_bg2_node");
				var lp = new LightPathNode(m_shader, "lp");
				bg.ins.Color.Value = new float4(1.0f);
				bg.ins.Strength.Value = m_original_background.SkyStrength;
				bg2.ins.Color.Value = new float4(0.8f, 0.1f, 0.1f);
				bg2.ins.Strength.Value = 1.0f;

				bgAzimuthAltitudeTransformNode.Altitude = m_original_background.BgTexture.Transform.z.x;
				bgAzimuthAltitudeTransformNode.Azimuth = m_original_background.BgTexture.Transform.z.z;

				mixcol.ins.Color2.Value = new float4(0.1f, 0.8f, 0.1f);
				mixcol.ins.Fac.Value = 0.8f;

				mixbgs.ins.Fac.Value = 0.8f;

				/*

				texco.outs.Generated.Connect(bgAzimuthAltitudeTransformNode.ins.Vector);
				bgAzimuthAltitudeTransformNode.outs.Vector.Connect(bgenv.ins.Vector);

				bg.outs.Background.Connect(mixbgs.ins.Closure1);
				bg2.outs.Background.Connect(mixbgs.ins.Closure2);

				lp.outs.IsCameraRay.Connect(mixbgs.ins.Fac);

				bgenv.outs.Color.Connect(mixcol.ins.Color1);

				mixcol.outs.Color.Connect(bg.ins.Color);

				mixbgs.outs.Closure.Connect(m_shader.Output.ins.Surface);
				*/
				bg.ins.Strength.Value = 1.0f;
				bg.outs.Background.Connect(m_shader.Output.ins.Surface);
				m_shader.WriteDataToNodes();
				m_shader.Tag();
			}
			else if (!string.IsNullOrEmpty(m_original_background.Xml))
			{
				var xml = m_original_background.Xml;
				Shader.ShaderFromXml(m_shader, xml, true);
			}
			else
			{
				var texcoord210 = new RhinoTextureCoordinateNode(m_shader, "texcoord");

				RhinoAzimuthAltitudeTransformNode bgAzimuthAltitudeTransformNode = new RhinoAzimuthAltitudeTransformNode(m_shader, "bgAzimuthAltitudeTransform");
				RhinoAzimuthAltitudeTransformNode reflAzimuthAltitudeTransformNode = new RhinoAzimuthAltitudeTransformNode(m_shader, "reflAzimuthAltitudeTransform");
				RhinoAzimuthAltitudeTransformNode skyAzimuthAltitudeTransformNode = new RhinoAzimuthAltitudeTransformNode(m_shader, "skyAzimuthAltitudeTransform");

				var bg_env_texture255 = new EnvironmentTextureNode(m_shader, "bg_env_texture");
				bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				bg_env_texture255.ColorSpace = TextureNode.TextureColorSpace.None;
				bg_env_texture255.Extension = TextureNode.TextureExtension.Repeat;
				bg_env_texture255.Interpolation = InterpolationType.Linear;
				bg_env_texture255.IsLinear = false;

				var bg_color_or_texture259 = new MixNode(m_shader, "bg_color_or_texture");
				bg_color_or_texture259.ins.Color1.Value = m_original_background.Color1AsFloat4;
				bg_color_or_texture259.ins.Fac.Value = m_original_background.HasBgEnvTextureAsFloat;
				bg_color_or_texture259.BlendType = MixNode.BlendTypes.Blend;
				bg_color_or_texture259.UseClamp = false;

				var separate_bg_color265 = new SeparateRgbNode(m_shader, "separate_bg_color");

				var skylight_strength_factor299 = new MathMaximum(m_shader, "skylight_strength_factor");
				skylight_strength_factor299.ins.Value1.Value = m_original_background.BgStrength;
				skylight_strength_factor299.ins.Value2.Value = m_original_background.NonSkyEnvStrengthFactor;
				skylight_strength_factor299.Operation = MathNode.Operations.Maximum;
				skylight_strength_factor299.UseClamp = false;

				var factor_r262 = new MathMultiply(m_shader, "factor_r");
				factor_r262.Operation = MathNode.Operations.Multiply;
				factor_r262.UseClamp = false;

				var factor_g263 = new MathMultiply(m_shader, "factor_g");
				factor_g263.Operation = MathNode.Operations.Multiply;
				factor_g263.UseClamp = false;

				var factor_b264 = new MathMultiply(m_shader, "factor_b");
				factor_b264.Operation = MathNode.Operations.Multiply;
				factor_b264.UseClamp = false;

				var gradienttexture278 = new GradientTextureNode(m_shader, "gradienttexture");

				var factored_bg_color266 = new CombineRgbNode(m_shader, "factored_bg_color");

				var gradient_colorramp279 = new ColorRampNode(m_shader, "gradient_colorramp");
				gradient_colorramp279.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() { Color = new ccl.float4(0.9411765f, 0.5803922f, 0.07843138f, 1f), Position = 0f });
				gradient_colorramp279.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() { Color = new ccl.float4(0.5019608f, 0f, 0f, 1f), Position = 1f });

				var light_path235 = new LightPathNode(m_shader, "light_path");

				var maximum303 = new MathMaximum(m_shader, "maximum");
				maximum303.Operation = MathNode.Operations.Maximum;
				maximum303.UseClamp = true;

				var maximum305 = new MathMaximum(m_shader, "maximum");
				maximum305.Operation = MathNode.Operations.Maximum;
				maximum305.UseClamp = true;

				var gradient_or_other280 = new MixNode(m_shader, "gradient_or_other");
				gradient_or_other280.ins.Fac.Value = m_original_background.UseGradientAsFloat;
				gradient_or_other280.BlendType = MixNode.BlendTypes.Blend;
				gradient_or_other280.UseClamp = false;

				var maximum306 = new MathMaximum(m_shader, "maximum");
				maximum306.Operation = MathNode.Operations.Maximum;
				maximum306.UseClamp = true;

				var bg_no_customs301 = new BackgroundNode(m_shader, "bg_no_customs");

				var refl_env_texture256 = new EnvironmentTextureNode(m_shader, "refl_env_texture");
				refl_env_texture256.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				refl_env_texture256.ColorSpace = TextureNode.TextureColorSpace.None;
				refl_env_texture256.Extension = TextureNode.TextureExtension.Repeat;
				refl_env_texture256.Interpolation = InterpolationType.Linear;
				refl_env_texture256.IsLinear = false;

				var refl_color_or_texture260 = new MixNode(m_shader, "refl_color_or_texture");
				refl_color_or_texture260.ins.Color1.Value = m_original_background.ReflectionColorAs4float;
				refl_color_or_texture260.ins.Fac.Value = m_original_background.HasReflEnvTextureAsFloat;
				refl_color_or_texture260.BlendType = MixNode.BlendTypes.Blend;
				refl_color_or_texture260.UseClamp = false;

				var separate_refl_color270 = new SeparateRgbNode(m_shader, "separate_refl_color");

				var skylight_strength_factor300 = new MathMultiply(m_shader, "skylight_strength_factor");
				skylight_strength_factor300.ins.Value1.Value = m_original_background.ReflStrength;
				skylight_strength_factor300.ins.Value2.Value = m_original_background.NonSkyEnvStrengthFactor;
				skylight_strength_factor300.Operation = MathNode.Operations.Multiply;
				skylight_strength_factor300.UseClamp = false;

				var factor_refl_r267 = new MathMultiply(m_shader, "factor_refl_r");
				factor_refl_r267.Operation = MathNode.Operations.Multiply;
				factor_refl_r267.UseClamp = false;

				var factor_refl_g268 = new MathMultiply(m_shader, "factor_refl_g");
				factor_refl_g268.Operation = MathNode.Operations.Multiply;
				factor_refl_g268.UseClamp = false;

				var factor_refl_b269 = new MathMultiply(m_shader, "factor_refl_b");
				factor_refl_b269.Operation = MathNode.Operations.Multiply;
				factor_refl_b269.UseClamp = false;

				var use_reflect_refract_when_glossy_and_reflection282 = new MathMultiply(m_shader, "use_reflect_refract_when_glossy_and_reflection");
				use_reflect_refract_when_glossy_and_reflection282.Operation = MathNode.Operations.Multiply;
				use_reflect_refract_when_glossy_and_reflection282.UseClamp = false;

				var factored_refl_color271 = new CombineRgbNode(m_shader, "factored_refl_color");

				var refl_env_when_enabled283 = new MathMultiply(m_shader, "refl_env_when_enabled");
				refl_env_when_enabled283.ins.Value1.Value = m_original_background.UseCustomReflectionEnvironmentAsFloat;
				refl_env_when_enabled283.Operation = MathNode.Operations.Multiply;
				refl_env_when_enabled283.UseClamp = false;

				var skycolor_or_final_bg281 = new MixNode(m_shader, "skycolor_or_final_bg");
				skycolor_or_final_bg281.ins.Color2.Value = m_original_background.SkyColorAs4float;
				skycolor_or_final_bg281.ins.Fac.Value = m_original_background.UseSkyColorAsFloat;
				skycolor_or_final_bg281.BlendType = MixNode.BlendTypes.Blend;
				skycolor_or_final_bg281.UseClamp = false;

				var sky_env_texture257 = new EnvironmentTextureNode(m_shader, "sky_env_texture");
				sky_env_texture257.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				sky_env_texture257.ColorSpace = TextureNode.TextureColorSpace.None;
				sky_env_texture257.Extension = TextureNode.TextureExtension.Repeat;
				sky_env_texture257.Interpolation = InterpolationType.Linear;
				sky_env_texture257.IsLinear = false;

				var sky_color_or_texture258 = new MixNode(m_shader, "sky_color_or_texture");
				sky_color_or_texture258.ins.Fac.Value = m_original_background.HasSkyEnvTextureAsFloat;
				sky_color_or_texture258.BlendType = MixNode.BlendTypes.Blend;
				sky_color_or_texture258.UseClamp = false;

				var separate_sky_color275 = new SeparateRgbNode(m_shader, "separate_sky_color");

				var sky_or_not261 = new MathMultiply(m_shader, "sky_or_not");
				sky_or_not261.ins.Value1.Value = m_original_background.SkyStrength;
				sky_or_not261.ins.Value2.Value = m_original_background.SkylightEnabledAsFloat;
				sky_or_not261.Operation = MathNode.Operations.Multiply;
				sky_or_not261.UseClamp = false;

				var factor_sky_r272 = new MathMultiply(m_shader, "factor_sky_r");
				factor_sky_r272.Operation = MathNode.Operations.Multiply;
				factor_sky_r272.UseClamp = false;

				var factor_sky_g273 = new MathMultiply(m_shader, "factor_sky_g");
				factor_sky_g273.Operation = MathNode.Operations.Multiply;
				factor_sky_g273.UseClamp = false;

				var factor_sky_b274 = new MathMultiply(m_shader, "factor_sky_b");
				factor_sky_b274.Operation = MathNode.Operations.Multiply;
				factor_sky_b274.UseClamp = false;

				var factored_sky_color276 = new CombineRgbNode(m_shader, "factored_sky_color");

				var non_camera_rays287 = new MathSubtract(m_shader, "non_camera_rays");
				non_camera_rays287.ins.Value1.Value = 1f;
				non_camera_rays287.Operation = MathNode.Operations.Subtract;
				non_camera_rays287.UseClamp = false;

				var camera_and_transmission291 = new MathAdd(m_shader, "camera_and_transmission");
				camera_and_transmission291.Operation = MathNode.Operations.Add;
				camera_and_transmission291.UseClamp = false;

				var invert_refl_switch297 = new MathSubtract(m_shader, "invert_refl_switch");
				invert_refl_switch297.ins.Value1.Value = 1f;
				invert_refl_switch297.Operation = MathNode.Operations.Subtract;
				invert_refl_switch297.UseClamp = false;

				var invert_cam_and_transm289 = new MathSubtract(m_shader, "invert_cam_and_transm");
				invert_cam_and_transm289.ins.Value1.Value = 1f;
				invert_cam_and_transm289.Operation = MathNode.Operations.Subtract;
				invert_cam_and_transm289.UseClamp = false;

				var refl_bg_or_custom_env288 = new MixNode(m_shader, "refl_bg_or_custom_env");
				refl_bg_or_custom_env288.BlendType = MixNode.BlendTypes.Blend;
				refl_bg_or_custom_env288.UseClamp = false;

				var light_with_bg_or_sky286 = new MixNode(m_shader, "light_with_bg_or_sky");
				light_with_bg_or_sky286.BlendType = MixNode.BlendTypes.Blend;
				light_with_bg_or_sky286.UseClamp = false;

				var if_not_cam_nor_transm_nor_glossyrefl298 = new MathMultiply(m_shader, "if_not_cam_nor_transm_nor_glossyrefl");
				if_not_cam_nor_transm_nor_glossyrefl298.Operation = MathNode.Operations.Multiply;
				if_not_cam_nor_transm_nor_glossyrefl298.UseClamp = false;

				var mix292 = new MixNode(m_shader, "mix");
				mix292.BlendType = MixNode.BlendTypes.Blend;
				mix292.UseClamp = false;

				var final_bg277 = new BackgroundNode(m_shader, "final_bg");
				final_bg277.ins.Strength.Value = 1f;

				texcoord210.outs.Generated.Connect(bgAzimuthAltitudeTransformNode.ins.Vector);
				texcoord210.outs.Generated.Connect(reflAzimuthAltitudeTransformNode.ins.Vector);
				texcoord210.outs.Generated.Connect(skyAzimuthAltitudeTransformNode.ins.Vector);

				bg_color_or_texture259.outs.Color.Connect(separate_bg_color265.ins.Image);
				separate_bg_color265.outs.R.Connect(factor_r262.ins.Value1);
				skylight_strength_factor299.outs.Value.Connect(factor_r262.ins.Value2);
				separate_bg_color265.outs.G.Connect(factor_g263.ins.Value1);
				skylight_strength_factor299.outs.Value.Connect(factor_g263.ins.Value2);
				separate_bg_color265.outs.B.Connect(factor_b264.ins.Value1);
				skylight_strength_factor299.outs.Value.Connect(factor_b264.ins.Value2);
				texcoord210.outs.Window.Connect(gradienttexture278.ins.Vector);
				factor_r262.outs.Value.Connect(factored_bg_color266.ins.R);
				factor_g263.outs.Value.Connect(factored_bg_color266.ins.G);
				factor_b264.outs.Value.Connect(factored_bg_color266.ins.B);
				gradienttexture278.outs.Fac.Connect(gradient_colorramp279.ins.Fac);
				light_path235.outs.IsCameraRay.Connect(maximum303.ins.Value1);
				light_path235.outs.IsGlossyRay.Connect(maximum303.ins.Value2);
				maximum303.outs.Value.Connect(maximum305.ins.Value1);
				light_path235.outs.IsTransmissionRay.Connect(maximum305.ins.Value2);
				factored_bg_color266.outs.Image.Connect(gradient_or_other280.ins.Color1);
				gradient_colorramp279.outs.Color.Connect(gradient_or_other280.ins.Color2);
				maximum305.outs.Value.Connect(maximum306.ins.Value1);
				light_path235.outs.IsSingularRay.Connect(maximum306.ins.Value2);
				gradient_or_other280.outs.Color.Connect(bg_no_customs301.ins.Color);
				maximum306.outs.Value.Connect(bg_no_customs301.ins.Strength);
				refl_color_or_texture260.outs.Color.Connect(separate_refl_color270.ins.Image);
				separate_refl_color270.outs.R.Connect(factor_refl_r267.ins.Value1);
				skylight_strength_factor300.outs.Value.Connect(factor_refl_r267.ins.Value2);
				separate_refl_color270.outs.G.Connect(factor_refl_g268.ins.Value1);
				skylight_strength_factor300.outs.Value.Connect(factor_refl_g268.ins.Value2);
				separate_refl_color270.outs.B.Connect(factor_refl_b269.ins.Value1);
				skylight_strength_factor300.outs.Value.Connect(factor_refl_b269.ins.Value2);
				light_path235.outs.IsGlossyRay.Connect(use_reflect_refract_when_glossy_and_reflection282.ins.Value1);
				light_path235.outs.IsReflectionRay.Connect(use_reflect_refract_when_glossy_and_reflection282.ins.Value2);
				factor_refl_r267.outs.Value.Connect(factored_refl_color271.ins.R);
				factor_refl_g268.outs.Value.Connect(factored_refl_color271.ins.G);
				factor_refl_b269.outs.Value.Connect(factored_refl_color271.ins.B);
				use_reflect_refract_when_glossy_and_reflection282.outs.Value.Connect(refl_env_when_enabled283.ins.Value2);
				gradient_or_other280.outs.Color.Connect(skycolor_or_final_bg281.ins.Color1);
				skycolor_or_final_bg281.outs.Color.Connect(sky_color_or_texture258.ins.Color1);
				sky_color_or_texture258.outs.Color.Connect(separate_sky_color275.ins.Image);
				separate_sky_color275.outs.R.Connect(factor_sky_r272.ins.Value1);
				sky_or_not261.outs.Value.Connect(factor_sky_r272.ins.Value2);
				separate_sky_color275.outs.G.Connect(factor_sky_g273.ins.Value1);
				sky_or_not261.outs.Value.Connect(factor_sky_g273.ins.Value2);
				separate_sky_color275.outs.B.Connect(factor_sky_b274.ins.Value1);
				sky_or_not261.outs.Value.Connect(factor_sky_b274.ins.Value2);
				factor_sky_r272.outs.Value.Connect(factored_sky_color276.ins.R);
				factor_sky_g273.outs.Value.Connect(factored_sky_color276.ins.G);
				factor_sky_b274.outs.Value.Connect(factored_sky_color276.ins.B);
				light_path235.outs.IsCameraRay.Connect(non_camera_rays287.ins.Value2);
				light_path235.outs.IsCameraRay.Connect(camera_and_transmission291.ins.Value1);
				light_path235.outs.IsTransmissionRay.Connect(camera_and_transmission291.ins.Value2);
				refl_env_when_enabled283.outs.Value.Connect(invert_refl_switch297.ins.Value2);
				camera_and_transmission291.outs.Value.Connect(invert_cam_and_transm289.ins.Value2);
				gradient_or_other280.outs.Color.Connect(refl_bg_or_custom_env288.ins.Color1);
				factored_refl_color271.outs.Image.Connect(refl_bg_or_custom_env288.ins.Color2);
				refl_env_when_enabled283.outs.Value.Connect(refl_bg_or_custom_env288.ins.Fac);
				gradient_or_other280.outs.Color.Connect(light_with_bg_or_sky286.ins.Color1);
				factored_sky_color276.outs.Image.Connect(light_with_bg_or_sky286.ins.Color2);
				non_camera_rays287.outs.Value.Connect(light_with_bg_or_sky286.ins.Fac);
				invert_refl_switch297.outs.Value.Connect(if_not_cam_nor_transm_nor_glossyrefl298.ins.Value1);
				invert_cam_and_transm289.outs.Value.Connect(if_not_cam_nor_transm_nor_glossyrefl298.ins.Value2);
				refl_bg_or_custom_env288.outs.Color.Connect(mix292.ins.Color1);
				light_with_bg_or_sky286.outs.Color.Connect(mix292.ins.Color2);
				if_not_cam_nor_transm_nor_glossyrefl298.outs.Value.Connect(mix292.ins.Fac);
				mix292.outs.Color.Connect(final_bg277.ins.Color);

				// extra code

				gradient_colorramp279.ColorBand.Stops.Clear();
				// bottom color on 0.0f
				gradient_colorramp279.ColorBand.InsertColorStop(m_original_background.Color2AsFloat4, 1.0f);
				// top color on 1.0f
				gradient_colorramp279.ColorBand.InsertColorStop(m_original_background.Color1AsFloat4, 0.0f);

				// rotate the window vector
				gradienttexture278.Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796);

				if (m_original_background.BackgroundFill == BackgroundStyle.Environment && m_original_background.HasBgEnvTexture)
				{
					if(m_original_background.BgTexture.HasProcedural) {
						if (_IsBitmapTextureProcedural(m_original_background.BgTexture))
						{
							var envnode = m_original_background.BgTexture.Procedural.CreateAndConnectProceduralNode(m_shader, bgAzimuthAltitudeTransformNode.outs.Vector, bg_color_or_texture259.ins.Color2, parent_alpha_input: null, IsData: true) as EnvironmentTextureNode;
							_SetEnvironmentProjection(m_original_background.BgTexture, envnode);
							//bgAzimuthAltitudeTransformNode.outs.Vector.Connect(envnode.ins.Vector);
							bgAzimuthAltitudeTransformNode.Altitude = m_original_background.BgTexture.Transform.z.x;
							bgAzimuthAltitudeTransformNode.Azimuth = m_original_background.BgTexture.Transform.z.z;
						} else {
							var texcosocket = _GetTexcoordSocket(m_original_background.BgTexture, texcoord210);
							m_original_background.BgTexture.Procedural.CreateAndConnectProceduralNode(m_shader, texcosocket, bg_color_or_texture259.ins.Color2, parent_alpha_input: null, IsData: false);
						}
					}
					else {
						bgAzimuthAltitudeTransformNode.outs.Vector.Connect(bg_env_texture255.ins.Vector);
						RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.BgTexture);
						bg_env_texture255.outs.Color.Connect(bg_color_or_texture259.ins.Color2);
						_SetEnvironmentProjection(m_original_background.BgTexture, bg_env_texture255);
						bgAzimuthAltitudeTransformNode.Altitude = m_original_background.BgTexture.Transform.z.x;
						bgAzimuthAltitudeTransformNode.Azimuth = m_original_background.BgTexture.Transform.z.z;
					}
				}
				else if (m_original_background.BackgroundFill == BackgroundStyle.WallpaperImage && m_original_background.Wallpaper.HasTextureImage)
				{
					RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.Wallpaper);
					bg_env_texture255.outs.Color.Connect(bg_color_or_texture259.ins.Color2);
					bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}
				if (m_original_background.HasReflEnvTexture)
				{
					if (m_original_background.ReflectionTexture.HasProcedural) {
						if (_IsBitmapTextureProcedural(m_original_background.ReflectionTexture))
						{
							var envnode = m_original_background.ReflectionTexture.Procedural.CreateAndConnectProceduralNode(m_shader, reflAzimuthAltitudeTransformNode.outs.Vector, refl_color_or_texture260.ins.Color2, parent_alpha_input: null, IsData: true) as EnvironmentTextureNode;
							_SetEnvironmentProjection(m_original_background.ReflectionTexture, envnode);
							//reflAzimuthAltitudeTransformNode.outs.Vector.Connect(envnode.ins.Vector);
							reflAzimuthAltitudeTransformNode.Altitude = m_original_background.ReflectionTexture.Transform.z.x;
							reflAzimuthAltitudeTransformNode.Azimuth = m_original_background.ReflectionTexture.Transform.z.z;
						} else {
							var texcosocket = _GetTexcoordSocket(m_original_background.ReflectionTexture, texcoord210);
							m_original_background.ReflectionTexture.Procedural.CreateAndConnectProceduralNode(m_shader, texcosocket, refl_color_or_texture260.ins.Color2, parent_alpha_input: null, IsData: false);
						}
					}
					else {
						RenderEngine.SetTextureImage(refl_env_texture256, m_original_background.ReflectionTexture);
						refl_env_texture256.outs.Color.Connect(refl_color_or_texture260.ins.Color2);
						_SetEnvironmentProjection(m_original_background.ReflectionTexture, refl_env_texture256);
						reflAzimuthAltitudeTransformNode.outs.Vector.Connect(refl_env_texture256.ins.Vector);
						reflAzimuthAltitudeTransformNode.Altitude = m_original_background.ReflectionTexture.Transform.z.x;
						reflAzimuthAltitudeTransformNode.Azimuth = m_original_background.ReflectionTexture.Transform.z.z;
					}
				}
				if (m_original_background.HasSkyEnvTexture)
				{
					if (m_original_background.SkyTexture.HasProcedural)
					{
						if (_IsBitmapTextureProcedural(m_original_background.SkyTexture))
						{
							var envnode = m_original_background.SkyTexture.Procedural.CreateAndConnectProceduralNode(m_shader, skyAzimuthAltitudeTransformNode.outs.Vector, sky_color_or_texture258.ins.Color2, parent_alpha_input: null, IsData: true);
							_SetEnvironmentProjection(m_original_background.SkyTexture, envnode as EnvironmentTextureNode);
							skyAzimuthAltitudeTransformNode.Altitude = m_original_background.SkyTexture.Transform.z.x;
							skyAzimuthAltitudeTransformNode.Azimuth = m_original_background.SkyTexture.Transform.z.z;
						}
						else
						{
							var texcosocket = _GetTexcoordSocket(m_original_background.SkyTexture, texcoord210);
							m_original_background.SkyTexture.Procedural.CreateAndConnectProceduralNode(m_shader, texcosocket, sky_color_or_texture258.ins.Color2, parent_alpha_input: null, IsData: false);
						}
					}
					else
					{
						RenderEngine.SetTextureImage(sky_env_texture257, m_original_background.SkyTexture);
						sky_env_texture257.outs.Color.Connect(sky_color_or_texture258.ins.Color2);
						_SetEnvironmentProjection(m_original_background.SkyTexture, sky_env_texture257);
						skyAzimuthAltitudeTransformNode.outs.Vector.Connect(sky_env_texture257.ins.Vector);
						skyAzimuthAltitudeTransformNode.Altitude = m_original_background.SkyTexture.Transform.z.x;
						skyAzimuthAltitudeTransformNode.Azimuth = m_original_background.SkyTexture.Transform.z.z;
					}
				}

				if (m_original_background.NoCustomsWithSkylightEnabled)
				{
					bg_no_customs301.ins.Strength.ClearConnections();
					bg_no_customs301.ins.Strength.Value = 1.0f;
					bg_no_customs301.outs.Background.Connect(m_shader.Output.ins.Surface);
				}
				else
				if (m_original_background.NoCustomsWithSkylightDisabled)
				{
					bg_no_customs301.outs.Background.Connect(m_shader.Output.ins.Surface);
				}
				else
				{
					final_bg277.outs.Background.Connect(m_shader.Output.ins.Surface);
				}
			}

			// phew, done.
			m_shader.WriteDataToNodes();
			if (RcCore.It.AllSettings.DumpEnvironmentShaderGraph)
			{
				var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				var graph_path = System.IO.Path.Combine(home, $"rhinobg_{m_shader.Id}.dot");
				m_shader.DumpGraph(graph_path);
			}
			m_shader.Tag();

			return m_shader;
		}

		private void _SetEnvironmentProjection(CyclesTextureImage img, EnvironmentTextureNode envtexture)
		{
			switch (img.EnvProjectionMode)
			{
				case Rhino.Render.TextureEnvironmentMappingMode.Automatic:
				case Rhino.Render.TextureEnvironmentMappingMode.EnvironmentMap:
					envtexture.Projection = TextureNode.EnvironmentProjection.EnvironmentMap;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.Box:
					envtexture.Projection = TextureNode.EnvironmentProjection.Box;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.LightProbe:
					envtexture.Projection = TextureNode.EnvironmentProjection.LightProbe;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.Cube:
					envtexture.Projection = TextureNode.EnvironmentProjection.CubeMap;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.HorizontalCrossCube:
					envtexture.Projection = TextureNode.EnvironmentProjection.CubeMapHorizontal;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.VerticalCrossCube:
					envtexture.Projection = TextureNode.EnvironmentProjection.CubeMapVertical;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.Hemispherical:
					envtexture.Projection = TextureNode.EnvironmentProjection.Hemispherical;
					break;
				case Rhino.Render.TextureEnvironmentMappingMode.Spherical:
					envtexture.Projection = TextureNode.EnvironmentProjection.Spherical;
					break;
				default: // non-existing planar environment projection, value 4
					envtexture.Projection = TextureNode.EnvironmentProjection.Wallpaper;
					break;
			}
		}
	}
}
