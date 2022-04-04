﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ArchiveCacheManager
{
    /// <summary>
    /// Singleton class containing current launched game information, including that game's cache information.
    /// </summary>
    public class LaunchGameInfo
    {
        public class CacheData
        {
            public string ArchivePath;
            public string ArchiveCachePath;
            public bool? ArchiveInCache;
            public long? DecompressedSize;
            public bool? ExtractSingleFile;
            public bool CopyOnly;
        };

        /// <summary>
        /// The extensions in this list can be extracted, copied, and run individually without dependence on other files.
        /// </summary>
        private static readonly List<string> StandaloneFileList = new List<string> { ".gb", ".gbc", ".gba", ".agb", ".nes", ".fds", ".smc", ".sfc", ".n64", ".z64", ".v64", ".ndd", ".md", ".smd", ".gen", ".iso", ".chd", ".rvn", ".gg", ".gcm", ".32x", ".bin" };
        /// <summary>
        /// The extensions in this list are considered metadata, and are not required by an emulator to play
        /// </summary>
        private static readonly List<string> MetadataFileList = new List<string> { ".nfo", ".txt", ".dat", ".xml", ".json" };

        private static GameInfo mGame;
        private static CacheData mGameCacheData;
        private static Dictionary<int, CacheData> mMultiDiscCacheData;

        /// <summary>
        /// The game info from LaunchBox.
        /// </summary>
        public static GameInfo Game => mGame;

        static LaunchGameInfo()
        {
            mGame = new GameInfo(PathUtils.GetGameInfoPath());
            mGameCacheData = new CacheData();
            mGameCacheData.ArchivePath = mGame.ArchivePath;
            mGameCacheData.ArchiveCachePath = PathUtils.ArchiveCachePath(mGame.ArchivePath);
            mGameCacheData.CopyOnly = /* Config.Copy && */ PathUtils.IsPathCompressedArchive(mGame.ArchivePath);
            if (mGame.InfoLoaded)
            {
                Logger.Log(string.Format("Archive path set to \"{0}\".", mGameCacheData.ArchivePath));
                Logger.Log(string.Format("Archive cache path set to \"{0}\".", mGameCacheData.ArchiveCachePath));
            }
            mMultiDiscCacheData = new Dictionary<int, CacheData>();
            foreach (var disc in mGame.Discs)
            {
                mMultiDiscCacheData[disc.Disc] = new CacheData();
                mMultiDiscCacheData[disc.Disc].ArchivePath = disc.ArchivePath;
                mMultiDiscCacheData[disc.Disc].ArchiveCachePath = PathUtils.ArchiveCachePath(disc.ArchivePath);
                mMultiDiscCacheData[disc.Disc].CopyOnly = /* Config.Copy && */ PathUtils.IsPathCompressedArchive(disc.ArchivePath);
                Logger.Log(string.Format("Disc {0} archive path set to \"{1}\".", disc.Disc, mMultiDiscCacheData[disc.Disc].ArchivePath));
                Logger.Log(string.Format("Disc {0} archive cache path set to \"{1}\".", disc.Disc, mMultiDiscCacheData[disc.Disc].ArchiveCachePath));
            }
        }

        /// <summary>
        /// Get the size of the game archive when extracted. For multi-disc games, the size will be the total of all discs.
        /// </summary>
        /// <param name="disc"></param>
        /// <returns></returns>
        public static long GetDecompressedSize(int? disc = null)
        {
            if (disc != null)
            {
                try
                {
                    if (mMultiDiscCacheData[(int)disc].DecompressedSize == null)
                    {
                        mMultiDiscCacheData[(int)disc].DecompressedSize = Zip.GetDecompressedSize(mMultiDiscCacheData[(int)disc].ArchivePath);
                        Logger.Log(string.Format("Disc {0} decompressed archive size is {1} bytes.", (int)disc, (long)mMultiDiscCacheData[(int)disc].DecompressedSize));
                    }

                    return (long)mMultiDiscCacheData[(int)disc].DecompressedSize;
                }
                catch (KeyNotFoundException)
                {
                    Logger.Log(string.Format("Unknown disc number {0}, using DecompressedSize instead.", (int)disc));
                }
            }

            if (mGame.MultiDisc && Config.MultiDiscSupport && disc == null)
            {
                long multiDiscDecompressedSize = 0;

                foreach (var discCacheData in mMultiDiscCacheData)
                {
                    multiDiscDecompressedSize += GetDecompressedSize(discCacheData.Key);
                }

                return multiDiscDecompressedSize;
            }

            if (mGameCacheData.DecompressedSize == null)
            {
                string filename = null;
                if (GetExtractSingleFile())
                {
                    filename = mGame.SelectedFile;
                }

                mGameCacheData.DecompressedSize = Zip.GetDecompressedSize(mGameCacheData.ArchivePath, filename);
                mGame.DecompressedSize = (long)mGameCacheData.DecompressedSize;
                Logger.Log(string.Format("Decompressed archive size is {0} bytes.", (long)mGameCacheData.DecompressedSize));
            }

            return (long)mGameCacheData.DecompressedSize;
        }

        public static bool GetExtractSingleFile()
        {
            if (mGameCacheData.ExtractSingleFile == null)
            {
                mGameCacheData.ExtractSingleFile = false;

                if (Config.SmartExtract && !string.IsNullOrEmpty(mGame.SelectedFile))
                {
                    List<string> excludeList = new List<string>(MetadataFileList);

                    string extension = Path.GetExtension(mGame.SelectedFile).ToLower();
                    if (StandaloneFileList.Contains(extension))
                    {
                        excludeList.AddRange(StandaloneFileList);
                    }
                    else
                    {
                        excludeList.Add(extension);
                    }

                    if (Zip.GetFileList(mGame.ArchivePath, null, excludeList.ToArray(), true).Count() == 0)
                    {
                        mGameCacheData.ExtractSingleFile = true;
                        Logger.Log(string.Format("Smart Extraction enabled for file \"{0}\".", mGame.SelectedFile));
                    }
                }
            }

            return (bool)mGameCacheData.ExtractSingleFile;
        }

        /// <summary>
        /// Get the source archive path of the game. If disc is specified, the archive path will be to that disc.
        /// </summary>
        /// <param name="disc"></param>
        /// <returns></returns>
        public static string GetArchivePath(int? disc = null)
        {
            if (disc != null)
            {
                try
                {
                    return mMultiDiscCacheData[(int)disc].ArchivePath;
                }
                catch (KeyNotFoundException)
                {
                    Logger.Log(string.Format("Unknown disc number {0}, using ArchivePath instead.", (int)disc));
                }
            }

            return mGameCacheData.ArchivePath;
        }

        /// <summary>
        /// Get the cache path of the game. If disc is specified, the archive path will be to that disc.
        /// </summary>
        /// <param name="disc"></param>
        /// <returns></returns>
        public static string GetArchiveCachePath(int? disc = null)
        {
            if (disc != null)
            {
                try
                {
                    return mMultiDiscCacheData[(int)disc].ArchiveCachePath;
                }
                catch (KeyNotFoundException)
                {
                    Logger.Log(string.Format("Unknown disc number {0}, using ArchiveCachePath instead.", (int)disc));
                }
            }

            return mGameCacheData.ArchiveCachePath;
        }

        /// <summary>
        /// Check if the game is cached. Will check if all discs of a multi-disc game are cached.
        /// </summary>
        /// <param name="disc">When specified, checks if a particular disc of a game is cached.</param>
        /// <returns>True if the game is cached, and False otherwise.</returns>
        public static bool GetArchiveInCache(int? disc = null)
        {
            if (disc != null)
            {
                try
                {
                    if (mMultiDiscCacheData[(int)disc].ArchiveInCache == null)
                    {
                        mMultiDiscCacheData[(int)disc].ArchiveInCache = File.Exists(PathUtils.GetArchiveCacheGameInfoPath(mMultiDiscCacheData[(int)disc].ArchiveCachePath));
                    }

                    return (bool)mMultiDiscCacheData[(int)disc].ArchiveInCache;
                }
                catch (KeyNotFoundException)
                {
                    Logger.Log(string.Format("Unknown disc number {0}, using ArchiveInCache instead.", (int)disc));
                }
            }

            if (mGame.MultiDisc && Config.MultiDiscSupport && disc == null)
            {
                // Set true if there are multiple discs, false otherwise. When false, subsequent boolean operations will be false.
                bool multiDiscArchiveInCache = mMultiDiscCacheData.Count > 0;

                foreach (var discCacheData in mMultiDiscCacheData)
                {
                    multiDiscArchiveInCache &= GetArchiveInCache(discCacheData.Key);
                }

                return multiDiscArchiveInCache;
            }

            if (mGameCacheData.ArchiveInCache == null)
            {
                mGameCacheData.ArchiveInCache = File.Exists(PathUtils.GetArchiveCacheGameInfoPath(mGameCacheData.ArchiveCachePath));
            }

            if (Config.SmartExtract && !string.IsNullOrEmpty(mGame.SelectedFile))
            {
                mGameCacheData.ArchiveInCache &= File.Exists(Path.Combine(mGameCacheData.ArchiveCachePath, mGame.SelectedFile));
            }

            return (bool)mGameCacheData.ArchiveInCache;
        }

        /// <summary>
        /// Get the number of discs already cached.
        /// </summary>
        /// <returns>The number of game discs already in the cache. Will be 0 or 1 for non multi-disc games.</returns>
        public static int GetDiscCountInCache()
        {
            if (mGame.MultiDisc)
            {
                int discCount = 0;

                foreach (var discCacheData in mMultiDiscCacheData)
                {
                    discCount += GetArchiveInCache(discCacheData.Key) ? 1 : 0;
                }

                return discCount;
            }

            return GetArchiveInCache() ? 1 : 0;
        }

        public static void SaveToCache(int? disc = null)
        {
            if (mGame.MultiDisc && Config.MultiDiscSupport && disc == null)
            {
                foreach (var discInfo in mGame.Discs)
                {
                    SaveToCache(discInfo.Disc);
                }

                return;
            }

            string archiveCacheGameInfoPath = PathUtils.GetArchiveCacheGameInfoPath(GetArchiveCachePath(disc));
            GameInfo savedGameInfo = new GameInfo(mGame);
            GameInfo cachedGameInfo = new GameInfo(archiveCacheGameInfoPath);

            if (cachedGameInfo.InfoLoaded)
            {
                savedGameInfo.MergeCacheInfo(cachedGameInfo);
            }
            else
            {
                savedGameInfo.DecompressedSize = GetDecompressedSize(disc);
            }

            if (disc != null)
            {
                savedGameInfo.ArchivePath = mGame.Discs.Find(d => d.Disc == (int)disc).ArchivePath;
                savedGameInfo.SelectedDisc = (int)disc;
            }

            savedGameInfo.Save(archiveCacheGameInfoPath);
        }
    }
}
