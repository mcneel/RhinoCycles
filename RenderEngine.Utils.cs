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

using System;
using System.Drawing;
using ccl;
using ccl.ShaderNodes;
using Rhino.Render;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{
		public void SaveRenderedBuffer(int sample)
		{
			if (!RcCore.It.EngineSettings.SaveDebugImages) return;
			var tmpf = $"{Environment.GetEnvironmentVariable("TEMP")}\\RC_{sample.ToString("D5")}.png";
			RenderWindow.SaveDibAsBitmap(tmpf);
		}

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
			var scene_params = new SceneParameters(client, ShadingSystem.SVM, BvhType.Static, false, render_device.IsCpu, false);
			#endregion

			#region create scene
			var scene = new Scene(client, scene_params, render_device)
			{
				#region integrator settings
				Integrator =
				{
					MaxBounce = RcCore.It.EngineSettings.MaxBounce,
					TransparentMaxBounce = RcCore.It.EngineSettings.TransparentMaxBounce,
					MaxDiffuseBounce = RcCore.It.EngineSettings.MaxDiffuseBounce,
					MaxGlossyBounce = RcCore.It.EngineSettings.MaxGlossyBounce,
					MaxTransmissionBounce = RcCore.It.EngineSettings.MaxTransmissionBounce,
					MaxVolumeBounce = RcCore.It.EngineSettings.MaxVolumeBounce,
					NoCaustics = RcCore.It.EngineSettings.NoCaustics,
					DiffuseSamples = RcCore.It.EngineSettings.DiffuseSamples,
					GlossySamples = RcCore.It.EngineSettings.GlossySamples,
					TransmissionSamples = RcCore.It.EngineSettings.TransmissionSamples,
					AoSamples = RcCore.It.EngineSettings.AoSamples,
					MeshLightSamples = RcCore.It.EngineSettings.MeshLightSamples,
					SubsurfaceSamples = RcCore.It.EngineSettings.SubsurfaceSamples,
					VolumeSamples = RcCore.It.EngineSettings.VolumeSamples,
					AaSamples = RcCore.It.EngineSettings.AaSamples,
					FilterGlossy = RcCore.It.EngineSettings.FilterGlossy,
					IntegratorMethod = RcCore.It.EngineSettings.IntegratorMethod,
					SampleAllLightsDirect = RcCore.It.EngineSettings.SampleAllLights,
					SampleAllLightsIndirect = RcCore.It.EngineSettings.SampleAllLightsIndirect,
					SampleClampDirect = RcCore.It.EngineSettings.SampleClampDirect,
					SampleClampIndirect = RcCore.It.EngineSettings.SampleClampIndirect,
					LightSamplingThreshold =  RcCore.It.EngineSettings.LightSamplingThreshold,
					SamplingPattern = SamplingPattern.CMJ,
					Seed = RcCore.It.EngineSettings.Seed,
					NoShadows = RcCore.It.EngineSettings.NoShadows,
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
			bgnode.ins.Color.Value = new float4(1.0f);
			bgnode.ins.Strength.Value = 1.0f;

			background_shader.AddNode(bgnode);
			bgnode.outs.Background.Connect(background_shader.Output.ins.Surface);
			background_shader.FinalizeGraph();

			scene.AddShader(background_shader);

			scene.Background.Shader = background_shader;
			scene.Background.AoDistance = 0.0f;
			scene.Background.AoFactor = 0.0f;
			scene.Background.Visibility = PathRay.AllVisibility;
			scene.Background.Transparent = false;

			#endregion

			return scene;
		}

		static public float4 CreateFloat4(double x, double y, double z) { return new float4((float)x, (float)y, (float)z, 0.0f); }
		static public float4 CreateFloat4(byte x, byte y, byte z, byte w) { return new float4(x / 255.0f, y / 255.0f, z / 255.0f, w / 255.0f); }
		static public float4 CreateFloat4(Color color) { return CreateFloat4(color.R, color.G, color.B, color.A); }

		static public int ScaledPixelSize
		{
			get
			{
#if ON_RUNTIME_WIN
				var sdpi = RhinoWindows.Forms.Dpi.ScaleInt(1);
				return sdpi;
#else
				return 1;
#endif
			}

		}
		static public int DpiScale
		{
			get
			{
#if ON_RUNTIME_WIN
				var sdpi = RhinoWindows.Forms.Dpi.DpiScale();
				return sdpi;
#else
				return 1;
#endif
			}
		}
		static public int Dpi
		{
			get
			{
#if ON_RUNTIME_WIN
				var sdpi = RhinoWindows.Forms.Dpi.ScreenDpi();
				return sdpi;
#else
				return 72;
#endif
			}

		}
		static public bool OnHighDpi
		{
			get
			{
#if ON_RUNTIME_WIN
				var sdpi = RhinoWindows.Forms.Dpi.ScreenDpi();
				return sdpi > 96;
#else
				return false;
#endif
			}
		}

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
		/// <param name="shader"></param>
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
				SeparateXyzNode sepvec = new SeparateXyzNode();
				CombineXyzNode combvec = new CombineXyzNode();
				MathNode inverty = new MathNode {Operation = MathNode.Operations.Subtract};
				inverty.ins.Value1.Value = 1.0f;
				shader.AddNode(sepvec);
				shader.AddNode(combvec);
				shader.AddNode(inverty);

				texture_coordinates.outs.Window.Connect(sepvec.ins.Vector);

				sepvec.outs.Y.Connect(inverty.ins.Value2);

				sepvec.outs.X.Connect(combvec.ins.X);
				inverty.outs.Value.Connect(combvec.ins.Y);
				sepvec.outs.Z.Connect(combvec.ins.Z);

				combvec.outs.Vector.Connect(tfm.ins.Vector);

				tfm.Transform = tfm.Transform;
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
						texture_coordinates.outs.EnvEmap.Connect(image_node.ins.Vector);
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
