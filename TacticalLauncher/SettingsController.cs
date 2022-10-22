using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace TacticalLauncher
{
    public class SettingsController : INotifyPropertyChanged
    {
        public SettingsController()
        {
            Directory.CreateDirectory(GamesPath);
            Directory.CreateDirectory(DownloadPath);
        }

        private ICommand _selectCommand;
        public ICommand SelectCommand
        {
            get
            {
                return _selectCommand ??= new CommandHandler((x) => StartExplorer(x), () => true);  // TODO: start explorer folder selector
            }
        }

        private ICommand _resetCommand;
        public ICommand ResetCommand
        {
            get
            {
                return _resetCommand ??= new CommandHandler((x) =>
                {
                    switch (x)
                    {
                        case "GamesPath":
                            GamesPath = @"%LauncherPath%\Games";
                            break;
                        case "DownloadPath":
                            DownloadPath = @"%GamesPath%\Downloads";
                            break;
                    }
                }, () => true);
            }
        }

        private ICommand _browseCommand;
        public ICommand BrowseCommand => _browseCommand ??= new CommandHandler((x) => StartExplorer(x), () => true);

        public static void StartExplorer(object path) => Process.Start("explorer.exe", path.ToString());

        public static string LauncherPath => Directory.GetParent(Directory.GetCurrentDirectory()).ToString();

        public string RawGamesPath
        {
            get => Properties.Settings.Default.downloadPath;
            set => GamesPath = value;
        }

        public string GamesPath
        {
            get => Properties.Settings.Default.downloadPath.Replace("%LauncherPath%", LauncherPath, StringComparison.InvariantCultureIgnoreCase);
            set
            {
                // TODO: don't change while busy,
                //       check whether any game is running and close it and/or cancel the change to prevent unnecessary problems,
                //       prevent interactions with games while changing setting (especially while moving games) (lock window and show progress bar?),
                //       handle problems while moving (don't crash, show message box with retry button, detect and handle incomplete move)
                var value_replaced = value.Replace("%LauncherPath%", LauncherPath, StringComparison.InvariantCultureIgnoreCase);

                // check whether path works
                try
                {
                    Directory.CreateDirectory(value_replaced);
                    using FileStream fs = File.Create(
                        Path.Combine(value_replaced, Path.GetRandomFileName()),
                        1, FileOptions.DeleteOnClose);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Can't write to directory {value_replaced}: {ex.Message}\nKeeping the old path.");
                    return;
                }

                if (!Equals(GamesPath, value_replaced))
                {
                    MessageBoxResult result = MessageBox.Show("Move files to new path?", "Moving Games", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        // TODO: move files or copy and delete if on different volumes
                        // https://stackoverflow.com/questions/3911595/move-all-files-in-subfolders-to-another-folder
                    }
                }

                Properties.Settings.Default.downloadPath = value;

                // reset DownloadPath if moving from DownloadPath to GamesPath doesn't work
                var testPathDownloads = Path.Combine(DownloadPath, Path.GetRandomFileName());
                var testPathGames = Path.Combine(GamesPath, Path.GetRandomFileName());
                try
                {
                    Directory.CreateDirectory(DownloadPath);
                    File.Create(testPathDownloads).Dispose();
                    File.Move(testPathDownloads, testPathGames);
                    File.Delete(testPathGames);
                }
                catch
                {
                    // if the move wasn't successful, the test file needs to be deleted
                    File.Delete(testPathDownloads);

                    DownloadPath = @"%GamesPath%\Downloads";
                }

                // TODO: update game states
                //       (an easy way is to restart the application, but I would prefer a seamless experience)

                Properties.Settings.Default.Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RawGamesPath));
            }
        }

        public string RawDownloadPath
        {
            get => Properties.Settings.Default.gamesPath;
            set => DownloadPath = value;
        }

        public string DownloadPath
        {
            get => Properties.Settings.Default.gamesPath
                    .Replace("%GamesPath%", GamesPath, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("%LauncherPath%", LauncherPath, StringComparison.InvariantCultureIgnoreCase);
            set
            {
                // TODO: don't change while downloading or installing
                var value_replaced = value
                    .Replace("%GamesPath%", GamesPath, StringComparison.InvariantCultureIgnoreCase)
                    .Replace("%LauncherPath%", LauncherPath, StringComparison.InvariantCultureIgnoreCase);

                // check whether path works and moving to GamesPath is possible
                try
                {
                    Directory.CreateDirectory(value_replaced);
                    var testPathDownloads = Path.Combine(value_replaced, Path.GetRandomFileName());
                    var testPathGames = Path.Combine(GamesPath, Path.GetRandomFileName());
                    using (FileStream fs = File.Create(testPathDownloads, 1)) { }
                    File.Move(testPathDownloads, testPathGames);
                    File.Delete(testPathGames);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Either can't write to directory {value_replaced} or can't move files from there to the GamesPath: {ex.Message}\nKeeping the old path.");
                    return;
                }

                if (!Equals(DownloadPath, value_replaced))
                {
                    MessageBoxResult result = MessageBox.Show("Delete old downloads directory?", "Moving Downloads", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes) Directory.Delete(DownloadPath, true);
                }

                Properties.Settings.Default.gamesPath = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RawDownloadPath));
            }
        }

        public bool KeepDownloads
        {
            get => Properties.Settings.Default.keepDownloads;
            set
            {
                // delete files if changed from checked to unchecked
                if (Properties.Settings.Default.keepDownloads && !value)
                {
                    MessageBoxResult result = MessageBox.Show("Clean downloads directory?", "Keep Downloads", MessageBoxButton.YesNo);
                    switch (result)
                    {
                        case MessageBoxResult.Yes:
                            Directory.Delete(DownloadPath, true);
                            Directory.CreateDirectory(DownloadPath);
                            break;
                    }
                }

                Properties.Settings.Default.keepDownloads = value;
                Properties.Settings.Default.Save();
                RaisePropertyChanged();
            }
        }

        public static string GetConfigPath(ConfigurationUserLevel userLevel)
        {
            try
            {
                var UserConfig = ConfigurationManager.OpenExeConfiguration(userLevel);
                return UserConfig.FilePath;
            }
            catch (ConfigurationException e)
            {
                return e.Filename;
            }
        }
        public static string ConfigPath => GetConfigPath(ConfigurationUserLevel.PerUserRoamingAndLocal);
        public static string ConfigPathDirectory => Path.GetDirectoryName(ConfigPath);

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }
    }
}
