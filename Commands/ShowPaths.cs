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

using System.Runtime.InteropServices;
using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles.Commands
{
	[Guid("DC871C42-C401-4CCA-A57C-83863A027476")]
	[CommandStyle(Style.Hidden)]
	public class ShowPaths : Command
	{
		static ShowPaths _instance;
		public ShowPaths()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowPaths command.</summary>
		public static ShowPaths Instance => _instance;

		public override string EnglishName => "RhinoCycles_ShowPaths";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();

			RhinoApp.WriteLine("----------");
			RhinoApp.WriteLine($"Absolute path {RcCore.It.KernelPath}");
			RhinoApp.WriteLine($"Relative path {RcCore.It.KernelPathRelative}");
			RhinoApp.WriteLine($"Plug-in path {RcCore.It.PluginPath}");
			RhinoApp.WriteLine($"App path {RcCore.It.AppPath}");
			RhinoApp.WriteLine($"User data path {RcCore.It.DataUserPath}");
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
