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

using ccl;
using Rhino;
using Rhino.Commands;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	[Guid("9e91d7ea-7990-471f-a944-ad9ececcc88b")]
	[CommandStyle(Style.Hidden)]
	public class ListDevices : Command
	{
		static ListDevices _instance;
		public ListDevices()
		{
			_instance = this;
		}

		///<summary>The only instance of the ListDevices command.</summary>
		public static ListDevices Instance => _instance;

		public override string EnglishName => "RhinoCycles_ListDevices";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();

			// Which ccycles.dll am I actually running? There are two ways to get
			// one - the prebuilt payload from big_libs, or a local +Cycles build -
			// and they are indistinguishable without asking. The Cycles version
			// says what the source was; the file date and path say which copy.
			RhinoApp.WriteLine($"Cycles {CSycles.version_string()}");
			var ccyclesPath = Path.Combine(
				Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
				"ccycles.dll");
			if (File.Exists(ccyclesPath))
			{
				RhinoApp.WriteLine($"	{ccyclesPath}");
				RhinoApp.WriteLine($"	built {File.GetLastWriteTime(ccyclesPath):yyyy-MM-dd HH:mm}");
			}
			RhinoApp.WriteLine("----------");

			var numDevices = Device.Count;
			var endS = numDevices != 1 ? "s" : "";
			RhinoApp.WriteLine($"We have {numDevices} device{endS}");
			RhinoApp.WriteLine("----------");
			foreach (var dev in Device.Devices)
			{
				RhinoApp.WriteLine($"	Device {dev.Id}: {dev.Name} > {dev.Description} > {dev.Num} | {dev.DisplayDevice} | {dev.Type} | GPU: {dev.IsGpu}");
			}
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
