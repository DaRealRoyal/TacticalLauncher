using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TacticalLauncher
{
    public enum GameState
    {
        start,
        clickPlay,
        clickUpdate,
        clickInstall,
        downloading,
        installing,
        failed
    }

    class Game : INotifyPropertyChanged
    {
        static readonly string launcherPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString());
        static readonly string settingsPath = Path.Combine(launcherPath, "settings.xml");   // TODO
        static readonly string gamesPath = Path.Combine(launcherPath, "Games");
        static readonly string downloadPath = Path.Combine(gamesPath, "Downloads");

        readonly string versionFile;
        readonly string gameName;
        readonly string gamePath;
        readonly string downloadUrl;
        readonly string downloadVersionUrl;
        readonly string gameExe;

        private GameState _state;
        public GameState State
        {
            get { return _state; }
            set
            {
                _state = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(IsReady));
                RaisePropertyChanged(nameof(IsReadyOrFailed));
            }
        }

        public string StatusText
        {
            get
            {
                switch (_state)
                {
                    case GameState.start:
                        return "Checking For Updates...";
                    case GameState.clickPlay:
                        return "Play";
                    case GameState.clickUpdate:
                        return "Update";
                    case GameState.clickInstall:
                        return "Install";
                    case GameState.failed:
                        return "Failed - Retry?";
                    case GameState.downloading:
                        return "Downloading...";
                    case GameState.installing:
                        return "Installing...";
                }
                return _state.ToString();
            }
        }

        public bool IsReady =>
            _state == GameState.clickPlay ||
            _state == GameState.clickUpdate ||
            _state == GameState.clickInstall;

        public bool IsReadyOrFailed =>
            IsReady || _state == GameState.failed;

        private long _downloadSize;
        public long DownloadSize
        {
            get { return _downloadSize; }
            set
            {
                _downloadSize = value;
                RaisePropertyChanged();
            }
        }

        private long _downloadSizeCurrent;
        public long DownloadSizeCurrent
        {
            get { return _downloadSizeCurrent; }
            set
            {
                _downloadSizeCurrent = value;
                RaisePropertyChanged();
            }
        }

        private string _progressText;
        public string ProgressText
        {
            get { return _progressText; }
            set
            {
                _progressText = value;
                RaisePropertyChanged();
            }
        }

        private Version _localVersion;
        public Version LocalVersion
        {
            get { return _localVersion; }
            set
            {
                _localVersion = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Creates instance of the Game
        /// </summary>
        /// <param name="url">Download URL of the game's .zip file</param>
        /// <param name="versionUrl">Download URL of the text file containing the current verison string of the game</param>
        /// <param name="name">Name of the game (.zip has to contain a folder with the name)</param>
        /// <param name="exe">Name of the game's .exe file  (.zip has to contain the exe in the folder)</param>
        public Game(string url, string versionUrl, string name, string exe)
        {
            gameName = name;
            gamePath = Path.Combine(gamesPath, gameName);
            gameExe = Path.Combine(gamePath, exe);
            downloadUrl = url;
            downloadVersionUrl = versionUrl;
            versionFile = Path.Combine(gamesPath, name + ".version");

            CheckUpdates();
        }

        public void CheckUpdates()
        {
            if (File.Exists(versionFile) && File.Exists(gameExe))
            {
                LocalVersion = new Version(File.ReadAllText(versionFile));

                try
                {
                    WebClient webClient = new WebClient();
                    webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(VersionCallback);
                    webClient.DownloadStringAsync(new Uri(downloadVersionUrl));
                }
                catch (Exception ex)
                {
                    State = GameState.failed;
                    MessageBox.Show($"Error checking for updates: {ex}");
                }
            }
            else
                State = GameState.clickInstall;
        }

        private void VersionCallback(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                Version onlineVersion = new Version(e.Result.TrimStart('v'));
                State = onlineVersion == _localVersion ? GameState.clickPlay : GameState.clickUpdate;
            }
            catch (Exception ex)
            {
                State = GameState.failed;
                MessageBox.Show($"Error checking for updates: {ex}");
            }
        }

        private void DownloadGame(Version _onlineVersion)
        {
            State = GameState.downloading;
            try
            {
                WebClient webClient = new WebClient();
                _onlineVersion = new Version(webClient.DownloadString(downloadVersionUrl).TrimStart('v'));

                Directory.CreateDirectory(downloadPath);

                FileDownloader downloader = new FileDownloader();
                downloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressCallback);
                downloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompletedCallback);
                downloader.DownloadFileAsync(downloadUrl, Path.Combine(downloadPath, gameName + _onlineVersion + ".zip"), _onlineVersion);
            }
            catch (Exception ex)
            {
                State = GameState.failed;
                MessageBox.Show($"Error downloading game: {ex}");
            }
        }


        private void UpdateProgressCallback(object sender, FileDownloader.DownloadProgress e)
        {
            DownloadSize = e.TotalBytesToReceive;
            DownloadSizeCurrent = e.BytesReceived;

            ProgressText = "Progress: " + e.BytesReceived + "/" + e.TotalBytesToReceive + " (" + e.ProgressPercentage + "%)";
        }

        private void DownloadCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            ProgressText = "";
            InstallGame((Version)e.UserState);
        }

        private async void InstallGame(Version onlineVersion)
        {
            State = GameState.installing;

            try
            {
                string zipPath = Path.Combine(downloadPath, gameName + onlineVersion + ".zip");

                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, gamesPath));
                File.Delete(zipPath);

                File.WriteAllText(versionFile, onlineVersion.ToString());
                LocalVersion = onlineVersion;
                State = GameState.clickPlay;
            }
            catch (Exception ex)
            {
                State = GameState.failed;
                MessageBox.Show($"Error installing game: {ex}");
            }
        }

        private ICommand _playCommand;
        public ICommand PlayCommand => _playCommand ?? (_playCommand = new CommandHandler(() => Play(), () => true));

        public void Play()
        {
            switch (State)
            {
                case GameState.clickPlay:
                    ProcessStartInfo processStartInfo = new ProcessStartInfo()
                    {
                        FileName = gameExe
                    };
                    _ = Process.Start(processStartInfo);
                    break;
                case GameState.clickInstall:
                case GameState.clickUpdate:
                    State = GameState.downloading;
                    DownloadGame(new Version(0, 0));
                    break;
                case GameState.failed:
                    CheckUpdates();
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }
    }
}
