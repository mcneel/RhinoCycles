using System;
using Rhino;
using Rhino.Commands;
using ccl;
using Rhino.Input;
using Rhino.Input.Custom;

namespace RhinoCycles
{
	[System.Runtime.InteropServices.Guid("32D6D91A-779D-42D5-B76C-2974D5DBD7CA")]
	public class SelectDevice : Command
	{
		static SelectDevice _instance;
		public SelectDevice()
		{
			_instance = this;
		}

		///<summary>The only instance of the SelectDevice command.</summary>
		public static SelectDevice Instance
		{
			get { return _instance; }
		}

		public override string EnglishName
		{
			get { return "RhinoCycles_SelectDevice"; }
		}

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Plugin.InitialiseCSycles();
			var get_number = new GetInteger();
			get_number.SetLowerLimit(-1, false);
			get_number.SetUpperLimit((int)(Device.Count-1), true);
			get_number.SetDefaultInteger(Plugin.EngineSettings.SelectedDevice);
			get_number.SetCommandPrompt(String.Format("Select device to render on (-1 for default, 0-{0})", Device.Count-1));
			var get_rc = get_number.Get();
			if (get_number.CommandResult() != Result.Success) return get_number.CommandResult();
			if (get_rc == GetResult.Number)
			{
				var idx = get_number.Number();
				Device dev = null;
				if (idx > -1)
				{
					dev = Device.GetDevice(idx);
				}
				else
				{
					dev = Device.FirstCuda;
				}
				RhinoApp.WriteLine(String.Format("User selected device {0}: {1}", idx, dev));
				Plugin.EngineSettings.SelectedDevice = idx;
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
