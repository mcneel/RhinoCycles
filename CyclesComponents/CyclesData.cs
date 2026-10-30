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

using System;
using System.Collections.Generic;
using System.Linq;

using GrasshopperIO;
using Grasshopper2.Data;
using Grasshopper2.Types.Assistant;

namespace CyclesComponents
{
	/// <summary>
	/// One Cycles shader node with its settings and incoming links. Immutable; the
	/// graph upstream of a socket is reached through <see cref="Links"/>.
	/// </summary>
	[IoId("00000000-ffeb-43ee-9cd1-9c5db87ebfcf")]
	public sealed class CyclesNode : IStorable
	{
		public CyclesNode(string type, IReadOnlyDictionary<string, string> values, IReadOnlyDictionary<string, CyclesSocket> links)
		{
			Type = type ?? throw new ArgumentNullException(nameof(type));
			Values = values ?? new Dictionary<string, string>();
			Links = links ?? new Dictionary<string, CyclesSocket>();
		}

		public CyclesNode(IReader reader)
		{
			Type = reader.String(nameof(Type));

			var valueNames = reader.StringArray("ValueNames");
			var valueTexts = reader.StringArray(nameof(Values));
			var values = new Dictionary<string, string>();
			for (var i = 0; i < valueNames.Length; i++)
				values[valueNames[i]] = valueTexts[i];
			Values = values;

			var linkNames = reader.StringArray("LinkNames");
			var linkSockets = reader.StorableArray<CyclesSocket>(nameof(Links));
			var links = new Dictionary<string, CyclesSocket>();
			for (var i = 0; i < linkNames.Length; i++)
				links[linkNames[i]] = linkSockets[i];
			Links = links;
		}

		void IStorable.Store(IWriter writer)
		{
			writer.String(nameof(Type), Type);
			writer.StringArray("ValueNames", Values.Keys.ToArray());
			writer.StringArray(nameof(Values), Values.Values.ToArray());
			writer.StringArray("LinkNames", Links.Keys.ToArray());
			writer.StorableArray(nameof(Links), Links.Values);
		}

		/// <summary>
		/// Cycles node type name, e.g. "principled_bsdf".
		/// </summary>
		public string Type { get; }

		/// <summary>
		/// Input values by socket name, in Cycles XML attribute form.
		/// </summary>
		public IReadOnlyDictionary<string, string> Values { get; }

		/// <summary>
		/// Upstream sockets by the name of the input they feed.
		/// </summary>
		public IReadOnlyDictionary<string, CyclesSocket> Links { get; }

		public override string ToString()
		{
			return Type;
		}
	}

	/// <summary>
	/// An output socket of a <see cref="CyclesNode"/>; what flows along a wire.
	/// </summary>
	[IoId("00000000-4712-48b9-ab0f-6c6bcbc5384e")]
	public sealed class CyclesSocket : IStorable
	{
		public CyclesSocket(CyclesNode node, string socket)
		{
			Node = node ?? throw new ArgumentNullException(nameof(node));
			Socket = socket ?? throw new ArgumentNullException(nameof(socket));
		}

		public CyclesSocket(IReader reader)
		{
			Node = reader.Storable<CyclesNode>(nameof(Node));
			Socket = reader.String(nameof(Socket));
		}

		void IStorable.Store(IWriter writer)
		{
			writer.Storable(nameof(Node), Node);
			writer.String(nameof(Socket), Socket);
		}

		public CyclesNode Node { get; }

		/// <summary>
		/// Output socket name, e.g. "BSDF".
		/// </summary>
		public string Socket { get; }

		public override string ToString()
		{
			return $"{Node.Type}.{Socket}";
		}
	}

	public sealed class CyclesSocketAssistant : TypeAssistant<CyclesSocket>
	{
		public CyclesSocketAssistant() : base("Cycles Socket") { }

		public override CyclesSocket Copy(CyclesSocket instance)
		{
			// Immutable, so there is nothing to copy.
			return instance;
		}

		public override string DescribePrimary(Pear<CyclesSocket> pear)
		{
			return pear.Item.ToString();
		}

		public override bool Same(CyclesSocket a, CyclesSocket b)
		{
			return ReferenceEquals(a, b);
		}

		public override int Sort(CyclesSocket a, CyclesSocket b)
		{
			if (ReferenceEquals(a, b)) return 0;
			if (a is null) return -1;
			if (b is null) return +1;
			return string.CompareOrdinal(a.ToString(), b.ToString());
		}
	}
}
