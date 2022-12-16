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
using ccl;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Materials;
using RhinoCyclesCore.Converters;
using System.Collections.Generic;
using Rhino.Display;
using PbrCSN = Rhino.Render.ChildSlotNames.PhysicallyBased;
using StdCS = Rhino.Render.RenderMaterial.StandardChildSlots;
using RhinoCyclesCore.ExtensionMethods;
using System.Collections.Concurrent;
using Rhino.Runtime.InteropWrappers;
using System.Linq;

namespace RhinoCyclesCore
{

	/// <summary>
	/// Intermediate class to convert various Rhino shader types
	/// to Cycles shaders
	///
	/// @todo better organise shader intermediary code instead of overloading heavily
	/// </summary>
	public class CyclesShader : IDisposable
	{
		private ShaderBody _front;
		private ShaderBody _back;
		private BitmapConverter _bitmapConverter;
		private bool disposedValue;

		public CyclesShader(uint id, BitmapConverter bitmapConverter)
		{
			Id = id;
			_front = null;
			_back = null;
			_bitmapConverter = bitmapConverter;
		}

		public List<CyclesDecal> Decals { get; set; } = null;

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

		public bool InvisibleUnderside { get; set; } = false;

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

		private void HandleCustomTexture(RenderMaterial.StandardChildSlots childSlot, ShaderBody shb, CyclesTextureImage ti, RenderMaterial rm, bool checkForNormal)
		{
			var texture = rm.GetTextureFromUsage(childSlot);
			float amount = 0.0f;
			bool enabled = false;

			if (null != texture)
			{
				enabled = rm.GetTextureOnFromUsage(childSlot);
				if (enabled)
				{
					amount = (float)Math.Min(rm.GetTextureAmountFromUsage(childSlot) / 100.0f, 1.0f);
				} else {
					texture = null;
				}
			}
			else
			{
				//In all other cases, we have to simulate the material and use the textures as presented
				//to us in the simulation.
				var on_material = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Allow);
				var on_texture = on_material.GetTexture(RenderMaterial.TextureTypeFromSlot(childSlot));

				if (null != on_texture && on_texture.Enabled)
				{
					//Note that the simulated texture is created with the RenderMaterial's document association
					//so that when the new bitmap texture is created below, the WCS transforms are not applied.
					var simtex = new SimulatedTexture(rm.DocumentAssoc, on_texture);

					texture = RenderTexture.NewBitmapTexture(simtex, rm.DocumentAssoc);

					//Always use the actual values given by the simualtion at this point.
					on_texture.GetAlphaBlendValues(out double c, out double a0, out double a1, out double a2, out double a3);

					amount = (float)c;
				}
			}
			/*if(texture!=null && enabled) {
				Utilities.HandleRenderTexture(texture, ti, checkForNormal, _bitmapConverter, shb.Gamma);
				ti.Amount = amount;
			}*/
		}

		private void CreateCustomShaderPart(ShaderBody shb, RenderMaterial rm, float gamma)
		{
			var onMaterial = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Allow);

			// figure out what type of material we are.
			var probemat = WhatMaterial(rm, onMaterial);

			ShaderBody.CyclesMaterial mattype = ShaderBody.CyclesMaterial.No;

			var dcl = onMaterial.DiffuseColor;
			var scl = onMaterial.SpecularColor;
			var rcl = onMaterial.ReflectionColor;
			var rfcl = onMaterial.TransparentColor;
			var emcl = onMaterial.EmissionColor;
			var reflectivity = (float)onMaterial.Reflectivity;
			var metalic = 0f;
			var shine = (float)(onMaterial.Shine / Material.MaxShine);

			switch (probemat)
			{
				case ProbableMaterial.Plaster:
					mattype = ShaderBody.CyclesMaterial.Diffuse;
					break;
				case ProbableMaterial.Glass:
				case ProbableMaterial.Gem:
					metalic = 0f;
					mattype = onMaterial.IndexOfRefraction < 1.001 ? ShaderBody.CyclesMaterial.Diffuse : ShaderBody.CyclesMaterial.Glass;
					break;
				case ProbableMaterial.Metal:
					metalic = 1.0f;
					mattype = ShaderBody.CyclesMaterial.SimpleMetal;
					break;
				case ProbableMaterial.Plastic:
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

			var difftexAlpha = onMaterial.AlphaTransparency;

			var col = RenderEngine.CreateFloat4(dcl.R, dcl.G, dcl.B, 255);
			var spec = RenderEngine.CreateFloat4(scl.R, scl.G, scl.B, 255);
			var refl = RenderEngine.CreateFloat4(rcl.R, rcl.G, rcl.B, 255);
			var transp = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
			var refr = RenderEngine.CreateFloat4(rfcl.R, rfcl.G, rfcl.B, 255);
			var emis = RenderEngine.CreateFloat4(emcl.R, emcl.G, emcl.B, 255);

			//shb.Type = CyclesShader.Shader.Diffuse,
			shb.CyclesMaterialType = mattype;

			shb.Shadeless = onMaterial.DisableLighting;

			shb.DiffuseColor = col;
			shb.SpecularColor = spec;
			shb.ReflectionColor = refl;
			shb.ReflectionRoughness = (float)onMaterial.ReflectionGlossiness;
			shb.RefractionColor = refr;
			shb.RefractionRoughness = (float)onMaterial.RefractionGlossiness;
			shb.TransparencyColor = transp;
			shb.EmissionColor = emis;

			var transp_used = onMaterial.Transparency > 0.001;

			shb.FresnelIOR = (float)onMaterial.FresnelIndexOfRefraction;
			shb.IOR = transp_used ? (float)onMaterial.IndexOfRefraction : 1.0f;
			shb.Roughness = (float)onMaterial.ReflectionGlossiness;
			shb.Reflectivity = reflectivity;
			shb.Metallic = metalic;
			shb.Transparency = (float)onMaterial.Transparency;
			shb.Shine = shine;
			shb.Gloss = (float)onMaterial.ReflectionGlossiness;

			shb.FresnelReflections = onMaterial.FresnelReflections;

			shb.Gamma = gamma;

			shb.Name = onMaterial.Name ?? "";

			shb.DiffuseTexture.Amount = 0.0f;
			shb.BumpTexture.Amount = 0.0f;
			shb.TransparencyTexture.Amount = 0.0f;
			shb.EnvironmentTexture.Amount = 0.0f;

			HandleCustomTexture(StdCS.Diffuse, shb, shb.DiffuseTexture, rm, false);
			if (shb.HasDiffuseTexture)
			{
				shb.DiffuseTexture.UseAlpha = difftexAlpha;
			} else {
				shb.DiffuseTexture.UseAlpha = false;
			}
			HandleCustomTexture(StdCS.Bump, shb, shb.BumpTexture, rm, true);
			HandleCustomTexture(StdCS.Transparency, shb, shb.TransparencyTexture, rm, false);
			HandleCustomTexture(StdCS.Environment, shb, shb.EnvironmentTexture, rm, false);
		}

		void HandlePbrTexturedProperty<T>(RenderMaterial.StandardChildSlots childSlot, T v, RenderMaterial rm, TexturedValue<T> tv, CyclesTextureImage cti, float gamma = 1.0f)
		{
			tv.Value = v;

			//If we manage to get a texture from the usage, that means that either the material supports handing
			//off actual textures for specific usages, or it's actually a Rhino Physically Based material.
			RenderTexture texture = rm.GetTextureFromUsage(childSlot);

			if (null != texture)
			{
				tv.On = rm.GetTextureOnFromUsage(childSlot);

				if (tv.On)
				{
					tv.Amount = (float)rm.GetTextureAmountFromUsage(childSlot) / 100.0f;
					tv.Texture = texture;
				}
			}
			else
			{
				// TODO make this work with Procedurals
				//In all other cases, we have to simulate the material and use the textures as presented
				//to us in the simulation.  A good example of this is Substance - which doesn't actually
				//have any children, but it fills out the textures slots of an ON_Material in response to
				//simulate material.
				var mat = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Allow);
				var tex = mat.GetTexture(RenderMaterial.TextureTypeFromSlot(childSlot));

				if (null != tex)
				{
					//Always use the actual values given by the simualtion at this point.
					double c, a0, a1, a2, a3;
					tex.GetAlphaBlendValues(out c, out a0, out a1, out a2, out a3);

					tv.Amount = (float)c;
					tv.On = tex.Enabled;

					//Note that the simulated texture is created with the RenderMaterial's document association
					//so that when the new bitmap texture is created below, the WCS transforms are not applied.
					var simtex = new SimulatedTexture(rm.DocumentAssoc, tex);

					tv.Texture = RenderTexture.NewBitmapTexture(simtex, rm.DocumentAssoc);
				}
			}

			bool checkForNormal = childSlot == StdCS.Bump || childSlot == StdCS.PbrClearcoatBump || childSlot == StdCS.PbrDisplacement;

			Utilities.HandleRenderTexture(tv.Texture, cti, checkForNormal, false, _bitmapConverter, gamma);
		}

		private void CreatePbrShaderPart(ShaderBody shb, RenderMaterial rm, float gamma)
		{
			var pbrmat = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Allow).PhysicallyBased;

			shb.IsPbr = true;
			shb.Name = rm.Name ?? "";
			shb.Gamma = gamma;
			shb.UseBaseColorTextureAlphaAsObjectAlpha = pbrmat.UseBaseColorTextureAlphaForObjectAlphaTransparencyTexture;

			HandlePbrTexturedProperty(StdCS.PbrBaseColor, pbrmat.BaseColor.ApplyGamma(gamma), rm, shb.PbrBase, shb.PbrBaseTexture, gamma);
			HandlePbrTexturedProperty(StdCS.PbrSubSurfaceScattering, pbrmat.SubsurfaceScatteringColor.ApplyGamma(gamma), rm, shb.PbrSubsurfaceColor, shb.PbrSubsurfaceColorTexture, gamma);
			HandlePbrTexturedProperty(StdCS.PbrEmission, pbrmat.Emission.ApplyGamma(gamma), rm, shb.PbrEmission, shb.PbrEmissionTexture, gamma);

			HandlePbrTexturedProperty(StdCS.PbrMetallic,           (float)pbrmat.Metallic,            rm, shb.PbrMetallic,            shb.PbrMetallicTexture);
			HandlePbrTexturedProperty(StdCS.PbrSubsurface,         (float)pbrmat.Subsurface,          rm, shb.PbrSubsurface,          shb.PbrSubsurfaceTexture);
			HandlePbrTexturedProperty(StdCS.PbrSubsurfaceScatteringRadius, (float)pbrmat.SubsurfaceScatteringRadius, rm, shb.PbrSubsurfaceRadius, shb.PbrSubsurfaceRadiusTexture);
			HandlePbrTexturedProperty(StdCS.PbrRoughness,          (float)pbrmat.Roughness,           rm, shb.PbrRoughness,           shb.PbrRoughnessTexture);
			HandlePbrTexturedProperty(StdCS.PbrSpecular,           (float)pbrmat.Specular,            rm, shb.PbrSpecular,            shb.PbrSpecularTexture);
			HandlePbrTexturedProperty(StdCS.PbrSpecularTint,       (float)pbrmat.SpecularTint,        rm, shb.PbrSpecularTint,        shb.PbrSpecularTintTexture);
			HandlePbrTexturedProperty(StdCS.PbrAnisotropic,        (float)pbrmat.Anisotropic,         rm, shb.PbrAnisotropic,         shb.PbrAnisotropicTexture);
			HandlePbrTexturedProperty(StdCS.PbrAnisotropicRotation,(float)pbrmat.AnisotropicRotation, rm, shb.PbrAnisotropicRotation, shb.PbrAnisotropicRotationTexture);
			HandlePbrTexturedProperty(StdCS.PbrSheen,              (float)pbrmat.Sheen,               rm, shb.PbrSheen,               shb.PbrSheenTexture);
			HandlePbrTexturedProperty(StdCS.PbrSheenTint,          (float)pbrmat.SheenTint,           rm, shb.PbrSheenTint,           shb.PbrSheenTintTexture);
			HandlePbrTexturedProperty(StdCS.PbrClearcoat,          (float)pbrmat.Clearcoat,           rm, shb.PbrClearcoat,           shb.PbrClearcoatTexture);
			HandlePbrTexturedProperty(StdCS.PbrClearcoatRoughness, (float)pbrmat.ClearcoatRoughness,  rm, shb.PbrClearcoatRoughness,  shb.PbrClearcoatRoughnessTexture);
			HandlePbrTexturedProperty(StdCS.PbrClearcoatBump,      Color4f.Black,                     rm, shb.PbrClearcoatBump,       shb.PbrClearcoatBumpTexture);
			HandlePbrTexturedProperty(StdCS.PbrOpacity,            (float)pbrmat.Opacity,             rm, shb.PbrTransmission,        shb.PbrTransmissionTexture);
			HandlePbrTexturedProperty(StdCS.PbrOpacityIor,         (float)pbrmat.OpacityIOR,          rm, shb.PbrIor,                 shb.PbrIorTexture);
			HandlePbrTexturedProperty(StdCS.PbrOpacityRoughness,   (float)pbrmat.OpacityRoughness,    rm, shb.PbrTransmissionRoughness, shb.PbrTransmissionRoughnessTexture);
			HandlePbrTexturedProperty(StdCS.Bump,                  Color4f.Black,                     rm, shb.PbrBump,                shb.PbrBumpTexture);
			HandlePbrTexturedProperty(StdCS.PbrDisplacement,       Color4f.Black,                     rm, shb.PbrDisplacement,        shb.PbrDisplacementTexture);
			HandlePbrTexturedProperty(StdCS.PbrAmbientOcclusion,   0.0f,                              rm, shb.PbrAmbientOcclusion,    shb.PbrAmbientOcclusionTexture);
			HandlePbrTexturedProperty(StdCS.PbrAlpha,              (float)pbrmat.Alpha,               rm, shb.PbrAlpha,               shb.PbrAlphaTexture);
		}

		private void CreateCyclesShaderPart(ShaderBody shb, ICyclesMaterial cyclesMaterial, string name, float gamma)
		{
			cyclesMaterial.Gamma = gamma;
			cyclesMaterial.BitmapConverter = _bitmapConverter;
			cyclesMaterial.BakeParameters(_bitmapConverter);
			if (cyclesMaterial.MaterialType == ShaderBody.CyclesMaterial.CustomRenderMaterial)
			{
				shb.Crm = cyclesMaterial;
				shb.CyclesMaterialType = ShaderBody.CyclesMaterial.CustomRenderMaterial;
				shb.Gamma = gamma;
				shb.Name = name ?? "Cycles custom material";
			}
			else
			{
				cyclesMaterial.Gamma = gamma;
				cyclesMaterial.BakeParameters(_bitmapConverter);
				shb.Crm = cyclesMaterial;
				shb.CyclesMaterialType = ShaderBody.CyclesMaterial.Xml;
				shb.Gamma = gamma;
				shb.Name = name ?? "some cycles material";
			}
		}

		private bool CreateShaderPart(ShaderBody shb, RenderMaterial rm, float gamma)
		{
			if (null == rm)
				return false;

			//The only materials we deal with "natively" are Cycles materials.
			//So special case those
			if (null != rm as ICyclesMaterial)
			{
				CreateCyclesShaderPart(shb, rm as ICyclesMaterial, rm.Name, gamma);
				return true;
			}

			//Otherwise, we are always going to look at the simulation values.
			//However, in the interests of keeping textures as complete as possible (HDR stays HDR, for example)
			//the CreateXXXShaderPart will look at the actual textures in the RenderMaterial first.
			//Even so, at this point, we can decide which shader we're going to create based on whether it simulates
			//as a PBR or not.
			bool isPbr = rm.SimulatedMaterial(RenderTexture.TextureGeneration.Skip).IsPhysicallyBased;

			if (isPbr)
			{
				CreatePbrShaderPart(shb, rm, gamma);
			}
			else
			{
				CreateCustomShaderPart(shb, rm, gamma);
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

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					_front?.Dispose();
					_back?.Dispose();
				}

				_front = null;
				_back = null;
				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}

	public class ShaderBody : IDisposable
	{
		#region PBR style parameters
		public bool IsPbr { get; set; }

		public TexturedColor PbrBase = new TexturedColor(PbrCSN.BaseColor, Color4f.White, false, 0.0f);
		public CyclesTextureImage PbrBaseTexture = new CyclesTextureImage();


		/*****/

		public TexturedFloat PbrMetallic = new TexturedFloat(PbrCSN.Metallic, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrMetallicTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSubsurface = new TexturedFloat(PbrCSN.Subsurface, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceTexture = new CyclesTextureImage();

		/*****/

		public TexturedColor PbrSubsurfaceColor= new TexturedColor(PbrCSN.SubsurfaceScatteringColor, Color4f.White, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceColorTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSubsurfaceRadius = new TexturedFloat(PbrCSN.SubsurfaceScatteringRadius, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSubsurfaceRadiusTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSpecular = new TexturedFloat(PbrCSN.Specular, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSpecularTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSpecularTint = new TexturedFloat(PbrCSN.SpecularTint, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSpecularTintTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrRoughness = new TexturedFloat(PbrCSN.Roughness, 0.0f, false, 1.0f);
		public CyclesTextureImage PbrRoughnessTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrAnisotropic = new TexturedFloat(PbrCSN.Anisotropic, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAnisotropicTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrAnisotropicRotation = new TexturedFloat(PbrCSN.AnisotropicRotation, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAnisotropicRotationTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSheen = new TexturedFloat(PbrCSN.Sheen, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSheenTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrSheenTint = new TexturedFloat(PbrCSN.SheenTint, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrSheenTintTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrClearcoat = new TexturedFloat(PbrCSN.Clearcoat, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrClearcoatTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrClearcoatRoughness = new TexturedFloat(PbrCSN.ClearcoatRoughness, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrClearcoatRoughnessTexture = new CyclesTextureImage();

		/*****/

		public TexturedColor PbrClearcoatBump = new TexturedColor(PbrCSN.ClearcoatBump, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrClearcoatBumpTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrIor = new TexturedFloat(PbrCSN.OpacityIor, 1.0f, false, 0.0f);
		public CyclesTextureImage PbrIorTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrTransmission = new TexturedFloat(PbrCSN.Opacity, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrTransmissionTexture = new CyclesTextureImage();

		/*****/

		public TexturedFloat PbrTransmissionRoughness = new TexturedFloat(PbrCSN.OpacityRoughness, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrTransmissionRoughnessTexture = new CyclesTextureImage();

		public TexturedFloat PbrAmbientOcclusion = new TexturedFloat(PbrCSN.AmbientOcclusion, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAmbientOcclusionTexture = new CyclesTextureImage();

		public TexturedFloat PbrAlpha = new TexturedFloat(PbrCSN.Alpha, 0.0f, false, 0.0f);
		public CyclesTextureImage PbrAlphaTexture = new CyclesTextureImage();

		public bool UseBaseColorTextureAlphaAsObjectAlpha { get; set; } = true;

		public TexturedColor PbrEmission = new TexturedColor(PbrCSN.Emission, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrEmissionTexture = new CyclesTextureImage();
		public TexturedColor PbrBump = new TexturedColor(PbrCSN.Bump, Color4f.Black, false, 0.0f);
		public CyclesTextureImage PbrBumpTexture = new CyclesTextureImage();
		public TexturedColor PbrDisplacement = new TexturedColor(PbrCSN.Displacement, Color4f.Black, false, 0.0f);
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
						tex.TexByte = bytes[rid].Data as StdVectorByte;
					}
				}
				else if (tex.HasFloatImage)
				{
					if (floats.ContainsKey(rid))
					{
						tex.TexFloat = floats[rid].Data as StdVectorFloat;
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
		private bool disposedValue;

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

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					PbrBase.Texture?.Dispose();
					PbrBase.Texture = null;
					PbrBaseTexture?.Dispose();
					PbrBaseTexture = null;

					PbrMetallic.Texture?.Dispose();
					PbrMetallic.Texture = null;
					PbrMetallicTexture?.Dispose();
					PbrMetallicTexture = null;

					PbrSubsurface.Texture?.Dispose();
					PbrSubsurface.Texture = null;
					PbrSubsurfaceTexture?.Dispose();
					PbrSubsurfaceTexture = null;

					/*****/

					PbrSubsurfaceColor.Texture?.Dispose();
					PbrSubsurfaceColor.Texture = null;
					PbrSubsurfaceColorTexture?.Dispose();
					PbrSubsurfaceColorTexture = null;

					/*****/

					PbrSubsurfaceRadius.Texture?.Dispose();
					PbrSubsurfaceRadius.Texture = null;
					PbrSubsurfaceRadiusTexture?.Dispose();
					PbrSubsurfaceRadiusTexture = null;

					/*****/

					PbrSpecular.Texture?.Dispose();
					PbrSpecular.Texture = null;
					PbrSpecularTexture?.Dispose();
					PbrSpecularTexture = null;

					/*****/

					PbrSpecularTint.Texture?.Dispose();
					PbrSpecularTint.Texture = null;
					PbrSpecularTintTexture?.Dispose();
					PbrSpecularTintTexture = null;

					/*****/

					PbrRoughness.Texture?.Dispose();
					PbrRoughness.Texture = null;
					PbrRoughnessTexture?.Dispose();
					PbrRoughnessTexture = null;

					/*****/

					PbrAnisotropic.Texture?.Dispose();
					PbrAnisotropic.Texture = null;
					PbrAnisotropicTexture?.Dispose();
					PbrAnisotropicTexture = null;

					/*****/

					PbrAnisotropicRotation.Texture?.Dispose();
					PbrAnisotropicRotation.Texture = null;
					PbrAnisotropicRotationTexture?.Dispose();
					PbrAnisotropicRotationTexture = null;

					/*****/

					PbrSheen.Texture?.Dispose();
					PbrSheen.Texture = null;
					PbrSheenTexture?.Dispose();
					PbrSheenTexture = null;

					/*****/

					PbrSheenTint.Texture?.Dispose();
					PbrSheenTint.Texture = null;
					PbrSheenTintTexture?.Dispose();
					PbrSheenTintTexture = null;

					/*****/

					PbrClearcoat.Texture?.Dispose();
					PbrClearcoat.Texture = null;
					PbrClearcoatTexture?.Dispose();
					PbrClearcoatTexture = null;

					/*****/

					PbrClearcoatRoughness.Texture?.Dispose();
					PbrClearcoatRoughness.Texture = null;
					PbrClearcoatRoughnessTexture?.Dispose();
					PbrClearcoatRoughnessTexture = null;

					/*****/

					PbrClearcoatBump.Texture?.Dispose();
					PbrClearcoatBump.Texture = null;
					PbrClearcoatBumpTexture?.Dispose();
					PbrClearcoatBumpTexture = null;

					/*****/

					PbrIor.Texture?.Dispose();
					PbrIor.Texture = null;
					PbrIorTexture?.Dispose();
					PbrIorTexture = null;

					/*****/

					PbrTransmission.Texture?.Dispose();
					PbrTransmission.Texture = null;
					PbrTransmissionTexture?.Dispose();
					PbrTransmissionTexture = null;

					/*****/

					PbrTransmissionRoughness.Texture?.Dispose();
					PbrTransmissionRoughness.Texture = null;
					PbrTransmissionRoughnessTexture?.Dispose();
					PbrTransmissionRoughnessTexture = null;

					PbrAmbientOcclusion.Texture?.Dispose();
					PbrAmbientOcclusion.Texture = null;
					PbrAmbientOcclusionTexture?.Dispose();
					PbrAmbientOcclusionTexture = null;

					PbrAlpha.Texture?.Dispose();
					PbrAlpha.Texture = null;
					PbrAlphaTexture?.Dispose();
					PbrAlphaTexture = null;

					PbrEmission.Texture?.Dispose();
					PbrEmission.Texture = null;
					PbrEmissionTexture?.Dispose();
					PbrEmissionTexture = null;

					PbrBump.Texture?.Dispose();
					PbrBump.Texture = null;
					PbrBumpTexture?.Dispose();
					PbrBumpTexture = null;

					PbrDisplacement.Texture?.Dispose();
					PbrDisplacement.Texture = null;
					PbrDisplacementTexture?.Dispose();
					PbrDisplacementTexture = null;

					PbrSmudge.Texture?.Dispose();
					PbrSmudge.Texture = null;
					PbrSmudgeTexture?.Dispose();
					PbrSmudgeTexture = null;

					PbrScratch.Texture?.Dispose();
					PbrScratch.Texture = null;
					PbrScratchTexture?.Dispose();
					PbrScratchTexture = null;

					/*** CyclesTextureImages for custom shader case ***/
					DiffuseTexture?.Dispose();
					DiffuseTexture = null;
					BumpTexture?.Dispose();
					BumpTexture = null;
					TransparencyTexture?.Dispose();
					TransparencyTexture = null;
					EnvironmentTexture?.Dispose();
					EnvironmentTexture = null;
					GiEnvTexture?.Dispose();
					GiEnvTexture = null;
					BgEnvTexture?.Dispose();
					BgEnvTexture = null;
					ReflRefrEnvTexture?.Dispose();
					ReflRefrEnvTexture = null;
				}

				disposedValue = true;
			}
		}


		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
