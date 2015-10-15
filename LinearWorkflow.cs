using System;
using LwFlow = Rhino.Render.ChangeQueue.LinearWorkflow;
namespace RhinoCycles
{
	public class LinearWorkflow
	{
		public bool Active { get; set; }
		public float Gamma { get; set; }
		public float GammaReciprocal { get; set; }
		public LinearWorkflow(LwFlow lwf)
		{
			Active = lwf.Active;
			Gamma = lwf.Gamma;
			GammaReciprocal = lwf.GammaReciprocal;
		}

		public override bool Equals(object obj)
		{
			LinearWorkflow lwf = obj as LinearWorkflow;

			if (lwf == null) return false;

			return Active == lwf.Active && Math.Abs(Gamma-lwf.Gamma) < float.Epsilon && Math.Abs(GammaReciprocal - lwf.GammaReciprocal) < float.Epsilon;
		}
	}
}
