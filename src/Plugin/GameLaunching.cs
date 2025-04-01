using System;
using System.Collections.Generic;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace ArchiveCacheManager
{
    class GameLaunching : IGameLaunchingPlugin
    {
        private static readonly string tempArchivePath = PathUtils.GetTempArchivePath();

        public void OnAfterGameLaunched(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            Logger.Log("Game started.");
            LaunchBoxDataBackup.RestoreAllSettings();
        }

        private static bool UseArchiveCacheManager(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            // Extract ROMs must be enabled for the emulator \ platform
            if (!PluginUtils.GetEmulatorPlatformAutoExtract(emulator.Id, game.Platform))
            {
                return false;
            }

            string key = Config.EmulatorPlatformKey(emulator.Title, game.Platform);
            // In case where game is multi-disc, emulator supports m3u, and multi-disc support is disabled, don't try extract anything
            // as it will be incompatible with LaunchBox's m3u creation.
            if (!Config.GetMultiDisc(key) && PluginUtils.IsLaunchedGameMultiDisc(game, app) && PluginUtils.GetEmulatorPlatformM3uDiscLoadEnabled(emulator.Id, game.Platform))
            {
                return false;
            }

            string archivePath = PluginUtils.GetArchivePath(game, app);
            bool extract = (Config.GetAction(key) == Config.Action.Extract || Config.GetAction(key) == Config.Action.ExtractCopy);
            bool copy = (Config.GetAction(key) == Config.Action.Copy || Config.GetAction(key) == Config.Action.ExtractCopy);

            if (extract && (Zip.SupportedType(archivePath)
                            || (Config.GetChdman(key) && Chdman.SupportedType(archivePath))
                            || (Config.GetDolphinTool(key) && DolphinTool.SupportedType(archivePath))
                            || (Config.GetExtractXiso(key) && ExtractXiso.SupportedType(archivePath))))
            {
                return true;
            }
            else if (copy)
            {
                return true;
            }

            return false;
        }

        private static bool RedirectApplicationPath(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            if (Config.BypassPathCheck)
            {
                return true;
            }

            string archivePath = PluginUtils.GetArchivePath(game, app);
            if (PathUtils.HasExtension(archivePath, new string[] { ".zip", ".7z", ".rar" }))
            {
                return false;
            }

            string key = Config.EmulatorPlatformKey(emulator.Title, game.Platform);
            bool extract = (Config.GetAction(key) == Config.Action.Extract || Config.GetAction(key) == Config.Action.ExtractCopy);
            bool copy = (Config.GetAction(key) == Config.Action.Copy || Config.GetAction(key) == Config.Action.ExtractCopy);

            // Always redirect on copy or extract/copy when type isn't zip, 7z or rar (already checked above)
            if (copy)
            {
                return true;
            }
            // Only redirect extract when the extrator is enabled and the file type matches
            else if (extract && (Zip.SupportedType(archivePath)
                                 || (Config.GetChdman(key) && Chdman.SupportedType(archivePath))
                                 || (Config.GetDolphinTool(key) && DolphinTool.SupportedType(archivePath))
                                 || (Config.GetExtractXiso(key) && ExtractXiso.SupportedType(archivePath))))
            {
                return true;
            }

            // Only extract is enabled, but there's no corresponding extractor. Don't redirect and let the launch fail.
            return false;
        }

        private static (bool, string, string) GetExtractorExists(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            string archivePath = PluginUtils.GetArchivePath(game, app);
            string key = Config.EmulatorPlatformKey(emulator.Title, game.Platform);
            bool extract = (Config.GetAction(key) == Config.Action.Extract || Config.GetAction(key) == Config.Action.ExtractCopy);
            bool copy = (Config.GetAction(key) == Config.Action.Copy || Config.GetAction(key) == Config.Action.ExtractCopy);
            Extractor extractor;

            if (extract && Config.GetChdman(key) && Chdman.SupportedType(archivePath))
            {
                extractor = new Chdman();
            }
            else if (extract && Config.GetDolphinTool(key) && DolphinTool.SupportedType(archivePath))
            {
                extractor = new DolphinTool();
            }
            else if (extract && Config.GetExtractXiso(key) && ExtractXiso.SupportedType(archivePath))
            {
                extractor = new ExtractXiso();
            }
            else if (extract && Zip.SupportedType(archivePath))
            {
                extractor = new Zip();
            }
            else if (copy)
            {
                extractor = new Robocopy();
            }
            else
            {
                extractor = new Zip();
            }

            string extractorPath = extractor.GetExtractorPath();
            bool extractorExists = string.IsNullOrEmpty(extractorPath) ? true : File.Exists(extractorPath);

            return (extractorExists, extractor.Name(), extractorPath);
        }

        public static GameInfo SaveGameInfo(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            GameInfo gameInfo = new GameInfo(PathUtils.GetGameInfoPath());
            gameInfo.GameId = game.Id;
            gameInfo.ArchivePath = PluginUtils.GetArchivePath(game, app);
            gameInfo.Emulator = emulator.Title;
            gameInfo.Platform = game.Platform;
            gameInfo.Title = game.Title;
            gameInfo.Version = game.Version;
            gameInfo.SelectedFile = GameIndex.GetSelectedFile(game.Id);
            gameInfo.EmulatorPlatformM3u = PluginUtils.GetEmulatorPlatformM3uDiscLoadEnabled(emulator.Id, game.Platform);
            gameInfo.MultiDisc = PluginUtils.IsLaunchedGameMultiDisc(game, app);
            if (gameInfo.MultiDisc)
            {
                Logger.Log("Multi-disc game detected.");
                var (totalDiscs, selectedDisc, discs) = PluginUtils.GetMultiDiscInfo(game, app);

                gameInfo.TotalDiscs = totalDiscs;
                gameInfo.SelectedDisc = selectedDisc;
                gameInfo.Discs = discs;
            }
            gameInfo.Save();

            return gameInfo;
        }

            public void OnBeforeGameLaunching(IGame game, IAdditionalApplication app, IEmulator emulator)
        {
            if (UseArchiveCacheManager(game, app, emulator))
            {
                Logger.Log(string.Format("-------- {0} --------", game.Title.ToUpper()));
                Logger.Log(string.Format("Preparing cache for {0} ({1}) running with {2}.", game.Title, game.Platform, emulator.Title));

                (bool extractorExists, string extractorName, string extractorPath) = GetExtractorExists(game, app, emulator);
                if (!extractorExists)
                {
                    FlexibleMessageBox.Show(string.Format("Attempting to extract using {0}, but couldn't find {1} in {2}.\r\n\r\n"
                                            + "Please place a copy of {1} in this folder and try again, or disable the {0} option.\r\n\r\n"
                                            + "Game launch will continue without extraction or caching.", extractorName, Path.GetFileName(extractorPath), Path.GetDirectoryName(extractorPath)),
                                            "Archive Cache Manager Extractor Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LaunchBoxDataBackup.RestoreAllSettings();

                GameInfo gameInfo = SaveGameInfo(game, app, emulator);

                // This code block is responsible for making temporary settings changes, to support different game launch scenarios.
                // All settings are reverted after a game has launched, or after 5 seconds.
                #region Temporary settings changes
                Directory.CreateDirectory(PathUtils.GetTempPath());
                LaunchBoxDataBackup.SetLaunchDetails(game, app, emulator);
                #region Multi-Disc Support
                if (gameInfo.MultiDisc && Config.GetMultiDisc(Config.EmulatorPlatformKey(emulator.Title, game.Platform)) && gameInfo.EmulatorPlatformM3u)
                {
                    LaunchBoxDataBackup.BackupSetting(LaunchBoxDataBackup.SettingName.IEmulatorPlatform_M3uDiscLoadEnabled, true);
                    PluginUtils.SetEmulatorPlatformM3uDiscLoadEnabled(emulator.Id, game.Platform, false);
                    Logger.Log(string.Format("Temporarily set IEmulatorPlatform.M3uDiscLoadEnabled for {0} \\ {1} to {2}.", emulator.Title, game.Platform, false));
                }
                #endregion
                #region Game / App ApplicationPath

                if (RedirectApplicationPath(game, app, emulator))
                {
                    if (app != null)
                    {
                        DiskUtils.CreateFile(tempArchivePath);
                        LaunchBoxDataBackup.BackupSetting(LaunchBoxDataBackup.SettingName.IAdditionalApplication_ApplicationPath, app.ApplicationPath);
                        app.ApplicationPath = tempArchivePath;
                        Logger.Log(string.Format("Temporarily set IAdditionalApplication.ApplicationPath for {0} ({1} - {2}) to {3}.", app.Name, game.Title, game.Platform, app.ApplicationPath));
                    }
                    else
                    {
                        DiskUtils.CreateFile(tempArchivePath);
                        LaunchBoxDataBackup.BackupSetting(LaunchBoxDataBackup.SettingName.IGame_ApplicationPath, game.ApplicationPath);
                        game.ApplicationPath = tempArchivePath;
                        Logger.Log(string.Format("Temporarily set IGame.ApplicationPath for {0} ({1}) to {2}.", game.Title, game.Platform, game.ApplicationPath));
                    }
                }
                
                #endregion
                if (LaunchBoxDataBackup.Settings.Count > 0)
                {
                    LaunchBoxDataBackup.Save();
                    LaunchBoxDataBackup.RestoreAllSettingsDelay(5000);
                }
                #endregion
            }
        }

        public void OnGameExited()
        {
                // The temp path will be empty most times, but in the case where files were extracted to temp (the cache folder
                // doesn't exist or the archive is too small) AND a file priority has been applied, non priority files won't be
                // removed from the temp folder (as LB doesn't know about them). Force clear this folder on game exit.
                DiskUtils.DeleteDirectory(PathUtils.GetLaunchBox7zTempPath(), true);

                if (!PluginHelper.StateManager.IsBigBox)
                {
                    PluginHelper.LaunchBoxMainViewModel.RefreshData();
                }
        }

    }
}
