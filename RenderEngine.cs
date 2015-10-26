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
using System.Drawing;
using System.Linq;
using System.Threading;
using ccl;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using sdd = System.Diagnostics.Debug;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;

namespace RhinoCycles
{

	public enum State
	{
		Waiting,
		Uploading,
		Rendering,
		Stopped
	}
	/// <summary>
	/// The actual render engine, ready for asynchronous work in Rhino.
	/// </summary>
	public partial class RenderEngine : AsyncRenderContext
	{
		private readonly object m_flushlock = new object();

		private readonly CreatePreviewEventArgs m_preview_event_args;

		private Guid m_plugin_id = Guid.Empty;

		/// <summary>
		/// Reference to the client representation of this render engine instance.
		/// </summary>
		public Client Client { get; set; }

		/// <summary>
		/// Current render engine state.
		/// </summary>
		public State State { get; set; }

		/// <summary>
		/// Reference to the session of this render engine instance.
		/// </summary>
		public Session Session = null;

		/// <summary>
		/// Reference to the thread in which this render engine session lives.
		/// </summary>
		public Thread RenderThread { get; set; }

		/// <summary>
		/// Reference to the RenderWindow into which we're rendering.
		/// 
		/// Can be null, for instance in the case of material preview rendering
		/// </summary>
		public RenderWindow RenderWindow { get; set; }

		/// <summary>
		/// Reference to the bitmap we're rendering into.
		/// 
		/// This is used when rendering material previews.
		/// </summary>
		public Bitmap RenderBitmap { get; set; }

		/// <summary>
		/// Set to true when the render session should be cancelled - used for preview job cancellation
		/// </summary>
		public bool CancelRender { get; set; }

		public int RenderedSamples;

		public string TimeString;

		private readonly CSycles.UpdateCallback m_update_callback;
		private readonly CSycles.RenderTileCallback m_update_render_tile_callback;
		private readonly CSycles.RenderTileCallback m_write_render_tile_callback;
		private readonly CSycles.TestCancelCallback m_test_cancel_callback;

		/// <summary>
		/// Record Guid of our groundplane object.
		/// </summary>
		private readonly Tuple<Guid, int> m_groundplane_guid = new Tuple<Guid, int>(new Guid("306690EC-6E86-4676-B55B-1A50066D7432"), 0);

		private const uint GROUNDPLANE_MESHINSTANCEID = 1;

		/// <summary>
		/// Get the ground plane object ID
		/// </summary>
		public Tuple<Guid, int> GroundPlaneId
		{
			get
			{
				return m_groundplane_guid;
			}
		}

		/// <summary>
		/// Get the mesh instance id for ground plane
		/// </summary>
		public uint GroundPlaneMeshInstanceId
		{
			get
			{
				return GROUNDPLANE_MESHINSTANCEID;
			}
		}

		/// <summary>
		/// True if ground plane has been initialised
		/// </summary>
		public bool GroundPlaneInitialised { get; set; }

		private readonly Guid m_sun_guid = new Guid("82FE2C29-9632-473D-982B-9121E150E1D2");

		/// <summary>
		/// Get the Sun ID
		/// </summary>
		public Guid SunId
		{
			get
			{
				return m_sun_guid;
				
			}
		}

		private bool m_flush;
		/// <summary>
		/// Flag set to true when a flush on the changequeue is needed.
		///
		/// Setting of Flush is protected with a lock. Getting is not.
		/// </summary>
		public bool Flush
		{
			get
			{
				return m_flush;
			}
			set
			{
				lock (m_flushlock)
				{
					m_flush = value;
				}
			}
		}

		/// <summary>
		/// Our instance of the change queue. This is our access point for all
		/// data. The ChangeQueue mechanism will push data to it, record it
		/// with all necessary book keeping to track the data relations between
		/// Rhino and Cycles.
		/// </summary>
		public ChangeDatabase Database { get; set; }


		/// <summary>
		/// record view changes to push to cycles
		/// </summary>
		private readonly List<CyclesView> m_cq_view_changes = new List<CyclesView>();

		/// <summary>
		/// record light changes to push to cycles
		/// </summary>
		private readonly List<CyclesLight> m_cq_light_changes = new List<CyclesLight>();
		/// <summary>
		/// record what Guid corresponds to what light in cycles
		/// </summary>
		private readonly Dictionary<Guid, CclLight> m_rh_ccl_lights = new Dictionary<Guid, CclLight>();

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

		#region lists for shaders
		/// <summary>
		/// record material changes for objects
		/// </summary>
		private readonly List<CyclesObjectShader> m_cq_objects_shader_changes = new List<CyclesObjectShader>(); 
		/// <summary>
		/// record shader changes to push to cycles
		/// </summary>
		private readonly List<CyclesShader> m_cq_shaders = new List<CyclesShader>();
		private readonly List<Tuple<object, Shader>> m_all_shaders = new List<Tuple<object, Shader>>();
		/// <summary>
		/// record RenderMaterial CRC and Shader relationship. Key is RenderHash, Value is Shader.
		/// </summary>
		private readonly Dictionary<uint, Shader> m_rh_ccl_shaders = new Dictionary<uint, Shader>(); 
		/// <summary>
		/// record shader in scene relationship. Key is RenderMaterial.RenderHash, Value is shader id in scene.
		/// </summary>
		private readonly Dictionary<uint, uint> m_rh_ccl_scene_shader_ids = new Dictionary<uint, uint>(); 
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
		/// <summary>
		/// a approx avg measurement device.
		/// </summary>
		private readonly Measurement m_measurements = new Measurement();

		/// <summary>
		/// Return true if any change has been received through the changequeue
		/// </summary>
		/// <returns>true if any changes have been received.</returns>
		private bool HasSceneChanges()
		{
			return Database.HasChanges() ||
				m_cq_view_changes.Count > 0 || m_cq_shaders.Count > 0 ||
				m_cq_light_changes.Count > 0 || m_cq_mesh_changes.Count > 0 ||
				m_cq_new_updated_objects.Count > 0 || m_cq_deleted_objects.Count > 0 ||
				m_cq_meshes_to_delete.Count > 0 ||
				m_cq_object_transform.Count > 0 ||
				m_cq_objects_shader_changes.Count > 0;
		}

		/// <summary>
		/// Check if we should change render engine status. If the changequeue
		/// has notified us of any changes Flush will be true. If we're rendering
		/// then move to State.Halted and cancel our current render progress.
		/// </summary>
		private void CheckFlushQueue()
		{
			// not rendering, nor flush needed, bail
			if (State != State.Rendering || Database == null || !Flush) return;

			// We've been told we need to flush, so cancel current render
			//State = State.Halted;
			// acquire lock while flushing queue and uploading any data
			lock (m_flushlock)
			{
				// flush the queue
				Database.Flush();

				// reset flush flag directly, since we already have lock.
				m_flush = false;

				// if we've got actually changes we care about
				// lets upload that
				if (HasSceneChanges())
				{
					State = State.Uploading;
					if (Session != null) Session.Cancel("Scene changes detected.\n");
				}
				else
				{
					State = State.Rendering;
				}
			}
		}

		public void AddObjectMaterialChange(CyclesObjectShader o)
		{
			if(!m_cq_objects_shader_changes.Contains(o)) m_cq_objects_shader_changes.Add(o);
		}
		/// <summary>
		/// Clear queue of object shader changes
		/// </summary>
		public void ClearObjectShaderChanges()
		{
			m_cq_objects_shader_changes.Clear();
		}

		/// <summary>
		/// Clear queue of shader changes.
		/// </summary>
		public void ClearShaders()
		{
			m_cq_shaders.Clear();
		}

		/// <summary>
		/// Check if a shader for a certain RenderHash already exists.
		/// </summary>
		/// <param name="shaderId"></param>
		/// <returns></returns>
		public bool HasShader(uint shaderId)
		{
			return m_rh_ccl_shaders.ContainsKey(shaderId);
		}

		/// <summary>
		/// Get Shader for hash, or null if not found
		/// </summary>
		/// <param name="shaderId">Render hash</param>
		/// <returns>Shader if found, null otherwise</returns>
		public Shader GetShaderFromHash(uint shaderId)
		{
			return HasShader(shaderId) ? m_rh_ccl_shaders[shaderId] : null;
		}

		/// <summary>
		/// Get RenderHash for a <c>Shader</c>
		/// </summary>
		/// <param name="shader">Shader to search for</param>
		/// <returns>RenderHash for <c>shader</c></returns>
		public uint GetHashFromShader(Shader shader)
		{
			var hash = uint.MaxValue;
			foreach (var hash_shader in m_rh_ccl_shaders)
			{
				if (hash_shader.Value.Id == shader.Id) hash = hash_shader.Key;
			}

			return hash;
		}

		/// <summary>
		/// Add a CyclesShader to the list of shaders that will have to be committed to Cycles.
		/// </summary>
		/// <param name="shader"></param>
		public void AddShader(CyclesShader shader)
		{
			if (!m_rh_ccl_shaders.ContainsKey(shader.Id) && !m_cq_shaders.Contains(shader))
			{
				m_cq_shaders.Add(shader);
				//m_all_shaders.Add(shader);
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
		/// Remove renderhash--meshinstanceid
		/// </summary>
		/// <param name="hash"></param>
		/// <param name="meshInstanceId"></param>
		private void RemoveRenderHashMeshInstanceId(uint hash, uint meshInstanceId)
		{
			//if(m_objects_on_shader.ContainsKey(oldShader)) m_objects_on_shader[oldShader].RemoveAll(x => x.Equals(oid));
			if (m_rh_renderhash_objects.ContainsKey(hash)) m_rh_renderhash_objects[hash].RemoveAll(x => x.Equals(meshInstanceId));
			if (m_rh_meshinstance_renderhashes.ContainsKey(meshInstanceId)) m_rh_meshinstance_renderhashes.Remove(meshInstanceId);

			var meshid = m_rh_objectid_meshid[meshInstanceId];

			if (m_rh_renderhash_meshids.ContainsKey(hash)) m_rh_renderhash_meshids[hash].RemoveAll(x => x.Equals(meshid));
			if (m_rh_meshid_renderhash.ContainsKey(meshid)) m_rh_meshid_renderhash.Remove(meshid);
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

		/// <summary>
		/// Find all cycles objects for meshes that have meshid containing Guid id
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public List<CclObject> GetCyclesObjectsForGuid(Guid id)
		{
			var cclobs = new List<CclObject>();
			var obids = new List<uint>();
			foreach (var x in m_rh_objectid_meshid.Where(x => x.Value.Item1.Equals(id) && !obids.Contains(x.Key)))
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

		/// <summary>
		/// Update shader object relation so <c>oid</c> uses new <c>shader</c>
		/// </summary>
		/// <param name="oldShader">old shader renderhash</param>
		/// <param name="newShader">new shader renderhash</param>
		/// <param name="oid">object id (meshinstanceid)</param>
		public void ReplaceShaderRelation(uint oldShader, uint newShader, uint oid)
		{
			if(oldShader!=uint.MaxValue) RemoveRenderHashMeshInstanceId(oldShader, oid);

			var meshid = m_rh_objectid_meshid[oid];
			RecordRenderHashRelation(newShader, meshid, oid);
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
		/// Clear out list of light changes.
		/// </summary>
		public void ClearLights()
		{
			m_cq_light_changes.Clear();
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
		/// Record Cycles lights that correspond to specific Rhino light ID
		/// </summary>
		/// <param name="id"></param>
		/// <param name="cLight"></param>
		public void RecordLightRelation(Guid id, CclLight cLight)
		{
			m_rh_ccl_lights[id] = cLight;
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
		/// Record light changes
		/// </summary>
		/// <param name="light"></param>
		public void AddLight(CyclesLight light)
		{
			m_cq_light_changes.Add(light);
		}

		/// <summary>
		/// Clear view change queue
		/// </summary>
		public void ClearViewChanges()
		{
			m_cq_view_changes.Clear();
		}

		/// <summary>
		/// Record view change
		/// </summary>
		/// <param name="t">view info</param>
		public void ChangeView(CyclesView t)
		{
			m_cq_view_changes.Add(t);
		}

		/// <summary>
		/// Construct a new render engine
		/// </summary>
		/// <param name="doc"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public RenderEngine(RhinoDoc doc, Guid pluginId) : this(doc, pluginId, doc.Views.ActiveView)
		{
		}

		private uint m_doc_serialnumber;
		private RhinoView m_view;

		public RhinoDoc Doc
		{
			get { return RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber); }
		}

		public ViewportInfo ViewportInfo
		{
			get { return new ViewportInfo(m_view.ActiveViewport); }
		}

		public RenderEngine(RhinoDoc doc, Guid pluginId, RhinoView view)
		{
			m_plugin_id = pluginId;
			m_preview_event_args = null;
			m_doc_serialnumber = doc.RuntimeSerialNumber;
			m_view = view;
			if (doc != null)
			{
				Database = new ChangeDatabase(pluginId, this, m_doc_serialnumber, view);
			}
			RenderThread = null;
			ClearMeshes();
			ClearShaders();
			ClearViewChanges();
			Client = new Client();
			State = State.Rendering;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = UpdateRenderTileCallback;
			m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = null;

			CSycles.log_to_stdout(false);
#endregion
			
		}

		/// <summary>
		/// Construct a render engine for preview rendering
		/// </summary>
		/// <param name="createPreviewEventArgs"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public RenderEngine(CreatePreviewEventArgs createPreviewEventArgs, Guid pluginId)
		{
			m_preview_event_args = createPreviewEventArgs;
			Database = new ChangeDatabase(pluginId, this, createPreviewEventArgs);
			RenderThread = null;
			ClearMeshes();
			ClearShaders();
			ClearViewChanges();
			Client = new Client();
			State = State.Rendering;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = UpdateRenderTileCallback;
			m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = TestCancel;

			CSycles.log_to_stdout(false);
#endregion
		}

		/// <summary>
		/// Tell our changequeue instance to initialise world.
		/// </summary>
		public void CreateWorld()
		{
			Database.CreateWorld();
		}

		/// <summary>
		/// True if rendering for preview
		/// </summary>
		/// <returns></returns>
		public bool IsPreview()
		{
			return Database.IsPreview;
		}

		/// <summary>
		/// Flush
		/// </summary>
		public void FlushIt()
		{
			Database.Flush();
		}

		public void TestCancel(uint sid)
		{
			if (State == State.Stopped) return;

			if (m_preview_event_args != null)
			{
				if (m_preview_event_args.Cancel)
				{
					CancelRender = true;
					Session.Cancel("Preview Cancelled");
				}
			}
		}

		public class StatusTextEventArgs
		{
			public StatusTextEventArgs(string s, float progress, int samples)
			{
				StatusText = s;
				Progress = progress;
				Samples = samples;
			}

			public string StatusText { get; private set; }
			public float Progress { get; private set; }
			public int Samples { get; private set; }
		}

		public delegate void StatusTextHandler(object sender, StatusTextEventArgs e);
		public event StatusTextHandler StatusTextEvent;

		/// <summary>
		/// Handle status updates
		/// </summary>
		/// <param name="sid"></param>
		public void UpdateCallback(uint sid)
		{
			if (State == State.Stopped) return;

			var status = CSycles.progress_get_status(Client.Id, sid);
			var substatus = CSycles.progress_get_substatus(Client.Id, sid);
			RenderedSamples = CSycles.progress_get_sample(Client.Id, sid);
			int tile;
			float progress;
			double total_time, render_time, tile_time;
			CSycles.progress_get_tile(Client.Id, sid, out tile, out total_time, out render_time, out tile_time);
			CSycles.progress_get_progress(Client.Id, sid, out progress, out total_time, out render_time, out tile_time);
			int hr = ((int)total_time) / (60 * 60);
			int min = (((int)total_time) / 60) % 60;
			int sec = ((int)total_time) % 60;
			int hun = ((int)(total_time * 100.0)) % 100;

			if (!substatus.Equals(string.Empty)) status = status + ": " + substatus;

			TimeString = String.Format("{0}h {1}m {2}.{3}s", hr, min, sec, hun);

			status = String.Format("{0} {1}", status, TimeString);

			// don't set full 100% progress here yet, because that signals the renderwindow the end of async render
			if (progress >= 0.9999f) progress = 0.9999f;
			if (Settings.Samples == ushort.MaxValue) progress = -1.0f;
			if (null != RenderWindow) RenderWindow.SetProgress(status, progress);

			if (StatusTextEvent != null)
			{
				StatusTextEvent(this, new StatusTextEventArgs(status, progress, RenderedSamples));
			}

			CheckFlushQueue();
		}

		/// <summary>
		///  Clamp color so we get valid values for system bitmap
		/// </summary>
		/// <param name="ch"></param>
		/// <returns></returns>
		public static int ColorClamp(int ch)
		{
			if (ch < 0) return 0;
			return ch > 255 ? 255 : ch;
		}

		/// <summary>
		/// Update the RenderWindow or RenderBitmap with the updated tile from
		/// Cycles render progress.
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="tx"></param>
		/// <param name="ty"></param>
		/// <param name="tw"></param>
		/// <param name="th"></param>
		public void DisplayBuffer(uint sessionId, uint tx, uint ty, uint tw, uint th)
		{
			if (State == State.Stopped) return;
			var start = DateTime.Now;
			var rg = RenderBitmap;
			if (RenderWindow != null)
			{
				using (var channel = RenderWindow.OpenChannel(RenderWindow.StandardChannels.RGBA))
				{
					if (channel != null)
					{
						var pixelbuffer = new PixelBuffer(CSycles.session_get_buffer(Client.Id, sessionId));
						var size = Client.Scene.Camera.Size;
						var rect = new Rectangle((int) tx, (int) ty, (int) tw, (int) th);
						channel.SetValues(rect, size, pixelbuffer);
						RenderWindow.InvalidateArea(rect);
					}
				}
			}
			else if (rg != null)
			{
				uint buffer_size;
				uint buffer_stride;
				var width = RenderDimension.Width;
				CSycles.session_get_buffer_info(Client.Id, sessionId, out buffer_size, out buffer_stride);
				var pixels = CSycles.session_copy_buffer(Client.Id, sessionId, buffer_size);
				for (var x = (int)tx; x < (int)(tx + tw); x++)
				{
					for (var y = (int)ty; y < (int)(ty + th); y++)
					{
						var i = y * width * 4 + x * 4;
						var r = pixels[i];
						var g = pixels[i + 1];
						var b = pixels[i + 2];
						var a = pixels[i + 3];

						if (float.IsNaN(r)) r = 0.0f;
						if (float.IsNaN(g)) g = 0.0f;
						if (float.IsNaN(b)) b = 0.0f;
						if (float.IsNaN(a)) a = 0.0f;
						r = Math.Min(Math.Abs(r), 1.0f);
						g = Math.Min(Math.Abs(g), 1.0f);
						b = Math.Min(Math.Abs(b), 1.0f);
						a = Math.Min(Math.Abs(a), 1.0f);

						var c4_f = new Color4f(r, g, b, a);
						rg.SetPixel(x, y, c4_f.AsSystemColor());
					}
				}
			}
			var diff = (DateTime.Now - start).TotalMilliseconds;
			m_measurements.Add(diff);
		}

		/// <summary>
		/// Callback for debug logging facility. Will be called only for Debug builds of ccycles.dll
		/// </summary>
		/// <param name="msg"></param>
		public static void LoggerCallback(string msg)
		{
#if DEBUG
			sdd.WriteLine(String.Format("DBG: {0}", msg));
#endif
		}

		/// <summary>
		/// Handle write render tile callback
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="w"></param>
		/// <param name="h"></param>
		/// <param name="depth"></param>
		public void WriteRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint depth)
		{
			if (State == State.Stopped) return;
			DisplayBuffer(sessionId, x, y, w, h);
		}

		/// <summary>
		/// Handle update render tile callback
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="w"></param>
		/// <param name="h"></param>
		/// <param name="depth"></param>
		public void UpdateRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint depth)
		{
			if (State == State.Stopped) return;
			DisplayBuffer(sessionId, x, y, w, h);
		}

		/// <summary>
		/// Called when user presses the stop render button.
		/// </summary>
		override public void StopRendering()
		{
			if (RenderThread == null) return;

			StopTheRenderer();

			// done, let everybody know it
			if(Settings.Verbose) sdd.WriteLine("Rendering stopped. The render window can be closed safely.");
		}

		private void StopTheRenderer()
		{
			// signal that we should stop rendering.
			CancelRender = true;

			// get rid of our change queue
			Database.Dispose();
			Database = null;

			// set state to stopped
			State = State.Stopped;

			// signal our cycles session to stop rendering.
			if (Session != null) Session.Cancel("Render stop called.\n");

			// let's get back into the thread.
			if (RenderThread != null)
			{
				RenderThread.Join();
				RenderThread = null;
			}
		}

		/// <summary>
		/// Set progress to RenderWindow, if it is not null.
		/// </summary>
		/// <param name="rw"></param>
		/// <param name="msg"></param>
		/// <param name="progress"></param>
		protected void SetProgress(RenderWindow rw, string msg, float progress)
		{
			if (null != rw) rw.SetProgress(msg, progress);
		}

		/// <summary>
		/// Register the callbacks to the render engine session
		/// </summary>
		private void SetCallbacks()
		{
			#region register callbacks with Cycles session

			Session.UpdateCallback = m_update_callback;
			Session.UpdateTileCallback = m_update_render_tile_callback;
			Session.WriteTileCallback = m_write_render_tile_callback;
			Session.TestCancelCallback = m_test_cancel_callback;

			#endregion
		}
	}

}
