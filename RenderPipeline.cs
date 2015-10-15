using System.Drawing;
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
	class RenderPipeline : Rhino.Render.RenderPipeline
	{
		private bool m_bStopFlag;
		/// <summary>
		/// Context that contains the actual renderer instance
		/// </summary>
		readonly private RenderEngine cyclesEngine;

		public RenderPipeline(RhinoDoc doc, Rhino.Commands.RunMode mode, Rhino.PlugIns.RenderPlugIn plugin, ref AsyncRenderContext aRC)
			: base(doc, mode, plugin, RenderSize(doc),
					"RhinoCycles", Rhino.Render.RenderWindow.StandardChannels.RGBA, false, false, ref aRC)
		{
			cyclesEngine = (RenderEngine)aRC;
		}

		public bool Cancel()
		{
			return m_bStopFlag;
		}

		protected override bool OnRenderBegin()
		{
			m_bStopFlag = false;
			cyclesEngine.RenderThread = new Thread(RenderEngine.ModalRenderer)
			{
				Name = "A cool Cycles rendering thread"
			};
			cyclesEngine.RenderThread.Start(cyclesEngine);
			return true;
		}

		protected override bool OnRenderBeginQuiet(Size imageSize)
		{
			m_bStopFlag = false;
			cyclesEngine.RenderThread = new Thread(RenderEngine.ModalRenderer)
			{
				Name = "A quiet, cool Cycles rendering thread"
			};
			cyclesEngine.RenderThread.Start(cyclesEngine);
			return true;
		}

		protected override void OnRenderEnd(RenderEndEventArgs e)
		{
			// unused
		}

		protected override bool ContinueModal()
		{
			return true;
		}

		protected override bool OnRenderWindowBegin(Rhino.Display.RhinoView view, System.Drawing.Rectangle rect) { return false; }
	}
}
