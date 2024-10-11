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
using RhinoCycles.Viewport;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	[Guid("168C2084-CDA8-469E-44AA-4E0E8B8BD607")]
	[CommandStyle(Style.Hidden)]
	public class SetAdaptiveSampling : Command
	{
		static SetAdaptiveSampling _instance;
		public SetAdaptiveSampling()
		{
			if(_instance==null) _instance = this;
		}

		public override string EnglishName => "RhinoCycles_SetAdaptiveSampling";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();
			var getNumber = new GetNumber();
			getNumber.SetLowerLimit(1, true);
			getNumber.SetDefaultInteger(RcCore.It.AllSettings.AdaptiveMinSamples);
			getNumber.SetCommandPrompt("Set adaptive minimum samples");

			var useAdaptiveSamplingOption = new OptionToggle(RcCore.It.AllSettings.UseAdaptiveSampling, "Off", "On");

			var adaptiveThresholdOption = new OptionDouble(RcCore.It.AllSettings.AdaptiveThreshold, 0.00001, 1.0);

			getNumber.AddOptionToggle("UseAdaptiveSampling", ref useAdaptiveSamplingOption);
			getNumber.AddOptionDouble("AdaptiveThreshold", ref adaptiveThresholdOption);


			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success)
					return getNumber.CommandResult();

				switch (getRc)
				{
					case GetResult.Number:
						var minSamples = (int)getNumber.Number();
						var useAdaptiveSampling = useAdaptiveSamplingOption.CurrentValue;
						var adaptiveThreshold = (float)adaptiveThresholdOption.CurrentValue;
						RcCore.It.AllSettings.AdaptiveMinSamples = minSamples;
						RcCore.It.AllSettings.UseAdaptiveSampling = useAdaptiveSampling;
						RcCore.It.AllSettings.AdaptiveThreshold = adaptiveThreshold;
						var onoff = useAdaptiveSampling ? "on" : "off";
						RhinoApp.WriteLine($"Adaptive sampling: {onoff}, min samples: {minSamples}, threshold: {adaptiveThreshold}");
						return Result.Success;
					case GetResult.Option:
						continue;
				}

			}
		}
	}
}
