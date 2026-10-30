/**
Copyright 2026 Robert McNeel and Associates

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

using Grasshopper2.Diagnostics;
using Grasshopper2.Framework;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	public sealed class CyclesComponentsPluginInfo : Plugin
	{
		public override IIcon Icon => NodeCatalog.Icon("cycles");
		public override string LicenceDescription => DefaultLicence;
		public override string LicenceAgreement => "https://www.rhino3d.com/9/eula";

		public override void OnLoaded()
		{
			// Reading the registry loads ccycles; without it there are no node components,
			// but the plugin itself still loads.
			try
			{
				var count = CyclesNodeProxy.RegisterAll(this);
				Logger.Add(Level.Info, $"Cycles: registered {count} shader node components.");
			}
			catch (Exception ex)
			{
				Logger.Add(ex, $"{nameof(CyclesComponentsPluginInfo)}.{nameof(OnLoaded)}()");
			}
		}
	}
}
