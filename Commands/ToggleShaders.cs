using Rhino;
using Rhino.Commands;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("AB6ED632-D539-4E22-8DF1-D72E1C451064")]
	public class ToggleShaders : Command
	{
		private static ToggleShaders g_thecommand;

		public ToggleShaders()
		{
			g_thecommand = this;
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ToggleSimpleShaders"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.EngineSettings.UseSimpleShaders = !Plugin.EngineSettings.UseSimpleShaders;
			RhinoApp.WriteLine("UseSimpleShaders set to {0}", Plugin.EngineSettings.UseSimpleShaders);
			return Result.Success;
		}
	}
}
