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

namespace RhinoCycles.Commands
{
	[Guid("168C2084-CDA8-469E-BE98-4E0E8B8BD607")]
	public class ChangeSamples : Command
	{
		static ChangeSamples _instance;
		public ChangeSamples()
		{
			_instance = this;
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
					var get_number = new GetInteger();
					get_number.SetLowerLimit(1, true);
					get_number.SetDefaultInteger(rvp.HudMaximumPasses()+100);
					get_number.SetCommandPrompt("Set new sample count");
					var get_rc = get_number.Get();
					if (get_number.CommandResult() != Result.Success) return get_number.CommandResult();
					if (get_rc == GetResult.Number)
					{
						var nr = get_number.Number();
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
