# AX -> TcOpen translation table

Derived from the reference ports listed in SKILL.md (Examples).
Names use the placeholder families of SKILL.md: `AxoTemplate` (AX) and `TcoTemplate` (TcOpen).

## Component

| AX | TcOpen |
|---|---|
| `CLASS AxoTemplate EXTENDS AXOpen.Core.AxoComponent` | `FUNCTION_BLOCK TcoTemplatePn_v_1_x_x EXTENDS TcoIo.TcoPnIoComponent` (`TcoIo.TcoEcatComponent`, `TcoCore.TcoComponent` for .NET TCP) |
| `IMPLEMENTS IAxo<Abstraction>` (e.g. `IAxoRobotics`) | `IMPLEMENTS TcoAbstractions.ITco<Abstraction>`; reuse the `TcoAbstractions` types instead of the AX ones |
| `METHOD PUBLIC Run(inParent : IAxoObject; hwID : UINT)` | FB body with `VAR_IN_OUT inoData : TcoTemplate_IO_v_1_x_x`; parent bound at declaration `(THIS^)` + `FB_init(bInitRetains, bInCopyCode, ParentObject : ITcoObject)` |
| `SUPER.Run(inParent); Messenger.Serve(THIS);` | `IF NOT __ISVALIDREF(inoData) ...RETURN; SUPER^(inPnIoBoxState := ..., inPnIoBoxDiag := ...); _busError := NOT _profinetReady;` |
| `InitHwCheck` / `CheckSubmodule` (`ReadSlotFromHardwareID`, `ReadHardwareIDFromSlot`, `ReadHardwareIOAddress`, size checks) | **dropped** - IO is linked in the System Manager; module sizes are fixed by the IO struct |
| `Config.HWIDs`, `AxoTemplate_HWIDs` | **dropped** |
| `DistributedIO.ReadData(hwID, _module)` per module | one `ARRAY[0..n] OF BYTE AT %I*` per module in `TcoTemplate_Inputs_v_1_x_x` (`In1..InN`, one per module), copied into an image with `MEMCPY` in `UpdateStatus` |
| `DistributedIO.WriteData` | `Out1..OutN AT %Q*` in `_Outputs_`, filled in `UpdateControl` (last statement of the body) |
| `HardwareDiagnosticsTask : AxoHardwareDiagnostics` | **dropped** - PN box state / `_busError` of `TcoPnIoComponent` |
| `Inputs : AxoTemplate_State` + `Status : AxoTemplate_Component_Status` | `_status : TcoTemplate_Status_v_1_x_x` with `ActionDescription` + `State` (+ any extra AX status data) |
| `Outputs : AxoTemplate_Control` | `_control : TcoTemplate_Control_v_1_x_x` |
| public fields `Config`, `Status`, task fields | private `_camelCase` + `PROPERTY Status : REFERENCE TO ...` / `PROPERTY XTask : TcoCore.ITcoTaskStatus` |
| `METHOD ... : IAxoTaskState` | `METHOD ... : TcoCore.ITcoTaskStatus` |
| `RestoreTask.Execute(THIS)` -> `THIS.Restore()` (calls `RestoreTask.DoneWhen(TRUE)` inside) | `IF _restoreTask.Execute() THEN Restore(); _restoreTask.DoneWhen(TRUE); END_IF`; `Restore : ITcoRestorable` = `RestoreTasks()` + safe `_control` |
| `METHOD PROTECTED OVERRIDE ManualControl` | `METHOD PROTECTED ServiceMode` |
| `Status.Action.Id := 50` | `_status.ActionDescription := '<#Restore has been executed.#>'` |

## Tasks

| AX (`AxoTask`) | TcOpen (`TcoCore.TcoTask`) |
|---|---|
| `CLASS ABSTRACT AxoTemplate_BaseTask EXTENDS AxoTask` | `FUNCTION_BLOCK ABSTRACT TcoTemplateTask_v_1_x_x EXTENDS TcoCore.TcoTask` |
| `Run(parent, REF(Config), REF(Status), ...)` + `refX : REF_TO T`, NULL checks | `VAR_IN_OUT inoConfig, inoControl, inoStatus (, inoImage ...)` passed every call; step 0 = `CheckReferences()` (`__ISVALIDREF`) |
| `task.SetIsDisabled(cond); task.Run(...)` | `task.Enabled := NOT cond; task(inoConfig := ..., ...)` - **polarity inverted** |
| `IsBusy() / IsDone() / HasError() / IsReady()` | properties `Busy / Done / Error / Ready` |
| `Execute(THIS)`, `Invoke(THIS)`, `Abort()` | `Execute()`, `Invoke()`, `Abort()` |
| `DoneWhen(TRUE)` | `DoneWhen(TRUE)` |
| `ThrowWhen(cond, '<#text#>')` | `ThrowWhen(cond)` has no text: store the text in a task variable first (`Fail(inText)` helper), post it from `OnError` |
| `OnStart / OnDone / OnError / OnAbort` (OVERRIDE, `SUPER.OnX()`) | `OnStart / OnDone / OnError / OnAbort` (no SUPER call needed), each guarded by `IF __ISVALIDREF(inoStatus)` |
| `OnRestore` | override `Restore : ITcoRestorable` (`SUPER^.Restore(); ...`) |
| no auto-restore | task ends with `IF THIS^.Error AND NOT _errorMessage.Pinned THEN THIS^.Restore(); END_IF`; `OnError` pins `_errorMessage` |
| `Duration` | `ElapsedTime` (rarely needed; use timers) |
| `Config.TaskTimeout` + `ThrowWhen(Duration >= ...)` | **dropped** - `Config.ErrorTime` with `_errorTimer` / `ThrowWhen(_errorTimer.Q)` (T#0S = off) |
| `Messenger.Post(Status.ActionInfo, Info)` after `InfoTime` | `Messenger.OnCondition(_infoTimer.Q).Warning/Info(...)` |
| parameters stored on the component before `Invoke` | task-local variable + `InvokeWithParams(...)` or fluent `WithParams(...)`; or `_control` data written by the operation method (cyclic) |
| sequential `IF _progress = N THEN` | same idiom; comment each block `// AX step N` (step numbers may be kept as in AX, e.g. 0/10/20/100) |
| `GoTo(step)` restarting a step timer | `_progress := step; _stepTimer(IN := FALSE);` |
| `AxoRemoteTask` | `TcoCore.TcoRemoteTask` (.NET TCP archetype) |

## Messenger

| AX | TcOpen |
|---|---|
| `Messenger : AxoMessenger` field + `Serve(THIS)` | built-in `Messenger`; tasks add `_errorMessage : TcoCore.TcoMessenger(THIS^)` |
| `Messenger.Activate(UINT#n, Error)` + C# `MessengerTextList` (code -> text, help) | `Messenger.Error('<#text#>')` with the AX text (and help when short) inline; **numeric codes dropped** |
| `Status.Error.Id := n` | dropped (or a diagnostic field only when the user asks) |
| categories `Error / Warning / Info / ProgrammingError / Potential` | `Error / Warning / Info / Programming / Warning` |
| `ActivateOnCondition(n, cond, cat)` | `Messenger.OnCondition(cond).<Category>(...)` |
| `Messenger.Restore()` | `UnPin()` |

## Data types and language

| AX | TcOpen |
|---|---|
| `CLASS` with data + methods (Decode/Encode/Add/Clear) | `STRUCT` DUT + `FUNCTION`s `Tco<Vendor><Device>_<Verb><Type>_v_X_Y_x(VAR_IN_OUT ...)`; FB only if it must keep state between cycles |
| `CLASS` used only as data | `STRUCT` |
| `ARRAY[*] OF BYTE` parameter | fixed bounds (`ARRAY[0..255] OF BYTE`) or `POINTER TO BYTE` + size |
| `REF_TO T`, `^`, `REF(x)`, `NULL` | `VAR_IN_OUT` (preferred), `REFERENCE TO` + `REF=`, `__ISVALIDREF` |
| `THIS.` / `SUPER.` | `THIS^.` / `SUPER^.` |
| method overloads | distinct names |
| `VAR PUBLIC/PROTECTED` | FB `VAR` + properties; inherited `VAR` are visible to subclasses |
| `LTIME`, `LT#5S` | `TIME`, `T#5S` |
| `OnDelayTimer.OnDelay(THIS, sig, t)`, `.output`, `.Restore()` | `TON`: `t(IN := sig, PT := ...)`, `.Q`, `t(IN := FALSE)` |
| `AxoBlinker` | `TcoUtilities.TcoBlinker(THIS^)` (check period) |
| `TO_INT`, `TO_REAL`, `TO_WORD` | explicit `WORD_TO_INT`, `DINT_TO_REAL`, `INT_TO_WORD` ... |
| `UINT#5`, `WORD#16#8000` | plain literals with typed context (`16#8000`) |
| `STRING` (unbounded) | `STRING(n)` - `ActionDescription : STRING(100)`, texts up to 254 |
| `x.%X3`, `%B0` | `x.3`, byte helpers / `SHL` / `TcoUtilities.TcoBytesTo*` (`eTcoDataFormat.LittleEndian`) |

## Attributes

| AX | TcOpen |
|---|---|
| `{#ix-set:AttributeName = "<#X#>"}` | `{attribute addProperty Name "<#X#>"}` |
| `{#ix-attr:[ReadOnly()]}` | `{attribute clr [ReadOnly()]}` |
| `{#ix-attr:[RenderIgnore()]}` | `{attribute clr [RenderIgnore()]}` or `{attribute 'hide'}` for internals |
| `{#ix-attr:[ComponentDetails(...)]}`, `[Container(...)]`, `[Group(...)]`, `{S7.extern=...}` | drop - layout is the WPF view's job |
| `///<summary>` | `(*~ <docu><summary> ... </summary></docu> ~*)` |
| enum | `{attribute 'qualified_only'}` + `{attribute 'strict'}` |

## Tests and UI

| AX | TcOpen |
|---|---|
| AxUnit `{TestFixture}` / `{Test}` in `ctrl/test` | test PLC test context + NUnit `PlcExectutedTests/` (testing.md of tco-vendor-component) |
| `_harness/Fake<Device>.st` simulator | simulator section of the test context `Main`, same knobs as `_sim*` / `_fault*` variables |
| `S7Harness` (HW-ID / ReadData mocks) | none - tests write the unlinked `_io` struct directly |
| xUnit messenger-completeness tests | none (codes dropped); `Test-TcoLocalization.ps1` checks the texts instead |
| Blazor view + tabs | WPF `<Gen>/Service/<FB>ServiceView.xaml` + view model (status badges, values, task buttons, data panels) |

## Gotchas

- `Enabled` is the inverse of `SetIsDisabled`; re-read each interlock line.
- Several AX `IF _progress = N` blocks may advance within one cycle - the TcOpen idiom does the same; keep block order.
- AX step numbering: TcOpen adds `CheckReferences`; either insert it as an extra step before AX step 0 or check references in the body before the step blocks.
- Profinet devices with several modules: one `AT %I*` array per module; keep the AX image offsets so the decode code stays 1:1.
- Numbers that are 0-based on the bus and 1-based in AX (e.g. program or recipe numbers): keep the AX conversion in the encode/decode functions.
- AX writes the whole output image every cycle (zeroed then encoded) - do the same in `UpdateControl`.
- Unaligned multi-byte fields: decode via byte helpers, not struct overlays (TwinCAT pack mode).
