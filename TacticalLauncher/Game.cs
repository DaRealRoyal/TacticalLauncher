using Octokit;
using System;
using System.Collections.Generic;
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
    internal enum GameStatus
    {
        ready,
        failed,
        downloading,
        installing,
    }

    internal class Game
    {
        string gameName;

        string checksum;

        static string gamesPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString(), "Games");
        static string launcherPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString(), "Games");
        static string settingsPath = Path.Combine(launcherPath, "settings.xml");
        string downloadFileURL;
        string gamePath;
        string gameExe;

        private string localChecksum;
        public string GetLocalVersion() => localChecksum;

        private GameStatus status;
        public GameStatus GetStatus() => status;


        public Game(string name, string downloadURL)
        {
            gameName = name;
            gamePath = Path.Combine(gamePath, gameName);
            this.downloadFileURL = downloadURL;
        }

        public async void getDownload()
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(getDownloadCompletedCallback);
                webClient.DownloadStringAsync(new Uri(downloadFileURL));
            }
            catch (Exception ex)
            {
                status = GameStatus.failed;
                MessageBox.Show($"Error checking for updates: {ex}");
            }
        }

        void getDownloadCompletedCallback()
        {

        }

        internal async void CheckForUpdates()
        {
            if (File.Exists(gameExe))
            {
                if (localChecksum == checksum)
                {
                    status = GameStatus.ready;
                }
                else
                {
                    downloadGame();
                }
            }
            else
            {
                downloadGame();
            }
        }

        void downloadGame()
        {
            status = GameStatus.downloading;
            try
            {


                FileDownloader fileDownloader = new FileDownloader();
                fileDownloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressText);
                fileDownloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                fileDownloader.DownloadFileAsync(downloadUrlGame, Path.Combine(gamePath, "download"));

                installGame();
            }
            catch (Exception e)
            {
                status = GameStatus.failed;

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

        void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            string onlineVersion = ((Version)e.UserState).ToString();
            installGame();
        }


        async void installGame()
        {
            status = GameStatus.installing;

            try
            {
                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(gameZip, gamePath));
                File.Delete(gameZip);

                status = GameStatus.ready;
            }
            catch (Exception e)
            {
                status = GameStatus.failed;
                MessageBox.Show($"Error installing game: {e}");
            }
        }

        internal void Play()
        {
            if (File.Exists(gameExe) && status == GameStatus.ready)
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo()
                {
                    FileName = gameExe
                };
                _ = Process.Start(processStartInfo);
            }
            else if (status == GameStatus.failed)
            {
                CheckForUpdates();
            }
        }

        /*
        internal GameStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case GameStatus.ready:
                        PlayButton.Content = "Play";
                        PlayButton.IsEnabled = true;
                        ProgressBar.Value = ProgressBar.Maximum;
                        break;
                    case GameStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        PlayButton.IsEnabled = true;
                        break;
                    case GameStatus.downloadingGame:
                        PlayButton.Content = "Downloading Game...";
                        PlayButton.IsEnabled = false;
                        break;
                    case GameStatus.installingGame:
                        PlayButton.Content = "Installing Game...";
                        PlayButton.IsEnabled = false;
                        break;
                    case GameStatus.downloadingUpdate:
                        PlayButton.Content = "Downloading Update...";
                        PlayButton.IsEnabled = false;
                        break;
                    default:
                        break;
                }
            }
        }

        public async void CheckForUpdates()
        {

            if (File.Exists(versionFile) && File.Exists(gameExe))
            {
                await Task.Run(() => localVersion = new Version(File.ReadAllText(versionFile)));
                try
                {
                    WebClient webClient = new WebClient();
                    webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(DownloadVersionFileCompletedCallback);
                    webClient.DownloadStringAsync(new Uri(downloadUrlVersion));
                }
                catch (Exception ex)
                {
                    Status = GameStatus.failed;
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
                    Status = GameStatus.downloadingUpdate;
                }
                else
                {
                    Status = GameStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString(downloadUrlVersion).TrimStart('v'));
                }

                FileDownloader fileDownloader = new FileDownloader();
                fileDownloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressText);
                fileDownloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                fileDownloader.DownloadFileAsync(downloadUrlGame, gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = GameStatus.failed;
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
                    Status = GameStatus.ready;
                }
            }
            catch (Exception ex)
            {
                Status = GameStatus.failed;
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
            Status = GameStatus.installingGame;
            Progress.Text = "";
            try
            {
                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(gameZip, rootPath));
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);

                VersionTextGame.Text = "v" + onlineVersion;
                Status = GameStatus.ready;
            }
            catch (Exception ex)
            {
                Status = GameStatus.failed;
                MessageBox.Show($"Error installing game: {ex}");
            }
        }
        */
    }
}
