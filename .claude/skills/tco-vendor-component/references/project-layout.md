# Project layout, scaffolding, registration

## Target tree

```text
src/TcoTemplate/
  README.md
  TcoTemplate.slnf
  assets/                                       # screenshots, GSDML/ESI files
  src/
    XaeTcoTemplate/XaeTcoTemplate.tsproj
      TcoTemplate/TcoTemplate.plcproj                     # the library
        DUTs/<Generation>/                                # TcoTemplate_Config/_Control/_State/_Status_v_1_x_x
        POUs/Prototypes/<Generation>/                     # TcoTemplate_v_1_x_x
        POUs/Prototypes/<Generation>/IO/                  # TcoTemplate_Inputs/_Outputs/_IO_v_1_x_x (cyclic)
        POUs/Prototypes/<Tasks folder>/<Gen>_v_1_x_x/     # TcoTemplateTask_v_1_x_x + TcoTemplate_<Operation>Task_v_1_x_x
      TcoTemplateTests/TcoTemplateTests.plcproj           # test PLC (ADS port 852)
        GVLs/GVL.TcGVL                                    # IO instances linked to hardware
        POUs/MAIN.TcPOU
        Tests/TcoTemplate/TcoTemplateTestContext.TcPOU, eTcoTemplateTests.TcDUT
    TcoTemplateConnector/TcoTemplateConnector.csproj
      Entry.cs, pex/*.cs, Properties/AssemblyInfo.cs, Properties/Localizations*.resx, ddf/ (device files)
    TcoTemplate.Wpf/TcOpen.Inxton.TcoTemplate.Wpf.csproj
      <Generation>/Service/TcoTemplate_v_1_x_xServiceView.xaml(.cs), TcoTemplate_v_1_x_xServiceViewModel.cs, Properties/strings*.resx
  tests/
    TcoTemplateTestsConnector/            # twin of the test PLC
    TcoTemplateUnitTests/                 # NUnit: TestSetupFixture, ConnectorFixture, MockRootObject, PlcExectutedTests/, pex/
    TcoTemplate.Wpf.Sandbox/              # WPF exe
```

One folder per controller generation; the firmware lives in the `_v_X_Y_x` suffix of every FB, DUT and task of that generation.

## Scaffold

```powershell
cd D:\MTS\Develop\Inxton\TcOpen\src\librarytemplate     # must be run from here: target = parent folder
.\scaffoldnewlibrary.ps1 -libraryName TcoTemplate        # real library name here; no '.' or spaces; -baseDirectory is ignored
```

The script copies the template, replaces the literal `PlcTemplate` in file contents and names, then deletes the copied `.ps1` and `<Lib>.slnf`. Fix afterwards:

1. **Project GUIDs are NOT regenerated.** Give new GUIDs to every tsproj, plcproj and csproj (`ProjectGuid`) and to the PLC project instances inside the tsproj, then use the new ones in `TcOpen.sln`. Duplicate GUIDs break the solution.
2. **Recreate `src/TcoTemplate/TcoTemplate.slnf`** - copy an existing library's `.slnf` (e.g. `src/TcoKukaRobotics/TcoKukaRobotics.slnf`) and rename paths (`"path": "..\\..\\TcOpen.sln"`).
3. Delete copied `bin/`, `obj/`, `_generated/` content, if any.
4. Script uses `Set-Content` (ANSI on PowerShell 5.1): re-check non-ASCII characters in `.resx` and source files - resx must stay UTF-8 or the cs/de/es/sk translations break (see [localization.md](localization.md)).
5. Remove the template sample (`fbSampleComponent`, `SampleComponent/`, `SampleContext`, Wpf `_Sample/`, `SampleComponentTests`) once the real component exists.
6. Fill in `README.md` placeholders (`ENTER ...`).

## Registration checklist (repo root)

| File | Add |
|---|---|
| `TcOpen.sln` | solution folder `TcoTemplate` with nested `src` and `tests` folders, all projects, and configuration mappings for every solution configuration incl. TwinCAT platforms and `ReleaseDevelop` (copy the block of an existing vendor library and replace GUIDs) |
| `TcOpen.slnf` | all projects |
| `TcOpen.build.slnf` | .NET projects: Connector, Wpf, TestsConnector, UnitTests, Sandbox (built by msbuild, packed by `dotnet pack` in `pipelines/build.ps1`) |
| `TcOpen.plc.slnf` | Connector, Xae tsproj, TestsConnector (Vortex compiler runs on this; `CopyPlcLibs` copies the `*.library` next to each tsproj to `plcLibs/`) |
| `TcOpen.XAEs.slnf` | Xae tsproj |
| `TcOpen.test.build.plc.slnf` | Xae tsproj |

Existing typos - do not copy them: `src\\TcoTcoTixonFeeding\\...` in `TcOpen.test.build.plc.slnf`, the Mitsubishi line with a missing `\\` in `TcOpen.XAEs.slnf`.

Optional: test tuple in `pipelines/build.ps1` (no vendor library is there today), reference in `src/_packaging/TcOpen.Group*/*.csproj`.

## Project metadata

- **plcproj**: library category `TcOpen` (from `TcOpen.libcat.xml`), `Company` = `Vortex.Library`; placeholder references `TcoCore`, `TcoIo`, `TcoAbstractions`, `TcoUtilities` (+ Tc2/Tc3 system libraries as needed).
- **Connector csproj**: `net48;net5.0`, `PackageId` `TcOpen.TcoTemplateConnector`, project references to TcoAbstractions/TcoUtilities/TcoCore/TcoIo connectors, packs `_meta\TcoTemplate.library`, `.json`, `version.info` into `lib`, empty `_generated\` folder entry (filled by the Vortex build).
- **Wpf csproj**: `Microsoft.NET.Sdk.WindowsDesktop`, `net48;net5.0-windows`, `Inxton.Package.Vortex.Essentials.Wpf`, references `TcoCore.Wpf` + the connector. `Properties/AssemblyInfo.cs` must contain `[assembly: RenderableAssembly()]` or the renderer will not find the views.
- **Versions**: `AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion` in `Properties/AssemblyInfo.cs` with `GenerateAssemblyInfo=false`; global version from `GitVersion.yml`.
- **Entry.cs**: `Tc3ConnectorAdapter.Create(Environment.GetEnvironmentVariable("Tc3Target"), 851, true)` (library) / port 852 (tests connector).

## Building .NET projects from the command line

`dotnet build`/`dotnet test` of a single csproj fails with `*Undefined*_Vortex\builder` unless `SolutionDir` is set:

```bash
dotnet test src/TcoTemplate/tests/TcoTemplateUnitTests/TcoTemplateUnitTests.csproj -f net48 "-p:SolutionDir=D:\\MTS\\Develop\\Inxton\\TcOpen\\"
```

Twin code in `_generated/` is produced by the Vortex compiler from the PLC project; after changing PLC declarations the twin must be regenerated (Vortex build in Visual Studio / `TcOpen.plc.slnf`) before C# can use the new members. A C# error like "name `_messenger` does not exist" in a `pex/` partial usually means the twin of a new FB was not generated yet.
