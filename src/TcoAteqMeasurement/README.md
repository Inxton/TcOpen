# TcoAteqMeasurement

## Introduction

**TcoAteqMeasurement** contains TcOpen components for ATEQ measurement instruments.

| Component | Device | Interface |
|---|---|---|
| `TcoAteqG6Pn_v_3_5_x` | ATEQ G6 leak tester, fieldbus software 3.5 (Hilscher comX 51 PROFINET, firmware V3.5.35+ / G6 fieldbus software newer than 2.103) | PROFINET, fieldbus mode 5 "Standard": 4 x 64 input bytes, 4 x 64 output bytes |

The component is a port of the AXOpen component `AxoG6` (`AXOpen.Components.Ateq.Measurement.v_3_5_x`). Manual: ATEQ *G6 Series - Profinet* (FG_G6_PROFINET_EN_02, 2020-06-17).

What it does:

- reset, program selection, complete test cycle (select, start, wait for cycle end, read and check the last result), read last result, special cycles
- read parameters of the selected program: any list of 1-35 identifiers, the preset set (times, test type, fill and reject levels) or one selected parameter
- live state: echo / error words, current program, results in FIFO, status bits, step, real-time pressure and flow with units

Not supported (same as AX): reading or resetting the results FIFO, writing parameters, configuration and function bits, program names, any fieldbus mode other than Standard.

## General TcOpen Prerequisites

**Check general requisits for TcOpen [here](https://github.com/TcOpenGroup/TcOpen#prerequisites).**

## Instrument and hardware

- On the instrument, *CONFIGURATION > AUTOMATISM > FIELDBUS*: **ACCESS = 5 Standard**, **ADDRESS** = PROFINET device name (convention `ateqg6` + 3 digits, e.g. `ateqg6001`).
- Connectors X1 P1 / X1 P2: M12 D-coded, 4 pins.
- Device description: [`assets/GSDML-V2.32-HILSCHER-COMX 51XX-RE PNS-20160502.xml`](assets/) (VendorID `0x011E`, DeviceID `0x0117`). Copy it into `..\Config\Io\Profinet\` of the TwinCAT installation before opening Visual Studio.
- Use the DAP **`DIM 24`** and this module configuration:

| Slot | Module | Mapped to |
|---|---|---|
| 1 | `64byteinput` | `In.In1` - input image 00h..3Fh |
| 2 | `64byteinput` | `In.In2` - input image 40h..7Fh |
| 3 | `64byteinput` | `In.In3` - input image 80h..BFh |
| 4 | `64byteinput` | `In.In4` - input image C0h..FFh |
| 5 | `64byteoutput` | `Out.Out1` - output image 00h..3Fh |
| 6 | `64byteoutput` | `Out.Out2` - output image 40h..7Fh |
| 7 | `64byteoutput` | `Out.Out3` - output image 80h..BFh |
| 8 | `64byteoutput` | `Out.Out4` - output image C0h..FFh |

`PnIoBoxState` / `PnIoBoxDiag` / `PnIoBoxCtrl` of `TcoAteqG6Pn_IO_v_3_5_x` are linked to the device box variables, like in the other Profinet components.

### Data layout

All data are little-endian; measured values and parameters are LONG x1000; program numbers are 0-based on the bus and 1-based in the component (as shown on the instrument display).

| Input | Content | Output | Content |
|---|---|---|---|
| 00h | command echo | 00h | command word |
| 02h | command error word (FFFFh = command running, 0 = ok, bit = failed command) | 06h | program number |
| 06h | current program | 08h | special cycle id |
| 08h | results in FIFO | 09h | special cycle instruction |
| 0Ah | test type (not verified) | 20h | parameter request: count + up to 35 identifiers |
| 0Ch | status bits (0 pass, 1 fail test, 2 fail reference, 3 alarm, 4 pressure error, 5 cycle end, 6 recoverable, 7 calibration, 9 ATR, 15 key) | | |
| 0Eh | step (FFFFh = none, 0 pre-fill ... 5 dump) | | |
| 10h..1Fh | real-time pressure, pressure unit, flow, flow unit | | |
| 20h.. | exchange zone: last result (20h program, 22h test type, 24h relay image, 26h alarm code, 28h..37h measurement) or parameter response (id WORD + value LONG per parameter) | | |

Command bits: 0 reset, 1 start, 2 special cycle, 3 program selection, 5 read parameters, 15 read last result. Each command uses the ATEQ handshake: own echo low -> raise the bit -> echo / error word FFFFh -> error word 0 (or the failed command bit) -> drop the bit -> echo low. Every wait is bounded by `Config.CommandTimeout`.

## PLC

1. Declare the hardware structure in a GVL and link it to the device (see the table above).

    ```Pascal
    VAR_GLOBAL
        Ateq1 : TcoAteqMeasurement.TcoAteqG6Pn_IO_v_3_5_x;
    END_VAR
    ```

2. Declare the component in a `TcoCore.TcoContext` derived block and call it every cycle; `Service()` enables the service view.

    ```Pascal
    VAR
        _ateq1 : TcoAteqMeasurement.TcoAteqG6Pn_v_3_5_x(THIS^);
    END_VAR

    _ateq1.Service(); // only when the service mode is active
    _ateq1(inoData := GVL.Ateq1);
    ```

3. Use the operations (each returns `TcoCore.ITcoTaskStatus`, call them until `Done` or `Error`):

    | Operation | Result |
    |---|---|
    | `Reset()` | resets the instrument; aborts any other running command |
    | `SelectProgram(inProgram)` | program 1-128 selected and confirmed by the instrument |
    | `StartCycle(inProgram)` | complete test cycle, result in `Status.LastResult` (`Valid`, `Pass`, `FailTest`, `AlarmCode`, `Measurement`); `Status.LastPass` |
    | `ReadLastResult()` | reads the last result into `Status.LastResult` |
    | `SpecialCycle(inSpecialCycle, inInstruction)` | runs a special cycle on the selected program |
    | `ReadParameters(inoRequest)` | reads 1-35 parameters into `Status.Parameters` |
    | `ReadPresetParameters()` | reads the preset into `Status.ParameterSet` |
    | `ReadParameter(inParameter)` | reads one parameter into `Status.ParameterQuery` |

    ```Pascal
    IF _ateq1.StartCycle(inProgram := 3).Done THEN
        IF _ateq1.Status.LastPass THEN
            ...
        END_IF
    END_IF
    ```

    Only one command runs at a time: while a command is busy the other commands are disabled (Reset excepted), and the operation does not overwrite the data of the running command.

Configuration (`Config`): `InfoTime` (progress information), `ErrorTime` (task timeout, 0 = off), `CommandTimeout` (each handshake step, default 5 s), `CycleTimeout` (test cycle, default 60 s).

## Visualization

```XML
<vortex:RenderableContentControl DataContext="{Binding MAIN._wpfContext._ateq1}" PresentationType="Service"/>
```

The service view shows ready / command running / program / pass / fail / alarm, the commands, `Status`, `Control` (program number, special cycle, parameter request, selected parameter) and `Config`. See `tests/TcoAteqMeasurement.Wpf.Sandbox`.

## Troubleshooting

| Message | Cause / remedy |
|---|---|
| Command was not acknowledged by the instrument | no PROFINET connection, or the instrument is not in fieldbus mode 5 Standard |
| Instrument reported a command error | `Status.LastCommandError` holds the error word; the set bit names the failed command (e.g. 0008h program selection - program does not exist) |
| Command was not completed by the instrument | the error word stayed FFFFh - check the instrument display for an open menu or an alarm |
| Instrument still echoes the previous command / did not release the command echo | reset the instrument, then restore the component |
| Instrument is not ready (no cycle end) | a cycle is running; wait or reset |
| Test cycle did not start / did not end within Config.CycleTimeout | instrument alarm, invalid program, or program times longer than `CycleTimeout` |
| Last result does not belong to the requested program | a stale result was read - check that no other master resets the results FIFO |

Not verified on hardware (taken over from AX): relay image bits 4-8 of the last result, input word 0Ah, freshness of the last result after more than 8 cycles without a FIFO reset, CycleEnd behaviour of special cycles.

## Differences against the AXOpen component

- No hardware-ID bootstrap (AX errors 700-787) and no module read/write errors (1201-1214): the modules are linked in the TwinCAT System Manager; PN state is reported by `TcoIo.TcoPnIoComponent` (`PN Error`).
- No numeric error ids (`Status.Error.Id`, `Status.Action.Id`): failures are localized messages; `Status.LastCommandError` is kept.
- `Config.TaskTimeout` is `Config.ErrorTime`.
- AX `ParameterQuery.Parameter` is `Control.QueryParameter`; the lookup result is `Status.ParameterQuery`.
- `ReadParameter(inParameter)` is an operation (in AX it existed only in the Blazor view).
- The handshake and the task step sequences are 1:1 with AX (`// AX step N` comments).

## Tests

`tests/TcoAteqMeasurementUnitTests/PlcExectutedTests/TcoAteqG6Tests.cs` runs against the test PLC (`TcoAteqMeasurementTests`, AMS port 852, `Tc3Target` environment variable). The test context contains an instrument simulator (`TcoAteqG6Simulator`, port of the AX `FakeG6` harness) with fault injection (fail / silent / stuck-echo masks, ignored program selection, no / endless cycle, stale result).
