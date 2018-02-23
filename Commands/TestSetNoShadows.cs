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

using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCyclesCore;

namespace RhinoCycles.Commands
{

	[System.Runtime.InteropServices.Guid("43242d45-c432-4450-944e-ab590ba3ad8b")]
	[CommandStyle(Style.Hidden)]
	public class TestSetNoShadows : Command
	{
		static TestSetNoShadows _instance;
		public TestSetNoShadows()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetBumpStrength command.</summary>
		public static TestSetNoShadows Instance => _instance;

		public override string EnglishName => "TestSetNoShadows";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			if (doc == null || doc.Views.ActiveView == null) return Result.Nothing;

			var oldval = RhinoCyclesCore.Core.RcCore.It.EngineSettings.NoShadows;
			RhinoCyclesCore.Core.RcCore.It.EngineSettings.NoShadows = !oldval;

			if(doc.Views.ActiveView.RealtimeDisplayMode is RhinoCycles.Viewport.RenderedViewport rdp)
			{
				rdp.ToggleNoShadows();
				RhinoApp.WriteLine($"Set NoShadows from {oldval} to {RhinoCyclesCore.Core.RcCore.It.EngineSettings.NoShadows}");
				return Result.Success;
			}
			

			return Result.Nothing;
		}
	}
}
