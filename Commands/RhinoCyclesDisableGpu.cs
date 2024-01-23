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
using Rhino.UI;
using RhinoCyclesCore;
using System;
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
			Utilities.DisableGpus();
			var str = Localization.LocalizeString("GPUs for RhinoCycles have now been disabled. Restart Rhino for the change to take effect.", 62);
			RhinoApp.WriteLine(str);
			return Result.Success;
		}
	}
}
