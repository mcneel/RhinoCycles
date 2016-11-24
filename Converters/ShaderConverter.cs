/**
Copyright 2014-2016 Robert McNeel and Associates

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
using ccl;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using RhinoCyclesCore.Materials;
using Light = Rhino.Render.ChangeQueue.Light;
using Material = Rhino.DocObjects.Material;

namespace RhinoCyclesCore.Converters
{
	public class ShaderConverter
	{

		private readonly EngineSettings _engineSettings;
		public ShaderConverter(EngineSettings engineSettings)
		{
			_engineSettings = engineSettings;
		}

		private enum ProbableMaterial
		{
			Plaster,
			Glass,
			Gem,
			Plastic,
			Metal,
			Custom
		}

		/// <summary>
		/// Determine material type using smell, but also querying for
		/// specific parameters on the RenderMaterial
		/// </summary>
		/// <param name="rm"></param>
		/// <returns>ProbableMaterial</returns>
		private ProbableMaterial GuessMaterialFromSmell(RenderMaterial rm)
		{
			if(rm.SmellsLikePlaster) return ProbableMaterial.Plaster;

			if(rm.SmellsLikeMetal) return ProbableMaterial.Metal;

			if (rm.SmellsLikeGlass)
			{
				if (rm.GetParameter("type") != null)
				{
					return ProbableMaterial.Gem;
				}

				if(rm.GetParameter("ior") != null)
				{
					return ProbableMaterial.Glass;
				}

				return ProbableMaterial.Plastic;
			}

			return ProbableMaterial.Custom;
		}

		/// <summary>
		/// Create a CyclesShader based on given Material m
		/// </summary>
		/// <param name="rm">Material to convert to CyclesShader</param>
		/// <param name="gamma">gamma to use for this shader</param>
		/// <returns>The CyclesShader</returns>
		internal CyclesShader CreateCyclesShader(RenderMaterial rm, float gamma)
		{
			var mid = rm.RenderHash;
			CyclesShader shader = null;

			var crm = rm as ICyclesMaterial;

			if (crm == null)
			{
				// figure out what type of material we are.
				var probemat = GuessMaterialFromSmell(rm);
				// always simulate material, need to know now myself
				// what to read out from the simulated material to
				// populate my own material descriptions.
				var m = rm.SimulateMaterial(true);

				rm.BeginChange(RenderContent.ChangeContexts.Ignore);
				var dcl = m.DiffuseColor;
				var scl = m.SpecularColor;
				var rcl = m.ReflectionColor;
				var rfcl = m.TransparentColor;
				var emcl = m.EmissionColor;
				var polish = (float) m.ReflectionGlossiness; //*_engineSettings.PolishFactor;
				var reflectivity = (float) m.Reflectivity; //*_engineSettings.PolishFactor;
				var metalic = 0f;
				var shine = (float) (m.Shine/Material.MaxShine);

				switch (probemat)
				{
					case ProbableMaterial.Plaster:
						var plaster = new DiffuseMaterial();
						plaster.SetParameter("diffuse", m.DiffuseColor);
						crm = plaster;
						break;
					default:
						switch (probemat)
						{
							case ProbableMaterial.Glass:
							case ProbableMaterial.Gem:
								dcl = m.TransparentColor;
								metalic = 0f;
								break;
							case ProbableMaterial.Metal:
								dcl = m.ReflectionColor;
								metalic = reflectivity; //1.0f;
								break;
							case ProbableMaterial.Plastic:
								polish = reflectivity;
								shine = polish;
								reflectivity = 0f;
								metalic = 0f;
								break;
						}


						var difftexAlpha = m.AlphaTransparency;

						var col = RenderEngine.CreateFloat4(dcl.R, dcl.G, dcl.B, 255);
						var spec = RenderEngine.CreateFloat4(scl.R, scl.G, scl.B, 255);
						var refl = RenderEngine.CreateFloat4(rcl.R, rcl.G, rcl.B, 255);
						var transp = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
						var refr = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
						var emis = RenderEngine.CreateFloat4(emcl.R, emcl.G, emcl.B, 255);

						shader = new CyclesShader
						{
							Id = mid,
							Type = CyclesShader.Shader.Diffuse,
							CyclesMaterialType = CyclesShader.CyclesMaterial.No,

							Shadeless = m.DisableLighting,

							DiffuseColor = col,
							SpecularColor = spec,
							ReflectionColor = refl,
							ReflectionRoughness = polish,
							RefractionColor = refr,
							RefractionRoughness = (float) m.RefractionGlossiness,
							TransparencyColor = transp,
							EmissionColor = emis,


							FresnelIOR = (float) m.FresnelIndexOfRefraction,
							IOR = (float) m.IndexOfRefraction,
							Roughness = (float) m.ReflectionGlossiness,
							Reflectivity = reflectivity,
							Metalic =  metalic,
							Transparency = (float) m.Transparency,
							Shine = shine,

							FresnelReflections = m.FresnelReflections,

							Gamma = gamma,

							Name = m.Name ?? ""
						};

						shader.DiffuseTexture.Amount = 0.0f;
						shader.BumpTexture.Amount = 0.0f;
						shader.TransparencyTexture.Amount = 0.0f;
						shader.EnvironmentTexture.Amount = 0.0f;

						if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Diffuse))
						{
							var difftex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Diffuse);

							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, difftex, RenderMaterial.StandardChildSlots.Diffuse);
							if (shader.HasDiffuseTexture)
							{
								shader.DiffuseTexture.UseAlpha = difftexAlpha;
								shader.DiffuseTexture.Amount = (float) Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Diffuse)/100.0f, 1.0f);
							}
						}

						if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Bump))
						{
							var bumptex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump);
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, bumptex, RenderMaterial.StandardChildSlots.Bump);
							if (shader.HasBumpTexture)
							{
								shader.BumpTexture.Amount = (float) Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Bump)/100.0f, 1.0f);
							}
						}

						if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Transparency))
						{
							var transtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Transparency);
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, transtex,
								RenderMaterial.StandardChildSlots.Transparency);
							if (shader.HasTransparencyTexture)
							{
								shader.TransparencyTexture.Amount = (float) Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Transparency)/100.0f, 1.0f);
							}
						}

						if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Environment))
						{
							var envtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Environment);
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, envtex,
								RenderMaterial.StandardChildSlots.Environment);
							if (shader.HasEnvironmentTexture)
							{
								shader.EnvironmentTexture.Amount = (float) Math.Min(rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Environment)/100.0f, 1.0f);
							}
						}
						break;

				}

				rm.EndChange();

			}
			if (crm != null)
			{
				shader = new CyclesShader
				{
					Id = mid,
					CyclesMaterialType = crm.MaterialType,
					Gamma = gamma,
					Crm = crm
				};
			}

			shader.Gamma = gamma;

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
			var enabled = lg.IsEnabled ? 1.0 : 0.0;

			var spotangle = 0.0;
			var smooth = 0.0;
			var size = 0.0f;
			var strength = (float)(lg.Intensity * _engineSettings.PointlightFactor * enabled);
			var axisu = new float4(0.0f);
			var axisv = new float4(0.0f);
			var useMis = false;
			var sizeU = 0.0f;
			var sizeV = 0.0f;

			var co = RenderEngine.CreateFloat4(lg.Location.X, lg.Location.Y, lg.Location.Z);
			var dir = RenderEngine.CreateFloat4(lg.Direction.X, lg.Direction.Y, lg.Direction.Z);
			var color = RenderEngine.CreateFloat4(lg.Diffuse.R, lg.Diffuse.G, lg.Diffuse.B, lg.Diffuse.A);

			var lt = LightType.Point;
			if (lg.IsDirectionalLight)
			{
				lt = LightType.Distant;
				strength = (float)(lg.Intensity * _engineSettings.SunlightFactor * enabled);
				//size = 0.01f;
			}
			else if (lg.IsSpotLight)
			{
				lt = LightType.Spot;
				spotangle = lg.SpotAngleRadians * 2;
				smooth = 1.0 / Math.Max(lg.HotSpot, 0.001f) - 1.0;
				strength = (float)(lg.Intensity * _engineSettings.SpotlightFactor * enabled);
			}
			else if (lg.IsRectangularLight)
			{
				lt = LightType.Area;

				strength = (float)(lg.Intensity * _engineSettings.ArealightFactor * enabled);

				var width = lg.Width;
				var length = lg.Length;

				sizeU = (float)width.Length;
				sizeV = (float)length.Length;

				size = 1.0f;

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

			var clight = new CyclesLight
				{
					Type = lt,
					Co = co,
					Dir = dir,
					DiffuseColor = color,
					Size = size,

					SizeU = sizeU,
					SizeV = sizeV,

					AxisU = axisu,
					AxisV = axisv,

					UseMis = useMis,

					SpotAngle = (float)spotangle,
					SpotSmooth = (float)smooth,

					Strength = strength,

					CastShadow = true,

					Gamma = gamma,

					Id = lg.Id
				};

			return clight;
		}
	}
}
