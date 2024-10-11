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

namespace RhinoCyclesCore.Settings
{
	/// <summary>
	/// Integrator settings class to transport from GUI to RenderedViewport. Only those
	/// that can be changed.
	/// </summary>
	public class IntegratorSettings
	{
		EngineDocumentSettings m_eds;

		public IntegratorSettings(EngineDocumentSettings eds)
		{
			m_eds = eds;
		}

		public int Seed
		{
			get { return m_eds.Seed; }
			set { m_eds.Seed = value; }
		}
		public int Samples
		{
			get { return m_eds.Samples; }
			set { m_eds.Samples = value; }
		}
		public int DiffuseSamples
		{
			get { return m_eds.DiffuseSamples; }
			set { m_eds.DiffuseSamples = value; }
		}
		public int GlossySamples
		{
			get { return m_eds.GlossySamples; }
			set { m_eds.GlossySamples = value; }
		}
		public int MaxBounce
		{
			get { return m_eds.MaxBounce; }
			set { m_eds.MaxBounce = value; }
		}
		public int MaxDiffuseBounce
		{
			get { return m_eds.MaxDiffuseBounce; }
			set { m_eds.MaxDiffuseBounce = value; }
		}
		public int MaxGlossyBounce
		{
			get { return m_eds.MaxGlossyBounce; }
			set { m_eds.MaxGlossyBounce = value; }
		}
		public int MaxTransmissionBounce
		{
			get { return m_eds.MaxTransmissionBounce; }
			set { m_eds.MaxTransmissionBounce = value; }
		}
		public int MaxVolumeBounce
		{
			get { return m_eds.MaxVolumeBounce; }
			set { m_eds.MaxVolumeBounce = value; }
		}
		public int MaxTransparentBounce
		{
			get { return m_eds.TransparentMaxBounce; }
			set { m_eds.TransparentMaxBounce = value; }
		}

		public bool UseAdaptiveSampling
		{
			get { return m_eds.UseAdaptiveSampling; }
			set { }
		}

		public int AdaptiveMinSamples
		{
			get { return m_eds.AdaptiveMinSamples; }
			set { }
		}

		public float AdaptiveThreshold
		{
			get { return m_eds.AdaptiveThreshold; }
			set { }
		}
	}
}
