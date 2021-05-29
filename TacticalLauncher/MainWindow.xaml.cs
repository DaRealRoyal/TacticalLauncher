using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace TacticalLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        installingGame,
        downloadingUpdate
    }

    public partial class MainWindow : Window
    {
        const string downloadUrlVersion = "https://drive.google.com/uc?export=download&id=18FkOPeDDzqPPgmRb4XdzEHEchfM5U3HV";
        const string downloadUrlGame = "https://drive.google.com/uc?export=download&id=1FJL0sBPvt5AEdbkgcKshO15Vv-kuJ6gd";
        const string downloadUrlLauncher = "https://tacticalmath.games/download";

        string rootPath;
        string versionFile;
        string gameZip;
        string gameExe;
        string gamePath;

        Version localVersion;

        LauncherStatus _status;
        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Play";
                        PlayButton.IsEnabled = true;
                        ProgressBar.Value = ProgressBar.Maximum;
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        PlayButton.IsEnabled = true;
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game...";
                        PlayButton.IsEnabled = false;
                        break;
                    case LauncherStatus.installingGame:
                        PlayButton.Content = "Installing Game...";
                        PlayButton.IsEnabled = false;
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update...";
                        PlayButton.IsEnabled = false;
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            VersionTextLauncher.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            rootPath = Directory.GetParent(Directory.GetCurrentDirectory()).ToString();
            versionFile = Path.Combine(rootPath, "TacticalMathReturns.version");
            gameZip = Path.Combine(rootPath, "TacticalMathReturns.zip");
            gamePath = Path.Combine(rootPath, "TacticalMathReturns");
            gameExe = Path.Combine(gamePath, "TacticalMathReturns.exe");
        }

        void SquirrelLauncherUpdates()
        {
            string squirrel = Path.Combine(rootPath, "Update.exe");
            if (File.Exists(squirrel))
            {
                var processStartInfo = new ProcessStartInfo()
                {
                    FileName = squirrel,
                    Arguments = $"--update={downloadUrlLauncher}"
                };
                Process.Start(processStartInfo);
            }
        }

        async void CheckForUpdates()
        {
            await Task.Run(() => SquirrelLauncherUpdates());

            if (File.Exists(versionFile) && File.Exists(gameExe))
            {
                await Task.Run(() => localVersion = new Version(File.ReadAllText(versionFile)));
                VersionTextGame.Text = "v" + localVersion.ToString();

                try
                {
                    WebClient webClient = new WebClient();
                    webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadVersionFileCompletedCallback);
                    webClient.DownloadStringAsync(new Uri(downloadUrlVersion));
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for updates: {ex}");
                }
            }
            else
            {
                DownloadGame(false, new Version(0, 0));
            }
        }

        void DownloadGame(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                WebClient webClient = new WebClient();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString(downloadUrlVersion).TrimStart('v'));
                }

                FileDownloader fileDownloader = new FileDownloader();
                fileDownloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressText);
                fileDownloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                fileDownloader.DownloadFileAsync(downloadUrlGame, gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error downloading game: {ex}");
            }
        }

        void UpdateProgressText(object sender, FileDownloader.DownloadProgress e)
        {
            ProgressBar.Minimum = 0;
            ProgressBar.Maximum = e.TotalBytesToReceive;

            if (e.TotalBytesToReceive > 0 && e.TotalBytesToReceive != e.BytesReceived)
            {
                ProgressBar.Value = e.BytesReceived;
                Progress.Text = "Progress: " + e.BytesReceived + "/" + e.TotalBytesToReceive + " (" + e.ProgressPercentage + "%)";
            }
            else
            {
                Progress.Text = "Progress: " + e.BytesReceived;
            }
        }

        void DownloadVersionFileCompletedCallback(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                Version onlineVersion = new Version(e.Result.TrimStart('v'));

                if (onlineVersion != localVersion)
                {
                    DownloadGame(true, onlineVersion);
                }
                else
                {
                    Status = LauncherStatus.ready;
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error checking for updates: {ex}");
            }
        }

        void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            string onlineVersion = ((Version)e.UserState).ToString();
            InstallGame(onlineVersion);
        }

        async void InstallGame(string onlineVersion)
        {
            Status = LauncherStatus.installingGame;
            Progress.Text = "";
            try
            {
                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(gameZip, rootPath));
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionTextGame.Text = "v" + onlineVersion;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game: {ex}");
            }
        }

        void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                var processStartInfo = new ProcessStartInfo()
                {
                    FileName = gameExe,
                    WorkingDirectory = Path.Combine(rootPath, "TMR")
                };
                Process.Start(processStartInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }

        void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            };
            Process.Start(processStartInfo);

            e.Handled = true;
        }
    }
}
