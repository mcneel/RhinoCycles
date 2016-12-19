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
			CyclesMaterialType = CyclesMaterial.No;
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
		public float ShadelessAsFloat => Shadeless ? 1.0f : 0.0f;

		/// <summary>
		/// Set the CyclesMaterial type
		/// </summary>
		public CyclesMaterial CyclesMaterialType { get; set; }

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
			Paint,
			SimplePlastic,
			SimpleMetal,
			Emissive,

			SimpleNoiseEnvironment,
			XmlEnvironment,
		}

		public Shader Type { get; set; }

		/// <summary>
		/// Gamma corrected base color
		/// </summary>
		public float4 BaseColor
		{
			get
			{
				float4 c;
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

				return c ^ Gamma;
			}
		}

		public float4 DiffuseColor { get; set; }

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

		public float4 SpecularColor { get; set; }
		public float4 ReflectionColor { get; set; }
		public float ReflectionRoughness { get; set; }
		public float4 RefractionColor { get; set; }
		public float RefractionRoughness { get; set; }
		public float4 TransparencyColor { get; set; }
		public float4 EmissionColor { get; set; }
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
		public float4 GiEnvColor { get; set; }
		public bool HasGiEnv => HasGiEnvTexture || GiEnvColor != null;

		public CyclesTextureImage BgEnvTexture { get; set; }
		public bool HasBgEnvTexture => BgEnvTexture.HasTextureImage;
		public float4 BgEnvColor { get; set; }
		public bool HasBgEnv => HasBgEnvTexture || BgEnvColor != null;

		public CyclesTextureImage ReflRefrEnvTexture { get; set; }
		public bool HasReflRefrEnvTexture => ReflRefrEnvTexture.HasTextureImage;
		public float4 ReflRefrEnvColor { get; set; }
		public bool HasReflRefrEnv => HasReflRefrEnvTexture || ReflRefrEnvColor != null;

		public bool HasUV { get; set; }

		public float FresnelIOR { get; set; }
		public float IOR { get; set; }
		public float Roughness { get; set; }
		public float Reflectivity { get; set; }
		public float Metalic { get; set; }
		public bool NoMetalic => Math.Abs(Metalic) < 0.00001f;
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
