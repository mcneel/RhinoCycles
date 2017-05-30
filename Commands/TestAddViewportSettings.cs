using System;
using Rhino.Commands;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.DocObjects;
using RhinoCycles.Settings;

namespace RhinoCycles.Commands
{
	/// <summary>
	/// Although originally a test command, this is now internally used by
	/// RhinoCycles to add user data to an active viewport, if that user
	/// data doesn't already exist.
	/// </summary>
	[Guid("2ceb43dc-7623-492a-bd62-86d9f63aa9a9")]
	[CommandStyle(Style.Hidden)]
	public class TestAddViewportSettings : Command
	{
		static TestAddViewportSettings _instance;
		public TestAddViewportSettings()
		{
			if(_instance==null) _instance = this;
		}
		public override string EnglishName => "TestAddViewportSettings";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var vi = new ViewInfo(doc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;

			var vud = vpi.UserData.Find(typeof (ViewportSettings)) as ViewportSettings;

			if (vud == null)
			{
				var nvud = new ViewportSettings();
				vpi.UserData.Add(nvud);
			}

			return Result.Success;
		}
	}
}
