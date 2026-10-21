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
using Rhino.UI;
using RhinoCyclesCore;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	[Guid("C802AF8A-7FD8-4281-93A4-B434961E2388")]
	[CommandStyle(Style.Hidden)]
	public class RhinoCyclesDisableGpu : Command
	{
		static RhinoCyclesDisableGpu _instance;
		public RhinoCyclesDisableGpu()
		{
			if(_instance==null) _instance = this;
		}
		public override string LocalName => Localization.LocalizeString("RhinoCyclesDisableGpu", 61);

		public override string EnglishName => "RhinoCyclesDisableGpu";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			// Backend=All is what this command has always done. Naming a single backend records it
			// as failed instead, which is otherwise only reachable with hardware that really fails -
			// that is how the switched off warning and the Retry button get tested. RH-98701.
			var choices = new List<string> { Localization.LocalizeString("All", 111) };
			choices.AddRange(Utilities.GpuBackendNames);
			var picked = 0;

			var go = new GetOption();
			go.SetCommandPrompt(Localization.LocalizeString("Press Enter to switch off GPU use", 112));
			var list = go.AddOptionList("Backend", choices, picked);
			go.AcceptNothing(true);
			while (true)
			{
				var res = go.Get();
				if (res == GetResult.Nothing) break;
				if (res == GetResult.Option)
				{
					if (go.OptionIndex() == list) picked = go.Option().CurrentListOptionIndex;
					continue;
				}
				return Result.Cancel;
			}

			if (picked == 0)
			{
				Utilities.DisableGpus();
				RhinoApp.WriteLine(Localization.LocalizeString("GPUs for RhinoCycles have now been disabled. Restart Rhino for the change to take effect.", 62));
				return Result.Success;
			}

			var name = choices[picked];
			if (!Utilities.TryFindGpuBackend(name, out var backend)) return Result.Failure;
			// Say in the record that this was not a real failure, so a report from a tester's
			// machine cannot be mistaken for a driver problem.
			Utilities.DisableGpu(backend,
				"switched off with RhinoCyclesDisableGpu, not a real failure");
			RhinoApp.WriteLine(string.Format(
				Localization.LocalizeString("{0} has been switched off as if it had failed to start. Restart Rhino for the change to take effect.", 113),
				name));
			return Result.Success;
		}
	}
}
