using TcoElementsTests;

namespace TcoElementsUnitTests
{
    /// <summary>
    /// Starts the test connector once per test run. Calling BuildAndStart() again in every fixture
    /// reconnects the adapter, and on .NET (TwinCAT.Ads 5) the first writes after a reconnect
    /// can fail with ADS error 0x701 and get lost.
    /// </summary>
    internal static class ConnectorOnce
    {
        private static readonly object gate = new object();
        private static bool started;

        public static void Start()
        {
            lock (gate)
            {
                if (started) return;
                Entry.TcoElementsTests.Connector.BuildAndStart().ReadWriteCycleDelay = 100;
                started = true;
            }
        }
    }
}
