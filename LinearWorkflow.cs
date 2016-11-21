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
//using LwFlow = Rhino.Render.ChangeQueue.LinearWorkflow;
namespace RhinoCyclesCore
{
  /*
	/// <summary>
	/// Intermediate class to convey linear workflow settings:
	/// gamma, reciprocal gamma, active.
	/// </summary>
	public class LinearWorkflow
	{
		public bool Active { get; set; }
		public float Gamma { get; set; }
		public float GammaReciprocal { get; set; }
		/// <summary>
		/// Copy constructor from <see cref="Rhino.Render.ChangeQueue.LinearWorkflow"/>
		/// </summary>
		/// <param name="lwf"><see cref="Rhino.Render.ChangeQueue.LinearWorkflow"/></param>
		public LinearWorkflow(LwFlow lwf)
		{
			Active = lwf.Active;
			Gamma = lwf.Gamma;
			GammaReciprocal = lwf.GammaReciprocal;
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="active"></param>
		/// <param name="gamma"></param>
		public LinearWorkflow(bool active, float gamma)
		{
			Active = active;
			Gamma = gamma;
			GammaReciprocal = 1.0f/gamma;
		}

		/// <summary>
		/// Copy constructor from <see cref="LinearWorkflow"/>
		/// </summary>
		/// <param name="old"><see cref="LinearWorkflow"/></param>
		public LinearWorkflow(LinearWorkflow old)
		{
			Active = old.Active;
			Gamma = old.Gamma;
			GammaReciprocal = old.GammaReciprocal;
		}

		public override bool Equals(object obj)
		{
			LinearWorkflow lwf = obj as LinearWorkflow;

			if (lwf == null) return false;

			return Active == lwf.Active && Math.Abs(Gamma-lwf.Gamma) < float.Epsilon && Math.Abs(GammaReciprocal - lwf.GammaReciprocal) < float.Epsilon;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return $"Core.LinearWorkflow {Active} {Gamma} {GammaReciprocal}";
		}
	}
  */
}
