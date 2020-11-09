/**
Copyright 2014-2017 Robert McNeel and Associates

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

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using ccl;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.PlugIns;
using Rhino.Render;
using RhinoCycles.Settings;
using RhinoCyclesCore.Core;
using ObjectPropertiesPage = Rhino.UI.ObjectPropertiesPage;
using RhinoCyclesCore;

using System.Management;
using RhinoCyclesCore.Settings;

namespace RhinoCycles
{
	public class Plugin : PlugIn
	{
		/// <summary>
		/// Make sure we load AtStartup so that our view mode is
		/// available even when RhinoCycles isn't the current renderer
		/// </summary>
		public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

		private Dictionary<uint, ViewportPropertiesPage> m_propertiesPages = new Dictionary<uint, ViewportPropertiesPage>();

		private bool pluginLoaded = false;

		private bool SkipOpenCl() {
#if ON_RUNTIME_WIN
			SkipList skipList = new SkipList(SettingsDirectory);

			ManagementObjectSearcher objvide = new ManagementObjectSearcher("select * from Win32_VideoController");

			bool shouldskip = false;

			foreach (ManagementObject obj in objvide.Get())
			{
				string name = $"{obj["Name"]}";
				int avail = Convert.ToInt16(obj["Availability"]);
				string driverversion = $"{obj["DriverVersion"]}";

				if (avail != 3) continue;

				RhinoApp.OutputDebugString($"Name: {name}\n");
				RhinoApp.OutputDebugString($"Availability: {avail}\n");
				RhinoApp.OutputDebugString($"DriverVersion: {driverversion}\n");

				shouldskip |= skipList.Hit(name); // name.Contains("Intel") && name.Contains("530");

			}
			return shouldskip;
#else
			return false;
#endif
		}
		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			if(!pluginLoaded) {
				pluginLoaded = true;
				RhinoApp.Initialized += RhinoApp_Initialized;
				RcCore.It.InitializeResourceManager();

				// code got moved to separate DLL so use that to register from.
				var rccoreass = typeof(RcCore).Assembly;
				RenderContent.RegisterContent(rccoreass, Id);

				RenderContent.ContentFieldChanged += RenderContentOnContentFieldChanged;

				var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
				RcCore.It.PluginPath = path;
				var kernelPath = Path.Combine(path, "RhinoCycles");
				RcCore.It.KernelPath = kernelPath;
				var appPath = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
				RcCore.It.AppPath = appPath;
				kernelPath = RcCore.GetRelativePath(appPath, kernelPath);
				RcCore.It.KernelPathRelative = kernelPath;

				var dataPath = SettingsDirectory;
				var userPath = Path.Combine(dataPath, "..", "data");
				userPath = Path.GetFullPath(userPath);

				RcCore.It.DataUserPath = userPath;

				CSycles.path_init(RcCore.It.KernelPath, RcCore.It.DataUserPath);

				if(RhinoApp.RunningOnVMWare() || SkipOpenCl()) {
					CSycles.debug_set_opencl_device_type(0);
				} else {
					CSycles.debug_set_opencl_device_type(RcCore.It.EngineSettings.OpenClDeviceType);
					CSycles.debug_set_opencl_kernel(RcCore.It.EngineSettings.OpenClKernelType);
					CSycles.debug_set_opencl_single_program(RcCore.It.EngineSettings.OpenClSingleProgram);
				}
				CSycles.debug_set_cpu_kernel(RcCore.It.EngineSettings.CPUSplitKernel);

				RcCore.It.Initialised = false;
				AsyncInitialise();

				RhinoView.SetActive += RhinoView_SetActive;
			}
			return LoadReturnCode.Success;
		}

		private void RhinoApp_Initialized(object sender, EventArgs e)
		{
			RcCore.It.AppInitialised = true;
		}

		public static IViewportSettings GetActiveViewportSettings(uint doc_serial)
		{
			if (RhinoDoc.FromRuntimeSerialNumber(doc_serial) is RhinoDoc doc && doc.Views.ActiveView != null)
			{

				var vi = new ViewInfo(doc.Views.ActiveView.ActiveViewport);
				var vpi = vi.Viewport;
				var vud = vpi.UserData.Find(typeof(ViewportSettings)) as ViewportSettings;

				return vud;
			}
			return null;
		}
		private void RhinoView_SetActive(object sender, ViewEventArgs e)
		{
			if (e.View.Document == null) return;

			var sn = e.View.Document.RuntimeSerialNumber;
			if (!m_propertiesPages.ContainsKey(sn)) return;

			var m_page = m_propertiesPages[sn];

			var vi = new ViewInfo(e.View.ActiveViewport);
			var vpi = vi.Viewport;
			if (vpi.UserData.Find(typeof(ViewportSettings)) is ViewportSettings vud)
			{
				m_page.UserDataAvailable(vud);
			}
			else
			{
				m_page.NoUserDataAvailable();
			}
		}

		private static readonly object InitialiseLock = new object();
		private void AsyncInitialise()
		{
			var t = new Thread(InitialiseCSycles);
			t.Start();
		}

		/// <summary>
		/// Initialise Cycles if necessary.
		/// </summary>
		public void InitialiseCSycles()
		{
			lock(InitialiseLock)
			{
				if(!RcCore.It.Initialised)
				{
					CSycles.initialise();
					RcCore.It.Initialised = true;
					RcCore.It.TriggerInitialisationCompleted(this);
				}
			}
		}

		private void RenderContentOnContentFieldChanged(object sender, RenderContentFieldChangedEventArgs renderContentFieldChangedEventArgs)
		{
			//RhinoApp.WriteLine("... {0}", renderContentFieldChangedEventArgs.FieldName);
		}

		protected override void OnShutdown()
		{
			RhinoApp.Initialized -= RhinoApp_Initialized;
			/* Clean up everything from C[CS]?ycles. */
			CSycles.shutdown();
			base.OnShutdown();
		}

		protected override void OptionsDialogPages(List<Rhino.UI.OptionsDialogPage> pages)
		{
			var optionsPage = new Settings.OptionsDialogPage();
			pages.Add(optionsPage);
			base.OptionsDialogPages(pages);
		}

		protected override void ObjectPropertiesPages(List<ObjectPropertiesPage> pages)
		{
			if (RhinoDoc.ActiveDoc == null) return;
			var sn = RhinoDoc.ActiveDoc.RuntimeSerialNumber;

			if(!m_propertiesPages.ContainsKey(sn))
			{
				var prop_page = new ViewportPropertiesPage(sn);
				m_propertiesPages.Add(sn, prop_page);
				pages.Add(prop_page);
				base.ObjectPropertiesPages(pages);
			}
		}
	}
}
