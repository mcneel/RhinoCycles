/**
Copyright 2014-2024 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0(the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using Rhino.PlugIns;
using Rhino.Render;
using Rhino.Runtime;
using Rhino.UI;
using System.Collections.Generic;

namespace RhinoCycles
{
	public class CyclesPlugIn : PlugIn
	{
		ICyclesPlugin pluginImpl = null;
		/// <summary>
		/// Make sure we load AtStartup so that our view mode is
		/// available even when RhinoCycles isn't the current renderer
		/// </summary>
#if NET7_0_OR_GREATER
		public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;
#else
		public override PlugInLoadTime LoadTime => PlugInLoadTime.Disabled;
#endif

		private bool pluginLoaded = false;

		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			LoadReturnCode rc = LoadReturnCode.ErrorNoDialog;
#if NET7_0_OR_GREATER
			if(!pluginLoaded)
			{
				pluginImpl = new CyclesPlugInImpl(this);
				var implrc = pluginImpl.OnLoad(ref errorMessage) switch
				{
					CyclesPluginLoadReturnCode.ErrorShowDialog => LoadReturnCode.ErrorShowDialog,
					CyclesPluginLoadReturnCode.ErrorNoDialog => LoadReturnCode.ErrorNoDialog,
					_ => LoadReturnCode.Success
				};
				pluginLoaded = true;
			}
			string os = HostUtils.RunningOnWindows ? "Windows" : "MacOS";
#else
			// TODO: localize
			errorMessage = "RhinoCycles is not supported on .NET Framework.";
			if (!pluginLoaded)
			{
				pluginLoaded = true;
			}
#endif
			return rc;
		}

		/// <summary>
		/// Initialise Cycles if necessary.
		/// </summary>
		public void InitialiseCSycles()
		{
#if NET7_0_OR_GREATER
			if(pluginImpl == null) return;

			pluginImpl.InitialiseCSycles();
#endif
		}

		protected override void OnShutdown()
		{
#if NET7_0_OR_GREATER
			if(pluginImpl == null) return;

			pluginImpl.OnShutdown();
			base.OnShutdown();
#endif
		}

		protected override void OptionsDialogPages(List<Rhino.UI.OptionsDialogPage> pages)
		{
#if NET7_0_OR_GREATER
			if(pluginImpl == null) return;

			OptionsDialogPage optionsPage = (OptionsDialogPage)pluginImpl.OptionsDialogPage();
			pages.Add(optionsPage);
			base.OptionsDialogPages(pages);
#endif
		}

		public override bool IsTextureSupported(RenderTexture texture)
		{
#if NET7_0_OR_GREATER
			if(pluginImpl == null) return false;

			return pluginImpl.IsTextureSupported(texture);
#else
			return false;
#endif
		}
	}
}
