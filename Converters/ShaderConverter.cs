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
using sdd = System.Diagnostics.Debug;

namespace RhinoCyclesCore
{
	public class ShaderConverter
	{

		private readonly EngineSettings m_engine_settings;
		public ShaderConverter(EngineSettings engineSettings)
		{
			m_engine_settings = engineSettings;
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
		/// <param name="m">Material to convert to CyclesShader</param>
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

				switch (probemat)
				{
					case ProbableMaterial.Plaster:
						var plaster = new DiffuseMaterial();
						plaster.SetParameter("diffuse", m.DiffuseColor);
						crm = plaster;
						break;
					case ProbableMaterial.Glass:
					case ProbableMaterial.Gem:
						var glass = new GlassMaterial { Name = m.Name };
						glass.SetParameter("glass_color", m.TransparentColor);
						glass.SetParameter("frost-amount", (float)m.ReflectionGlossiness);
						glass.SetParameter("ior", (float)m.IndexOfRefraction);
						crm = glass;
						break;
					case ProbableMaterial.Plastic:
						var plastic = new SimplePlasticMaterial();
						plastic.SetParameter("diffuse", m.DiffuseColor);
						plastic.SetParameter("frost-amount", (float) m.RefractionGlossiness);
						plastic.SetParameter("polish-amount", (float) m.ReflectionGlossiness);
						plastic.SetParameter("reflectivity", (float) m.Reflectivity);
						plastic.SetParameter("transparency", (float) m.Transparency);
						crm = plastic;
						break;
					case ProbableMaterial.Metal:
						var metal = new SimpleMetalMaterial();
						metal.SetParameter("metal-color", m.ReflectionColor);
						metal.SetParameter("metal-polish", (float)m.ReflectionGlossiness);
						crm = metal;
						break;
					default:
						var dcl = m.DiffuseColor;
						var scl = m.SpecularColor;
						var rcl = m.ReflectionColor;
						var rfcl = m.TransparentColor;
						var emcl = m.EmissionColor;

						var difftex_alpha = m.AlphaTransparency;

						var col = RenderEngine.CreateFloat4(dcl.R, dcl.G, dcl.B, 255);
						var spec = RenderEngine.CreateFloat4(scl.R, scl.G, scl.B, 255);
						var refl = RenderEngine.CreateFloat4(rcl.R, rcl.G, rcl.B, 255);
						var transp = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
						var refr = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
						var emis = RenderEngine.CreateFloat4(emcl.R, emcl.G, emcl.B, 255);

						var polish = (float) m.ReflectionGlossiness*m_engine_settings.PolishFactor;
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
							Roughness = (float) m.Reflectivity, // TODO: expose roughness...
							Reflectivity = (float) m.Reflectivity,
							Transparency = (float) m.Transparency,
							Shine = (float) (m.Shine/Material.MaxShine)*2.0f,

							IsCyclesMaterial = false,

							FresnelReflections = m.FresnelReflections,

							Gamma = gamma,

							Name = m.Name ?? ""
						};

						if (rm != null)
						{
							var diffchan = rm.TextureChildSlotName(RenderMaterial.StandardChildSlots.Diffuse);
							var difftex = rm.FindChild(diffchan) as RenderTexture;
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, difftex, diffchan, RenderMaterial.StandardChildSlots.Diffuse);
							if (shader.HasDiffuseTexture)
							{
								shader.DiffuseTexture.UseAlpha = difftex_alpha;
								shader.DiffuseTexture.Amount = (float) Math.Min(rm.ChildSlotAmount(diffchan)/100.0f, 1.0f);
							}

							var bumpchan = rm.TextureChildSlotName(RenderMaterial.StandardChildSlots.Bump);
							var bumptex = rm.FindChild(bumpchan) as RenderTexture;
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, bumptex, bumpchan, RenderMaterial.StandardChildSlots.Bump);
							if (shader.HasBumpTexture)
							{
								shader.BumpTexture.Amount = (float) Math.Min(rm.ChildSlotAmount(bumpchan)/100.0f, 1.0f);
							}

							var transchan = rm.TextureChildSlotName(RenderMaterial.StandardChildSlots.Transparency);
							var transtex = rm.FindChild(transchan) as RenderTexture;
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, transtex, transchan, RenderMaterial.StandardChildSlots.Transparency);
							if (shader.HasTransparencyTexture)
							{
								shader.TransparencyTexture.Amount = (float) Math.Min(rm.ChildSlotAmount(transchan)/100.0f, 1.0f);
							}

							var envchan = rm.TextureChildSlotName(RenderMaterial.StandardChildSlots.Environment);
							var envtex = rm.FindChild(envchan) as RenderTexture;
							BitmapConverter.MaterialBitmapFromEvaluator(ref shader, rm, envtex, envchan, RenderMaterial.StandardChildSlots.Environment);
							if (shader.HasEnvironmentTexture)
							{
								shader.EnvironmentTexture.Amount = (float) Math.Min(rm.ChildSlotAmount(envchan)/100.0f, 1.0f);
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
					IsCyclesMaterial = true,
					Gamma = gamma,
					Crm = crm
				};
			}

			if(shader != null) shader.Gamma = gamma;

			return shader;
		}

		/// <summary>
		/// Convert a Rhino.Render.ChangeQueue.Light to a CyclesLight
		/// </summary>
		/// <param name="light"></param>
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
		/// <returns><c>CyclesLight</c></returns>
		internal CyclesLight ConvertLight(Rhino.Geometry.Light lg, float gamma)
		{
			var enabled = lg.IsEnabled ? 1.0 : 0.0;

			var spotangle = 0.0;
			var smooth = 0.0;
			var size = 0.0f;
			var strength = (float)(lg.Intensity * m_engine_settings.PointlightFactor * enabled);
			var axisu = new float4(0.0f);
			var axisv = new float4(0.0f);
			var use_mis = false;
			var sizeu = 0.0f;
			var sizev = 0.0f;

			var co = RenderEngine.CreateFloat4(lg.Location.X, lg.Location.Y, lg.Location.Z);
			var dir = RenderEngine.CreateFloat4(lg.Direction.X, lg.Direction.Y, lg.Direction.Z);
			var color = RenderEngine.CreateFloat4(lg.Diffuse.R, lg.Diffuse.G, lg.Diffuse.B, lg.Diffuse.A);

			var lt = LightType.Point;
			if (lg.IsDirectionalLight)
			{
				lt = LightType.Distant;
				strength = (float)(lg.Intensity * m_engine_settings.SunlightFactor * enabled);
				//size = 0.01f;
			}
			else if (lg.IsSpotLight)
			{
				lt = LightType.Spot;
				spotangle = lg.SpotAngleRadians * 2;
				smooth = 1.0 / Math.Max(lg.HotSpot, 0.001f) - 1.0;
				strength = (float)(lg.Intensity * m_engine_settings.SpotlightFactor * enabled);
			}
			else if (lg.IsRectangularLight)
			{
				lt = LightType.Area;

				strength = (float)(lg.Intensity * m_engine_settings.ArealightFactor * enabled);

				var width = lg.Width;
				var length = lg.Length;

				sizeu = (float)width.Length;
				sizev = (float)length.Length;

				size = 1.0f;

				var rect_loc = lg.Location + (lg.Width * 0.5) + (lg.Length * 0.5);

				co = RenderEngine.CreateFloat4(rect_loc.X, rect_loc.Y, rect_loc.Z);

				width.Unitize();
				length.Unitize();

				axisu = RenderEngine.CreateFloat4(width.X, width.Y, width.Z);
				axisv = RenderEngine.CreateFloat4(length.X, length.Y, length.Z);

				use_mis = true;
			}

			var clight = new CyclesLight
				{
					Type = lt,
					Co = co,
					Dir = dir,
					DiffuseColor = color,
					Size = size,

					SizeU = sizeu,
					SizeV = sizeu,

					AxisU = axisu,
					AxisV = axisv,

					UseMis = use_mis,

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
