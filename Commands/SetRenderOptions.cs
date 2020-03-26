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
			var getNumber = new GetNumber();
			getNumber.SetLowerLimit(2.0, false);
			getNumber.SetUpperLimit(uint.MaxValue, false);
			getNumber.SetDefaultInteger(RcCore.It.AllSettings.Samples);
			getNumber.SetCommandPrompt("Set render samples");

			var showMaxPasses = new OptionToggle(RcCore.It.AllSettings.ShowMaxPasses, "HideMaxPasses", "ShowMaxPasses");

			var maxBounce = new OptionInteger(RcCore.It.AllSettings.MaxBounce, 0, 500);
			var tileX = new OptionInteger(RcCore.It.AllSettings.TileX, 0, 10000);
			var tileY = new OptionInteger(RcCore.It.AllSettings.TileY, 0, 10000);


			var maxDiffuseBounce = new OptionInteger(RcCore.It.AllSettings.MaxDiffuseBounce, 0, 200);
			var maxGlossyBounce = new OptionInteger(RcCore.It.AllSettings.MaxGlossyBounce, 0, 200);
			var maxTransmissionBounce = new OptionInteger(RcCore.It.AllSettings.MaxTransmissionBounce, 0, 200);
			var maxVolumeBounce = new OptionInteger(RcCore.It.AllSettings.MaxVolumeBounce, 0, 200);

			var noCaustics = new OptionToggle(RcCore.It.AllSettings.NoCaustics, "Caustics", "NoCaustics");

			var aaSamples = new OptionInteger(RcCore.It.AllSettings.AaSamples, 1, 100);
			var diffSamples = new OptionInteger(RcCore.It.AllSettings.DiffuseSamples, 1, 100);
			var glossySamples = new OptionInteger(RcCore.It.AllSettings.GlossySamples, 1, 100);

			var seed = new OptionInteger(RcCore.It.AllSettings.Seed, 0, int.MaxValue);

			var sensorWidth = new OptionDouble(RcCore.It.AllSettings.SensorWidth, 10.0, 100.0);
			var sensorHeight = new OptionDouble(RcCore.It.AllSettings.SensorHeight, 10.0, 100.0);

			var transparentMaxBounce = new OptionInteger(RcCore.It.AllSettings.TransparentMaxBounce, 0, 200);

			var filterGlossy = new OptionDouble(RcCore.It.AllSettings.FilterGlossy, 0.0, 100.0);
			var sampleClampDirect = new OptionDouble(RcCore.It.AllSettings.SampleClampDirect, 0.0, 100.0);
			var sampleClampIndirect = new OptionDouble(RcCore.It.AllSettings.SampleClampIndirect, 0.0, 100.0);
			var lightSamplingThreshold = new OptionDouble(RcCore.It.AllSettings.LightSamplingThreshold, 0.0, 1.0);
			var sampleAllLights = new OptionToggle(RcCore.It.AllSettings.SampleAllLights, "no", "yes");
			var sampleAllLightsIndirect = new OptionToggle(RcCore.It.AllSettings.SampleAllLightsIndirect, "no", "yes");

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
			getNumber.AddOptionToggle("sample_all_lights", ref sampleAllLights);
			getNumber.AddOptionToggle("sample_all_lights_indirect", ref sampleAllLightsIndirect);

			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
				switch (getRc)
				{
					case GetResult.Number:
						RhinoApp.WriteLine($"We got: {getNumber.Number()}, {maxBounce.CurrentValue}");
						RcCore.It.AllSettings.Samples = (int)getNumber.Number();
						RcCore.It.AllSettings.ShowMaxPasses = showMaxPasses.CurrentValue;
						RcCore.It.AllSettings.Seed = seed.CurrentValue;
						RcCore.It.AllSettings.MaxBounce = maxBounce.CurrentValue;
						RcCore.It.AllSettings.TileX = tileX.CurrentValue;
						RcCore.It.AllSettings.TileY = tileY.CurrentValue;
						RcCore.It.AllSettings.NoCaustics = noCaustics.CurrentValue;
						RcCore.It.AllSettings.MaxDiffuseBounce = maxDiffuseBounce.CurrentValue;
						RcCore.It.AllSettings.MaxGlossyBounce = maxGlossyBounce.CurrentValue;
						RcCore.It.AllSettings.MaxTransmissionBounce = maxTransmissionBounce.CurrentValue;
						RcCore.It.AllSettings.MaxVolumeBounce = maxVolumeBounce.CurrentValue;
						RcCore.It.AllSettings.TransparentMaxBounce = transparentMaxBounce.CurrentValue;
						RcCore.It.AllSettings.AaSamples = aaSamples.CurrentValue;
						RcCore.It.AllSettings.DiffuseSamples = diffSamples.CurrentValue;
						RcCore.It.AllSettings.GlossySamples = glossySamples.CurrentValue;
						RcCore.It.AllSettings.SensorWidth = (float)sensorWidth.CurrentValue;
						RcCore.It.AllSettings.SensorHeight = (float)sensorHeight.CurrentValue;
						RcCore.It.AllSettings.FilterGlossy = (float)filterGlossy.CurrentValue;
						RcCore.It.AllSettings.SampleClampDirect = (float)sampleClampDirect.CurrentValue;
						RcCore.It.AllSettings.SampleClampIndirect = (float)sampleClampIndirect.CurrentValue;
						RcCore.It.AllSettings.LightSamplingThreshold = (float)lightSamplingThreshold.CurrentValue;
						RcCore.It.AllSettings.SampleAllLights = sampleAllLights.CurrentValue;
						RcCore.It.AllSettings.SampleAllLightsIndirect = sampleAllLightsIndirect.CurrentValue;
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

