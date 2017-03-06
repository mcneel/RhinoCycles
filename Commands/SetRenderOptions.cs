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

using System.Runtime.InteropServices;
using ccl;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCyclesCore.Core;

namespace RhinoCycles.Commands
{

	[Guid("3F09C94E-26BC-4CD5-8315-9F71F4F04DA1")]
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
			var getNumber = new GetNumber();
			getNumber.SetLowerLimit(2.0, false);
			getNumber.SetUpperLimit(uint.MaxValue, false);
			getNumber.SetDefaultInteger(RcCore.It.EngineSettings.Samples);
			getNumber.SetCommandPrompt("Set render samples");

			var showMaxPasses = new OptionToggle(RcCore.It.EngineSettings.ShowMaxPasses, "HideMaxPasses", "ShowMaxPasses");

			var minBounce = new OptionInteger(RcCore.It.EngineSettings.MinBounce, 0, 500);
			var maxBounce = new OptionInteger(RcCore.It.EngineSettings.MaxBounce, 0, 500);
			var tileX = new OptionInteger(RcCore.It.EngineSettings.TileX, 0, 10000);
			var tileY = new OptionInteger(RcCore.It.EngineSettings.TileY, 0, 10000);


			var maxDiffuseBounce = new OptionInteger(RcCore.It.EngineSettings.MaxDiffuseBounce, 0, 200);
			var maxGlossyBounce = new OptionInteger(RcCore.It.EngineSettings.MaxGlossyBounce, 0, 200);
			var maxTransmissionBounce = new OptionInteger(RcCore.It.EngineSettings.MaxTransmissionBounce, 0, 200);
			var maxVolumeBounce = new OptionInteger(RcCore.It.EngineSettings.MaxVolumeBounce, 0, 200);

			var noCaustics = new OptionToggle(RcCore.It.EngineSettings.NoCaustics, "Caustics", "NoCaustics");

			var aaSamples = new OptionInteger(RcCore.It.EngineSettings.AaSamples, 1, 100);
			var diffSamples = new OptionInteger(RcCore.It.EngineSettings.DiffuseSamples, 1, 100);
			var glossySamples = new OptionInteger(RcCore.It.EngineSettings.GlossySamples, 1, 100);

			var seed = new OptionInteger(RcCore.It.EngineSettings.Seed, 0, int.MaxValue);

			var sensorWidth = new OptionDouble(RcCore.It.EngineSettings.SensorWidth, 10.0, 100.0);
			var sensorHeight = new OptionDouble(RcCore.It.EngineSettings.SensorHeight, 10.0, 100.0);

			var transparentMinBounce = new OptionInteger(RcCore.It.EngineSettings.TransparentMinBounce, 0, 200);
			var transparentMaxBounce = new OptionInteger(RcCore.It.EngineSettings.TransparentMaxBounce, 0, 200);
			var transparentShadows = new OptionToggle(RcCore.It.EngineSettings.TransparentShadows, "NoTransparentShadows", "TransparentShadows");

			var branched = new OptionToggle(RcCore.It.EngineSettings.IntegratorMethod==IntegratorMethod.BranchedPath, "Path", "BranchedPath");

			var samplingPattern = new OptionToggle(RcCore.It.EngineSettings.SamplingPattern == SamplingPattern.CMJ, "Sobol", "CMJ");
			var filterGlossy = new OptionDouble(RcCore.It.EngineSettings.FilterGlossy, 0.0, 100.0);
			var sampleClampDirect = new OptionDouble(RcCore.It.EngineSettings.SampleClampDirect, 0.0, 100.0);
			var sampleClampIndirect = new OptionDouble(RcCore.It.EngineSettings.SampleClampIndirect, 0.0, 100.0);
			var lightSamplingThreshold = new OptionDouble(RcCore.It.EngineSettings.LightSamplingThreshold, 0.0, 1.0);
			var sampleAllLights = new OptionToggle(RcCore.It.EngineSettings.SampleAllLights, "no", "yes");
			var sampleAllLightsIndirect = new OptionToggle(RcCore.It.EngineSettings.SampleAllLightsIndirect, "no", "yes");

			getNumber.AddOptionToggle("show_max_passes", ref showMaxPasses);
			getNumber.AddOptionInteger("min_bounces", ref minBounce);
			getNumber.AddOptionInteger("max_bounces", ref maxBounce);
			getNumber.AddOptionInteger("tile_x", ref tileX);
			getNumber.AddOptionInteger("tile_y", ref tileY);
			getNumber.AddOptionToggle("no_caustics", ref noCaustics);

			getNumber.AddOptionInteger("max_diffuse_bounce", ref maxDiffuseBounce);
			getNumber.AddOptionInteger("max_glossy_bounce", ref maxGlossyBounce);
			getNumber.AddOptionInteger("max_transmission_bounce", ref maxTransmissionBounce);
			getNumber.AddOptionInteger("max_volume_bounce", ref maxVolumeBounce);

			getNumber.AddOptionInteger("transparent_min_bounce", ref transparentMinBounce);
			getNumber.AddOptionInteger("transparent_max_bounce", ref transparentMaxBounce);
			getNumber.AddOptionToggle("transparent_shadows", ref transparentShadows);

			getNumber.AddOptionInteger("aa_samples", ref aaSamples);
			getNumber.AddOptionInteger("diffuse_samples", ref diffSamples);
			getNumber.AddOptionInteger("glossy_samples", ref glossySamples);

			getNumber.AddOptionDouble("sensor_width", ref sensorWidth);
			getNumber.AddOptionDouble("sensor_height", ref sensorHeight);

			getNumber.AddOptionToggle("integrator_method", ref branched);

			getNumber.AddOptionInteger("seed", ref seed, "Seed to use for sampling distribution");

			getNumber.AddOptionToggle("sampling_pattern", ref samplingPattern);
			getNumber.AddOptionDouble("filter_glossy", ref filterGlossy);
			getNumber.AddOptionDouble("sample_clamp_direct", ref sampleClampDirect);
			getNumber.AddOptionDouble("sample_clamp_indirect", ref sampleClampIndirect);
			getNumber.AddOptionDouble("light_sampling_threshold", ref lightSamplingThreshold);
			getNumber.AddOptionToggle("sample_all_lights", ref sampleAllLights);
			getNumber.AddOptionToggle("sample_all_lights_indirect", ref sampleAllLightsIndirect);

			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
				switch (getRc)
				{
					case GetResult.Number:
						RhinoApp.WriteLine($"We got: {getNumber.Number()}, {minBounce.CurrentValue}, {maxBounce.CurrentValue}");
						RcCore.It.EngineSettings.Samples = (int)getNumber.Number();
						RcCore.It.EngineSettings.ShowMaxPasses = showMaxPasses.CurrentValue;
						RcCore.It.EngineSettings.Seed = seed.CurrentValue;
						RcCore.It.EngineSettings.MaxBounce = maxBounce.CurrentValue;
						RcCore.It.EngineSettings.MinBounce = minBounce.CurrentValue;
						RcCore.It.EngineSettings.TileX = tileX.CurrentValue;
						RcCore.It.EngineSettings.TileY = tileY.CurrentValue;
						RcCore.It.EngineSettings.NoCaustics = noCaustics.CurrentValue;
						RcCore.It.EngineSettings.MaxDiffuseBounce = maxDiffuseBounce.CurrentValue;
						RcCore.It.EngineSettings.MaxGlossyBounce = maxGlossyBounce.CurrentValue;
						RcCore.It.EngineSettings.MaxTransmissionBounce = maxTransmissionBounce.CurrentValue;
						RcCore.It.EngineSettings.MaxVolumeBounce = maxVolumeBounce.CurrentValue;
						RcCore.It.EngineSettings.TransparentMinBounce = transparentMinBounce.CurrentValue;
						RcCore.It.EngineSettings.TransparentMaxBounce = transparentMaxBounce.CurrentValue;
						RcCore.It.EngineSettings.TransparentShadows = transparentShadows.CurrentValue;
						RcCore.It.EngineSettings.AaSamples = aaSamples.CurrentValue;
						RcCore.It.EngineSettings.DiffuseSamples = diffSamples.CurrentValue;
						RcCore.It.EngineSettings.GlossySamples = glossySamples.CurrentValue;
						RcCore.It.EngineSettings.SensorWidth = (float)sensorWidth.CurrentValue;
						RcCore.It.EngineSettings.SensorHeight = (float)sensorHeight.CurrentValue;
						RcCore.It.EngineSettings.IntegratorMethod = branched.CurrentValue ? IntegratorMethod.BranchedPath : IntegratorMethod.Path;
						RcCore.It.EngineSettings.SamplingPattern = SamplingPattern.CMJ;
						RcCore.It.EngineSettings.FilterGlossy = (float)filterGlossy.CurrentValue;
						RcCore.It.EngineSettings.SampleClampDirect = (float)sampleClampDirect.CurrentValue;
						RcCore.It.EngineSettings.SampleClampIndirect = (float)sampleClampIndirect.CurrentValue;
						RcCore.It.EngineSettings.LightSamplingThreshold = (float)lightSamplingThreshold.CurrentValue;
						RcCore.It.EngineSettings.SampleAllLights = sampleAllLights.CurrentValue;
						RcCore.It.EngineSettings.SampleAllLightsIndirect = sampleAllLightsIndirect.CurrentValue;
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

