/**
Copyright 2014-2017 Robert McNeel and Associates

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
using Rhino.Display;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoBackground : RhinoShader
	{

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing) : this(client, intermediate, existing, "background")
		{
		}

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing, string name) : base(client, intermediate, name, existing)
		{
		}

		public override Shader GetShader()
		{
			if (!string.IsNullOrEmpty(m_original_background.Xml))
			{
				var xml = m_original_background.Xml;
				Shader.ShaderFromXml(ref m_shader, xml);
			}
			else
			{
				var texcoord210 = new TextureCoordinateNode("texcoord");

				var bg_env_texture255 = new EnvironmentTextureNode("bg_env_texture");
					bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					bg_env_texture255.ColorSpace = TextureNode.TextureColorSpace.None;
					bg_env_texture255.Extension = TextureNode.TextureExtension.Repeat;
					bg_env_texture255.Interpolation = InterpolationType.Linear;
					bg_env_texture255.IsLinear = false;

				var bg_color_or_texture259 = new MixNode("bg_color_or_texture");
					bg_color_or_texture259.ins.Color1.Value = m_original_background.Color1AsFloat4;
					bg_color_or_texture259.ins.Fac.Value = m_original_background.HasBgEnvTextureAsFloat;
					bg_color_or_texture259.BlendType = MixNode.BlendTypes.Mix;
					bg_color_or_texture259.UseClamp = false;

				var separate_bg_color265 = new SeparateRgbNode("separate_bg_color");

				var factor_r262 = new MathMultiply("factor_r");
					factor_r262.ins.Value2.Value = m_original_background.BgStrength;
					factor_r262.Operation = MathNode.Operations.Multiply;
					factor_r262.UseClamp = false;

				var factor_g263 = new MathMultiply("factor_g");
					factor_g263.ins.Value2.Value = m_original_background.BgStrength;
					factor_g263.Operation = MathNode.Operations.Multiply;
					factor_g263.UseClamp = false;

				var factor_b264 = new MathMultiply("factor_b");
					factor_b264.ins.Value2.Value = m_original_background.BgStrength;
					factor_b264.Operation = MathNode.Operations.Multiply;
					factor_b264.UseClamp = false;

				var gradienttexture281 = new GradientTextureNode("gradienttexture");

				var factored_bg_color266 = new CombineRgbNode("factored_bg_color");

				var gradient_colorramp282 = new ColorRampNode("gradient_colorramp");
					gradient_colorramp282.ColorBand.Stops.Add(new ColorStop() {Color=new ccl.float4(0.9411765f, 0.5803922f, 0.07843138f, 1f), Position=0f});
					gradient_colorramp282.ColorBand.Stops.Add(new ColorStop() {Color=new ccl.float4(0.5019608f, 0f, 0f, 1f), Position=1f});

				var refl_env_texture256 = new EnvironmentTextureNode("refl_env_texture");
					refl_env_texture256.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					refl_env_texture256.ColorSpace = TextureNode.TextureColorSpace.None;
					refl_env_texture256.Extension = TextureNode.TextureExtension.Repeat;
					refl_env_texture256.Interpolation = InterpolationType.Linear;
					refl_env_texture256.IsLinear = false;

				var refl_color_or_texture260 = new MixNode("refl_color_or_texture");
					refl_color_or_texture260.ins.Color1.Value = m_original_background.ReflectionColorAs4float;
					refl_color_or_texture260.ins.Fac.Value = m_original_background.HasReflEnvTextureAsFloat;
					refl_color_or_texture260.BlendType = MixNode.BlendTypes.Mix;
					refl_color_or_texture260.UseClamp = false;

				var separate_refl_color270 = new SeparateRgbNode("separate_refl_color");

				var factor_refl_r267 = new MathMultiply("factor_refl_r");
					factor_refl_r267.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_r267.Operation = MathNode.Operations.Multiply;
					factor_refl_r267.UseClamp = false;

				var factor_refl_g268 = new MathMultiply("factor_refl_g");
					factor_refl_g268.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_g268.Operation = MathNode.Operations.Multiply;
					factor_refl_g268.UseClamp = false;

				var factor_refl_b269 = new MathMultiply("factor_refl_b");
					factor_refl_b269.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_b269.Operation = MathNode.Operations.Multiply;
					factor_refl_b269.UseClamp = false;

				var light_path235 = new LightPathNode("light_path");

				var use_reflect_refract_when_glossy_and_reflection285 = new MathMultiply("use_reflect_refract_when_glossy_and_reflection");
					use_reflect_refract_when_glossy_and_reflection285.Operation = MathNode.Operations.Multiply;
					use_reflect_refract_when_glossy_and_reflection285.UseClamp = false;

				var gradient_or_other283 = new MixNode("gradient_or_other");
					gradient_or_other283.ins.Fac.Value = m_original_background.UseGradientAsFloat;
					gradient_or_other283.BlendType = MixNode.BlendTypes.Mix;
					gradient_or_other283.UseClamp = false;

				var factored_refl_color271 = new CombineRgbNode("factored_refl_color");

				var refl_env_when_enabled286 = new MathMultiply("refl_env_when_enabled");
					refl_env_when_enabled286.ins.Value1.Value = m_original_background.UseCustomReflectionEnvironmentAsFloat;
					refl_env_when_enabled286.Operation = MathNode.Operations.Multiply;
					refl_env_when_enabled286.UseClamp = false;

				var skycolor_or_final_bg284 = new MixNode("skycolor_or_final_bg");
					skycolor_or_final_bg284.ins.Color2.Value = m_original_background.SkyColorAs4float;
					skycolor_or_final_bg284.ins.Fac.Value = m_original_background.UseSkyColorAsFloat;
					skycolor_or_final_bg284.BlendType = MixNode.BlendTypes.Mix;
					skycolor_or_final_bg284.UseClamp = false;

				var sky_env_texture257 = new EnvironmentTextureNode("sky_env_texture");
					sky_env_texture257.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					sky_env_texture257.ColorSpace = TextureNode.TextureColorSpace.None;
					sky_env_texture257.Extension = TextureNode.TextureExtension.Repeat;
					sky_env_texture257.Interpolation = InterpolationType.Linear;
					sky_env_texture257.IsLinear = false;

				var sky_color_or_texture258 = new MixNode("sky_color_or_texture");
					sky_color_or_texture258.ins.Fac.Value = m_original_background.HasSkyEnvTextureAsFloat;
					sky_color_or_texture258.BlendType = MixNode.BlendTypes.Mix;
					sky_color_or_texture258.UseClamp = false;

				var separate_sky_color275 = new SeparateRgbNode("separate_sky_color");

				var sky_or_not261 = new MathMultiply("sky_or_not");
					sky_or_not261.ins.Value1.Value = m_original_background.SkyStrength;
					sky_or_not261.ins.Value2.Value = m_original_background.SkylightEnabledAsFloat;
					sky_or_not261.Operation = MathNode.Operations.Multiply;
					sky_or_not261.UseClamp = false;

				var factor_sky_r272 = new MathMultiply("factor_sky_r");
					factor_sky_r272.Operation = MathNode.Operations.Multiply;
					factor_sky_r272.UseClamp = false;

				var factor_sky_g273 = new MathMultiply("factor_sky_g");
					factor_sky_g273.Operation = MathNode.Operations.Multiply;
					factor_sky_g273.UseClamp = false;

				var factor_sky_b274 = new MathMultiply("factor_sky_b");
					factor_sky_b274.Operation = MathNode.Operations.Multiply;
					factor_sky_b274.UseClamp = false;

				var factored_sky_color276 = new CombineRgbNode("factored_sky_color");

				var non_camera_rays290 = new MathSubtract("non_camera_rays");
					non_camera_rays290.ins.Value1.Value = 1f;
					non_camera_rays290.Operation = MathNode.Operations.Subtract;
					non_camera_rays290.UseClamp = false;

				var refl_bg_or_custom_env291 = new MixNode("refl_bg_or_custom_env");
					refl_bg_or_custom_env291.BlendType = MixNode.BlendTypes.Mix;
					refl_bg_or_custom_env291.UseClamp = false;

				var light_with_bg_or_sky289 = new MixNode("light_with_bg_or_sky");
					light_with_bg_or_sky289.BlendType = MixNode.BlendTypes.Mix;
					light_with_bg_or_sky289.UseClamp = false;

				var invert_refl_switch292 = new MathSubtract("invert_refl_switch");
					invert_refl_switch292.ins.Value1.Value = 1f;
					invert_refl_switch292.Operation = MathNode.Operations.Subtract;
					invert_refl_switch292.UseClamp = false;

				var mix295 = new MixNode("mix");
					mix295.BlendType = MixNode.BlendTypes.Mix;
					mix295.UseClamp = false;

				var final_bg280 = new BackgroundNode("final_bg");
					final_bg280.ins.Strength.Value = 1f;
				

				m_shader.AddNode(texcoord210);
				m_shader.AddNode(bg_env_texture255);
				m_shader.AddNode(bg_color_or_texture259);
				m_shader.AddNode(separate_bg_color265);
				m_shader.AddNode(factor_r262);
				m_shader.AddNode(factor_g263);
				m_shader.AddNode(factor_b264);
				m_shader.AddNode(gradienttexture281);
				m_shader.AddNode(factored_bg_color266);
				m_shader.AddNode(gradient_colorramp282);
				m_shader.AddNode(refl_env_texture256);
				m_shader.AddNode(refl_color_or_texture260);
				m_shader.AddNode(separate_refl_color270);
				m_shader.AddNode(factor_refl_r267);
				m_shader.AddNode(factor_refl_g268);
				m_shader.AddNode(factor_refl_b269);
				m_shader.AddNode(light_path235);
				m_shader.AddNode(use_reflect_refract_when_glossy_and_reflection285);
				m_shader.AddNode(gradient_or_other283);
				m_shader.AddNode(factored_refl_color271);
				m_shader.AddNode(refl_env_when_enabled286);
				m_shader.AddNode(skycolor_or_final_bg284);
				m_shader.AddNode(sky_env_texture257);
				m_shader.AddNode(sky_color_or_texture258);
				m_shader.AddNode(separate_sky_color275);
				m_shader.AddNode(sky_or_not261);
				m_shader.AddNode(factor_sky_r272);
				m_shader.AddNode(factor_sky_g273);
				m_shader.AddNode(factor_sky_b274);
				m_shader.AddNode(factored_sky_color276);
				m_shader.AddNode(non_camera_rays290);
				m_shader.AddNode(refl_bg_or_custom_env291);
				m_shader.AddNode(light_with_bg_or_sky289);
				m_shader.AddNode(invert_refl_switch292);
				m_shader.AddNode(mix295);
				m_shader.AddNode(final_bg280);
				

				texcoord210.outs.Generated.Connect(bg_env_texture255.ins.Vector);
				bg_env_texture255.outs.Color.Connect(bg_color_or_texture259.ins.Color2);
				bg_color_or_texture259.outs.Color.Connect(separate_bg_color265.ins.Image);
				separate_bg_color265.outs.R.Connect(factor_r262.ins.Value1);
				separate_bg_color265.outs.G.Connect(factor_g263.ins.Value1);
				separate_bg_color265.outs.B.Connect(factor_b264.ins.Value1);
				texcoord210.outs.Window.Connect(gradienttexture281.ins.Vector);
				factor_r262.outs.Value.Connect(factored_bg_color266.ins.R);
				factor_g263.outs.Value.Connect(factored_bg_color266.ins.G);
				factor_b264.outs.Value.Connect(factored_bg_color266.ins.B);
				gradienttexture281.outs.Fac.Connect(gradient_colorramp282.ins.Fac);
				texcoord210.outs.Generated.Connect(refl_env_texture256.ins.Vector);
				refl_env_texture256.outs.Color.Connect(refl_color_or_texture260.ins.Color2);
				refl_color_or_texture260.outs.Color.Connect(separate_refl_color270.ins.Image);
				separate_refl_color270.outs.R.Connect(factor_refl_r267.ins.Value1);
				separate_refl_color270.outs.G.Connect(factor_refl_g268.ins.Value1);
				separate_refl_color270.outs.B.Connect(factor_refl_b269.ins.Value1);
				light_path235.outs.IsGlossyRay.Connect(use_reflect_refract_when_glossy_and_reflection285.ins.Value1);
				light_path235.outs.IsReflectionRay.Connect(use_reflect_refract_when_glossy_and_reflection285.ins.Value2);
				factored_bg_color266.outs.Image.Connect(gradient_or_other283.ins.Color1);
				gradient_colorramp282.outs.Color.Connect(gradient_or_other283.ins.Color2);
				factor_refl_r267.outs.Value.Connect(factored_refl_color271.ins.R);
				factor_refl_g268.outs.Value.Connect(factored_refl_color271.ins.G);
				factor_refl_b269.outs.Value.Connect(factored_refl_color271.ins.B);
				use_reflect_refract_when_glossy_and_reflection285.outs.Value.Connect(refl_env_when_enabled286.ins.Value2);
				gradient_or_other283.outs.Color.Connect(skycolor_or_final_bg284.ins.Color1);
				texcoord210.outs.Generated.Connect(sky_env_texture257.ins.Vector);
				skycolor_or_final_bg284.outs.Color.Connect(sky_color_or_texture258.ins.Color1);
				sky_env_texture257.outs.Color.Connect(sky_color_or_texture258.ins.Color2);
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
				light_path235.outs.IsCameraRay.Connect(non_camera_rays290.ins.Value2);
				gradient_or_other283.outs.Color.Connect(refl_bg_or_custom_env291.ins.Color1);
				factored_refl_color271.outs.Image.Connect(refl_bg_or_custom_env291.ins.Color2);
				refl_env_when_enabled286.outs.Value.Connect(refl_bg_or_custom_env291.ins.Fac);
				gradient_or_other283.outs.Color.Connect(light_with_bg_or_sky289.ins.Color1);
				factored_sky_color276.outs.Image.Connect(light_with_bg_or_sky289.ins.Color2);
				non_camera_rays290.outs.Value.Connect(light_with_bg_or_sky289.ins.Fac);
				refl_env_when_enabled286.outs.Value.Connect(invert_refl_switch292.ins.Value2);
				refl_bg_or_custom_env291.outs.Color.Connect(mix295.ins.Color1);
				light_with_bg_or_sky289.outs.Color.Connect(mix295.ins.Color2);
				invert_refl_switch292.outs.Value.Connect(mix295.ins.Fac);
				mix295.outs.Color.Connect(final_bg280.ins.Color);

				// extra code

				gradient_colorramp282.ColorBand.Stops.Clear();
				// bottom color on 0.0f
				gradient_colorramp282.ColorBand.InsertColorStop(m_original_background.Color2AsFloat4, 0.0f);
				// top color on 1.0f
				gradient_colorramp282.ColorBand.InsertColorStop(m_original_background.Color1AsFloat4, 1.0f);

				// rotate the window vector
				gradienttexture281.Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796);

				if (m_original_background.BackgroundFill == BackgroundStyle.Environment && m_original_background.HasBgEnvTexture)
				{
					RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.BgTexture);
					switch(m_original_background.BgTexture.EnvProjectionMode)
					{
						case Rhino.Render.TextureEnvironmentMappingMode.Automatic:
						case Rhino.Render.TextureEnvironmentMappingMode.EnvironmentMap:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.EnvironmentMap;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.Box:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Box;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.LightProbe:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.LightProbe;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.Cube:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.CubeMap;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.HorizontalCrossCube:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.CubeMapHorizontal;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.VerticalCrossCube:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.CubeMapVertical;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.Hemispherical:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Hemispherical;
							break;
						case Rhino.Render.TextureEnvironmentMappingMode.Spherical:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Spherical;
							break;
						default:
							bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Wallpaper;
							break;
					}
					bg_env_texture255.Rotation = m_original_background.BgTexture.Transform.z;
				}
				if(m_original_background.BackgroundFill == BackgroundStyle.WallpaperImage && m_original_background.Wallpaper.HasTextureImage)
				{
					RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.Wallpaper);
					bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}
				if(m_original_background.HasReflEnvTexture)
				{
					RenderEngine.SetTextureImage(refl_env_texture256, m_original_background.ReflectionTexture);
					var reflrot = m_original_background.ReflectionTexture.Transform.z;
					reflrot.z += (float)	System.Math.PI;
					refl_env_texture256.Rotation = reflrot;
				}
				if(m_original_background.HasSkyEnvTexture)
				{
					RenderEngine.SetTextureImage(sky_env_texture257, m_original_background.SkyTexture);
					sky_env_texture257.Rotation = m_original_background.SkyTexture.Transform.z;
				}

				final_bg280.outs.Background.Connect(m_shader.Output.ins.Surface);
			}

			// phew, done.
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
