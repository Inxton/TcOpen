using System;

namespace TcoAteqMeasurement
{
    public class Entry
    {
        static string amsId = Environment.GetEnvironmentVariable("Tc3Target");
        const int amsPort = 851;

        public static TcoAteqMeasurement.TcoAteqMeasurementTwinController TcoAteqMeasurement { get; }
            = new TcoAteqMeasurement.TcoAteqMeasurementTwinController(Vortex.Adapters.Connector.Tc3.Adapter.Tc3ConnectorAdapter.Create(amsId, amsPort, true));
    }
}
