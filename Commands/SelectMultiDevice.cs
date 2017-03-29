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

using System.Linq;
using Rhino;
using Rhino.Commands;
using ccl;
using RhinoCyclesCore.Core;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Collections.Generic;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("50988F79-779D-49CC-80EB-8D776A83D31B")]
	[CommandStyle(Style.Hidden)]
	public class SelectMultiDevice : Command
	{
		static SelectMultiDevice _instance;
		public SelectMultiDevice()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SelectMultiDevice command.</summary>
		public static SelectMultiDevice Instance => _instance;

		public override string EnglishName => "TestSelectMultiDevice";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();
			var numDevices = Device.Count;
			var endS = numDevices != 1 ? "s" : "";
			RhinoApp.WriteLine($"We have {numDevices} device{endS}");
			RhinoApp.WriteLine("----------");
			List<int> allowedIds = new List<int>();
			foreach (var dev in Device.Devices)
			{
				if (dev.Id >= 100000)
				{
					RhinoApp.WriteLine($"	Device {dev.Id}: {dev.Name} ({dev.Description})");
					allowedIds.Add((int)dev.Id);
				}
			}
			if(allowedIds.Count < 1)
			{
				RhinoApp.WriteLine("No multi-devices available");
				return Result.Nothing;
			}
			var lowest = allowedIds.Min();
			var highest = allowedIds.Min();
			RhinoApp.WriteLine("----------");
			var getNumber = new GetInteger();
			getNumber.SetLowerLimit(lowest, false);
			getNumber.SetUpperLimit(highest, false);
			getNumber.SetDefaultInteger(lowest);
			getNumber.SetCommandPrompt($"Select multi-device to render");
			var getRc = getNumber.Get();
			if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
			if (getRc == GetResult.Number)
			{
				var idx = getNumber.Number();
				Device dev = Device.GetDevice(idx);
				RhinoApp.WriteLine($"User selected device {idx}: {dev}");
				List<int> sdidx = new List<int>();
				if(dev.IsMulti)
				{
					foreach(var sd in dev.SubdevicesIndex)
					{
						RhinoApp.WriteLine($"  {sd.Item1} {sd.Item2}");
						sdidx.Add(sd.Item1);
					}
				}
				var idxstr = string.Join(",", sdidx);

				RcCore.It.EngineSettings.SelectedDeviceStr = idxstr;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
