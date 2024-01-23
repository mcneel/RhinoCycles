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

using System.Reflection;
using System.Runtime.InteropServices;
using Rhino.PlugIns;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Rhino Render")]
[assembly: AssemblyDescription("RhinoRenderCycles")]
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
[assembly: AssemblyCompany("Robert McNeel and Associates")]
[assembly: AssemblyProduct("RhinoRender2020")]
[assembly: AssemblyCopyright("Copyright ©  2016-2019")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Plug-in Description Attributes - all of these are optional
[assembly: PlugInDescription(DescriptionType.Address, "-")]
[assembly: PlugInDescription(DescriptionType.Country, "Finland")]
[assembly: PlugInDescription(DescriptionType.Email, "nathan@mcneel.com")]
[assembly: PlugInDescription(DescriptionType.Phone, "-")]
[assembly: PlugInDescription(DescriptionType.Fax, "-")]
[assembly: PlugInDescription(DescriptionType.Organization, "McNeel")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "https://www.rhino3d.com")]
[assembly: PlugInDescription(DescriptionType.WebSite, "https://www.rhino3d.com")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("4F793AD6-60CE-4AAF-8A7E-6E36C752486C")]
