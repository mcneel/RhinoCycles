/**
Copyright 2014-2021 Robert McNeel and Associates

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

namespace RhinoCyclesCore.Database
{
	public class ObjectShaderDatabase : IDisposable
	{
		#region lists for shaders
		/// <summary>
		/// Record meshids that use a renderhash
		/// </summary>
		private readonly Dictionary<uint, List<Tuple<Guid, int>>> _rhRenderhashMeshids = new Dictionary<uint, List<Tuple<Guid, int>>>();
		/// <summary>
		/// Record renderhash for meshid
		/// </summary>
		private readonly Dictionary<Tuple<Guid, int>, uint> _rhMeshidRenderhash = new Dictionary<Tuple<Guid, int>, uint>();
		/// <summary>
		/// Record renderhash used object id (meshinstanceid)
		/// </summary>
		private readonly Dictionary<uint, uint> _rhMeshinstanceRenderhashes = new Dictionary<uint, uint>(); 
		/// <summary>
		/// Record object ids (meshinstanceid) on renderhash
		/// </summary>
		private readonly Dictionary<uint, List<uint>> _rhRenderhashObjects = new Dictionary<uint, List<uint>>();
		#endregion

		/// <summary>
		/// Reference to the ObjectDatabase so we can query it.
		/// </summary>
		private readonly ObjectDatabase _objectDatabase;
		/// <summary>
		/// Construct a ObjectShaderDatabase that has access to objects.
		/// </summary>
		/// <param name="objects"></param>
		public ObjectShaderDatabase(ObjectDatabase objects)
		{
			_objectDatabase = objects;
		}

		public void Dispose()
		{
			_rhRenderhashMeshids.Clear();
			_rhMeshidRenderhash.Clear();
			_rhMeshinstanceRenderhashes.Clear();
			_rhRenderhashObjects.Clear();
		}


		/// <summary>
		/// Record meshid and meshinstanceid (object id) for renderhash.
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshId"></param>
		/// <param name="meshInstanceId"></param>
		public void RecordRenderHashRelation(uint hash, Tuple<Guid, int> meshId, uint meshInstanceId)
		{
			RecordRenderHashMeshId(hash, meshId);
			RecordRenderHashMeshInstanceId(hash, meshInstanceId);
		}

		/// <summary>
		/// Record relationship for renderhash -- meshinstanceid (object id)
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshInstanceId"></param>
		private void RecordRenderHashMeshInstanceId(uint hash, uint meshInstanceId)
		{
			// save meshinstanceid (object id) for render hash
			if (!_rhRenderhashObjects.ContainsKey(hash)) _rhRenderhashObjects.Add(hash, new List<uint>());
			if (!_rhRenderhashObjects[hash].Contains(meshInstanceId)) _rhRenderhashObjects[hash].Add(meshInstanceId);

			// save render hash for meshinstanceId (object id)
			_rhMeshinstanceRenderhashes[meshInstanceId] = hash;
		}

		/// <summary>
		/// record relationship for renderhash -- meshid (tuple of guid and int)
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshId"></param>
		private void RecordRenderHashMeshId(uint hash, Tuple<Guid, int> meshId)
		{
			// save meshid into list for render hash
			if (!_rhRenderhashMeshids.ContainsKey(hash)) _rhRenderhashMeshids.Add(hash, new List<Tuple<Guid, int>>());
			if (!_rhRenderhashMeshids[hash].Contains(meshId)) _rhRenderhashMeshids[hash].Add(meshId);
			// save render hash for meshid
			_rhMeshidRenderhash[meshId] = hash;
		}

		/// <summary>
		/// Remove renderhash--meshinstanceid
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshInstanceId"></param>
		private void RemoveRenderHashMeshInstanceId(uint hash, uint meshInstanceId)
		{
			//if(m_objects_on_shader.ContainsKey(oldShader)) m_objects_on_shader[oldShader].RemoveAll(x => x.Equals(oid));
			if (_rhRenderhashObjects.ContainsKey(hash)) _rhRenderhashObjects[hash].RemoveAll(x => x.Equals(meshInstanceId));
			if (_rhMeshinstanceRenderhashes.ContainsKey(meshInstanceId)) _rhMeshinstanceRenderhashes.Remove(meshInstanceId);

			var meshid = _objectDatabase.FindMeshIdOnObjectId(meshInstanceId);

			if (_rhRenderhashMeshids.ContainsKey(hash)) _rhRenderhashMeshids[hash].RemoveAll(x => x.Equals(meshid));
			if (_rhMeshidRenderhash.ContainsKey(meshid)) _rhMeshidRenderhash.Remove(meshid);
		}

		/// <summary>
		/// Update shader object relation so <c>oid</c> uses new <c>shader</c>
		/// </summary>
		/// <param name="oldShader">old shader renderhash</param>
		/// <param name="newShader">new shader renderhash</param>
		/// <param name="oid">object id (meshinstanceid)</param>
		public void ReplaceShaderRelation(uint oldShader, uint newShader, uint oid)
		{
			if(oldShader!=uint.MaxValue) RemoveRenderHashMeshInstanceId(oldShader, oid);

			var meshid = _objectDatabase.FindMeshIdOnObjectId(oid);
			RecordRenderHashRelation(newShader, meshid, oid);
		}

		/// <summary>
		/// Find Rhino material RenderHash for mesh.
		/// </summary>
		/// <param name="meshId"></param>
		/// <returns>The RenderHash for the Rhino material that is used on this mesh.</returns>
		public uint FindRenderHashForMeshId(Tuple<Guid, int> meshId)
		{
			if (_rhMeshidRenderhash.ContainsKey(meshId)) return _rhMeshidRenderhash[meshId];

			return uint.MaxValue;
		}

		/// <summary>
		/// Find renderhash used by object id (meshinstanceid)
		/// </summary>
		/// <param name="objectid"></param>
		/// <returns></returns>
		public uint FindRenderHashForObjectId(uint objectid)
		{
			if (_rhMeshinstanceRenderhashes.ContainsKey(objectid)) return _rhMeshinstanceRenderhashes[objectid];
			return uint.MaxValue;
		}

	}
}
