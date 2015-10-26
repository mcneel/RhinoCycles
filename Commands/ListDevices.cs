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
using System;
using Rhino;
using Rhino.Commands;
using ccl;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("9e91d7ea-7990-471f-a944-ad9ececcc88b")]
	public class ListDevices : Command
	{
		static ListDevices _instance;
		public ListDevices()
		{
			_instance = this;
		}

		///<summary>The only instance of the ListDevices command.</summary>
		public static ListDevices Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ListDevices"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();

			var num_devices = Device.Count;
			RhinoApp.WriteLine(String.Format("We have {0} device{1}", num_devices, num_devices != 1 ? "s" : ""));
			RhinoApp.WriteLine("----------");
			foreach (var dev in Device.Devices)
			{
				RhinoApp.WriteLine(String.Format("	Device {0}: {1} > {2} > {3} | {4} | {5} | {6} | {7}", dev.Id,
					dev.Name, dev.Description, dev.Num,
					dev.DisplayDevice, dev.AdvancedShading,
					dev.PackImages, dev.Type));
			}
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
