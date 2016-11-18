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
using System.Drawing;
using System.Linq;
using ccl;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using sdd = System.Diagnostics.Debug;
using CqMaterial = Rhino.Render.ChangeQueue.Material;
using CqMesh = Rhino.Render.ChangeQueue.Mesh;
using CqGroundPlane = Rhino.Render.ChangeQueue.GroundPlane;
using CqLight = Rhino.Render.ChangeQueue.Light;
using CqSkylight = Rhino.Render.ChangeQueue.Skylight;
using CQLinearWorkflow = Rhino.Render.ChangeQueue.LinearWorkflow;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;
using RGLight = Rhino.Geometry.Light;
using Rhino.Geometry;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Shaders;

namespace RhinoCyclesCore.Database
{
	public class ChangeDatabase : ChangeQueue
	{
		/// <summary>
		/// Reference to the Cycles render engine C# level implementation.
		/// </summary>
		private readonly RenderEngine _renderEngine;

		/// <summary>
		/// Note that this ViewInfo is valid only during the Apply* function calls
		/// for the ongoing Flush. At the end this should be set to null.
		/// </summary>
		private ViewInfo _currentViewInfo;

		#region DATABASES

		/// <summary>
		/// Database responsible for keeping track of objects/meshes and their shaders.
		/// </summary>
		private readonly ObjectShaderDatabase _objectShaderDatabase;

		/// <summary>
		/// Database responsible for all material shaders
		/// </summary>
		private readonly ShaderDatabase _shaderDatabase = new ShaderDatabase();

		/// <summary>
		/// Database responsible for keeping track of objects and meshes and their relations between
		/// Rhino and Cycles.
		/// </summary>
		private readonly ObjectDatabase _objectDatabase = new ObjectDatabase();

		/// <summary>
		/// The database responsible for keeping track of light changes and the relations between Rhino
		/// and Cycles lights and their shaders.
		/// </summary>
		private readonly LightDatabase _lightDatabase = new LightDatabase();

		/// <summary>
		/// Database responsible for keeping track of background and environment changes and their
		/// relations between Rhino and Cycles.
		/// </summary>
		private readonly EnvironmentDatabase _environmentDatabase = new EnvironmentDatabase();

		/// <summary>
		/// Database responsible for managing camera transforms from Rhino to Cycles.
		/// </summary>
		private readonly CameraDatabase _cameraDatabase = new CameraDatabase();

		/// <summary>
		/// Database responsible for managing render settings.
		/// </summary>
		private readonly RenderSettingsDatabase _renderSettingsDatabase = new RenderSettingsDatabase();

		#endregion

		private readonly ShaderConverter _shaderConverter;

		private readonly bool _modalRenderer;

		/// <summary>
		/// This should be called with true to read texture as byte image instead
		/// of float images. This is necessary currently for OpenCL environment
		/// textures, as HDRi isn't properly supported there.
		/// </summary>
		/// <param name="floatAsByte"></param>
		internal void SetFloatTextureAsByteTexture(bool floatAsByte)
		{
			_environmentDatabase.SetFloatTextureAsByteTexture(floatAsByte);
		}

		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="doc">Document runtime serial number</param>
		/// <param name="view">Reference to the RhinoView for which this queue is created.</param>
		/// <param name="modal">Set to true if rendering modal</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, uint doc, ViewInfo view, bool modal) : base(pluginId, doc, view, !modal)
		{
			_renderEngine = engine;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
			_shaderConverter = new ShaderConverter(engine.Settings);
			_modalRenderer = modal;
		}


		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="createPreviewEventArgs">preview event arguments</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, CreatePreviewEventArgs createPreviewEventArgs) : base(pluginId, createPreviewEventArgs)
		{
			_renderEngine = engine;
			_modalRenderer = true;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
			_shaderConverter = new ShaderConverter(engine.Settings);
		}

		protected override void Dispose(bool isDisposing)
		{
			_environmentDatabase?.Dispose();
			_objectShaderDatabase?.Dispose();
			_objectDatabase?.Dispose();
			base.Dispose(isDisposing);
		}

		public void UploadLinearWorkflowChanges()
		{
			if (LinearWorkflowHasChanged)
			{
				Gamma = LinearWorkflow.Gamma;
				TriggerLinearWorkflowUploaded();
			}
		}

		/// <summary>
		/// Notify interested clients of i.e. sample count changes.
		/// </summary>
		public void UploadRenderSettingsChanges()
		{
			if (_renderSettingsDatabase.HasChanged)
				_renderEngine.TriggerSamplesChanged(_renderEngine.Settings.Samples);
		}

		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		public void UploadObjectShaderChanges()
		{
			foreach (var obshad in _shaderDatabase.ObjectShaderChanges)
			{

				var cob = _objectDatabase.FindObjectRelation(obshad.Id);
				if(cob!=null)
				{
					// get shaders
					var newShader = _shaderDatabase.GetShaderFromHash(obshad.NewShaderHash);
					var oldShader = _shaderDatabase.GetShaderFromHash(obshad.OldShaderHash);
					if (newShader != null)
					{
						cob.Mesh?.ReplaceShader(newShader);
						newShader.Tag();
					}
					oldShader?.Tag();
					cob.TagUpdate();
					_objectShaderDatabase.ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
				}
			}
		}

		public event EventHandler<LinearWorkflowChangedEventArgs> LinearWorkflowChanged;
		public event EventHandler<MaterialShaderUpdatedEventArgs> MaterialShaderChanged;
		public event EventHandler<LightShaderUpdatedEventArgs> LightShaderChanged;
		public event EventHandler FilmUpdateTagged;

		public void UploadGammaChanges()
		{
			if (GammaHasChanged || LinearWorkflowHasChanged)
			{
				TriggerLinearWorkflowUploaded();
				BitmapConverter.ApplyGammaToTextures(GammaLinearWorkflow);

				_environmentDatabase.CurrentBackgroundShader?.Reset();

				foreach (var tup in _shaderDatabase.AllShaders)
				{
					var matsh = tup.Item1 as CyclesShader;
					if (matsh != null)
					{
						matsh.Gamma = GammaLinearWorkflow;
						TriggerMaterialShaderChanged(matsh, tup.Item2);
					}

					var lgsh = tup.Item1 as CyclesLight;
					if (lgsh != null)
					{
						lgsh.Gamma = GammaLinearWorkflow;
						TriggerLightShaderChanged(lgsh, tup.Item2);
					}

				}

				TriggerFilmUpdateTagged();
			}
		}

		internal void TriggerFilmUpdateTagged()
		{
			FilmUpdateTagged?.Invoke(this, EventArgs.Empty);
		}

		internal void TriggerMaterialShaderChanged(CyclesShader rcShader, Shader cclShader)
		{
			MaterialShaderChanged?.Invoke(this, new MaterialShaderUpdatedEventArgs(rcShader, cclShader));
		}

		internal void TriggerLightShaderChanged(CyclesLight rcLightShader, Shader cclShader)
		{
			LightShaderChanged?.Invoke(this, new LightShaderUpdatedEventArgs(rcLightShader, cclShader));
		}

		internal void TriggerLinearWorkflowUploaded()
		{
			LinearWorkflowChanged?.Invoke(this, new LinearWorkflowChangedEventArgs(new LinearWorkflow(LinearWorkflow), Gamma));
		}

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		public void UploadDynamicObjectTransforms()
		{
			foreach (var cot in _objectDatabase.ObjectTransforms)
			{
				var cob = _objectDatabase.FindObjectRelation(cot.Id);
				if (cob == null) continue;

				cob.Transform = cot.Transform;
				cob.Mesh.TagRebuild();
				cob.TagUpdate();
			}
		}

		/// <summary>
		/// Upload mesh changes
		/// </summary>
		public void UploadMeshChanges()
		{
			// handle mesh deletes first
			foreach (var meshDelete in _objectDatabase.MeshesToDelete)
			{
				var cobs = _objectDatabase.GetCyclesObjectsForGuid(meshDelete);

				foreach (var cob in cobs)
				{
					// remove mesh data
					cob.Mesh.ClearData();
					cob.Mesh.TagRebuild();
					// hide object containing the mesh
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			var curmesh = 0;
			var totalmeshes = _objectDatabase.MeshChanges.Count;
			foreach (var meshChange in _objectDatabase.MeshChanges)
			{
				var cyclesMesh = meshChange.Value;
				var mid = meshChange.Key;

				var me = _objectDatabase.FindMeshRelation(mid);

				// newme true if we have to upload new mesh data
				var newme = me == null;

				if (_renderEngine.CancelRender) return;

				// lets find the shader for this, or use 0 if none found.
				uint shid;
				var matid = _objectShaderDatabase.FindRenderHashForMeshId(cyclesMesh.MeshId);
				try
				{
					// @todo check this is correct naming and dictionary to query from
					shid = _shaderDatabase.GetShaderIdForMatId(matid);
				}
				catch (Exception)
				{
					shid = 0;
				}

				var shader = _renderEngine.Client.Scene.ShaderFromSceneId(shid);

				// creat a new mesh to upload mesh data to
				if (newme)
				{
					me = new CclMesh(_renderEngine.Client, shader);
				}

				me.Resize((uint)cyclesMesh.verts.Length/3, (uint)cyclesMesh.faces.Length/3);

				// update status bar of render window.
				var stat =
					$"Upload mesh {curmesh}/{totalmeshes} [v: {cyclesMesh.verts.Length}, t: {cyclesMesh.faces.Length} using shader {shid}]";

				// set progress, but without rendering percentage (hence the -1.0f)
				_renderEngine.SetProgress(_renderEngine.RenderWindow, stat, -1.0f);

				// upload, if we get false back we were signalled to stop rendering by user
				if (!UploadMeshData(me, cyclesMesh)) return;

				// if we re-uploaded mesh data, we need to make sure the shader
				// information doesn't get lost.
				if (!newme) me.ReplaceShader(shader);

				// don't forget to record this new mesh
				if(newme) _objectDatabase.RecordObjectMeshRelation(cyclesMesh.MeshId, me);
				//RecordShaderRelation(shader, cycles_mesh.MeshId);

				curmesh++;
			}
		}

		/// <summary>
		/// Upload mesh data, return false if cancel render is signalled.
		/// </summary>
		/// <param name="me">mesh to upload to</param>
		/// <param name="cyclesMesh">data to upload from</param>
		/// <returns>true if uploaded without cancellation, false otherwise</returns>
		private bool UploadMeshData(CclMesh me, CyclesMesh cyclesMesh)
		{
			// set raw vertex data
			me.SetVerts(ref cyclesMesh.verts);
			if (_renderEngine.CancelRender) return false;
			// set the triangles
			me.SetVertTris(ref cyclesMesh.faces, cyclesMesh.vertex_normals != null);
			if (_renderEngine.CancelRender) return false;
			// set vertex normals
			if (cyclesMesh.vertex_normals != null)
			{
				me.SetVertNormals(ref cyclesMesh.vertex_normals);
			}
			if (_renderEngine.CancelRender) return false;
			// set uvs
			if (cyclesMesh.uvs != null)
			{
				me.SetUvs(ref cyclesMesh.uvs);
			}
			// and finally tag for rebuilding
			me.TagRebuild();
			return true;
		}

		/// <summary>
		/// Reset changequeue lists and dictionaries. Generally this is done once all changes
		/// have been handled, and thus no longer needed.
		/// </summary>
		public void ResetChangeQueue()
		{
			_dynamic = false;
			_currentViewInfo = null;
			ClearGamma();
			ClearLinearWorkflow();
			_environmentDatabase.ResetBackgroundChangeQueue();
			_cameraDatabase.ResetViewChangeQueue();
			_lightDatabase.ResetLightChangeQueue();
			_shaderDatabase.ClearShaders();
			_shaderDatabase.ClearObjectShaderChanges();
			_objectDatabase.ResetObjectsChangeQueue();
			_objectDatabase.ResetMeshChangeQueue();
			_objectDatabase.ResetDynamicObjectTransformChangeQueue();
		}

		/// <summary>
		/// Tell if any changes have been recorded by the ChangeQueue mechanism since
		/// the last flush.
		/// </summary>
		/// <returns>True if changes where recorded, false otherwise.</returns>
		public bool HasChanges()
		{
			return
				_cameraDatabase.HasChanges() ||
				_environmentDatabase.BackgroundHasChanged ||
				_lightDatabase.HasChanges() || 
				_shaderDatabase.HasChanges() ||
				_objectDatabase.HasChanges() ||
				LinearWorkflowHasChanged ||
				GammaHasChanged;
		}

		private float _gamma = 1.0f;

		public bool GammaHasChanged { get; private set; }

		public float Gamma
		{
			set
			{
				_gamma = value;
				GammaHasChanged = true;
			}
			get
			{
				return _gamma;
			}
		}

		public float GammaLinearWorkflow
		{
			get
			{
				if (LinearWorkflow != null)
				{
					return LinearWorkflow.Active ? Gamma : 1.0f;
				}

				return 1.0f;
			}
		}


		private void ClearGamma()
		{
			GammaHasChanged = false;
		}

		protected override void ApplyGammaChanges(double dGamma)
		{
			Gamma= (float) dGamma;
		}

		private LinearWorkflow _linearWorkflow = new LinearWorkflow(false, 1.0f);

		public bool LinearWorkflowHasChanged { get; private set; }

		public LinearWorkflow LinearWorkflow
		{
			set
			{
				_linearWorkflow = value;
				LinearWorkflowHasChanged = true;

				Gamma = _linearWorkflow.Gamma;
			}
			get
			{
				return _linearWorkflow;
			}
		}

		private void ClearLinearWorkflow()
		{
			LinearWorkflowHasChanged = false;
		}

		protected override void ApplyLinearWorkflowChanges(CQLinearWorkflow lw)
		{
			LinearWorkflow = new LinearWorkflow(lw);
			sdd.WriteLine($"LinearWorkflow {lw.Active} {lw.Gamma} {lw.GammaReciprocal}");
		}

		/// <summary>
		/// Upload camera (viewport) changes to Cycles.
		/// </summary>
		public void UploadCameraChanges()
		{
			if (!_cameraDatabase.HasChanges()) return;

			var view = _cameraDatabase.LatestView();
			if(view!=null)
			{
				UploadCamera(view);
			}
			var fb = _cameraDatabase.GetBlur();
			UploadFocalBlur(fb);
		}

		/// <summary>
		/// Event arguments for ViewChanged event.
		/// </summary>
		public class ViewChangedEventArgs: EventArgs
		{
			/// <summary>
			/// Construct ViewChangedEventArgs
			/// </summary>
			/// <param name="view">The new CRC for the view</param>
			/// <param name="sizeChanged">true if the render size has changed</param>
			/// <param name="newSize">The render size</param>
			public ViewChangedEventArgs(ViewInfo view, bool sizeChanged, Size newSize)
			{
				View = view;
				SizeChanged = sizeChanged;
				NewSize = newSize;
			}

			/// <summary>
			/// View CRC
			/// </summary>
			public ViewInfo View { get; private set; }
			/// <summary>
			/// True if the render size has changed
			/// </summary>
			public bool SizeChanged { get; private set; }
			/// <summary>
			/// The new rendering dimension
			/// </summary>
			public Size NewSize { get; private set; }
		}

		/// <summary>
		/// Event that gets fired when the Rhino viewport has changed. This
		/// event gives the new CRC for the view, true if the render
		/// size has changed and the new render size
		/// </summary>
		public event EventHandler<ViewChangedEventArgs> ViewChanged;

		private void TriggerViewChanged(ViewInfo view, bool sizeChanged, Size newSize)
		{
			ViewChanged?.Invoke(this, new ViewChangedEventArgs(view, sizeChanged, newSize));
		}

		private void UploadFocalBlur(FocalBlur fb)
		{
			var scene = _renderEngine.Session.Scene;
			scene.Camera.FocalDistance = fb.FocalDistance;
			scene.Camera.ApertureSize = fb.FocalAperture;

		}

		/// <summary>
		/// Set the camera based on CyclesView
		/// </summary>
		/// <param name="view"></param>
		private void UploadCamera(CyclesView view)
		{
			var scene = _renderEngine.Session.Scene;
			var oldSize = _renderEngine.RenderDimension;
			var newSize = new Size(view.Width, view.Height);
			_renderEngine.RenderDimension = newSize;

			TriggerViewChanged(view.View, oldSize!=newSize, newSize);

			var ha = newSize.Width > newSize.Height ? view.Horizontal: view.Vertical;

			var angle = (float) Math.Atan(Math.Tan(ha)/view.ViewAspectRatio) * 2.0f;

			//System.Diagnostics.Debug.WriteLine("size: {0}, matrix: {1}, angle: {2}, Sensorsize: {3}x{4}", size, view.Transform, angle, Settings.SensorHeight, Settings.SensorWidth);

			scene.Camera.Size = newSize;
			scene.Camera.Matrix = view.Transform;
			scene.Camera.Type = view.Projection;
			scene.Camera.Fov = angle;
			scene.Camera.FarClip = 1.0E+14f; // gp_side_extension;
			if (view.Projection == CameraType.Orthographic || view.TwoPoint) scene.Camera.SetViewPlane(view.Viewplane.Left, view.Viewplane.Right, view.Viewplane.Top, view.Viewplane.Bottom);
			else if(view.Projection == CameraType.Perspective) scene.Camera.ComputeAutoViewPlane();

			scene.Camera.SensorHeight = _renderEngine.Settings.SensorHeight;
			scene.Camera.SensorWidth = _renderEngine.Settings.SensorWidth;
			scene.Camera.Update();
		}

		public Size RenderDimension { get; set; }

		/// <summary>
		/// Handle view changes.
		/// </summary>
		/// <param name="viewInfo"></param>
		protected override void ApplyViewChange(ViewInfo viewInfo)
		{
			if (!IsPreview && !viewInfo.Viewport.Id.Equals(ViewId)) return;

			_currentViewInfo = viewInfo;

			//System.Diagnostics.Debug.WriteLine(String.Format("ChangeDatabase ApplyViewChange on view {0}", viewInfo.Name));

			_environmentDatabase.BackgroundWallpaper(viewInfo);

			var vp = viewInfo.Viewport;

			// camera transform, camera to world conversion
			var rhinocam = vp.GetXform(CoordinateSystem.Camera, CoordinateSystem.World);
			// lens length
			var lenslength = vp.Camera35mmLensLength;

			// lets see if we need to do magic for two-point perspective
			var twopoint = false; // @todo add support for vp.IsTwoPointPerspectiveProjection;

			// frustum values, used for two point
			double frt, frb, frr, frl, frf, frn;
			vp.GetFrustum(out frl, out frr, out frb, out frt, out frn, out frf);

			//sdd.WriteLine(String.Format(
			//	"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));

			// distance between top and bottom of frustum
			var dist = frt - frb;
			var disthalf = dist/2.0f;

			// if we have a disthalf and twopoint, adjust frustum top and bottom
			if (twopoint && Math.Abs(dist) >= 0.001)
			{
				frt = disthalf;
				frb = -disthalf;
				//System.Diagnostics.Debug.WriteLine(String.Format(
				//	"ADJUSTED Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));
			}

			var parallel = vp.IsParallelProjection;
			var viewscale = vp.ViewScale;

			/*sdd.WriteLine(String.Format(
				"Camera projection type {0}, lens length {1}, scale {2}x{3}, two-point {4}, dist {5}, disthalf {6}", parallel ? "ORTHOGRAPHIC" : "PERSPECTIVE",
				lenslength, viewscale.Width, viewscale.Height, twopoint, dist, disthalf));

			sdd.WriteLine(String.Format(
				"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));
				*/

			int near, far;
			var screenport = vp.GetScreenPort(out near, out far);
			var bottom = screenport.Bottom;
			var top = screenport.Top;
			var left = screenport.Left;
			var right = screenport.Right;

			int w = 0;
			int h = 0;

			// We shouldn't be taking render dimensions from the viewport when
			// rendering into render window, since this can be completely
			// different (for instance Rendering panel, custom render size)
			// see http://mcneel.myjetbrains.com/youtrack/issue/RH-32533
			if (!_modalRenderer)
			{
				w = Math.Abs(right - left);
				h = Math.Abs(bottom - top);
			}
			else
			{
				w = RenderDimension.Width;
				h = RenderDimension.Height;
			}
			var portrait = w < h;
			var viewAspectratio = portrait ? h/(float)w : w/(float)h;

			// get camera angles
			double diagonal, vertical, horizontal;
			vp.GetCameraAngles(out diagonal, out vertical, out horizontal);

			// convert rhino transform to ccsycles transform
			var t = CclXformFromRhinoXform(rhinocam);
			// then convert to Cycles orientation
			t = t * ccl.Transform.RhinoToCyclesCam;

			// ready, lets push our data
			var cyclesview = new CyclesView
			{
				LensLength = lenslength,
				Transform = t,
				Diagonal =  diagonal,
				Vertical = vertical,
				Horizontal = horizontal,
				ViewAspectRatio = viewAspectratio,
				Projection = parallel ? CameraType.Orthographic : CameraType.Perspective,
				Viewplane = new ViewPlane((float)frl, (float)frr, (float)frt, (float)frb),
				TwoPoint = twopoint,
				Width = w,
				Height = h,
				View = GetQueueView() // use GetQueueView to ensure we have a valid ViewInfo even after Flush
			};
			_cameraDatabase.AddViewChange(cyclesview);
		}

		/// <summary>
		/// Handle mesh changes
		/// </summary>
		/// <param name="deleted"></param>
		/// <param name="added"></param>
		protected override void ApplyMeshChanges(Guid[] deleted, List<CqMesh> added)
		{
			//System.Diagnostics.Debug.WriteLine("ChangeDatabase ApplyMeshChanges, deleted {0}, added {1}", deleted.Length, added.Count);

			foreach (var guid in deleted)
			{
				// only delete those that aren't listed in the added list
				if (!(from mesh in added where mesh.Id() == guid select mesh).Any())
				{
					//System.Diagnostics.Debug.WriteLine("Deleting {0}", guid);
					_objectDatabase.DeleteMesh(guid);
				}
			}

			foreach (var cqm in added)
			{
				var meshes = cqm.GetMeshes();
				var meshguid = cqm.Id();

				var meshIndex = 0;

				foreach(var meshdata in meshes)
				{
					HandleMeshData(meshguid, meshIndex, meshdata);
					meshIndex++;
				}
			}
		}

		public void HandleMeshData(Guid meshguid, int meshIndex, Rhino.Geometry.Mesh meshdata)
		{
			// Get face indices flattened to an
			// integer array.
			var findices = meshdata.Faces.ToIntArray(true);

			// Get texture coordinates and
			// flattens to a float array.
			var tc = meshdata.TextureCoordinates;
			var rhuv = tc.ToFloatArray();

			// Get rhino vertex normals and
			// flatten to a float array.
			var vn = meshdata.Normals;
			var rhvn = vn.ToFloatArray();

			// now convert UVs: from vertex indexed array to per face per vertex
			var cmuv = rhuv.Length > 0 ? new float[findices.Length * 2] : null;
			if (cmuv != null)
			{
				for (var fi = 0; fi < findices.Length; fi++)
				{
					var fioffs = fi * 2;
					var findex = findices[fi];
					var findex2 = findex * 2;
					var rhuvit = rhuv[findex2];
					var rhuvit1 = rhuv[findex2 + 1];
					cmuv[fioffs] = rhuvit;
					cmuv[fioffs + 1] = rhuvit1;
				}
			}

			var meshid = new Tuple<Guid, int>(meshguid, meshIndex);

			var crc = _objectShaderDatabase.FindRenderHashForMeshId(meshid);
			if (crc == uint.MaxValue) crc = 0;

			// now we have everything we need
			// so we can create a CyclesMesh that the
			// RenderEngine can eventually commit to Cycles
			var cyclesMesh = new CyclesMesh
			{
				MeshId = meshid,
				verts = meshdata.Vertices.ToFloatArray(),
				faces = findices,
				uvs = cmuv,
				vertex_normals = rhvn,
				matid = crc
			};
			_objectDatabase.AddMesh(cyclesMesh);
		}

		/// <summary>
		/// Convert a Rhino.Geometry.Transform to ccl.Transform
		/// </summary>
		/// <param name="rt">Rhino.Geometry.Transform</param>
		/// <returns>ccl.Transform</returns>
		static ccl.Transform CclXformFromRhinoXform(Rhino.Geometry.Transform rt)
		{
			var t = new ccl.Transform(
				(float) rt.M00, (float) rt.M01, (float) rt.M02, (float) rt.M03,
				(float) rt.M10, (float) rt.M11, (float) rt.M12, (float) rt.M13,
				(float) rt.M20, (float) rt.M21, (float) rt.M22, (float) rt.M23,
				(float) rt.M30, (float) rt.M31, (float) rt.M32, (float) rt.M33
				);

			return t;
		}

		protected override void ApplyMeshInstanceChanges(List<uint> deleted, List<MeshInstance> addedOrChanged)
		{
			// helper list to ensure we don't add same material multiple times.
			var addedmats = new List<uint>();

			foreach (var d in deleted)
			{
				var cob = _objectDatabase.FindObjectRelation(d);
				var delob = new CyclesObject {cob = cob};
				_objectDatabase.DeleteObject(delob);
				//System.Diagnostics.Debug.WriteLine("Deleted MI {0}", d);
			}
			foreach (var a in addedOrChanged)
			{
				var matid = a.MaterialId;
				var mat = a.RenderMaterial;

				if (!addedmats.Contains(matid))
				{
					HandleRenderMaterial(mat);
					addedmats.Add(matid);
				}

				var meshid = new Tuple<Guid, int>(a.MeshId, a.MeshIndex);
				//System.Diagnostics.Debug.WriteLine("Added MI {0}", a.InstanceId);
				var ob = new CyclesObject {obid = a.InstanceId, meshid = meshid, Transform = CclXformFromRhinoXform(a.Transform), matid = a.MaterialId};

				var shaderchange = new CyclesObjectShader(a.InstanceId)
				{
					OldShaderHash = uint.MaxValue,
					NewShaderHash = a.MaterialId
				};

				_shaderDatabase.AddObjectMaterialChange(shaderchange);

				_objectShaderDatabase.RecordRenderHashRelation(a.MaterialId, meshid, a.InstanceId);
				_objectDatabase.RecordObjectIdMeshIdRelation(a.InstanceId, meshid);
				_objectDatabase.AddOrUpdateObject(ob);
			}
		}

		#region SHADERS

		/// <summary>
		/// Handle RenderMaterial - will queue new shader if necessary
		/// </summary>
		/// <param name="mat"></param>
		private void HandleRenderMaterial(RenderMaterial mat)
		{
			if (_shaderDatabase.HasShader(mat.RenderHash)) return;

			//System.Diagnostics.Debug.WriteLine("Add new material with RenderHash {0}", mat.RenderHash);
			var sh = _shaderConverter.CreateCyclesShader(mat.TopLevelParent as RenderMaterial, GammaLinearWorkflow);
			_shaderDatabase.AddShader(sh);
		}

		/// <summary>
		/// Change the material on given object
		/// </summary>
		/// <param name="matid">RenderHash of material</param>
		/// <param name="obid">MeshInstanceId</param>
		private void HandleMaterialChangeOnObject(uint matid, uint obid)
		{
			var oldhash = _objectShaderDatabase.FindRenderHashForObjectId(obid);
			// skip if no change in renderhash
			if (oldhash != matid)
			{
				var o = new CyclesObjectShader(obid)
				{
					NewShaderHash = matid,
					OldShaderHash = oldhash
				};

				//System.Diagnostics.Debug.WriteLine("CQMat.Id: {0} meshinstanceid: {1}", mat.Id, obid);

				_shaderDatabase.AddObjectMaterialChange(o);
			}
		}


		/// <summary>
		/// Handle changes in materials to create (or re-use) shaders.
		/// </summary>
		/// <param name="mats">List of <c>CQMaterial</c></param>
		protected override void ApplyMaterialChanges(List<CqMaterial> mats)
		{
			// list of material hashes
			var distinctMats = new List<uint>();

			foreach (var mat in mats)
			{
				var rm = MaterialFromId(mat.Id);

				if (!distinctMats.Contains(mat.Id))
				{
					distinctMats.Add(mat.Id);
				}

				var obid = mat.MeshInstanceId;

				HandleMaterialChangeOnObject(rm.RenderHash, obid);
			}

			// list over material hashes, check if they exist. Create if new
			foreach (var distinct in distinctMats)
			{
				var existing = _shaderDatabase.GetShaderFromHash(distinct);
				if (existing == null)
				{
					var rm = MaterialFromId(distinct);
					HandleRenderMaterial(rm);
				}
			}
		}

		/// <summary>
		/// Upload changes to shaders
		/// </summary>
		public void UploadShaderChanges()
		{
			// map shaders. key is RenderHash
			foreach (var shader in _shaderDatabase.ShaderChanges)//m_cq_shaders)
			{
				if (_renderEngine.CancelRender) return;

				shader.Gamma = GammaLinearWorkflow;

				// create a cycles shader
				var sh = _renderEngine.CreateMaterialShader(shader);
				_shaderDatabase.RecordRhCclShaderRelation(shader.Id, sh);
				_shaderDatabase.Add(shader, sh);
				// add the new shader to scene
				var scshid = _renderEngine.Client.Scene.AddShader(sh);
				_shaderDatabase.RecordCclShaderSceneId(shader.Id, scshid);

				sh.Tag();
			}
		}

		#endregion SHADERS

		#region GROUNDPLANE

		/// <summary>
		/// Guid of our groundplane object.
		/// </summary>
		private readonly Tuple<Guid, int> _groundplaneGuid = new Tuple<Guid, int>(new Guid("306690EC-6E86-4676-B55B-1A50066D7432"), 0);


		/// <summary>
		/// The mesh instance id for ground plane
		/// </summary>
		private const uint GroundPlaneMeshInstanceId = 1;

		private readonly float gp_side_extension = 1.0E+10f;
		private void InitialiseGroundPlane(CqGroundPlane gp)
		{
			var gpid = _groundplaneGuid;
			var altitude = (float) (gp.Enabled ? gp.Altitude : 0.0);
			if (!_dynamic)
			{
				Plane p = new Plane(Point3d.Origin, Vector3d.ZAxis);
				Plane pmap = new Plane(Point3d.Origin, Vector3d.ZAxis);
				var xext = new Interval(-gp_side_extension, gp_side_extension);
				var yext = new Interval(-gp_side_extension, gp_side_extension);
				var smext = new Interval(0.0, 1.0);
				PlaneSurface ps = new PlaneSurface(p, xext, yext);
				var mp = new MeshingParameters
				{
					GridMinCount = 512,
					GridMaxCount = 1024,
					SimplePlanes = false
				};
				var meshes = Rhino.Geometry.Mesh.CreateFromBrep(ps.ToBrep(), mp);
				Rhino.Geometry.Mesh m = new Rhino.Geometry.Mesh();
				foreach (var mesh in meshes) m.Append(mesh);

				Rhino.Geometry.Transform tfm = Rhino.Geometry.Transform.Identity;
				var texscale = gp.TextureScale;
				var tscale = Rhino.Geometry.Transform.Scale(p, texscale.X, texscale.Y, 1.0);
				tfm *= tscale;
				var motion = new Rhino.Geometry.Vector3d(gp.TextureOffset.X, gp.TextureOffset.Y, 0.0);
				var ttrans = Rhino.Geometry.Transform.Translation(motion);
				tfm *= ttrans;
				var trot = Rhino.Geometry.Transform.Rotation(gp.TextureRotation, Point3d.Origin);
				tfm *= trot;
				var texturemapping = TextureMapping.CreatePlaneMapping(pmap, smext, smext, smext);
				if (texturemapping != null)
				{
					m.SetTextureCoordinates(texturemapping, tfm, false);
					m.SetCachedTextureCoordinates(texturemapping, ref tfm);
				}


				HandleMeshData(_groundplaneGuid.Item1, _groundplaneGuid.Item2, m);

				var t = ccl.Transform.Translate(0.0f, 0.0f, altitude);
				var cyclesObject = new CyclesObject
				{
					matid = gp.MaterialId,
					obid = GroundPlaneMeshInstanceId,
					meshid = gpid,
					Transform = t,
					Visible = gp.Enabled,
					IsShadowCatcher = gp.IsShadowOnly
				};

				_objectShaderDatabase.RecordRenderHashRelation(gp.MaterialId, gpid, GroundPlaneMeshInstanceId);
				_objectDatabase.RecordObjectIdMeshIdRelation(GroundPlaneMeshInstanceId, gpid);
				_objectDatabase.AddOrUpdateObject(cyclesObject);
			}
			else
			{
				var t = ccl.Transform.Translate(0.0f, 0.0f, altitude);
				CyclesObjectTransform cot = new CyclesObjectTransform(GroundPlaneMeshInstanceId, t);
				_objectDatabase.AddDynamicObjectTransform(cot);
			}
		}
		/// <summary>
		/// Handle ground plane changes.
		/// </summary>
		/// <param name="gp"></param>
		protected override void ApplyGroundPlaneChanges(CqGroundPlane gp)
		{
			//System.Diagnostics.Debug.WriteLine("groundplane");
			InitialiseGroundPlane(gp);

			var mat = MaterialFromId(gp.MaterialId);
			HandleRenderMaterial(mat);

			var obid = GroundPlaneMeshInstanceId;

			HandleMaterialChangeOnObject(mat.RenderHash, obid);
		}

		#endregion

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		/// <param name="dynamicObjectTransforms">List of DynamicObject transforms</param>
		protected override void ApplyDynamicObjectTransforms(List<DynamicObjectTransform> dynamicObjectTransforms)
		{
			foreach (var dot in dynamicObjectTransforms)
			{
				//System.Diagnostics.Debug.WriteLine("DynObXform {0}", dot.MeshInstanceId);
				var cot = new CyclesObjectTransform(dot.MeshInstanceId, CclXformFromRhinoXform(dot.Transform));
				_objectDatabase.AddDynamicObjectTransform(cot);
			}
		}

		#region LIGHT & SUN

		/// <summary>
		/// Upload all light changes to the Cycles render engine
		/// </summary>
		public void UploadLightChanges()
		{

			/* new light shaders and lights. */
			foreach (var l in _lightDatabase.LightsToAdd)
			{
				if (_renderEngine.CancelRender) return;

				l.Gamma = GammaLinearWorkflow;

				var lgsh = _renderEngine.CreateSimpleEmissionShader(l);
				_renderEngine.Client.Scene.AddShader(lgsh);
				_shaderDatabase.Add(l, lgsh);

				if (_renderEngine.CancelRender) return;

				var light = new CclLight(_renderEngine.Client, _renderEngine.Client.Scene, lgsh)
				{
					Type = l.Type,
					Size = l.Size,
					Location = l.Co,
					Direction = l.Dir,
					UseMis = l.UseMis,
					CastShadow = l.CastShadow,
					Samples = 1,
					MaxBounces = 1024,
					SizeU = l.SizeU,
					SizeV = l.SizeV,
					AxisU = l.AxisU,
					AxisV = l.AxisV,
				};

				switch (l.Type)
				{
					case LightType.Area:
						break;
					case LightType.Point:
						break;
					case LightType.Spot:
						light.SpotAngle = l.SpotAngle;
						light.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						break;
				}

				light.TagUpdate();
				_lightDatabase.RecordLightRelation(l.Id, light);
			}

			// update existing ones
			foreach (var l in _lightDatabase.LightsToUpdate)
			{
				var existingL = _lightDatabase.ExistingLight(l.Id);
				TriggerLightShaderChanged(l, existingL.Shader);

				existingL.Type = l.Type;
				existingL.Size = l.Size;
				existingL.Location = l.Co;
				existingL.Direction = l.Dir;
				existingL.UseMis = l.UseMis;
				existingL.CastShadow = l.CastShadow;
				existingL.Samples = 1;
				existingL.MaxBounces = 1024;
				existingL.SizeU = l.SizeU;
				existingL.SizeV = l.SizeV;
				existingL.AxisU = l.AxisU;
				existingL.AxisV = l.AxisV;

				switch (l.Type)
				{
					case LightType.Area:
						break;
					case LightType.Point:
						break;
					case LightType.Spot:
						existingL.SpotAngle = l.SpotAngle;
						existingL.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						break;
				}
				existingL.TagUpdate();
			}
		}

		private uint LinearLightMaterialCRC(Rhino.Geometry.Light ll)
		{
			uint crc = 0xBABECAFE;

			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.R);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.G);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.B);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Intensity);

			return crc;
		}

		private void HandleLightMaterial(Rhino.Geometry.Light rgl)
		{
			var matid = LinearLightMaterialCRC(rgl);
			if (_shaderDatabase.HasShader(matid)) return;

			var emissive = new Materials.EmissiveMaterial();
			Color4f color = new Color4f(rgl.Diffuse);
			emissive.BeginChange(RenderContent.ChangeContexts.Ignore);
			emissive.SetParameter("emission_color", color);
			emissive.SetParameter("strength", (float)rgl.Intensity);
			emissive.EndChange();
			var shader = new CyclesShader
			{
				Name = rgl.Name,
				Id = matid,
				Crm = emissive,
				CyclesMaterialType = CyclesShader.CyclesMaterial.Emissive
			};

			_shaderDatabase.AddShader(shader);
		}

		/// <summary>
		/// Handle light changes
		/// </summary>
		/// <param name="lightChanges"></param>
		protected override void ApplyLightChanges(List<CqLight> lightChanges)
		{
			foreach (var light in lightChanges)
			{
				if (light.Data.IsLinearLight)
				{
					uint lightmeshinstanceid = light.IdCrc;
					var ld = light.Data;
					switch (light.ChangeType)
					{
						case CqLight.Event.Deleted:
							var cob = _objectDatabase.FindObjectRelation(lightmeshinstanceid);
							var delob = new CyclesObject {cob = cob};
							_objectDatabase.DeleteObject(delob);
							_objectDatabase.DeleteMesh(ld.Id);
							break;
						default:
							HandleLinearLightAddOrModify(lightmeshinstanceid, ld);
							break;
					}
				}
				else
				{
				// we don't necessarily get view changes prior to light changes, so
				// the old _currentViewInfo could be null - at the end of a Flush
				// it would be thrown away. Hence we now ask the ChangeQueue for the
				// proper view info. It will be given if one constructed the ChangeQueue
				// with a view to force it to be a single-view only ChangeQueue.
				// See #RH-32345 and #RH-32356
					var v = GetQueueView();
					var cl = _shaderConverter.ConvertLight(this, light, v, GammaLinearWorkflow);

					_lightDatabase.AddLight(cl);
				}
			}
		}

		private void HandleLinearLightAddOrModify(uint lightmeshinstanceid, RGLight ld)
		{
			var brepf = ld.HasBrepForm;
			var p = new Plane(ld.Location, ld.Direction);
			var circle = new Circle(p, ld.Width.Length);
			var c = new Cylinder(circle, ld.Direction.Length);
			var m = Rhino.Geometry.Mesh.CreateFromBrep(c.ToBrep(true, true));
			var mesh = new Rhino.Geometry.Mesh();
			foreach (var im in m) mesh.Append(im);
			var t = ccl.Transform.Identity();
			//t.SetTranslate(ld.Location.X, ld.Location.Y, ld.Location.Z);

			var ldid = new Tuple<Guid, int>(ld.Id, 0);

			var matid = LinearLightMaterialCRC(ld);

			HandleLightMaterial(ld);

			HandleMeshData(ld.Id, 0, mesh);

			var lightObject = new CyclesObject
			{
				matid = matid,
				obid = lightmeshinstanceid,
				meshid = ldid,
				Transform = t,
				Visible = true,
				IsShadowCatcher = false
			};

			_objectShaderDatabase.RecordRenderHashRelation(matid, ldid, lightmeshinstanceid);
			_objectDatabase.RecordObjectIdMeshIdRelation(lightmeshinstanceid, ldid);
			_objectDatabase.AddOrUpdateObject(lightObject);
			HandleMaterialChangeOnObject(matid, lightmeshinstanceid);
		}

		protected override void ApplyDynamicLightChanges(List<RGLight> dynamicLightChanges)
		{
			foreach (var light in dynamicLightChanges)
			{
				if (light.IsLinearLight)
				{
					uint lightmeshinstanceid = CrcFromGuid(light.Id);
					HandleLinearLightAddOrModify(lightmeshinstanceid, light);
				}
				else
				{
					var cl = _shaderConverter.ConvertLight(light, GammaLinearWorkflow);
					//System.Diagnostics.Debug.WriteLine("dynlight {0} @ {1}", light.Id, light.Location);
					_lightDatabase.AddLight(cl);
				}
			}
		}

		/// <summary>
		/// Sun ID
		/// </summary>
		private readonly Guid _sunGuid = new Guid("82FE2C29-9632-473D-982B-9121E150E1D2");

		/// <summary>
		/// Handle sun changes
		/// </summary>
		/// <param name="sun"></param>
		protected override void ApplySunChanges(RGLight sun)
		{
			var cl = _shaderConverter.ConvertLight(sun, GammaLinearWorkflow);
			cl.Id = _sunGuid;
			_lightDatabase.AddLight(cl);
			//System.Diagnostics.Debug.WriteLine("Sun {0} {1} {2}", sun.Id, sun.Intensity, sun.Diffuse);
		}

#endregion

		public void UploadEnvironmentChanges()
		{
			if (_environmentDatabase.BackgroundHasChanged)
			{
				RhinoShader curbg;
				_renderEngine.RecreateBackgroundShader(_environmentDatabase.CyclesShader, out curbg);
			}
		}

		/// <summary>
		/// Upload object changes
		/// </summary>
		public void UploadObjectChanges()
		{
			// first delete objects
			foreach (var ob in _objectDatabase.DeletedObjects)
			{
				if (ob.cob != null)
				{
					var cob = ob.cob;
					// deleting we do (for now?) by marking object as hidden.
					// we *don't* clear mesh data here, since that very mesh
					// may be used elsewhere.
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			// now combine objects and meshes, creating new objects when necessary
			foreach (var ob in _objectDatabase.NewOrUpdatedObjects)
			{
				// mesh for this object id
				var mesh = _objectDatabase.FindMeshRelation(ob.meshid);

				// hmm, no mesh. Oh well, lets get on with the next
				if (mesh == null) continue;

				// see if we already have an object here.
				// update it, otherwise create new one
				var cob = _objectDatabase.FindObjectRelation(ob.obid);

				var newcob = cob == null;

				// new object, so lets create it and record necessary stuff about it
				if (newcob)
				{
					cob = new CclObject(_renderEngine.Client);
					_objectDatabase.RecordObjectRelation(ob.obid, cob);
					_objectDatabase.RecordObjectIdMeshIdRelation(ob.obid, ob.meshid);
				}

				// set mesh reference and other stuff
				cob.Mesh = mesh;
				cob.Transform = ob.Transform;
				cob.IsShadowCatcher = ob.IsShadowCatcher;
				cob.Visibility = ob.Visible ? (ob.IsShadowCatcher ? PathRay.Camera : PathRay.AllVisibility): PathRay.Hidden;
				cob.TagUpdate();
			}
		}

		/// <summary>
		/// Handle skylight changes
		/// </summary>
		/// <param name="skylight">New skylight information</param>
		protected override void ApplySkylightChanges(CqSkylight skylight)
		{
			//System.Diagnostics.Debug.WriteLine("{0}", skylight);
			_environmentDatabase.SetSkylightEnabled(skylight.Enabled);
			_environmentDatabase.SetGamma(GammaLinearWorkflow);
		}


		protected override void ApplyRenderSettingsChanges(RenderSettings rs)
		{
			if (rs != null)
			{
				_cameraDatabase.HandleBlur(rs);
				_environmentDatabase.SetBackgroundData(rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				if (rs.BackgroundStyle == BackgroundStyle.Environment)
				{
					UpdateAllEnvironments();
				}
				else if (rs.BackgroundStyle == BackgroundStyle.WallpaperImage)
				{
					var view = GetQueueView();
					var y = string.IsNullOrEmpty(view.WallpaperFilename);
					sdd.WriteLine(
						$"view has {(y ? "no" : "")} wallpaper {(y ? "" : "with filename ")} {(y ? "" : view.WallpaperFilename)} {(y ? "" : "its grayscale bool")} {(y ? "" : $"{view.ShowWallpaperInGrayScale}")} {(y ? "" : "its hidden bool")} {(y ? "" : $"{view.WallpaperHidden}")}");
					_environmentDatabase.BackgroundWallpaper(view, rs.ScaleBackgroundToFit);
				}
				_environmentDatabase.SetGamma(GammaLinearWorkflow);
				_renderSettingsDatabase.SetQuality(rs.AntialiasLevel);
				_renderEngine.Settings.SetQuality(rs.AntialiasLevel);
			}
		}

		/// <summary>
		/// Handle environment changes
		/// </summary>
		/// <param name="usage"></param>
		protected override void ApplyEnvironmentChanges(RenderEnvironment.Usage usage)
		{
			/* instead of just reading the one environment we have to read everything.
			 * 
			 * The earlier assumption that non-changing EnvironmentIdForUsage meant non-changing
			 * environment instance is wrong. See http://mcneel.myjetbrains.com/youtrack/issue/RH-32418
			 */
			UpdateAllEnvironments();
			_environmentDatabase.SetGamma(GammaLinearWorkflow);

			//System.Diagnostics.Debug.WriteLine("{0}, env {1}", usage, env);
		}

		private void UpdateAllEnvironments()
		{
			var bgenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Background);
			var skyenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Skylighting);
			var reflenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.ReflectionAndRefraction);
			var bgenv = EnvironmentForid(bgenvId);
			var skyenv = EnvironmentForid(skyenvId);
			var reflenv = EnvironmentForid(reflenvId);

			_environmentDatabase.SetBackground(bgenv, RenderEnvironment.Usage.Background);
			_environmentDatabase.SetBackground(skyenv, RenderEnvironment.Usage.Skylighting);
			_environmentDatabase.SetBackground(reflenv, RenderEnvironment.Usage.ReflectionAndRefraction);
		}

		/// <summary>
		/// We get notified of (dynamic?) changes.
		/// </summary>
		protected override void NotifyBeginUpdates()
		{
			// nothing
		}

		/// <summary>
		/// Changes have been signalled.
		/// </summary>
		protected override void NotifyEndUpdates()
		{
			_renderEngine.Flush = true;
		}

		private bool _dynamic = false;
		protected override void NotifyDynamicUpdatesAreAvailable()
		{
			_dynamic = true;
			// nothing
			//System.Diagnostics.Debug.WriteLine("dyn changes...");
		}

		/// <summary>
		/// Tell ChangeQueue we want baking for
		/// - Decals
		/// - ProceduralTextures
		/// - MultipleMappingChannels
		/// </summary>
		/// <returns></returns>
		protected override BakingFunctions BakeFor()
		{
			return BakingFunctions.Decals | BakingFunctions.ProceduralTextures | BakingFunctions.MultipleMappingChannels;
		}

		protected override bool ProvideOriginalObject()
		{
			return true;
		}
	}
}
