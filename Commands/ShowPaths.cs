using System;
using Rhino;
using Rhino.Commands;
using ccl;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("DC871C42-C401-4CCA-A57C-83863A027476")]
	public class ShowPaths : Command
	{
		static ShowPaths _instance;
		public ShowPaths()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowPaths command.</summary>
		public static ShowPaths Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ShowPaths"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();

			RhinoApp.WriteLine("----------");
			RhinoApp.WriteLine("Absolute path {0}", Plugin.KernelPath);
			RhinoApp.WriteLine("Relative path {0}", Plugin.KernelPathRelative);
			RhinoApp.WriteLine("Plug-in path {0}", Plugin.PluginPath);
			RhinoApp.WriteLine("App path {0}", Plugin.AppPath);
			RhinoApp.WriteLine("User data path {0}", Plugin.DataUserPath);
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
