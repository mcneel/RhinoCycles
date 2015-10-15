using System.Diagnostics;
using System.Reflection;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("CB9C1C0D-83F6-4157-BD57-F2AD6093FF73")]
	public class ShowInfo : Command
	{
		static ShowInfo _instance;
		public ShowInfo()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowInfo command.</summary>
		public static ShowInfo Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ShowInfo"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();

			var rhcycles_ass = Assembly.GetExecutingAssembly();
			var csycles_ass = Assembly.GetAssembly(typeof(ccl.Client));
			var csycles_fvi = FileVersionInfo.GetVersionInfo(csycles_ass.Location);

			RhinoApp.WriteLine("----------");
			RhinoApp.WriteLine("RhinoCycles {0} @ {1}", RhinoBuildConstants.VERSION_STRING, rhcycles_ass.Location);
			RhinoApp.WriteLine("CCSycles {0} @ {1}", csycles_fvi.FileVersion, csycles_ass.Location);
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
