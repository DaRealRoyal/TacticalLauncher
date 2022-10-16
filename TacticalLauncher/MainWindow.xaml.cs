using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace TacticalLauncher
{
    public partial class MainWindow : Window
    {
        private const string downloadUrlLauncher = "https://tacticalmath.games/download";

        //readonly Game tmrGDrive = new Game(
        //    "https://drive.google.com/uc?export=download&id=1FJL0sBPvt5AEdbkgcKshO15Vv-kuJ6gd",
        //    "https://drive.google.com/uc?export=download&id=18FkOPeDDzqPPgmRb4XdzEHEchfM5U3HV",
        //    "TacticalMathReturns", "TacticalMathReturns.exe");
        readonly Game tmr = new Game("DaRealRoyal", "TacticalMathReturns", "TacticalMathReturns.exe");
        readonly Game md2 = new Game("Nalsai", "MothershipDefender2", "MothershipDefender2.exe");

        public MainWindow()
        {
            InitializeComponent();
            VersionTextLauncher.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            //TmrTab.DataContext = tmrGDrive;
            TmrTab.DataContext = tmr;
            MD2Tab.DataContext = md2;
        }

        private void SquirrelLauncherUpdates()
        {
            string squirrel = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString(), "Update.exe");
            if (File.Exists(squirrel))
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo()
                {
                    FileName = squirrel,
                    Arguments = $"--update={downloadUrlLauncher}"
                };
                _ = Process.Start(processStartInfo);
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            _ = Task.Run(() => SquirrelLauncherUpdates());
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo()
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            };
            _ = Process.Start(processStartInfo);
            e.Handled = true;
        }
    }
}
