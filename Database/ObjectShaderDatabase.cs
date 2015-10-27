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
using System.Text;
using System.Threading.Tasks;
using CclObject = ccl.Object;

namespace RhinoCycles.Database
{
	public class ObjectShaderDatabase
	{
		#region lists for shaders
		/// <summary>
		/// Record meshids that use a renderhash
		/// </summary>
		private readonly Dictionary<uint, List<Tuple<Guid, int>>> m_rh_renderhash_meshids = new Dictionary<uint, List<Tuple<Guid, int>>>();
		/// <summary>
		/// Record renderhash for meshid
		/// </summary>
		private readonly Dictionary<Tuple<Guid, int>, uint> m_rh_meshid_renderhash = new Dictionary<Tuple<Guid, int>, uint>();
		/// <summary>
		/// Record renderhash used object id (meshinstanceid)
		/// </summary>
		private readonly Dictionary<uint, uint> m_rh_meshinstance_renderhashes = new Dictionary<uint, uint>(); 
		/// <summary>
		/// Record object ids (meshinstanceid) on renderhash
		/// </summary>
		private readonly Dictionary<uint, List<uint>> m_rh_renderhash_objects = new Dictionary<uint, List<uint>>();
		#endregion

		private IReadShaderDatabase shaders;
		private ObjectDatabase objects;

		/// <summary>
		/// Construct a ObjectShaderDatabase that has access to shaders and objects.
		/// </summary>
		/// <param name="_shaders"></param>
		/// <param name="_objects"></param>
		public ObjectShaderDatabase(IReadWriteShaderDatabase _shaders, ObjectDatabase _objects)
		{
			shaders = _shaders;
			objects = _objects;
		}

		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		public void UploadObjectShaderChanges()
		{
			foreach (var obshad in shaders.ObjectShaderChanges)
			{

				var cob = objects.FindObjectRelation(obshad.Id);

				//if(mesh!=null) mesh.ReplaceShader(new_shader);

				if(cob!=null)
				{
					// get shaders
					var new_shader = shaders.GetShaderFromHash(obshad.NewShaderHash);
					var old_shader = shaders.GetShaderFromHash(obshad.OldShaderHash);
					if (new_shader != null)
					{
						if (cob.Mesh != null) cob.Mesh.ReplaceShader(new_shader);
						new_shader.Tag();
					}
					if (old_shader != null)
					{
						old_shader.Tag();
					}
					cob.TagUpdate();
				}
				ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
			}
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
			if (!m_rh_renderhash_objects.ContainsKey(hash)) m_rh_renderhash_objects.Add(hash, new List<uint>());
			if (!m_rh_renderhash_objects[hash].Contains(meshInstanceId)) m_rh_renderhash_objects[hash].Add(meshInstanceId);

			// save render hash for meshinstanceId (object id)
			m_rh_meshinstance_renderhashes[meshInstanceId] = hash;
		}

		/// <summary>
		/// record relationship for renderhash -- meshid (tuple of guid and int)
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshId"></param>
		private void RecordRenderHashMeshId(uint hash, Tuple<Guid, int> meshId)
		{
			// save meshid into list for render hash
			if (!m_rh_renderhash_meshids.ContainsKey(hash)) m_rh_renderhash_meshids.Add(hash, new List<Tuple<Guid, int>>());
			if (!m_rh_renderhash_meshids[hash].Contains(meshId)) m_rh_renderhash_meshids[hash].Add(meshId);
			// save render hash for meshid
			m_rh_meshid_renderhash[meshId] = hash;
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

			var meshid = objects.FindMeshIdOnObjectId(oid);
			RecordRenderHashRelation(newShader, meshid, oid);
		}

		/// <summary>
		/// Remove renderhash--meshinstanceid
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshInstanceId"></param>
		private void RemoveRenderHashMeshInstanceId(uint hash, uint meshInstanceId)
		{
			//if(m_objects_on_shader.ContainsKey(oldShader)) m_objects_on_shader[oldShader].RemoveAll(x => x.Equals(oid));
			if (m_rh_renderhash_objects.ContainsKey(hash)) m_rh_renderhash_objects[hash].RemoveAll(x => x.Equals(meshInstanceId));
			if (m_rh_meshinstance_renderhashes.ContainsKey(meshInstanceId)) m_rh_meshinstance_renderhashes.Remove(meshInstanceId);

			var meshid = objects.FindMeshIdOnObjectId(meshInstanceId);

			if (m_rh_renderhash_meshids.ContainsKey(hash)) m_rh_renderhash_meshids[hash].RemoveAll(x => x.Equals(meshid));
			if (m_rh_meshid_renderhash.ContainsKey(meshid)) m_rh_meshid_renderhash.Remove(meshid);
		}

		/// <summary>
		/// Find RenderHash for mesh
		/// </summary>
		/// <param name="meshId"></param>
		/// <returns></returns>
		public uint FindRenderHashForMeshId(Tuple<Guid, int> meshId)
		{
			if (m_rh_meshid_renderhash.ContainsKey(meshId)) return m_rh_meshid_renderhash[meshId];

			return uint.MaxValue;
		}

		/// <summary>
		/// Find renderhash used by object id (meshinstanceid)
		/// </summary>
		/// <param name="objectid"></param>
		/// <returns></returns>
		public uint FindRenderHashForObjectId(uint objectid)
		{
			if (m_rh_meshinstance_renderhashes.ContainsKey(objectid)) return m_rh_meshinstance_renderhashes[objectid];
			return uint.MaxValue;
		}

		/// <summary>
		/// Find meshids that use RenderHash
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		public List<Tuple<Guid, int>> FindMeshIdsForRenderHash(uint hash)
		{
			if (m_rh_renderhash_meshids.ContainsKey(hash)) return m_rh_renderhash_meshids[hash];
			return new List<Tuple<Guid, int>>();
		}

	}
}
