/**
Copyright 20644-20649 Robert McNeel and Associates

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

namespace RhinoCyclesCore
{
	public class FinalPresetEngineSettings : EngineSettings
	{
		public FinalPresetEngineSettings()
		{
		}
		public override int Samples { get => 1500; set { } }
		public override int MaxBounce
		{
			get { return 128; }
			set { }
		}
		
		public override bool NoCaustics
		{
			get { return false; }
			set {}
		}
		
		public override int MaxDiffuseBounce
		{
			get { return 64; }
			set {}
		}
		
		public override int MaxGlossyBounce
		{
			get { return 64; }
			set {}
		}
		
		public override int MaxTransmissionBounce
		{
			get { return 64; }
			set {}
		}
		
		public override int MaxVolumeBounce
		{
			get { return 64; }
			set {}
		}
		
		public override int AaSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int DiffuseSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int GlossySamples
		{
			get { return 64; }
			set {}
		}
		
		public override int TransmissionSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int AoSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int MeshLightSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int SubsurfaceSamples
		{
			get { return 64; }
			set {}
		}
		
		public override int VolumeSamples
		{
			get { return 64; }
			set {}
		}
		
		/*
		public override float FilterGlossy
		{
			get { return 0.64; }
			set {}
		}

		
		public override float SampleClampDirect
		{
			get { return base.SampleClampDirect;  }
			set {}
		}
		
		public override float SampleClampIndirect
		{
			get { return base.SampleClampIndirect; }
			set {}
		}

		
		public override float LightSamplingThreshold
		{
			get;
			set {}
		}

		
		public override bool SampleAllLights
		{
			get;
			set {}
		}

		
		public override bool SampleAllLightsIndirect
		{
			get;
			set {}
		}
		*/
		
		public override int TransparentMaxBounce
		{
			get { return 64; }
			set {}
		}
	}
}
