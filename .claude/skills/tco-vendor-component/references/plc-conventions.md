# PLC conventions (both archetypes)

Source of truth: `docs/Component.md` + the reference implementations listed in SKILL.md. All names below use the `TcoTemplate` placeholder family (see SKILL.md, "Placeholder names").

## Naming

| Element | Pattern |
|---|---|
| Component FB | `TcoTemplate_v_1_x_x` (bus suffix only if both buses exist: `TcoTemplatePn_v_1_x_x`) |
| DUTs | `TcoTemplate_Config_v_1_x_x`, `TcoTemplate_Control_v_1_x_x`, `TcoTemplate_State_v_1_x_x`, `TcoTemplate_Status_v_1_x_x` |
| IO DUTs | `TcoTemplate_Inputs_v_1_x_x`, `TcoTemplate_Outputs_v_1_x_x`, `TcoTemplate_IO_v_1_x_x` |
| Task base | `TcoTemplateTask_v_1_x_x` (ABSTRACT) |
| Tasks | `TcoTemplate_<Operation>Task_v_1_x_x`, e.g. `TcoTemplate_StopProgramTask_v_1_x_x` |
| Private members | `_camelCase`: `_stopProgramTask`, `_config` |
| FB body params | `in...` (VAR_INPUT), `out...` (VAR_OUTPUT), `ino...` (VAR_IN_OUT): `inoData`, `inoConfig` |
| Enums | `eTcoTemplate...`, `{attribute 'qualified_only'}` + `{attribute 'strict'}` |

- **No `%I*`/`%Q*` inside the component.** IO structs carry the `AT %I*`/`%Q*` and are instantiated in the application (GVL) and passed in as `inoData`.
- Every user-visible member / text is localizable: `{attribute addProperty Name "<#Text#>"}` on declarations and `'<#Text#>'` in strings. Key rule, cultures and translation procedure: [localization.md](localization.md).
- Read-only status in the UI: `{attribute clr [ReadOnly()]}`; hidden internals: `{attribute 'hide'}`.

## Component FB

```st
FUNCTION_BLOCK TcoTemplate_v_1_x_x EXTENDS TcoIo.TcoPnIoComponent   // base class by archetype, see below
VAR_INPUT
    inEnable : BOOL;
END_VAR
VAR_IN_OUT
    inoData : TcoTemplate_IO_v_1_x_x;                 // cyclic only
END_VAR
VAR
    {attribute addProperty Name "<#Status#>"}
    {attribute clr [ReadOnly()]}
    _status : TcoTemplate_Status_v_1_x_x;
    {attribute addProperty Name "<#Control#>"}
    _control : TcoTemplate_Control_v_1_x_x;           // cyclic only
    {attribute addProperty Name "<#Config#>"}
    _config : TcoTemplate_Config_v_1_x_x;
END_VAR
VAR
    {attribute addProperty Name "<#Stop program#>"}
    _stopProgramTask : TcoTemplate_StopProgramTask_v_1_x_x(THIS^);
    // ... one member per task ...
    {attribute addProperty Name "<#Restore#>"}
    _restoreTask : TcoCore.TcoTask(THIS^);
END_VAR
VAR
    {attribute 'hide'}
    _sb : TcoCore.StringBuilder;
END_VAR
```

Base class: `TcoCore.TcoComponent` (TCP, discrete), `TcoIo.TcoPnIoComponent` (Profinet), `TcoIo.TcoEcatComponent` (EtherCAT). Robots add `IMPLEMENTS TcoAbstractions.ITcoRobotics` (cyclic-io.md).

Members of the FB (TwinCAT folders `Operations` and `Task`):

| Member | Shape |
|---|---|
| Operation method | `METHOD PUBLIC StopProgram : TcoCore.ITcoTaskStatus` -> `StopProgram := _stopProgramTask.Invoke();` (with params: `_task.InvokeWithParams(inX)` or `_task.WithParams(inX).Invoke()`) |
| Task property | `PROPERTY StopProgramTask : TcoCore.ITcoTaskStatus` -> `StopProgramTask := _stopProgramTask;` |
| `Config`/`Status`/`Control` | `PROPERTY Status : REFERENCE TO TcoTemplate_Status_v_1_x_x` -> `Status REF= _status;` |
| `Restore : ITcoRestorable` | `RestoreTasks();` then reset `_control` outputs to safe values |
| `RestoreTasks` (INTERNAL) | `.Restore()` of every task |
| `ServiceMode` (PROTECTED) | override, may be empty |
| `FB_init` | `(bInitRetains, bInCopyCode, ParentObject : ITcoObject)` |

Body order:

1. guard `IF NOT __ISVALIDREF(inoData) THEN Messenger.Post('Invalid reference', TcoCore.eMessageCategory.ProgrammingError); RETURN; END_IF`
2. `SUPER^(...)` and bus error (cyclic)
3. `UpdateStatus()` (cyclic)
4. interlocks via `task.Enabled := ...` then `task(inoConfig := _config, inoControl := _control, inoStatus := _status);` for every task
5. restore task:

   ```st
   _restoreTask.Enabled := TRUE;
   IF _restoreTask.Execute() THEN
       Restore();
       _restoreTask.DoneWhen(TRUE);
   END_IF
   ```

6. diagnostics: `Messenger.OnCondition(timer.Q).Error(...)` only while a task is active / after a delay
7. `UpdateControl()` (cyclic) - last, so outputs reflect this cycle's task decisions.

## Task base (abstract)

```st
FUNCTION_BLOCK PUBLIC ABSTRACT TcoTemplateTask_v_1_x_x EXTENDS TcoCore.TcoTask   // TcoCore.TcoRemoteTask for .NET TCP
VAR_IN_OUT
    inoConfig  : TcoTemplate_Config_v_1_x_x;
    inoControl : TcoTemplate_Control_v_1_x_x;          // cyclic only
    inoStatus  : TcoTemplate_Status_v_1_x_x;
END_VAR
VAR
    _errorMessage : TcoCore.TcoMessenger(THIS^);
    {attribute 'hide'} _progress : INT := 0;
    {attribute 'hide'} _infoTimer : TON;
    {attribute 'hide'} _errorTimer : TON;
    {attribute 'hide'} _timer : TON;
    {attribute 'hide'} _varInfo : __SYSTEM.VAR_INFO;
    {attribute 'hide'} _sb : TcoCore.StringBuilder;
END_VAR
```

Methods:

- `CallTimers(inValue : BOOL)` - runs `_errorTimer`/`_infoTimer` with `inoConfig.ErrorTime`/`InfoTime` (a time of `T#0S` disables that timer).
- `CheckReferences : BOOL` - `__ISVALIDREF` of each `ino*`, posts `Messenger.Programming('<#Invalid reference: #>' + symbol).Pin()`.
- `Restore : ITcoRestorable` - `SUPER^.Restore(); _progress := 0; _timer(In:=FALSE); CallTimers(FALSE); Messenger.UnPin(); _errorMessage.UnPin();`

Config always has `InfoTime : TIME := T#2S; ErrorTime : TIME := T#0S;` (0 = no timeout). Status always has `ActionDescription : STRING(100)` + `State`.

## Task FB - canonical body

```st
FUNCTION_BLOCK TcoTemplate_StopProgramTask_v_1_x_x EXTENDS TcoTemplateTask_v_1_x_x
---
IF Execute() THEN
    IF _progress = 0 THEN
        IF CheckReferences() THEN
            _progress := _progress + 1;
        END_IF
    END_IF
    IF _progress = 1 THEN
        inoControl.ProgramEnable := FALSE;                                   // write request
        _varInfo := __VARINFO(inoStatus.State.ProgramActive);
        THIS^.Messenger.OnCondition(_infoTimer.Q AND inoStatus.State.ProgramActive).Warning(_sb.Clear().Append('<#Waiting for signal: #>').Append(_varInfo.Symbol).Append('<# is OFF#>').ToString());
        IF NOT inoStatus.State.ProgramActive THEN                            // wait for confirmation
            _progress := _progress + 1;
        END_IF
    END_IF
    IF _progress = 2 THEN
        DoneWhen(TRUE);
    END_IF
    CallTimers(TRUE);
    THIS^.ThrowWhen(_errorTimer.Q);
END_IF
IF THIS^.Error AND NOT _errorMessage.Pinned THEN
    THIS^.Restore();
END_IF
```

`ProgramEnable` / `ProgramActive` stand for the device's real request and confirmation signals.

Reset `CallTimers(FALSE)` when moving to a step that needs a fresh timeout. Lifecycle overrides (each guarded with `IF __ISVALIDREF(inoStatus) THEN ... END_IF`):

- `OnStart`: `ActionDescription := '<#Stop program started.#>'; CallTimers(FALSE);`
- `OnDone`: `ActionDescription := '<#Stop program finished successfully.#>'; _progress := 0;`
- `OnAbort`: `ActionDescription := '<#Stop program was aborted.#>';`
- `OnError`: `ActionDescription := description; _errorMessage.Error(_sb.Clear().Append(description).Append(' To restore acknowledge this alarm.').ToString()).Pin();`
- `Restore`: `SUPER^.Restore();`

Parameters: a task-local variable plus `METHOD INTERNAL InvokeWithParams : ITcoTaskStatus` (`_x := inX; InvokeWithParams := THIS^.Invoke();`), or a fluent `METHOD WithParams : REFERENCE TO TcoTemplate_<Operation>Task_v_1_x_x` (`_command := inCommand; WithParams REF= THIS^;`) used as `_task.WithParams(cmd).Invoke()`.

## Docs comments

Component and important methods carry `(*~ <docu><summary> ... </summary></docu> ~*)` blocks - describe the device, the interface size and the differences against the previous firmware version there.
