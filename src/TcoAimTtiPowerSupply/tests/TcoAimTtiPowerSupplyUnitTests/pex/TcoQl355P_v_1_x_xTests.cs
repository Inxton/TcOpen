using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcoAimTtiPowerSupply;

namespace TcoAimTtiPowerSupplyUnitTests.PexTests
{
    /// <summary>
    /// Tests of the .NET side of TcoQl355P_v_1_x_x against a fake power supply on localhost. No PLC is required.
    /// The remote task actions are called directly; exceptions thrown from them are what TcoRemoteTask reports to the PLC.
    /// </summary>
    [TestFixture]
    public class TcoQl355P_v_1_x_xTests
    {
        private TcoQl355P_v_1_x_x sut;
        private FakeQlSupply supply;

        [SetUp]
        public void Setup()
        {
            sut = new TcoQl355P_v_1_x_x(new MockRootObject(), string.Empty, string.Empty);
            sut.Initialize();
            supply = new FakeQlSupply();
            sut._config.IpAddress.Synchron = IPAddress.Loopback.ToString();
            sut._config.Port.Synchron = (ushort)supply.Port;
            sut._config.Timeout.Synchron = 500;
        }

        [TearDown]
        public void TearDown()
        {
            supply.Dispose();
        }

        private void Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x command, byte n)
        {
            sut._getCommandTask._command.Command.Synchron = (short)command;
            sut._getCommandTask._command.NValue.Synchron = n;
            sut.InvokeGetCommand();
        }

        private void Set(eTcoQWlSeriesSupplySetCommands_v_1_x_x command, byte n, float nrf)
        {
            sut._setCommandTask._command.Command.Synchron = (short)command;
            sut._setCommandTask._command.NValue.Synchron = n;
            sut._setCommandTask._command.NrfValue.Synchron = nrf;
            sut.InvokeSetCommand();
        }

        [Test]
        [Timeout(5000)]
        public void GetReadbackVoltageParsesNr2()
        {
            supply.Reply = "12.34V";

            Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.VO, 1);

            Assert.AreEqual("V1O?", supply.Received.Trim());
            Assert.AreEqual(12.34f, sut._status.State.Nr2Value.Synchron, 0.001f);
            Assert.AreEqual("12.34V", sut._status.State.Resposne.Synchron);
            Assert.AreEqual(string.Empty, sut._status.State.ConvertMessage.Synchron);
        }

        [Test]
        [Timeout(5000)]
        public void GetSetVoltageParsesOutputAndNr2()
        {
            supply.Reply = "V2 5.00";

            Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.V, 2);

            Assert.AreEqual("V2?", supply.Received.Trim());
            Assert.AreEqual(2, sut._status.State.NValue.Synchron);
            Assert.AreEqual(5.0f, sut._status.State.Nr2Value.Synchron, 0.001f);
        }

        [Test]
        [Timeout(5000)]
        public void GetOutputStatusParsesNr1()
        {
            supply.Reply = "1";

            Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.OP, 1);

            Assert.AreEqual("OP1?", supply.Received.Trim());
            Assert.AreEqual(1, sut._status.State.Nr1Value.Synchron);
        }

        [Test]
        [Timeout(5000)]
        public void SetVoltageSendsFormattedCommand()
        {
            Set(eTcoQWlSeriesSupplySetCommands_v_1_x_x.V, 1, 5.5f);

            Assert.AreEqual("V1  05.50", supply.WaitForReceived());
        }

        [Test]
        [Timeout(5000)]
        public void GetThrowsWhenSupplyIsUnreachable()
        {
            supply.Dispose();

            Assert.Throws<SocketException>(() => Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.VO, 1));
        }

        [Test]
        [Timeout(5000)]
        public void SetThrowsWhenSupplyIsUnreachable()
        {
            supply.Dispose();

            Assert.Throws<SocketException>(() => Set(eTcoQWlSeriesSupplySetCommands_v_1_x_x.V, 1, 5.0f));
        }

        [Test]
        [Timeout(5000)]
        public void GetThrowsWhenSupplyDoesNotRespond()
        {
            supply.Reply = null; // keep the connection open without answering

            Assert.Throws<IOException>(() => Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.VO, 1));
        }

        [Test]
        [Timeout(5000)]
        public void GetThrowsWhenSupplyClosesWithoutResponse()
        {
            supply.Reply = string.Empty; // close the connection without answering

            Assert.Throws<IOException>(() => Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.VO, 1));
        }

        [Test]
        [Timeout(5000)]
        public void GetThrowsOnUnparsableResponse()
        {
            supply.Reply = "garbage";

            Assert.Throws<FormatException>(() => Get(eTcoQWlSeriesSupplyGetCommands_v_1_x_x.VO, 1));
            Assert.AreEqual("garbage", sut._status.State.Resposne.Synchron);
            StringAssert.StartsWith("Unable to convert", sut._status.State.ConvertMessage.Synchron);
        }

        [Test]
        public void EveryGetCommandIsDefined()
        {
            foreach (var name in Enum.GetNames(typeof(eTcoQWlSeriesSupplyGetCommands_v_1_x_x)))
            {
                Assert.IsTrue(QlSeriesCommands.Commands.Any(c => c.Type == eQlCommandType.GetCommand && c.Key == name), $"Get command '{name}' is missing in {nameof(QlSeriesCommands)}.");
            }
        }

        [Test]
        public void EverySetCommandIsDefined()
        {
            foreach (var name in Enum.GetNames(typeof(eTcoQWlSeriesSupplySetCommands_v_1_x_x)))
            {
                Assert.IsTrue(QlSeriesCommands.Commands.Any(c => c.Type == eQlCommandType.SetCommand && c.Key == name), $"Set command '{name}' is missing in {nameof(QlSeriesCommands)}.");
            }
        }

        /// <summary>
        /// Line based fake of the QL series LAN interface. Accepts one connection, reads one line and answers with <see cref="Reply"/>:
        /// a text is sent as a line, <see cref="string.Empty"/> closes the connection, null keeps it open without answering.
        /// </summary>
        private sealed class FakeQlSupply : IDisposable
        {
            private readonly TcpListener listener;
            private readonly Task serverTask;
            private readonly ManualResetEventSlim received = new ManualResetEventSlim();
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private bool disposed;

            public FakeQlSupply()
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                serverTask = Task.Run(Serve);
            }

            public int Port { get; }

            public string Reply { get; set; } = string.Empty;

            public string Received { get; private set; }

            public string WaitForReceived()
            {
                Assert.IsTrue(received.Wait(2000), "The fake supply did not receive any command.");
                return Received;
            }

            private async Task Serve()
            {
                try
                {
                    using (var client = await listener.AcceptTcpClientAsync())
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream) { AutoFlush = true })
                    {
                        Received = await reader.ReadLineAsync();
                        received.Set();
                        if (Reply == null)
                        {
                            await Task.Delay(Timeout.Infinite, cts.Token);
                        }
                        else if (Reply != string.Empty)
                        {
                            await writer.WriteLineAsync(Reply);
                        }
                    }
                }
                catch (Exception) when (disposed)
                {
                    // listener stopped by Dispose
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                cts.Cancel();
                listener.Stop();
                try { serverTask.Wait(1000); } catch (AggregateException) { }
            }
        }
    }
}
