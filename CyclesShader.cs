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
using RhinoCyclesCore.Materials;

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

		public CyclesShader()
		{
			DiffuseTexture = new CyclesTextureImage();
			BumpTexture = new CyclesTextureImage();
			TransparencyTexture = new CyclesTextureImage();
			EnvironmentTexture = new CyclesTextureImage();
			GiEnvTexture = new CyclesTextureImage();
			BgEnvTexture = new CyclesTextureImage();
			ReflRefrEnvTexture = new CyclesTextureImage();
			IsCyclesMaterial = false;
			CyclesMaterialType = CyclesMaterial.No;
			Xml = "";
		}

		public ICyclesMaterial Crm { get; set; }

		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as CyclesShader;

			return other != null && Id.Equals(other.Id);
		}

		/// <summary>
		/// Set to true if a shadeless effect is wanted (self-illuminating).
		/// </summary>
		public bool Shadeless { get; set; }
		/// <summary>
		/// Get <c>Shadeless</c> as a float value
		/// </summary>
		public float ShadelessAsFloat { get { return Shadeless ? 1.0f : 0.0f; } }

		/// <summary>
		/// Set to true if shader is for a CyclesMaterial.
		/// </summary>
		public bool IsCyclesMaterial { get; set; }
		/// <summary>
		/// Set the CyclesMaterial type
		/// </summary>
		public CyclesMaterial CyclesMaterialType { get; set; }
		/// <summary>
		/// XML representation to use for shadergraph creation
		/// instead of the V6 basic material simulation graph.
		/// Used when <c>IsCyclesMaterial</c> is true.
		/// </summary>
		public string Xml { get; set; }
		/// <summary>
		/// RenderHash of the RenderMaterial for which this intermediary is created.
		/// </summary>
		public uint Id { get; set; }

		/// <summary>
		/// Type of shader this represents.
		/// </summary>
		public enum Shader
		{
			Background,
			Diffuse
		}

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

			Brick,
			Test,
			FlakedCarPaint,
			BrickCheckeredMortar,
			Translucent,
			PhongTest,

			Glass,
			Diffuse,
			SimplePlastic,
			SimpleMetal,

			SimpleNoiseEnvironment,
			XmlEnvironment,
		}

		public Shader Type { get; set; }

		public float4 DiffuseColor { get; set; }

		public bool HasOnlyDiffuseColor
		{
			get
			{
				return !HasDiffuseTexture
					&& !HasBumpTexture
					&& !HasTransparencyTexture
					&& !HasEmission
					&& !Shadeless
					&& NoTransparency
					&& NoReflectivity;
			}
		}

		public bool HasOnlyDiffuseTexture
		{
			get
			{
				return HasDiffuseTexture
					&& !HasBumpTexture
					&& !HasTransparencyTexture
					&& !HasEmission
					&& !Shadeless
					&& NoTransparency
					&& NoReflectivity;
			}
		}

		public bool DiffuseAndBumpTexture
		{
			get
			{
				return HasDiffuseTexture
					&& HasBumpTexture
					&& !HasTransparencyTexture
					&& !HasEmission
					&& !Shadeless
					&& NoTransparency
					&& NoReflectivity;
			}
			
		}

		public bool HasOnlyReflectionColor
		{
			get
			{
				return HasReflectivity
					&& !HasDiffuseTexture
					&& !HasEmission
					&& !Shadeless
					&& NoTransparency
					&& !HasTransparency
					&& !HasBumpTexture;
			}
		}

		public float4 SpecularColor { get; set; }
		public float4 ReflectionColor { get; set; }
		public float ReflectionRoughness { get; set; }
		public float4 RefractionColor { get; set; }
		public float RefractionRoughness { get; set; }
		public float4 TransparencyColor { get; set; }
		public float4 EmissionColor { get; set; }
		public bool HasEmission { get { return !EmissionColor.IsZero(false); } }

		public CyclesTextureImage DiffuseTexture { get; set; }
		public bool HasDiffuseTexture { get { return DiffuseTexture.HasTextureImage; } }
		public CyclesTextureImage BumpTexture { get; set; }
		public bool HasBumpTexture { get { return BumpTexture.HasTextureImage; } }
		public CyclesTextureImage TransparencyTexture { get; set; }
		public bool HasTransparencyTexture { get { return TransparencyTexture.HasTextureImage; } }
		public CyclesTextureImage EnvironmentTexture { get; set; }
		public bool HasEnvironmentTexture { get { return EnvironmentTexture.HasTextureImage; } }

		public CyclesTextureImage GiEnvTexture { get; set; }
		public bool HasGiEnvTexture { get { return GiEnvTexture.HasTextureImage; } }
		public float4 GiEnvColor { get; set; }
		public bool HasGiEnv
		{
			get { return HasGiEnvTexture || GiEnvColor != null; }
		}

		public CyclesTextureImage BgEnvTexture { get; set; }
		public bool HasBgEnvTexture { get { return BgEnvTexture.HasTextureImage; } }
		public float4 BgEnvColor { get; set; }
		public bool HasBgEnv
		{
			get { return HasBgEnvTexture || BgEnvColor != null;  }
		}

		public CyclesTextureImage ReflRefrEnvTexture { get; set; }
		public bool HasReflRefrEnvTexture { get { return ReflRefrEnvTexture.HasTextureImage; } }
		public float4 ReflRefrEnvColor { get; set; }
		public bool HasReflRefrEnv
		{
			get { return HasReflRefrEnvTexture || ReflRefrEnvColor != null; }
		}

		public bool HasUV { get; set; }

		public float FresnelIOR { get; set; }
		public float IOR { get; set; }
		public float Roughness { get; set; }
		public float Reflectivity { get; set; }
		public float Shine { get; set; }
		public float Transparency { get; set; }
		public bool NoTransparency { get { return Math.Abs(Transparency) < 0.00001f; } }
		public bool HasTransparency { get { return !NoTransparency; } }
		public bool NoReflectivity { get { return Math.Abs(Reflectivity) < 0.00001f; } }
		public bool HasReflectivity { get { return !NoReflectivity; } }

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

		public string Name { get; set; }
	}
}
