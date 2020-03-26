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
using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using ccl.ShaderNodes;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("c682f808-6124-4b74-bb10-bee5dfbc55d2")]
	[CommandStyle(Style.Hidden)]
	public class SetSssMethod : Command
	{
		static SetSssMethod _instance;
		public SetSssMethod()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetThreads command.</summary>
		public static SetSssMethod Instance => _instance;

		public override string EnglishName => "TestSetSssMethod";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getOption = new GetOption();
			var cubic = getOption.AddOption("Cubic"); // = 40
			var gaussian = getOption.AddOption("Gaussian"); // = 41,
			var principled = getOption.AddOption("Principled"); // = 42,
			var burley = getOption.AddOption("Burley"); // = 43,
			var randomwalk = getOption.AddOption("RandomWalk"); // = 44,
			var principledrandomwalk = getOption.AddOption("PrincipledRandomWalk"); // = 45

			getOption.SetDefaultString(SubsurfaceScatteringNode.SssMethodFromInt(RcCore.It.AllSettings.SssMethod));

			var getOrc = getOption.Get();
			if (getOption.CommandResult() != Result.Success) return getOption.CommandResult();
			if(getOrc == GetResult.Option) {
				var idx = getOption.OptionIndex();
				string m = "Burley";
				switch(idx) {
					case 1:
						m = "Cubic";
						break;
					case 2:
						m = "Gaussian";
						break;
					case 3:
						m = "Principled";
						break;
					case 4:
						m = "Burley";
						break;
					case 5:
						m = "RandomWalk";
						break;
					case 6:
						m = "PrincipledRandomWalk";
						break;
					default:
						m = "Burley";
						break;
				}
				var i = SubsurfaceScatteringNode.IntFromSssMethod(m);
				RhinoApp.WriteLine($"User chose {m} ({i})");
				RcCore.It.AllSettings.SssMethod = i;
				return Result.Success;
			}
			return Result.Nothing;
		}
	}
}
