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

using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("0AB57C1A-7FDB-4C36-85D8-807E6A606389")]
	[CommandStyle(Style.Hidden)]
	public class SetDebugOptions : Command
	{
		private static SetDebugOptions _gThecommand;

		public SetDebugOptions()
		{
			if(_gThecommand==null) _gThecommand = this;
		}

		public override string EnglishName => "RhinoCycles_SetDebugOptions";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getNumber = new GetInteger();
			getNumber.SetLowerLimit(0, false);
			getNumber.SetUpperLimit(500, false);
			getNumber.SetDefaultInteger(RcCore.It.AllSettings.ThrottleMs);
			getNumber.SetCommandPrompt("Set throttle (in ms)");

			var toggleVerbose = new OptionToggle(RcCore.It.AllSettings.Verbose, "No", "Yes");

			var spotlightFactor = new OptionDouble(RcCore.It.AllSettings.SpotLightFactor, 0.0, 1000000.0);
			var pointlightFactor = new OptionDouble(RcCore.It.AllSettings.PointLightFactor, 0.0, 1000000.0);
			var sunlightFactor = new OptionDouble(RcCore.It.AllSettings.SunLightFactor, 0.0, 1000000.0);
			var arealightFactor = new OptionDouble(RcCore.It.AllSettings.AreaLightFactor, 0.0, 1000000.0);
			var polishFactor = new OptionDouble(RcCore.It.AllSettings.PolishFactor, 0.0, 1000000.0);

			getNumber.AddOptionToggle("verbose", ref toggleVerbose);

			getNumber.AddOptionDouble("spotlight_factor", ref spotlightFactor);
			getNumber.AddOptionDouble("pointlight_factor", ref pointlightFactor);
			getNumber.AddOptionDouble("sunlight_factor", ref sunlightFactor);
			getNumber.AddOptionDouble("arealight_factor", ref arealightFactor);
			getNumber.AddOptionDouble("polish_factor", ref polishFactor);


			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
				switch (getRc)
				{
					case GetResult.Nothing:
					case GetResult.Number:
						RcCore.It.AllSettings.ThrottleMs = getNumber.Number();
						ReadOptions(toggleVerbose, spotlightFactor, pointlightFactor, sunlightFactor, arealightFactor, polishFactor);
						break;
					case GetResult.Option:
						ReadOptions(toggleVerbose, spotlightFactor, pointlightFactor, sunlightFactor, arealightFactor, polishFactor);
						continue;
					default:
						continue;
				}

				break;
			}
			return Result.Success;
		}

		private static void ReadOptions(OptionToggle toggleVerbose,
			OptionDouble spotlightFactor, OptionDouble pointlightFactor, OptionDouble sunlightFactor,
			OptionDouble arealightFactor, OptionDouble polishFactor)
		{
			RcCore.It.AllSettings.Verbose = toggleVerbose.CurrentValue;
			RcCore.It.AllSettings.SpotLightFactor = (float) spotlightFactor.CurrentValue;
			RcCore.It.AllSettings.PointLightFactor = (float) pointlightFactor.CurrentValue;
			RcCore.It.AllSettings.SunLightFactor = (float) sunlightFactor.CurrentValue;
			RcCore.It.AllSettings.AreaLightFactor = (float) arealightFactor.CurrentValue;
			RcCore.It.AllSettings.PolishFactor = (float) polishFactor.CurrentValue;
		}
	}
}
