# TcoAbbRobotics

## Introduction

The **TcoAbbRobotics** is a set of libraries that cover two product platforms in ABB's manufacturing portfolio: the **IRC5** and the **Omnicore** platform." for the target PLC platform [Twincat](https://www.beckhoff.com/en-en/products/automation/twincat/twincat-3-build-4024/) and [TcOpen](https://github.com/TcOpenGroup/TcOpen#readme) framework.

The package consists of a PLC library providing control logic and its .NET twin counterpart aimed at the visualization part.

## General TcOpen Prerequisites

**Check general prerequisites for TcOpen [here](https://github.com/TcOpenGroup/TcOpen#prerequisites).**

 ## TcoAbbRobotics
### PLC enviroment
--- 
#### **_Preconditions:_** The **`gsdml`** file(s) should be copied into the subfolder ..\Config\Io\EtherCat\ of the TwinCAT3 instalation folder, before opening Visual Studio. The Profinet interface of the slave device is activated. The file depends on manufacturer of drive. Robot settings needs to by done in settings   by RobotStudio sfotware by ABB or directly via robot teach pendant. 
---

#### **_Preconditions:_** The robot software is part of repository. Use **RobotStudio** to open project. And transfer it  to robot.
---

#### Implementation steps.
#### 1. Declare the hardware structures in the Global Variable list (GVL).
```csharp

VAR_GLOBAL
  	Robot1	:	TcoAbbRobotics.TcoIrc5_IO_v_1_x_x;
	Robot2	:	TcoAbbRobotics.TcoOmnicore_IO_v_1_x_x;
END_VAR
```
#### 2. Build the XAE project.

#### 3. Add Profinet master device, set its network adapter and network parameters.

#### 4. Scann for new Profinet devices, or use already prepared `xti` files and  use `Add Existing`. This files are localized in `.\src\TcoAbbRobotics\src\TcoAbbRoboticsConnector\ddf\`. `Robot1.xti` is valid for Irc5 and `Robot2.xti` for `Omnicore` 

#### 5. Connect your Gvl structures with  hardware. Refers to bechokff  documentation if there are some issues, or for guidance how to mapping. 

#### 6. Create the Function Block that extends the **`TcoCore.TcoContext`** function block.

#### 7. Inside the declaration part of the function block created, add an instance of the **`TcoAbbRobotics.TcoIrc5_v_1_x_x`**  or **`TcoAbbRobotics.TcoOmnicore_v_1_x_x`**. Add the **`Main`** method into this function block  and insert the instances. Call with passing the mapped hardware structure

```csharp
FUNCTION_BLOCK WpfContext EXTENDS TcoCore.TcoContext
VAR
     {attribute addProperty Name "<#Abb IRC 5#>"}
    _robot1 : TcoAbbRobotics.TcoIrc5_v_1_x_x(THIS^);
	{attribute addProperty Name "<#Abb Omnicore#>"}
    _robot2 : TcoAbbRobotics.TcoOmnicore_v_1_x_x(THIS^);


END_VAR

```

#### 8. Here  instances are called with passing the mapped hardware structure. By calling method `Service()` , all control elements of this component are accessible later in the visualization. By calling method `Service` is allowed control elements(components) via visualisation (service/ manual mode)

```csharp
/IF _serviceModeActive THEN
   	_robot1.Service();
  	_robot2.Service();
 


END_IF
_robot1(
    inoData := GVL.Robot1);


_robot2(
    inoData := GVL.Robot2);
```

#### 9. In the declaration part of the **`MAIN(PRG)`** create an instance of the function block created in the step 7 according to the example. 
```csharp
PROGRAM MAIN
VAR
      _wpfContext : WpfContext;
END_VAR
```

#### 10. Into the body of the **`MAIN(PRG)`** add the call of the **`Run()`** method of the instance created in the previous step, according to the example.    

```csharp
_wpfContext.Run();
```

#### 14. Build and save the XAE project.

#### 15. Activate configuration, load the PLC program and swith the PLC into the run mode.

-------------------------------

- ### .NET enviroment

--- 

#### **_Preconditions:_** All neccessary packages are installed, all neccessary references are set, connector to the target PLC is set.
- #### Implementation steps.

#### 1. Run the **`Vortex Builder`**.

#### 2. Into the **`MainWindow.xaml`** define **`DataContext`** to the **`MAIN`** of the **`TcoAbbRoboticsTestsPlc`**.
```xml
<Window x:Class="TcoAbbRobotics.Wpf.Sandbox.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:tcopen="clr-namespace:TcoAbbRoboticsTests;assembly=TcoAbbRoboticsTestsConnector"
        xmlns:vortex="http://vortex.mts/xaml" 
        Title="MainWindow"
        Width="800"
        Height="450"
        DataContext="{x:Static tcopen:Entry.TcoAbbRoboticsTestsPlc}"
        mc:Ignorable="d">


</Window>
```

```csharp
       static string TargetAmsId = Environment.GetEnvironmentVariable("Tc3Target");
        static int TargetAmsPort = 852;

        public static TcoAbbRoboticsTestsTwinController TcoAbbRoboticsTestsPlc { get; }
            = new TcoAbbRoboticsTestsTwinController(Vortex.Adapters.Connector.Tc3.Adapter.Tc3ConnectorAdapter.Create(TargetAmsId, TargetAmsPort, true));
```

#### 3. Into the container added, insert the **`RenderableContentControl`** and bind its **`DataContext`** to the **`   _robot1 : TcoAbbRobotics.TcoIrc5_v_1_x_x(THIS^)`** or **` _robot2 : TcoAbbRobotics.TcoOmnicore_v_1_x_x(THIS^)`**, using the **`PresentationType`** of the value **`Service`**.
```XML
    <vortex:RenderableContentControl Grid.Row="0" DataContext="{Binding MAIN._wpfContext._robot1}" PresentationType="Service"/>
	<vortex:RenderableContentControl Grid.Row="0" DataContext="{Binding MAIN._wpfContext._robot2}" PresentationType="Service"/>
```

```
#### 4. After starting the application and expanding the view, final view should look as follows:
```


Collapsed Service view 

![](assets/robotCollapsed.png)



Expanded (detailed info) view

![](assets/robotExpanded.png)

Service view report an error notification

![](assets/robotEstopActive.png)




### Plc Example usage in sequencer

```csharp
seq REF= _sequence1;

seq.Open();
seq.Observer := _observer;




 
		
IF (seq.Step(inStepID := 0,
    inEnabled := TRUE,
    inStepDescription := 'READY TO START')) THEN
    //--------------------------------------------------------
	_param.GlobalSpeed :=100;
		_noOfAttmets:=0;

	answer := _dialog			
			.Show()	
			.WithType(eDialogType.Question)				
			.WithCaption('Ready to go?')
			.WithText('Do you really want to start movements? Do we go ahead?')			
			.WithYesNo().Answer;
			//
			
	 IF (answer = TcoCore.eDialogAnswer.Yes) THEN    	 	
		seq.CompleteStep();
	 ELSIF(answer = TcoCore.eDialogAnswer.No) THEN
		_sequence1Task.Restore();
	 END_IF;	

    //--------------------------------------------------------			
END_IF;


IF (seq.Step(inStepID := 10,
    inEnabled := TRUE,
    inStepDescription := 'RESTORE')) THEN
//--------------------------------------------------------   
	
	_robot1.Restore();
	seq.CompleteStep();
	
		
//--------------------------------------------------------			
END_IF;
IF (seq.Step(inStepID := 11,
    inEnabled := TRUE,
    inStepDescription := 'STOP MOVEMENTS AND PROGRAM')) THEN
//--------------------------------------------------------   
	
	 	IF _robot1.StopMovementsAndProgram(inStopType:=eTcoRoboticsStopType.Soft).Done  THEN
			seq.CompleteStep();
		END_IF
		
//--------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 12,
    inEnabled := TRUE,
    inStepDescription := 'START AT MAIN')) THEN
//--------------------------------------------------------   
	
	 	IF _robot1.StartAtMain().Done  THEN
			seq.CompleteStep();
		END_IF;
		
//--------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 13,
    inEnabled := TRUE,
    inStepDescription := 'START MOTORS AND PROGRAM')) THEN
//--------------------------------------------------------   
	
	 	IF _robot1.StartMotorsAndProgram().Done  THEN
			seq.CompleteStep();
		END_IF;
		
//--------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 14,
    inEnabled := TRUE,
    inStepDescription := 'START MOVEMENTS')) THEN
//--------------------------------------------------------   
	_maxAllowedAttempts:=10;
	
	
	seq.CompleteStep();
	
	
//--------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 15,
    inEnabled := TRUE,
    inStepDescription := 'START MOVEMENTS')) THEN
//--------------------------------------------------------   
	_param.ActionNo:=100;
	
	IF _robot1.StartMovements(inData:=_param).Done   THEN
		seq.CompleteStep();
		_robot1.Restore();
	END_IF
	
//--------------------------------------------------------			
END_IF;


IF (seq.Step(inStepID := 16,
    inEnabled := TRUE,
    inStepDescription := 'START ANOTHER MOVE MOVEMENTS')) THEN
//--------------------------------------------------------   
	_param.ActionNo:=1;
	_param.GlobalSpeed:=90;
	IF _robot1.StartMovements(inData:=_param).Done  THEN
		IF _noOfAttmets<_maxAllowedAttempts THEN
			_noOfAttmets:=_noOfAttmets+1;
			seq.RequestStep(14);
			_robot1.Restore();
		ELSE 
		seq.CompleteStep();
		end_if;
	END_IF
	
//--------------------------------------------------------			
END_IF;















//
IF (seq.Step(inStepID := 500,
    inEnabled := TRUE,
    inStepDescription := 'EXAMPLE OF MOVMENT ABORTED')) THEN
//    --------------------------------------------------------

	answer := _dialog			
			.Show()	
			.WithType(eDialogType.Question)				
			.WithCaption('Abort movement?')
			.WithText('Do you  want to start movements and abort it?')			
			.WithYesNo().Answer;
			
			
	 IF (answer = TcoCore.eDialogAnswer.Yes) THEN    	 	
		seq.CompleteStep();
	 ELSIF(answer = TcoCore.eDialogAnswer.No) THEN
		_sequence1Task.Restore();
	 END_IF;	

//    --------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 525,
    inEnabled := TRUE,
    inStepDescription := 'STOP MOVEMENT  EXAMPLE')) THEN
//    --------------------------------------------------------    
		

		answer := _dialog			
			.Show()	
			.WithType(eDialogType.Question)				
			.WithCaption('Movement running')
			.WithText('Do you  want to abort movement?')			
			.WithYesNo().Answer;
			
			
		 IF (answer = TcoCore.eDialogAnswer.Yes) THEN 
			_robot1.StopMovements(inStopType:=TcoAbstractions.eTcoRoboticsStopType.Quick);   	 	
			seq.CompleteStep();
		 ELSIF(answer = TcoCore.eDialogAnswer.No) THEN
			_sequence1Task.Restore();
		 END_IF;	
		
	_param.ActionNo:=100;	
	_param.GlobalSpeed :=20;
	IF _robot1.StartMovements(inData:=_param).Done  THEN
		seq.CompleteStep();
	END_IF
	
		
		
//    --------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := 600,
    inEnabled := TRUE,
    inStepDescription := 'ASKING FOR RESTORING')) THEN
//    --------------------------------------------------------

	answer := _dialog			
			.Show()	
			.WithType(eDialogType.Question)				
			.WithCaption('Question?')
			.WithText('Do you want to restore TcoIrc5 component?')			
			.WithYesNo().Answer;
			
			
	 IF (answer = TcoCore.eDialogAnswer.Yes) THEN    	 	
		_robot1.Restore();
		seq.CompleteStep();
	 ELSIF(answer = TcoCore.eDialogAnswer.No) THEN
		_sequence1Task.Restore();
	 END_IF;	
//    --------------------------------------------------------			
END_IF;

IF (seq.Step(inStepID := seq.RESTORE_STEP_ID,
    inEnabled := TRUE,
    inStepDescription := 'RETURN TO THE START OF THE SEQUENCE')) THEN
    //--------------------------------------------------------	
    	seq.CompleteSequence();
    //--------------------------------------------------------			
END_IF;

seq.Close();

## OmniCore with RobotWare 8.2 - `TcoAbbRW_v_8_2_x`

`TcoAbbRobotics.TcoAbbRW_v_8_2_x` is the component for OmniCore controllers running RobotWare 8.2. It is used the same way as `TcoOmnicore_v_1_x_x` (steps above), with the hardware structure `TcoAbbRobotics.TcoAbbRW_IO_v_8_2_x`:

```csharp
VAR_GLOBAL
	Robot3	:	TcoAbbRobotics.TcoAbbRW_IO_v_8_2_x;
END_VAR

{attribute addProperty Name "<#Abb RW8#>"}
_robot3 : TcoAbbRobotics.TcoAbbRW_v_8_2_x(THIS^);

_robot3(inoData := GVL.Robot3);
```

Device description: `GSDML-V2.46-ABB-Robotics-OmniCore-20260330.xml` (`.\src\TcoAbbRobotics\assets\` and `.\src\TcoAbbRobotics\src\TcoAbbRoboticsConnector\ddf\`). Module configuration: `DI 64 bytes` + `DO 64 bytes`.

### Starting and stopping the program

RobotWare 8 accepts the start commands only from a control station that holds write access. The start tasks take and release write access themselves; the application only calls the task.

Required controller configuration (topic *Controller*, type *Control Station*): *Motion Control* = `Yes`, *Implicit Write Access* = `No`. The system input *Write Access*, the system output *Write Access Status* and the start inputs must belong to the same control station. Remote access must be allowed on the FlexPendant.

`StartProgram()` (continue) and `StartAtMain()` (start from the beginning):

1. release the stop commands (`StopProgram`, `QuickStop`, `SoftStop`, `StopCycle`, `StopInstruction`) - the controller rejects a start while one of them is set
2. wait for `AutoOn`; `StartAtMain` also waits until the program is stopped (`CycleOn` = 0), `StartProgram` is done at once when the program already runs
3. wait for `RemoteAccess` and `WriteAccessAvailable`, set `Control.WriteAccess`, wait for `State.WriteAccess`
4. wait for `MotorOnState`
5. one start pulse of 200 ms (`StartProgram` or `StartAtMain`), wait for `CycleOn`
6. reset `Control.WriteAccess`, wait for `State.WriteAccess` = 0

`StartAtMain` is done when the program runs (`CycleOn`), not when the program pointer has moved (`PpMoved`) as in `TcoOmnicore_v_1_x_x`.

`StartMotors()` does not need write access. It pulses `MotorOn` only when the safety circuit is closed (`SafetyOk`) and the controller is not in guard stop (`ResetState`). A guard stop can be left only by a safety reset, not by the PLC.

`StartMotorsAndProgram()` resets the error and the emergency stop, takes write access, switches the motors on, sends the start pulse and releases write access. `MotorOnAndStart` is not used.

The stop tasks (`StopProgram`, `StopMovements`, `StopMovementsAndProgram`, `StopMotors`) do not need write access. When a stop task starts, it resets the start commands and a write access request left by an interrupted start. Every stop task resets its stop commands when it is done, aborted or restored.

The controller gives no feedback for a rejected command except an event log entry. Each waiting step therefore reports what it is waiting for; set `Config.ErrorTime` to let a task end with an error instead of waiting.

### Differences against `TcoOmnicore_v_1_x_x` (RobotWare 7)

| Direction | Byte.Bit | RobotWare 7 | RobotWare 8.2 |
|---|---|---|---|
| PLC -> robot | 0.1 | `LoadProg` (di_Load) | `WriteAccess` (di_WriteAccess) |
| robot -> PLC | 1.4 | `SystemInputBusy` | `WriteAccessAvailable` (do_WriteAccessAvailable) |
| robot -> PLC | 1.7 | `BatteryLow` | `WriteAccess` (do_WriteAccess) |
| robot -> PLC | 2.0 | `InArea_1` | `RemoteAccess` (do_RemoteAccess) |
| robot -> PLC | 2.1 | `InArea_2` | `ResetState` (do_ResetState) |
| robot -> PLC | 2.2 | `InArea_3` | `InArea_1` |
| robot -> PLC | 2.3 | `InArea_4` | `InArea_2` |

`LoadProg`, `SystemInputBusy`, `BatteryLow`, `InArea_3` and `InArea_4` do not exist in the RobotWare 8.2 structures.

### Process image - PLC -> robot (`Out.Outputs`, `TcoAbbRW_Control_v_8_2_x`)

| Byte | Bit 0 | Bit 1 | Bit 2 | Bit 3 | Bit 4 | Bit 5 | Bit 6 | Bit 7 |
|---|---|---|---|---|---|---|---|---|
| 0 | Interupt | WriteAccess | LoadAndStartProg | MotorOff | MotorOn | MotorOnAndStart | ResetEmgStop | ResetError |
| 1 | StartProgram | StartAtMain | StopProgram | QuickStop | SoftStop | StopCycle | StopInstruction | LimitSpeed |
| 2 | Tool_1_Retract | Tool_1_Extend | Tool_2_Retract | Tool_2_Extend | Tool_3_Retract | Tool_3_Extend | Tool_4_Retract | Tool_4_Extend |

| Byte(s) | Member | Encoding |
|---|---|---|
| 3 | MasterMode | BYTE |
| 5 | Zone | BYTE |
| 6 | InPosition | BYTE |
| 7 | ActionNo | BYTE |
| 8 | GlobalSpeed | BYTE |
| 9 | ToolNo | BYTE |
| 10 | WorkobjectNo | BYTE |
| 11 | PointNo | BYTE |
| 12..15 | UserSpecSpeed1 | DINT, little endian |
| 16..19 | UserSpecSpeed2 | DINT, little endian |
| 20..31 | Coordinates X, Y, Z | DINT, little endian, (value + 10000.0) * 10000.0 |
| 32..43 | Coordinates Rx, Ry, Rz | DINT, little endian, (value + 360.0) * 10000.0 |

### Process image - robot -> PLC (`In.Inputs`, `TcoAbbRW_State_v_8_2_x`)

| Byte | Bit 0 | Bit 1 | Bit 2 | Bit 3 | Bit 4 | Bit 5 | Bit 6 | Bit 7 |
|---|---|---|---|---|---|---|---|---|
| 0 | AutoOn | CycleOn | EmgStop | Error | MoveActive | MoveInactive | MotorOff | MotorOn |
| 1 | MotorOffState | MotorOnState | ProgExecError | SafetyOk | WriteAccessAvailable | PpMoved | MotionSupervision | WriteAccess |
| 2 | RemoteAccess | ResetState | InArea_1 | InArea_2 | InPosition_1 | InPosition_2 | InPosition_3 | InPosition_4 |
| 3 | Tool_1_Retract | Tool_1_Extend | Tool_2_Retract | Tool_2_Extend | Tool_3_Retract | Tool_3_Extend | Tool_4_Retract | Tool_4_Extend |
| 4 | AutoStop_1 | AutoStop_2 | EmergencyStop_1 | EmergencyStop_2 | GeneralStop_1 | GeneralStop_2 | Enable_1 | Enable_2 |

| Byte(s) | Member | Encoding |
|---|---|---|
| 5 | Zone | BYTE |
| 6 | InPosition | BYTE |
| 7 | ActionNo | BYTE |
| 8 | GlobalSpeed | BYTE |
| 9 | ToolNo | BYTE |
| 10 | WorkobjectNo | BYTE |
| 11 | PointNo | BYTE |
| 12..15 | UserSpecSpeed1 | DINT, little endian |
| 16..19 | UserSpecSpeed2 | DINT, little endian |
| 20..31 | Coordinates X, Y, Z | DINT, little endian, value / 10000.0 - 10000.0 |
| 32..43 | Coordinates Rx, Ry, Rz | DINT, little endian, value / 10000.0 - 360.0 |
| 44..47 | EventId | UDINT, little endian |
