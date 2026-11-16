using FlaUI.UIA3;
using NUnit.Framework;
using System.Reflection;

namespace IntegrationProjectTests
{
    public class ExecuteWpfIntegrationProject
    {       
        [Test]
        [Timeout(15000)]
        public void OpenApplicationTest()
        {
            var someAssemblyType = new Sandbox.IntegrationProjects.Wpf.App();
            var assembly = someAssemblyType.GetType().Assembly;
            // on .NET (Core) Assembly.Location is the .dll; the app is started through its .exe apphost next to it
            var appPath = assembly.Location;
            var exePath = System.IO.Path.ChangeExtension(appPath, ".exe");
            if (System.IO.File.Exists(exePath)) appPath = exePath;
            var app = FlaUI.Core.Application.Launch(appPath);
            using (var automation = new UIA3Automation())
            {
                var window = app.GetMainWindow(automation);
                System.Threading.Thread.Sleep(5000);
                window.Close();
            }
        }
    }
}