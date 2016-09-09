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
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCycles.Viewport;

namespace RhinoCycles.Commands
{
	[Guid("168C2084-CDA8-469E-BE98-4E0E8B8BD607")]
	public class ChangeSamples : Command
	{
		static ChangeSamples _instance;
		public ChangeSamples()
		{
			if(_instance==null) _instance = this;
		}

		public override string EnglishName => "RhinoCycles_ChangeSamples";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();
			if (doc.Views.ActiveView.ActiveViewport.DisplayMode.Id == Guid.Parse("69E0C7A5-1C6A-46C8-B98B-8779686CD181"))
			{
				var rvp = doc.Views.ActiveView.RenderedDisplayMode as RenderedViewport;

				if (rvp != null)
				{
					var getNumber = new GetInteger();
					getNumber.SetLowerLimit(1, true);
					getNumber.SetDefaultInteger(rvp.HudMaximumPasses()+100);
					getNumber.SetCommandPrompt("Set new sample count");
					var getRc = getNumber.Get();
					if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
					if (getRc == GetResult.Number)
					{
						var nr = getNumber.Number();
						RhinoApp.WriteLine($"User changes samples to {nr}");
						rvp.ChangeSamples(nr);
						return Result.Success;
					}
				}
			}

			RhinoApp.WriteLine("Active view isn't rendering with Cycles");

			return Result.Nothing;
		}
	}
}
