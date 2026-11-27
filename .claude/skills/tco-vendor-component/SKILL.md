---
name: tco-vendor-component
description: Compose a new TcOpen vendor component (PLC library + Inxton twin + WPF view + tests) or a new firmware version of an existing one. Use when the user wants a new Tco<Vendor> library, a new robot / device / controller generation, a new `_v_X_Y_x` variant, or asks how vendor components in this repo are built. Covers two archetypes - cyclic fieldbus IO (robots, Profinet/EtherCAT) and .NET-side TCP communication.
---

# TcOpen vendor component

A vendor component is one library under `src/TcoTemplate/` made of:

| Part | Project | Content |
|---|---|---|
| PLC library | `src/XaeTcoTemplate/TcoTemplate/TcoTemplate.plcproj` | component FB, tasks, DUTs |
| Test PLC | `src/XaeTcoTemplate/TcoTemplateTests/TcoTemplateTests.plcproj` | test contexts, simulator, GVL with IO |
| Twin | `src/TcoTemplateConnector/TcoTemplateConnector.csproj` | generated `_generated/` + hand-written `pex/*.cs` partials |
| View | `src/TcoTemplate.Wpf/TcOpen.Inxton.TcoTemplate.Wpf.csproj` | `TcoTemplate_v_1_x_xServiceView.xaml` + view model |
| Tests | `tests/TcoTemplateTestsConnector`, `tests/TcoTemplateUnitTests`, `tests/TcoTemplate.Wpf.Sandbox` | twin of test PLC, NUnit, manual sandbox |

## Placeholder names

All files of this skill use one generic name family. Replace it consistently when building a real component:

| Placeholder | Meaning | Real example (cyclic / TCP reference implementation) |
|---|---|---|
| `TcoTemplate` | library, PLC namespace, C# namespace | `TcoKukaRobotics` / `TcoAimTtiPowerSupply` |
| `TcoTemplate_v_1_x_x` | component FB; `_v_1_x_x` = the real firmware/protocol version `_v_<major>_<minor>_x` | `TcoKukaKssPn_v_9_3_x` / `TcoQl355P_v_1_x_x` |
| `TcoTemplate_Config_v_1_x_x`, `_Control_`, `_State_`, `_Status_` | DUTs | `TcoKukaKss_Status_v_9_3_x` / `TcoQlSeriesSupply_Status_v_1_x_x` |
| `TcoTemplate_Inputs_v_1_x_x`, `_Outputs_`, `TcoTemplate_IO_v_1_x_x` | process image (cyclic) | `TcoKukaKss_Inputs_v_9_3_x`, `TcoKukaKssPn_IO_v_9_3_x` |
| `TcoTemplateTask_v_1_x_x` | abstract task base | `TcoKukaKssTask_v_9_3_x` / `TcoQlSeriesSupplyTask` |
| `TcoTemplate_<Operation>Task_v_1_x_x` | task per operation | `TcoKukaKss_StopProgramTask_v_9_3_x` / `TcoQlGetCommandTask` |
| `TcoTemplate_GetCommand_v_1_x_x`, `eTcoTemplate_GetCommands_v_1_x_x` | command DUT + enum (TCP) | `TcoQlSeriesSupply_GetControl_v_1_x_x`, `eTcoQWlSeriesSupplyGetCommands_v_1_x_x` |
| `TemplateCommand`, `TemplateCommands`, `eTemplateCommandType` | C# protocol table (TCP) | `QlSeriesCommand`, `QlSeriesCommands`, `eQlCommandType` |
| `TcoTemplateEvents_v_1_x_x` | C# event-code dictionary (cyclic) | `TcoKrc4ControllerEvents_v_5_x_x` |
| `TcoTemplateTestContext`, `eTcoTemplateTests` | test context + test enum | `TcoKukaKssTestContext`, `eTcoKukaKssTests` |

Naming of a real component: `Tco<Vendor><Domain>` for the library (e.g. `TcoKukaRobotics`), `Tco<Device>_v_X_Y_x` for the FB. If both buses are ever needed, the FBs get a bus suffix (`TcoTemplatePn_v_1_x_x`, `TcoTemplateEcat_v_1_x_x`).

Reference implementations to read when a detail is unclear:

- cyclic: `src/TcoKukaRobotics` - KSS 9.3 generation (`POUs/Prototypes/KSS/`, `DUTs/Kss/`, `POUs/Prototypes/RoboticsTasks/Kss_v_9_3_x/`, test `Tests/TcoKukaKss/`)
- .NET TCP: `src/TcoAimTtiPowerSupply` (`POUs/Prototypes/QLSeries/`, `POUs/Prototypes/Command/`, connector `pex/`, tests `tests/TcoAimTtiPowerSupplyUnitTests/pex/`)

Do not copy vendor names, signal names or texts from them into a new component.

## Step 0 - grill before you build

Do not start until these are answered (use AskUserQuestion, one decision at a time when they depend on each other):

1. **Vendor, controller, firmware** -> library name and version suffix `_v_<major>_<minor>_x`. New firmware of an existing vendor = new versioned FBs/DUTs/tasks in the existing library, not a new library.
2. **Communication** -> picks the archetype (below).
3. **Bus** (cyclic only): Profinet **or** EtherCAT. Build only the bus the project needs; the other one is added later by copying the FB and swapping base class + IO struct.
4. **Interface map**: byte/bit layout of the process image (vendor manual, GSDML/ESI) or the command protocol (manual). Do not guess bit positions - ask for the document or the table.
5. **Operations**: the list of tasks (start/stop/reset/get/set...) and the handshake of each (which outputs are written, which input confirms completion).

## Archetype decision

| The device talks... | Archetype | Read |
|---|---|---|
| through a fixed process image every PLC cycle (Profinet IO, EtherCAT PDO) | **cyclic IO** - all logic in PLC | [references/cyclic-io.md](references/cyclic-io.md) |
| request/response over a TCP socket (SCPI, ASCII, proprietary) | **.NET TCP** - PLC tasks are `TcoRemoteTask` mailboxes, socket code in the C# twin | [references/dotnet-tcp.md](references/dotnet-tcp.md) |
| discrete IO only | neither - see `src/TcoPneumatics` and `docs/Component.md` | - |

There is **no PLC-side TCP (Tf6310 / Tc2_TcpIp) component** in the repo. If the user wants one, stop and agree the design first; do not invent a pattern silently.

## Workflow

Always read [references/plc-conventions.md](references/plc-conventions.md) and [references/project-layout.md](references/project-layout.md) first, then the archetype file, then [references/localization.md](references/localization.md) and [references/testing.md](references/testing.md).

1. **Scaffold** the library from `src/librarytemplate` and fix the known template gaps (project-layout.md).
2. **Register** it in `TcOpen.sln` and every `*.slnf` (project-layout.md checklist).
3. **DUTs**: `_Config`, `_Status` (with `ActionDescription` + `State`), `_State`, `_Control` (cyclic) or command structs + enums (TCP), IO structs (cyclic).
4. **Task base** (abstract) + one task FB per operation.
5. **Component FB**: members, body (status update -> interlocks -> task calls -> restore -> diagnostics -> control update), `Operations` methods, `Task` properties, `Restore`/`RestoreTasks`, `ServiceMode`.
6. **Twin partials** in `TcoTemplateConnector/pex/` (diagnostics for cyclic; `Initialize()` + socket code for TCP).
7. **Service view** + view model in the Wpf project.
8. **Localize** (localization.md): Vortex build fills the neutral `Localizations.resx`; translate every new PLC and WPF text into `cs-CZ`, `de-DE`, `es-ES`, `sk-SK`; run `scripts/Test-TcoLocalization.ps1` until clean.
9. **Test PLC**: test context with simulator (cyclic) or a test context calling the operations (TCP); add it to `MAIN`.
10. **NUnit tests** (testing.md).
11. **Sandbox** app showing the component in `PresentationType="Service"`.
12. **README.md** of the library (template has `ENTER ...` placeholders) with wiring/IO table and screenshot.

## Definition of done

- [ ] Names follow `Tco<Device>_v_X_Y_x`; no `%I`/`%Q` inside the component FB; no leftover `TcoTemplate` / `PlcTemplate` placeholders.
- [ ] Every task: messenger warnings while waiting, error timer -> `ThrowWhen`, `OnError` pins the message, `Restore` unpins.
- [ ] Every task restored by `RestoreTasks()`; restore task wired in the body.
- [ ] Projects registered in `TcOpen.sln` + `TcOpen.slnf`, `TcOpen.build.slnf`, `TcOpen.plc.slnf`, `TcOpen.XAEs.slnf`, `TcOpen.test.build.plc.slnf` and the library's own `.slnf`.
- [ ] Unique project GUIDs (not copied from the template).
- [ ] `scripts/Test-TcoLocalization.ps1 -LibraryPath src\TcoTemplate` reports `Errors: 0` for `cs-CZ`, `de-DE`, `es-ES`, `sk-SK`; new translations listed in the PR description.
- [ ] Tests from testing.md exist and pass (`dotnet test` for pure .NET tests; PLC-executed tests need `Tc3Target` and the test PLC running on AMS port 852 - say so if you could not run them).
- [ ] .NET TCP: no swallowed exceptions in remote task actions.
- [ ] No stub files copied from other libraries (e.g. an empty test class with another library's namespace).
