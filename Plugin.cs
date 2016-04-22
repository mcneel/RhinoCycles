/**
Copyright 2014-2016 Robert McNeel and Associates

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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ccl;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.PlugIns;
using Rhino.Render;
using RhinoCyclesCore;

namespace RhinoCycles
{
	public class Plugin : PlugIn
	{
		/// <summary>
		/// Make sure we load AtStartup so that our view mode is
		/// available even when RhinoCycles isn't the current renderer
		/// </summary>
		public override PlugInLoadTime LoadTime
		{
			get
			{
				return PlugInLoadTime.AtStartup;
			}
		}

		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			RenderContent.RegisterContent(this);
			// code got moved to separate DLL so use that to register from.
			var rccoreass = typeof(RhinoCyclesCore.RcCore).Assembly;
			RenderContent.RegisterContent(rccoreass, Id);
			RenderedDisplayMode.RegisterDisplayModes(this);

			RenderContent.ContentFieldChanged += RenderContentOnContentFieldChanged;

			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
			RcCore.It.PluginPath = path;
			var kernel_path = Path.Combine(path, "RhinoCycles");
			RcCore.It.KernelPath = kernel_path;
			var app_path = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
			RcCore.It.AppPath = app_path;
			kernel_path = RcCore.GetRelativePath(app_path, kernel_path);
			RcCore.It.KernelPathRelative = kernel_path;

			var dataPath = RhinoApp.GetDataDirectory(true, true);
			var user_path = Path.Combine(dataPath, "RhinoCycles", "data");

			RcCore.It.DataUserPath = user_path;

			CSycles.path_init(RcCore.It.KernelPath, RcCore.It.DataUserPath);

			RcCore.It.Initialised = false;
			AsyncInitialise();

			return LoadReturnCode.Success;
		}

		private static readonly object m_initialise_lock = new System.Object();
		private void AsyncInitialise()
		{
			var t = new Thread(InitialiseCSycles);
			t.Start();
		}

		/// <summary>
		/// Initialise Cycles if necessary.
		/// </summary>
		public static void InitialiseCSycles()
		{
			lock (m_initialise_lock)
			{
				if (!RcCore.It.Initialised)
				{
					CSycles.initialise();
					RcCore.It.Initialised = true;
				}
			}
		}

		private void RenderContentOnContentFieldChanged(object sender, RenderContentFieldChangedEventArgs renderContentFieldChangedEventArgs)
		{
			//RhinoApp.WriteLine("... {0}", renderContentFieldChangedEventArgs.FieldName);
		}

		protected override void OnShutdown()
		{
			/* Clean up everything from C[CS]?ycles. */
			CSycles.shutdown();
			base.OnShutdown();
		}

	}
}
