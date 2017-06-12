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
using ccl;
using CclShader = ccl.Shader;
using ccl.ShaderNodes;
using RhinoCyclesCore.Shaders;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{
		internal CclShader CreateMaterialShader(CyclesShader shader)
		{
			CclShader sh = null;
			if (shader.DisplayMaterial && shader.ValidDisplayMaterial)
			{
				sh = CreateCyclesShaderFromRhinoV6BasicMat(shader);
			}
			else
			{
				switch (shader.Front.CyclesMaterialType)
				{
					case ShaderBody.CyclesMaterial.Xml:
					case ShaderBody.CyclesMaterial.FlakedCarPaint:
						sh = CreateCyclesShaderFromXml(shader.Front);
						break;
					default:
						sh = CreateCyclesShaderFromRhinoV6BasicMat(shader);
						break;
				}
			}

			return sh;
		}

		internal CclShader RecreateMaterialShader(CyclesShader shader, CclShader existing)
		{
			CclShader sh = null;
			if (shader.DisplayMaterial && shader.ValidDisplayMaterial)
			{
				sh = RecreateCyclesShaderFromRhinoV6BasicMat(shader, existing);
			}
			else
			{
				switch (shader.Front.CyclesMaterialType)
				{
					case ShaderBody.CyclesMaterial.Xml:
					case ShaderBody.CyclesMaterial.FlakedCarPaint:
						sh = RecreateCyclesShaderFromXml(shader.Front, existing);
						break;
					default:
						sh = RecreateCyclesShaderFromRhinoV6BasicMat(shader, existing);
						break;
				}
			}

			return sh;
		}

		internal CclShader CreateCyclesShaderFromXml(ShaderBody shader)
		{
			var sh = new CclShader(Client, CclShader.ShaderType.Material)
			{
				UseMis = true,
				UseTransparentShadow = true,
				HeterogeneousVolume = false,
				Name = shader.Name ?? $"V6 Basic Material {shader.Id}"
			};

			CclShader.ShaderFromXml(ref sh, shader.Crm.MaterialXml);

			return sh;
		}

		internal CclShader RecreateCyclesShaderFromXml(ShaderBody shader, CclShader existing)
		{
			existing.Recreate();
			CclShader.ShaderFromXml(ref existing, shader.Crm.MaterialXml);
			return existing;
		}

		internal static void SetTextureImage(ImageTextureNode imnode, CyclesTextureImage texture)
		{
			if (texture.HasTextureImage)
			{
				if (texture.HasByteImage)
				{
					imnode.ByteImage = texture.TexByte;
				}
				else if (texture.HasFloatImage)
				{
					imnode.FloatImage = texture.TexFloat;
				}
				imnode.Filename = texture.Name;
				imnode.Width = (uint) texture.TexWidth;
				imnode.Height = (uint) texture.TexHeight;
			}
		}

		internal static void SetTextureImage(EnvironmentTextureNode envnode, CyclesTextureImage texture)
		{
			if (texture.HasTextureImage)
			{
				if (texture.HasByteImage)
				{
					envnode.ByteImage = texture.TexByte;
				}
				else if (texture.HasFloatImage)
				{
					envnode.FloatImage = texture.TexFloat;
					envnode.Interpolation = InterpolationType.Cubic;
				}
				envnode.Filename = texture.Name;
				envnode.Width = (uint) texture.TexWidth;
				envnode.Height = (uint) texture.TexHeight;
			}
		}

		internal CclShader CreateCyclesShaderFromRhinoV6BasicMat(CyclesShader shader)
		{
			var v6 = RhinoShader.CreateRhinoMaterialShader(Client, shader);

			return v6.GetShader();
		}

		internal CclShader RecreateCyclesShaderFromRhinoV6BasicMat(CyclesShader shader, CclShader existing)
		{
			var v6 = RhinoShader.RecreateRhinoMaterialShader(Client, shader, existing);

			return v6.GetShader();
		}

		internal void RecreateBackgroundShader(CyclesBackground background)
		{
			var bg = Session.Scene.Background.Shader;
			var rhinobg = RhinoShader.CreateRhinoBackgroundShader(Client, background, bg);
			Session.Scene.Background.Shader = rhinobg.GetShader();
		}

		internal CclShader CreateSimpleEmissionShader(CyclesLight light)
		{
			var rhinolight = RhinoShader.CreateRhinoLightShader(Client, light, null);

			return rhinolight.GetShader();
		}

		internal CclShader ReCreateSimpleEmissionShader(CyclesLight light, CclShader emission_shader)
		{
			var rhinolight = RhinoShader.CreateRhinoLightShader(Client, light, emission_shader);

			return rhinolight.GetShader();
		}
	}
}
