using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TcoCoreExamples;
using TcOpen.Inxton;
using TcOpen.Inxton.Local.Security;
using TcOpen.Inxton.Local.Security.Wpf;
using TcOpen.Inxton.Security;
using TcOpen.Inxton.TcoCore.Wpf;
using Vortex.Adapters.Connector.Tc3.Adapter;
using Vortex.Presentation.Wpf;

namespace TcoCore.Sandbox.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
         
            Color primaryColor = SwatchHelper.Lookup[MaterialDesignColor.DeepPurple];
            Color accentColor = SwatchHelper.Lookup[MaterialDesignColor.Lime];
            ITheme theme = Theme.Create(new MaterialDesignLightTheme(), primaryColor, accentColor);
            Resources.SetTheme(theme);




            base.OnStartup(e);
        }
        public App() : base()
        {
            CultureInfo ci = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            PlcTcoCoreExamples.Connector.ReadWriteCycleDelay = 250;
            PlcTcoCoreExamples.Connector.BuildAndStart();

            TcOpen.Inxton.TcoAppDomain.Current.Builder
            .SetUpLogger(new TcOpen.Inxton.Logging.SerilogAdapter(new LoggerConfiguration()
                                                    .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose)
                                                    .WriteTo.Notepad()
                                                    .MinimumLevel.Verbose()))
            .SetDispatcher(TcoCore.Wpf.Threading.Dispatcher.Get)
            .SetPlcDialogs(DialogProxyServiceWpf.Create(new[] { PlcTcoCoreExamples.EXAMPLES_PRG._diaglogsContext}));

            
            PlcTcoCoreExamples.MANIPULATOR._context._logger.StartLoggingMessages(eMessageCategory.All);
            PlcTcoCoreExamples.EXAMPLES_PRG._context._logger.StartLoggingMessages(eMessageCategory.All);
            PlcTcoCoreExamples.MAIN._station001._logger.StartLoggingMessages(eMessageCategory.All);
            PlcTcoCoreExamples.EXAMPLES_PRG._loggerContext._loggerUsage._logger.StartLoggingMessages(eMessageCategory.All);
            PlcTcoCoreExamples.MAIN._station001._components._wrappedComponent2.SearchComponentsDepth = 1;
            PlcTcoCoreExamples.MAIN._station001._components._di.IsExpanded = false;


            Directory.EnumerateFiles(@"C:\INXTON\USERS\").ToList().ForEach(File.Delete);
            Directory.EnumerateFiles(@"C:\INXTON\GROUP\").ToList().ForEach(File.Delete);
            var userDataRepo = new DefaultUserDataRepository<UserData>();
            var groups = new DefaultGroupDataRepository<GroupData>();
            var roleGroupManager = new RoleGroupManager(groups);

            roleGroupManager.CreateGroup("OperatorGroup");
            roleGroupManager.AddRoleToGroup("OperatorGroup", "Operator");
            roleGroupManager.AddRoleToGroup("OperatorGroup", "can_terminate_inspection");
            roleGroupManager.AddRoleToGroup("OperatorGroup", "can_override_inspection");

            SecurityManager.Create(userDataRepo, roleGroupManager);
            SecurityManager.Manager.GetOrCreateRole(new Role("Operator", "OperatorGroup"));

            var userName = "Operator";
            var password = "OperatorPassword";

            userDataRepo.Create(userName, new UserData(userName, string.Empty, password, new string[] { "OperatorGroup" }, "Operator", string.Empty) { CanUserChangePassword = true });

            LazyRenderer.Get.CreateSecureContainer = (permissions) => new PermissionBox { Permissions = permissions, SecurityMode = SecurityModeEnum.Disabled };
            SecurityManager.Manager.Service.OnUserAuthenticateSuccess += Service_OnUserAuthenticateSuccess; ;
            SecurityManager.Manager.Service.OnDeAuthenticated += Service_OnDeAuthenticated; ; ;

            SecurityManager.Manager.Service.AuthenticateUser(userName, password);

           
        }

        private void Service_OnDeAuthenticated(string username)
        {
            PlcTcoCoreExamples.EXAMPLES_PRG._diaglogsContext._operatorName.Cyclic = "";
        }

        private void Service_OnUserAuthenticateSuccess(string username)
        {
            PlcTcoCoreExamples.EXAMPLES_PRG._diaglogsContext._operatorName.Cyclic = username;
            var authentificated = SecurityManager.Manager.UserRepository.Queryable.Where(p => p.Username == username).FirstOrDefault();
            PlcTcoCoreExamples.EXAMPLES_PRG._diaglogsContext._operatorName.Cyclic = username;
            PlcTcoCoreExamples.EXAMPLES_PRG._diaglogsContext._userLevel.Cyclic = authentificated.Level;
        }

        private static string AMS_ID = Environment.GetEnvironmentVariable("Tc3Target");
        private static volatile object mutex = new object();

        public static TcoCoreExamplesTwinController _plc;        
        
        /// <summary>
        /// Gets Plc twin for this application.
        /// </summary>
        public static TcoCoreExamplesTwinController PlcTcoCoreExamples
        {
            get
            {
                if (_plc == null)
                {
                    lock (mutex)
                    {
                        if (_plc == null)
                        {
                            _plc = CreateTwin();
                        }
                    }
                }

                return _plc;
            }
        }

        /// <summary>
        /// Creates twin connector for runtime or design mode.
        /// </summary>
        /// <returns>Plc twin</returns>
        private static TcoCoreExamplesTwinController CreateTwin()
        {
            if (!IsInDesign)
            {
                return new TcoCoreExamplesTwinController(Tc3ConnectorAdapter.Create(AMS_ID,853, true));
            }
            else
            {
                return new TcoCoreExamplesTwinController(new Vortex.Connector.ConnectorAdapter(typeof(Vortex.Connector.DummyConnectorFactory)), new object[] { string.Empty });
            }
        }

        /// <summary>
        /// Gets true when running in design mode
        /// </summary>
        private static bool IsInDesign
        {
            get
            {
                return System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject());
            }
        }
    }
}
