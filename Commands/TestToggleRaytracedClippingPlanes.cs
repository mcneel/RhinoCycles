/**
Copyright 2014-2017 Robert McNeel and Associates

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

namespace RhinoCycles.Commands
{

	[System.Runtime.InteropServices.Guid("7a5797c2-954b-49e4-8bec-b507d476631e")]
	[CommandStyle(Style.Hidden)]
	public class TestToggleRaytracedClippingPlanes : Command
	{
		static TestToggleRaytracedClippingPlanes _instance;
		public TestToggleRaytracedClippingPlanes()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetBumpStrength command.</summary>
		public static TestToggleRaytracedClippingPlanes Instance => _instance;

		public override string EnglishName => "TestToggleRaytracedClippingPlanes";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{

			var oldval = RhinoCyclesCore.Core.RcCore.It.EngineSettings.RaytracedClippingPlanes;
			RhinoCyclesCore.Core.RcCore.It.EngineSettings.RaytracedClippingPlanes = !oldval;
			return Result.Success;
		}
	}
}
