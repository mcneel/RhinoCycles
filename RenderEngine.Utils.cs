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
using System.Drawing;
using ccl;
using ccl.ShaderNodes;
using Rhino.Render;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Settings;
using static Rhino.Render.RenderWindow;
using Rhino.UI;
using Eto.Forms;
using Rhino.Runtime;
using ccl.ShaderNodes.Sockets;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{

		public static ccl.PassType PassTypeForStandardChannel(StandardChannels channel) {
			switch(channel) {
				case StandardChannels.RGB:
				case StandardChannels.RGBA:
					return PassType.Combined;
				case StandardChannels.DistanceFromCamera:
					return PassType.Depth;
				case StandardChannels.NormalXYZ:
					return PassType.Normal;
				case StandardChannels.AlbedoRGB:
					return PassType.DiffuseColor;
				case StandardChannels.MaterialIds:
					return PassType.MaterialId;
				case StandardChannels.ObjectIds:
					return PassType.ObjectId;
				default:
					return PassType.Combined;
			}
		}

		public static StandardChannels StandardChannelForPassType(PassType pass) {
			switch(pass) {
				case PassType.Combined:
					return StandardChannels.RGBA;
				case PassType.Depth:
					return StandardChannels.DistanceFromCamera;
				case PassType.Normal:
					return StandardChannels.NormalXYZ;
				case PassType.DiffuseColor:
					return StandardChannels.AlbedoRGB;
				case PassType.MaterialId:
					return StandardChannels.MaterialIds;
				case PassType.ObjectId:
					return StandardChannels.ObjectIds;
				default:
					return StandardChannels.RGBA;
			}
		}


		/// <summary>
		/// Construct a full path name to the temp folder for
		/// McNeel/Rhino/VERSIONNR
		/// </summary>
		/// <returns>The full path.</returns>
		/// <param name="fileName">File name.</param>
		public static string TempPathForFile(string fileName)
		{
			var tmpfhdr = System.IO.Path.Combine(
				new [] {
					System.IO.Path.GetTempPath(),
					"McNeel",
					"Rhino",
					$"V{Rhino.RhinoApp.Version.Major}",
					fileName
				}
			);

			return tmpfhdr;
		}

		public void SaveRenderedBuffer(int sample)
		{
			if (!RcCore.It.AllSettings.SaveDebugImages) return;
			Eto.Forms.Application.Instance.AsyncInvoke(() =>
			{
				var tmpf = TempPathForFile($"RC_{ sample.ToString("D5")}.png");
				RenderWindow.SaveDibAsBitmap(tmpf);
			});
		}

		/// <summary>
		/// create a ccl.Scene
		/// </summary>
		/// <param name="client">Client to create scene for</param>
		/// <param name="session">Session this scene is created for</param>
		/// <param name="render_device">Render device this scene is created for</param>
		/// <param name="cycles_engine">Engine instance to create for</param>
		/// <returns></returns>
		protected static /*Scene*/ void CreateScene(Client client, Session session, Device render_device,
			RenderEngine cycles_engine, IAllSettings engineSettings)
		{
			#region set up scene parameters
			BvhLayout bvhLayout = BvhLayout.Default;
			if(render_device.IsOptix) {
				bvhLayout = BvhLayout.OptiX;
			}
			else if (render_device.IsCpu && HostUtils.RunningOnOSX) {
				bvhLayout = BvhLayout.Bvh2;
			}
			var scene_params = new SceneParameters(client, ShadingSystem.SVM, BvhType.Static, false, bvhLayout, false);
			#endregion

			#region create scene
			var scene = new Scene(client, scene_params, session)
			{
				#region integrator settings
				Integrator =
				{
					MaxBounce = engineSettings.MaxBounce,
					TransparentMaxBounce = engineSettings.TransparentMaxBounce,
					MaxDiffuseBounce = engineSettings.MaxDiffuseBounce,
					MaxGlossyBounce = engineSettings.MaxGlossyBounce,
					MaxTransmissionBounce = engineSettings.MaxTransmissionBounce,
					MaxVolumeBounce = engineSettings.MaxVolumeBounce,
					NoCaustics = engineSettings.NoCaustics,
					DiffuseSamples = engineSettings.DiffuseSamples,
					GlossySamples = engineSettings.GlossySamples,
					TransmissionSamples = engineSettings.TransmissionSamples,
					AoSamples = engineSettings.AoSamples,
					MeshLightSamples = engineSettings.MeshLightSamples,
					SubsurfaceSamples = engineSettings.SubsurfaceSamples,
					VolumeSamples = engineSettings.VolumeSamples,
					AaSamples = engineSettings.AaSamples,
					FilterGlossy = engineSettings.FilterGlossy,
					IntegratorMethod = engineSettings.IntegratorMethod,
					SampleAllLightsDirect = engineSettings.SampleAllLights,
					SampleAllLightsIndirect = engineSettings.SampleAllLightsIndirect,
					SampleClampDirect = engineSettings.SampleClampDirect,
					SampleClampIndirect = engineSettings.SampleClampIndirect,
					LightSamplingThreshold =  engineSettings.LightSamplingThreshold,
					SamplingPattern = SamplingPattern.Sobol,
					Seed = engineSettings.Seed,
					NoShadows = engineSettings.NoShadows,
				}
				#endregion
			};
			#endregion

			scene.Film.SetFilter(FilterType.Gaussian, 1.5f);
			scene.Film.Exposure = 1.0f;
			scene.Film.Update();


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

			session.Scene = scene;
		}

		static public float4 CreateFloat4(double x, double y, double z) { return new float4((float)x, (float)y, (float)z, 0.0f); }
		static public float4 CreateFloat4(double x, double y, double z, double w) { return new float4((float)x, (float)y, (float)z, (float)w); }
		static public float4 CreateFloat4(byte x, byte y, byte z, byte w) { return new float4(x / 255.0f, y / 255.0f, z / 255.0f, w / 255.0f); }
		static public float4 CreateFloat4(Color color) { return CreateFloat4(color.R, color.G, color.B, color.A); }

		/// <summary>
		/// Pixel count provided by the main monitor where Rhino resides.
		///
		/// Note: on MacOS we are always looking at the primary screen, regardless of where Rhino is opened.
		/// </summary>
		static public int _MonitorPixelCount
		{
			get;
			set;
		}

		/// <summary>
		/// Default pixel size based on monitor resolution.
		/// The screen resolution the Rhino main window is mostly on is used. The width
		/// and height are multiplied, that is used to determine pixel size. Currently:
		/// 8K (7680x4320) and larger: 4
		/// 4K (3840x2160) and larger: 2
		/// Anything lower than Full HD: 1
		/// </summary>
		static public int DefaultPixelSizeBasedOnMonitorResolution
		{
			get {
				int pixelSize = 1;
				int pixelCount = _MonitorPixelCount;

				if(pixelCount >= 7_680*4_320) {
					pixelSize = 4;
				}
				else if (pixelCount >= 3_840*2_160) {
					pixelSize = 2;
				}
				return pixelSize;
			}
		}

		public static float DegToRad(float ang)
		{
			return ang * (float)Math.PI / 180.0f;
		}

		public static Size TileSize(ccl.Device device)
		{
			var tilex = RcCore.It.AllSettings.TileX;
			var tiley = RcCore.It.AllSettings.TileY;
			if (!RcCore.It.AllSettings.DebugNoOverrideTileSize)
			{
				if (device.IsOpenCl)
				{
					tilex = tiley = 1024;
				}
				else if (device.IsCuda)
				{
					tilex = tiley = 512;
				}
				else if (device.IsCpu)
				{
					tilex = tiley = 32;
				}
			}

			return new Size(tilex, tiley);
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

			float4 t = texture.Transform.x;
			image_node.Translation = t;
			image_node.Translation.z = 0;
			image_node.Translation.w = 1;
			image_node.Scale.x = 1.0f / texture.Transform.y.x;
			image_node.Scale.y = 1.0f / texture.Transform.y.y;
			image_node.Rotation.z = -1.0f * DegToRad(texture.Transform.z.z);

			image_node.Projection = TextureNode.TextureProjection.Flat;
			image_node.Interpolation = InterpolationType.Cubic;

			if (texture.ProjectionMode == TextureProjectionMode.WcsBox)
			{
				texture_coordinates.UseTransform = true;
				texture_coordinates.outs.WcsBox.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Wcs)
			{
				texture_coordinates.UseTransform = true;
				texture_coordinates.outs.Object.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Screen)
			{
				texture_coordinates.outs.Window.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.View)
			{
				texture_coordinates.outs.Camera.Connect(image_node.ins.Vector);
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
				texture_coordinates.outs.UV.Connect(image_node.ins.Vector);
			}
		}

		public static VectorSocket GetProjectionModeOutputSocket(CyclesTextureImage texture, TextureCoordinateNode texture_coordinates)
		{
			if (texture.ProjectionMode == TextureProjectionMode.WcsBox)
			{
				return texture_coordinates.outs.WcsBox;
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Wcs)
			{
				return texture_coordinates.outs.Object;
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Screen)
			{
				return texture_coordinates.outs.Window;
			}
			else if (texture.ProjectionMode == TextureProjectionMode.View)
			{
				return texture_coordinates.outs.Camera;
			}
			else if (texture.ProjectionMode == TextureProjectionMode.EnvironmentMap)
			{
				switch (texture.EnvProjectionMode)
				{
					case TextureEnvironmentMappingMode.Spherical:
						return texture_coordinates.outs.EnvSpherical;
					case TextureEnvironmentMappingMode.EnvironmentMap:
						return texture_coordinates.outs.EnvEmap;
					case TextureEnvironmentMappingMode.Box:
						return texture_coordinates.outs.EnvBox;
					case TextureEnvironmentMappingMode.LightProbe:
						return texture_coordinates.outs.EnvLightProbe;
					case TextureEnvironmentMappingMode.Cube:
						return texture_coordinates.outs.EnvCubemap;
					case TextureEnvironmentMappingMode.VerticalCrossCube:
						return texture_coordinates.outs.EnvCubemapVerticalCross;
					case TextureEnvironmentMappingMode.HorizontalCrossCube:
						return texture_coordinates.outs.EnvCubemapHorizontalCross;
					case TextureEnvironmentMappingMode.Hemispherical:
						return texture_coordinates.outs.EnvHemispherical;
					default:
						return texture_coordinates.outs.EnvEmap;
				}
			}
			else
			{
				return texture_coordinates.outs.UV;
			}
		}
	}
}
