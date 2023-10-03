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
using System.Drawing;
using Rhino;
using Rhino.Render;
using Rhino.UI;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.RenderEngines;

namespace RhinoCycles
{
	/// <summary>
	/// For asynchronous renderer this is just the entry point.
	///
	/// Inherits Rhino.Render.RenderPipeline
	/// <seealso cref="Rhino.Render.RenderPipeline"/>
	/// </summary>
	public class RenderPipeline : Rhino.Render.RenderPipeline
	{
		private bool m_bStopFlag;
		/// <summary>
		/// Context that contains the actual renderer instance
		/// </summary>
		readonly private ModalRenderEngine cyclesEngine;

		public RenderPipeline(RhinoDoc doc, Rhino.Commands.RunMode mode, Rhino.PlugIns.RenderPlugIn plugin, Size rwSize, ModalRenderEngine aRC)
			: base(
					doc,
					mode,
					plugin,
					rwSize,
					String.Format(Localization.LocalizeString("Rhino Render on {0}", 40),
									 $"{RcCore.It.IsDeviceReady(RcCore.It.AllSettings.RenderDevice).actualDevice.NiceName}{(RcCore.It.IsDeviceReady(RcCore.It.AllSettings.RenderDevice).isDeviceReady ? "" : " - OpenCL compiling")}"),
					Rhino.Render.RenderWindow.StandardChannels.RGBA,
					false,
					false)
		{
			cyclesEngine = aRC;
		}

		public bool Cancel()
		{
			return m_bStopFlag;
		}

		protected override bool OnRenderBegin()
		{
			m_bStopFlag = false;
			return cyclesEngine.StartRenderThread(cyclesEngine.Renderer, "A cool Cycles modal rendering thread");
		}

		protected override bool OnRenderBeginQuiet(Size imageSize)
		{
			return OnRenderBegin();
		}

		protected override void OnRenderEnd(RenderEndEventArgs e)
		{
			cyclesEngine.StopRendering();
		}

		protected override bool ContinueModal()
		{
			return !cyclesEngine.ShouldBreak;
		}

		protected override bool OnRenderWindowBegin(Rhino.Display.RhinoView view, System.Drawing.Rectangle rect)
		{
			m_bStopFlag = false;
			return cyclesEngine.StartRenderThread(cyclesEngine.Renderer, "A cool Cycles modal rendering thread | RenderWindowBegin");
		}

		public override bool SupportsPause()
		{
			return cyclesEngine.SupportsPause();
		}

		public override void PauseRendering()
		{
			cyclesEngine.PauseRendering();
		}

		public override void ResumeRendering()
		{
			cyclesEngine.ResumeRendering();
		}

		protected override void Dispose(bool isDisposing)
		{
			cyclesEngine.Dispose();
			base.Dispose(isDisposing);
		}
	}
}
