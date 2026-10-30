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

using Grasshopper2.Doc;
using Grasshopper2.Framework;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	/// <summary>
	/// A palette entry for one Cycles node type, emitting a <see cref="CyclesNodeComponent"/> set up for it.
	/// </summary>
	internal sealed class CyclesNodeProxy : ObjectProxy
	{
		private readonly string _nodeType;

		private CyclesNodeProxy(string nodeType, Plugin plugin)
			: base(typeof(CyclesNodeComponent), NodeCatalog.ProxyId(nodeType), NodeCatalog.Nomen(nodeType), plugin)
		{
			_nodeType = nodeType;
		}

		public override IIcon Icon => NodeCatalog.Icon(_nodeType);

		public override IDocumentObject Emit(string initCode = null)
		{
			var previous = CyclesNodeComponent.PendingNodeType;
			CyclesNodeComponent.PendingNodeType = _nodeType;
			try
			{
				return base.Emit(initCode);
			}
			finally
			{
				CyclesNodeComponent.PendingNodeType = previous;
			}
		}

		/// <summary>
		/// Register one proxy per Cycles shader node type. Returns how many were added.
		/// </summary>
		internal static int RegisterAll(Plugin plugin)
		{
			var count = 0;
			foreach (var nodeType in NodeCatalog.VisibleTypes())
				if (ObjectProxies.RegisterProxy(new CyclesNodeProxy(nodeType.Name, plugin)))
					count++;
			return count;
		}
	}
}
