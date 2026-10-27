using System;

using System.Windows;



using System.IO;
using System.Reflection;
using TcOpen.Inxton.TcoCore.Wpf;

namespace TcoAteqMeasurement.Wpf.Sandbox
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() : base()
        {
            TcOpen.Inxton.TcoAppDomain.Current.Builder
                .SetPlcDialogs(DialogProxyServiceWpf.Create(new[] { TcoAteqMeasurementTests.Entry.TcoAteqMeasurementTestsPlc.MAIN._wpfContext }))
                .SetUpLogger(new TcOpen.Inxton.Logging.SerilogAdapter())
                .SetDispatcher(TcoCore.Wpf.Threading.Dispatcher.Get);
            




            TcoAteqMeasurementTests.Entry.TcoAteqMeasurementTestsPlc.Connector.BuildAndStart();
            TcoAteqMeasurementTests.Entry.TcoAteqMeasurementTestsPlc.MAIN._wpfContextCall.Synchron = true;
            TcoAteqMeasurementTests.Entry.TcoAteqMeasurementTestsPlc.MAIN._wpfContext._serviceModeActive.Synchron = true;
            this.ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

     
      
    }
}
