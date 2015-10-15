using System;
using Rhino;
using Rhino.Commands;
using ccl;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("7413124D-31F8-4B15-B92A-B6A17A884320")]
	public class ShowDeviceCapabilities : Command
	{
		static ShowDeviceCapabilities _instance;
		public ShowDeviceCapabilities()
		{
			_instance = this;
		}

		///<summary>The only instance of the ShowDeviceCapabilities command.</summary>
		public static ShowDeviceCapabilities Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ShowDeviceCapabilities"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var capabilities = Device.Capabilities;
			RhinoApp.WriteLine(String.Format("The following capabilities have been found by Cycles:\n----------\n{0}\n\n----------", capabilities));
			return Result.Success;
		}
	}
}
