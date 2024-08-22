/**
Copyright 2014-2024 Robert McNeel and Associates

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
using System.Collections.Concurrent;
using System.Linq;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;

namespace RhinoCyclesCore.Database
{
	public class ObjectDatabase : IDisposable
	{
		#region lists for meshes
		/// <summary>
		/// record changes to push to cycles (new meshes, changes to existing ones)
		/// </summary>
		private readonly ConcurrentDictionary<Tuple<Guid, int>, CyclesMesh> _cqMeshChanges = new ();
		private readonly ConcurrentDictionary<IntPtr, ccl.Transform> _cqMeshOcsFrames = new ();
		/// <summary>
		/// record mesh removes from cycles
		/// </summary>
		private readonly ConcurrentQueue<Guid> _cqMeshesToDelete = new ();
		/// <summary>
		/// record what meshid tuple corresponds to what cycles mesh
		/// </summary>
		private readonly ConcurrentDictionary<Tuple<Guid, int>, CclMesh> _rhCclMeshes = new ();

		/// <summary>
		/// Record the clipping object status of MeshIds.
		/// </summary>
		private readonly Dictionary<Tuple<Guid, int>, bool> _rhCclMeshesCutout = new Dictionary<Tuple<Guid, int>, bool>();
		/// <summary>
		/// Record what meshinstanceid (objectid) points to what meshid
		/// </summary>
		private readonly ConcurrentDictionary<uint, Tuple<Guid, int>> _rhObjectidMeshid = new ();
		#endregion
		#region lists for objects (rhino <-> cycles)
		/// <summary>
		/// record what uint corresponds to what object id in cycles
		/// Key is InstanceAncestry.Id
		/// </summary>
		private readonly ConcurrentDictionary<uint, CclObject> _rhCclObjects = new ();
		/// <summary>
		/// record objects to push to cycles
		/// </summary>
		private readonly ConcurrentQueue<CyclesObject> _cqNewUpdatedObjects = new ();
		/// <summary>
		/// record objects to remove/hide in cycles
		/// </summary>
		private readonly ConcurrentQueue<CyclesObject> _cqDeletedObjects = new ();
		/// <summary>
		/// record dynamic object transformations
		/// </summary>
		private readonly ConcurrentQueue<CyclesObjectTransform> _cqObjectTransform =  new ();
		#endregion

		public void Dispose()
		{
			ResetObjectsChangeQueue();
			ResetMeshChangeQueue();
			ResetDynamicObjectTransformChangeQueue();
			_rhCclMeshes.Clear();
			_rhObjectidMeshid.Clear();
			_rhCclObjects.Clear();
		}

		/// <summary>
		/// True if ChangeQueue recorded changes for objects or meshes.
		/// </summary>
		/// <returns>True if there were changes</returns>
		public bool HasChanges()
		{
				return
					_cqMeshChanges.Any() ||
					_cqNewUpdatedObjects.Any() ||
					_cqDeletedObjects.Any() ||
					_cqMeshesToDelete.Any() ||
					_cqObjectTransform.Any();
		}

		/// <summary>
		/// Get list of object transforms
		/// </summary>
		public ConcurrentQueue<CyclesObjectTransform> ObjectTransforms => _cqObjectTransform;

		/// <summary>
		/// Get list of deleted objects.
		/// </summary>
		public ConcurrentQueue<CyclesObject> DeletedObjects => _cqDeletedObjects;

		/// <summary>
		/// Get list of objects that are added or have been changed.
		/// </summary>
		public ConcurrentQueue<CyclesObject> NewOrUpdatedObjects => _cqNewUpdatedObjects;

		/// <summary>
		/// Get mapping of object and mesh changes.
		/// </summary>
		public ConcurrentDictionary<Tuple<Guid, int>, CyclesMesh> MeshChanges => _cqMeshChanges;

		public ConcurrentDictionary<IntPtr, ccl.Transform> MeshOcsFrames => _cqMeshOcsFrames;

		/// <summary>
		/// Get list of meshes to delete.
		/// </summary>
		public ConcurrentQueue<Guid> MeshesToDelete => _cqMeshesToDelete;

		/// <summary>
		/// Find meshid based on obid
		/// </summary>
		/// <param name="obid"></param>
		/// <returns></returns>
		public Tuple<Guid, int> FindMeshIdOnObjectId(uint obid)
		{
			if (_rhObjectidMeshid.ContainsKey(obid)) return _rhObjectidMeshid[obid];

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
			if (_rhCclObjects.ContainsKey(obid)) return _rhCclObjects[obid];

			return null;
		}
		/// <summary>
		/// Add an object change.
		/// </summary>
		/// <param name="ob"></param>
		public void AddOrUpdateObject(CyclesObject ob)
		{
			if(!_cqNewUpdatedObjects.Contains(ob)) _cqNewUpdatedObjects.Enqueue(ob);
		}

		/// <summary>
		/// Add info to delete (hide) object from cycles
		/// </summary>
		/// <param name="ob"></param>
		public void DeleteObject(CyclesObject ob)
		{
			if(!_cqDeletedObjects.Contains(ob)) _cqDeletedObjects.Enqueue(ob);
		}

		/// <summary>
		/// Record which object meshes to delete (hide)
		/// </summary>
		/// <param name="id">Object id</param>
		public void DeleteMesh(Guid id)
		{
			_cqMeshesToDelete.Enqueue(id);
		}

		/// <summary>
		/// Record CyclesMesh as new mesh data to commit to Cycles.
		/// </summary>
		/// <param name="me"></param>
		public void AddMesh(CyclesMesh me) {
			_cqMeshChanges[me.MeshId] = me;
		}

		/// <summary>
		/// Record the clipping object status of MeshId
		/// </summary>
		/// <param name="id"></param>
		/// <param name="status"></param>
		public void SetIsClippingObject(Tuple<Guid, int> id, bool status)
		{
			_rhCclMeshesCutout[id] = status;
		}

		/// <summary>
		/// Get the clipping object status for MeshId
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public bool MeshIsClippingObject(Tuple<Guid, int> id)
		{
			return _rhCclMeshesCutout.ContainsKey(id) ? _rhCclMeshesCutout[id] : false;
		}

		/// <summary>
		/// Record to what Rhino object Guid a Cycles mesh belongs.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="mid"></param>
		public void RecordObjectMeshRelation(Tuple<Guid, int> id, CclMesh mid)
		{
			_rhCclMeshes[id] = mid;
		}


		/// <summary>
		/// Find all Cycles meshes for a Rhino object Guid
		/// </summary>
		/// <param name="id"></param>
		/// <returns>List of meshes</returns>
		public CclMesh FindMeshRelation(Tuple<Guid, int> id)
		{
			if (_rhCclMeshes.ContainsKey(id)) return _rhCclMeshes[id];

			return null;
		}

		/// <summary>
		/// Record Cycles objects that belong to one Rhino object.
		/// </summary>
		/// <param name="obid">uint of Rhino Object</param>
		/// <param name="mid"></param>
		public void RecordObjectRelation(uint obid, CclObject mid)
		{
			_rhCclObjects[obid] = mid;
		}

		/// <summary>
		/// record meshid for obid
		/// </summary>
		/// <param name="obid"></param>
		/// <param name="meshid"></param>
		public void RecordObjectIdMeshIdRelation(uint obid, Tuple<Guid, int> meshid)
		{
			_rhObjectidMeshid[obid] = meshid;
		}

		/// <summary>
		/// Add a new dynamic object transformation
		/// </summary>
		/// <param name="cot"></param>
		public void AddDynamicObjectTransform(CyclesObjectTransform cot)
		{
			_cqObjectTransform.Enqueue(cot);
		}

		/// <summary>
		/// Clear out the dynamic object transforms
		/// </summary>
		public void ResetDynamicObjectTransformChangeQueue()
		{
			while(_cqObjectTransform.Count > 0) {
				_cqObjectTransform.TryDequeue(out _);
			}
		}

		/// <summary>
		/// Clear out lists and dictionary related to mesh changes that need to be committed to Cycles.
		/// </summary>
		public void ResetMeshChangeQueue()
		{
			while(_cqMeshesToDelete.Count > 0) _cqMeshesToDelete.TryDequeue(out _);

			foreach(CyclesMesh cyclesMesh in _cqMeshChanges.Values)
			{
				cyclesMesh.Clear();
			}
			_cqMeshChanges.Clear();
		}

		/// <summary>
		/// Clear out the list of object changes that need to be committed to Cycles.
		/// </summary>
		public void ResetObjectsChangeQueue()
		{
			while (_cqNewUpdatedObjects.Count > 0)
			{
				if(_cqNewUpdatedObjects.TryDequeue(out CyclesObject ob))
				{
					ob.Dispose();
				}
			}
			while (_cqDeletedObjects.Count > 0)
			{
				if(_cqDeletedObjects.TryDequeue(out CyclesObject delob))
				{
					delob.Dispose();
				}
			}
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
			foreach (var x in _rhObjectidMeshid.Where(x => x.Value.Item1.Equals(id) && !obids.Contains(x.Key)))
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

		public void RecordMeshOcsFrame(IntPtr meshId, ccl.Transform t)
		{
			_cqMeshOcsFrames[meshId] = t;
		}
	}
}
