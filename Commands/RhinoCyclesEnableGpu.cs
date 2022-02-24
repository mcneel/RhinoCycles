/**
Copyright 2014-2021 Robert McNeel and Associates

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
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCycles.Viewport;
using RhinoCyclesCore;
using RhinoCyclesCore.Core;
using Rhino.UI;

namespace RhinoCycles.Commands
{
	[Guid("0B994E8A-CB1B-48F2-918A-EC5935998F16")]
	public class RhinoCyclesEnableGpu : Command
	{
		static RhinoCyclesEnableGpu _instance;
		public RhinoCyclesEnableGpu()
		{
			if(_instance==null) _instance = this;
		}
		public override string LocalName => Localization.LocalizeString("RhinoCyclesEnableGpu", 63);

		public override string EnglishName => "RhinoCyclesEnableGpu";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Utilities.EnableGpus();
			var str = Localization.LocalizeString("GPUs for RhinoCycles have now been enabled. Restart Rhino for the change to take effect.", 64);
			RhinoApp.WriteLine(str);
			return Result.Success;
		}
	}
}
