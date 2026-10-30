/**
Copyright 2026 Robert McNeel and Associates

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

using System.Collections.Generic;

using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	/// <summary>
	/// The end of a Cycles shader graph: collects what feeds the material output and writes
	/// the graph as Cycles XML.
	/// </summary>
	[IoId("00000000-9b72-468f-9c8b-88878374dda5"), Grasshopper2.Author("Lars")]
	public sealed class CyclesMaterialOutput : Component
	{
		/* Input sockets of Cycles' output node, in parameter order. */
		private static readonly string[] g_outputSockets = { "surface", "volume", "displacement" };

		#region constructor
		public CyclesMaterialOutput()
			: base(new Nomen("Material Output", "Write a Cycles shader graph as Cycles XML.", NodeCatalog.Chapter, "Output"))
		{ }

		public CyclesMaterialOutput(IReader reader) : base(reader) { }

		protected override void AddInputs(InputAdder inputs)
		{
			inputs.AddGeneric("Surface", g_outputSockets[0], "Shader for the surface.", Access.Item, Requirement.MayBeMissing);
			inputs.AddGeneric("Volume", g_outputSockets[1], "Shader for the volume.", Access.Item, Requirement.MayBeMissing);
			inputs.AddGeneric("Displacement", g_outputSockets[2], "Displacement vector.", Access.Item, Requirement.MayBeMissing);
		}

		protected override void AddOutputs(OutputAdder outputs)
		{
			outputs.AddText("XML", "X", "The shader graph in Cycles XML.");
		}
		#endregion

		#region implementation
		protected override IIcon IconInternal => NodeCatalog.Icon("material_output");

		protected override void Process(IDataAccess access)
		{
			var connected = new List<KeyValuePair<string, CyclesSocket>>();
			for (var i = 0; i < g_outputSockets.Length; i++)
			{
				if (!access.GetItem(i, out object value) || value is null)
					continue;

				if (value is CyclesSocket socket)
					connected.Add(new KeyValuePair<string, CyclesSocket>(g_outputSockets[i], socket));
				else
					access.AddWarning("Not a Cycles socket", $"{access.NameIn(i)} takes the output of a Cycles node, not a {value.GetType().Name}.");
			}

			if (connected.Count == 0)
			{
				access.AddRemark("Nothing connected", "Connect a Cycles shader to Surface or Volume.");
				return;
			}

			access.SetItem(0, CyclesXmlWriter.Write(connected));
		}
		#endregion
	}
}
