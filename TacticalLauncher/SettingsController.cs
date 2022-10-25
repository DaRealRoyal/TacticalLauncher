using Ookii.Dialogs.Wpf;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
                return _selectCommand ??= new CommandHandler((x) => StartFolderBrowser(x), () => true);
            }
        }

        public void StartFolderBrowser(object path)
        {
            // needs at least windows 7
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                (Environment.OSVersion.Version.Major != 6 || Environment.OSVersion.Version.Minor <= 0) &&
                Environment.OSVersion.Version.Major <= 6)
            {
                MessageBox.Show($"Sorry, the folder browser dialog is currently not supported on your platform.");
                return;
            }

            VistaFolderBrowserDialog ookiiDialog = new();
            if (ookiiDialog.ShowDialog() == true)
            {
                switch (path)
                {
                    case "GamesPath":
                        GamesPath = ookiiDialog.SelectedPath;
                        break;
                    case "DownloadPath":
                        DownloadPath = ookiiDialog.SelectedPath;
                        break;
                }
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
                var value_replaced = value.Replace("%LauncherPath%", LauncherPath, StringComparison.InvariantCultureIgnoreCase);

                // check whether path works
                try
                {
                    Directory.CreateDirectory(value_replaced);

                    // check if target is not empty and prompt for confirmation
                    if (Directory.EnumerateFileSystemEntries(value_replaced).Any() &&
                        MessageBox.Show("The selected path is not empty.\nDo you really want to continue?", "Moving Games", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;

                    using FileStream fs = File.Create(
                        Path.Combine(value_replaced, Path.GetRandomFileName()),
                        1, FileOptions.DeleteOnClose);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Can't write to directory {value_replaced}: {ex.Message}\nKeeping the old path.");
                    return;
                }

                // move files
                if (!Equals(GamesPath, value_replaced) &&
                    MessageBox.Show("Move files to new path?", "Moving Games", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
				    if (MessageBox.Show("Please make sure all games are closed and no download or install is in progress.", "Moving Games", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.Ok)
					    return;

				    // TODO: disable all buttons and inputs
				    //       https://social.msdn.microsoft.com/Forums/en-US/481935ac-3e89-40c2-be5a-c560ed7e705c/need-to-disable-all-controls-on-window-xaml-from-code-behind
			        // TODO: progress bar/text

                    // copy files over
                    try
                    {
                        CopyDirectory(GamesPath, value_replaced, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Couldn't copy files: {ex.Message}\nKeeping the old path.");
                        
                        // delete incomplete copy
                        try { Directory.CreateDirectory(value_replaced, true); } catch { }

                        return;
                    }

                    // delete old files
                    try
                    {
                        Directory.Delete(GamesPath, true); 
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Couldn't fully delete old files: {ex.Message}\nThere may be files left at the old path.");
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

                Properties.Settings.Default.Save();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(RawGamesPath));

                // restart launcher to update game states
                if (MessageBox.Show("Successfully moved games, restarting launcher.", "Done Moving Games", MessageBoxButton.Ok) == MessageBoxResult.Ok)
                {
                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
            }
        }
        
        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
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
                if (MessageBox.Show("Please make sure no download or install is in progress.", "Moving Downloads", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.Ok)
                    return;

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

                if (!Equals(DownloadPath, value_replaced) &&
                    MessageBox.Show("Delete old downloads directory?", "Moving Downloads", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Directory.Delete(DownloadPath, true);
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
                if (Properties.Settings.Default.keepDownloads && !value && Directory.Exists(DownloadPath) &&
                    MessageBox.Show("Clean downloads directory?", "Keep Downloads", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Directory.Delete(DownloadPath, true);
                    Directory.CreateDirectory(DownloadPath);
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
