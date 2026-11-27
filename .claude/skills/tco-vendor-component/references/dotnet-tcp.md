# Archetype B - .NET-side TCP communication

Reference implementation: `src/TcoAimTtiPowerSupply` (line-based SCPI-like protocol).

The PLC owns the API and the state machine; each operation is a `TcoCore.TcoRemoteTask` that the C# twin executes. The socket code lives in the twin partial class.

## How a remote task runs

1. PLC: `_getCommandTask.WithParams(cmd).Invoke()` -> `TcoRemoteTask` writes `_startSignature`.
2. .NET: `TcoRemoteTask.ExecuteAsync` (`src/TcoCore/src/TcoCoreConnector/pex/TcoRemoteTask/TcoRemoteTask.cs`) sees the new signature and runs the action registered with `InitializeExclusively(...)` on a worker thread.
3. Success -> `_doneSignarure := _startSignature` (sic) -> PLC `DoneWhen(...)` -> task Done.
4. **Exception** -> `_hasException := TRUE`, `_exceptionMessage := ex.Message`, logged via `TcoAppDomain.Current.Logger.Error`, Done is NOT signalled -> PLC `ThrowWhen(TRUE)` + `Messenger.Error(_exceptionMessage)` -> task Error -> `OnError` pins the alarm.
5. If the twin never called `Initialize()`, `_isInitialized` stays FALSE: the PLC task posts `Messenger.Programming('Remote call not initialized.')` and never finishes - the application/sandbox **must** call `component.Initialize()` after `BuildAndStart()`.

## PLC side

```st
FUNCTION_BLOCK TcoTemplate_v_1_x_x EXTENDS TcoCore.TcoComponent
VAR
    {attribute addProperty Name "<#Status#>"} _status : TcoTemplate_Status_v_1_x_x;
    {attribute addProperty Name "<#Config#>"} _config : TcoTemplate_Config_v_1_x_x;
    {attribute addProperty Name "<#Get Command#>"} _getCommandTask : TcoTemplate_GetCommandTask_v_1_x_x(THIS^);
    {attribute addProperty Name "<#Set Command#>"} _setCommandTask : TcoTemplate_SetCommandTask_v_1_x_x(THIS^);
    {attribute addProperty Name "<#Restore#>"} _restoreTask : TcoCore.TcoTask(THIS^);
END_VAR
// body: _getCommandTask(inoConfig := _config, inoStatus := _status); _setCommandTask(...); restore task pattern
```

- Config: `InfoTime`, `ErrorTime`, `IpAddress : STRING`, `Port : UINT := <device default>`, `Timeout : UINT := 1000` (ms, used for socket send/receive).
- Status: `ActionDescription : STRING(100)` + `State` with the raw `Response`, the parsed values and a `ConvertMessage : STRING` for parse errors.
- Command DUT per direction (`TcoTemplate_GetCommand_v_1_x_x`: `Command : eTcoTemplate_GetCommands_v_1_x_x; Channel`; `TcoTemplate_SetCommand_v_1_x_x`: + `Value`) and command enums whose member names equal the keys of the C# command table.
- Task base `TcoTemplateTask_v_1_x_x EXTENDS TcoCore.TcoRemoteTask` with `VAR_IN_OUT inoConfig, inoStatus`, `_errorMessage : TcoMessenger`, timers, `OnStart/OnDone/OnError/Restore` as in plc-conventions.md (call `SUPER^.OnX()` first - `TcoRemoteTask` has its own).
- Derived tasks hold `_command` and expose `METHOD WithParams : REFERENCE TO TcoTemplate_GetCommandTask_v_1_x_x`.
- Generic operations `GetCommand(inCommand)`/`SetCommand(inCommand)` + typed convenience operations:

  ```st
  METHOD PUBLIC GetValue : TcoCore.ITcoTaskStatus
  VAR_INPUT inChannel : USINT; END_VAR
  VAR_OUTPUT outValue : REAL; END_VAR
  VAR command : TcoTemplate_GetCommand_v_1_x_x; END_VAR
  ---
  outValue := 0;
  command.Command := eTcoTemplate_GetCommands_v_1_x_x.Value;
  command.Channel := inChannel;
  GetValue := GetCommand(inCommand := command);
  IF GetValue.Done THEN outValue := Status.State.Value; END_IF
  ```

## .NET side (`TcoTemplateConnector/pex/TcoTemplate_v_1_x_x.cs`)

Partial class in the namespace of the PLC library (= twin namespace):

```csharp
namespace TcoTemplate
{
    public partial class TcoTemplate_v_1_x_x
    {
        public void Initialize()
        {
            _getCommandTask.InitializeExclusively(InvokeGetCommand);
            _setCommandTask.InitializeExclusively(InvokeSetCommand);
        }

        // Exceptions are intentionally not caught here: TcoRemoteTask reports them to the PLC (_hasException)
        // and to the logger, and does not signal Done.
        internal void InvokeGetCommand()
        {
            this.Read();                                             // fresh config + command from PLC
            var cmd = FindCommand(eTemplateCommandType.Get, Enum.GetName(typeof(eTcoTemplate_GetCommands_v_1_x_x), _getCommandTask._command.Command.LastValue));
            var message = cmd.Syntax.Replace("<N>", _getCommandTask._command.Channel.LastValue.ToString());

            using (TcpClient client = new TcpClient(_config.IpAddress.LastValue, _config.Port.LastValue))
            {
                client.SendTimeout = _config.Timeout.LastValue;
                client.ReceiveTimeout = _config.Timeout.LastValue;
                using (NetworkStream stream = client.GetStream())
                using (StreamWriter writer = new StreamWriter(stream))
                using (StreamReader reader = new StreamReader(stream))
                {
                    writer.WriteLine(message);
                    writer.Flush();
                    var response = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(response))
                        throw new IOException($"No response from {_config.IpAddress.LastValue}:{_config.Port.LastValue} to '{message.Trim()}'.");
                    // parse into _status.State.*.Cyclic ...
                    _status.State.Write();                           // push parsed values to PLC
                    if (!string.IsNullOrEmpty(_status.State.ConvertMessage.Cyclic))
                        throw new FormatException(...);              // after Write(), so the operator sees the raw response
                }
            }
        }

        private static TemplateCommand FindCommand(eTemplateCommandType type, string key)
        {
            var cmd = TemplateCommands.Commands.FirstOrDefault(p => p.Type == type && p.Key == key);
            if (cmd == null)
                throw new InvalidOperationException($"Command '{key}' of type {type} is not defined in {nameof(TemplateCommands)}.");
            return cmd;
        }
    }
}
```

Rules:

- **Never catch-and-swallow in a remote task action** (no `catch { Console.WriteLine }`). A swallowed exception makes the PLC task report Done for a command that failed. Let it propagate, or catch to add context and rethrow.
- Throw on: unknown command (`FindCommand` instead of `FirstOrDefault` + null dereference), no/empty response, unparsable response, device error replies.
- Always apply `Timeout` to both send and receive; the PLC `ErrorTime` is the outer watchdog.
- Read inputs with `this.Read()` + `.LastValue`; write results to `.Cyclic` then `.Write()` the struct.
- Keep the protocol table in its own class (`pex/TemplateCommand.cs`: `TemplateCommand { Key, Syntax, ResponseSyntax, HelpDescription, Type }`, `static class TemplateCommands { Commands }`, `enum eTemplateCommandType { Get, Set }`) so tests can check that every PLC enum member has an entry.
- Make the invoke methods `internal` and add `[assembly: InternalsVisibleTo("TcoTemplateUnitTests")]` to the connector `Properties/AssemblyInfo.cs` so tests can call them without a PLC.
- Opening a connection per command is fine for slow command rates. For high rates or devices that require a session, keep one client per component, guard it with a lock, and reconnect on failure - agree this with the user first.
- Logging beyond the automatic remote-task logging: `TcOpen.Inxton.TcoAppDomain.Current.Logger`, never `Console`.

## View

Service view only (`TcoTemplate_v_1_x_xServiceView.xaml` + `TcoTemplate_v_1_x_xViewModel.cs` / `TcoTemplate_v_1_x_xServiceViewModel`): `tcoCore:TcoComponentView` with the restore task in `ComponentHeader` and GroupBoxes for config (`PresentationType="ControlSlim"`), status (`"DisplaySlim"`) and the command tasks in `ComponentDetails`. View texts in `strings*.resx`, translated per [localization.md](localization.md).

Exception messages thrown from remote task actions are shown to the operator as-is (`_exceptionMessage`); they are not localized - keep them short and technical (device, address, command, response).

## Sandbox

```csharp
var plc = TcoTemplateTests.Entry.TcoTemplateTestsPlc;
TcOpen.Inxton.TcoAppDomain.Current.Builder
    .SetPlcDialogs(DialogProxyServiceWpf.Create(new[] { plc.MAIN._wpfContext }))
    .SetUpLogger(new TcOpen.Inxton.Logging.SerilogAdapter())
    .SetDispatcher(TcoCore.Wpf.Threading.Dispatcher.Get);
plc.Connector.BuildAndStart();
plc.MAIN._wpfContextCall.Synchron = true;
plc.MAIN._wpfContext._serviceModeActive.Synchron = true;
plc.MAIN._wpfContext._component.Initialize();   // registers the remote tasks
```
