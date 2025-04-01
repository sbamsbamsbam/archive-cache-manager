using System;
using System.Diagnostics;
using System.Collections.Generic;
using Unbroken.LaunchBox.Plugins;
using System.IO;

namespace ArchiveCacheManager
{
    class SystemEvents : ISystemEventsPlugin
    {
        public void OnEventRaised(string eventType)
        {
            if (eventType == "PluginInitialized")
            {
#if DEBUG
                //Debugger.Launch();
#endif
                Logger.Init();
                Logger.Log("-------- PLUGIN INITIALIZED --------");
                Logger.Log(string.Format("Archive Cache Manager plugin initialized ({0}).", CacheManager.VersionString));
                // Restore 7z in event Archive Cache Manager files are still in ThirdParty\7-Zip (caused by crash, etc)
                bool copyError = false;
				string pluginRootPath = PathUtils.GetPluginRootPath();
				string plugin7zRootPath = PathUtils.GetPlugin7zRootPath();
				string launchBox7zRootPath = PathUtils.GetLaunchBox7zRootPath();
				Dictionary<string, string> paths = new Dictionary<string, string>();

				paths.Add(Path.Combine(pluginRootPath, "ArchiveCacheManager.exe"), Path.Combine(launchBox7zRootPath, "7z.exe"));
				paths.Add(Path.Combine(plugin7zRootPath, "7z.exe.original"), Path.Combine(launchBox7zRootPath, "7-zip.exe"));
				paths.Add(Path.Combine(pluginRootPath, "ArchiveCacheManager.Core.dll"), Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.Core.dll"));
				paths.Add(Path.Combine(pluginRootPath, "ConfigParser.dll"), Path.Combine(launchBox7zRootPath, "ConfigParser.dll"));
				paths.Add(Path.Combine(pluginRootPath, "ArchiveCacheManager.dll"), Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.dll"));
				paths.Add(Path.Combine(pluginRootPath, "ArchiveCacheManager.runtimeconfig.json"), Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.runtimeconfig.json"));
				
				foreach (var path in paths)
				{
					try
					{
						File.Copy(path.Key, path.Value, true);
					}
					catch (Exception e)
					{
						copyError = true;
						Logger.Log(e.ToString(), Logger.LogLevel.Exception);
                    break;
					}
				}

				if (copyError)
				{
					Logger.Log("Error setting up Archive Cache Manager, cleaning up 7-Zip folder.");
					string[] pathz = new string[] { Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.Core.dll"),
													Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.dll"),
													Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.runtimeconfig.json"),
													Path.Combine(launchBox7zRootPath, "ConfigParser.dll"),
													Path.Combine(launchBox7zRootPath, "7-zip.exe"),
													PathUtils.GetGameInfoPath() };

					try
					{
						File.Copy(Path.Combine(plugin7zRootPath, "7z.exe.original"), Path.Combine(launchBox7zRootPath, "7z.exe"), true);
						File.Copy(Path.Combine(plugin7zRootPath, "7z.dll.original"), Path.Combine(launchBox7zRootPath, "7z.dll"), true);
					}
					catch (Exception e)
					{
						Logger.Log(e.ToString(), Logger.LogLevel.Exception);
					}

					foreach (string path in pathz)
					{
						try
						{
							if (File.Exists(path))
							{
								File.Delete(path);
							}
						}
						catch (Exception e)
						{
							Logger.Log(e.ToString(), Logger.LogLevel.Exception);
						}
					}
				}

                // Remove any invalid entries from the cache (from failed or aborted launches, or game.ini changes)
                CacheManager.VerifyCacheIntegrity();
            }
            // Only perform the actions below in LaunchBox
            else if (eventType == "LaunchBoxStartupCompleted")
            {
                // Restore any overridden settings if LaunchBox closed before they could be restored on normal game launch
                LaunchBoxDataBackup.RestoreAllSettingsDelay(1000);

                if (Config.UpdateCheck == true)
                {
                    Updater.CheckForUpdate(2000);
                }
                // UpdateCheck will be null if the option has never been set before. Prompt the user to enable or disable update checks.
                else if (Config.UpdateCheck == null)
                {
                    Updater.EnableUpdateCheckPrompt(2000);
                }
			}
			else if (eventType == "LaunchBoxShutdownBeginning" || eventType == "BigBoxShutdownBeginning")
            {
                Logger.Log("LaunchBox / BigBox shutdown.");
                // Restore 7z in event Archive Cache Manager files are still in ThirdParty\7-Zip (caused by crash, etc)
                string plugin7zRootPath = PathUtils.GetPlugin7zRootPath();
				string launchBox7zRootPath = PathUtils.GetLaunchBox7zRootPath();

				string[] paths = new string[] { Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.Core.dll"),
												Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.dll"),
												Path.Combine(launchBox7zRootPath, "ArchiveCacheManager.runtimeconfig.json"),
												Path.Combine(launchBox7zRootPath, "ConfigParser.dll"),
												Path.Combine(launchBox7zRootPath, "7-zip.exe"),
												PathUtils.GetGameInfoPath() };

				try
				{
					File.Copy(Path.Combine(plugin7zRootPath, "7z.exe.original"), Path.Combine(launchBox7zRootPath, "7z.exe"), true);
					File.Copy(Path.Combine(plugin7zRootPath, "7z.dll.original"), Path.Combine(launchBox7zRootPath, "7z.dll"), true);
				}
				catch (Exception e)
				{
					Logger.Log(e.ToString(), Logger.LogLevel.Exception);
				}

				foreach (string path in paths)
				{
					try
					{
						if (File.Exists(path))
						{
							File.Delete(path);
						}
					}
					catch (Exception e)
					{
						Logger.Log(e.ToString(), Logger.LogLevel.Exception);
					}
				}
                // Restore any overridden settings if LaunchBox closed before they could be restored on normal game launch
                LaunchBoxDataBackup.RestoreAllSettings();
            }
        }
    }
}
