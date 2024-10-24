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

using ccl;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CclClippingPlane = ccl.ClippingPlane;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;
using CqClippingPlane = Rhino.Render.ChangeQueue.ClippingPlane;
using CqGroundPlane = Rhino.Render.ChangeQueue.GroundPlane;
using CqLight = Rhino.Render.ChangeQueue.Light;
using CqMaterial = Rhino.Render.ChangeQueue.Material;
using CqMesh = Rhino.Render.ChangeQueue.Mesh;
using CqSkylight = Rhino.Render.ChangeQueue.Skylight;
using RGLight = Rhino.Geometry.Light;

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
		private LightDatabase _lightDatabase { get; } = new LightDatabase();

		/// <summary>
		/// Database responsible for keeping track of background and environment changes and their
		/// relations between Rhino and Cycles.
		/// </summary>
		private readonly EnvironmentDatabase _environmentDatabase;

		/// <summary>
		/// Database responsible for managing camera transforms from Rhino to Cycles.
		/// </summary>
		private readonly CameraDatabase _cameraDatabase = new CameraDatabase();

		/// <summary>
		/// Database responsible for managing render settings.
		/// </summary>
		private readonly RenderSettingsDatabase _renderSettingsDatabase = new RenderSettingsDatabase();

		#endregion

		private readonly ShaderConverter _shaderConverter = new ShaderConverter();

		private readonly bool _modalRenderer;

		public uint Blades { get; } = (uint)RcCore.It.AllSettings.Blades;
		public float BladesRotation { get; } = RcCore.It.AllSettings.BladesRotation;
		public float ApertureRatio { get; } = RcCore.It.AllSettings.ApertureRatio;
		public float ApertureFactor { get; } = RcCore.It.AllSettings.ApertureFactor;

		public BitmapConverter BitmapConverter { get; private set; }
		uint _doc_serialnr;

		internal ChangeDatabase(Guid pluginId, RenderEngine engine, uint doc, ViewInfo view, DisplayPipelineAttributes attributes, bool modal, BitmapConverter bitmapConverter) : base(pluginId, doc, view, attributes, true, !modal)
		{
			var _doc = RhinoDoc.FromRuntimeSerialNumber(doc);
			shadowCatcherMaterial = (RenderMaterial)RenderContent.FromId(_doc, ContentUuids.DefaultMaterialInstance);
			BitmapConverter = bitmapConverter;
			_environmentDatabase = new EnvironmentDatabase(BitmapConverter, doc);
			_renderEngine = engine;
			_doc_serialnr = doc;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
			_modalRenderer = modal;
		}


		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="createPreviewEventArgs">preview event arguments</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, CreatePreviewEventArgs createPreviewEventArgs, BitmapConverter bitmapConverter, uint docsrn) : base(pluginId, createPreviewEventArgs)
		{
			BitmapConverter = bitmapConverter;
			_doc_serialnr = docsrn;
			_environmentDatabase = new EnvironmentDatabase(BitmapConverter, docsrn);
			_renderEngine = engine;
			_modalRenderer = true;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
			_environmentDatabase.CyclesShader.PreviewBg = engine is RenderEngines.PreviewRenderEngine;
		}

		protected override void Dispose(bool isDisposing)
		{
			_environmentDatabase?.Dispose();
			_objectShaderDatabase?.Dispose();
			_objectDatabase?.Dispose();
			_shaderDatabase?.Dispose();
			base.Dispose(isDisposing);
		}

		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		public void UploadObjectShaderChanges()
		{
			if (_shaderDatabase?.ObjectShaderChanges.Count == 0) return;

			RcCore.It.AddLogStringIfVerbose("\tUploadObjectShaderChanges entry");
			RcCore.It.AddLogStringIfVerbose($"Uploading object shader changes {_shaderDatabase.ObjectShaderChanges.Count}\n");
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
						cob.Shader = newShader.Id;
						newShader.Tag();
					}
					oldShader?.Tag(false); // tag old shader to be no longer used (on this object)
					cob.TagUpdate();
					_objectShaderDatabase.ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
				}
			}
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Object shaders handled", -1.0f);
			RcCore.It.AddLogStringIfVerbose("\tUploadObjectShaderChanges exit");
		}

		public event EventHandler<LinearWorkflowChangedEventArgs> LinearWorkflowChanged;
		public event EventHandler<MaterialShaderUpdatedEventArgs> MaterialShaderChanged;
		public event EventHandler<LightShaderUpdatedEventArgs> LightShaderChanged;
		public event EventHandler FilmUpdateTagged;

		public void UploadGammaChanges()
		{
			if (LinearWorkflowHasChanged)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadGammaChanges entry");
				//_environmentDatabase.CurrentBackgroundShader?.Reset();

				foreach (var tup in _shaderDatabase.AllShaders)
				{
					var cclsh = tup.Item2;
					if (tup.Item1 is CyclesShader matsh)
					{
						RcCore.It.AddLogStringIfVerbose($"Updating material {cclsh.Id}, old gamma {matsh.Gamma} new gamma {PreProcessGamma}");
						matsh.Gamma = PreProcessGamma;
						TriggerMaterialShaderChanged(matsh, cclsh);
					}

					if (tup.Item1 is CyclesLight lgsh)
					{
						RcCore.It.AddLogStringIfVerbose($"Updating light {cclsh.Id}, old gamma {lgsh.Gamma} new gamma {PreProcessGamma}");
						lgsh.Gamma = PreProcessGamma;
						TriggerLightShaderChanged(lgsh, cclsh);
					}

				}

				TriggerLinearWorkflowUploaded();
				TriggerFilmUpdateTagged();

				_renderEngine.SetProgress(_renderEngine.RenderWindow, "Gamma handled", -1.0f);
				RcCore.It.AddLogStringIfVerbose("\tUploadGammaChanges exit");
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
			LinearWorkflowChanged?.Invoke(this, new LinearWorkflowChangedEventArgs(LinearWorkflow));
		}

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		public void UploadDynamicObjectTransforms()
		{
			if(_objectDatabase?.ObjectTransforms.Count > 0) {
				RcCore.It.AddLogStringIfVerbose("\tUploadDynamicObjectTransforms entry");
				Parallel.ForEach(_objectDatabase.ObjectTransforms, cot =>
				{
					var cob = _objectDatabase.FindObjectRelation(cot.Id);
					if (cob == null) return;

					cob.Transform = cot.Transform;
					cob.Mesh?.TagRebuild();
					cob.TagUpdate();
				});

				RcCore.It.AddLogStringIfVerbose("\tUploadDynamicObjectTransforms exit");
			}
		}

		/// <summary>
		/// Upload mesh changes
		/// </summary>
		public void UploadMeshChanges()
		{
			if(_objectDatabase?.MeshesToDelete.Count > 0 || _objectDatabase?.MeshChanges.Count > 0)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadMeshChanges entry");
				// handle mesh deletes first
				foreach (var meshDelete in _objectDatabase.MeshesToDelete)
				{
					var cobs = _objectDatabase.GetCyclesObjectsForGuid(meshDelete);

					foreach (var cob in cobs)
					{
						if (cob == null) continue;

						RcCore.It.AddLogStringIfVerbose($"\t\tDeleting mesh {cob}.{cob.Mesh?.GeometryPointer} ({meshDelete})");
						// remove mesh data
						cob.Mesh?.ClearData();
						cob.Mesh?.TagRebuild();
						// hide object containing the mesh
						cob.Visibility = PathRay.Hidden;
						cob.TagUpdate();
					}
				}

				var totalmeshes = _objectDatabase.MeshChanges.Count;
				RcCore.It.AddLogStringIfVerbose($"\t\tUploading {totalmeshes} mesh changes");
				_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Uploading mesh changes ({totalmeshes})", -1.0f);
				// First collect list of changes, so we can parallelize actual data
				// upload.
				List<Tuple<CyclesMesh, ccl.Mesh>> meshChangesList = new();
				foreach (var meshChange in _objectDatabase.MeshChanges)
				{
					var cyclesMesh = meshChange.Value;
					var mid = meshChange.Key;

					var me = _objectDatabase.FindMeshRelation(mid);

					// newme true if we have to upload new mesh data
					var newme = me == null;

					if (_renderEngine.ShouldBreak) return;

					// creat a new mesh to upload mesh data to
					if (newme)
					{
						me = new CclMesh(_renderEngine.Session, _renderEngine._Shader);
					}

					if (newme) _objectDatabase.RecordObjectMeshRelation(cyclesMesh.MeshId, me);
					_objectDatabase.RecordMeshOcsFrame(me.GeometryPointer, cyclesMesh.OcsFrame);

					meshChangesList.Add(new Tuple<CyclesMesh, ccl.Mesh>(cyclesMesh, me));
				}

				bool _uploadMeshData = true;
				// now upload mesh data in parallel
				Parallel.ForEach(meshChangesList, meshChange =>
				{
					if (!_uploadMeshData) return;
					var cyclesMesh = meshChange.Item1;
					var me = meshChange.Item2;

					me.Resize((uint)cyclesMesh.Verts.Length / 3, (uint)cyclesMesh.Faces.Length / 3);

					// update status bar of render window.
					var stat =
						$"Upload mesh {cyclesMesh.MeshId.Item1} [v: {cyclesMesh.Verts.Length / 3}, t: {cyclesMesh.Faces.Length / 3}]";
					RcCore.It.AddLogStringIfVerbose($"\t\t{stat}");

					// upload, if we get false back we were signalled to stop rendering by user
					if (!UploadMeshData(me, cyclesMesh)) _uploadMeshData = false;
				});
				_renderEngine.SetProgress(_renderEngine.RenderWindow, "Mesh changes done", -1.0f);
				RcCore.It.AddLogStringIfVerbose("\tUploadMeshChanges exit");
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
			var verts = cyclesMesh.Verts;
			me.SetVerts(ref verts);
			if (_renderEngine.ShouldBreak) return false;
			// set the triangles
			var faces = cyclesMesh.Faces;
			me.SetVertTris(ref faces, cyclesMesh.VertexNormals != null);
			if (_renderEngine.ShouldBreak) return false;
			// set vertex normals
			if (cyclesMesh.VertexNormals != null)
			{
				var vertex_normals = cyclesMesh.VertexNormals;
				me.SetVertNormals(ref vertex_normals);
			}
			if (_renderEngine.ShouldBreak) return false;
			// set uvs
			if (cyclesMesh.Uvs?.Count > 0)
			{
				for (int idx = 0; idx < cyclesMesh.Uvs.Count; idx++)
				{
					var (chanidx, uvs) = cyclesMesh.Uvs[idx];
					string uvmap_name = $"uvmap{chanidx}";
					me.SetUvs(ref uvs, uvmap_name);
					// compute tangent space
					me.AttrTangentSpace(uvmap_name);
				}
			}
			// set vertex colors
			if(cyclesMesh.VertexColors != null)
			{
				var vcs = cyclesMesh.VertexColors;
				me.SetVertexColors(ref vcs);
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
			_currentViewInfo = null;
			DisplayPipelineAttributesChanged = false;
			HasClippingPlaneChanges = false;
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
			bool cameraChanges = _cameraDatabase.HasChanges();
			bool lightChanges = _lightDatabase.HasChanges();
			bool shaderChanges = _shaderDatabase.HasChanges();
			bool objectChanges = _objectDatabase.HasChanges();
			RcCore.It.AddLogString($"ChangeDatabase.HasChanges\n\tview: {cameraChanges}\n\tlight: {lightChanges}\n\tshader: {shaderChanges}\n\tobject: {objectChanges}\n\tclipping planes: {HasClippingPlaneChanges}\n\tbackground: {_environmentDatabase.BackgroundHasChanged}\n\tlwf: {LinearWorkflowHasChanged}\n\tdisplay attr: {DisplayPipelineAttributesChanged}");
			return
				HasClippingPlaneChanges ||
				cameraChanges ||
				_environmentDatabase.BackgroundHasChanged ||
				lightChanges ||
				shaderChanges ||
				objectChanges ||
				LinearWorkflowHasChanged ||
				DisplayPipelineAttributesChanged;
		}

		public bool HasBvhChanges()
		{
			return
				_objectDatabase?.MeshChanges.Count > 0 ||
				_objectDatabase?.NewOrUpdatedObjects.Count > 0;
		}

		private LinearWorkflow _linearWorkflow = new LinearWorkflow();

		public bool LinearWorkflowHasChanged { get; private set; }

		public float PreProcessGamma => _linearWorkflow.PreProcessColors || _linearWorkflow.PreProcessTextures ? _linearWorkflow.PreProcessGamma : 1.0f;

		public LinearWorkflow LinearWorkflow
		{
			set
			{
				if (_linearWorkflow.Equals(value)) return;

				_linearWorkflow.CopyFrom(value);
				LinearWorkflowHasChanged = true;
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

		protected override void ApplyLinearWorkflowChanges(Rhino.Render.LinearWorkflow lw)
		{
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Apply linear workflow", -1.0f);
			LinearWorkflow = lw;
			_environmentDatabase.SetGamma(PreProcessGamma);
		}


		private const uint ClippingPlaneMeshInstanceId = 2;

		private readonly Tuple<Guid, int> _clippingPlaneGuid = new Tuple<Guid, int>(new Guid("6A7DB550-7E42-4129-A36D-A4C8AAB06F4B"), 0);
		private readonly Dictionary<Guid, Plane> ClippingPlanes = new Dictionary<Guid, Plane>(16);
		private bool HasClippingPlaneChanges = false;

		protected override void ApplyDynamicClippingPlaneChanges(List<CqClippingPlane> changed)
		{
			HandleClippingPlaneChanges(changed, true);
			_renderEngine.Flush = true;
		}

		protected override void ApplyClippingPlaneChanges(Guid[] deleted, List<CqClippingPlane> addedOrModified)
		{
			foreach (var d in deleted)
			{
				ClippingPlanes.Remove(d);
				HasClippingPlaneChanges = true;
			}
			HandleClippingPlaneChanges(addedOrModified, false);
		}

		private void HandleClippingPlaneChanges(List<CqClippingPlane> addedOrModified, bool isDynamic)
		{
			foreach (var cp in addedOrModified)
			{
				if (cp.IsEnabled && (isDynamic || cp.ViewIds.Contains(ViewId)))
				{
					ClippingPlanes[cp.Id] = new Plane(cp.Plane);
				}
				else
				{
					ClippingPlanes.Remove(cp.Id);
				}
				HasClippingPlaneChanges = true;
			}
		}

		/// <summary>
		/// Upload clipping plane equations to Cycles.
		/// </summary>
		public void UploadClippingPlaneChanges()
		{
			if (HasClippingPlaneChanges)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadClippingPlaneChanges entry");
				_renderEngine.Session.Scene.ClearClippingPlanes();
				foreach (var cp in ClippingPlanes)
				{
					var equation = new float4(cp.Value.GetPlaneEquation());
					var cclcp = new CclClippingPlane(_renderEngine.Session, equation);
				}
				HasClippingPlaneChanges = false;
				RcCore.It.AddLogStringIfVerbose("\tUploadClippingPlaneChanges exit");
			}
		}

		/// <summary>
		/// Upload camera (viewport) changes to Cycles.
		/// </summary>
		public void UploadCameraChanges()
		{
			if (!_cameraDatabase?.HasChanges() ?? false) return;

			RcCore.It.AddLogStringIfVerbose("\tUploadCameraChanges entry");

			var view = _cameraDatabase.LatestView();
			if(view!=null)
			{
				UploadCamera(view);
			}
			var fb = _cameraDatabase.GetBlur();
			UploadFocalBlur(fb);
			RcCore.It.AddLogStringIfVerbose("\tUploadCameraChanges exit");
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
			var unitscale = (float)RhinoMath.UnitScale(UnitSystem.Millimeters, ModelUnitSystem);
			scene.Camera.ApertureSize = fb.FocalAperture < 0.00001f ? 0.0f : (fb.LensLength * unitscale) / fb.FocalAperture;
			scene.Camera.Blades = Blades;
			scene.Camera.BladesRotation = (float)RhinoMath.ToRadians(BladesRotation);
			scene.Camera.ApertureRatio = ApertureRatio;
			scene.Camera.Update();
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Focal blur handled", -1.0f);

		}

		/// <summary>
		/// Set the camera based on CyclesView
		/// </summary>
		/// <param name="view"></param>
		private void UploadCamera(CyclesView view)
		{
			var scene = _renderEngine.Session.Scene;
			var oldSize = _modalRenderer ? _renderEngine.FullSize : _renderEngine.RenderDimension;
			var newSize = new Size(view.Width, view.Height);
			if(!_modalRenderer) _renderEngine.RenderDimension = newSize;

			TriggerViewChanged(view.View, oldSize!=newSize, newSize);

			// Pick smaller of the angles
			var angle = newSize.Width > newSize.Height ? (float)view.Vertical * 2.0f : (float)view.Horizontal * 2.0f;

			scene.Camera.Size = _modalRenderer ? _renderEngine.FullSize : newSize;
			scene.Camera.Matrix = view.Transform;
			scene.Camera.Type = view.Projection;
			scene.Camera.Fov = angle;
			scene.Camera.BladesRotation = (float)Rhino.RhinoMath.ToRadians(BladesRotation);
			scene.Camera.ApertureRatio = ApertureRatio;
			scene.Camera.Blades = Blades;

			//scene.Camera.NearClip = (float)view.Near;
			scene.Camera.FarClip = (float)view.Far; // 1.0E+14f; // gp_side_extension;
			if (view.Projection == CameraType.Orthographic || view.TwoPoint) scene.Camera.SetViewPlane(view.Viewplane.Left, view.Viewplane.Right, view.Viewplane.Top, view.Viewplane.Bottom);
			else if(view.Projection == CameraType.Perspective) scene.Camera.ComputeAutoViewPlane();

			scene.Camera.SensorHeight = RcCore.It.AllSettings.SensorHeight;
			scene.Camera.SensorWidth = RcCore.It.AllSettings.SensorWidth;
			scene.Camera.Update();
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Camera changes handled", -1.0f);
		}

		public Size RenderDimension { get; set; }

		Rhino.Geometry.Transform _gObTransform = Rhino.Geometry.Transform.Identity;

		/// <summary>
		/// Handle view changes.
		/// </summary>
		/// <param name="viewInfo"></param>
		protected override void ApplyViewChange(ViewInfo viewInfo)
		{
			var fb = _cameraDatabase.HandleBlur(viewInfo);
			if (!_modalRenderer && !viewInfo.Viewport.Id.Equals(ViewId)) return;

			if (_wallpaperInitialized)
			{
				_environmentDatabase.SetGamma(PreProcessGamma);
				_environmentDatabase.BackgroundWallpaper(viewInfo, _previousScaleBackgroundToFit);
			}

			_currentViewInfo = viewInfo;

			var vp = new ViewportInfo(viewInfo.Viewport);

			_gObTransform = Rhino.Geometry.Transform.Identity;
			var viewscale = vp.GetViewScale();
			bool is_reflected = Math.Abs(viewscale[2] + 1.0) < 0.01 ;
			if(vp.IsParallelProjection && is_reflected) {
				// create a scale transform against the view plane on world origin.
				// this needs to be applied to all Cycles objects.
				var fp = vp.FrustumFarPlane;
				fp.Origin = Rhino.Geometry.Point3d.Origin;
				var scfp = Rhino.Geometry.Transform.Scale(fp, 1, 1, -1);
				_gObTransform = scfp;


				RcCore.It.AddLogStringIfVerbose($"ApplyViewChange\n\t{viewscale[0]}, {viewscale[1]}, {viewscale[2]}\n\tParallel projection reflected");
				RcCore.It.AddLogStringIfVerbose($"\n\n\t{scfp.ToString()}\n\n");
			}

			if(_modalRenderer) {
				var targetw = (float)_renderEngine.FullSize.Width;
				var targeth = (float)_renderEngine.FullSize.Height;
				vp.FrustumAspect = targetw / targeth;
			}

			// camera transform, camera to world conversion
			var rhinocam = vp.GetXform(CoordinateSystem.Camera, CoordinateSystem.World);
			// lens length
			var lenslength = vp.Camera35mmLensLength;

			// lets see if we need to do magic for two-point perspective
			var twopoint = vp.IsTwoPointPerspectiveProjection || vp.IsPerspectiveProjection;

			// frustum values, used for two point
			vp.GetFrustum(out double frl, out double frr, out double frb, out double frt, out double frn, out double frf);

			// For 2 point perspective frustum needs to be scaled
			if (twopoint)
			{
				// Rhino frustum dimensions
				var rhino_frustum_height = frt - frb;
				var rhino_frustum_width = frr - frl;

				var frustum_scale_factor = 1.0;
				if (rhino_frustum_width >= rhino_frustum_height)
				{
					// Use Cycles frustum height of 2 so that Cycles frustum width is at least 2
					var cycles_frustum_height = 2.0;
					frustum_scale_factor = cycles_frustum_height / rhino_frustum_height;
				}
				else
				{
					// Use Cycles frustum width of 2 so that Cycles frustum height is at least 2
					var cycles_frustum_width = 2.0;
					frustum_scale_factor = cycles_frustum_width / rhino_frustum_width;
				}

				frb = frustum_scale_factor * frb;
				frt = frustum_scale_factor * frt;
				frl = frustum_scale_factor * frl;
				frr = frustum_scale_factor * frr;
			}

			frb /= viewscale[1];
			frt /= viewscale[1];
			frl /= viewscale[0];
			frr /= viewscale[0];

			var parallel = vp.IsParallelProjection;

			var screenport = vp.GetScreenPort(out int near, out int far);
			var bottom = screenport.Bottom;
			var top = screenport.Top;
			var left = screenport.Left;
			var right = screenport.Right;
			int w;
			int h;
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
				w = _renderEngine.FullSize.Width;
				h = _renderEngine.FullSize.Height;
			}
			var viewAspectratio = (float)w / (float)h;

			// get camera angles
			vp.GetCameraAngles(out double diagonal, out double vertical, out double horizontal);

			if (twopoint)
			{
				// Calculate vertical camera angle for 2 point perspective by horizontal camera angle and view aspect ratio.
				vertical = Math.Atan(Math.Tan(horizontal) / viewAspectratio);
				if(vp.IsTwoPointPerspectiveProjection) {
					(frt, frb) = (frb, frt);
				}
			}



			// convert rhino transform to ccsycles transform
			var rt = rhinocam.ToCyclesTransform();
			// then convert to Cycles orientation
			var conversion_matrix = vp.IsTwoPointPerspectiveProjection ? ccl.Transform.RhinoToCyclesCamNoFlip : (is_reflected ? ccl.Transform.RhinoToCyclesCamReflected : ccl.Transform.RhinoToCyclesCam);
			var t = rt * conversion_matrix;

			// ready, lets push our data
			var cyclesview = new CyclesView
			{
				LensLength = lenslength,
				Transform = t,
				RhinoTransform = rt,
				Diagonal =  diagonal,
				Vertical = vertical,
				Horizontal = horizontal,
				ViewAspectRatio = viewAspectratio,
				Projection = parallel ? CameraType.Orthographic : CameraType.Perspective,
				Viewplane = new ViewPlane((float)frl, (float)frr, (float)frt, (float)frb),
				TwoPoint = twopoint,
				Width = w,
				Height = h,
				Near = frn,
				Far = frf > 1.0E+10f ? frf : 1.0E+10f,
				View = GetQueueView() // use GetQueueView to ensure we have a valid ViewInfo even after Flush
			};
			_renderEngine.View = null;
			_cameraDatabase.AddViewChange(cyclesview);
		}

		/// <summary>
		/// Handle mesh changes
		/// </summary>
		/// <param name="deleted"></param>
		/// <param name="added"></param>
		protected override void ApplyMeshChanges(Guid[] deleted, List<CqMesh> added)
		{
			RcCore.It.AddLogStringIfVerbose($"\tChangeDatabase ApplyMeshChanges, deleted {deleted.Length}");

			var totaldeletes = deleted.Length;
			var curdelete = 0;
			foreach (var guid in deleted)
			{
				curdelete++;
				// only delete those that aren't listed in the added list
				if (!(from mesh in added where mesh.Id() == guid select mesh).Any())
				{
					RcCore.It.AddLogStringIfVerbose($"\t\trecord mesh deletion {guid}\n");
					_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Delete mesh {curdelete}/{totaldeletes}", -1.0f);
					_objectDatabase.DeleteMesh(guid);
				}
			}

			RcCore.It.AddLogStringIfVerbose($"\t\tChangeDatabase ApplyMeshChanges added {added.Count}");
			var totaladds = added.Count;
			var curadd = 0;
			if (_renderEngine.ShouldBreak) return;

			foreach (var cqm in added)
			{
			if (_renderEngine.ShouldBreak) return;
				curadd++;
				_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Handle mesh {curadd}/{totaladds}", -1.0f);
				var meshes = cqm.GetMeshes();
				var mappingCollection = cqm.Mapping;
				Rhino.Geometry.Transform ocsInv;
				cqm.OcsTransform.TryGetInverse(out ocsInv);
				var t = ocsInv.ToCyclesTransform();
				var meshguid = cqm.Id();

				var attr = cqm.Attributes;

				bool isClippingObject = false;
				if(attr!=null && attr.HasUserData)
				{
					if(attr.UserData.Find(typeof(RhinoCyclesData)) is RhinoCyclesData ud)
					{
						isClippingObject = ud.IsClippingObject;
					}
				}
				if (_renderEngine.ShouldBreak) return;

				var meshIndex = 0;

				foreach(var meshdata in meshes)
				{
					// Do not add meshes that have no vertices. Adding them will only
					// prime Cycles for crashes when shaders are changed later.
					if (meshdata.Vertices.Count < 3)
					{
						RcCore.It.AddLogStringIfVerbose($"ApplyMeshChanges. skipping mesh with fewer than 3 vertices {meshguid}+{meshIndex}");
						continue;
					}

					if (_renderEngine.ShouldBreak) return;

					HandleMeshData(meshguid, meshIndex, meshdata, mappingCollection, isClippingObject, uint.MaxValue, t);
					meshIndex++;
				}
			}
		}

		public List<CyclesDecal> HandleMeshDecals(Guid meshguid, Decals decals, Rhino.Geometry.Transform instanceTransform)
		{
			// remove preprocessor stuff when working on this
			if (decals == null) return null;
			if(decals.Count()<1) return null;
			int idx = 0;
			List<CyclesDecal> decalList = new List<CyclesDecal>(decals.Count());

			StringBuilder sb = new StringBuilder();
			foreach (Decal decal in decals) {
				var mapping = decal.Mapping;
				var projection = decal.Projection;

				var across = decal.VectorAcross;
				var up = decal.VectorUp;
				var origin = decal.Origin;
				var height = decal.Height;
				var radius = decal.Radius;
				var horsweepstart = decal.StartLatitude;
				var horsweepend = decal.EndLatitude;
				var versweepstart = decal.StartLongitude;
				var versweepend = decal.EndLongitude;
				double umin = 0.0, umax = 1.0, vmin = 0.0, vmax = 1.0;
				decal.UVBounds(ref umin, ref vmin, ref umax, ref vmax);

				var texmapping = decal.GetTextureMapping();
				string mapstr = mapping.ToString();
				string projstr = projection.ToString();

				Plane fromPlane = Plane.WorldXY;

				switch (mapping) {
					case DecalMapping.Cylindrical:
					case DecalMapping.Spherical:
						if(decal.MapToInside) {
							projection = DecalProjection.Backward;
						} else {
							projection = DecalProjection.Forward;
						}
						break;
					case DecalMapping.UV:
						horsweepstart = (float)umin;
						horsweepend = (float)umax;
						versweepstart = (float)vmin;
						versweepend = (float)vmax;
						break;
					default:
						break;
				}

				// now need to fix up the texmapping to incorporate the given instance transform so we can
				// show decals properly in block instances.
				Plane toPlane = new Plane(origin, across, up);
				double acrossLength = across.Length;
				double upLength = up.Length;
				toPlane.Transform(instanceTransform);
				origin.Transform(instanceTransform);
				across.Transform(instanceTransform);
				up.Transform(instanceTransform);
				// create a new plane for cylindrical and spherical decals, this needs to be rotated -90°
				// on the plane X axis to ensure the decal gets oriented correctly.
				Plane cyl_spherPlane = new Plane(toPlane);
				cyl_spherPlane.Rotate(-Math.PI * 0.5, toPlane.XAxis);

				// create the new mappings using the transformed planes
				switch(mapping) {
					case DecalMapping.Planar:
						{
							Rhino.Geometry.Interval dx = new Interval(0, acrossLength);
							Rhino.Geometry.Interval dy = new Interval(0, upLength);
							Rhino.Geometry.Interval dz = new Interval(0, 0);
							texmapping = TextureMapping.CreatePlaneMapping(toPlane, dx, dy, dz);
						}
						break;
					case DecalMapping.Cylindrical:
						{
							var n = cyl_spherPlane.Normal;
							var l = new Rhino.Geometry.Line(cyl_spherPlane.Origin, n, -height/2.0);
							cyl_spherPlane.Origin = l.To;
							Rhino.Geometry.Circle circle = new Circle(cyl_spherPlane, radius);
							Cylinder cylinder = new Cylinder(circle, height);
							texmapping = TextureMapping.CreateCylinderMapping(cylinder, true);
						}
						break;
					case DecalMapping.Spherical:
						{
							Sphere sphere = new Sphere(cyl_spherPlane, radius);
							texmapping = TextureMapping.CreateSphereMapping(sphere);
						}
					break;
					case DecalMapping.UV:
					break;
				}

				// create the decal xform. We go from the XY plane to the (transformed) toPlane
				// which gives as the rotation and translation we need.
				Rhino.Geometry.Transform decalXform = Rhino.Geometry.Transform.PlaneToPlane(fromPlane, toPlane);

				// scale we calculate for planar decal based on the across and up vector lengths
				Rhino.Geometry.Transform scaleXform = Rhino.Geometry.Transform.Identity;

				if(mapping != DecalMapping.Cylindrical && mapping != DecalMapping.Spherical)
				{
					scaleXform = Rhino.Geometry.Transform.Scale(Rhino.Geometry.Plane.WorldXY, acrossLength, upLength, 1.0);
					decalXform = decalXform * scaleXform;
				}

				var rt = TextureFromOriginalInstanceId(decal.TextureInstanceId);

				CyclesTextureImage tex = new CyclesTextureImage();
				Utilities.HandleRenderTexture(rt, tex, false, true, BitmapConverter, _doc_serialnr, LinearWorkflow.PreProcessGamma, false, true);

				// TODO XXX
				var rtid = rt?.Id ?? Guid.Empty;
				string textype = tex.HasTextureImage ? (tex.HasByteImage ? "byte" : "float") : "no image";

				sb.Append($"\t{idx} : {mapstr} / {projstr} -> {textype} < {tex.TexWidth}, {tex.TexHeight}> ) | {decalXform}");
				CyclesDecal cyclesDecal = new CyclesDecal {
					Mapping = mapping,
					Projection = projection,
					TextureMapping = texmapping,
					Texture = tex,
					Height = (float)height,
					Radius = (float)radius,
					HorizontalSweepStart = (float)horsweepstart,
					HorizontalSweepEnd = (float)horsweepend,
					VerticalSweepStart = (float)versweepstart,
					VerticalSweepEnd = (float)versweepend,
					Transparency = (float)decal.Transparency,
					Transform = decalXform.ToCyclesTransform(),
					Origin = origin.ToFloat4(),
					Across = across.ToFloat4(),
					Up = up.ToFloat4(),
					CRC = (uint)decal.CRC
				};
				decalList.Insert(0, cyclesDecal);

				idx++;
			}
			string sbstr = sb.ToString();
			RhinoApp.OutputDebugString($"{sbstr}\n\n");
			return decalList;
		}

		public void HandleMeshTextureCoordinates(Rhino.Geometry.Mesh meshdata, int[] findices, List<Tuple<int, float[]>> cmuvList, int channelIndex)
		{
				var tc = meshdata.TextureCoordinates;
				var rhuv = tc.ToFloatArray();
				var cmuv = rhuv.Length > 0 ? new float[findices.Length * 2] : null;
				if (cmuv != null)
				{
					for (var fi = 0; fi < findices.Length; fi++)
					{
						if (_renderEngine.ShouldBreak) return;
						var fioffs = fi * 2;
						var findex = findices[fi];
						var findex2 = findex * 2;
						var rhuvit = rhuv[findex2];
						var rhuvit1 = rhuv[findex2 + 1];
						cmuv[fioffs] = rhuvit;
						cmuv[fioffs + 1] = rhuvit1;
					}
					cmuvList.Add(new Tuple<int, float[]>(channelIndex, cmuv));
				}
		}

		public void HandleMeshData(Guid meshguid, int meshIndex, Rhino.Geometry.Mesh meshdata, MappingChannelCollection mappingCollection, bool isClippingObject, uint linearlightMatId, ccl.Transform t)
		{
			if (_renderEngine.ShouldBreak) return;
			// Get face indices flattened to an
			// integer array. The result will be triangulated faces.
			var findices = meshdata.Faces.ToIntArray(true);
			RcCore.It.AddLogStringIfVerbose($"\tHandleMeshData: {meshdata.Faces.Count} .. {findices.Length / 3}");

			float[] rhvc = meshdata.VertexColors.ToFloatArray(meshdata.Vertices.Count);
			float[] cmvc = rhvc != null ? new float[findices.Length * 3] : null;
			if (cmvc != null)
			{
				for (var fi = 0; fi < findices.Length; fi++)
				{
					if (_renderEngine.ShouldBreak) return;
					var fioffs = fi * 3;
					var findex = findices[fi];
					var findex2 = findex * 3;
					var rhvcit = rhvc[findex2];
					var rhvcit1 = rhvc[findex2 + 1];
					var rhvcit2 = rhvc[findex2 + 2];
					cmvc[fioffs] = rhvcit;
					cmvc[fioffs + 1] = rhvcit1;
					cmvc[fioffs + 2] = rhvcit2;
				}
			}

			var vn = meshdata.Normals;
			var rhvn = vn.ToFloatArray();

			var cmuvList = new List<Tuple<int, float[]>>();

			if (_renderEngine.ShouldBreak) return;
			// now convert UVs: from vertex indexed array to per face per vertex
			if (mappingCollection == null)
			{
				// Get texture coordinates and
				// flattens to a float array.
				HandleMeshTextureCoordinates(meshdata, findices, cmuvList, 1);
			} else {
				foreach(var mapping in mappingCollection.Channels) {
					RcCore.It.AddLogStringIfVerbose($"\t\tHandleMeshData: mapping {mapping.Channel} {mapping.Mapping} {mapping.Local}");
					meshdata.SetTextureCoordinates(mapping.Mapping, mapping.Local, false);
					HandleMeshTextureCoordinates(meshdata, findices, cmuvList, mapping.Channel == 0 ? 1 : mapping.Channel);
				}
			}

			var meshid = new Tuple<Guid, int>(meshguid, meshIndex);

			var crc = linearlightMatId==uint.MaxValue ? _objectShaderDatabase.FindRenderHashForMeshId(meshid) : linearlightMatId;
			if (crc == uint.MaxValue) crc = 0;
			if (_renderEngine.ShouldBreak) return;

			// now we have everything we need
			// so we can create a CyclesMesh that the
			// RenderEngine can eventually commit to Cycles
			var cyclesMesh = new CyclesMesh
			{
				MeshId = meshid,
				Verts = meshdata.Vertices.ToFloatArray(),
				Faces = findices,
				Uvs = cmuvList,
				VertexNormals = rhvn,
				VertexColors = cmvc,
				MatId = crc,
				OcsFrame = t,
			};
			_objectDatabase.AddMesh(cyclesMesh);
			_objectDatabase.SetIsClippingObject(meshid, isClippingObject);
		}

		public double ModelAbsoluteTolerance { get; set; }
		public double ModelAngleToleranceRadians { get; set; }
		public Rhino.UnitSystem ModelUnitSystem { get; set; }

		private float jiggleFactor => 0.001f;
		private float gpJiggleFactor => 0.0011f;

		/// <summary>
		/// Create a jiggled transform of the MeshInstance transform.
		///
		/// To ensure the jiggling is stable use the MeshId GUID to generate a
		/// jiggle vector.
		/// </summary>
		/// <param name="a">MeshInstance to create jiggled transform for</param>
		/// <returns>Transform with jiggle translation applied</returns>
		private Rhino.Geometry.Transform Jiggle(MeshInstance a)
		{
			var objectXform = a.Transform;
			RcCore.It.AddLogStringIfVerbose($"\tJiggle: {objectXform}");
			var p = a.MeshId.ToByteArray();
			long l0 = BitConverter.ToInt64(p, 0);
			long l1 = BitConverter.ToInt64(p, 4);
			long l2 = BitConverter.ToInt64(p, 8);

			float f0 = (float)(l0 / (double)int.MaxValue);
			float f1 = (float)(l1 / (double)int.MaxValue);
			float f2 = (float)(l2 / (double)int.MaxValue);

			Vector3f jiggleVector = new Vector3f(f0, f1, f2);
			jiggleVector.Unitize();
			jiggleVector *= jiggleFactor;

			var jiggleTranslationXform = Rhino.Geometry.Transform.Translation(jiggleVector);

			RcCore.It.AddLogStringIfVerbose($"\tJiggle: {jiggleTranslationXform}");
			return objectXform * jiggleTranslationXform;
		}

		protected override void ApplyMeshInstanceChanges(List<uint> deleted, List<MeshInstance> addedOrChanged)
		{
			RcCore.It.AddLogStringIfVerbose($"ApplyMeshInstanceChanges: Received {deleted.Count} mesh instance deletes");
			foreach (var dm in deleted)
			{
				RcCore.It.AddLogStringIfVerbose($"\ttold to DELETE {dm}");
			}
			foreach (var aoc in addedOrChanged)
			{
				RcCore.It.AddLogStringIfVerbose($"\ttold to ADD {aoc.InstanceId}");
			}
			if (_renderEngine.ShouldBreak) return;
			RcCore.It.AddLogStringIfVerbose($"ApplyMeshInstanceChanges: Received {deleted.Count} mesh instance deletes");
			var inDeleted = from inst in addedOrChanged where deleted.Contains(inst.InstanceId) select inst;
			var skipFromDeleted = (from inst in inDeleted where true select inst.InstanceId).ToList();

			if (skipFromDeleted.Count > 0)
			{
				RcCore.It.AddLogStringIfVerbose($"\t{skipFromDeleted.Count} in both deleted and addedOrChanged!");
				foreach (var skip in skipFromDeleted)
				{
					RcCore.It.AddLogStringIfVerbose($"\t\t{skip} should not be deleted!");
				}
			}
			var realDeleted = (from dlt in deleted where !skipFromDeleted.Contains(dlt) select dlt).ToList();
			RcCore.It.AddLogStringIfVerbose($"\tActually deleting {realDeleted.Count} mesh instances!");
			var totaldel = realDeleted.Count;

			_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Deleting mesh instances: {totaldel}", -1.0f);
			Parallel.ForEach(realDeleted, d =>
			{
				if (_renderEngine.ShouldBreak) return;
				var cob = _objectDatabase.FindObjectRelation(d);
				if (cob != null)
				{
					var delob = new CyclesObject { cob = cob };
					_objectDatabase.DeleteObject(delob);
					RcCore.It.AddLogStringIfVerbose($"\tDeleting mesh instance {d} (ptr {cob.ObjectPtr})");
				}
				else
				{
					RcCore.It.AddLogStringIfVerbose($"\tMesh instance {d} has no object relation..");
				}
			});
			var totalmeshes = addedOrChanged.Count;
			var curmesh = 0;
			RcCore.It.AddLogStringIfVerbose($"ApplyMeshInstanceChanges: Received {totalmeshes} mesh instance changes");
			_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Handling adds/edits mesh instances: {totalmeshes}", -1.0f);
			//foreach (var a in addedOrChanged)
			Parallel.ForEach(addedOrChanged, a =>
			{
				curmesh++;

				if (_renderEngine.ShouldBreak) return;

#pragma warning disable CS0618
				var meshid = new Tuple<Guid, int>(a.MeshId, a.MeshIndex);
				var cyclesDecals = HandleMeshDecals(a.MeshId, a.Decals, a.Transform);

				var matid = a.MaterialId;
				var mat = a.RenderMaterial;

				var stat = $"\tHandling mesh instance ({a.InstanceId}). material {mat.Name}. Mesh id {meshid}.";
				RcCore.It.AddLogStringIfVerbose(stat);

				if (cyclesDecals != null)
				{
					uint decalsCRC = CyclesDecal.CRCForList(cyclesDecals);
					matid = a.Transform.TransformCrc(matid);
					matid = RhinoMath.CRC32(matid, a.MeshId.ToByteArray());
					matid = RhinoMath.CRC32(matid, a.MeshIndex);
					matid = RhinoMath.CRC32(matid, decalsCRC);
				}

				HandleRenderMaterial(mat, matid, cyclesDecals, false);

				//var cutout = _objectDatabase.MeshIsClippingObject(meshid);
#pragma warning disable CS0618
				// Cycles does not cope well with coincident surfaces. Therefor it is
				// important to ever so slightly move around the objects - to jiggle
				// them.
				var obxform = _gObTransform * Jiggle(a) ;
				var ob = new CyclesObject
				{
					obid = a.InstanceId,
					meshid = meshid,
					Transform = obxform.ToCyclesTransform(),
					OcsFrame = Rhino.Geometry.Transform.Identity.ToCyclesTransform(),
					matid = matid,
					CastShadow = a.CastShadows,
					Cutout = false, //cutout,
					Decals = cyclesDecals
				};
				var oldhash = _objectShaderDatabase.FindRenderHashForObjectId(a.InstanceId);

				HandleShaderChange(a.InstanceId, oldhash, matid, meshid);

				_objectDatabase.AddOrUpdateObject(ob);
			});
		}

		/// <summary>
		/// Record shader change if any change found
		/// </summary>
		/// <param name="obid">object id (meshinstance id)</param>
		/// <param name="oldhash">render hash (material id) from previously used material</param>
		/// <param name="newhash">render hash (material id) from new material</param>
		/// <param name="meshid"> mesh id the materials are for. (guid * mesh index)</param>
		public void HandleShaderChange(uint obid, uint oldhash, uint newhash, Tuple<Guid, int> meshid)
		{
			var shaderchange = new CyclesObjectShader(obid)
			{
				OldShaderHash = oldhash,
				NewShaderHash = newhash
			};

			if (shaderchange.Changed)
			{
				RcCore.It.AddLogStringIfVerbose(
					$"\t\tsetting material, from old {shaderchange.OldShaderHash} to new {shaderchange.NewShaderHash}");

				_shaderDatabase.AddObjectMaterialChange(shaderchange);

				if (meshid != null)
				{
					_objectShaderDatabase.RecordRenderHashRelation(newhash, meshid, obid);
					_objectDatabase.RecordObjectIdMeshIdRelation(obid, meshid);
				}
			}
		}

#region SHADERS

		/// <summary>
		/// Handle RenderMaterial - will queue new shader if necessary
		/// </summary>
		/// <param name="mat">RenderMaterial instance to handle</param>
		/// <param name="decals">List of CyclesDecal that need to be integrated into the shader</param>
		/// <param name="invisibleUnderside">True if geometry should be see-through from the backface. Used for the groundplane.</param>
		private void HandleRenderMaterial(RenderMaterial mat, uint matId, List<CyclesDecal> decals, bool invisibleUnderside, bool shadowCatcher = false)
		{
			if (_shaderDatabase.HasShader(matId))
			{
				return;
			}

			var sh = _shaderConverter.RecordDataToSetupCyclesShader(mat.TopLevelParent as RenderMaterial, LinearWorkflow, matId, BitmapConverter, decals, _doc_serialnr);
			sh.InvisibleUnderside = invisibleUnderside;
			sh.ShadowCatcher = shadowCatcher;
			_shaderDatabase.AddShader(sh);
		}

		/// <summary>
		/// Change the material on given object
		/// </summary>
		/// <param name="matid">RenderHash of material</param>
		/// <param name="obid">MeshInstanceId</param>
		/// <param name="meshid">mesh id (Guid * meshindex), can be null</param>
		private void HandleMaterialChangeOnObject(uint matid, uint obid, Tuple<Guid, int> meshid)
		{
			var oldhash = _objectShaderDatabase.FindRenderHashForObjectId(obid);
			RcCore.It.AddLogStringIfVerbose($"handle material change on object {oldhash} {matid}");

			HandleShaderChange(obid, oldhash, matid, meshid);
		}


		/// <summary>
		/// Handle changes in materials to create (or re-use) shaders.
		/// </summary>
		/// <param name="mats">List of <c>CQMaterial</c></param>
		protected override void ApplyMaterialChanges(List<CqMaterial> mats)
		{
			// list of material hashes
			var distinctMats = new List<uint>();

			RcCore.It.AddLogStringIfVerbose($"ApplyMaterialChanges: {mats.Count}");
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Apply materials", -1.0f);

			var totalmats = mats.Count;
			var curmat = 0;

			foreach (var mat in mats)
			{
				curmat++;

				_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Apply material {curmat}/{totalmats}", -1.0f);
				RcCore.It.AddLogStringIfVerbose($"\t[material {mat.Id}, {mat.MeshInstanceId}, {mat.MeshIndex}]");
				var rm = MaterialFromId(mat.Id);

				if (!distinctMats.Contains(mat.Id))
				{
					distinctMats.Add(mat.Id);
				}

				var obid = mat.MeshInstanceId;

				// no mesh id here, but shouldn't be necessary either. Passing in null.
				HandleMaterialChangeOnObject(mat.Id, obid, null);
			}

			totalmats = distinctMats.Count;
			curmat = 0;
			// list over material hashes, check if they exist. Create if new
			foreach (var distinct in distinctMats)
			{
				curmat++;
				_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Apply distinct material {curmat}/{totalmats}", -1.0f);
				var existing = _shaderDatabase.GetShaderFromHash(distinct);
				if (existing == null)
				{
					var rm = MaterialFromId(distinct);
					HandleRenderMaterial(rm, distinct, null, false);
				}
			}
		}

		/// <summary>
		/// Upload changes to shaders
		/// </summary>
		public void UploadShaderChanges()
		{
			if (_shaderDatabase?.ShaderChanges.Count > 0)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadShaderChanges entry");
				RcCore.It.AddLogStringIfVerbose($"\t\tUploading shader changes {_shaderDatabase.ShaderChanges.Count}");
				var totalshaders = _shaderDatabase.ShaderChanges.Count;
				var curshader = 0;
				// map shaders. key is RenderHash
				foreach (var shader in _shaderDatabase.ShaderChanges)
				{
					curshader++;
					if (_renderEngine.ShouldBreak) return;

					_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Uploading shader {curshader}/{totalshaders}", -1.0f);

					shader.Gamma = PreProcessGamma;

					// create a cycles shader
					var sh = _renderEngine.CreateMaterialShader(shader);
					_shaderDatabase.RecordRhCclShaderRelation(shader.Id, sh);
					_shaderDatabase.Add(shader, sh);

					sh.Tag();
				}
				_renderEngine.SetProgress(_renderEngine.RenderWindow, "Shaders handled", -1.0f);

				RcCore.It.AddLogStringIfVerbose("\tUploadShaderChanges entry");
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

		private readonly float gp_side_extension = 1.0E+6f;
		private uint currentGpRenderMaterial = 0;
		private bool isGpShadowsOnly = false;

		private RenderMaterial shadowCatcherMaterial {get; set; } = null;

		private const int shadowCatcherMaterialId = 42;
		private void InitialiseGroundPlane(CqGroundPlane gp)
		{
			var materialId = gp.IsShadowOnly ? shadowCatcherMaterialId : gp.MaterialId;
			var mat = gp.IsShadowOnly ? shadowCatcherMaterial : MaterialFromId(materialId);

			/* now adjust mat id with set values since we want this to be a special
			 * ground plane instance
			 */
			materialId = RhinoMath.CRC32(materialId, 42);
			materialId = RhinoMath.CRC32(materialId, gp.ShowUnderside ? 1 : 0);

			var gpid = _groundplaneGuid;
			var altitude = (float)(gp.Enabled ? gp.Altitude : 0.0);
			altitude -= gpJiggleFactor;
			Point3d pp = new Point3d(0.0, 0.0, altitude);
			Plane p = new Plane(pp, Vector3d.ZAxis);
			Plane pmap = new Plane(pp, Vector3d.ZAxis);
			var xext = new Interval(-gp_side_extension, gp_side_extension);
			var yext = new Interval(-gp_side_extension, gp_side_extension);
			var smext = new Interval(0.0, 1.0);
			var m = Rhino.Geometry.Mesh.CreateFromPlane(p, xext, yext, 100, 100);
			m.Weld(0.1);

			Rhino.Geometry.Transform tfm = Rhino.Geometry.Transform.Identity;
			var motion = new Rhino.Geometry.Vector3d(gp.TextureOffset.X, gp.TextureOffset.Y, 0.0);
			var ttrans = Rhino.Geometry.Transform.Translation(motion);
			tfm *= ttrans;
			var rad = Rhino.RhinoMath.ToRadians(gp.TextureRotation);
			var trot = Rhino.Geometry.Transform.Rotation(rad, pp);
			tfm *= trot;
			var texscale = gp.TextureScale;
			var tscale = Rhino.Geometry.Transform.Scale(p, texscale.X, texscale.Y, 1.0);
			tfm *= tscale;
			var texturemapping = TextureMapping.CreatePlaneMapping(pmap, smext, smext, smext);
			if (texturemapping != null)
			{
				m.SetTextureCoordinates(texturemapping, tfm, false);
				m.SetCachedTextureCoordinates(texturemapping, ref tfm);
			}

			isGpShadowsOnly = gp.IsShadowOnly;

			HandleMeshData(gpid.Item1, gpid.Item2, m, null, false, uint.MaxValue, ccl.Transform.Identity());

			HandleRenderMaterial(mat, materialId, null, !gp.ShowUnderside, isGpShadowsOnly);

			var matrenderhash = materialId;
			var cyclesObject = new CyclesObject
			{
				matid = matrenderhash,
				obid = GroundPlaneMeshInstanceId,
				meshid = gpid,
				Transform = _gObTransform.ToCyclesTransform(),
				Visible = gp.Enabled,
				CastShadow = true,
				IsShadowCatcher = isGpShadowsOnly,
				IgnoreCutout = true,
			};

			HandleShaderChange(GroundPlaneMeshInstanceId, currentGpRenderMaterial, matrenderhash, gpid);
			currentGpRenderMaterial = matrenderhash;

			_objectDatabase.AddOrUpdateObject(cyclesObject);
		}

		private uint old_gp_crc = 0;
		private bool old_gp_enabled = false;
		/// <summary>
		/// Handle ground plane changes.
		/// </summary>
		/// <param name="gp"></param>
		protected override void ApplyGroundPlaneChanges(CqGroundPlane gp)
		{
			var gpcrc = gp.Crc;
			if (old_gp_crc == 0 && !gp.Enabled) return;
			if (gpcrc == old_gp_crc && old_gp_enabled == gp.Enabled) return;

			RcCore.It.AddLogStringIfVerbose("ApplyGroundPlaneChanges.");
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Apply groundplane", -1.0f);

			old_gp_crc = gpcrc;
			old_gp_enabled = gp.Enabled;

			InitialiseGroundPlane(gp);
		}

#endregion

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		/// <param name="dynamicObjectTransforms">List of DynamicObject transforms</param>
		protected override void ApplyDynamicObjectTransforms(List<DynamicObjectTransform> dynamicObjectTransforms)
		{
			Parallel.ForEach(dynamicObjectTransforms, dot =>
			{
				var cot = new CyclesObjectTransform(dot.MeshInstanceId, dot.Transform.ToCyclesTransform());
				_objectDatabase.AddDynamicObjectTransform(cot);

			});
			_renderEngine.Flush = true;
		}

#region LIGHT & SUN

		/// <summary>
		/// Upload all light changes to the Cycles render engine
		/// </summary>
		public void UploadLightChanges()
		{
			if(_lightDatabase?.LightsToAdd.Count == 0 && _lightDatabase?.LightsToUpdate.Count == 0) return;

			RcCore.It.AddLogStringIfVerbose("\tUploadLightChanges entry");

			/* new light shaders and lights. */
			foreach (var l in _lightDatabase.LightsToAdd)
			{
				if (_renderEngine.ShouldBreak) return;

				l.Gamma = PreProcessGamma;

				var lgsh = l.Type!=LightType.Background ? _renderEngine.CreateSimpleEmissionShader(l) : _renderEngine.Session.Scene.Background.Shader;

				if (l.Type != LightType.Background)
				{
					_shaderDatabase.Add(l, lgsh);
				}

				if (_renderEngine.ShouldBreak) return;

				var light = new CclLight(_renderEngine.Session, _renderEngine.Session.Scene, lgsh)
				{
					Type = l.Type,
					Size = l.Size,
					Angle = 0.0f,
					Location = l.Co,
					Direction = l.Dir,
					UseMis = l.UseMis,
					CastShadow = l.CastShadow,
					Samples = 1,
					MaxBounces = 8,
					SizeU = l.SizeU,
					SizeV = l.SizeV,
					AxisU = l.AxisU,
					AxisV = l.AxisV,
				};

				switch (l.Type)
				{
					case LightType.Spot:
						light.SpotAngle = l.SpotAngle;
						light.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						light.Size = 0.0f;
						light.Angle = l.Angle;
						break;
					default:
						break;
				}

				light.TagUpdate();
				_lightDatabase.RecordLightRelation(l.Id, light);
			}

			// update existing ones
			foreach (var l in _lightDatabase.LightsToUpdate)
			{
				var existingL = _lightDatabase.ExistingLight(l.Id);
				if(l.Type == LightType.Background)
				{
					existingL.Shader = _renderEngine.Session.Scene.Background.Shader;
				} else
				{
					TriggerLightShaderChanged(l, existingL.Shader);
				}

				existingL.Type = l.Type;
				existingL.Size = l.Size;
				existingL.Angle = l.Angle;
				existingL.Location = l.Co;
				existingL.Direction = l.Dir;
				existingL.UseMis = l.UseMis;
				existingL.CastShadow = l.CastShadow;
				existingL.SpotAngle = l.SpotAngle;
				existingL.SpotSmooth = l.SpotSmooth;
				existingL.Samples = 1;
				existingL.MaxBounces = 8;
				existingL.SizeU = l.SizeU;
				existingL.SizeV = l.SizeV;
				existingL.AxisU = l.AxisU;
				existingL.AxisV = l.AxisV;

				if(l.Type == LightType.Distant) {
						existingL.Samples = (uint)(isGpShadowsOnly ? 1 : 1024);
						break;
				}
				existingL.TagUpdate();
			}
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Lights handled", -1.0f);
			RcCore.It.AddLogStringIfVerbose("\tUploadLightChanges exit");
		}

		private uint LinearLightMaterialCRC(Rhino.Geometry.Light ll)
		{
			uint crc = 0xBABECAFE;

			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.R);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.G);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.B);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Intensity);
			crc = Rhino.RhinoMath.CRC32(crc, ll.ShadowIntensity);
			crc = Rhino.RhinoMath.CRC32(crc, ll.IsEnabled ? 1 : 0);

			return crc;
		}

		private void HandleLightMaterial(Rhino.Geometry.Light rgl, uint matid)
		{
			if (_shaderDatabase.HasShader(matid)) return;

			float sizeterm= rgl.ShadowIntensity > 0.1 ? (float)rgl.ShadowIntensity : 0.1f;

			var emissive = new Materials.EmissiveMaterial();
			Color4f color = new Color4f(rgl.Diffuse);
			emissive.BeginChange(RenderContent.ChangeContexts.Ignore);
			emissive.Name = rgl.Name;
			emissive.Gamma = PreProcessGamma;
			emissive.SetParameter(Materials.EmissiveMaterial._Emissive, color);
			switch(rgl.AttenuationType) {
				case RGLight.Attenuation.Linear:
					emissive.SetParameter(Materials.EmissiveMaterial._Falloff, 1);
					break;
				case RGLight.Attenuation.InverseSquared:
					emissive.SetParameter(Materials.EmissiveMaterial._Falloff, 2);
					break;
				default:
					emissive.SetParameter(Materials.EmissiveMaterial._Falloff, 0);
					break;
			}
			emissive.SetParameter(Materials.EmissiveMaterial._Strength, (float)rgl.Intensity * RcCore.It.AllSettings.LinearLightFactor * (rgl.IsEnabled ? 1 : 0)*sizeterm*sizeterm);
			emissive.EndChange();
			emissive.BakeParameters(BitmapConverter, _doc_serialnr);
			var shader = new CyclesShader(matid, BitmapConverter, _doc_serialnr);
			shader.RecordDataForFrontShader(emissive, PreProcessGamma);
			shader.Type = CyclesShader.Shader.Diffuse;

			_shaderDatabase.AddShader(shader);
		}

		public void UpdateBackgroundLight()
		{
			_lightDatabase.UpdateBackgroundLight();
		}

		private int _enabledLights = 0;
		public void PushEnabledLight()
		{
			_enabledLights++;
		}

		public void PopEnabledLight()
		{
			_enabledLights--;
			if (_enabledLights < 0) _enabledLights = 0;
		}

		public bool AnyActiveLights()
		{
			return _enabledLights > 0;
		}

		/// <summary>
		/// Handle light changes
		/// </summary>
		/// <param name="lightChanges"></param>
		protected override void ApplyLightChanges(List<CqLight> lightChanges)
		{
			// we don't necessarily get view changes prior to light changes, so
			// the old _currentViewInfo could be null - at the end of a Flush
			// it would be thrown away. Hence we now ask the ChangeQueue for the
			// proper view info. It will be given if one constructed the ChangeQueue
			// with a view to force it to be a single-view only ChangeQueue.
			// See #RH-32345 and #RH-32356
			var v = GetQueueView();

			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Apply lights", -1.0f);

			foreach (var light in lightChanges)
			{
				if (light.ChangeType == CqLight.Event.Deleted) PopEnabledLight();
				else if (light.ChangeType == CqLight.Event.Added || light.ChangeType == CqLight.Event.Undeleted) PushEnabledLight();

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
					var cl = _shaderConverter.ConvertLight(this, light, v, PreProcessGamma, _gObTransform);

					_lightDatabase.AddLight(cl);
				}
			}
			_environmentDatabase.TagUpdate();
		}

		private readonly MeshingParameters mp = MeshingParameters.FastRenderMesh;
		//new MeshingParameters(0.1) { MinimumEdgeLength = 0.001, GridMinCount = 16, JaggedSeams = false };
		private readonly MeshingParameters mpclipping = new MeshingParameters(0.1) { MinimumEdgeLength = 0.1, GridMinCount = 8, JaggedSeams = false };

		private void HandleLinearLightAddOrModify(uint lightmeshinstanceid, RGLight ld)
		{
			float sizeterm= 1.0f - (float)ld.ShadowIntensity;
			float size = 1.0f + sizeterm*sizeterm*sizeterm * 100.0f; // / 100.f;

			var p = new Plane(ld.Location, ld.Direction);
			var circle = new Circle(p, ld.Width.Length*0.5*size);
			var c = Surface.CreateExtrusion(circle.ToNurbsCurve(), ld.Direction);
			//var c = new Cylinder(circle, ld.Direction.Length);
			var mesh = new Rhino.Geometry.Mesh();
			if (c.IsValid)
			{
				var m = Rhino.Geometry.Mesh.CreateFromBrep(c.ToBrep(), mp);
				foreach (var im in m) mesh.Append(im);
				mesh.RebuildNormals();
			}
			else
			{
				mesh.Vertices.Add(new Rhino.Geometry.Point3d(0.01, 0.01, 0.01));
				mesh.Vertices.Add(new Rhino.Geometry.Point3d(0.01, 0.01, -0.01));
				mesh.Vertices.Add(new Rhino.Geometry.Point3d(0.01, -0.01, 0.01));

				mesh.Translate(new Vector3d(ld.Location));

				mesh.Faces.AddFace(0, 1, 2);

				mesh.FaceNormals.ComputeFaceNormals();
				mesh.Normals.ComputeNormals();
			}
			var t = ccl.Transform.Identity();

			var ldid = new Tuple<Guid, int>(ld.Id, 0);

			var matid = LinearLightMaterialCRC(ld);

			HandleLightMaterial(ld, matid);

			HandleMeshData(ld.Id, 0, mesh, null, false, matid, t);


			var lightObject = new CyclesObject
			{
				matid = matid,
				obid = lightmeshinstanceid,
				meshid = ldid,
				Transform = t,
				Visible = c.IsValid ? ld.IsEnabled : false,
				CastShadow = false,
				IsShadowCatcher = false,
				CastNoShadow = ld.ShadowIntensity < 0.05,
				IgnoreCutout = true,
			};

			_objectDatabase.AddOrUpdateObject(lightObject);
			HandleMaterialChangeOnObject(matid, lightmeshinstanceid, ldid);
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
					var cl = _shaderConverter.ConvertLight(light, PreProcessGamma, _gObTransform);
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
			var cl = _shaderConverter.ConvertLight(sun, PreProcessGamma, _gObTransform);
			cl.Id = _sunGuid;
			_lightDatabase.AddLight(cl);
			if (sun.IsEnabled) PushEnabledLight();
			else PopEnabledLight();
			_environmentDatabase.TagUpdate();
		}

#endregion

		public void UploadEnvironmentChanges()
		{
			if (_environmentDatabase?.BackgroundHasChanged ?? false)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadEnvironmentChanges entry");
				_environmentDatabase.CyclesShader.EnabledLights = AnyActiveLights();
				RcCore.It.AddLogStringIfVerbose($"\t\tUploading background changes, active lights: {_environmentDatabase.CyclesShader.EnabledLights}, sky {_environmentDatabase.CyclesShader.SkylightEnabled} skystrength {_environmentDatabase.CyclesShader.SkyStrength}");
				_renderEngine.SetProgress(_renderEngine.RenderWindow, "Handling Environment changes", -1.0f);
				_renderEngine.RecreateBackgroundShader(_environmentDatabase.CyclesShader);
				RcCore.It.AddLogStringIfVerbose("\tUploadEnvironmentChanges exit");
			}
		}

		private void HandleGroundPlaneShadowcatcherState(CyclesObject ob)
		{
			if (ob == null) return;
			if(ob.obid == GroundPlaneMeshInstanceId)
			{
				CSycles.film_set_use_approximate_shadow_catcher(_renderEngine.Session.Id, ob.IsShadowCatcher);
			}
		}

		/// <summary>
		/// Upload object changes
		/// </summary>
		public void UploadObjectChanges()
		{
			if(_objectDatabase?.DeletedObjects.Count == 0 && _objectDatabase?.NewOrUpdatedObjects.Count == 0) return;

			RcCore.It.AddLogStringIfVerbose("\tUploadObjectChanges entry");

			// first delete objects
			foreach (var ob in _objectDatabase.DeletedObjects)
			{
				HandleGroundPlaneShadowcatcherState(ob);
				if (ob.cob != null)
				{
					RcCore.It.AddLogStringIfVerbose($"\t\tUploadObjectChanges: deleting object {ob.obid} {ob.cob.ObjectPtr}");
					var cob = ob.cob;
					// deleting we do (for now?) by marking object as hidden.
					// we *don't* clear mesh data here, since that very mesh
					// may be used elsewhere.
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			RcCore.It.AddLogStringIfVerbose($"\t\tUploadObjectChanges: adding/modifying objects {_objectDatabase.NewOrUpdatedObjects.Count}");

			var totalobcount = _objectDatabase.NewOrUpdatedObjects.Count;
			var curcount = 0;

			// now combine objects and meshes, creating new objects when necessary
			foreach (var ob in _objectDatabase.NewOrUpdatedObjects)
			{
				HandleGroundPlaneShadowcatcherState(ob);
				curcount++;
				_renderEngine.SetProgress(_renderEngine.RenderWindow, $"handling object {curcount}/{totalobcount}", -1.0f);
				// mesh for this object id
				var mesh = _objectDatabase.FindMeshRelation(ob.meshid);

				// hmm, no mesh. Oh well, lets get on with the next
				if (mesh == null) continue;


				ccl.Transform t = _objectDatabase.MeshOcsFrames.ContainsKey(mesh.GeometryPointer) ? _objectDatabase.MeshOcsFrames[mesh.GeometryPointer] : ccl.Transform.Identity();

				if(false && _gObTransform != Rhino.Geometry.Transform.Identity) {
					var rt = t.ToRhinoTransform();
					if(rt.TryGetInverse(out Rhino.Geometry.Transform rtinv))
					{
						rt = _gObTransform * rtinv;
					}

					rt = _gObTransform * rt;
					t = rt.ToCyclesTransform();
				}

				// see if we already have an object here.
				// update it, otherwise create new one
				var cob = _objectDatabase.FindObjectRelation(ob.obid);

				var newcob = cob == null;

				// new object, so lets create it and record necessary stuff about it
				if (newcob)
				{
					cob = new CclObject(_renderEngine.Session);
					_objectDatabase.RecordObjectRelation(ob.obid, cob);
					_objectDatabase.RecordObjectIdMeshIdRelation(ob.obid, ob.meshid);
				}

				RcCore.It.AddLogStringIfVerbose($"\t\tadding/modifying object {ob.obid} {ob.meshid} (ptr: {cob.ObjectPtr})");

				// set mesh reference and other stuff
				cob.Mesh = mesh;
				cob.RandomId = ob.obid;
				cob.PassId = ob.passobid;
				cob.Transform = ob.Transform;
				cob.OcsFrame = t;
				cob.IsShadowCatcher = ob.IsShadowCatcher;
				//cob.IsBlockInstance = true;
				var norefl = PathRay.AllVisibility & ~PathRay.Reflect;
				var vis = ob.Visible ? (ob.IsShadowCatcher ? norefl: PathRay.AllVisibility): PathRay.Hidden;
				if (ob.CastShadow == false)
				{
					vis &= ~PathRay.Shadow;
				}
				cob.MeshLightNoCastShadow = ob.CastNoShadow;
				cob.Visibility = vis;

				Shader shader = _shaderDatabase.GetShaderFromHash(ob.matid);
				cob.Shader = shader.Id;
				//cob.Cutout = false;
				cob.TagUpdate();
			}
			_renderEngine.SetProgress(_renderEngine.RenderWindow, "Objects handled", -1.0f);
			RcCore.It.AddLogStringIfVerbose("\tUploadObjectChanges exit");
		}

		/// <summary>
		/// Handle skylight changes
		/// </summary>
		/// <param name="skylight">New skylight information</param>
		protected override void ApplySkylightChanges(CqSkylight skylight)
		{
			_environmentDatabase.SetSkylightEnabled(skylight.Enabled);
			_environmentDatabase.SetGamma(PreProcessGamma);
			_lightDatabase.UpdateBackgroundLight();
		}


		private bool _previousScaleBackgroundToFit = false;
		private bool _wallpaperInitialized = false;
		private IntegratorSettings integratorSettings { get; set; } = null;
		private uint _oldIntegratorHash { get; set; } = 0;
		private bool _integratorChanged { get; set; } = false;
		protected override void ApplyRenderSettingsChanges(RenderSettings rs)
		{
			if (rs != null)
			{
				EngineDocumentSettings eds = new EngineDocumentSettings(rs.UserDictionary);
				if(eds.IntegratorHash!=_oldIntegratorHash)
				{
					integratorSettings = new IntegratorSettings(eds);
					_oldIntegratorHash = eds.IntegratorHash;
					_integratorChanged = true;
				}
				var trbg = TransparentBackground;
				TransparentBackground = rs.TransparentBackground;
				DisplayPipelineAttributesChanged |= trbg != TransparentBackground;
				_environmentDatabase.SetGamma(PreProcessGamma);
				_environmentDatabase.SetBackgroundData(rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				if (rs.BackgroundStyle == BackgroundStyle.WallpaperImage)
				{
					var view = GetQueueView();
					var y = string.IsNullOrEmpty(view.WallpaperFilename);
					RcCore.It.AddLogStringIfVerbose(
						$"\t\tview has {(y ? "no" : "")} wallpaper {(y ? "" : "with filename ")} {(y ? "" : view.WallpaperFilename)} {(y ? "" : "its grayscale bool")} {(y ? "" : $"{view.ShowWallpaperInGrayScale}")} {(y ? "" : "its hidden bool")} {(y ? "" : $"{view.WallpaperHidden}")}");
					_environmentDatabase.BackgroundWallpaper(view, rs.ScaleBackgroundToFit);
					_wallpaperInitialized = true;
				}
				_previousScaleBackgroundToFit = rs.ScaleBackgroundToFit;
				_lightDatabase.UpdateBackgroundLight();
			}
		}

		public bool UploadIntegratorChanges()
		{
			if(_integratorChanged)
			{
				if(_renderEngine is RenderEngines.ViewportRenderEngine vpe)
				{
					RcCore.It.AddLogStringIfVerbose("\tUploadIntegratorChanges entry");
					vpe.ChangeIntegrator(integratorSettings);
					vpe.RenderedViewport?.UpdateMaxSamples(integratorSettings.Samples);
					vpe.ChangeSamples(integratorSettings.Samples);
					integratorSettings = null;
					_integratorChanged = false;
					RcCore.It.AddLogStringIfVerbose("\tUploadIntegratorChanges exit");
				}
			}
			return true;
		}

		public bool DisplayPipelineAttributesChanged { get; private set; } = false;
		public int RealtimePreviewPasses { get; private set; } = -1;
		public bool TransparentBackground { get; private set; } = false;
		protected override void ApplyDisplayPipelineAttributesChanges(DisplayPipelineAttributes displayPipelineAttributes)
		{
			if (displayPipelineAttributes.ShowRealtimeRenderProgressBar)
			{
				DisplayPipelineAttributesChanged = true;
				RealtimePreviewPasses = displayPipelineAttributes.RealtimeRenderPasses;
				Rhino.RhinoApp.OutputDebugString($"{displayPipelineAttributes.ShowRealtimeRenderProgressBar} {displayPipelineAttributes.RealtimeRenderPasses}\n");
			}
			bool trbg = TransparentBackground;
			TransparentBackground = displayPipelineAttributes.FillMode == DisplayPipelineAttributes.FrameBufferFillMode.Transparent;
			DisplayPipelineAttributesChanged |= trbg != TransparentBackground;
		}

		public bool UploadDisplayPipelineAttributesChanges()
		{
			if(DisplayPipelineAttributesChanged)
			{
				RcCore.It.AddLogStringIfVerbose("\tUploadDataDisplayPipelineAttributesChanges entry");
				if (RealtimePreviewPasses>-1 && _renderEngine is RenderEngines.ModalRenderEngine mre)
				{
					mre.MaxSamples = RealtimePreviewPasses;
				}
				_renderEngine.Session.Scene.Background.Transparent = TransparentBackground;
				RcCore.It.AddLogStringIfVerbose("\tUploadDataDisplayPipelineAttributesChanges exit");
			}
			return true;
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
			RcCore.It.AddLogStringIfVerbose($"ApplyEnvironmentChanges {usage}");
			_renderEngine.SetProgress(_renderEngine.RenderWindow, $"Apply environment {usage}", -1.0f);
			_environmentDatabase.SetGamma(PreProcessGamma);
			UpdateAllEnvironments(usage);
		}

		private void UpdateAllEnvironments(RenderEnvironment.Usage usage)
		{
			switch (usage)
			{
				case RenderEnvironment.Usage.Background:
					var bgenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Background);
					var bgenv = EnvironmentForid(bgenvId);
					_environmentDatabase.SetBackground(bgenv, RenderEnvironment.Usage.Background);
					break;
				case RenderEnvironment.Usage.Skylighting:
					var skyenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Skylighting);
					var skyenv = EnvironmentForid(skyenvId);
					_environmentDatabase.SetBackground(skyenv, RenderEnvironment.Usage.Skylighting);
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					var reflenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.ReflectionAndRefraction);
					var reflenv = EnvironmentForid(reflenvId);

					_environmentDatabase.SetBackground(reflenv, RenderEnvironment.Usage.ReflectionAndRefraction);
					break;
			}

			_environmentDatabase.HandleEnvironments(usage);
			_lightDatabase.UpdateBackgroundLight();
		}

		private static int _updateCounter = 0;

		/// <summary>
		/// We get notified of (dynamic?) changes.
		/// </summary>
		protected override void NotifyBeginUpdates()
		{
			RcCore.It.AddLogStringIfVerbose($"NotifyBeginUpdates {++_updateCounter}");
			_renderEngine.TriggerBeginChangesNotified();
		}

		/// <summary>
		/// Changes have been signalled.
		/// </summary>
		protected override void NotifyEndUpdates()
		{
			RcCore.It.AddLogStringIfVerbose($"NotifyEndUpdates {_updateCounter}");
			_renderEngine.Flush = true;
		}

		protected override void NotifyDynamicUpdatesAreAvailable()
		{
			RcCore.It.AddLogStringIfVerbose("NotifyDynamicUpdatesAreAvailable");
			_renderEngine.TriggerBeginChangesNotified();
		}

		/// <summary>
		/// Tell ChangeQueue we want baking for
		/// - ProceduralTextures
		/// - CustomObjectMappings
		/// </summary>
		/// <returns></returns>
		protected override BakingFunctions BakeFor()
		{
			if(_renderEngine._textureBakeQuality == 4) { // Disable
				return BakingFunctions.None;
			}
			return BakingFunctions.ProceduralTextures;
		}

		protected override int BakingSize(RhinoObject ro, RenderMaterial material, TextureType type)
		{
			switch(_renderEngine._textureBakeQuality) {
				case 1:
					return 2048*2;
				case 2:
					return 2048*4;
				case 3:
					return 2048*8;
				case 4:
					return 2; // Disabled, give some value other than 0 in case we get here
				default:
					return 2048;
			}
		}

		protected override bool ProvideOriginalObject()
		{
			return true;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("ChangeDatabase:");
			var props = typeof(ChangeDatabase).GetProperties();
			foreach(var prop in props) {
				sb.Append($"\t{prop.Name} := {prop.GetValue(this)}\n");
			}
			return sb.ToString();
		}
	}
}
