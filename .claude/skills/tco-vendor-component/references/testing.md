# Testing

Minimum bar for a new component: **cyclic** -> simulator test context in the test PLC + NUnit PLC-executed tests per task; **.NET TCP** -> pure .NET NUnit tests against a fake TCP server (no PLC) + a test context exercising the operations.

Reference implementations: cyclic `src/TcoKukaRobotics` (test context `Tests/TcoKukaKss/`, NUnit `tests/TcoKukaRoboticsUnitTests/PlcExectutedTests/`), TCP `src/TcoAimTtiPowerSupply` (`tests/TcoAimTtiPowerSupplyUnitTests/pex/`).

## Shared NUnit scaffolding (`tests/TcoTemplateUnitTests/`)

- `TcoTemplateUnitTests.csproj`: `net5.0;net48`, NUnit 3.13.2, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, JunitXml.TestLogger; references the connector and the tests connector.
- `TestSetupFixture` (`[SetUpFixture]`): `TargetAmsId = Environment.GetEnvironmentVariable("Tc3Target")`, `TargetAmsPort = 852`.
- `ConnectorFixture.Connector`: lazily `Tc3ConnectorAdapter.Create(TargetAmsId, TargetAmsPort, true)`, `ReadWriteCycleDelay = 100`, `BuildAndStart()`.
- `MockRootObject`: dummy-connector parent that lets you instantiate any twin without a PLC: `new TcoTemplate_v_1_x_x(new MockRootObject(), string.Empty, string.Empty)`; `.Synchron` reads/writes work in memory.
- Folders: `PlcExectutedTests/` (needs PLC; existing spelling) and `pex/` (pure .NET).
- Run: `dotnet test <csproj> -f net48 "-p:SolutionDir=D:\\MTS\\Develop\\Inxton\\TcOpen\\"`. PLC-executed tests need the test PLC activated on `Tc3Target`; if it is not available, say so instead of claiming they pass.

## Cyclic: simulator test context (test PLC)

`Tests/TcoTemplate/TcoTemplateTestContext.TcPOU` + `eTcoTemplateTests.TcDUT`:

```st
FUNCTION_BLOCK TcoTemplateTestContext EXTENDS TcoCore._internals_TcoContext
VAR
    _sut : TcoTemplate.TcoTemplate_v_1_x_x(THIS^);
    _io : TcoTemplate.TcoTemplate_IO_v_1_x_x;          // NOT linked - the simulator drives it
    _sim : TcoTemplate.TcoTemplate_State_v_1_x_x;      // device-side signals, set from NUnit
    _simulate : BOOL;                                  // TRUE = minimal controller model reacts to outputs
    _simProgramBlocked : BOOL;                         // fault injection flags
    _taskDone : BOOL;
    _taskError : BOOL;
END_VAR
```

`METHOD PROTECTED Main`, every cycle:

1. **Simulator** (when `_simulate`): derive `_sim.*` from `_io.Out.Outputs[...]` like the real controller (interface activation, drives on/off, error acknowledge, program start, handshake mirroring). Keep it minimal - only what the tasks wait for. Fault flags block a reaction so timeout/error paths can be tested.
2. **Input image**: write `_sim.*` into `_io.In.Inputs[...]` using exactly the documented bit map.
3. `_sut(inoData := _io);`
4. `CASE _testId OF` - one case per test enum member:

   ```st
   eTcoTemplateTests.Restore:
       _sut.Restore(); _sut._messenger.Clear(); _taskDone := FALSE; _taskError := FALSE;
       ProbeDoneWhen(TRUE);
   eTcoTemplateTests.Run:
       ;
   eTcoTemplateTests.StartProgram:
       _taskDone := _sut.StartProgram().Done;
       _taskError := _sut.StartProgramTask.Error;
       ProbeDoneWhen(_taskDone OR _taskError);
   ```

Test enum: `{attribute 'qualified_only'} {attribute 'strict'}`, `None := 0, Restore := 10, Run := 20, <Operation> := 30, ...`. Add the context to test `MAIN` (`_templateTestContext : TcoTemplateTestContext;`) and call `_templateTestContext.ProbeRun();` every cycle.

## Cyclic: NUnit (`PlcExectutedTests/TcoTemplate_v_1_x_xTests.cs`)

```csharp
TcoTemplateTestContext sut = ConnectorFixture.Connector.MAIN._templateTestContext;

[SetUp]
public void TestSetup()
{
    sut._simulate.Synchron = false;
    ResetDeviceSide();                                    // all _sim.* to defaults
    sut.ExecuteProbeRun((int)eTcoTemplateTests.Restore);
    sut.ExecuteProbeRun(2, (int)eTcoTemplateTests.Run);
}

[Test, Timeout(10000)]
public void InputsAreMappedToState()
{
    sut._sim.Ready.Synchron = true;
    sut.ExecuteProbeRun(2, (int)eTcoTemplateTests.Run);
    Assert.IsTrue(sut._sut._status.State.Ready.Synchron);
}
```

Required test groups:

- **Mapping**: every input bit/byte -> `_status.State`, every `_control` member -> output bit/byte (catches bit-map mistakes).
- **Per task, happy path**: healthy device (`HealthyDevice()` sets safety signals + `_simulate = true`), run the task case until done, assert `_taskDone` and the outputs it left.
- **Per task, error path**: block the simulator (fault flag) with a short `ErrorTime`, assert `_taskError` and that `Restore` clears it.
- **Interlocks / special behaviour** (e.g. start blocked while a stop task is busy, outputs set on mode edges).

## .NET TCP: fake server tests (`pex/TcoTemplate_v_1_x_xTests.cs`) - no PLC needed

- Instantiate the twin on `MockRootObject`, call `Initialize()`, point `_config.IpAddress`/`Port` at a `TcpListener` on `IPAddress.Loopback`, port 0, set a short `Timeout`.
- Set the command via `_command.*.Synchron`, call the internal `InvokeGetCommand()`/`InvokeSetCommand()` directly (they are the remote task actions).
- Fake server: accept one connection, read one line into `Received`, then answer by `Reply` (text = reply line, `string.Empty` = close, `null` = stay silent).

Required cases:

- request formatting (assert `Received`) and response parsing into `_status.State` for each response syntax
- unreachable device -> `SocketException`
- silent device -> `IOException` (receive timeout)
- closed without response -> `IOException`
- unparsable/error reply -> exception, raw response still written to status
- every PLC command enum member has an entry in `TemplateCommands.Commands`

Exceptions thrown from the action are what `TcoRemoteTask` turns into a PLC task error, so asserting the throw is asserting the PLC-visible behaviour.

## Sandbox (`tests/TcoTemplate.Wpf.Sandbox`)

Manual check of the Service view against the test PLC: `MainWindow` with `vortex:RenderableContentControl DataContext="{Binding MAIN._wpfContext._component}" PresentationType="Service"`; `_wpfContextCall` and `_serviceModeActive` set `TRUE` at start-up.
