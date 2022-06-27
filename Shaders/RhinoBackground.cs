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

using ccl;
using ccl.ShaderNodes;
using Rhino.Display;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoBackground : RhinoShader
	{

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing) : this(client, intermediate, existing, "background", true)
		{
		}

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing, string name, bool recreate) : base(client, intermediate, name, existing, recreate)
		{
		}

		public override Shader GetShader()
		{
			if(RcCore.It.AllSettings.DebugSimpleShaders)
			{
				var bg = new BackgroundNode();
				bg.ins.Color.Value = new float4(0.7f);
				bg.ins.Strength.Value = 1.0f;

				m_shader.AddNode(bg);

				bg.outs.Background.Connect(m_shader.Output.ins.Surface);
				//m_shader.FinalizeGraph();
				//m_shader.Tag();
			}
			else if (!string.IsNullOrEmpty(m_original_background.Xml))
			{
				var xml = m_original_background.Xml;
				Shader.ShaderFromXml(m_shader, xml, true);
			}
			else
			{
				TextureCoordinateNode texcoords = new TextureCoordinateNode("textureCoordinates");
				LightPathNode lightPath = new LightPathNode("lightPath");
				BackgroundNode background = new BackgroundNode("background");
				background.ins.Strength.Value = m_original_background.BgStrength;
				EnvironmentTextureNode backgroundTexture = new EnvironmentTextureNode("backgroundTexture");
				backgroundTexture.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				backgroundTexture.ColorSpace = TextureNode.TextureColorSpace.None;
				backgroundTexture.Extension = TextureNode.TextureExtension.Repeat;
				backgroundTexture.Interpolation = InterpolationType.Linear;
				backgroundTexture.IsLinear = false;
				MixNode backgroundColorOrTexture = new MixNode("backgroundColorOrTexture");
				backgroundColorOrTexture.ins.Color1.Value = m_original_background.Color1AsFloat4;
				backgroundColorOrTexture.ins.Fac.Value = m_original_background.HasBgEnvTextureAsFloat;
				backgroundColorOrTexture.BlendType = MixNode.BlendTypes.Blend;
				backgroundColorOrTexture.UseClamp = false;
				MixClosureNode backgroundSkylightAndReflection = new MixClosureNode("backgroundSkylightAndReflection");
				MathAdd cameraOrTransmission = new MathAdd("cameraOrTransmission");
				cameraOrTransmission.UseClamp = true;
				MathSubtract cameraTransmissionRayFirstOnly = new MathSubtract("cameraTransmissionRayFirstOnly");

				BackgroundNode reflection = new BackgroundNode("reflection");
				reflection.ins.Strength.Value = m_original_background.ReflStrength;
				EnvironmentTextureNode reflectionTexture = new EnvironmentTextureNode("reflectionTexture");
				reflectionTexture.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				reflectionTexture.ColorSpace = TextureNode.TextureColorSpace.None;
				reflectionTexture.Extension = TextureNode.TextureExtension.Repeat;
				reflectionTexture.Interpolation = InterpolationType.Linear;
				reflectionTexture.IsLinear = false;
				MixNode reflectionColorOrTexture = new MixNode("reflectionColorOrTexture");
				reflectionColorOrTexture.ins.Color1.Value = m_original_background.ReflectionColorAs4float;
				reflectionColorOrTexture.ins.Fac.Value = m_original_background.HasReflEnvTextureAsFloat;
				reflectionColorOrTexture.BlendType = MixNode.BlendTypes.Blend;
				reflectionColorOrTexture.UseClamp = false;

				BackgroundNode skylight = new BackgroundNode("skylight");
				skylight.ins.Strength.Value = m_original_background.SkyStrength;
				EnvironmentTextureNode skylightTexture = new EnvironmentTextureNode("skylightTexture");
				skylightTexture.Projection = TextureNode.EnvironmentProjection.Equirectangular;
				skylightTexture.ColorSpace = TextureNode.TextureColorSpace.None;
				skylightTexture.Extension = TextureNode.TextureExtension.Repeat;
				skylightTexture.Interpolation = InterpolationType.Linear;
				skylightTexture.IsLinear = false;
				MixNode skylightColorOrTexture = new MixNode("skylightColorOrTexture");
				skylightColorOrTexture.ins.Color1.Value = m_original_background.SkyColorAs4float;
				skylightColorOrTexture.ins.Fac.Value = m_original_background.HasSkyEnvTextureAsFloat;
				skylightColorOrTexture.BlendType = MixNode.BlendTypes.Blend;
				skylightColorOrTexture.UseClamp = false;

				MathSubtract glossyFirstOnly = new MathSubtract("glossyFirstOnly");
				MixClosureNode skylightAndReflection = new MixClosureNode("skylightAndReflection");

				ColorRampNode gradientColorRamp = new ColorRampNode("gradient_colorramp");
				gradientColorRamp.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() { Color = m_original_background.Color1AsFloat4, Position = 0f });
				gradientColorRamp.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() { Color = m_original_background.Color2AsFloat4, Position = 1f });
				// rotate the window vector
				GradientTextureNode gradientTexture = new GradientTextureNode("gradientTexture");
				gradientTexture.Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796);

				MixNode regularOrGradient = new MixNode("regularOrGradient");
				regularOrGradient.ins.Fac.Value = m_original_background.UseGradientAsFloat;
				regularOrGradient.BlendType = MixNode.BlendTypes.Blend;
				regularOrGradient.UseClamp = false;

				// Three math nodes to disable skylighting effect when skylight
				// has been turned off
				MathAdd backgroundSkylightDelimiterOne = new MathAdd("backgroundSkylightDelimiterOne");
				MathAdd backgroundSkylightDelimiterTwo = new MathAdd("backgroundSkylightDelimiterTwo");
				MathSubtract backgroundSkylightDelimiterThree = new MathSubtract("backgroundSkylightDelimiterThree");

				m_shader.AddNode(texcoords);
				m_shader.AddNode(lightPath);
				m_shader.AddNode(background);
				m_shader.AddNode(backgroundColorOrTexture);
				m_shader.AddNode(backgroundTexture);
				m_shader.AddNode(reflection);
				m_shader.AddNode(reflectionColorOrTexture);
				m_shader.AddNode(reflectionTexture);
				m_shader.AddNode(skylight);
				m_shader.AddNode(skylightColorOrTexture);
				m_shader.AddNode(skylightTexture);
				m_shader.AddNode(glossyFirstOnly);
				m_shader.AddNode(cameraOrTransmission);
				m_shader.AddNode(cameraTransmissionRayFirstOnly);
				m_shader.AddNode(skylightAndReflection);
				m_shader.AddNode(backgroundSkylightAndReflection);
				m_shader.AddNode(gradientColorRamp);
				m_shader.AddNode(gradientTexture);
				m_shader.AddNode(regularOrGradient);
				m_shader.AddNode(backgroundSkylightDelimiterOne);
				m_shader.AddNode(backgroundSkylightDelimiterTwo);
				m_shader.AddNode(backgroundSkylightDelimiterThree);

				// Connect environment textures to respective mixers to be able to switch
				// between environment texture or solid color.
				reflectionTexture.outs.Color.Connect(reflectionColorOrTexture.ins.Color2);
				skylightTexture.outs.Color.Connect(skylightColorOrTexture.ins.Color2);
				backgroundTexture.outs.Color.Connect(backgroundColorOrTexture.ins.Color2);

				// Reflection and sky mixers go directly into their respective
				// background nodes. For reflection and skylight we will use the
				// background texture/color if their respective custom
				// environments aren't in use (HasRefl, HasSky)
				if(m_original_background.HasRefl)
				{
					reflectionColorOrTexture.outs.Color.Connect(reflection.ins.Color);
				}
				else
				{
					backgroundColorOrTexture.outs.Color.Connect(reflection.ins.Color);
				}
				if(m_original_background.HasSky && m_original_background.SkylightEnabled)
				{
					skylightColorOrTexture.outs.Color.Connect(skylight.ins.Color);
				}
				else
				{
					backgroundColorOrTexture.outs.Color.Connect(skylight.ins.Color);
					// Skylight is disabled, so we need to adapt background so we don't
					// get skylighting effect. We do that by ensuring no diffuse lighting
					// past diffuse ray depth of 1
					if(!m_original_background.SkylightEnabled) {
						backgroundSkylightDelimiterOne.UseClamp = true;
						backgroundSkylightDelimiterTwo.UseClamp = true;
						lightPath.outs.IsCameraRay.Connect(backgroundSkylightDelimiterOne.ins.Value1);
						lightPath.outs.IsDiffuseRay.Connect(backgroundSkylightDelimiterOne.ins.Value2);
						backgroundSkylightDelimiterOne.outs.Value.Connect(backgroundSkylightDelimiterTwo.ins.Value1);
						lightPath.outs.IsGlossyRay.Connect(backgroundSkylightDelimiterTwo.ins.Value2);
						backgroundSkylightDelimiterTwo.outs.Value.Connect(backgroundSkylightDelimiterThree.ins.Value1);
						lightPath.outs.DiffuseDepth.Connect(backgroundSkylightDelimiterThree.ins.Value2);
						backgroundSkylightDelimiterThree.outs.Value.Connect(background.ins.Strength);
					}
				}

				// Background still can have either environment texture/color or gradient
				// set. The regularOrGradient mixer helps to pick right one. If the
				// UseGradientAsFloat evaluates to 1 we will get the gradient, otherwise
				// either color or environment texture
				backgroundColorOrTexture.outs.Color.Connect(regularOrGradient.ins.Color1);
				texcoords.outs.Window.Connect(gradientTexture.ins.Vector);
				gradientTexture.outs.Fac.Connect(gradientColorRamp.ins.Fac);
				gradientColorRamp.outs.Color.Connect(regularOrGradient.ins.Color2);
				regularOrGradient.outs.Color.Connect(background.ins.Color);

				// to glossyFirstOnly we connect IsGlossyRay into first and DiffuseDepth
				// into second input. This will have the effect that only the first
				// glossy bounce will sample the reflection background
				lightPath.outs.IsGlossyRay.Connect(glossyFirstOnly.ins.Value1);
				lightPath.outs.DiffuseDepth.Connect(glossyFirstOnly.ins.Value2);

				// Hook up skylight and reflection background to closure mixer, driven
				// by glossyFirstOnly. Whenever we have the first glossy ray we sample
				// reflection background in Closure2, otherwise sample skylight background
				glossyFirstOnly.outs.Value.Connect(skylightAndReflection.ins.Fac);
				if(m_original_background.HasSky)
				{
					skylight.outs.Background.Connect(skylightAndReflection.ins.Closure1);
				} else
				{
					background.outs.Background.Connect(skylightAndReflection.ins.Closure1);
				}
				if (m_original_background.HasRefl)
				{
					reflection.outs.Background.Connect(skylightAndReflection.ins.Closure2);
				} else
				{
					background.outs.Background.Connect(skylightAndReflection.ins.Closure2);
				}

				// Ensure we sample background for camera rays, and transmission rays such
				// that we don't have any glossy depth at all. This will allow us to see
				// the background through transparent surfaces
				lightPath.outs.IsCameraRay.Connect(cameraOrTransmission.ins.Value1);
				lightPath.outs.IsTransmissionRay.Connect(cameraOrTransmission.ins.Value2);
				cameraOrTransmission.outs.Value.Connect(cameraTransmissionRayFirstOnly.ins.Value1);
				lightPath.outs.GlossyDepth.Connect(cameraTransmissionRayFirstOnly.ins.Value2);

				// Join skylight/reflection and bacgkround in closure mixer driven by
				// the camera ray and transmission ray when glossy depth is 0.
				skylightAndReflection.outs.Closure.Connect(backgroundSkylightAndReflection.ins.Closure1);
				background.outs.Background.Connect(backgroundSkylightAndReflection.ins.Closure2);
				cameraTransmissionRayFirstOnly.outs.Value.Connect(backgroundSkylightAndReflection.ins.Fac);

				backgroundSkylightAndReflection.outs.Closure.Connect(m_shader.Output.ins.Surface);

				// set up the different texture nodes.
				if (m_original_background.BackgroundFill == BackgroundStyle.Environment && m_original_background.HasBgEnvTexture)
				{
					RenderEngine.SetTextureImage(backgroundTexture, m_original_background.BgTexture);
					_SetEnvironmentProjection(m_original_background.BgTexture, backgroundTexture);
					backgroundTexture.Translation = m_original_background.BgTexture.Transform.x;
					backgroundTexture.Scale = m_original_background.BgTexture.Transform.y;
					backgroundTexture.Rotation = m_original_background.BgTexture.Transform.z;
				}
				else if (m_original_background.BackgroundFill == BackgroundStyle.WallpaperImage && m_original_background.Wallpaper.HasTextureImage)
				{
					RenderEngine.SetTextureImage(backgroundTexture, m_original_background.Wallpaper);
					backgroundTexture.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}
				if (m_original_background.HasReflEnvTexture)
				{
					RenderEngine.SetTextureImage(reflectionTexture, m_original_background.ReflectionTexture);
					_SetEnvironmentProjection(m_original_background.ReflectionTexture, reflectionTexture);
					reflectionTexture.Translation = m_original_background.ReflectionTexture.Transform.x;
					reflectionTexture.Scale = m_original_background.ReflectionTexture.Transform.y;
					reflectionTexture.Rotation = m_original_background.ReflectionTexture.Transform.z;
				}
				if (m_original_background.HasSkyEnvTexture)
				{
					RenderEngine.SetTextureImage(skylightTexture, m_original_background.SkyTexture);
					_SetEnvironmentProjection(m_original_background.SkyTexture, skylightTexture);
					skylightTexture.Translation = m_original_background.SkyTexture.Transform.x;
					skylightTexture.Scale = m_original_background.SkyTexture.Transform.y;
					skylightTexture.Rotation = m_original_background.SkyTexture.Transform.z;
				}
			}

			// phew, done.
			m_shader.FinalizeGraph();
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
