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

using System;
using System.Drawing;
using ccl;
using ccl.ShaderNodes;
using Rhino.Render;

namespace RhinoCycles
{
	partial class RenderEngine
	{
		/// <summary>
		/// create a ccl.Scene
		/// </summary>
		/// <param name="client">Client to create scene for</param>
		/// <param name="render_device">Render device to use</param>
		/// <param name="cycles_engine">Engine instance to create for</param>
		/// <returns></returns>
		protected static Scene CreateScene(Client client, Device render_device,
			RenderEngine cycles_engine)
		{
			#region set up scene parameters
			var scene_params = new SceneParameters(client, ShadingSystem.SVM, BvhType.Dynamic, false, false, false, false);
			#endregion

			#region create scene
			var scene = new Scene(client, scene_params, render_device)
			{
				#region integrator settings
				Integrator =
				{
					MaxBounce = cycles_engine.Settings.MaxBounce,
					MinBounce = cycles_engine.Settings.MinBounce,
					TransparentMinBounce = cycles_engine.Settings.TransparentMinBounce,
					TransparentMaxBounce = cycles_engine.Settings.TransparentMaxBounce,
					MaxDiffuseBounce = cycles_engine.Settings.MaxDiffuseBounce,
					MaxGlossyBounce = cycles_engine.Settings.MaxGlossyBounce,
					MaxTransmissionBounce = cycles_engine.Settings.MaxTransmissionBounce,
					MaxVolumeBounce = cycles_engine.Settings.MaxVolumeBounce,
					NoCaustics = cycles_engine.Settings.NoCaustics,
					TransparentShadows = cycles_engine.Settings.TransparentShadows,
					DiffuseSamples = cycles_engine.Settings.DiffuseSamples,
					GlossySamples = cycles_engine.Settings.GlossySamples,
					TransmissionSamples = cycles_engine.Settings.TransmissionSamples,
					AoSamples = cycles_engine.Settings.AoSamples,
					MeshLightSamples = cycles_engine.Settings.MeshLightSamples,
					SubsurfaceSamples = cycles_engine.Settings.SubsurfaceSamples,
					VolumeSamples = cycles_engine.Settings.VolumeSamples,
					AaSamples = cycles_engine.Settings.AaSamples,
					FilterGlossy = cycles_engine.Settings.FilterGlossy,
					IntegratorMethod = cycles_engine.Settings.IntegratorMethod,
					SampleAllLightsDirect = cycles_engine.Settings.SampleAllLights,
					SampleAllLightsIndirect = cycles_engine.Settings.SampleAllLightsIndirect,
					SampleClampDirect = cycles_engine.Settings.SampleClampDirect,
					SampleClampIndirect = cycles_engine.Settings.SampleClampIndirect,
					/* TODO : make sure CMJ doesn't crash on CPU */
					SamplingPattern = render_device.IsCpu ? SamplingPattern.Sobol : cycles_engine.Settings.SamplingPattern,
					Seed = cycles_engine.Settings.Seed
				}
				#endregion
			};

			scene.Film.SetFilter(FilterType.Gaussian, 1.5f);
			scene.Film.Exposure = 1.0f;
			scene.Film.Update();

			#endregion

			#region background shader

			// we add here a simple background shader. This will be repopulated with
			// other nodes whenever background changes are detected.
			var background_shader = new Shader(client, Shader.ShaderType.World)
			{
				Name = "Rhino Background"
			};

			var bgnode = new BackgroundNode("orig bg");
			bgnode.ins.Color.Value = new float4(0.7f);
			bgnode.ins.Strength.Value = 1.0f;

			background_shader.AddNode(bgnode);
			bgnode.outs.Background.Connect(background_shader.Output.ins.Surface);
			background_shader.FinalizeGraph();

			scene.AddShader(background_shader);

			scene.Background.Shader = background_shader;
			scene.Background.AoDistance = 0.0f;
			scene.Background.AoFactor = 0.0f;
			scene.Background.Visibility = PathRay.AllVisibility;

			#endregion

			return scene;
		}

		static public float4 CreateFloat4(double x, double y, double z) { return new float4((float)x, (float)y, (float)z, 0.0f); }
		static public float4 CreateFloat4(byte x, byte y, byte z, byte w) { return new float4(x / 255.0f, y / 255.0f, z / 255.0f, w / 255.0f); }
		static public float4 CreateFloat4(Color color) { return CreateFloat4(color.R, color.G, color.B, color.A); }

		public static float DegToRad(float ang)
		{
			return ang * (float)Math.PI / 180.0f;
		}

		/// <summary>
		/// Set image texture node and link up with correct TextureCoordinateNode output based on
		/// texture ProjectionMode.
		/// 
		/// This may add new nodes to the shader!
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="image_node"></param>
		/// <param name="texture_coordinates"></param>
		public static void SetProjectionMode(Shader shader, CyclesTextureImage texture, ImageTextureNode image_node,
			TextureCoordinateNode texture_coordinates)
		{
			if (!texture.HasTextureImage) return;

			Guid g = Guid.NewGuid();

			texture_coordinates.UseTransform = false;

			var tfm = new MatrixMathNode("texture transform" + g.ToString())
			{
				Transform = texture.Transform
			};
			shader.AddNode(tfm);

			image_node.Projection = TextureNode.TextureProjection.Flat;

			if (texture.ProjectionMode == TextureProjectionMode.WcsBox)
			{
				texture_coordinates.UseTransform = true;
				texture_coordinates.outs.WcsBox.Connect(tfm.ins.Vector);
				tfm.outs.Vector.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Wcs)
			{
				texture_coordinates.UseTransform = true;
				texture_coordinates.outs.Object.Connect(tfm.ins.Vector);
				tfm.outs.Vector.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Screen)
			{
				texture_coordinates.outs.Window.Connect(tfm.ins.Vector);
				tfm.outs.Vector.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.View)
			{
				texture_coordinates.outs.Camera.Connect(tfm.ins.Vector);
				tfm.outs.Vector.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.EnvironmentMap)
			{
				texture_coordinates.UseTransform = false;
				switch (texture.EnvProjectionMode)
				{
					case TextureEnvironmentMappingMode.Spherical:
						texture_coordinates.outs.EnvSpherical.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.EnvironmentMap:
						texture_coordinates.outs.EnvEmap.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.Box:
						texture_coordinates.outs.EnvBox.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.LightProbe:
						texture_coordinates.outs.EnvLightProbe.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.Cube:
						texture_coordinates.outs.EnvCubemap.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.VerticalCrossCube:
						texture_coordinates.outs.EnvCubemapVerticalCross.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.HorizontalCrossCube:
						texture_coordinates.outs.EnvCubemapHorizontalCross.Connect(image_node.ins.Vector);
						break;
					case TextureEnvironmentMappingMode.Hemispherical:
						texture_coordinates.outs.EnvHemispherical.Connect(image_node.ins.Vector);
						break;
					default:
						texture_coordinates.outs.EnvLightProbe.Connect(image_node.ins.Vector);
						break;
				}
			}
			else
			{
				texture_coordinates.outs.UV.Connect(tfm.ins.Vector);
				tfm.outs.Vector.Connect(image_node.ins.Vector);
			}
		}
	}
}
