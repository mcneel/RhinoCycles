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

using RhinoCyclesCore;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("0AB57C1A-7FDB-4C36-85D8-807E6A606389")]
	public class SetDebugOptions : Command
	{
		private static SetDebugOptions g_thecommand;

		public SetDebugOptions()
		{
			g_thecommand = this;
		}

		public override string EnglishName => "RhinoCycles_SetDebugOptions";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var get_number = new GetInteger();
			get_number.SetLowerLimit(2, false);
			get_number.SetUpperLimit(10000000, false);
			get_number.SetDefaultInteger(RcCore.It.EngineSettings.Samples);
			get_number.SetCommandPrompt("Set Debug Options");

			var toggle_verbose = new OptionToggle(RcCore.It.EngineSettings.Verbose, "No", "Yes");
			var toggle_interactive = new OptionToggle(RcCore.It.EngineSettings.UseInteractiveRenderer, "No", "Yes");

			var spotlight_factor = new OptionDouble(RcCore.It.EngineSettings.SpotlightFactor, 0.0, 1000000.0);
			var pointlight_factor = new OptionDouble(RcCore.It.EngineSettings.PointlightFactor, 0.0, 1000000.0);
			var sunlight_factor = new OptionDouble(RcCore.It.EngineSettings.SunlightFactor, 0.0, 1000000.0);
			var arealight_factor = new OptionDouble(RcCore.It.EngineSettings.ArealightFactor, 0.0, 1000000.0);
			var polish_factor = new OptionDouble(RcCore.It.EngineSettings.PolishFactor, 0.0, 1000000.0);

			get_number.AddOptionToggle("verbose", ref toggle_verbose);
			get_number.AddOptionToggle("use_interactive_renderer", ref toggle_interactive);

			get_number.AddOptionDouble("spotlight_factor", ref spotlight_factor);
			get_number.AddOptionDouble("pointlight_factor", ref pointlight_factor);
			get_number.AddOptionDouble("sunlight_factor", ref sunlight_factor);
			get_number.AddOptionDouble("arealight_factor", ref arealight_factor);
			get_number.AddOptionDouble("polish_factor", ref polish_factor);


			while (true)
			{
				var get_rc = get_number.Get();
				if (get_number.CommandResult() != Result.Success) return get_number.CommandResult();
				switch (get_rc)
				{
					case GetResult.Nothing:
					case GetResult.Number:
						RcCore.It.EngineSettings.Samples = get_number.Number();
						ReadOptions(toggle_verbose, toggle_interactive, spotlight_factor, pointlight_factor, sunlight_factor, arealight_factor, polish_factor);
						break;
					case GetResult.Option:
						ReadOptions(toggle_verbose, toggle_interactive, spotlight_factor, pointlight_factor, sunlight_factor, arealight_factor, polish_factor);
						continue;
					default:
						continue;
				}

				break;
			}
			return Result.Success;
		}

		private static void ReadOptions(OptionToggle toggle_verbose, OptionToggle toggle_interactive,
			OptionDouble spotlight_factor, OptionDouble pointlight_factor, OptionDouble sunlight_factor,
			OptionDouble arealight_factor, OptionDouble polish_factor)
		{
			RcCore.It.EngineSettings.Verbose = toggle_verbose.CurrentValue;
			RcCore.It.EngineSettings.UseInteractiveRenderer = toggle_interactive.CurrentValue;
			RcCore.It.EngineSettings.SpotlightFactor = (float) spotlight_factor.CurrentValue;
			RcCore.It.EngineSettings.PointlightFactor = (float) pointlight_factor.CurrentValue;
			RcCore.It.EngineSettings.SunlightFactor = (float) sunlight_factor.CurrentValue;
			RcCore.It.EngineSettings.ArealightFactor = (float) arealight_factor.CurrentValue;
			RcCore.It.EngineSettings.PolishFactor = (float) polish_factor.CurrentValue;
		}
	}
}
