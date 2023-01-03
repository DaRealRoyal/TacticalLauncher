using Octokit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
        clickFind,
        clickUpdate,
        clickInstall,
        downloading,
        installing,
        failedRetry,
        failed
    }

    class Game : INotifyPropertyChanged
    {
        static SettingsController settings;
        static readonly GitHubClient client = new(new ProductHeaderValue("TacticalLauncher"));
        readonly string versionFile;
        readonly string updateFile;
        readonly string gameName;
        readonly string gameExeName;
        readonly string downloadVersionUrl;
        readonly string githubOwner;
        readonly string githubRepo;
        string downloadUrl;
        string gamePath;
        string gameExe;

        #region Public variables
        private GameState _state;
        public GameState State
        {
            get => _state;
            set
            {
                _state = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(IsReady));
                RaisePropertyChanged(nameof(IsReadyOrFailed));
            }
        }

        public string StatusText => _state switch
        {
            GameState.start => "Checking For Updates...",
            GameState.clickPlay => "Play",
            GameState.clickFind => "Get Download",
            GameState.clickUpdate => "Update",
            GameState.clickInstall => "Install",
            GameState.failedRetry => "Failed - Retry?",
            GameState.failed => "Failed",
            GameState.downloading => "Downloading...",
            GameState.installing => "Installing...",
            _ => _state.ToString(),
        };

        public bool IsReady =>
            _state == GameState.clickPlay ||
            _state == GameState.clickFind ||
            _state == GameState.clickUpdate ||
            _state == GameState.clickInstall;

        public bool IsReadyOrFailed =>
            IsReady || _state == GameState.failedRetry;

        private long _downloadSize;
        public long DownloadSize
        {
            get => _downloadSize;
            set
            {
                _downloadSize = value;
                RaisePropertyChanged();
            }
        }

        private long _downloadSizeCurrent;
        public long DownloadSizeCurrent
        {
            get => _downloadSizeCurrent;
            set
            {
                _downloadSizeCurrent = value;
                RaisePropertyChanged();
            }
        }

        private string _progressText;
        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                RaisePropertyChanged();
            }
        }

        private Version _onlineVersion;
        public Version OnlineVersion
        {
            get => _onlineVersion;
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
            get => _localVersion;
            set
            {
                _localVersion = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(OnlineVersionVisibility));
            }
        }

        public Visibility OnlineVersionVisibility => Equals(_onlineVersion, _localVersion) ? Visibility.Hidden : Visibility.Visible;

        private ICommand _playCommand;
        public ICommand PlayCommand => _playCommand ??= new CommandHandler((x) => Play(), () => true);
        #endregion

        #region Intilization
        /// <summary>
        /// Reusable intilization code
        /// </summary>
        private Game(string name, string exe, SettingsController set)
        {
            settings = set;
            gameName = name;
            gameExeName = exe;
            gamePath = Path.Combine(settings.GamesPath, gameName);
            versionFile = Path.Combine(settings.GamesPath, gameName + "-version.txt");
            updateFile = Path.Combine(settings.GamesPath, gameName + "-update.txt");
            if (File.Exists(versionFile)) LocalVersion = new Version(File.ReadAllText(versionFile));
            if (!Directory.Exists(gamePath))
            {
                // fallback for folders like MothershipDefender2_v2.3.1
                var gamePath2 = gamePath + "_v" + LocalVersion;
                if (Directory.Exists(gamePath2)) gamePath = gamePath2;
            }
            gameExe = Path.Combine(gamePath, exe);
        }

        /// <summary>
        /// Creates an instance of Game using download links
        /// </summary>
        /// <param name="url">Download URL of the game's .zip file</param>
        /// <param name="versionUrl">Download URL of the text file containing the current verison string of the game</param>
        /// <param name="name">Name of the game (.zip has to contain a folder with the name)</param>
        /// <param name="exe">Name of the game's .exe file (.zip has to contain the exe in the folder)</param>
        /// <param name="set">SettingsController</param>
        public Game(string url, string versionUrl, string name, string exe, SettingsController set) : this(name, exe, set)
        {
            downloadUrl = url;
            downloadVersionUrl = versionUrl;
            if (File.Exists(gameExe) && !File.Exists(updateFile))
            {
                if (RateLimit())
                    State = GameState.clickPlay;
                else
                    TryFindNewestVersion();
                return;
            }
            State = GameState.clickFind;
        }

        /// <summary>
        /// Creates an instance of Game using GitHub Releases
        /// </summary>
        /// <param name="owner">Owner of the GitHub Repo (example: DaRealRoyal for DaRealRoyal/TacticalMathReturns)</param>
        /// <param name="name">Name of the game and the GitHub Repo</param>
        /// <param name="exe">Name of the game's .exe file</param>
        /// <param name="set">SettingsController</param>
        public Game(string owner, string name, string exe, SettingsController set) : this(name, exe, set)
        {
            githubOwner = owner;
            githubRepo = name;
            if (File.Exists(gameExe) && !File.Exists(updateFile))
            {
                if (RateLimit())
                    State = GameState.clickPlay;
                else
                    TryFindNewestVersion();
                return;
            }
            State = GameState.clickFind;
        }
        #endregion

        public void Play()
        {
            switch (State)
            {
                case GameState.clickPlay:
                    ProcessStartInfo processStartInfo = new()
                    {
                        FileName = gameExe
                    };
                    _ = Process.Start(processStartInfo);
                    break;
                case GameState.clickFind:
                    TryFindNewestVersion();
                    break;
                case GameState.clickInstall:
                case GameState.clickUpdate:
                    State = GameState.downloading;
                    DownloadGame();
                    break;
                case GameState.failedRetry:
                    TryFindNewestVersion();
                    break;
            }
        }

        /// <summary>
        /// Rate limit requests to one update check per 15 minutes (per game)
        /// false means no rate limit; true means rate limit -> don't check for updates again
        /// </summary>
        public bool RateLimit()
        {
            if (File.Exists(versionFile))
            {
                var lastModified = File.GetLastWriteTime(versionFile);
                Trace.WriteLine(gameName + " last checked " + lastModified.ToString("yyyy-MM-dd HH:mm:ss"));

                if (DateTime.Now.Subtract(lastModified) > TimeSpan.FromMinutes(15))
                {
                    File.SetLastWriteTime(versionFile, DateTime.Now);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Finds the first file matching the name regex in a list of releases
        /// </summary>
        /// <param name="releases">List of releases from GitHub</param>
        /// <param name="name">Regex for name of file</param>
        /// <returns>version with without v at the start, download url</returns>
        public static (string, string) GitHubAssetDownload(IReadOnlyList<Release> releases, string name)
        {
            foreach (var release in releases)
            {
                foreach (var asset in release.Assets)
                {
                    if (Regex.IsMatch(asset.Name, name, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                        return (release.TagName.TrimStart('v'), asset.BrowserDownloadUrl);
                }
            }
            return ("", "");
        }

        public void TryFindNewestVersion()
        {
            try
            {
                FindNewestVersion();
            }
            catch (Exception ex)
            {
                State = GameState.failed;
                ProgressText = $"{ex.GetType().Name} while checking for updates - {ex.Message}";
                if (File.Exists(gameExe)) State = GameState.clickPlay;
            }
        }
        public async void FindNewestVersion()
        {
            if (downloadVersionUrl == null)
            {
                // GitHub Releases
                var releases = await client.Repository.Release.GetAll(githubOwner, githubRepo);
                (var version, downloadUrl) = GitHubAssetDownload(releases, gameName + "(.+)?.zip");
                OnlineVersion = new Version(version);
                Trace.WriteLine($"{githubOwner}/{githubRepo} {OnlineVersion} -> {downloadUrl}");

                if (!File.Exists(versionFile) || !File.Exists(gameExe))
                {
                    State = GameState.clickInstall;
                    return;
                }

                LocalVersion = new Version(File.ReadAllText(versionFile));
                {
                    if (OnlineVersion == _localVersion)
                        State = GameState.clickPlay;
                    else
                    {
                        State = GameState.clickUpdate;
                        File.WriteAllText(updateFile, OnlineVersion.ToString());
                    }
                }
            }
            else
            {
                // Download Links
                using HttpClient client = new();
                HttpResponseMessage response = await client.GetAsync(downloadVersionUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new FileNotFoundException();
                }

                var contentString = await response.Content.ReadAsStringAsync();
                OnlineVersion = new Version(contentString.TrimStart('v'));
                Trace.WriteLine($"{gameName} {OnlineVersion} -> {downloadUrl}");

                if (!File.Exists(versionFile) || !File.Exists(gameExe))
                {
                    State = GameState.clickInstall;
                    return;
                }

                if (OnlineVersion == _localVersion)
                    State = GameState.clickPlay;
                else
                {
                    State = GameState.clickUpdate;
                    File.WriteAllText(updateFile, OnlineVersion.ToString());
                }
            }
        }

        private void DownloadGame()
        {
            State = GameState.downloading;
            try
            {
                Directory.CreateDirectory(settings.DownloadPath);

                FileDownloader downloader = new();
                downloader.DownloadProgressChanged += new FileDownloader.DownloadProgressChangedEventHandler(UpdateProgressCallback);
                downloader.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompletedCallback);
                downloader.DownloadFileAsync(downloadUrl, Path.Combine(settings.DownloadPath, gameName + "_v" + OnlineVersion + ".zip"), OnlineVersion);
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
                string zipPath = Path.Combine(settings.DownloadPath, gameName + "_v" + version + ".zip");

                if (Directory.Exists(gamePath)) Directory.Delete(gamePath, true);

                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, settings.GamesPath));
                if (!settings.KeepDownloads) File.Delete(zipPath);

                File.WriteAllText(versionFile, version.ToString());
                LocalVersion = version;

                if (!Directory.Exists(gamePath))
                {
                    // fallback for folders like MothershipDefender2_v2.3.1
                    var gamePath2 = gamePath + "_v" + LocalVersion;
                    if (Directory.Exists(gamePath2)) gamePath = gamePath2;
                }
                gameExe = Path.Combine(gamePath, gameExeName);

                try { File.Delete(updateFile); } catch { }
                State = GameState.clickPlay;
            }
            catch (Exception ex)
            {
                State = GameState.failedRetry;
                MessageBox.Show($"Error installing game: {ex.Message}");
            }
        }

        // Source: https://stackoverflow.com/a/14488941
        private static readonly string[] sizeSuffixes = { "bytes", "KiB", "MiB", "GiB", "TiB" };
        public static string SizeSuffix(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException(nameof(decimalPlaces)); }
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
                sizeSuffixes[mag]);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyname = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
    }
}
