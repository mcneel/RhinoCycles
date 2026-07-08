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

using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCyclesCore.Settings;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{

	[Guid("3F09C94E-26BC-4CD5-8315-9F71F4F04DA1")]
	[CommandStyle(Style.Hidden)]
	public class SetRenderOptions : Command
	{
		private static SetRenderOptions _gThecommand;

		public SetRenderOptions()
		{
			if(_gThecommand==null) _gThecommand = this;
		}

		public override string EnglishName => "RhinoCycles_SetRenderOptions";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			// All options always change the document.
			IAllSettings CurrentSource () => new EngineDocumentSettings(doc.RuntimeSerialNumber);
			var source = CurrentSource();

			var preset = source.RenderPreset;

			var getNumber = new GetNumber();
			getNumber.SetLowerLimit(2.0, false);
			getNumber.SetUpperLimit(uint.MaxValue, false);
			getNumber.SetDefaultInteger(source.Samples);
			getNumber.SetCommandPrompt("Set render samples");

			var presetToggle = new OptionToggle(preset == RenderPresetHelpers.Presets.Product, "Architecture", "Product");

			var showMaxPasses = new OptionToggle(source.ShowMaxPasses, "HideMaxPasses", "ShowMaxPasses");

			var maxBounce = new OptionInteger(source.MaxBounce, 0, 500);
			var tileX = new OptionInteger(source.TileX, 0, 10000);
			var tileY = new OptionInteger(source.TileY, 0, 10000);

			var maxDiffuseBounce = new OptionInteger(source.MaxDiffuseBounce, 0, 200);
			var maxGlossyBounce = new OptionInteger(source.MaxGlossyBounce, 0, 200);
			var maxTransmissionBounce = new OptionInteger(source.MaxTransmissionBounce, 0, 200);
			var maxVolumeBounce = new OptionInteger(source.MaxVolumeBounce, 0, 200);

			var noCaustics = new OptionToggle(source.NoCaustics, "Caustics", "NoCaustics");

			var aaSamples = new OptionInteger(source.AaSamples, 1, 100);
			var diffSamples = new OptionInteger(source.DiffuseSamples, 1, 100);
			var glossySamples = new OptionInteger(source.GlossySamples, 1, 100);

			var seed = new OptionInteger(source.Seed, 0, int.MaxValue);

			var sensorWidth = new OptionDouble(source.SensorWidth, 10.0, 100.0);
			var sensorHeight = new OptionDouble(source.SensorHeight, 10.0, 100.0);

			var transparentMaxBounce = new OptionInteger(source.TransparentMaxBounce, 0, 200);

			var filterGlossy = new OptionDouble(source.FilterGlossy, 0.0, 100.0);
			var sampleClampDirect = new OptionDouble(source.SampleClampDirect, 0.0, 100.0);
			var sampleClampIndirect = new OptionDouble(source.SampleClampIndirect, 0.0, 100.0);
			var lightSamplingThreshold = new OptionDouble(source.LightSamplingThreshold, 0.0, 1.0);
			var useDirectLight = new OptionToggle(source.UseDirectLight, "no", "yes");
			var useIndirectLight = new OptionToggle(source.UseIndirectLight, "no", "yes");

			var useAdaptiveSampling = new OptionToggle(source.UseAdaptiveSampling, "no", "yes");
			var adaptiveMinSamples = new OptionInteger(source.AdaptiveMinSamples, 1, 4096);
			var adaptiveThreshold = new OptionDouble(source.AdaptiveThreshold, 0.0, 1.0);

			int presetOption = getNumber.AddOptionToggle("preset", ref presetToggle);
			getNumber.AddOptionToggle("show_max_passes", ref showMaxPasses);
			getNumber.AddOptionInteger("max_bounces", ref maxBounce);
			getNumber.AddOptionInteger("tile_x", ref tileX);
			getNumber.AddOptionInteger("tile_y", ref tileY);
			getNumber.AddOptionToggle("no_caustics", ref noCaustics);

			getNumber.AddOptionInteger("max_diffuse_bounce", ref maxDiffuseBounce);
			getNumber.AddOptionInteger("max_glossy_bounce", ref maxGlossyBounce);
			getNumber.AddOptionInteger("max_transmission_bounce", ref maxTransmissionBounce);
			getNumber.AddOptionInteger("max_volume_bounce", ref maxVolumeBounce);

			getNumber.AddOptionInteger("transparent_max_bounce", ref transparentMaxBounce);

			getNumber.AddOptionInteger("aa_samples", ref aaSamples);
			getNumber.AddOptionInteger("diffuse_samples", ref diffSamples);
			getNumber.AddOptionInteger("glossy_samples", ref glossySamples);

			getNumber.AddOptionDouble("sensor_width", ref sensorWidth);
			getNumber.AddOptionDouble("sensor_height", ref sensorHeight);

			getNumber.AddOptionInteger("seed", ref seed, "Seed to use for sampling distribution");

			getNumber.AddOptionDouble("filter_glossy", ref filterGlossy);
			getNumber.AddOptionDouble("sample_clamp_direct", ref sampleClampDirect);
			getNumber.AddOptionDouble("sample_clamp_indirect", ref sampleClampIndirect);
			getNumber.AddOptionDouble("light_sampling_threshold", ref lightSamplingThreshold);
			getNumber.AddOptionToggle("sample_all_lights", ref useDirectLight);
			getNumber.AddOptionToggle("sample_all_lights_indirect", ref useIndirectLight);

			getNumber.AddOptionToggle("use_adaptive_sampling", ref useAdaptiveSampling);
			getNumber.AddOptionInteger("adaptive_min_samples", ref adaptiveMinSamples);
			getNumber.AddOptionDouble("adaptive_threshold", ref adaptiveThreshold);

			void LoadFrom(IAllSettings s)
			{
				getNumber.SetDefaultInteger(s.Samples);
				showMaxPasses.CurrentValue = s.ShowMaxPasses;
				seed.CurrentValue = s.Seed;
				maxBounce.CurrentValue = s.MaxBounce;
				tileX.CurrentValue = s.TileX;
				tileY.CurrentValue = s.TileY;
				noCaustics.CurrentValue = s.NoCaustics;
				maxDiffuseBounce.CurrentValue = s.MaxDiffuseBounce;
				maxGlossyBounce.CurrentValue = s.MaxGlossyBounce;
				maxTransmissionBounce.CurrentValue = s.MaxTransmissionBounce;
				maxVolumeBounce.CurrentValue = s.MaxVolumeBounce;
				transparentMaxBounce.CurrentValue = s.TransparentMaxBounce;
				aaSamples.CurrentValue = s.AaSamples;
				diffSamples.CurrentValue = s.DiffuseSamples;
				glossySamples.CurrentValue = s.GlossySamples;
				sensorWidth.CurrentValue = s.SensorWidth;
				sensorHeight.CurrentValue = s.SensorHeight;
				filterGlossy.CurrentValue = s.FilterGlossy;
				sampleClampDirect.CurrentValue = s.SampleClampDirect;
				sampleClampIndirect.CurrentValue = s.SampleClampIndirect;
				lightSamplingThreshold.CurrentValue = s.LightSamplingThreshold;
				useDirectLight.CurrentValue = s.UseDirectLight;
				useIndirectLight.CurrentValue = s.UseIndirectLight;
				useAdaptiveSampling.CurrentValue = s.UseAdaptiveSampling;
				adaptiveMinSamples.CurrentValue = s.AdaptiveMinSamples;
				adaptiveThreshold.CurrentValue = s.AdaptiveThreshold;
			}

			void SaveToDocument(int samples)
			{
				var rs = doc.RenderSettings.Duplicate();
				var d = rs.UserDictionary;
				d[SettingNames.Samples] = samples;
				d[SettingNames.MaxPasses] = showMaxPasses.CurrentValue;
				d[SettingNames.Seed] = seed.CurrentValue;
				d[SettingNames.MaxBounce] = maxBounce.CurrentValue;
				d[SettingNames.TileX] = tileX.CurrentValue;
				d[SettingNames.TileY] = tileY.CurrentValue;
				d[SettingNames.NoCaustics] = noCaustics.CurrentValue;
				d[SettingNames.MaxDiffuseBounce] = maxDiffuseBounce.CurrentValue;
				d[SettingNames.MaxGlossyBounce] = maxGlossyBounce.CurrentValue;
				d[SettingNames.MaxTransmissionBounce] = maxTransmissionBounce.CurrentValue;
				d[SettingNames.MaxVolumeBounce] = maxVolumeBounce.CurrentValue;
				d[SettingNames.TransparentMaxBounce] = transparentMaxBounce.CurrentValue;
				d[SettingNames.AaSamples] = aaSamples.CurrentValue;
				d[SettingNames.DiffuseSamples] = diffSamples.CurrentValue;
				d[SettingNames.GlossySamples] = glossySamples.CurrentValue;
				d[SettingNames.SensorWidth] = sensorWidth.CurrentValue;
				d[SettingNames.SensorHeight] = sensorHeight.CurrentValue;
				d[SettingNames.FilterGlossy] = filterGlossy.CurrentValue;
				d[SettingNames.SampleClampDirect] = sampleClampDirect.CurrentValue;
				d[SettingNames.SampleClampIndirect] = sampleClampIndirect.CurrentValue;
				d[SettingNames.LightSamplingThreshold] = lightSamplingThreshold.CurrentValue;
				d[SettingNames.UseDirectLight] = useDirectLight.CurrentValue;
				d[SettingNames.UseIndirectLight] = useIndirectLight.CurrentValue;
				d[SettingNames.UseAdaptiveSampling] = useAdaptiveSampling.CurrentValue;
				d[SettingNames.AdaptiveMinSamples] = adaptiveMinSamples.CurrentValue;
				d[SettingNames.AdaptiveThreshold] = adaptiveThreshold.CurrentValue;
				doc.RenderSettings = rs;
			}

			// Record the selected preset on the document and align the values that differ
			// per preset with that preset's defaults (consistent with RenderPresetHelpers.SetPreset).
			void StorePreset(RenderPresetHelpers.Presets p)
			{
				var defaults = PresetDefaults.ForPreset(p);
				var rs = doc.RenderSettings.Duplicate();
				var d = rs.UserDictionary;
				d[SettingNames.RenderPreset] = (int)p;
				d[SettingNames.FilterGlossy] = (double)defaults.FilterGlossy;
				d[SettingNames.SampleClampIndirect] = (double)defaults.SampleClampIndirect;
				d[SettingNames.AdaptiveMinSamples] = defaults.AdaptiveMinSamples;
				doc.RenderSettings = rs;
			}

			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
				switch (getRc)
				{
					case GetResult.Number:
						int samples = (int)getNumber.Number();
						SaveToDocument(samples);
						break;
					case GetResult.Option:
						CommandLineOption cmdOption = getNumber.Option();
						if (cmdOption != null)
						{
							if (cmdOption.Index == presetOption)
							{
								preset = presetToggle.CurrentValue ? RenderPresetHelpers.Presets.Product : RenderPresetHelpers.Presets.Architecture;
								StorePreset(preset);
								LoadFrom(CurrentSource());
							}
						}
						continue;
				}

				break;
			}
			return Result.Success;
		}
	}
}
