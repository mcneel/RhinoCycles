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
using ccl;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Materials;
using RhinoCyclesCore.Converters;
using System.Collections.Generic;
using Rhino.Display;
using Pbr = Rhino.Render.RenderMaterial.PhysicallyBased.ChildSlotNames;
using RhinoCyclesCore.ExtensionMethods;
using System.Collections.Concurrent;

namespace RhinoCyclesCore
{

	/// <summary>
	/// Intermediate class to convert various Rhino shader types
	/// to Cycles shaders
	///
	/// @todo better organise shader intermediary code instead of overloading heavily
	/// </summary>
	public class CyclesShader
	{
		private ShaderBody _front;
		private ShaderBody _back;
		public CyclesShader(uint id)
		{
			Id = id;
			_front = null;
			_back = null;

		}

		/// <summary>
		/// RenderHash of the RenderMaterial for which this intermediary is created.
		/// </summary>
		public uint Id { get; }

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as CyclesShader;

			return other != null && Id.Equals(other.Id);
		}

		public bool CreateFrontShader(RenderMaterial rm, float gamma)
		{
			_front = new ShaderBody(Id);
			return CreateShaderPart(_front, rm, gamma);
		}

		/// <summary>
		/// Sets up a shader with Front set to a default ShaderBody. Used for CodeShader
		/// </summary>
		public void SetupShaderShim()
		{
			_front = new ShaderBody(Id);
		}

		public void FrontXmlShader(string name, ICyclesMaterial crm)
		{
			_front = new ShaderBody(Id)
			{
				Name = name,
				Crm = crm,
				CyclesMaterialType = ShaderBody.CyclesMaterial.Xml
			};
		}

		public bool CreateBackShader(RenderMaterial rm, float gamma)
		{
			_back = new ShaderBody(Id);
			return CreateShaderPart(_back, rm, gamma);
		}

		public ShaderBody Front => _front;
		public ShaderBody Back => _back;

		public bool DisplayMaterial => _front != null && _back != null;

		public bool ValidDisplayMaterial =>
			_front?.CyclesMaterialType != ShaderBody.CyclesMaterial.Xml
			&&
			_back?.CyclesMaterialType != ShaderBody.CyclesMaterial.Xml;

		public float Gamma { get; set; }

		private enum ProbableMaterial
		{
			Plaster,
			Picture,
			Paint,
			Glass,
			Gem,
			Plastic,
			Metal,
			Custom
		}
		private static ProbableMaterial WhatMaterial(RenderMaterial rm, Rhino.DocObjects.Material m)
		{
			if (rm.TypeId.Equals(RenderMaterial.PictureMaterialGuid))
			{
				return ProbableMaterial.Picture;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PlasterMaterialGuid))
			{
				return ProbableMaterial.Plaster;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.GlassMaterialGuid))
			{
				return ProbableMaterial.Glass;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.GemMaterialGuid))
			{
				return ProbableMaterial.Gem;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PaintMaterialGuid))
			{
				return ProbableMaterial.Paint;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.PlasticMaterialGuid))
			{
				return ProbableMaterial.Plastic;
				
			}
			if (rm.TypeId.Equals(RenderMaterial.MetalMaterialGuid))
			{
				return ProbableMaterial.Metal;
			}


			if (rm.SmellsLikePlaster || rm.SmellsLikeTexturedPlaster)
			{
				return ProbableMaterial.Plaster;
				
			}
			if (rm.SmellsLikeGlass || rm.SmellsLikeTexturedGlass)
			{
				return ProbableMaterial.Glass;
				
			}
			if (rm.SmellsLikeGem || rm.SmellsLikeTexturedGem)
			{
				return ProbableMaterial.Gem;
				
			}
			if (rm.SmellsLikePaint || rm.SmellsLikeTexturedPaint)
			{
				return ProbableMaterial.Paint;
				
			}
			if (rm.SmellsLikePlastic || rm.SmellsLikeTexturedPlastic)
			{
				return ProbableMaterial.Plastic;
				
			}
			if (rm.SmellsLikeMetal || rm.SmellsLikeTexturedMetal)
			{
				return ProbableMaterial.Metal;
			}

			return ProbableMaterial.Custom;
		}

		private bool CreateShaderPart(ShaderBody shb, RenderMaterial rm, float gamma)
		{
			var pbrbasecol = new NamedValue("pbr-base-color", rm.GetParameter("pbr-base-color"));
			if (pbrbasecol.Value != null)
			{
				// always simulate material, need to know now myself
				// what to read out from the simulated material to
				// populate my own material descriptions.
				//var m = rm.SimulatedMaterial(true);
				// figure out what type of material we are.
				//var probemat = GuessMaterialFromSmell(rm);
				//var probemat = WhatMaterial(rm, m);
				shb.IsPbr = true;
				rm.BeginChange(RenderContent.ChangeContexts.Ignore);
				shb.Name = rm.Name ?? "";
				shb.Gamma = gamma;
				rm.HandleTexturedValue(Pbr.BaseColor, shb.PbrBase);
				shb.PbrBase.Value = (shb.PbrBase.Value.ToFloat4() ^ gamma).ToColor4f();
				Utilities.HandleRenderTexture(shb.PbrBase.Texture, shb.PbrBaseTexture, false, gamma);
				rm.HandleTexturedValue(Pbr.Metallic, shb.PbrMetallic);
				Utilities.HandleRenderTexture(shb.PbrMetallic.Texture, shb.PbrMetallicTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Subsurface, shb.PbrSubsurface);
				Utilities.HandleRenderTexture(shb.PbrSubsurface.Texture, shb.PbrSubsurfaceTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.SubsurfaceScatteringColor, shb.PbrSubsurfaceColor);
				Utilities.HandleRenderTexture(shb.PbrSubsurfaceColor.Texture, shb.PbrSubsurfaceColorTexture, false, gamma);
				rm.HandleTexturedValue(Pbr.SubsurfaceScatteringRadius, shb.PbrSubsurfaceRadius);
				Utilities.HandleRenderTexture(shb.PbrSubsurfaceRadius.Texture, shb.PbrSubsurfaceRadiusTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Roughness, shb.PbrRoughness);
				Utilities.HandleRenderTexture(shb.PbrRoughness.Texture, shb.PbrRoughnessTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Specular, shb.PbrSpecular);
				Utilities.HandleRenderTexture(shb.PbrSpecular.Texture, shb.PbrSpecularTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.SpecularTint, shb.PbrSpecularTint);
				Utilities.HandleRenderTexture(shb.PbrSpecularTint.Texture, shb.PbrSpecularTintTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Anisotropic, shb.PbrAnisotropic);
				Utilities.HandleRenderTexture(shb.PbrAnisotropic.Texture, shb.PbrAnisotropicTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.AnisotropicRotation, shb.PbrAnisotropicRotation);
				Utilities.HandleRenderTexture(shb.PbrAnisotropicRotation.Texture, shb.PbrAnisotropicRotationTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Sheen, shb.PbrSheen);
				Utilities.HandleRenderTexture(shb.PbrSheen.Texture, shb.PbrSheenTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.SheenTint, shb.PbrSheenTint);
				Utilities.HandleRenderTexture(shb.PbrSheenTint.Texture, shb.PbrSheenTintTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Clearcoat, shb.PbrClearcoat);
				Utilities.HandleRenderTexture(shb.PbrClearcoat.Texture, shb.PbrClearcoatTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.ClearcoatRoughness, shb.PbrClearcoatRoughness);
				Utilities.HandleRenderTexture(shb.PbrClearcoatRoughness.Texture, shb.PbrClearcoatRoughnessTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.ClearcoatBump, shb.PbrClearcoatBump);
				Utilities.HandleRenderTexture(shb.PbrClearcoatBump.Texture, shb.PbrClearcoatBumpTexture, true, 1.0f);
				rm.HandleTexturedValue(Pbr.Opacity, shb.PbrTransmission);
				Utilities.HandleRenderTexture(shb.PbrTransmission.Texture, shb.PbrTransmissionTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.OpacityIor, shb.PbrIor);
				Utilities.HandleRenderTexture(shb.PbrIor.Texture, shb.PbrIorTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.OpacityRoughness, shb.PbrTransmissionRoughness);
				Utilities.HandleRenderTexture(shb.PbrTransmissionRoughness.Texture, shb.PbrTransmissionRoughnessTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.Emission, shb.PbrEmission);
				Utilities.HandleRenderTexture(shb.PbrEmission.Texture, shb.PbrEmissionTexture, false, gamma);
				rm.HandleTexturedValue(Pbr.Bump, shb.PbrBump);
				Utilities.HandleRenderTexture(shb.PbrBump.Texture, shb.PbrBumpTexture, true, 1.0f);
				rm.HandleTexturedValue(Pbr.Displacement, shb.PbrDisplacement);
				Utilities.HandleRenderTexture(shb.PbrDisplacement.Texture, shb.PbrDisplacementTexture, true, 1.0f);
				rm.HandleTexturedValue("smudge", shb.PbrSmudge);
				Utilities.HandleRenderTexture(shb.PbrSmudge.Texture, shb.PbrSmudgeTexture, false, 1.0f);
				rm.HandleTexturedValue("scratch", shb.PbrScratch);
				Utilities.HandleRenderTexture(shb.PbrScratch.Texture, shb.PbrScratchTexture, false, 1.0f);
				rm.HandleTexturedValue(Pbr.AmbientOcclusion, shb.PbrAmbientOcclusion);
				Utilities.HandleRenderTexture(shb.PbrAmbientOcclusion.Texture, shb.PbrAmbientOcclusionTexture, false, 1.0f);
				rm.EndChange();
			}
			else
			{
				var crm = rm as ICyclesMaterial;
				ShaderBody.CyclesMaterial mattype = ShaderBody.CyclesMaterial.No;
				if (crm == null)
				{
					// always simulate material, need to know now myself
					// what to read out from the simulated material to
					// populate my own material descriptions.
					var m = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Disallow);
					var backuprm = RenderMaterial.CreateBasicMaterial(m);
					// figure out what type of material we are.
					//var probemat = GuessMaterialFromSmell(rm);
					var probemat = WhatMaterial(rm, m);

					rm.BeginChange(RenderContent.ChangeContexts.Ignore);
					var dcl = m.DiffuseColor;
					var scl = m.SpecularColor;
					var rcl = m.ReflectionColor;
					var rfcl = m.TransparentColor;
					var emcl = m.EmissionColor;
					var reflectivity = (float)m.Reflectivity;
					var metalic = 0f;
					var shine = (float)(m.Shine / Material.MaxShine);

					switch (probemat)
					{
						case ProbableMaterial.Plaster:
							mattype = ShaderBody.CyclesMaterial.Diffuse;
							break;
						case ProbableMaterial.Glass:
						case ProbableMaterial.Gem:
							metalic = 0f;
							mattype = m.IndexOfRefraction < 1.001 ? ShaderBody.CyclesMaterial.Diffuse : ShaderBody.CyclesMaterial.Glass;
							break;
						case ProbableMaterial.Metal:
							metalic = 1.0f;
							mattype = ShaderBody.CyclesMaterial.SimpleMetal;
							break;
						case ProbableMaterial.Plastic:
							//polish = reflectivity;
							//shine = polish;
							//reflectivity = 0f;
							metalic = 0f;
							mattype = ShaderBody.CyclesMaterial.SimplePlastic;
							break;
						case ProbableMaterial.Paint:
							mattype = ShaderBody.CyclesMaterial.Paint;
							break;
						case ProbableMaterial.Custom:
							mattype = ShaderBody.CyclesMaterial.No;
							break;
					}

					var difftexAlpha = m.AlphaTransparency;

					var col = RenderEngine.CreateFloat4(dcl.R, dcl.G, dcl.B, 255);
					var spec = RenderEngine.CreateFloat4(scl.R, scl.G, scl.B, 255);
					var refl = RenderEngine.CreateFloat4(rcl.R, rcl.G, rcl.B, 255);
					var transp = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
					var refr = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
					var emis = RenderEngine.CreateFloat4(emcl.R, emcl.G, emcl.B, 255);

					//shb.Type = CyclesShader.Shader.Diffuse,
					shb.CyclesMaterialType = mattype;

					shb.Shadeless = m.DisableLighting;

					shb.DiffuseColor = col;
					shb.SpecularColor = spec;
					shb.ReflectionColor = refl;
					shb.ReflectionRoughness = (float)m.ReflectionGlossiness;
					shb.RefractionColor = refr;
					shb.RefractionRoughness = (float)m.RefractionGlossiness;
					shb.TransparencyColor = transp;
					shb.EmissionColor = emis;

					var transp_used = m.Transparency > 0.001;

					shb.FresnelIOR = (float)m.FresnelIndexOfRefraction;
					shb.IOR = transp_used ? (float)m.IndexOfRefraction : 1.0f;
					shb.Roughness = (float)m.ReflectionGlossiness;
					shb.Reflectivity = reflectivity;
					shb.Metallic = metalic;
					shb.Transparency = (float)m.Transparency;
					shb.Shine = shine;
					shb.Gloss = (float)m.ReflectionGlossiness;

					shb.FresnelReflections = m.FresnelReflections;

					shb.Gamma = gamma;

					shb.Name = m.Name ?? "";

					shb.DiffuseTexture.Amount = 0.0f;
					shb.BumpTexture.Amount = 0.0f;
					shb.TransparencyTexture.Amount = 0.0f;
					shb.EnvironmentTexture.Amount = 0.0f;

					if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Diffuse) || backuprm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Diffuse))
					{
						var useorig = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Diffuse) != null;
						var difftex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Diffuse) ?? backuprm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Diffuse);
						BitmapConverter.MaterialBitmapFromEvaluator(ref shb, difftex, RenderMaterial.StandardChildSlots.Diffuse);
						if (shb.HasDiffuseTexture)
						{
							shb.CyclesMaterialType = ShaderBody.CyclesMaterial.No;
							shb.DiffuseTexture.UseAlpha = difftexAlpha;
							shb.DiffuseTexture.Amount = (float)Math.Min((useorig ? rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Diffuse) : backuprm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Diffuse)) / 100.0f, 1.0f);
						}
					}

					if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Bump) || backuprm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Bump))
					{
						var useorig = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump) != null;
						var bumptex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump) ?? backuprm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump);
						BitmapConverter.MaterialBitmapFromEvaluator(ref shb, bumptex, RenderMaterial.StandardChildSlots.Bump);
						if (shb.HasBumpTexture)
						{
							shb.BumpTexture.Amount = (float)Math.Min((useorig ? rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Bump) : backuprm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Bump)) / 100.0f, 1.0f);
						}
					}

					if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Transparency) || backuprm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Transparency))
					{
						var useorig = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Transparency) != null;
						var transtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Transparency) ?? backuprm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Transparency);
						BitmapConverter.MaterialBitmapFromEvaluator(ref shb, transtex,
							RenderMaterial.StandardChildSlots.Transparency);
						if (shb.HasTransparencyTexture)
						{
							shb.TransparencyTexture.Amount = (float)Math.Min((useorig ? rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Transparency) : backuprm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Transparency)) / 100.0f, 1.0f);
						}
					}

					if (rm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Environment) || backuprm.GetTextureOnFromUsage(RenderMaterial.StandardChildSlots.Environment))
					{
						var useorig = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Environment) != null;
						var envtex = rm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Environment) ?? backuprm.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Environment);
						BitmapConverter.MaterialBitmapFromEvaluator(ref shb, envtex,
							RenderMaterial.StandardChildSlots.Environment);
						if (shb.HasEnvironmentTexture)
						{
							shb.EnvironmentTexture.Amount = (float)Math.Min((useorig ? rm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Environment) : backuprm.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Environment)) / 100.0f, 1.0f);
						}
					}

					rm.EndChange();
				}
				else
				{
					crm.Gamma = gamma;
					crm.BakeParameters();
					if (crm.MaterialType == ShaderBody.CyclesMaterial.CustomRenderMaterial)
					{
						shb.Crm = crm;
						shb.CyclesMaterialType = ShaderBody.CyclesMaterial.CustomRenderMaterial;
						shb.Gamma = gamma;
						shb.Name = rm.Name ?? "Cycles custom material";
					}
					else
					{
						crm.Gamma = gamma;
						crm.BakeParameters();
						shb.Crm = crm;
						shb.CyclesMaterialType = ShaderBody.CyclesMaterial.Xml;
						shb.Gamma = gamma;
						shb.Name = rm.Name ?? "some cycles material";
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Type of shader this represents.
		/// </summary>
		public enum Shader
		{
			Background,
			Diffuse
		}

		public Shader Type { get; set; }

		/// <summary>
		/// A shader should override this function if it needs to reload textures.
		/// 
		/// Textures change after i.e. gamma changes.
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="floats"></param>
		public void ReloadTextures(ConcurrentDictionary<uint, ByteBitmap> bytes, ConcurrentDictionary<uint, FloatBitmap> floats)
		{
			_front?.ReloadTextures(bytes, floats);
			_back?.ReloadTextures(bytes, floats);
		}

	}

	public class ShaderBody
	{
		#region PBR style parameters
		public bool IsPbr { get; set; }

		public TexturedColor PbrBase = new TexturedColor(Pbr.BaseColor, Color4f.White, false, 0.0f);
		public CyclesTextureImage PbrBaseTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrMetallic = new TexturedFloat(Pbr.Metallic, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrMetallicTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSubsurface = new TexturedFloat(Pbr.Subsurface, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceTexture = new CyclesTextureImage();

		/*****/

		public TexturedColor PbrSubsurfaceColor= new TexturedColor(Pbr.SubsurfaceScatteringColor, Color4f.White, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceColorTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSubsurfaceRadius = new TexturedFloat(Pbr.SubsurfaceScatteringRadius, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceRadiusTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSpecular = new TexturedFloat(Pbr.Specular, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSpecularTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSpecularTint = new TexturedFloat(Pbr.SpecularTint, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSpecularTintTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrRoughness = new TexturedFloat(Pbr.Roughness, 0.0f, false, 1.0f);
		public CyclesTextureImage PbrRoughnessTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrAnisotropic = new TexturedFloat(Pbr.Anisotropic, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAnisotropicTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrAnisotropicRotation = new TexturedFloat(Pbr.AnisotropicRotation, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAnisotropicRotationTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSheen = new TexturedFloat(Pbr.Sheen, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSheenTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSheenTint = new TexturedFloat(Pbr.SheenTint, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSheenTintTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrClearcoat = new TexturedFloat(Pbr.Clearcoat, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrClearcoatTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrClearcoatRoughness = new TexturedFloat(Pbr.ClearcoatRoughness, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrClearcoatRoughnessTexture = new CyclesTextureImage();

		/*****/

		public TexturedColor PbrClearcoatBump = new TexturedColor(Pbr.ClearcoatBump, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrClearcoatBumpTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrIor = new TexturedFloat(Pbr.OpacityIor, 1.0f, false, 0.0f);
		public CyclesTextureImage PbrIorTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrTransmission = new TexturedFloat(Pbr.Opacity, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrTransmissionTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrTransmissionRoughness = new TexturedFloat(Pbr.OpacityRoughness, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrTransmissionRoughnessTexture = new CyclesTextureImage();

		public TexturedFloat PbrAmbientOcclusion = new TexturedFloat(Pbr.AmbientOcclusion, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAmbientOcclusionTexture = new CyclesTextureImage();

		public TexturedColor PbrEmission = new TexturedColor(Pbr.Emission, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrEmissionTexture = new CyclesTextureImage();
		public TexturedColor PbrBump = new TexturedColor(Pbr.Bump, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrBumpTexture = new CyclesTextureImage();
		public TexturedColor PbrDisplacement = new TexturedColor(Pbr.Displacement, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrDisplacementTexture = new CyclesTextureImage();

		public TexturedFloat PbrSmudge = new TexturedFloat("smudge", 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSmudgeTexture = new CyclesTextureImage();
		public TexturedFloat PbrScratch = new TexturedFloat("scratch", 0.0f, false, 0.0f);
		public CyclesTextureImage PbrScratchTexture = new CyclesTextureImage();

		#endregion

		public uint Id { get; }

		public ShaderBody(uint id)
		{
			Id = id;
			DiffuseTexture = new CyclesTextureImage();
			BumpTexture = new CyclesTextureImage();
			TransparencyTexture = new CyclesTextureImage();
			EnvironmentTexture = new CyclesTextureImage();
			GiEnvTexture = new CyclesTextureImage();
			BgEnvTexture = new CyclesTextureImage();
			ReflRefrEnvTexture = new CyclesTextureImage();
			CyclesMaterialType = CyclesMaterial.No;
		}
		public ICyclesMaterial Crm { get; set; }
		/// <summary>
		/// Set the CyclesMaterial type
		/// </summary>
		public CyclesMaterial CyclesMaterialType { get; set; }

		/// <summary>
		/// Enumeration of Cycles custom materials.
		/// 
		/// Note: don't forget to update this enumeration for each
		/// custom material that is added.
		///
		/// Enumeration for both material and background (world)
		/// shaders.
		/// </summary>
		public enum CyclesMaterial
		{
			/// <summary>
			/// No is used when the material isn't a Cycles material
			/// </summary>
			No,

			Xml,
			CustomRenderMaterial,

			Brick,
			Test,
			FlakedCarPaint,
			BrickCheckeredMortar,
			Translucent,
			PhongTest,

			Glass,
			Diffuse,
			Paint,
			SimplePlastic,
			SimpleMetal,
			Emissive,
			VertexColor,

			SimpleNoiseEnvironment,
			XmlEnvironment,
		}

		/// <summary>
		/// A shader should override this function if it needs to reload textures.
		/// 
		/// Textures change after i.e. gamma changes.
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="floats"></param>
		public void ReloadTextures(ConcurrentDictionary<uint, ByteBitmap> bytes, ConcurrentDictionary<uint, FloatBitmap> floats)
		{

			if (HasDiffuseTexture)
			{
				LoadOneTexture(DiffuseTexture, bytes, floats);
			}
			if (HasBumpTexture)
			{
				LoadOneTexture(BumpTexture, bytes, floats);
			}
			if (HasTransparencyTexture)
			{
				LoadOneTexture(TransparencyTexture, bytes, floats);
			}
			if (HasEnvironmentTexture)
			{
				LoadOneTexture(EnvironmentTexture, bytes, floats);
			}
			if (HasGiEnvTexture)
			{
				LoadOneTexture(GiEnvTexture, bytes, floats);
			}
			if (HasBgEnvTexture)
			{
				LoadOneTexture(BgEnvTexture, bytes, floats);
			}
			if (HasReflRefrEnvTexture)
			{
				LoadOneTexture(ReflRefrEnvTexture, bytes, floats);
			}
		}

		static private void LoadOneTexture(CyclesTextureImage tex, ConcurrentDictionary<uint, ByteBitmap> bytes, ConcurrentDictionary<uint, FloatBitmap> floats)
		{
			if (uint.TryParse(tex.Name, out uint rid))
			{
				if (tex.HasByteImage)
				{
					if (bytes.ContainsKey(rid))
					{
						tex.TexByte = bytes[rid].Data;
					}
				}
				else if (tex.HasFloatImage)
				{
					if (floats.ContainsKey(rid))
					{
						tex.TexFloat = floats[rid].Data;
					}
				}
			}
		}

		/// <summary>
		/// Set to true if a shadeless effect is wanted (self-illuminating).
		/// </summary>
		public bool Shadeless { get; set; }
		/// <summary>
		/// Get <c>Shadeless</c> as a float value
		/// </summary>
		public float ShadelessAsFloat => Shadeless ? 1.0f : 0.0f;

		/// <summary>
		/// Gamma corrected base color
		/// </summary>
		public float4 BaseColor
		{
			get
			{
				float4 c = DiffuseColor;
				switch (CyclesMaterialType)
				{
					case CyclesMaterial.SimpleMetal:
						c = ReflectionColor;
						break;
					case CyclesMaterial.Glass:
						c = TransparencyColor;
						break;
					default:
						c = DiffuseColor;
						break;
				}

				var gcc = c ^ Gamma;

				return gcc;
			}
		}

		public float4 DiffuseColor { get; set; } = new float4();

		public bool HasOnlyDiffuseColor => !HasDiffuseTexture
		                                   && !HasBumpTexture
		                                   && !HasTransparencyTexture
		                                   && !HasEmission
		                                   && !Shadeless
		                                   && NoTransparency
		                                   && NoReflectivity;

		public bool HasOnlyDiffuseTexture => HasDiffuseTexture
		                                     && !HasBumpTexture
		                                     && !HasTransparencyTexture
		                                     && !HasEmission
		                                     && !Shadeless
		                                     && NoTransparency
		                                     && NoReflectivity;

		public bool DiffuseAndBumpTexture => HasDiffuseTexture
		                                     && HasBumpTexture
		                                     && !HasTransparencyTexture
		                                     && !HasEmission
		                                     && !Shadeless
		                                     && NoTransparency
		                                     && NoReflectivity;

		public bool HasOnlyReflectionColor => HasReflectivity
		                                      && !HasDiffuseTexture
		                                      && !HasEmission
		                                      && !Shadeless
		                                      && NoTransparency
		                                      && !HasTransparency
		                                      && !HasBumpTexture;

		public float4 SpecularColor { get; set; } = new float4();
		public float4 SpecularColorGamma => SpecularColor ^ Gamma;
		public float4 ReflectionColor { get; set; } = new float4();
		public float4 ReflectionColorGamma => ReflectionColor ^ Gamma;
		public float ReflectionRoughness { get; set; }
		public float ReflectionRoughnessPow2 => ReflectionRoughness * ReflectionRoughness;
		public float4 RefractionColor { get; set; } = new float4();
		public float4 RefractionColorGamma => RefractionColor ^ Gamma;
		public float RefractionRoughness { get; set; }
		public float RefractionRoughnessPow2 => RefractionRoughness * RefractionRoughness;
		public float4 TransparencyColor { get; set; } = new float4();
		public float4 TransparencyColorGamma => TransparencyColor ^ Gamma;
		public float4 EmissionColor { get; set; } = new float4();
		public float4 EmissionColorGamma => EmissionColor ^ Gamma;
		public bool HasEmission => !EmissionColor.IsZero(false);

		public CyclesTextureImage DiffuseTexture { get; set; }
		public bool HasDiffuseTexture => DiffuseTexture.HasTextureImage;
		public float HasDiffuseTextureAsFloat => HasDiffuseTexture ? 1.0f : 0.0f;
		public CyclesTextureImage BumpTexture { get; set; }
		public bool HasBumpTexture => BumpTexture.HasTextureImage;
		public float HasBumpTextureAsFloat => HasBumpTexture ? 1.0f : 0.0f;
		public CyclesTextureImage TransparencyTexture { get; set; }
		public bool HasTransparencyTexture => TransparencyTexture.HasTextureImage;
		public float HasTransparencyTextureAsFloat => HasTransparencyTexture ? 1.0f : 0.0f;
		public CyclesTextureImage EnvironmentTexture { get; set; }
		public bool HasEnvironmentTexture => EnvironmentTexture.HasTextureImage;
		public float HasEnvironmentTextureAsFloat => HasEnvironmentTexture ? 1.0f : 0.0f;

		public CyclesTextureImage GiEnvTexture { get; set; }
		public bool HasGiEnvTexture => GiEnvTexture.HasTextureImage;
		public float4 GiEnvColor { get; set; } = new float4();
		public bool HasGiEnv => HasGiEnvTexture || GiEnvColor != null;

		public CyclesTextureImage BgEnvTexture { get; set; }
		public bool HasBgEnvTexture => BgEnvTexture.HasTextureImage;
		public float4 BgEnvColor { get; set; } = new float4();
		public bool HasBgEnv => HasBgEnvTexture || BgEnvColor != null;

		public CyclesTextureImage ReflRefrEnvTexture { get; set; }
		public bool HasReflRefrEnvTexture => ReflRefrEnvTexture.HasTextureImage;
		public float4 ReflRefrEnvColor { get; set; } = new float4();
		public bool HasReflRefrEnv => HasReflRefrEnvTexture || ReflRefrEnvColor != null;

		public bool HasUV { get; set; }

		public float FresnelIOR { get; set; }
		public float IOR { get; set; }
		public float Roughness { get; set; }
		public float Reflectivity { get; set; }
		public float Specular => Reflectivity;
		public float SpecularTint => ReflectivityInverse;
		public float ReflectivityInverse => 1.0f - Reflectivity;
		public float Sheen => Reflectivity;
		public float SheenTint => ReflectivityInverse;
		public float ClearCoat => NoMetalic ? Reflectivity : 0.0f;
		public float ClearCoatGloss => ClearCoat;
		public float Metallic { get; set; }
		public bool NoMetalic => Math.Abs(Metallic) < 0.00001f;
		public float Shine { get; set; }
		public float Gloss { get; set; }
		public float Transparency { get; set; }
		public bool NoTransparency => Math.Abs(Transparency) < 0.00001f;
		public bool HasTransparency => !NoTransparency;
		public bool NoReflectivity => Math.Abs(Reflectivity) < 0.00001f;
		public bool HasReflectivity => !NoReflectivity;

		private float m_gamma;
		public float Gamma
		{
			get { return m_gamma; }
			set
			{
				m_gamma = value;
				if (Crm != null)
				{
					Crm.Gamma = m_gamma;
				}
			}
		}

		public bool FresnelReflections { get; set; }
		public float FresnelReflectionsAsFloat => FresnelReflections ? 1.0f : 0.0f;

		public string Name { get; set; }
		
	}
}
