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
using ccl;
using RhinoCyclesCore.Core;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Collections.Generic;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("75b0608f-ddc9-4377-bb32-99218f227a32")]
	[CommandStyle(Style.Hidden)]
	public class TestCreateMultiCuda : Command
	{
		static TestCreateMultiCuda _instance;
		public TestCreateMultiCuda()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the TestCreateMultiCuda command.</summary>
		public static TestCreateMultiCuda Instance => _instance;

		public override string EnglishName => "TestCreateMultiCuda";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();
			var numDevices = Device.Count;
			var endS = numDevices != 1 ? "s" : "";
			RhinoApp.WriteLine("Possible devices to select from:");
			HashSet<int> allowedIds = new HashSet<int>();
			foreach (var dev in Device.Devices)
			{
				if (dev.IsCpu || dev.IsCuda)
				{
					RhinoApp.WriteLine($"	Device {dev.Id}: {dev.Name} ({dev.Description})");
					allowedIds.Add((int)dev.Id);
				}
			}
			if(allowedIds.Count <2)
			{
				RhinoApp.WriteLine("Not enough devices to create a multi-device from");
				return Result.Nothing;
			}
			var getString = new GetString();
			getString.SetCommandPrompt($"Enter comma-delimited string");
			var getRc = getString.Get();
			if (getString.CommandResult() != Result.Success) return getString.CommandResult();
			if (getRc == GetResult.String)
			{
				var res = getString.StringResult();
				var set = Device.IdSetFromString(res);
				set.IntersectWith(allowedIds);
				List<int> idList = new List<int>();
				foreach (var s in set) idList.Add(s);
				idList.Sort();

				if(idList.Count<2)
				{
					RhinoApp.WriteLine("A multi-device needs to have at least 2 devices");
					return Result.Failure;
				}

				List<Device> devList = new List<Device>();
				foreach(var id in idList)
				{
					devList.Add(Device.GetDevice(id));
				}

				var multiDevice = Device.CreateMultiDevice(devList);
				RhinoApp.WriteLine($"	Device {multiDevice.Id}: {multiDevice.Name} ({multiDevice.Description}), {multiDevice.SubdeviceCount} devices");
				foreach(var sd in multiDevice.SubdevicesIndex)
				{
					RhinoApp.WriteLine($"  {sd.Item1} {sd.Item2}");
				}

				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
