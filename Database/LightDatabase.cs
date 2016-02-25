/**
Copyright 2014-2016 Robert McNeel and Associates

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
using CclLight = ccl.Light;

namespace RhinoCyclesCore.Database
{
	/// <summary>
	/// Class responsible for recording changes from the ChangeQueue. Also record relation
	/// between Rhino light - CyclesLight - ccl.Light.
	/// </summary>
	public class LightDatabase
	{
		/// <summary>
		/// record light changes to push to cycles
		/// </summary>
		private readonly List<CyclesLight> m_cq_light_changes = new List<CyclesLight>();
		/// <summary>
		/// record what Guid corresponds to what light in cycles
		/// </summary>
		private readonly Dictionary<Guid, CclLight> m_rh_ccl_lights = new Dictionary<Guid, CclLight>();

		/// <summary>
		/// Return true if any changes have been recorded by the ChangeQueue
		/// </summary>
		/// <returns>true for changes, false otherwise</returns>
		public bool HasChanges()
		{
			return m_cq_light_changes.Any();
		}

		/// <summary>
		/// Clear out list of light changes.
		/// </summary>
		public void ResetLightChangeQueue()
		{
			m_cq_light_changes.Clear();
		}

		/// <summary>
		/// Get existing light for given Guid.
		/// </summary>
		/// <param name="id">CyclesLight.Id</param>
		/// <returns>ccl.Light</returns>
		public CclLight ExistingLight(Guid id)
		{
			return m_rh_ccl_lights[id];
		}

		/// <summary>
		/// Record light changes
		/// </summary>
		/// <param name="light"></param>
		public void AddLight(CyclesLight light)
		{
			m_cq_light_changes.Add(light);
		}

		/// <summary>
		/// Get a list of Guids from the ChangeQueue recorded light changes.
		/// </summary>
		private List<Guid> LightIds
		{
			get
			{
				var light_ids = (from light in m_cq_light_changes select light.Id).ToList();
				return light_ids;
			}
		}

		/// <summary>
		/// Get list of CyclesLights that need to be added
		/// </summary>
		public List<CyclesLight> LightsToAdd
		{
			get
			{
				// determine Guids of lights that need to be added
				var addIds = from lightkey in LightIds where !m_rh_ccl_lights.ContainsKey(lightkey) select lightkey;
				// get the CyclesLights for the Guids
				var addLights = (from aid in addIds from ll in m_cq_light_changes where aid == ll.Id select ll).ToList();
				return addLights;
			}
		}

		/// <summary>
		/// Get list of CyclesLights that need to be updated
		/// </summary>
		public List<CyclesLight> LightsToUpdate
		{
			get
			{
				// determine Guids of lights that need updating
				var updateIds = from lightkey in LightIds where m_rh_ccl_lights.ContainsKey(lightkey) select lightkey;
				// find the CyclesLights for the Guids
				var updateLights = (from uid in updateIds from ll in m_cq_light_changes where uid == ll.Id select ll).ToList();
				return updateLights;
			}
		}

		/// <summary>
		/// Record Cycles lights that correspond to specific Rhino light ID
		/// </summary>
		/// <param name="id">CyclesLight.Id ( equals Rhino light ID)</param>
		/// <param name="cLight">ccl.Light to save</param>
		public void RecordLightRelation(Guid id, CclLight cLight)
		{
			m_rh_ccl_lights[id] = cLight;
		}
	}
}
