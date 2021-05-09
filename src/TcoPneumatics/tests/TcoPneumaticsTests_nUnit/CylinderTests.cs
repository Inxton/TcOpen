using NUnit.Framework;
using TcoPneumatics;
using Tc.Prober.Runners;
using Tc.Prober.Recorder;
using System.Reflection;
using System.IO;
using TcoPneumaticsTests;

namespace TcoPneumaticsTests
{
    public class CylinderTests
    {
        Cylinder sut=ConnectorFixture.Connector.MAIN._defaultContext._wpfCyclinder;

        [OneTimeSetUp()]
        public void OneTimeSetUp()
        {
            //Entry.TcoPneumaticsTestsPlc.Connector.BuildAndStart();
            //sut = Entry.TcoPneumaticsTestsPlc.MAIN._defaultContext._wpfCyclinder;
            var executingAssembly = new FileInfo(Assembly.GetExecutingAssembly().Location);
            Runner.RecordingsShell = Path.GetFullPath(Path.Combine(executingAssembly.DirectoryName, @"..\..\..\recodrings"));            
        }

        [SetUp]
        public void TestSetup()
        {
            sut.Run(a => a._StopTest());
        }

        [Test]
        // [Timeout(10000)]
        public void MoveCylinderToHomeTest()
        {
            sut.Run(a =>
            {
                var done = a._MoveToHomeTest();
                //Entry.TcoPneumaticsTestsPlc.IO.A1[0].Synchron = true;
                //Entry.TcoPneumaticsTestsPlc.IO.A1[1].Synchron = false;
                ConnectorFixture.Connector.IO.A1[0].Synchron = true;
                ConnectorFixture.Connector.IO.A1[1].Synchron = false;
                return done;
            });


            Assert.IsFalse(sut.inAtWorkPos.Synchron);
            Assert.IsTrue(sut.inAtHomePos.Synchron);
            Assert.IsTrue(sut.outToHomePos.Synchron);
            Assert.False(sut.outToWorkPos.Synchron);
        }

        
        [Test]
        [Timeout(10000)]
        public void MoveCylinderToWorkTest()
        {
            sut.Run(a =>
            {
                var done = a._MoveToWorkTest();
                //Entry.TcoPneumaticsTestsPlc.IO.A1[0].Synchron = false;
                //Entry.TcoPneumaticsTestsPlc.IO.A1[1].Synchron = true;
                ConnectorFixture.Connector.IO.A1[0].Synchron = false;
                ConnectorFixture.Connector.IO.A1[1].Synchron = true;
                return done;
            });
            Assert.IsTrue(sut.inAtWorkPos.Synchron);
            Assert.IsFalse(sut.inAtHomePos.Synchron);
            Assert.IsFalse(sut.outToHomePos.Synchron);
            Assert.IsTrue(sut.outToWorkPos.Synchron);
        }

        [Test]
        [Timeout(10000)]
        public void StopCylinderTest()
        {
            sut.Run(a =>
            {
                var done = a._StopTest();              
                return done;
            });

            Assert.IsFalse(sut.outToHomePos.Synchron);
            Assert.IsFalse(sut.outToWorkPos.Synchron);
        }

        private RecorderModeEnum mode = RecorderModeEnum.Player;
        

        [Test]
        [Timeout(10000)]
        public void MoveCylinderToWorkTestWithRecording()
        {
            //var actor = new Recorder<IO, PlainIO>(Entry.TcoPneumaticsTestsPlc.IO, mode, 1).Actor;
            var actor = new Recorder<IO, PlainIO>(ConnectorFixture.Connector.IO, mode, 1).Actor;
            var done = false;

            sut.Run(() => { done = sut._MoveToWorkTest(); return done; },
                    () => { return done; },
                    null,
                    null,
                    actor,
                    Path.Combine(Runner.RecordingsShell, $"{nameof(MoveCylinderToWorkTestWithRecording)}.json"));

            Assert.IsTrue(sut.inAtWorkPos.Synchron);
            Assert.IsFalse(sut.inAtHomePos.Synchron);
            Assert.IsFalse(sut.outToHomePos.Synchron);
            Assert.IsTrue(sut.outToWorkPos.Synchron);
        }

        [Test]
        [Timeout(10000)]
        public void MoveCylinderToHomeTestWithRecording()
        {
            //var actor = new Recorder<IO, PlainIO>(Entry.TcoPneumaticsTestsPlc.IO, mode, 1).Actor;
            var actor = new Recorder<IO, PlainIO>(ConnectorFixture.Connector.IO, mode, 1).Actor;
            var done = false;

            sut.Run(() => { done = sut._MoveToHomeTest(); return done; },
                    () => { return done; },
                    null,
                    null,
                    actor,
                    Path.Combine(Runner.RecordingsShell, $"{nameof(MoveCylinderToHomeTestWithRecording)}.json"));

            Assert.IsFalse(sut.inAtWorkPos.Synchron);
            Assert.IsTrue(sut.inAtHomePos.Synchron);
            Assert.IsTrue(sut.outToHomePos.Synchron);
            Assert.False(sut.outToWorkPos.Synchron);
        }
    }
}