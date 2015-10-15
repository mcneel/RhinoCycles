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

		public override string EnglishName
		{
			get { return "RhinoCycles_SetDebugOptions"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var get_number = new GetInteger();
			get_number.SetLowerLimit(2, false);
			get_number.SetUpperLimit(10000000, false);
			get_number.SetDefaultInteger(Plugin.EngineSettings.Samples);
			get_number.SetCommandPrompt("Set Debug Options");

			var toggle_verbose = new OptionToggle(Plugin.EngineSettings.Verbose, "No", "Yes");
			var toggle_interactive = new OptionToggle(Plugin.EngineSettings.UseInteractiveRenderer, "No", "Yes");

			var spotlight_factor = new OptionDouble(Plugin.EngineSettings.SpotlightFactor, 0.0, 1000000.0);
			var pointlight_factor = new OptionDouble(Plugin.EngineSettings.PointlightFactor, 0.0, 1000000.0);
			var sunlight_factor = new OptionDouble(Plugin.EngineSettings.SunlightFactor, 0.0, 1000000.0);
			var arealight_factor = new OptionDouble(Plugin.EngineSettings.ArealightFactor, 0.0, 1000000.0);
			var polish_factor = new OptionDouble(Plugin.EngineSettings.PolishFactor, 0.0, 1000000.0);

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
						Plugin.EngineSettings.Samples = get_number.Number();
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
			Plugin.EngineSettings.Verbose = toggle_verbose.CurrentValue;
			Plugin.EngineSettings.UseInteractiveRenderer = toggle_interactive.CurrentValue;
			Plugin.EngineSettings.SpotlightFactor = (float) spotlight_factor.CurrentValue;
			Plugin.EngineSettings.PointlightFactor = (float) pointlight_factor.CurrentValue;
			Plugin.EngineSettings.SunlightFactor = (float) sunlight_factor.CurrentValue;
			Plugin.EngineSettings.ArealightFactor = (float) arealight_factor.CurrentValue;
			Plugin.EngineSettings.PolishFactor = (float) polish_factor.CurrentValue;
		}
	}
}
