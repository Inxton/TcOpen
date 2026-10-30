# Archetype A - cyclic fieldbus IO (robots, Profinet / EtherCAT)

Reference implementation: `src/TcoKukaRobotics`, KSS 9.3 generation. The same pattern is used by the other robotics, vision (Dataman/Insight) and press (EtherCAT) libraries.

All logic is in the PLC. The C# twin only adds diagnostics; there are no remote tasks.

## IO structs (in `POUs/Prototypes/<Generation>/IO/`)

```st
TYPE TcoTemplate_Inputs_v_1_x_x :
STRUCT
    Inputs AT %I* : ARRAY[0..63] OF BYTE;          // size = configured process image
END_STRUCT
END_TYPE

TYPE TcoTemplate_IO_v_1_x_x :                      // Profinet
STRUCT
    PnIoBoxState AT %I* : UINT;
    PnIoBoxDiag AT %I* : UINT;
    PnIoBoxCtrl AT %Q* : UINT;
    {attribute addProperty Name "<#IN#>"}
    In : TcoTemplate_Inputs_v_1_x_x;
    {attribute addProperty Name "<#OUT#>"}
    Out : TcoTemplate_Outputs_v_1_x_x;
END_STRUCT
END_TYPE

TYPE TcoTemplate_IO_v_1_x_x :                      // EtherCAT: In/Out only
STRUCT
    {attribute addProperty Name "<#IN#>"}
    In : TcoTemplate_Inputs_v_1_x_x;
    {attribute addProperty Name "<#OUT#>"}
    Out : TcoTemplate_Outputs_v_1_x_x;
END_STRUCT
END_TYPE
```

Raw byte arrays, sized to the configured process image. The application declares an instance in a GVL (`Device1 : TcoTemplate_IO_v_1_x_x;`), links it in the System Manager and passes it as `inoData`.

## Status / Control mapping

- `TcoTemplate_State_v_1_x_x`: one named member per device signal (`Ready`, `DrivesReady`, `ProgramActive`, `Error`, `EventId`, ...).
- `TcoTemplate_Control_v_1_x_x`: one named member per PLC request (`ProgramEnable`, `DrivesOn`, `ErrorAck`, `ActionNo`, `Coordinates`, ...).
- `METHOD INTERNAL UpdateStatus` unpacks inputs, called right after `SUPER^`:

  ```st
  index := 0;
  _status.State.Ready := inoData.In.Inputs[index].0;
  _status.State.DrivesReady := inoData.In.Inputs[index].1;
  ...
  ```

- `METHOD INTERNAL UpdateControl` packs `_control` into `inoData.Out.Outputs[...]` - last statement of the body.
- Put the byte/bit map as a comment table in the docu of the component FB and in the README; tests assert it (testing.md).
- Signal names come from the vendor manual of the new device - never reuse another vendor's signal names.

## Component body specifics

Profinet (`EXTENDS TcoIo.TcoPnIoComponent`):

```st
SUPER^(inPnIoBoxState := THIS^.inoData.PnIoBoxState, inPnIoBoxDiag := THIS^.inoData.PnIoBoxDiag);
_busError := NOT _profinetReady;
UpdateStatus();
```

EtherCAT (`EXTENDS TcoIo.TcoEcatComponent`):

```st
SUPER^(AdsAddr := AdsAddr);
_busError := NOT SUPER^._ecatState.EcatOk;
UpdateStatus();
```

Label `_busError` `<#PN Error#>` or `<#ECat Error#>`.

Interlocks:

- start tasks are disabled while any stop task is `Busy`; competing start tasks disable each other
- stop tasks are always `Enabled := TRUE`
- `ResetAllOutputs` only when no task is active (`_someTaskIsActive`)
- safety diagnostics (E-stop, safety switch) are reported only while a task is active, so an unused device does not flood alarms
- controller error: `_errorReportTimer(IN := _status.State.Error, PT := _config.InfoTime)` then `Messenger.OnCondition(_errorReportTimer.Q).Error(... EventId ... ' |[[0]AdvancedDiagnosticMessage]|' ...)` - the `|[[0]Property]|` placeholder is resolved by the twin property

## Robotics contract

`IMPLEMENTS TcoAbstractions.ITcoRobotics` (`src/TcoAbstractions/src/XAE/XAE/TcoAbstractions/Robotics/`): `StartAtMain`, `StartMotorsAndProgram`, `StartMovements(TcoRoboticsMovementsParams)`, `StopMovements(eTcoRoboticsStopType)`, `StopMovementsAndProgram(eTcoRoboticsStopType)`. Reuse `TcoRoboticsMovementsParams`, `TcoRoboticsCoordinates`, `eTcoRoboticsStopType`, `eTcoRoboticsDistance` - do not redefine them.

Usual task set (adapt to the device's handshake): StartAtMain, StartMotors, StartProgram, StartMotorsAndProgram, StartMovements, StartMotorsProgramAndMovements, StopMovements, StopMovementsAndProgram, StopProgram, StopMotors, ResetAllOutputs (+ Restore). `StartMovements` writes ActionNo/tool/workobject/point/speed/coordinates to `_control` and waits for the device to mirror them back (handshake), then for in-position.

## Single bus

Build only the bus the project needs. The second bus = copy the FB, add the bus suffix to both names (`TcoTemplatePn_v_1_x_x`, `TcoTemplateEcat_v_1_x_x`), change `EXTENDS`, the `inoData` type, the `SUPER^` call and the bus-error label; everything else (DUTs, tasks, Inputs/Outputs) is shared.

## Twin (`TcoTemplateConnector/pex/TcoTemplate_v_1_x_x.cs`)

Only diagnostics, e.g. an event code dictionary and a property used by the messenger placeholder:

```csharp
namespace TcoTemplate
{
    public partial class TcoTemplate_v_1_x_x
    {
        public string AdvancedDiagnosticMessage
        {
            get
            {
                // parse the event number from _messenger._mime.Text and look it up in TcoTemplateEvents_v_1_x_x.Ids
            }
        }
    }
}
```

Event tables live in `pex/TcoTemplateEvents_v_1_x_x.cs` (`public static Dictionary<uint, string> Ids`), filled from the vendor's message list.

## View (`TcoTemplate.Wpf/<Generation>/Service/`)

- `TcoTemplate_v_1_x_xServiceView.xaml` + `.xaml.cs` (design-time `DataContext = new TcoTemplate_v_1_x_xServiceViewModel()`), and `TcoTemplate_v_1_x_xServiceViewModel.cs` with `public class TcoTemplate_v_1_x_xViewModel : TcoTemplate_v_1_x_xServiceViewModel {}`.
- View model base: `RenderableViewModel` with `Component { get; }` and `Model => Component`; if the library gets several generations, introduce a generic `TcoTemplateBaseServiceViewModel<T>` (pattern exists in the cyclic reference implementation).
- XAML: `DataContext="{Binding Component}"`, status flags with `vortex:BooleanToVisibilityConverter`, `vortex:RenderableContentControl` bound to each task (`DataContext="{Binding _resetAllOutputsTask}"`), texts from `p:strings` resources - translated per [localization.md](localization.md).
- Only the Service view is required; the renderer picks it for `PresentationType="Service"`.

## Device description files

GSDML (Profinet) / ESI (EtherCAT) go to `assets/` and `TcoTemplateConnector/ddf/`; reference them in the README with the module configuration that matches the IO struct sizes.
