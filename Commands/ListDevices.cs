using System;
using Rhino;
using Rhino.Commands;
using ccl;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("9e91d7ea-7990-471f-a944-ad9ececcc88b")]
	public class ListDevices : Command
	{
		static ListDevices _instance;
		public ListDevices()
		{
			_instance = this;
		}

		///<summary>The only instance of the ListDevices command.</summary>
		public static ListDevices Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_ListDevices"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();

			var num_devices = Device.Count;
			RhinoApp.WriteLine(String.Format("We have {0} device{1}", num_devices, num_devices != 1 ? "s" : ""));
			RhinoApp.WriteLine("----------");
			foreach (var dev in Device.Devices)
			{
				RhinoApp.WriteLine(String.Format("	Device {0}: {1} > {2} > {3} | {4} | {5} | {6} | {7}", dev.Id,
					dev.Name, dev.Description, dev.Num,
					dev.DisplayDevice, dev.AdvancedShading,
					dev.PackImages, dev.Type));
			}
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}
	}
}
