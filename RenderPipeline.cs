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
using System.Drawing;
using RhinoCyclesCore;
using Rhino;
using Rhino.Render;
using System.Threading;

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

		public RenderPipeline(RhinoDoc doc, Rhino.Commands.RunMode mode, Rhino.PlugIns.RenderPlugIn plugin, ModalRenderEngine aRC)
			: base(doc, mode, plugin, RenderSize(doc),
					"RhinoCycles", Rhino.Render.RenderWindow.StandardChannels.RGBA, false, false)
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
			return !cyclesEngine.CancelRender;
		}

		protected override bool OnRenderWindowBegin(Rhino.Display.RhinoView view, System.Drawing.Rectangle rect) { return false; }
	}
}
