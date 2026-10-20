using NUnit.Framework;
using TcoKukaRoboticsTests;

namespace TcoKukaRoboticsUnitTests.PlcExecutedTests
{
    /// <summary>
    /// PLC executed tests of TcoKukaKssPn_v_9_3_x. Requires the TcoKukaRoboticsTests PLC running on the target set in the 'Tc3Target' environment variable.
    /// </summary>
    public class TcoKukaKssTests
    {
        TcoKukaKssTestContext sut = ConnectorFixture.Connector.MAIN._kssTestContext;

        [SetUp]
        public void TestSetup()
        {
            sut._simulate.Synchron = false;
            sut._simProActiveBlocked.Synchron = false;
            ResetRobotSide();
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.Restore);
            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);
        }

        private void ResetRobotSide()
        {
            var s = sut._sim;
            s.RcReady.Synchron = false;
            s.AlarmStopActive.Synchron = false;
            s.UserSafetySwitchClosed.Synchron = false;
            s.DrivesReady.Synchron = false;
            s.InterfaceActivated.Synchron = false;
            s.StopMess.Synchron = false;
            s.RobotStopped.Synchron = false;
            s.Manual.Synchron = false;
            s.LocalEstopOk.Synchron = false;
            s.ExternalAutomatic.Synchron = false;
            s.ProActive.Synchron = false;
            s.PpMoved.Synchron = false;
            s.Error.Synchron = false;
            s.NormalStop.Synchron = false;
            s.CutEnableDrives.Synchron = false;
            s.EventId.Synchron = 0;
            s.ActionNo.Synchron = 0;
            for (int i = 0; i < s.DataFromRobot.Length; i++)
                s.DataFromRobot[i].Synchron = 0;
        }

        private void HealthyRobot()
        {
            var s = sut._sim;
            s.AlarmStopActive.Synchron = true;
            s.UserSafetySwitchClosed.Synchron = true;
            s.LocalEstopOk.Synchron = true;
            s.ExternalAutomatic.Synchron = true;
            s.CutEnableDrives.Synchron = true;
            sut._simulate.Synchron = true;
        }

        private static bool Bit(byte value, int bit) => (value & (1 << bit)) != 0;

        private byte Out(int index) => sut._io.Out.Outputs[index].Synchron;

        [Test]
        [Timeout(10000)]
        public void InputsAreMappedToState()
        {
            var s = sut._sim;
            s.LocalEstopOk.Synchron = true;
            s.NormalStop.Synchron = true;
            s.CutEnableDrives.Synchron = true;
            s.StopMess.Synchron = true;
            s.ActionNo.Synchron = 42;
            s.EventId.Synchron = 0x01020304;
            s.DataFromRobot[0].Synchron = 0xAB;
            s.DataFromRobot[15].Synchron = 0xCD;

            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);

            var state = sut._sut._status.State;
            Assert.IsTrue(state.LocalEstopOk.Synchron);
            Assert.IsTrue(state.NormalStop.Synchron);
            Assert.IsTrue(state.CutEnableDrives.Synchron);
            Assert.IsTrue(state.StopMess.Synchron);
            Assert.IsFalse(state.Manual.Synchron);
            Assert.AreEqual(42, state.ActionNo.Synchron);
            Assert.AreEqual(0x01020304u, state.EventId.Synchron);
            Assert.AreEqual(0xAB, state.DataFromRobot[0].Synchron);
            Assert.AreEqual(0xCD, state.DataFromRobot[15].Synchron);
        }

        [Test]
        [Timeout(10000)]
        public void ControlIsMappedToOutputs()
        {
            var c = sut._sut._control;
            c.ActivateInterface.Synchron = true;
            c.StartAtMain.Synchron = true;
            c.ActionNo.Synchron = 7;
            c.DataToRobot[0].Synchron = 0x5A;
            c.DataToRobot[19].Synchron = 0xA5;

            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);

            Assert.IsTrue(Bit(Out(0), 5));
            Assert.IsTrue(Bit(Out(0), 6));
            Assert.AreEqual(7, Out(7));
            Assert.AreEqual(0x5A, Out(44));
            Assert.AreEqual(0xA5, Out(63));
        }

        [Test]
        [Timeout(10000)]
        public void MoveEnableFollowsManualModeEdges()
        {
            sut._sim.Manual.Synchron = true;
            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);
            Assert.IsTrue(sut._sut._control.MoveEnable.Synchron);

            // operator may drop MoveEnable while in manual mode, it is not forced every cycle
            sut._sut._control.MoveEnable.Synchron = false;
            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);
            Assert.IsFalse(sut._sut._control.MoveEnable.Synchron);

            sut._sut._control.MoveEnable.Synchron = true;
            sut._sim.Manual.Synchron = false;
            sut.ExecuteProbeRun(2, (int)eTcoKukaKssTests.Run);
            Assert.IsFalse(sut._sut._control.MoveEnable.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsFinishesWhenDrivesAreReady()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.StartMotors);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._taskError.Synchron);
            Assert.IsTrue(sut._sut._status.State.DrivesReady.Synchron);
            Assert.IsTrue(sut._sut._control.ActivateInterface.Synchron);
            Assert.IsFalse(sut._sut._control.DrivesOn.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsDoesNotPulseDrivesOnWithoutCutEnableDrives()
        {
            HealthyRobot();
            sut._sim.CutEnableDrives.Synchron = false;
            sut.ExecuteProbeRun(200, (int)eTcoKukaKssTests.StartMotors);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._drivesOnSeen.Synchron);
            Assert.IsFalse(sut._sut._status.State.DrivesReady.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsAndProgramKeepsInterfaceActive()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.StartMotorsAndProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.ProActive.Synchron);
            Assert.IsTrue(sut._sut._control.ActivateInterface.Synchron);
            Assert.IsFalse(sut._sut._control.ExternalStart.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsAndProgramDoesNotStartProgramOnNormalStop()
        {
            HealthyRobot();
            sut._sim.NormalStop.Synchron = true;
            sut.ExecuteProbeRun(300, (int)eTcoKukaKssTests.StartMotorsAndProgram);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._externalStartSeen.Synchron);
            Assert.IsFalse(sut._sut._status.State.ProActive.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramDoesNotSwitchDrivesOn()
        {
            HealthyRobot();
            sut.ExecuteProbeRun(200, (int)eTcoKukaKssTests.StartProgram);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._drivesOnSeen.Synchron);
            Assert.IsFalse(sut._externalStartSeen.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartAtMainFinishesWhenProgramPointerMoved()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.StartAtMain);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._sut._control.StartAtMain.Synchron);
        }

        [Test]
        [Timeout(60000)]
        public void StartMovementsRunsTheHandshake()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.StartMotorsAndProgram);
            Assert.IsTrue(sut._taskDone.Synchron);

            sut._moveParams.ActionNo.Synchron = 10;
            sut._moveParams.GlobalSpeed.Synchron = 50;
            sut._moveParams.ToolNo.Synchron = 1;
            sut.ExecuteProbeRun((int)eTcoKukaKssTests.StartMovements);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._taskError.Synchron);
            Assert.AreEqual(255, sut._sut._control.ActionNo.Synchron);
            Assert.AreEqual(50, sut._sut._status.State.GlobalSpeed.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void ResetAllOutputsKeepsUserData()
        {
            var c = sut._sut._control;
            c.ActivateInterface.Synchron = true;
            c.ActionNo.Synchron = 9;
            c.DataToRobot[3].Synchron = 0x33;

            sut.ExecuteProbeRun((int)eTcoKukaKssTests.ResetAllOutputs);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(c.ActivateInterface.Synchron);
            Assert.AreEqual(0, c.ActionNo.Synchron);
            Assert.AreEqual(0x33, c.DataToRobot[3].Synchron);
        }
    }
}
