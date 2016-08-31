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

using RhinoCyclesCore;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("AB6ED632-D539-4E22-8DF1-D72E1C451064")]
	public class ToggleShaders : Command
	{
		private static ToggleShaders g_thecommand;

		public ToggleShaders()
		{
			g_thecommand = this;
		}

		public override string EnglishName => "RhinoCycles_ToggleSimpleShaders";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			RcCore.It.EngineSettings.UseSimpleShaders = !RcCore.It.EngineSettings.UseSimpleShaders;
			RhinoApp.WriteLine($"UseSimpleShaders set to {RcCore.It.EngineSettings.UseSimpleShaders}");
			return Result.Success;
		}
	}
}
