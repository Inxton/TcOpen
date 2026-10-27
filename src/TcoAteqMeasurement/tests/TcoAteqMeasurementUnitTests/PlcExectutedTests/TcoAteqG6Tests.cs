using System;
using NUnit.Framework;
using TcoAteqMeasurement;
using TcoAteqMeasurementTests;

namespace TcoAteqMeasurementUnitTests.PlcExecutedTests
{
    /// <summary>
    /// PLC executed tests of TcoAteqG6Pn_v_3_5_x (ports of the AX AxoG6 codec, task and coordination tests).
    /// Requires the TcoAteqMeasurementTests PLC running on the target set in the 'Tc3Target' environment variable.
    /// </summary>
    public class TcoAteqG6Tests
    {
        TcoAteqG6TestContext sut = ConnectorFixture.Connector.MAIN._g6TestContext;

        [SetUp]
        public void TestSetup()
        {
            sut._simulate.Synchron = false;
            sut.ExecuteProbeRun((int)eTcoAteqG6Tests.Restore);
            sut._sut._config.CommandTimeout.Synchron = TimeSpan.FromMilliseconds(500);
            sut._sut._config.CycleTimeout.Synchron = TimeSpan.FromMilliseconds(1000);
            sut._sut._config.ErrorTime.Synchron = TimeSpan.Zero;
            sut._sut._control.ProgramNumber.Synchron = 0;
            sut._sut._control.SpecialCycle.Synchron = (short)eTcoAteqG6_SpecialCycle_v_3_5_x.None;
            sut._sut._control.QueryParameter.Synchron = (ushort)eTcoAteqG6_ParameterId_v_3_5_x.None;
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
        }

        private void HealthyInstrument()
        {
            sut._simulate.Synchron = true;
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
        }

        private void In(int offset, params byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                sut._inImage[offset + i].Synchron = bytes[i];
        }

        private void InDint(int offset, int value) => In(offset, BitConverter.GetBytes(value));

        private byte Out(int offset) => sut._outImage[offset].Synchron;

        private ushort Command => sut._sut._control.Command.Synchron;

        private void Run(eTcoAteqG6Tests test) => sut.ExecuteProbeRun((ulong)test);

        private void AssertDone()
        {
            Assert.IsTrue(sut._taskDone.Synchron, "task not done");
            Assert.IsFalse(sut._taskError.Synchron, "task in error");
        }

        private void AssertError()
        {
            Assert.IsTrue(sut._taskError.Synchron, "task not in error");
            Assert.IsFalse(sut._taskDone.Synchron, "task done");
        }

        // ------------------------------------------------------------ codec / mapping

        [Test, Timeout(10000)]
        public void InputImageIsDecodedIntoState()
        {
            In(0x00, 0x08, 0x80);           // echo: program selection + read last result
            In(0x02, 0xFF, 0xFF);           // error word FFFFh = command running
            In(0x06, 0x04, 0x00);           // program 4 on the bus = 5
            In(0x08, 0x07, 0x00);           // results in FIFO
            In(0x0C, 0x21, 0x82);           // pass, cycle end, ATR error (bit 9), key presence (bit 15)
            In(0x0E, 0x03, 0x00);           // step 3 = stabilization
            InDint(0x10, 207000);           // pressure 207
            InDint(0x14, 14000);            // mbar
            InDint(0x18, -108);             // flow -0.108
            InDint(0x1C, 84000);            // SCCM

            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);

            var s = sut._sut._status.State;
            Assert.AreEqual(0x8008, s.Echo.Raw.Synchron);
            Assert.IsTrue(s.Echo.ProgramSelection.Synchron);
            Assert.IsTrue(s.Echo.ReadLastResult.Synchron);
            Assert.IsFalse(s.Echo.Reset.Synchron);
            Assert.IsTrue(s.CommandRunning.Synchron);
            Assert.AreEqual(5, s.CurrentProgram.Synchron);
            Assert.AreEqual(7, s.ResultsInFifo.Synchron);
            Assert.IsTrue(s.StatusBits.Pass.Synchron);
            Assert.IsTrue(s.StatusBits.CycleEnd.Synchron);
            Assert.IsTrue(s.StatusBits.AtrError.Synchron);
            Assert.IsTrue(s.StatusBits.KeyPresence.Synchron);
            Assert.IsFalse(s.StatusBits.Alarm.Synchron);
            Assert.AreEqual((short)eTcoAteqG6_Step_v_3_5_x.Stabilization, s.Step.Synchron);
            Assert.AreEqual(207.0f, s.RealTime.Pressure.Synchron, 0.0001f);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.mbar, s.RealTime.PressureUnit.Synchron);
            Assert.AreEqual(-0.108f, s.RealTime.Flow.Synchron, 0.0001f);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.SCCM, s.RealTime.FlowUnit.Synchron);
            Assert.IsFalse(sut._sut._status.Ready.Synchron, "command running -> not ready");
        }

        [Test, Timeout(10000)]
        public void UnknownCodesDecodeToUnknown()
        {
            In(0x0E, 0x07, 0x00);           // step 7
            InDint(0x14, 1500);             // not a multiple of 1000
            InDint(0x1C, 99000);            // not in the unit table
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);

            var s = sut._sut._status.State;
            Assert.AreEqual((short)eTcoAteqG6_Step_v_3_5_x.Unknown, s.Step.Synchron);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.Unknown, s.RealTime.PressureUnit.Synchron);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.Unknown, s.RealTime.FlowUnit.Synchron);

            In(0x0E, 0xFF, 0xFF);           // FFFFh = no cycle
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual((short)eTcoAteqG6_Step_v_3_5_x.None, s.Step.Synchron);
        }

        [Test, Timeout(10000)]
        public void ReadyFollowsCycleEndAndCommandRunning()
        {
            In(0x0C, 0x20, 0x00);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.IsTrue(sut._sut._status.Ready.Synchron);
        }

        [Test, Timeout(10000)]
        public void ControlIsEncodedIntoOutputImage()
        {
            var c = sut._sut._control;
            c.ProgramNumber.Synchron = 3;
            c.SpecialCycle.Synchron = (short)eTcoAteqG6_SpecialCycle_v_3_5_x.PiezoAutoZero;
            c.SpecialCycleInstruction.Synchron = 5;
            c.ParameterRequest.Count.Synchron = 3;
            c.ParameterRequest.Ids[0].Synchron = 1;
            c.ParameterRequest.Ids[1].Synchron = 3;
            c.ParameterRequest.Ids[2].Synchron = 60;

            sut.ExecuteProbeRun(3, (int)eTcoAteqG6Tests.Run);

            Assert.AreEqual(0x02, Out(0x06), "program 3 is 2 on the bus");
            Assert.AreEqual(0x00, Out(0x07));
            Assert.AreEqual(9, Out(0x08), "special cycle id");
            Assert.AreEqual(5, Out(0x09), "special cycle instruction");
            Assert.AreEqual(3, Out(0x20), "parameter count");
            Assert.AreEqual(1, Out(0x22));
            Assert.AreEqual(3, Out(0x24));
            Assert.AreEqual(60, Out(0x26));
            Assert.AreEqual(0, Out(0x28));
        }

        [Test, Timeout(10000)]
        public void CommandWordIsLittleEndian()
        {
            sut._sut._control.Command.Synchron = 0x8006;
            sut.ExecuteProbeRun(3, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual(0x06, Out(0x00));
            Assert.AreEqual(0x80, Out(0x01));
            Assert.IsTrue(sut._sut._control.CommandBits.Start.Synchron);
            Assert.IsTrue(sut._sut._control.CommandBits.SpecialCycle.Synchron);
            Assert.IsTrue(sut._sut._control.CommandBits.ReadLastResult.Synchron);
            sut._sut._control.Command.Synchron = 0;
        }

        // ------------------------------------------------------------ handshake (via Reset)

        [Test, Timeout(10000)]
        public void ResetRunsOneHandshake()
        {
            HealthyInstrument();
            Run(eTcoAteqG6Tests.Reset);

            AssertDone();
            Assert.AreEqual(0, Command, "command bit dropped");
            Assert.AreEqual(1, sut._sim.RisingEdges.Synchron, "exactly one rising edge");
            Assert.AreEqual(0x0001, sut._sim.LastRaised.Synchron);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual(0, sut._sim.Echo.Synchron, "echo released");
        }

        [Test, Timeout(10000)]
        public void CommandErrorFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.FailMask.Synchron = 0x0001;
            Run(eTcoAteqG6Tests.Reset);

            AssertError();
            Assert.AreEqual(0x0001, sut._sut._status.LastCommandError.Synchron);
            Assert.AreEqual(0, Command, "command bit dropped on error");
        }

        [Test, Timeout(10000)]
        public void SilentInstrumentFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.SilentMask.Synchron = 0x0001;
            Run(eTcoAteqG6Tests.Reset);

            AssertError();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void NeverCompletingCommandFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.CommandCycles.Synchron = 10000;
            Run(eTcoAteqG6Tests.Reset);

            AssertError();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void StuckEchoFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.StuckEchoMask.Synchron = 0x0001;
            Run(eTcoAteqG6Tests.Reset);

            AssertError();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void EchoStillHighRaisesNoEdge()
        {
            HealthyInstrument();
            sut._sim.Echo.Synchron = 0x0001;
            Run(eTcoAteqG6Tests.Reset);

            AssertError();
            Assert.AreEqual(0, sut._sim.RisingEdges.Synchron);
        }

        [Test, Timeout(10000)]
        public void RestoreDropsTheCommandBit()
        {
            HealthyInstrument();
            sut._sim.CommandCycles.Synchron = 10000;
            sut._sut._config.CommandTimeout.Synchron = TimeSpan.FromSeconds(30);
            Run(eTcoAteqG6Tests.InvokeReset);
            sut.ExecuteProbeRun(5, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual(0x0001, Command, "command bit raised while running");

            Run(eTcoAteqG6Tests.Restore);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual(0, Command);
            Assert.IsFalse(sut._resetBusy.Synchron);
        }

        // ------------------------------------------------------------ select program

        [Test, Timeout(10000)]
        public void SelectProgramSwitchesTheProgram()
        {
            HealthyInstrument();
            sut._program.Synchron = 5;
            Run(eTcoAteqG6Tests.SelectProgram);

            AssertDone();
            Assert.AreEqual(4, sut._sim.BusProgram.Synchron);
            Assert.AreEqual(0x0008, sut._sim.LastRaised.Synchron);
            Assert.AreEqual(5, sut._sut._status.State.CurrentProgram.Synchron);
        }

        [TestCase(0)]
        [TestCase(129)]
        [Timeout(10000)]
        public void SelectProgramOutOfRangeRaisesNoEdge(int program)
        {
            HealthyInstrument();
            sut._program.Synchron = (short)program;
            Run(eTcoAteqG6Tests.SelectProgram);

            AssertError();
            Assert.AreEqual(0, sut._sim.RisingEdges.Synchron);
        }

        [Test, Timeout(10000)]
        public void IgnoredProgramSelectionFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.IgnoreProgramSelection.Synchron = true;
            sut._program.Synchron = 5;
            Run(eTcoAteqG6Tests.SelectProgram);

            AssertError();
        }

        // ------------------------------------------------------------ start cycle

        [Test, Timeout(10000)]
        public void StartCycleReadsTheResult()
        {
            HealthyInstrument();
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertDone();
            var r = sut._sut._status.LastResult;
            Assert.IsTrue(r.Valid.Synchron);
            Assert.IsTrue(r.Pass.Synchron);
            Assert.AreEqual(3, r.ProgramNo.Synchron);
            Assert.AreEqual(207.0f, r.Measurement.Pressure.Synchron, 0.0001f);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.mbar, r.Measurement.PressureUnit.Synchron);
            Assert.AreEqual(-0.108f, r.Measurement.Flow.Synchron, 0.0001f);
            Assert.AreEqual((short)eTcoAteqG6_Unit_v_3_5_x.cm3_min, r.Measurement.FlowUnit.Synchron);
            Assert.AreEqual(0, Command);
            Assert.AreEqual(0x800A, sut._sim.RaisedUnion.Synchron, "only select, start and read last result");
            Assert.AreEqual(3, sut._sim.RisingEdges.Synchron);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.IsTrue(sut._sut._status.LastPass.Synchron);
        }

        [Test, Timeout(10000)]
        public void StartCycleWaitsForInstrumentReady()
        {
            HealthyInstrument();
            sut._sim.CycleEnd.Synchron = false;
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertError();
            Assert.AreEqual(0, sut._sim.RisingEdges.Synchron);
        }

        [Test, Timeout(10000)]
        public void CycleThatNeverStartsFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.NoCycle.Synchron = true;
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertError();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void CycleThatNeverEndsFailsTheTask()
        {
            HealthyInstrument();
            sut._sim.EndlessCycle.Synchron = true;
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertError();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void StaleResultIsRefused()
        {
            HealthyInstrument();
            sut._sim.ResultProgramOffset.Synchron = 1;
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertError();
            Assert.IsFalse(sut._sut._status.LastResult.Valid.Synchron);
        }

        [Test, Timeout(10000)]
        public void AlarmResultIsInvalid()
        {
            HealthyInstrument();
            sut._sim.ResultRelay.Synchron = 0x0008;
            sut._sim.ResultAlarm.Synchron = 44;
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.StartCycle);

            AssertDone();
            var r = sut._sut._status.LastResult;
            Assert.IsFalse(r.Valid.Synchron);
            Assert.IsTrue(r.Alarm.Synchron);
            Assert.AreEqual((short)eTcoAteqG6_AlarmCode_v_3_5_x.PressureTooLow, r.AlarmCode.Synchron);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.IsFalse(sut._sut._status.LastPass.Synchron);
        }

        [Test, Timeout(10000)]
        public void ReadLastResultDecodesTheExchangeZone()
        {
            HealthyInstrument();
            sut._sim.BusProgram.Synchron = 2;
            Run(eTcoAteqG6Tests.ReadLastResult);

            AssertDone();
            Assert.AreEqual(0x8000, sut._sim.LastRaised.Synchron);
            Assert.AreEqual(3, sut._sut._status.LastResult.ProgramNo.Synchron);
            Assert.IsTrue(sut._sut._status.LastResult.Valid.Synchron);
        }

        // ------------------------------------------------------------ special cycle

        [Test, Timeout(10000)]
        public void SpecialCycleRaisesStartAndSpecialTogether()
        {
            HealthyInstrument();
            sut._specialCycle.Synchron = (short)eTcoAteqG6_SpecialCycle_v_3_5_x.PiezoAutoZero;
            sut._instruction.Synchron = 0;
            Run(eTcoAteqG6Tests.SpecialCycle);

            AssertDone();
            Assert.AreEqual(0x0006, sut._sim.LastRaised.Synchron);
            Assert.AreEqual(1, sut._sim.RisingEdges.Synchron);
            Assert.AreEqual(9, sut._sim.LastSpecialId.Synchron);
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void SpecialCycleNoneRaisesNoEdge()
        {
            HealthyInstrument();
            sut._specialCycle.Synchron = (short)eTcoAteqG6_SpecialCycle_v_3_5_x.None;
            Run(eTcoAteqG6Tests.SpecialCycle);

            AssertError();
            Assert.AreEqual(0, sut._sim.RisingEdges.Synchron);
        }

        // ------------------------------------------------------------ parameters

        [Test, Timeout(10000)]
        public void ReadParametersDecodesTheList()
        {
            HealthyInstrument();
            sut._request.Count.Synchron = 2;
            sut._request.Ids[0].Synchron = 3;
            sut._request.Ids[1].Synchron = 2;
            Run(eTcoAteqG6Tests.ReadParameters);

            AssertDone();
            Assert.AreEqual(0x0020, sut._sim.LastRaised.Synchron);
            var list = sut._sut._status.Parameters;
            Assert.AreEqual(2, list.Count.Synchron);
            Assert.AreEqual(3, list.Items[0].Id.Synchron);
            Assert.AreEqual(3.0f, list.Items[0].Value.Synchron, 0.0001f);
            Assert.AreEqual(2, list.Items[1].Id.Synchron);
            Assert.AreEqual(2000, list.Items[1].Raw.Synchron);
        }

        [Test, Timeout(10000)]
        public void EmptyParameterRequestRaisesNoEdge()
        {
            HealthyInstrument();
            sut._request.Count.Synchron = 0;
            Run(eTcoAteqG6Tests.ReadParameters);

            AssertError();
            Assert.AreEqual(0, sut._sim.RisingEdges.Synchron);
        }

        [Test, Timeout(10000)]
        public void PresetParametersAreMapped()
        {
            HealthyInstrument();
            Run(eTcoAteqG6Tests.ReadPresetParameters);

            AssertDone();
            Assert.AreEqual(11, sut._sut._control.ParameterRequest.Count.Synchron);
            Assert.AreEqual(53, sut._sut._control.ParameterRequest.Ids[7].Synchron);
            Assert.AreEqual(66, sut._sut._control.ParameterRequest.Ids[10].Synchron);
            var set = sut._sut._status.ParameterSet;
            // the simulator answers value = identifier
            Assert.AreEqual(1.0f, set.FillTime.Synchron, 0.0001f);
            Assert.AreEqual(2.0f, set.StabTime.Synchron, 0.0001f);
            Assert.AreEqual(3.0f, set.TestTime.Synchron, 0.0001f);
            Assert.AreEqual(9.0f, set.DumpTime.Synchron, 0.0001f);
            Assert.AreEqual(60.0f, set.TestFail.Synchron, 0.0001f);
            Assert.AreEqual(66.0f, set.SetFill.Synchron, 0.0001f);
            Assert.AreEqual((short)eTcoAteqG6_TestType_v_3_5_x.Unknown, set.TestType.Synchron, "21000 is not a test type code");
        }

        [Test, Timeout(10000)]
        public void SingleParameterIsQueried()
        {
            HealthyInstrument();
            sut._parameter.Synchron = (short)eTcoAteqG6_ParameterId_v_3_5_x.TestFail;
            Run(eTcoAteqG6Tests.ReadParameter);

            AssertDone();
            var q = sut._sut._status.ParameterQuery;
            Assert.IsTrue(q.Found.Synchron);
            Assert.AreEqual(60.0f, q.Value.Synchron, 0.0001f);
        }

        [Test, Timeout(10000)]
        public void QueriedParameterNotInRequestIsNotFound()
        {
            HealthyInstrument();
            sut._sut._control.QueryParameter.Synchron = (ushort)eTcoAteqG6_ParameterId_v_3_5_x.SetFill;
            sut._request.Count.Synchron = 1;
            sut._request.Ids[0].Synchron = 3;
            Run(eTcoAteqG6Tests.ReadParameters);

            AssertDone();
            Assert.IsFalse(sut._sut._status.ParameterQuery.Found.Synchron);
        }

        // ------------------------------------------------------------ coordination

        [Test, Timeout(10000)]
        public void BusyCommandBlocksOtherCommandsAndKeepsItsData()
        {
            HealthyInstrument();
            sut._sim.EndlessCycle.Synchron = true;
            sut._sut._config.CycleTimeout.Synchron = TimeSpan.FromSeconds(30);
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.InvokeStartCycle);
            sut.ExecuteProbeRun(20, (int)eTcoAteqG6Tests.Run);
            Assert.IsTrue(sut._startCycleBusy.Synchron);

            sut._program.Synchron = 7;
            Run(eTcoAteqG6Tests.InvokeSelectProgram);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);

            Assert.IsFalse(sut._selectProgramBusy.Synchron, "select program is disabled");
            Assert.AreEqual(3, sut._sut._control.ProgramNumber.Synchron, "running command data is not overwritten");
        }

        [Test, Timeout(10000)]
        public void ResetAbortsARunningCommand()
        {
            HealthyInstrument();
            sut._sim.EndlessCycle.Synchron = true;
            sut._sut._config.CycleTimeout.Synchron = TimeSpan.FromSeconds(30);
            sut._program.Synchron = 3;
            Run(eTcoAteqG6Tests.InvokeStartCycle);
            sut.ExecuteProbeRun(20, (int)eTcoAteqG6Tests.Run);
            Assert.IsTrue(sut._startCycleBusy.Synchron);

            Run(eTcoAteqG6Tests.InvokeReset);
            sut.ExecuteProbeRun(1, (int)eTcoAteqG6Tests.Run);
            Assert.IsFalse(sut._startCycleBusy.Synchron, "start cycle aborted");
            Assert.AreEqual(0x01, Out(0x00), "only the reset bit is on the wire");

            Run(eTcoAteqG6Tests.Reset);
            AssertDone();
            Assert.AreEqual(0, Command);
        }

        [Test, Timeout(10000)]
        public void RestoreClearsCommandAndError()
        {
            HealthyInstrument();
            sut._sim.FailMask.Synchron = 0x0001;
            Run(eTcoAteqG6Tests.Reset);
            AssertError();

            Run(eTcoAteqG6Tests.Restore);
            sut.ExecuteProbeRun(2, (int)eTcoAteqG6Tests.Run);
            Assert.AreEqual(0, Command);
            Assert.AreEqual(0, sut._sut._status.LastCommandError.Synchron);
            Assert.IsFalse(sut._resetBusy.Synchron);
        }
    }
}
