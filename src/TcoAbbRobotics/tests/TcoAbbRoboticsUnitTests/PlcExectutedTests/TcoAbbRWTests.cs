using System;
using NUnit.Framework;
using TcoAbbRoboticsTests;

namespace TcoAbbRoboticsUnitTests.PlcExecutedTests
{
    /// <summary>
    /// PLC executed tests of TcoAbbRW_v_8_2_x. Requires the TcoAbbRoboticsTests PLC running on the target set in the 'Tc3Target' environment variable.
    /// </summary>
    public class TcoAbbRWTests
    {
        TcoAbbRWTestContext sut = ConnectorFixture.Connector.MAIN._rwTestContext;

        [SetUp]
        public void TestSetup()
        {
            sut._simulate.Synchron = false;
            sut._simWriteAccessTaken.Synchron = false;
            sut._simStartBlocked.Synchron = false;
            sut._stopType.Synchron = 0;
            sut._sut._config.ErrorTime.Synchron = TimeSpan.Zero;
            ResetRobotSide();
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.Restore);
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);
        }

        private void ResetRobotSide()
        {
            var s = sut._sim;
            s.AutoOn.Synchron = false;
            s.CycleOn.Synchron = false;
            s.EmgStop.Synchron = false;
            s.Error.Synchron = false;
            s.MoveActive.Synchron = false;
            s.MoveInactive.Synchron = false;
            s.MotorOff.Synchron = false;
            s.MotorOn.Synchron = false;
            s.MotorOffState.Synchron = false;
            s.MotorOnState.Synchron = false;
            s.ProgExecError.Synchron = false;
            s.SafetyOk.Synchron = false;
            s.WriteAccessAvailable.Synchron = false;
            s.PpMoved.Synchron = false;
            s.MotionSupervision.Synchron = false;
            s.WriteAccess.Synchron = false;
            s.RemoteAccess.Synchron = false;
            s.ResetState.Synchron = false;
            s.InArea_1.Synchron = false;
            s.InArea_2.Synchron = false;
            s.EventId.Synchron = 0;
            s.ActionNo.Synchron = 0;
        }

        /// <summary>
        /// Robot in automatic mode, safety circuit closed, not in guard stop, remote access allowed, motors off, program stopped.
        /// </summary>
        private void HealthyRobot()
        {
            var s = sut._sim;
            s.AutoOn.Synchron = true;
            s.SafetyOk.Synchron = true;
            s.ResetState.Synchron = true;
            s.RemoteAccess.Synchron = true;
            sut._simulate.Synchron = true;
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);
        }

        private void HealthyRobotWithMotorsOn()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartMotors);
            Assert.IsTrue(sut._taskDone.Synchron, "arrange: motors on");
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.Restore);
        }

        private void HealthyRobotWithProgramRunning()
        {
            HealthyRobot();
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartMotorsAndProgram);
            Assert.IsTrue(sut._taskDone.Synchron, "arrange: program running");
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.Restore);
        }

        private static bool Bit(byte value, int bit) => (value & (1 << bit)) != 0;

        private byte Out(int index) => sut._io.Out.Outputs[index].Synchron;

        private bool WriteAccessOutput => Bit(Out(0), 1);

        private void AssertNoStartCommandIsSet()
        {
            Assert.IsFalse(WriteAccessOutput, "WriteAccess");
            Assert.IsFalse(Bit(Out(0), 4), "MotorOn");
            Assert.IsFalse(Bit(Out(1), 0), "StartProgram");
            Assert.IsFalse(Bit(Out(1), 1), "StartAtMain");
        }

        private void AssertNoStopCommandIsSet()
        {
            Assert.IsFalse(Bit(Out(0), 3), "MotorOff");
            Assert.IsFalse(Bit(Out(1), 2), "StopProgram");
            Assert.IsFalse(Bit(Out(1), 3), "QuickStop");
            Assert.IsFalse(Bit(Out(1), 4), "SoftStop");
        }

        [Test]
        [Timeout(10000)]
        public void WriteAccessSignalsAreMapped()
        {
            var s = sut._sim;
            s.WriteAccessAvailable.Synchron = true;
            s.WriteAccess.Synchron = true;
            s.RemoteAccess.Synchron = true;
            s.ResetState.Synchron = true;
            s.InArea_1.Synchron = true;
            sut._sut._control.WriteAccess.Synchron = true;

            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);

            var state = sut._sut._status.State;
            Assert.IsTrue(state.WriteAccessAvailable.Synchron);
            Assert.IsTrue(state.WriteAccess.Synchron);
            Assert.IsTrue(state.RemoteAccess.Synchron);
            Assert.IsTrue(state.ResetState.Synchron);
            Assert.IsTrue(state.InArea_1.Synchron);
            Assert.IsFalse(state.InArea_2.Synchron);
            Assert.IsTrue(WriteAccessOutput);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsDoesNotNeedWriteAccess()
        {
            HealthyRobot();
            sut._simWriteAccessTaken.Synchron = true;

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartMotors);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.MotorOnState.Synchron);
            Assert.IsFalse(sut._writeAccessSeen.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsDoesNotPulseMotorOnInGuardStop()
        {
            HealthyRobot();
            sut._sim.ResetState.Synchron = false;

            sut.ExecuteProbeRun(200, (int)eTcoAbbRWTests.StartMotors);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._motorOnSeen.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsDoesNotPulseMotorOnWithSafetyCircuitOpen()
        {
            HealthyRobot();
            sut._sim.SafetyOk.Synchron = false;

            sut.ExecuteProbeRun(200, (int)eTcoAbbRWTests.StartMotors);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._motorOnSeen.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramTakesWriteAccessAndReleasesItWhenProgramRuns()
        {
            HealthyRobotWithMotorsOn();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.CycleOn.Synchron);
            Assert.IsTrue(sut._writeAccessSeen.Synchron);
            Assert.AreEqual(1, sut._startProgramPulses.Synchron);
            Assert.AreEqual(0, sut._startAtMainPulses.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron);
            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartAtMainTakesWriteAccessAndReleasesItWhenProgramRuns()
        {
            HealthyRobotWithMotorsOn();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartAtMain);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.CycleOn.Synchron);
            Assert.AreEqual(0, sut._startProgramPulses.Synchron);
            Assert.AreEqual(1, sut._startAtMainPulses.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron);
            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartMotorsAndProgramTakesWriteAccessBeforeMotorsOn()
        {
            HealthyRobot();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartMotorsAndProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.MotorOnState.Synchron);
            Assert.IsTrue(sut._sut._status.State.CycleOn.Synchron);
            Assert.AreEqual(1, sut._startProgramPulses.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron);
            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramWaitsWhileWriteAccessIsNotAvailable()
        {
            HealthyRobotWithMotorsOn();
            sut._simWriteAccessTaken.Synchron = true;

            sut.ExecuteProbeRun(200, (int)eTcoAbbRWTests.StartProgram);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._writeAccessSeen.Synchron);
            Assert.AreEqual(0, sut._startProgramPulses.Synchron);

            // write access becomes available, the task continues
            sut._simWriteAccessTaken.Synchron = false;
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.AreEqual(1, sut._startProgramPulses.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramDoesNotRequestWriteAccessWithoutRemoteAccess()
        {
            HealthyRobotWithMotorsOn();
            sut._sim.RemoteAccess.Synchron = false;

            sut.ExecuteProbeRun(200, (int)eTcoAbbRWTests.StartProgram);

            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsFalse(sut._writeAccessSeen.Synchron);
            Assert.AreEqual(0, sut._startProgramPulses.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramEndsWithErrorAndReleasesWriteAccessWhenRobotDoesNotStart()
        {
            HealthyRobotWithMotorsOn();
            sut._simStartBlocked.Synchron = true;
            sut._sut._config.ErrorTime.Synchron = TimeSpan.FromSeconds(2);

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);
            sut.ExecuteProbeRun(5, (int)eTcoAbbRWTests.Run);

            Assert.IsTrue(sut._taskError.Synchron);
            Assert.IsFalse(sut._taskDone.Synchron);
            Assert.IsTrue(sut._writeAccessSeen.Synchron);
            Assert.AreEqual(1, sut._startProgramPulses.Synchron, "single start pulse, no retry");
            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramReleasesWriteAccessWhenAborted()
        {
            HealthyRobotWithMotorsOn();
            sut._simStartBlocked.Synchron = true;

            sut.ExecuteProbeRun(100, (int)eTcoAbbRWTests.StartProgram);
            Assert.IsTrue(WriteAccessOutput, "write access is held while the task waits for the program");

            sut._sut._startProgramTask._abortRequest.Synchron = true;
            sut.ExecuteProbeRun(5, (int)eTcoAbbRWTests.Run);

            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramReleasesStopCommandsBeforeStart()
        {
            HealthyRobotWithMotorsOn();
            sut._sut._control.StopProgram.Synchron = true;
            sut._sut._control.QuickStop.Synchron = true;
            sut._sut._control.SoftStop.Synchron = true;
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron, "start pulse sent while a stop command was set");
            AssertNoStopCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartProgramIsDoneWithoutWriteAccessWhenProgramAlreadyRuns()
        {
            HealthyRobotWithProgramRunning();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._writeAccessSeen.Synchron);
            Assert.AreEqual(0, sut._startProgramPulses.Synchron);
        }

        [Test]
        [Timeout(30000)]
        public void StopProgramDoesNotNeedWriteAccess()
        {
            HealthyRobotWithProgramRunning();
            sut._simWriteAccessTaken.Synchron = true;

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StopProgram);
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._sut._status.State.CycleOn.Synchron);
            Assert.IsFalse(sut._writeAccessSeen.Synchron);
            AssertNoStopCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StopMovementsAndProgramReleasesItsStopCommands()
        {
            HealthyRobotWithProgramRunning();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StopMovementsAndProgram);
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._sut._status.State.CycleOn.Synchron);
            AssertNoStopCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StopMotorsReleasesMotorOff()
        {
            HealthyRobotWithMotorsOn();

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StopMotors);
            sut.ExecuteProbeRun(2, (int)eTcoAbbRWTests.Run);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.MotorOffState.Synchron);
            AssertNoStopCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StopProgramReleasesWriteAccessLeftByInterruptedStart()
        {
            HealthyRobotWithMotorsOn();
            sut._simStartBlocked.Synchron = true;
            sut.ExecuteProbeRun(100, (int)eTcoAbbRWTests.StartProgram);
            Assert.IsTrue(WriteAccessOutput, "write access is held while the task waits for the program");

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StopProgram);
            sut.ExecuteProbeRun(5, (int)eTcoAbbRWTests.Run);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsFalse(sut._sut._status.State.WriteAccess.Synchron);
            AssertNoStartCommandIsSet();
            AssertNoStopCommandIsSet();
        }

        [Test]
        [Timeout(30000)]
        public void StartAfterStopIsAccepted()
        {
            HealthyRobotWithProgramRunning();
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StopProgram);
            Assert.IsTrue(sut._taskDone.Synchron, "arrange: program stopped");
            sut.ExecuteProbeRun((int)eTcoAbbRWTests.Restore);

            sut.ExecuteProbeRun((int)eTcoAbbRWTests.StartProgram);

            Assert.IsTrue(sut._taskDone.Synchron);
            Assert.IsTrue(sut._sut._status.State.CycleOn.Synchron);
            Assert.AreEqual(0, sut._startRejected.Synchron);
        }
    }
}
