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

using System.Collections.Generic;
using System.Linq;

namespace RhinoCycles.Database
{
	public class CameraDatabase
	{
		/// <summary>
		/// record view changes to push to cycles
		/// </summary>
		private readonly List<CyclesView> m_cq_view_changes = new List<CyclesView>();

		/// <summary>
		/// Return true if ChangeQueue mechanism recorded viewport changes.
		/// </summary>
		/// <returns></returns>
		public bool HasChanges()
		{
			return m_cq_view_changes.Any();
		}

		/// <summary>
		/// Clear view change queue
		/// </summary>
		public void ResetViewChangeQueue()
		{
			m_cq_view_changes.Clear();
		}

		/// <summary>
		/// Record view change
		/// </summary>
		/// <param name="t">view info</param>
		public void AddViewChange(CyclesView t)
		{
			m_cq_view_changes.Add(t);
		}

		/// <summary>
		/// Get latest CyclesView recorded.
		/// </summary>
		/// <returns></returns>
		public CyclesView LatestView()
		{
			return m_cq_view_changes.Last();
		}
	}
}
