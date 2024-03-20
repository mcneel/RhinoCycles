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
	[Guid("f076c1c3-5f2e-45e7-9d01-bd1d7ba902f5")]
	[CommandStyle(Style.Hidden)]
	public class RhinoCyclesLogManualMarker : Command
	{
		static RhinoCyclesLogManualMarker _instance;
		public RhinoCyclesLogManualMarker()
		{
			if (_instance == null) _instance = this;
		}

		public override string LocalName => LOC.COMMANDNAME("RhinoCyclesLogManualMarker");

		public override string EnglishName => "RhinoCyclesLogManualMarker";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			RcCore.It.StartLogStopwatch("MANUAL MARKER");

			return Result.Success;
		}
	}
}