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

using System.Diagnostics;
using System.Reflection;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("CB9C1C0D-83F6-4157-BD57-F2AD6093FF73")]
	[CommandStyle(Style.Hidden)]
	public class ShowInfo : Command
	{
		static ShowInfo _instance;
		public ShowInfo()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowInfo command.</summary>
		public static ShowInfo Instance => _instance;

		public override string EnglishName => "RhinoCycles_ShowInfo";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();

			var rhcyclesAss = Assembly.GetExecutingAssembly();
			var csyclesAss = Assembly.GetAssembly(typeof(ccl.Client));
			var csyclesFvi = FileVersionInfo.GetVersionInfo(csyclesAss.Location);

			RhinoApp.WriteLine("----------");
			RhinoApp.WriteLine($"RhinoCycles {RhinoBuildConstants.VERSION_STRING} @ {rhcyclesAss.Location}");
			RhinoApp.WriteLine($"CCSycles {csyclesFvi.FileVersion} @ {csyclesAss.Location}");
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
