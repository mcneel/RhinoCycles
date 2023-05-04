/**
Copyright 2014-2021 Robert McNeel and Associates

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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCycles.Viewport;
using RhinoCyclesCore.Settings;

namespace RhinoCycles.Commands
{
	[Guid("0F322EED-0CEF-4A94-8759-633574974F60")]
	[CommandStyle(Style.Hidden)]
	public class TriggerChangeIntegrator : Command
	{
		static TriggerChangeIntegrator _instance;
		public TriggerChangeIntegrator()
		{
			if(_instance==null) _instance = this;
		}

		public override string EnglishName => "RhinoCycles_TriggerChangeIntegrator";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();
			List<RenderedViewport> rvps = RenderedViewport.GetRenderedViewports(doc);

			if(rvps.Count>0)
			{
				var integratorSettings = new RhinoCyclesCore.Settings.IntegratorSettings(new EngineDocumentSettings(doc.RuntimeSerialNumber));
				foreach(var rvp in rvps)
				{
					rvp.ChangeIntegrator(integratorSettings);
				}
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
