/**
Copyright 2014-2016 Robert McNeel and Associates

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

using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("f463e853-eaa2-401e-82ba-3d3e9cfb168b")]
	[CommandStyle(Style.Hidden)]
	public class TestSaveDebugImagesToggle : Command
	{
		static TestSaveDebugImagesToggle _instance;
		public TestSaveDebugImagesToggle()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the TestSaveDebugImagesToggle command.</summary>
		public static TestSaveDebugImagesToggle Instance => _instance;

		public override string EnglishName => "TestSaveDebugImagesToggle";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			RcCore.It.AllSettings.SaveDebugImages = !RcCore.It.AllSettings.SaveDebugImages;
			var saving = RcCore.It.AllSettings.SaveDebugImages ? "Saving" : "Not saving";
			RhinoApp.WriteLine($"{saving} debug images");
			return Result.Success;
		}
	}
}
