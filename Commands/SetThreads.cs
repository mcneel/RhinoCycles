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
