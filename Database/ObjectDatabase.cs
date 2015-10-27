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

using System;
using System.Collections.Generic;
using System.Linq;
using ccl;
using CclObject = ccl.Object;
using CclMesh = ccl.Mesh;

namespace RhinoCycles.Database
{
	public class ObjectDatabase
	{
		#region lists for meshes
		/// <summary>
		/// record changes to push to cycles (new meshes, changes to existing ones)
		/// </summary>
		private readonly Dictionary<Tuple<Guid, int>, CyclesMesh> m_cq_mesh_changes = new Dictionary<Tuple<Guid, int>, CyclesMesh>();
		/// <summary>
		/// record mesh removes from cycles
		/// </summary>
		private readonly List<Guid> m_cq_meshes_to_delete = new List<Guid>();
		/// <summary>
		/// record what meshid tuple corresponds to what cycles mesh
		/// </summary>
		private readonly Dictionary<Tuple<Guid, int>, CclMesh> m_rh_ccl_meshes = new Dictionary<Tuple<Guid, int>, CclMesh>(); 
		/// <summary>
		/// Record what meshinstanceid (objectid) points to what meshid
		/// </summary>
		private readonly Dictionary<uint, Tuple<Guid, int>> m_rh_objectid_meshid = new Dictionary<uint, Tuple<Guid, int>>();
		#endregion
		#region lists for objects (rhino <-> cycles)
		/// <summary>
		/// record what uint corresponds to what object id in cycles
		/// Key is InstanceAncestry.Id
		/// </summary>
		private readonly Dictionary<uint, CclObject> m_rh_ccl_objects = new Dictionary<uint, CclObject>(); 
		/// <summary>
		/// record objects to push to cycles
		/// </summary>
		private readonly List<CyclesObject> m_cq_new_updated_objects = new List<CyclesObject>();
		/// <summary>
		/// record objects to remove/hide in cycles
		/// </summary>
		private readonly List<CyclesObject> m_cq_deleted_objects = new List<CyclesObject>(); 
		/// <summary>
		/// record dynamic object transformations
		/// </summary>
		private readonly List<CyclesObjectTransform> m_cq_object_transform =  new List<CyclesObjectTransform>();
		#endregion

		public bool HasChanges()
		{
				return
					m_cq_mesh_changes.Any() ||
					m_cq_new_updated_objects.Any() ||
					m_cq_deleted_objects.Any() ||
					m_cq_meshes_to_delete.Any() ||
					m_cq_object_transform.Any();
		}

		public List<CyclesObjectTransform> ObjectTransforms
		{
			get
			{
				return m_cq_object_transform;
			}
		}

		public Dictionary<uint, Tuple<Guid, int>> ObjectMeshDictionary
		{
			get
			{
				return m_rh_objectid_meshid;
			}
		}

		public List<CyclesObject> DeletedObjects
		{
			get
			{
				return m_cq_deleted_objects;
			}
		}

		public List<CyclesObject> NewOrUpdatedObjects
		{
			get
			{
				return m_cq_new_updated_objects;
			}
		}

		public Dictionary<Tuple<Guid, int>, CyclesMesh> MeshChanges
		{
			get
			{
				return m_cq_mesh_changes;
			}
		}

		public List<Guid> MeshesToDelete
		{
			get
			{
				return m_cq_meshes_to_delete;
			}
		} 

		/// <summary>
		/// Find meshid based on obid
		/// </summary>
		/// <param name="obid"></param>
		/// <returns></returns>
		public Tuple<Guid, int> FindMeshIdOnObjectId(uint obid)
		{
			if (m_rh_objectid_meshid.ContainsKey(obid)) return m_rh_objectid_meshid[obid];

			return null;
		}

		/// <summary>
		/// Find CclObjects based on obid.
		/// This will find all submeshes as well.
		/// </summary>
		/// <param name="obid"></param>
		/// <returns></returns>
		public CclObject FindObjectRelation(uint obid)
		{
			if (m_rh_ccl_objects.ContainsKey(obid)) return m_rh_ccl_objects[obid];

			return null;
		} 
		/// <summary>
		/// Add an object change.
		/// </summary>
		/// <param name="ob"></param>
		public void AddNewOrUpdateObject(CyclesObject ob)
		{
			if(!m_cq_new_updated_objects.Contains(ob)) m_cq_new_updated_objects.Add(ob);
		}

		/// <summary>
		/// Add info to delete (hide) object from cycles
		/// </summary>
		/// <param name="ob"></param>
		public void AddObjectDelete(CyclesObject ob)
		{
			if(!m_cq_deleted_objects.Contains(ob)) m_cq_deleted_objects.Add(ob);
		}

		/// <summary>
		/// Record which object meshes to delete (hide)
		/// </summary>
		/// <param name="id">Object id</param>
		public void DeleteMesh(Guid id)
		{
			m_cq_meshes_to_delete.Add(id);
		}

		/// <summary>
		/// Record CyclesMesh as new mesh data to commit to Cycles.
		/// </summary>
		/// <param name="me"></param>
		public void AddMesh(CyclesMesh me) {
			m_cq_mesh_changes[me.MeshId] = me;
		}

		/// <summary>
		/// Record to what Rhino object Guid a Cycles mesh belongs.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="mid"></param>
		public void RecordObjectMeshRelation(Tuple<Guid, int> id, CclMesh mid)
		{
			m_rh_ccl_meshes[id] = mid;
		}


		/// <summary>
		/// Find all Cycles meshes for a Rhino object Guid
		/// </summary>
		/// <param name="id"></param>
		/// <returns>List of meshes</returns>
		public CclMesh FindMeshRelation(Tuple<Guid, int> id)
		{
			if (m_rh_ccl_meshes.ContainsKey(id)) return m_rh_ccl_meshes[id];

			return null;
		}

		/// <summary>
		/// Record Cycles objects that belong to one Rhino object.
		/// </summary>
		/// <param name="obid">uint of Rhino Object</param>
		/// <param name="mid"></param>
		public void RecordObjectRelation(uint obid, CclObject mid)
		{
			m_rh_ccl_objects[obid] = mid;
		}

		/// <summary>
		/// record meshid for obid
		/// </summary>
		/// <param name="obid"></param>
		/// <param name="meshid"></param>
		public void RecordObjectIdMeshIdRelation(uint obid, Tuple<Guid, int> meshid)
		{
			m_rh_objectid_meshid[obid] = meshid;
		}

		/// <summary>
		/// Add a new dynamic object transformation
		/// </summary>
		/// <param name="cot"></param>
		public void AddDynamicObjectTransform(CyclesObjectTransform cot)
		{
			m_cq_object_transform.RemoveAll(x => x.Equals(cot));
			m_cq_object_transform.Add(cot);
		}

		/// <summary>
		/// Clear out the dynamic object transforms
		/// </summary>
		public void ClearDynamicObjectTransforms()
		{
			m_cq_object_transform.Clear();
		}

		/// <summary>
		/// Clear out lists and dictionary related to mesh changes that need to be committed to Cycles.
		/// </summary>
		public void ClearMeshes()
		{
			m_cq_meshes_to_delete.Clear();
			m_cq_mesh_changes.Clear();
		}

		/// <summary>
		/// Clear out the list of object changes that need to be committed to Cycles.
		/// </summary>
		public void ClearObjectsChanges()
		{
			m_cq_new_updated_objects.Clear();
			m_cq_deleted_objects.Clear();
		}

		/// <summary>
		/// Find all cycles objects for meshes that have meshid containing Guid id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public List<CclObject> GetCyclesObjectsForGuid(Guid id)
		{
			var cclobs = new List<CclObject>();
			var obids = new List<uint>();
			foreach (var x in ObjectMeshDictionary.Where(x => x.Value.Item1.Equals(id) && !obids.Contains(x.Key)))
			{
				obids.Add(x.Key);
			}

			foreach(var obid in obids)
			{
				var cclob = FindObjectRelation(obid);
				if(!cclobs.Contains(cclob)) cclobs.Add(cclob);
			}

			return cclobs;
		}
	}
}
