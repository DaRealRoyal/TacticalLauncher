using Squirrel;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace TacticalLauncher
{
    public partial class MainWindow : Window
    {
        private const string updateUrl = "https://tacticalmath.games/download";

        //readonly Game tmrGDrive = new(
        //    "https://drive.google.com/uc?export=download&id=1FJL0sBPvt5AEdbkgcKshO15Vv-kuJ6gd",
        //    "https://drive.google.com/uc?export=download&id=18FkOPeDDzqPPgmRb4XdzEHEchfM5U3HV",
        //    "TacticalMathReturns", "TacticalMathReturns.exe");
        readonly Game tmr = new("DaRealRoyal", "TacticalMathReturns", "TacticalMathReturns.exe");
        readonly Game md2 = new("Nalsai", "MothershipDefender2", "MothershipDefender2.exe");

        public MainWindow()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: OnAppInstall,
                    onAppUninstall: OnAppUninstall,
                    onEveryRun: OnAppRun);

            InitializeComponent();
            VersionTextLauncher.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            //TmrTab.DataContext = tmrGDrive;
            TmrTab.DataContext = tmr;
            MD2Tab.DataContext = md2;
        }

        [SupportedOSPlatform("windows")]
        private static void OnAppInstall(SemanticVersion version, IAppTools tools)
        {
            tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        [SupportedOSPlatform("windows")]
        private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
        {
            tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);
        }

        [SupportedOSPlatform("windows")]
        private static void OnAppRun(SemanticVersion version, IAppTools tools, bool firstRun)
        {
            tools.SetProcessAppUserModelId();
        }

        [SupportedOSPlatform("windows")]
        private static async void SquirrelUpdate()
        {
            using var mgr = new UpdateManager(updateUrl);
            if (mgr.IsInstalledApp) await mgr.UpdateApp();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SquirrelUpdate();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            };
            _ = Process.Start(processStartInfo);
            e.Handled = true;
        }
    }
}
