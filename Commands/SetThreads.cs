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
using RhinoCyclesCore.Core;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("e677727c-41a7-42aa-9422-1faf387f3f66")]
	[CommandStyle(Style.Hidden)]
	public class SetThreads : Command
	{
		static SetThreads _instance;
		public SetThreads()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetThreads command.</summary>
		public static SetThreads Instance => _instance;

		public override string EnglishName => "RhinoCycles_SetThreads";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getNumber = new GetInteger();
			getNumber.SetLowerLimit(0, false);
			getNumber.SetUpperLimit(RhinoCyclesCore.Utilities.GetSystemProcessorCount(), false);
			getNumber.SetDefaultInteger(RcCore.It.AllSettings.Threads);
			getNumber.SetCommandPrompt($"Set Cpu render threads (max {RhinoCyclesCore.Utilities.GetSystemProcessorCount()}, 0 for automatic)");
			var getRc = getNumber.Get();
			if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
			if (getRc == GetResult.Number)
			{
				var nr = getNumber.Number();
				var endS = nr != 1 ? "s" : "";
				RhinoApp.WriteLine($"User wants {nr} Cpu thread{endS}");
				RcCore.It.AllSettings.Threads = nr;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
