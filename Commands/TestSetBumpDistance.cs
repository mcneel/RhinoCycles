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
using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("15443ac7-3e43-4dde-8705-529a2a672374")]
	[CommandStyle(Style.Hidden)]
	public class TestSetBumpDistance : Command
	{
		static TestSetBumpDistance _instance;
		public TestSetBumpDistance()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetBumpStrength command.</summary>
		public static TestSetBumpDistance Instance => _instance;

		public override string EnglishName => "TestSetBumpDistance";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getNumber = new GetNumber();
			getNumber.SetDefaultNumber(RcCore.It.AllSettings.BumpDistance);
			getNumber.SetCommandPrompt($"Set bump distance");
			var getRc = getNumber.Get();
			if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
			if (getRc == GetResult.Number)
			{
				var nr = getNumber.Number();
				RhinoApp.WriteLine($"User wants bump distance {nr}");
				RcCore.It.AllSettings.BumpDistance = (float)nr;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
