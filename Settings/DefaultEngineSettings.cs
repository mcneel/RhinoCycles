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

using System;
using System.Collections.Generic;

namespace RhinoCyclesCore.Settings
{
	public static class DefaultEngineSettings
	{
		static public bool Verbose => false;

		static public float SpotLightFactor => 40.0f;
		static public float PointLightFactor => 40.0f;
		static public float SunLightFactor => 3.2f;
		static public float LinearLightFactor => 10.0f;
		static public float AreaLightFactor => 17.2f;
		static public float PolishFactor => 0.09f;

		static public int ThrottleMs => 100;
		static public int Threads => Math.Max(1, Utilities.GetSystemProcessorCount() - 2);

		static public bool ExperimentalCpuInMulti => false;

		static public float BumpDistance => 1.0f;
		static public float NormalStrengthFactor => 1.0f;
		static public float BumpStrengthFactor => 1.0f;

		static public string SelectedDeviceStr => "-1";
		static public bool AllowSelectedDeviceOverride => false;

		static public bool UseStartResolution => RenderEngine.DefaultPixelSizeBasedOnMonitorResolution > 1;
		static public int StartResolution => RenderEngine.DefaultPixelSizeBasedOnMonitorResolution > 1 ? 128 : int.MaxValue;

		static public int PixelSize => Math.Max(1, RenderEngine.DefaultPixelSizeBasedOnMonitorResolution);
		static public float OldDpiScale => Math.Max(1.0f, RenderEngine.DefaultPixelSizeBasedOnMonitorResolution);

		static public int TileX => 128;
		static public int TileY => 128;

		static public int MaxBounce => 32;

		static public bool NoCaustics => false;
		static public bool CausticsReflective => true;
		static public bool CausticsRefractive => true;

		static public int MaxDiffuseBounce => 4;
		static public int MaxGlossyBounce => 16;
		static public int MaxTransmissionBounce => 32;

		static public int MaxVolumeBounce => 32;

		static public int AaSamples => 32;

		static public int DiffuseSamples => 32;
		static public int GlossySamples => 32;
		static public int TransmissionSamples => 32;

		static public int AoBounces => 0;
		static public float AoFactor => 0.0f;
		static public float AoDistance => float.MaxValue;
		static public float AoAdditiveFactor => 0.0f;

		static public int MeshLightSamples => 32;
		static public int SubSurfaceSamples => 32;
		static public int VolumeSamples => 32;

		static public int Samples => 1000;
		static public bool UseDocumentSamples => false;
		/// <summary>
		/// Texture bake quality 0-3
		///
		/// 0 = low
		/// 1 = standard
		/// 2 = high
		/// 3 = ultra
		/// 4 = disabled
		/// </summary>
		static public int TextureBakeQuality => 0;
		static public int Seed => 128;

		static public float FilterGlossy => 0.5f;

		static public float SampleClampDirect => 3.0f;
		static public float SampleClampIndirect => 3.0f;
		static public float LightSamplingThreshold => 0.05f;

		static public bool UseDirectLight => true;
		static public bool UseIndirectLight => true;

		static public int Blades => 0;
		static public float BladesRotation => 0.0f;
		static public float ApertureRatio => 1.0f;
		static public float ApertureFactor => 0.1f;

		static public float SensorWidth => 32.0f;
		static public float SensorHeight => 18.0f;

		static public int TransparentMaxBounce => 32;

		static public int SssMethod => 44;

		static public bool ShowMaxPasses => true;
		static public int OpenClDeviceType => 4;
		static public int OpenClKernelType => 0;
		static public bool CPUSplitKernel => true;
		static public bool OpenClSingleProgram => true;
		static public bool NoShadows => false;
		static public bool SaveDebugImages => false;
		static public bool DebugSimpleShaders => false;
		static public bool DebugNoOverrideTileSize => false;
		static public bool FlushAtEndOfCreateWorld => false;
		static public int PreviewSamples => 150;

		static public bool DumpMaterialShaderGraph => false;
		static public bool DumpEnvironmentShaderGraph => false;
		static public bool StartGpuKernelCompiler => true;
		static public bool VerboseLogging => false;
		static public int RetentionDays => 3;
		static public int TriggerPostEffectsSample => 5;

		static public bool UseLightTree => true;
		static public bool UseAdaptiveSampling	=> true;
		static public int AdaptiveMinSamples => 16;
		static public float AdaptiveThreshold => 0.01f;
		static public float JiggleFactor => 0.0001f;
		static public float GpJiggleDistance => 0.0001f;
		static public bool SkipPreview => false;
	}

	/// <summary>
	/// A complete set of render option default values for a single render preset
	/// (see <see cref="RenderPresetHelpers.Presets"/>). Each preset has its own set:
	/// the shared base values come from <see cref="DefaultEngineSettings"/> and each
	/// preset then applies its own overrides in <see cref="ForPreset"/>.
	/// </summary>
	public class PresetDefaults
	{
		public int Samples;
		public bool ShowMaxPasses;
		public int Seed;
		public int MaxBounce;
		public int TileX;
		public int TileY;
		public bool NoCaustics;
		public int MaxDiffuseBounce;
		public int MaxGlossyBounce;
		public int MaxTransmissionBounce;
		public int MaxVolumeBounce;
		public int TransparentMaxBounce;
		public int AaSamples;
		public int DiffuseSamples;
		public int GlossySamples;
		public float SensorWidth;
		public float SensorHeight;
		public float FilterGlossy;
		public float SampleClampDirect;
		public float SampleClampIndirect;
		public float LightSamplingThreshold;
		public bool UseDirectLight;
		public bool UseIndirectLight;
		public bool UseAdaptiveSampling;
		public int AdaptiveMinSamples;
		public float AdaptiveThreshold;

		private PresetDefaults() { }

		/// <summary>
		/// The shared base defaults (<see cref="DefaultEngineSettings"/>) that every preset
		/// derives from. No preset is the base: each preset - Architecture included - applies
		/// its own overrides on top of this in <see cref="ForPreset"/>.
		/// </summary>
		private static PresetDefaults BaseDefaults()
		{
			return new PresetDefaults
			{
				Samples = DefaultEngineSettings.Samples,
				ShowMaxPasses = DefaultEngineSettings.ShowMaxPasses,
				Seed = DefaultEngineSettings.Seed,
				MaxBounce = DefaultEngineSettings.MaxBounce,
				TileX = DefaultEngineSettings.TileX,
				TileY = DefaultEngineSettings.TileY,
				NoCaustics = DefaultEngineSettings.NoCaustics,
				MaxDiffuseBounce = DefaultEngineSettings.MaxDiffuseBounce,
				MaxGlossyBounce = DefaultEngineSettings.MaxGlossyBounce,
				MaxTransmissionBounce = DefaultEngineSettings.MaxTransmissionBounce,
				MaxVolumeBounce = DefaultEngineSettings.MaxVolumeBounce,
				TransparentMaxBounce = DefaultEngineSettings.TransparentMaxBounce,
				AaSamples = DefaultEngineSettings.AaSamples,
				DiffuseSamples = DefaultEngineSettings.DiffuseSamples,
				GlossySamples = DefaultEngineSettings.GlossySamples,
				SensorWidth = DefaultEngineSettings.SensorWidth,
				SensorHeight = DefaultEngineSettings.SensorHeight,
				FilterGlossy = DefaultEngineSettings.FilterGlossy,
				SampleClampDirect = DefaultEngineSettings.SampleClampDirect,
				SampleClampIndirect = DefaultEngineSettings.SampleClampIndirect,
				LightSamplingThreshold = DefaultEngineSettings.LightSamplingThreshold,
				UseDirectLight = DefaultEngineSettings.UseDirectLight,
				UseIndirectLight = DefaultEngineSettings.UseIndirectLight,
				UseAdaptiveSampling = DefaultEngineSettings.UseAdaptiveSampling,
				AdaptiveMinSamples = DefaultEngineSettings.AdaptiveMinSamples,
				AdaptiveThreshold = DefaultEngineSettings.AdaptiveThreshold,
			};
		}

		/// <summary>
		/// Build the full set of default option values for the given preset: the shared base
		/// defaults with that preset's overrides applied. Add preset-specific overrides in the
		/// switch below as they are defined - each preset, including Architecture, may override.
		/// </summary>
		public static PresetDefaults ForPreset(RenderPresetHelpers.Presets preset)
		{
			var d = BaseDefaults();

			switch (preset)
			{
				case RenderPresetHelpers.Presets.Product:
					d.FilterGlossy = 0.0f;
					// 20 is high enough not to visibly dim caustics (tested indistinguishable
					// from unclamped on caustic scenes) while still bounding the worst firefly
					// spikes. Do not use 0: unbounded spikes never converge. RH-95847.
					d.SampleClampIndirect = 20.0f;
					// Product scenes are dominated by caustics whose energy arrives through rare
					// bright paths. With a low minimum the adaptive sampler retires shadow pixels
					// before such a path ever hits them, leaving holes in the caustics. RH-95847.
					d.AdaptiveMinSamples = 256;
					break;

				case RenderPresetHelpers.Presets.Architecture:
					// Architecture currently matches the shared base. Add its own overrides here
					// as they are defined - the base, not Architecture, is the reference point.
					break;

				default:
					break;
			}

			return d;
		}

		/// <summary>
		/// True when <paramref name="settings"/> carries this preset's signature: this preset
		/// overrides at least one value away from the shared base defaults, and every such
		/// overridden value equals this preset's value in <paramref name="settings"/>. Values
		/// this preset does not override are ignored. The comparison is against the shared base
		/// - never Architecture - so any preset (Architecture included) may define overrides.
		/// Backs <see cref="RenderPresetHelpers.PresetFromValues"/>, the legacy fallback for
		/// documents without a stored RenderPreset. Add a line here whenever a new per-preset
		/// value is introduced.
		/// </summary>
		public bool MatchesSignature(IDocumentSettings settings)
		{
			var b = BaseDefaults();
			int signatureFields = 0;
			return TryMatch(Samples, b.Samples, settings.Samples, ref signatureFields)
				&& TryMatch(ShowMaxPasses, b.ShowMaxPasses, settings.ShowMaxPasses, ref signatureFields)
				&& TryMatch(Seed, b.Seed, settings.Seed, ref signatureFields)
				&& TryMatch(MaxBounce, b.MaxBounce, settings.MaxBounce, ref signatureFields)
				&& TryMatch(TileX, b.TileX, settings.TileX, ref signatureFields)
				&& TryMatch(TileY, b.TileY, settings.TileY, ref signatureFields)
				&& TryMatch(NoCaustics, b.NoCaustics, settings.NoCaustics, ref signatureFields)
				&& TryMatch(MaxDiffuseBounce, b.MaxDiffuseBounce, settings.MaxDiffuseBounce, ref signatureFields)
				&& TryMatch(MaxGlossyBounce, b.MaxGlossyBounce, settings.MaxGlossyBounce, ref signatureFields)
				&& TryMatch(MaxTransmissionBounce, b.MaxTransmissionBounce, settings.MaxTransmissionBounce, ref signatureFields)
				&& TryMatch(MaxVolumeBounce, b.MaxVolumeBounce, settings.MaxVolumeBounce, ref signatureFields)
				&& TryMatch(TransparentMaxBounce, b.TransparentMaxBounce, settings.TransparentMaxBounce, ref signatureFields)
				&& TryMatch(AaSamples, b.AaSamples, settings.AaSamples, ref signatureFields)
				&& TryMatch(DiffuseSamples, b.DiffuseSamples, settings.DiffuseSamples, ref signatureFields)
				&& TryMatch(GlossySamples, b.GlossySamples, settings.GlossySamples, ref signatureFields)
				&& TryMatch(SensorWidth, b.SensorWidth, settings.SensorWidth, ref signatureFields)
				&& TryMatch(SensorHeight, b.SensorHeight, settings.SensorHeight, ref signatureFields)
				&& TryMatch(FilterGlossy, b.FilterGlossy, settings.FilterGlossy, ref signatureFields)
				&& TryMatch(SampleClampDirect, b.SampleClampDirect, settings.SampleClampDirect, ref signatureFields)
				&& TryMatch(SampleClampIndirect, b.SampleClampIndirect, settings.SampleClampIndirect, ref signatureFields)
				&& TryMatch(LightSamplingThreshold, b.LightSamplingThreshold, settings.LightSamplingThreshold, ref signatureFields)
				&& TryMatch(UseDirectLight, b.UseDirectLight, settings.UseDirectLight, ref signatureFields)
				&& TryMatch(UseIndirectLight, b.UseIndirectLight, settings.UseIndirectLight, ref signatureFields)
				&& TryMatch(UseAdaptiveSampling, b.UseAdaptiveSampling, settings.UseAdaptiveSampling, ref signatureFields)
				&& TryMatch(AdaptiveMinSamples, b.AdaptiveMinSamples, settings.AdaptiveMinSamples, ref signatureFields)
				&& TryMatch(AdaptiveThreshold, b.AdaptiveThreshold, settings.AdaptiveThreshold, ref signatureFields)
				&& signatureFields > 0;
		}

		// A value is part of this preset's signature only when it differs from the shared base
		// default. Signature values must equal the preset's value; non-signature values are
		// ignored. Counts the signature fields so a preset with no overrides matches nothing.
		private static bool TryMatch<T>(T presetValue, T baseValue, T actual, ref int signatureFields)
		{
			var cmp = EqualityComparer<T>.Default;
			if (cmp.Equals(presetValue, baseValue)) return true;   // not a signature field
			signatureFields++;
			return cmp.Equals(actual, presetValue);
		}
	}
}
