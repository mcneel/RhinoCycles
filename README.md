RhinoCycles
===========

The Cycles integration for Rhinoceros 3D: the Raytraced viewport and Rhino Render.

| Project | What |
| --- | --- |
| `RhinoCycles.csproj` | the RhinoCycles plug-in (`RhinoCycles.rhp`) and its commands |
| `RhinoCyclesCore.csproj` | render engines, settings, scene and material conversion; deploys the Cycles payload from `big_libs` into the build output |
| `RhinoRenderCycles/` | the Rhino Render plug-in (`RhinoRenderCycles.rhp`) |
| `RhinoCyclesKernelCompiler/` | separate process that prepares GPU kernels for the devices present at plug-in load |

Cycles itself, its C API (ccycles) and the C# wrapper (csycles) are in the sibling
repository `../cycles-core`. Building, the payload and known issues are documented
there: [README](../cycles-core/README.md), [BUILDING](../cycles-core/BUILDING.md),
[KNOWN-ISSUES](../cycles-core/KNOWN-ISSUES.md).
