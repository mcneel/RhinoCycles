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
using RhinoCyclesCore.Core;
using System;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	[Guid("6698E514-4A6E-4E79-BC26-A5C91BA75DCC")]
	[CommandStyle(Style.Hidden)]
	public class RhinoCyclesRecompileKernels : Command
	{
		static RhinoCyclesRecompileKernels _instance;
		public RhinoCyclesRecompileKernels()
		{
			if (_instance == null) _instance = this;
		}

		public override string LocalName => LOC.COMMANDNAME("RhinoCyclesRecompileKernels");

		public override string EnglishName => "RhinoCyclesRecompileKernels";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			RcCore.It.RecompileKernels();
			return Result.Success;
		}
	}
}