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
using ccl;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCyclesCore.Core;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace RhinoCycles.Commands
{
	[Guid("ededf48b-dd86-4329-a08d-dc762b8c4adf")]
	public class SetAdvancedOptions : Command
	{
		private static SetAdvancedOptions _gThecommand;

		public SetAdvancedOptions()
		{
			if(_gThecommand==null) _gThecommand = this;
		}

		public override string EnglishName => "RhinoCycles_SetAdvancedOptions";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getNumber = new GetNumber();
			getNumber.SetLowerLimit(2.0, false);
			getNumber.SetUpperLimit(uint.MaxValue, false);
			getNumber.SetDefaultInteger(RcCore.It.EngineSettings.Samples);
			getNumber.SetCommandPrompt("Set render samples");

			var props = RcCore.It.EngineSettings.GetType().GetProperties();

			Dictionary<string, Tuple<PropertyInfo, object>> opts = new Dictionary<string, Tuple<PropertyInfo, object>>(32);

			foreach(var prop in props)
			{
				if (prop.Name.Contains("Default")) continue;
				if (prop.Name.Contains("Device")) continue;
				if (prop.Name.Contains("Hash")) continue;
				if (prop.Name.Contains("Clip")) continue;
				if (prop.Name.Equals("Samples")) continue;

				if(prop.PropertyType == typeof(bool))
				{
					var curbool = (bool)(prop.GetValue(RcCore.It.EngineSettings, null));
					var boolopt = new OptionToggle(curbool, $"No{prop.Name}", $"{prop.Name}");
					getNumber.AddOptionToggle(prop.Name, ref boolopt);
					opts[prop.Name] = new Tuple<PropertyInfo, object>(prop, boolopt);
				}
				if(prop.PropertyType == typeof(int))
				{
					var curint = (int)(prop.GetValue(RcCore.It.EngineSettings, null));
					var intopt = new OptionInteger(curint);
					getNumber.AddOptionInteger(prop.Name, ref intopt);
					opts[prop.Name] = new Tuple<PropertyInfo, object>(prop, intopt);
				}
				if(prop.PropertyType == typeof(float))
				{
					var curfloat = (float)(prop.GetValue(RcCore.It.EngineSettings, null));
					var floatopt = new OptionDouble(curfloat);
					getNumber.AddOptionDouble(prop.Name, ref floatopt);
					opts[prop.Name] = new Tuple<PropertyInfo, object>(prop, floatopt);
				}

			}

			while (true)
			{
				var getRc = getNumber.Get();
				if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
				switch (getRc)
				{
					case GetResult.Number:
						RcCore.It.EngineSettings.Samples = (int)getNumber.Number();
						foreach(var opt in opts)
						{
							var v = opt.Value;
							var k = opt.Key;
							var pinf = v.Item1;
#if DEBUG
							string msg ="";
#endif
							if(pinf.PropertyType == typeof(bool))
							{
								var blopt = v.Item2 as OptionToggle;
#if DEBUG
								msg = $"\t{k} # {v.Item1.Name} -> {blopt.CurrentValue}";
#endif
								pinf.SetValue(RcCore.It.EngineSettings, blopt.CurrentValue);
							}
							if(pinf.PropertyType == typeof(int))
							{
								var intopt = v.Item2 as OptionInteger;
								var oldint = pinf.GetValue(RcCore.It.EngineSettings, null);
								pinf.SetValue(RcCore.It.EngineSettings, intopt.CurrentValue);
								var newint = pinf.GetValue(RcCore.It.EngineSettings, null);
#if DEBUG
								msg = $"\t{k} # {v.Item1.Name} = {intopt.CurrentValue}. {oldint} -> {newint}";
#endif
							}
							if(pinf.PropertyType == typeof(float))
							{
								var flopt = v.Item2 as OptionDouble;
#if DEBUG
								msg = $"\t{k} # {v.Item1.Name} -> {flopt.CurrentValue}";
#endif
								pinf.SetValue(RcCore.It.EngineSettings, (float)flopt.CurrentValue);
							}

#if DEBUG
							RhinoApp.WriteLine(msg);
#endif
						}
						break;
					case GetResult.Option:
						continue;
				}

				break;
			}

			return Result.Success;
		}
	}
}

