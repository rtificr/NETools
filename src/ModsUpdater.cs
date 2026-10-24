using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using ModLoader;
using SFS.Input;
using SFS.IO;
using SFS.UI;
using SFS.UI.ModGUI;
using UnityEngine;

namespace NETools
{
    public static class ModsUpdater
    {
        // Shared HttpClient instance
        static readonly HttpClient Http = new();
        // Tracks how many files were successfully updated
        static int loadedFiles;
        static bool updateStarted;

        sealed class PendingFile
        {
            public readonly FilePath Destination;
            public readonly string StagedPath;

            public PendingFile(FilePath destination, string stagedPath)
            {
                Destination = destination;
                StagedPath = stagedPath;
            }
        }

        // Entry point for running the update process detached from scene context
        internal static void StartUpdate()
        {
            if (updateStarted) return;
            updateStarted = true;
            UpdateAll().Forget();
        }

        // Main update flow
        static async UniTask UpdateAll()
        {
            loadedFiles = 0;
            var tempDir = Path.Combine(Path.GetTempPath(), "ModUpdates", Guid.NewGuid().ToString());
            try
            {
                Directory.CreateDirectory(tempDir);
                // Download candidate files to temporary storage and compare locally. Any download
                // URL can be hosted on any domain; no separate hash service is needed.
                var updatesByMod = await GetFilesNeedingUpdate(tempDir);
                if (updatesByMod.Count == 0) return;

                // Ask user to confirm update
                if (!await ConfirmUpdatePrompt(updatesByMod))
                    return;

                // Tracks failed mods and which files failed in each
                var failedMods = new Dictionary<Mod, List<string>>();

                // Attempt update per mod
                foreach (var entry in updatesByMod)
                {
                    Mod mod = entry.Key;
                    var files = entry.Value;

                    if (TryInstallMod(mod, files, failedMods)) continue;

                    // Ensure mod is listed in failedMods if any file fails
                    if (!failedMods.ContainsKey(mod))
                        failedMods[mod] = new List<string>();
                }

                // Retry failed mods if user agrees
                if (failedMods.Count > 0 && await ConfirmRetryPrompt(failedMods.Keys))
                {
                    foreach (Mod mod in failedMods.Keys.ToList())
                    {
                        var files = updatesByMod[mod];
                        if (TryInstallMod(mod, files, failedMods))
                            failedMods.Remove(mod);
                    }

                    // Show final list of failed files (if any)
                    if (failedMods.Count > 0)
                        await ShowFailedModsPrompt(failedMods);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch (Exception cleanupError)
                {
                    Debug.Log($"[ModUpdater] Could not clean temporary update files: {cleanupError.Message}");
                }

                // If any files were updated, offer to restart
                if (loadedFiles > 0)
                    MenuGenerator.OpenConfirmation(CloseMode.Current,
                        () => "Updates successful. Restart so changes take effect?",
                        () => "Restart",
                        ApplicationUtility.Relaunch);
            }
        }

        // Downloads update candidates to tempDir, compares each against the installed file, and
        // keeps only changed candidates staged for installation after user confirmation.
        static async UniTask<Dictionary<Mod, List<PendingFile>>> GetFilesNeedingUpdate(string tempDir)
        {
            var result = new Dictionary<Mod, List<PendingFile>>();
            var candidates = Loader.main.GetAllMods().OfType<IUpdatable>()
                .Where(updatable => updatable is Mod)
                .SelectMany(updatable => updatable.UpdatableFiles.Select(file =>
                    (Mod: (Mod)updatable, Url: file.Key, Destination: file.Value)))
                .ToList();
            var semaphore = new SemaphoreSlim(3);

            await UniTask.WhenAll(candidates.Select(async candidate =>
            {
                await semaphore.WaitAsync();
                var stagedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".download");
                try
                {
                    if (!await Download(candidate.Url, stagedPath))
                    {
                        Debug.Log($"[ModUpdater] Failed to check '{candidate.Url}' for mod '{candidate.Mod.DisplayName}'");
                        return;
                    }

                    var localHash = candidate.Destination.FileExists()
                        ? HashFile(candidate.Destination)
                        : Array.Empty<byte>();
                    var remoteHash = HashFile(stagedPath);
                    if (localHash.SequenceEqual(remoteHash))
                    {
                        File.Delete(stagedPath);
                        return;
                    }

                    lock (result)
                    {
                        if (!result.TryGetValue(candidate.Mod, out var files))
                        {
                            files = new List<PendingFile>();
                            result[candidate.Mod] = files;
                        }
                        files.Add(new PendingFile(candidate.Destination, stagedPath));
                    }
                }
                catch (Exception ex)
                {
                    if (File.Exists(stagedPath)) File.Delete(stagedPath);
                    Debug.Log($"[ModUpdater] Error checking update for '{candidate.Url}': {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }));

            return result;
        }

        // Installs files already staged by GetFilesNeedingUpdate. Staged copies remain available
        // if retry is offered, avoiding a second download.
        static bool TryInstallMod(Mod mod, List<PendingFile> files, Dictionary<Mod, List<string>> failedMods)
        {
            var failedFiles = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    File.Copy(file.StagedPath, file.Destination, true);
                    loadedFiles++;
                }
                catch (Exception ex)
                {
                    var fileName = Path.GetFileName(file.Destination);
                    failedFiles.Add(fileName);
                    Debug.Log($"[ModUpdater] Failed to install '{fileName}' for mod '{mod.DisplayName}': {ex.Message}");
                }
            }

            if (failedFiles.Count == 0) return true;
            failedMods[mod] = failedFiles;
            return false;
        }

        // Download a file and write to disk
        static async UniTask<bool> Download(string url, string path)
        {
            try
            {
                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode) return false;

                using var source = await resp.Content.ReadAsStreamAsync();
                using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination);
                return destination.Length > 0;
            }
            catch (Exception ex)
            {
                Debug.Log($"[ModUpdater] Exception during download: {ex.Message}");
                if (File.Exists(path)) File.Delete(path);
                return false;
            }
        }

        static byte[] HashFile(string path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            return md5.ComputeHash(stream);
        }

        static byte[] HashFile(FilePath path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            return md5.ComputeHash(stream);
        }

        // Confirmation prompt before updates begin
        static async UniTask<bool> ConfirmUpdatePrompt(
            Dictionary<Mod, List<PendingFile>> updates)
        {
            var list = string.Join("\n", updates
                .OrderBy(kvp => kvp.Key.DisplayName)
                .Select(kvp => kvp.Key.DisplayName));

            return await MenuGenerator.OpenConfirmationAsync(CloseMode.Current,
                () => $"Updates are available for the following mods:\n\n{list}\n\nInstall now?",
                () => "Update All");
        }

        // Retry prompt for mods that failed to update
        static async UniTask<bool> ConfirmRetryPrompt(IEnumerable<Mod> failed)
        {
            var list = string.Join("\n", failed.Select(m => m.DisplayName));
            return await MenuGenerator.OpenConfirmationAsync(CloseMode.Current,
                () => $"The following mods failed to update:\n\n{list}\n\nRetry failed mods?",
                () => "Retry");
        }

        // Show which files failed after retry
        static async UniTask ShowFailedModsPrompt(Dictionary<Mod, List<string>> failedMods)
        {
            var msg = string.Join("\n\n", failedMods.OrderBy(m => m.Key.DisplayName)
                .Select(kvp =>
                {
                    var files = string.Join("\n  - ", kvp.Value.OrderBy(f => f));
                    return $"{kvp.Key.DisplayName}\n  - {files}";
                }));

            await MenuGenerator.OpenConfirmationAsync(CloseMode.Current,
                () => $"The following mods still failed to update:\n\n{msg}",
                () => "OK");
        }
    }

    /// <summary>
    ///     Implement this interface on main mod class if you want it to be updated at game start
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public interface IUpdatable
    {
        /// <summary>
        ///     Returns dictionary of files that should be updated
        ///     string is web link, FilePath is path where file will be downloaded
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, FilePath> UpdatableFiles { get; }
    }
}
