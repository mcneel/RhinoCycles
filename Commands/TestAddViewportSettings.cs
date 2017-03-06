using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Commands;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.DocObjects;
using RhinoCycles.Settings;

namespace RhinoCycles.Commands
{
	[Guid("2CEB43DC-7623-492A-BD62-86D9F63AA9A9")]
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
			var vpi = ViewportInfo.FromRhinoViewport(doc.Views.ActiveView.ActiveViewport);

			var vud = vpi.UserData.Find(typeof (ViewportSettings)) as ViewportSettings;

			if (vud != null)
			{
				int test = 0;
				if (vud.Dictionary.TryGetInteger("test", out test))
				{
					vud.Dictionary.Set("test", ++test);
				}
				else
				{
					vud.Dictionary.Set("test", 0);
				}
			}
			else
			{
				var nvud = new ViewportSettings();
				nvud.Dictionary.Set("test", 1);
				vpi.UserData.Add(nvud);
			}

			return Result.Success;
		}
	}
}
