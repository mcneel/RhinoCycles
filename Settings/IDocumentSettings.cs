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
using Rhino;
using RhinoCyclesCore.Core;
using System;
using System.Net;

namespace RhinoCyclesCore.Settings
{
	public static class RenderPresetHelpers
	{
		public enum Presets
		{
			Architecture = 0,
			Product = 1,
		};

		/// <summary>
		/// Preset returned when a legacy document's values match no preset's signature. This is
		/// a deliberate default, NOT an assumption that this preset equals the base defaults.
		/// </summary>
		public const Presets DefaultPreset = Presets.Architecture;

		/// <summary>
		/// Legacy fallback for documents saved before the RenderPreset value was stored.
		/// Determines the preset by matching the document's values against each preset's
		/// signature (<see cref="PresetDefaults.MatchesSignature"/>) rather than any fixed set
		/// of parameters, so it keeps working as more values start to differ per preset - for
		/// any preset, Architecture included.
		/// </summary>
		public static Presets PresetFromValues(IDocumentSettings settings)
		{
			foreach (Presets preset in Enum.GetValues(typeof(Presets)))
			{
				if (preset == DefaultPreset) continue;
				if (PresetDefaults.ForPreset(preset).MatchesSignature(settings)) return preset;
			}

			// Frozen historical signature: before RH-95847 the Product preset forced
			// FilterGlossy and SampleClampIndirect to 0, so documents from that era carry
			// these values instead of the current Product defaults. Never update this check
			// when preset values change - it identifies old documents only.
			if (settings.FilterGlossy == 0.0f && settings.SampleClampIndirect == 0.0f) return Presets.Product;

			return DefaultPreset;
		}

		public static Presets ProductPreset(IDocumentSettings settings)
		{
			return settings.RenderPreset;
		}

		public static void SetPreset(IDocumentSettings settings, Presets preset)
		{
			settings.RenderPreset = preset;

			var defaults = PresetDefaults.ForPreset(preset);
			settings.FilterGlossy = defaults.FilterGlossy;
			settings.SampleClampIndirect = defaults.SampleClampIndirect;
			settings.AdaptiveMinSamples = defaults.AdaptiveMinSamples;
		}
	}

	public interface IDocumentSettings
	{
		uint IntegratorHash { get; }
		int Samples { get; set; }
		bool UseDocumentSamples { get; set; }
		int TextureBakeQuality { get; set; }
		int Seed { get; set; }
		int DiffuseSamples { get; set; }
		int GlossySamples { get; set; }
		int TransmissionSamples { get; set; }
		int MaxBounce { get; set; }
		int MaxDiffuseBounce { get; set; }
		int MaxGlossyBounce { get; set; }
		int MaxVolumeBounce { get; set; }
		int MaxTransmissionBounce { get; set; }
		int TransparentMaxBounce { get; set; }

		int TileX { get; set; }
		int TileY { get; set; }

		float SpotLightFactor { get; set; }
		float PointLightFactor { get; set; }
		float SunLightFactor { get; set; }
		float LinearLightFactor { get; set; }
		float AreaLightFactor { get; set; }
		float PolishFactor { get; set; }

		float BumpDistance { get; set; }
		float NormalStrengthFactor { get; set; }
		float BumpStrengthFactor { get; set; }

		bool NoCaustics { get; set; }
		bool CausticsReflective { get; set; }
		bool CausticsRefractive { get; set; }

		int AaSamples { get; set; }

		int AoBounces { get; set; }
		float AoFactor { get; set; }
		float AoDistance { get; set; }
		float AoAdditiveFactor { get; set; }

		int SubsurfaceSamples { get; set; }
		int VolumeSamples { get; set; }

		float FilterGlossy { get; set; }
		RenderPresetHelpers.Presets RenderPreset { get; set; }
		bool IsProductPreset { get; }

		bool UseAdaptiveSampling { get; set; }
		int AdaptiveMinSamples { get; set; }
		float AdaptiveThreshold { get; set; }

		float SampleClampDirect { get; set; }
		float SampleClampIndirect { get; set; }
		float LightSamplingThreshold { get; set; }

		bool UseDirectLight { get; set; }
		bool UseIndirectLight { get; set; }

		int Blades { get; set; }
		float BladesRotation { get; set; }
		float ApertureRatio { get; set; }
		float ApertureFactor { get; set; }

		float SensorWidth { get; set; }
		float SensorHeight { get; set; }

		int SssMethod { get; set; }
		bool AllowSelectedDeviceOverride { get; }
		Device RenderDevice { get; }
		bool ShowMaxPasses { get; set; }
	}
}
