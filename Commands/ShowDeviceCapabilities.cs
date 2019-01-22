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

using System.Runtime.InteropServices;
using ccl;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles.Commands
{
	[Guid("7413124D-31F8-4B15-B92A-B6A17A884320")]
	[CommandStyle(Style.Hidden)]
	public class ShowDeviceCapabilities : Command
	{
		static ShowDeviceCapabilities _instance;
		public ShowDeviceCapabilities()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowDeviceCapabilities command.</summary>
		public static ShowDeviceCapabilities Instance => _instance;

		public override string EnglishName => "RhinoCycles_ShowDeviceCapabilities";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var capabilities = Device.Capabilities;
			RhinoApp.WriteLine($"The following capabilities have been found by Cycles:\n----------\n{capabilities}\n\n----------");
			return Result.Success;
		}
	}
}
