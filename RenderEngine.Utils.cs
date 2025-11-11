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
using Rhino.Render;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System;
using System.Drawing;
using static Rhino.Render.RenderWindow;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{

		public static ccl.PassType PassTypeForStandardChannel(StandardChannels channel) {
			switch(channel) {
				case StandardChannels.RGB:
				case StandardChannels.RGBA:
					return PassType.PASS_COMBINED;
				case StandardChannels.DistanceFromCamera:
					return PassType.PASS_DEPTH;
				case StandardChannels.NormalXYZ:
					return PassType.PASS_NORMAL;
				case StandardChannels.AlbedoRGB:
					return PassType.PASS_DIFFUSE_COLOR;
				case StandardChannels.MaterialIds:
					return PassType.PASS_MATERIAL_ID;
				case StandardChannels.ObjectIds:
					return PassType.PASS_OBJECT_ID;
				default:
					return PassType.PASS_COMBINED;
			}
		}


		public static StandardChannels StandardChannelForPassType(PassType pass) {
			switch(pass) {
				case PassType.PASS_COMBINED:
					return StandardChannels.RGBA;
				case PassType.PASS_DEPTH:
					return StandardChannels.DistanceFromCamera;
				case PassType.PASS_NORMAL:
					return StandardChannels.NormalXYZ;
				case PassType.PASS_DIFFUSE_COLOR:
					return StandardChannels.AlbedoRGB;
				case PassType.PASS_MATERIAL_ID:
					return StandardChannels.MaterialIds;
				case PassType.PASS_OBJECT_ID:
					return StandardChannels.ObjectIds;
				default:
					return StandardChannels.RGBA;
			}
		}

		public static string NameForPassType(PassType pass)
		{
			return pass.ToString().Replace("PASS_", "").ToLowerInvariant();
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
		/// create a ccl.Session
		/// </summary>
		/// <param name="client">Client to create scene for</param>
		/// <param name="session">Session this scene is created for</param>
		/// <param name="render_device">Render device this scene is created for</param>
		/// <param name="cycles_engine">Engine instance to create for</param>
		/// <returns></returns>
		protected static void InitializeSceneSettings(Session session, Device render_device,
			RenderEngine cycles_engine, IAllSettings engineSettings)
		{
			session.Scene.Film.ins.FilterType.Value = Film.FilmFilter.Gaussian;
			session.Scene.Film.ins.FilterWidth.Value = 1.5f;
			session.Scene.Film.ins.Exposure.Value = 1.0f;

			session.Scene.Film.Update();
			#region integrator settings
			session.Scene.Integrator.ins.MaxBounce.Value = engineSettings.MaxBounce;
			session.Scene.Integrator.ins.TransparentMaxBounce.Value = engineSettings.TransparentMaxBounce;
			session.Scene.Integrator.ins.MaxDiffuseBounce.Value = engineSettings.MaxDiffuseBounce;
			session.Scene.Integrator.ins.MaxGlossyBounce.Value = engineSettings.MaxGlossyBounce;
			session.Scene.Integrator.ins.MaxTransmissionBounce.Value = engineSettings.MaxTransmissionBounce;
			session.Scene.Integrator.ins.MaxVolumeBounce.Value = engineSettings.MaxVolumeBounce;
			// TODO no caustics? session.Scene.Integrator.ins.NoCaustics = engineSettings.NoCaustics;
			session.Scene.Integrator.ins.ReflectiveCaustics.Value = engineSettings.CausticsReflective;
			session.Scene.Integrator.ins.RefractiveCaustics.Value = engineSettings.CausticsRefractive;
			session.Scene.Integrator.ins.AOBounces.Value = engineSettings.AoBounces;
			session.Scene.Integrator.ins.AOFactor.Value = engineSettings.AoFactor;
			session.Scene.Integrator.ins.AODistance.Value = engineSettings.AoDistance;
			session.Scene.Integrator.ins.AOAdditiveFactor.Value = engineSettings.AoAdditiveFactor;
			// TODO Volume samples? session.Scene.Integrator.ins.Volu = engineSettings.VolumeSamples;
			session.Scene.Integrator.ins.AASamples.Value = engineSettings.AaSamples;
			session.Scene.Integrator.ins.FilterGlossy.Value = engineSettings.FilterGlossy;
			session.Scene.Integrator.ins.UseDirectLight.Value = engineSettings.UseDirectLight;
			session.Scene.Integrator.ins.UseIndirectLight.Value= engineSettings.UseIndirectLight;
			session.Scene.Integrator.ins.SampleClampDirect.Value = engineSettings.SampleClampDirect;
			session.Scene.Integrator.ins.SampleClampIndirect.Value = engineSettings.SampleClampIndirect;
			session.Scene.Integrator.ins.LightSamplingThreshold.Value =  engineSettings.LightSamplingThreshold;
			session.Scene.Integrator.ins.SamplingPattern.Value = Integrator.IntegratorSamplingPattern.SobolBurley;
			session.Scene.Integrator.ins.Seed.Value = engineSettings.Seed;
			#endregion
		}

		static public float4 CreateFloat4(double x, double y, double z) { return new float4((float)x, (float)y, (float)z, 0.0f); }
		static public float4 CreateFloat4(double x, double y, double z, double w) { return new float4((float)x, (float)y, (float)z, (float)w); }
		static public float3 CreateFloat3(double x, double y, double z) { return new float3((float)x, (float)y, (float)z); }
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

		public static int TileSize(ccl.Device device)
		{
			var tilex = RcCore.It.AllSettings.TileX;
			if (!RcCore.It.AllSettings.DebugNoOverrideTileSize)
			{
				tilex = 2048;
			}

			return tilex;
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
			RhinoTextureCoordinateNode texture_coordinates)
		{
			if (!texture.HasTextureImage) return;

			Guid g = Guid.NewGuid();

			texture_coordinates.ins.UseTransform.Value = false;

			float3 t = texture.Transform.x;
			t.z = 0.0f;
			image_node.SetTexMappingTranslation(t);
			float3 s = texture.Transform.y;
			s.x = 1.0f / texture.Transform.y.x;
			s.y = 1.0f / texture.Transform.y.y;
			image_node.SetTexMappingScale(s);
			float3 rot = texture.Transform.z;
			rot.z = -1.0f * DegToRad(texture.Transform.z.z);
			image_node.SetTexMappingRotation(rot);

			image_node.ins.Projection.Value = ImageTextureNode.ImageTextureNodeProjection.Flat;
			image_node.ins.Interpolation.Value = ImageTextureNode.ImageTextureNodeInterpolation.Cubic;

			if (texture.ProjectionMode == TextureProjectionMode.WcsBox)
			{
				texture_coordinates.ins.UseTransform.Value = true;
				texture_coordinates.outs.WcsBox.Connect(image_node.ins.Vector);
			}
			else if (texture.ProjectionMode == TextureProjectionMode.Wcs)
			{
				texture_coordinates.ins.UseTransform.Value = true;
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
				texture_coordinates.ins.UseTransform.Value = false;
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
						texture_coordinates.outs.EnvHemi.Connect(image_node.ins.Vector);
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

		public static VectorSocket GetProjectionModeOutputSocket(Shader sh, Rhino.Render.TextureProjectionMode projectionMode, Rhino.Render.TextureEnvironmentMappingMode environmentMappingMode, RhinoTextureCoordinateNode texture_coordinates)
		{
			if (projectionMode == TextureProjectionMode.WcsBox)
			{
				return texture_coordinates.outs.WcsBox;
			}
			else if (projectionMode == TextureProjectionMode.Wcs)
			{
				return texture_coordinates.outs.Object;
			}
			else if (projectionMode == TextureProjectionMode.Screen)
			{
				return texture_coordinates.outs.Window;
			}
			else if (projectionMode == TextureProjectionMode.View)
			{
				return texture_coordinates.outs.Camera;
			}
			else if (projectionMode == TextureProjectionMode.EnvironmentMap)
			{
				switch (environmentMappingMode)
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
						return texture_coordinates.outs.EnvHemi;
					default:
						{
								var separate_envmap_texco = new SeparateXYZNode(sh, "envmap texco separate vector");

								var flip_sign_envmap_texco_y = new MathMultiply(sh, "flip sign envmap texco y");
								flip_sign_envmap_texco_y.ins.Value2.Value = -1.0f;
								flip_sign_envmap_texco_y.ins.UseClamp.Value = false;

								var flip_sign_envmap_texco_o = new MathMultiply(sh, "flip sign envmap texco o");
								flip_sign_envmap_texco_o.ins.Value2.Value = -1.0f;
								flip_sign_envmap_texco_o.ins.UseClamp.Value = false;

								var recombine_envmap_texco = new CombineXYZNode(sh, "recombine envmap texco");

								separate_envmap_texco.outs.X.Connect(flip_sign_envmap_texco_o.ins.Value1);
								separate_envmap_texco.outs.Y.Connect(flip_sign_envmap_texco_y.ins.Value1);

								flip_sign_envmap_texco_o.outs.Value.Connect(recombine_envmap_texco.ins.X);
								flip_sign_envmap_texco_y.outs.Value.Connect(recombine_envmap_texco.ins.Y);
								separate_envmap_texco.outs.Z.Connect(recombine_envmap_texco.ins.Z);
								texture_coordinates.outs.EnvEmap.Connect(separate_envmap_texco.ins.Vector);
								return recombine_envmap_texco.outs.Vector;

						}
				}
			}
			else
			{
				return texture_coordinates.outs.UV;
			}
		}
	}
}
