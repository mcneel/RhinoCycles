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

namespace RhinoCyclesCore
{
	public class UploadProgressEventArgs : EventArgs
	{
		public float Progress { get; private set; }
		public string Message { get; private set; }
		public UploadProgressEventArgs(float progress, string message)
		{
			Progress = progress;
			Message = message;
		}
	}
	partial class RenderEngine
	{
		public event EventHandler<UploadProgressEventArgs> UploadProgress;
		/// <summary>
		/// Main entry point for uploading data to Cycles.
		/// </summary>
		protected bool UploadData()
		{
			if (CancelRender) return false;

			Database.UploadDisplayPipelineAttributesChanges();

			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.1f, "Start data upload"));

			if (CancelRender) return false;

			Database.UploadClippingPlaneChanges();

			// linear workflow & gamma changes
			Database.UploadGammaChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.2f, "Linear workflow (gamma changes) uploaded"));

			if (CancelRender) return false;

			// environment changes
			Database.UploadEnvironmentChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.3f, "Environments uploaded"));

			if (CancelRender) return false;

			// transforms on objects, no geometry changes
			Database.UploadDynamicObjectTransforms();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.4f, "Dynamic object transforms uploaded"));

			if (CancelRender) return false;

			// viewport changes
			Database.UploadCameraChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.5f, "Viewport uploaded"));

			if (CancelRender) return false;

			// new shaders we've got
			Database.UploadShaderChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.6f, "Shaders uploaded"));

			if (CancelRender) return false;

			// mesh changes (new ones, updated ones)
			Database.UploadMeshChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.8f, "Mesh data uploaded"));

			if (CancelRender) return false;

			// light changes
			Database.UploadLightChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.7f, "Lights uploaded"));

			if (CancelRender) return false;

			// object changes (new ones, deleted ones)
			Database.UploadObjectChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(1.0f, "Object changes uploaded"));

			if (CancelRender) return false;

			// shader changes on objects (replacement)
			Database.UploadObjectShaderChanges();
			UploadProgress?.Invoke(this, new UploadProgressEventArgs(0.9f, "Shader assignments uploaded"));

			if (CancelRender) return false;

			// done, now clear out our change queue stuff so we're ready for the next time around :)
			Database.ResetChangeQueue();

			if (CancelRender) return false;
			return true;
		}
	}
}
