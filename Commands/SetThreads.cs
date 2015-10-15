using System;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("e677727c-41a7-42aa-9422-1faf387f3f66")]
	public class SetThreads : Command
	{
		static SetThreads _instance;
		public SetThreads()
		{
			_instance = this;
		}

		///<summary>The only instance of the SetThreads command.</summary>
		public static SetThreads Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_SetThreads"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var get_number = new GetInteger();
			get_number.SetLowerLimit(0, false);
			get_number.SetUpperLimit(Environment.ProcessorCount, false);
			get_number.SetDefaultInteger(Plugin.EngineSettings.Threads);
			get_number.SetCommandPrompt(String.Format("Set CPU render threads (max {0}, 0 for automatic)", Environment.ProcessorCount));
			var get_rc = get_number.Get();
			if (get_number.CommandResult() != Result.Success) return get_number.CommandResult();
			if (get_rc == GetResult.Number)
			{
				var nr = get_number.Number();
				RhinoApp.WriteLine(String.Format("User wants {0} CPU thread{1}", nr, nr > 0 ? "s" : ""));
				Plugin.EngineSettings.Threads = nr;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
