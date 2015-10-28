/**
Copyright 2014-2015 Robert McNeel and Associates

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
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles
{

	[System.Runtime.InteropServices.Guid("3F09C94E-26BC-4CD5-8315-9F71F4F04DA1")]
	public class SetRenderOptions : Command
	{
		private static SetRenderOptions g_thecommand;

		public SetRenderOptions()
		{
			g_thecommand = this;
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_SetRenderOptions"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var get_number = new GetNumber();
			get_number.SetLowerLimit(2.0, false);
			get_number.SetUpperLimit(100000.0, false);
			get_number.SetDefaultInteger(Plugin.EngineSettings.Samples);
			get_number.SetCommandPrompt("Set render samples");

			var use_custom_settings = new OptionToggle(Plugin.EngineSettings.UseCustomQualitySettings, "No", "Yes");

			var min_bounce = new OptionInteger(Plugin.EngineSettings.MinBounce, 0, 500);
			var max_bounce = new OptionInteger(Plugin.EngineSettings.MaxBounce, 0, 500);

			var max_diffuse_bounce = new OptionInteger(Plugin.EngineSettings.MaxDiffuseBounce, 0, 200);
			var max_glossy_bounce = new OptionInteger(Plugin.EngineSettings.MaxGlossyBounce, 0, 200);
			var max_transmission_bounce = new OptionInteger(Plugin.EngineSettings.MaxTransmissionBounce, 0, 200);
			var max_volume_bounce = new OptionInteger(Plugin.EngineSettings.MaxVolumeBounce, 0, 200);

			var no_caustics = new OptionToggle(Plugin.EngineSettings.NoCaustics, "Caustics", "NoCaustics");

			var aa_samples = new OptionInteger(Plugin.EngineSettings.AaSamples, 1, 100);
			var diff_samples = new OptionInteger(Plugin.EngineSettings.DiffuseSamples, 1, 100);
			var glossy_samples = new OptionInteger(Plugin.EngineSettings.GlossySamples, 1, 100);

			var seed = new OptionInteger(Plugin.EngineSettings.Seed, 0, int.MaxValue);

			var sensor_width = new OptionDouble(Plugin.EngineSettings.SensorWidth, 10.0, 100.0);
			var sensor_height = new OptionDouble(Plugin.EngineSettings.SensorHeight, 10.0, 100.0);

			var transparent_min_bounce = new OptionInteger(Plugin.EngineSettings.TransparentMinBounce, 0, 200);
			var transparent_max_bounce = new OptionInteger(Plugin.EngineSettings.TransparentMaxBounce, 0, 200);
			var transparent_shadows = new OptionToggle(Plugin.EngineSettings.TransparentShadows, "NoTransparentShadows", "TransparentShadows");

			var branched = new OptionToggle(Plugin.EngineSettings.IntegratorMethod==IntegratorMethod.BranchedPath, "Path", "BranchedPath");

			var sampling_pattern = new OptionToggle(Plugin.EngineSettings.SamplingPattern == SamplingPattern.CMJ, "Sobol", "CMJ");
			var filter_glossy = new OptionDouble(Plugin.EngineSettings.FilterGlossy, 0.0, 100.0);
			var sample_clamp_direct = new OptionDouble(Plugin.EngineSettings.SampleClampDirect, 0.0, 100.0);
			var sample_clamp_indirect = new OptionDouble(Plugin.EngineSettings.SampleClampIndirect, 0.0, 100.0);
			var sample_all_lights = new OptionToggle(Plugin.EngineSettings.SampleAllLights, "no", "yes");
			var sample_all_lights_indirect = new OptionToggle(Plugin.EngineSettings.SampleAllLightsIndirect, "no", "yes");

			get_number.AddOptionToggle("use_custom_quality_settings", ref use_custom_settings);

			get_number.AddOptionInteger("min_bounces", ref min_bounce);
			get_number.AddOptionInteger("max_bounces", ref max_bounce);
			get_number.AddOptionToggle("no_caustics", ref no_caustics);

			get_number.AddOptionInteger("max_diffuse_bounce", ref max_diffuse_bounce);
			get_number.AddOptionInteger("max_glossy_bounce", ref max_glossy_bounce);
			get_number.AddOptionInteger("max_transmission_bounce", ref max_transmission_bounce);
			get_number.AddOptionInteger("max_volume_bounce", ref max_volume_bounce);

			get_number.AddOptionInteger("transparent_min_bounce", ref transparent_min_bounce);
			get_number.AddOptionInteger("transparent_max_bounce", ref transparent_max_bounce);
			get_number.AddOptionToggle("transparent_shadows", ref transparent_shadows);

			get_number.AddOptionInteger("aa_samples", ref aa_samples);
			get_number.AddOptionInteger("diffuse_samples", ref diff_samples);
			get_number.AddOptionInteger("glossy_samples", ref glossy_samples);

			get_number.AddOptionDouble("sensor_width", ref sensor_width);
			get_number.AddOptionDouble("sensor_height", ref sensor_height);

			get_number.AddOptionToggle("integrator_method", ref branched);

			get_number.AddOptionInteger("seed", ref seed, "Seed to use for sampling distribution");

			get_number.AddOptionToggle("sampling_pattern", ref sampling_pattern);
			get_number.AddOptionDouble("filter_glossy", ref filter_glossy);
			get_number.AddOptionDouble("sample_clamp_direct", ref sample_clamp_direct);
			get_number.AddOptionDouble("sample_clamp_indirect", ref sample_clamp_indirect);
			get_number.AddOptionToggle("sample_all_lights", ref sample_all_lights);
			get_number.AddOptionToggle("sample_all_lights_indirect", ref sample_all_lights_indirect);

			while (true)
			{
				var get_rc = get_number.Get();
				if (get_number.CommandResult() != Result.Success) return get_number.CommandResult();
				switch (get_rc)
				{
					case GetResult.Number:
						RhinoApp.WriteLine(String.Format("We got: {0}, {1}, {2}", get_number.Number(), min_bounce.CurrentValue, max_bounce.CurrentValue));
						Plugin.EngineSettings.Samples = (int)get_number.Number();
						Plugin.EngineSettings.UseCustomQualitySettings = use_custom_settings.CurrentValue;
						Plugin.EngineSettings.Seed = seed.CurrentValue;
						Plugin.EngineSettings.MaxBounce = max_bounce.CurrentValue;
						Plugin.EngineSettings.MinBounce = min_bounce.CurrentValue;
						Plugin.EngineSettings.NoCaustics = no_caustics.CurrentValue;
						Plugin.EngineSettings.MaxDiffuseBounce = max_diffuse_bounce.CurrentValue;
						Plugin.EngineSettings.MaxGlossyBounce = max_glossy_bounce.CurrentValue;
						Plugin.EngineSettings.MaxTransmissionBounce = max_transmission_bounce.CurrentValue;
						Plugin.EngineSettings.MaxVolumeBounce = max_volume_bounce.CurrentValue;
						Plugin.EngineSettings.TransparentMinBounce = transparent_min_bounce.CurrentValue;
						Plugin.EngineSettings.TransparentMaxBounce = transparent_max_bounce.CurrentValue;
						Plugin.EngineSettings.TransparentShadows = transparent_shadows.CurrentValue;
						Plugin.EngineSettings.AaSamples = aa_samples.CurrentValue;
						Plugin.EngineSettings.DiffuseSamples = diff_samples.CurrentValue;
						Plugin.EngineSettings.GlossySamples = glossy_samples.CurrentValue;
						Plugin.EngineSettings.SensorWidth = (float)sensor_width.CurrentValue;
						Plugin.EngineSettings.SensorHeight = (float)sensor_height.CurrentValue;
						Plugin.EngineSettings.IntegratorMethod = branched.CurrentValue ? IntegratorMethod.BranchedPath : IntegratorMethod.Path;
						Plugin.EngineSettings.SamplingPattern = SamplingPattern.Sobol;
						Plugin.EngineSettings.FilterGlossy = 0.0f;
						Plugin.EngineSettings.SampleClampDirect = 0.0f;
						Plugin.EngineSettings.SampleClampIndirect = 0.0f;
						Plugin.EngineSettings.SampleAllLights = true;
						Plugin.EngineSettings.SampleAllLightsIndirect = true;
						break;
					case GetResult.Option:
						continue;
				}

				break;
			}
			return Result.Success;
		}
	}
}

