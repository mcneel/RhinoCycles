/**
Copyright 2014-2017 Robert McNeel and Associates

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
		private readonly List<CyclesLight> _cqLightChanges = new List<CyclesLight>();
		/// <summary>
		/// record what Guid corresponds to what light in cycles
		/// </summary>
		private readonly Dictionary<Guid, CclLight> _rhCclLights = new Dictionary<Guid, CclLight>();

		public readonly Guid BackgroundLightGuid = new Guid("e9bb5342-bbd7-466a-8cd1-96f471e57e65");

		public LightDatabase()
		{
			UpdateBackgroundLight();
		}

		/// <summary>
		/// Return true if any changes have been recorded by the ChangeQueue
		/// </summary>
		/// <returns>true for changes, false otherwise</returns>
		public bool HasChanges()
		{
			return bgLightPoked || _cqLightChanges.Any();
		}

		/// <summary>
		/// Clear out list of light changes.
		/// </summary>
		public void ResetLightChangeQueue()
		{
			bgLightPoked = false;
			_cqLightChanges.Clear();
		}

		/// <summary>
		/// Get existing light for given Guid.
		/// </summary>
		/// <param name="id">CyclesLight.Id</param>
		/// <returns>ccl.Light</returns>
		public CclLight ExistingLight(Guid id)
		{
			return _rhCclLights[id];
		}

		/// <summary>
		/// Record light changes
		/// </summary>
		/// <param name="light"></param>
		public void AddLight(CyclesLight light)
		{
			_cqLightChanges.Add(light);
		}

		/// <summary>
		/// Get a list of Guids from the ChangeQueue recorded light changes.
		/// </summary>
		private List<Guid> LightIds
		{
			get
			{
				var lightIds = (from light in _cqLightChanges select light.Id).ToList();
				return lightIds;
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
				var addIds = from lightkey in LightIds where !_rhCclLights.ContainsKey(lightkey) select lightkey;
				// get the CyclesLights for the Guids
				var addLights = (from aid in addIds from ll in _cqLightChanges where aid == ll.Id select ll).ToList();
				return addLights;
			}
		}

		private bool bgLightPoked { get; set; } = false;
		public void UpdateBackgroundLight()
		{
			if(!bgLightPoked)
			{
				CyclesLight bgLight = new CyclesLight()
				{
					Id = BackgroundLightGuid,
					UseMis = true,
					Strength = 1.0f,
					Type = ccl.LightType.Background,
				};
				AddLight(bgLight);
				bgLightPoked = true;
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
				var updateIds = from lightkey in LightIds where _rhCclLights.ContainsKey(lightkey) select lightkey;
				// find the CyclesLights for the Guids
				var updateLights = (from uid in updateIds from ll in _cqLightChanges where uid == ll.Id select ll).ToList();
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
			_rhCclLights[id] = cLight;
		}
	}
}
