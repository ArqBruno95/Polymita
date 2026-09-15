# Polymita 0.8.5 source

Build with `build.ps1` on Windows with Rhino 8 installed, or use `Polymita.csproj` with the .NET Framework 4.8 targeting pack. The source uses C# 5. Rhino and Grasshopper SDK assemblies are referenced locally and are not redistributed.

```powershell
.\build.ps1
dotnet build Polymita.csproj -c Release -o dist
```

The optional `RhinoNuGetVersion` MSBuild property supports a machine without a local Rhino installation. Set it to the exact Rhino 8 SDK version you need.

`release/Polymita.gha` is the packaged binary. `dist/` and `test-output/` are local build outputs. Harmony 2.3.3 for net48, net7.0 and net8.0 is embedded during compilation. McNeel SDK assemblies are not included.

Namespace and assembly metadata are Polymita. The assembly GUID, library schema, recipe data and command identifiers remain stable. Only `SettingsStorage.cs` and the installer's compatibility list recognize old product names; those paths are never used for new settings writes.

See README.md for the source map, test commands and behavior details.
