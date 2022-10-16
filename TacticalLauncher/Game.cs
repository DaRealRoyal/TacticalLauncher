using Octokit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
        failedRetry,
        failed
    }

    class Game : INotifyPropertyChanged
    {
        static readonly string launcherPath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).ToString());
        static readonly string settingsPath = Path.Combine(launcherPath, "settings.xml");   // TODO
        static readonly string gamesPath = Path.Combine(launcherPath, "Games");
        static readonly string downloadPath = Path.Combine(gamesPath, "Downloads");

        GitHubClient client = new GitHubClient(new ProductHeaderValue("TacticalLauncher"));

        readonly string versionFile;
        readonly string gameName;
        readonly string gameExeName;
        readonly string downloadVersionUrl;
        string downloadUrl;
        string gamePath;
        string gameExe;


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
                    case GameState.failedRetry:
                        return "Failed - Retry?";
                    case GameState.failed:
                        return "Failed";
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
            IsReady || _state == GameState.failedRetry;

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

        private Version _onlineVersion;

        public Version OnlineVersion
        {
            get { return _onlineVersion; }
            set
            {
                _onlineVersion = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(OnlineVersionVisibility));
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
                RaisePropertyChanged(nameof(OnlineVersionVisibility));
            }
        }

        public Visibility OnlineVersionVisibility
        {
            get { return Equals(_onlineVersion, _localVersion) ? Visibility.Hidden : Visibility.Visible; }
        }

        /// <summary>
        /// Creates an instance of Game using download links
        /// </summary>
        /// <param name="url">Download URL of the game's .zip file</param>
        /// <param name="versionUrl">Download URL of the text file containing the current verison string of the game</param>
        /// <param name="name">Name of the game (.zip has to contain a folder with the name)</param>
        /// <param name="exe">Name of the game's .exe file  (.zip has to contain the exe in the folder)</param>
        public Game(string url, string versionUrl, string name, string exe)
        {
            gameName = name;
            gameExeName = exe;
            gamePath = Path.Combine(gamesPath, gameName);
            versionFile = Path.Combine(gamesPath, gameName + "-version.txt");
            if (File.Exists(versionFile)) LocalVersion = new Version(File.ReadAllText(versionFile)); // TODO is if check needed?
            if (!Directory.Exists(gamePath))
            {
                // fallback for folders like MothershipDefender2_v2.3.1
                var gamePath2 = gamePath + "_v" + LocalVersion;
                if (Directory.Exists(gamePath2)) gamePath = gamePath2;
            }
            gameExe = Path.Combine(gamePath, exe);

            downloadUrl = url;
            downloadVersionUrl = versionUrl;
            CheckUpdates();
        }

        /// <summary>
        /// Creates an instance of Game using GitHub Releases
        /// </summary>
        public Game(string owner, string name, string exe)
        {
            gameName = name;
            gameExeName = exe;
            gamePath = Path.Combine(gamesPath, gameName);
            versionFile = Path.Combine(gamesPath, gameName + "-version.txt");
            if (File.Exists(versionFile)) LocalVersion = new Version(File.ReadAllText(versionFile)); // TODO is if check needed?
            if (!Directory.Exists(gamePath))
            {
                // fallback for folders like MothershipDefender2_v2.3.1
                var gamePath2 = gamePath + "_v" + LocalVersion;
                if (Directory.Exists(gamePath2)) gamePath = gamePath2;
            }
            gameExe = Path.Combine(gamePath, exe);

            GetGitHubData(owner, name, exe);
        }

        public async void GetGitHubData(string owner, string repo, string exe)
        {
            try
            {
                var releases = await client.Repository.Release.GetAll(owner, repo);
                var latest = releases[0];
                downloadUrl = GitHubAssetDownload(latest.Assets, gameName + "(.+)?.zip");
                OnlineVersion = new Version(latest.TagName.TrimStart('v'));
                Console.WriteLine("{0}/{1} {2} -> {3}", owner, repo, OnlineVersion, downloadUrl);

                if (File.Exists(versionFile) && File.Exists(gameExe))
                {
                    LocalVersion = new Version(File.ReadAllText(versionFile));
                    State = OnlineVersion == _localVersion ? GameState.clickPlay : GameState.clickUpdate;
                }
                else
                    State = GameState.clickInstall;
            }
            catch (Exception ex)
            {
                State = GameState.failed;
                ProgressText = $"{ex.GetType().Name} while checking for updates - {ex.Message}";
                if (File.Exists(gameExe)) State = GameState.clickPlay;
            }
        }

        public string GitHubAssetDownload(IReadOnlyList<ReleaseAsset> releaseAssets, string name)
        {
            foreach (var asset in releaseAssets)
            {
                if (Regex.IsMatch(asset.Name, name, RegexOptions.IgnoreCase | RegexOptions.Compiled)) return asset.BrowserDownloadUrl;
            }
            return "";
        }

        public void CheckUpdates()
        {
            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadStringCompleted += new DownloadStringCompletedEventHandler(VersionCallback);
                webClient.DownloadStringAsync(new Uri(downloadVersionUrl));
            }
            catch (Exception ex)
            {
                State = GameState.failedRetry;
                ProgressText = $"{ex.GetType().Name} while checking for updates - {ex.Message}";
                if (File.Exists(gameExe)) State = GameState.clickPlay;
            }
        }

        private void VersionCallback(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                OnlineVersion = new Version(e.Result.TrimStart('v'));
                Console.WriteLine("{0} {1} -> {2}", gameName, OnlineVersion, downloadUrl);

                if (File.Exists(versionFile) && File.Exists(gameExe))
                {
                    State = OnlineVersion == _localVersion ? GameState.clickPlay : GameState.clickUpdate;
                }
                else
                    State = GameState.clickInstall;
            }
            catch (Exception ex)
            {
                State = GameState.failedRetry;
                ProgressText = $"{ex.GetType().Name} while checking for updates - {ex.Message}";
                if (File.Exists(gameExe)) State = GameState.clickPlay;
            }
        }

        private void DownloadGame()
        {
            State = GameState.downloading;
            try
            {
                WebClient webClient = new WebClient();

                Directory.CreateDirectory(downloadPath);

                FileDownloader downloader = new FileDownloader();
                downloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressCallback);
                downloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompletedCallback);
                downloader.DownloadFileAsync(downloadUrl, Path.Combine(downloadPath, gameName + "_v" + OnlineVersion + ".zip"), OnlineVersion);
            }
            catch (Exception ex)
            {
                State = GameState.failedRetry;
                MessageBox.Show($"Error downloading game: {ex.Message}");
            }
        }


        private void UpdateProgressCallback(object sender, FileDownloader.DownloadProgress e)
        {
            DownloadSize = e.TotalBytesToReceive;
            DownloadSizeCurrent = e.BytesReceived;

            ProgressText = "Downloaded " + SizeSuffix(e.BytesReceived);
            ProgressText += e.TotalBytesToReceive > 0 ? " out of " + SizeSuffix(e.TotalBytesToReceive) : "";
        }

        private void DownloadCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            ProgressText = "";
            InstallGame((Version)e.UserState);
        }

        private async void InstallGame(Version version)
        {
            State = GameState.installing;

            try
            {
                string zipPath = Path.Combine(downloadPath, gameName + "_v" + version + ".zip");

                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, gamesPath));
                File.Delete(zipPath);

                File.WriteAllText(versionFile, version.ToString());
                LocalVersion = version;

                if (!Directory.Exists(gamePath))
                {
                    // fallback for folders like MothershipDefender2_v2.3.1
                    var gamePath2 = gamePath + "_v" + LocalVersion;
                    if (Directory.Exists(gamePath2)) gamePath = gamePath2;
                }
                gameExe = Path.Combine(gamePath, gameExeName);

                State = GameState.clickPlay;
            }
            catch (Exception ex)
            {
                State = GameState.failedRetry;
                MessageBox.Show($"Error installing game: {ex.Message}");
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
                    DownloadGame();
                    break;
                case GameState.failedRetry:
                    if (downloadVersionUrl == null)
                        DownloadGame();
                    else
                        CheckUpdates();
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        // Source: https://stackoverflow.com/a/14488941
        static readonly string[] SizeSuffixes = { "bytes", "KiB", "MiB", "GiB", "TiB" };
        static string SizeSuffix(Int64 value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + SizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
}
