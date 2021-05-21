using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Navigation;

namespace TacticalLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    public partial class MainWindow : Window
    {
        const string downloadUrlVersion = "https://drive.google.com/uc?export=download&id=18FkOPeDDzqPPgmRb4XdzEHEchfM5U3HV";
        const string downloadUrlGame = "https://drive.google.com/uc?export=download&id=1Ts8BzGfmp_JF_XTchyQMPjiIT90PDKGk";
        const string downloadUrlLauncher = "https://tacticalmath.games/download";

        string rootPath;
        string versionFile;
        string gameZip;
        string gameExe;

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
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game...";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update...";
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
            versionFile = Path.Combine(rootPath, "VersionTMR.txt");
            gameZip = Path.Combine(rootPath, "TMR.zip");
            gameExe = Path.Combine(rootPath, "TMR", "TacticalMathReturns.exe");

            SquirrelLauncherUpdates();
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

        void CheckForUpdates()
        {
            if (File.Exists(versionFile) && File.Exists(gameExe))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionTextGame.Text = localVersion.ToString();

                try
                {
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString(downloadUrlVersion).TrimStart('v'));

                    if (onlineVersion != localVersion)
                    {
                        InstallGame(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                        PlayButton.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for updates: {ex}");
                }
            }
            else
            {
                InstallGame(false, new Version(0, 0));
            }
        }

        void InstallGame(bool _isUpdate, Version _onlineVersion)
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
            if (e.TotalBytesToReceive != -1 && e.TotalBytesToReceive != e.BytesReceived)
            {
                Progress.Text = "Progress: " + e.BytesReceived + "/" + e.TotalBytesToReceive + " (" + +Math.Round((float)e.BytesReceived / (long)e.TotalBytesToReceive * 100) + "%)";
            }
            else
            {
                Progress.Text = "Progress: " + e.BytesReceived;
            }
        }

        void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            Progress.Text = "";
            try
            {
                string onlineVersion = ((Version)e.UserState).ToString();
                File.Delete(Path.Combine(rootPath, "TMR"));
                ZipFile.ExtractToDirectory(gameZip, rootPath);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionTextGame.Text = onlineVersion;
                Status = LauncherStatus.ready;
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game: {ex}");
            }
            PlayButton.IsEnabled = true;
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
