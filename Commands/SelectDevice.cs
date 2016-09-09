﻿/**
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

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("32D6D91A-779D-42D5-B76C-2974D5DBD7CA")]
	public class SelectDevice : Command
	{
		static SelectDevice _instance;
		public SelectDevice()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SelectDevice command.</summary>
		public static SelectDevice Instance => _instance;

		public override string EnglishName => "RhinoCycles_SelectDevice";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();
			var getNumber = new GetInteger();
			getNumber.SetLowerLimit(-1, false);
			getNumber.SetUpperLimit((int)(Device.Count-1), false);
			getNumber.SetDefaultInteger(RcCore.It.EngineSettings.SelectedDevice);
			getNumber.SetCommandPrompt($"Select device to render on (-1 for default, 0-{Device.Count - 1})");
			var getRc = getNumber.Get();
			if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
			if (getRc == GetResult.Number)
			{
				var idx = getNumber.Number();
				Device dev = idx > -1 ? Device.GetDevice(idx) : Device.FirstCuda;
				RhinoApp.WriteLine($"User selected device {idx}: {dev}");
				RcCore.It.EngineSettings.SelectedDevice = idx;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
