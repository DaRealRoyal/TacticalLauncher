using Octokit;
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
        readonly Game tmr = new Game("DaRealRoyal", "TacticalMathReturns");

        public MainWindow()
        {
            InitializeComponent();
            VersionTextLauncher.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (tmr.GetLocalVersion() != "") VersionTextGame.Text = "v" + tmr.GetLocalVersion();

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
            tmr.CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            tmr.Play();
            Close();
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
