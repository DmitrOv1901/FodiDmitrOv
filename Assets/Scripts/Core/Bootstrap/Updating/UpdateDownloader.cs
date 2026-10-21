#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Kern.Core;

/// <summary>
/// Загрузка файлов обновления: дифф манифеста против установленного состояния,
/// скачивание только изменённого в staging/v{version}, проверка SHA-256 и размера
/// каждого файла. Ретраи с бэкоффом; прогресс — по байтам. Частичные/битые файлы
/// в применении не участвуют: applier запускается только после полной верификации.
/// </summary>
public sealed class UpdateDownloader
{
    public readonly record struct Progress(long DownloadedBytes, long TotalBytes, int FilesDone, int FilesTotal)
    {
        public float Fraction => TotalBytes <= 0 ? 0f : (float)((double)DownloadedBytes / TotalBytes);
    }
    public List<UpdateManifestFile> BuildMissingList(UpdateManifest manifest, InstalledUpdateState? installed)
    {
        Dictionary<string, string> installedHashes = new(StringComparer.OrdinalIgnoreCase);
        if (installed is not null)
        {
            foreach (UpdateManifestFile file in installed.Files)
            {
                installedHashes[file.Path] = file.Sha256;
            }
        }

        List<UpdateManifestFile> missing = new();
        foreach (UpdateManifestFile file in manifest.Files)
        {
            if (!installedHashes.TryGetValue(file.Path, out string? sha) ||
                !string.Equals(sha, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(file);
            }
        }

        return missing;
    }

    public bool HasEnoughDiskSpace(List<UpdateManifestFile> files)
    {
        long needed = 0;
        foreach (UpdateManifestFile file in files)
        {
            needed += file.Size;
        }

        needed = needed * ProjectRuntimeContracts.Update.RequiredFreeSpaceFactorPercent / 100;
        string? installDirectory = UpdateApplier.GetInstallDirectory();
        string probeDirectory = installDirectory ?? Application.persistentDataPath;
        try
        {
            DriveInfo drive = new(Path.GetPathRoot(Path.GetFullPath(probeDirectory)) ?? probeDirectory);
            return drive.AvailableFreeSpace >= needed;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] не удалось оценить свободное место: {exception.Message}");
            return false;
        }
    }

    public async UniTask<bool> DownloadAsync(
        UpdateManifest manifest,
        List<UpdateManifestFile> files,
        string baseUrl,
        CancellationToken cancellationToken,
        Action<Progress>? progress = null)
    {
        string stagingDirectory = UpdateStateStore.StagingDirectory(manifest.Version);
        Directory.CreateDirectory(stagingDirectory);

        long totalBytes = 0;
        foreach (UpdateManifestFile file in files)
        {
            totalBytes += file.Size;
        }

        long downloadedBytes = 0;
        int filesDone = 0;
        for (int index = 0; index < files.Count; index++)
        {
            UpdateManifestFile file = files[index];
            cancellationToken.ThrowIfCancellationRequested();
            bool downloaded = await DownloadWithRetriesAsync(
                manifest,
                file,
                baseUrl,
                stagingDirectory,
                ProjectRuntimeContracts.Update.DownloadAttempts,
                bytes =>
                {
                    progress?.Invoke(new Progress(
                        downloadedBytes + bytes,
                        totalBytes,
                        filesDone,
                        files.Count));
                },
                cancellationToken);
            if (!downloaded)
            {
                Debug.LogError($"[Update] файл {file.Path} не скачался после ретраев — обновление отменено.");
                return false;
            }

            downloadedBytes += file.Size;
            filesDone++;
            progress?.Invoke(new Progress(downloadedBytes, totalBytes, filesDone, files.Count));
        }

        return true;
    }

    private async UniTask<bool> DownloadWithRetriesAsync(
        UpdateManifest manifest,
        UpdateManifestFile file,
        string baseUrl,
        string stagingDirectory,
        int attempts,
        Action<long>? reportPartialBytes,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            if (attempt > 1)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(attempt), cancellationToken: cancellationToken);
            }

            string targetPath = StagingPathFor(stagingDirectory, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            try
            {
                await DownloadFileAsync(manifest, file, baseUrl, targetPath, reportPartialBytes, cancellationToken);
                string actualHash = UpdateCrypto.Sha256HexOfFile(targetPath);
                long actualSize = new FileInfo(targetPath).Length;
                if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase) ||
                    actualSize != file.Size)
                {
                    throw new InvalidOperationException(
                        $"хэш/размер не сошлись ({actualHash}/{actualSize}, ожидалось {file.Sha256}/{file.Size})");
                }

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                TryDelete(targetPath);
                throw;
            }
            catch (Exception exception)
            {
                TryDelete(targetPath);
                Debug.LogWarning($"[Update] {file.Path}: попытка {attempt}/{attempts} не удалась — {exception.Message}");
            }
        }

        return false;
    }

    private static async UniTask DownloadFileAsync(
        UpdateManifest manifest,
        UpdateManifestFile file,
        string baseUrl,
        string targetPath,
        Action<long>? reportPartialBytes,
        CancellationToken cancellationToken)
    {
        string url = $"{baseUrl}/file/{EscapePath(file.Path)}";
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(ProjectRuntimeContracts.Update.FileTimeoutSeconds));
        // DownloadHandlerFile запечатан и не даёт ни лимита, ни прогресса, поэтому пишем
        // в файл сами через DownloadHandlerScript — тот же приём, что у WebAssetLoader.
        using var downloadHandler = new BoundedFileDownloadHandler(targetPath, file.Size, reportPartialBytes);
        using UnityWebRequest request = new(url, UnityWebRequest.kHttpVerbGET, downloadHandler, null);
        request.timeout = ProjectRuntimeContracts.Update.FileTimeoutSeconds;
        try
        {
            await request.SendWebRequest().WithCancellation(timeoutSource.Token);
            if (request.result != UnityWebRequest.Result.Success || downloadHandler.LimitExceeded)
            {
                throw new InvalidOperationException(
                    downloadHandler.LimitExceeded
                        ? $"размер ответа не сошёлся с подписанным манифестом ({file.Size} байт)"
                        : $"{request.error} ({request.responseCode})");
            }
        }
        finally
        {
            // Закрываем файл до того, как вызывающий удалит недокачанный кусок.
            downloadHandler.Finish();
        }
    }

    /// <summary>Путь к файлу внутри staging; путь приходит из подписанного манифеста, но проверяем и здесь.</summary>
    private static string StagingPathFor(string stagingDirectory, string manifestPath)
    {
        string normalized = manifestPath.Replace('\\', '/');
        if (normalized.Contains("..") || Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException($"небезопасный путь из манифеста: {manifestPath}");
        }

        return Path.Combine(stagingDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string EscapePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return Uri.EscapeDataString(normalized).Replace("%2F", "/");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // недописанный файл в staging уберёт CleanupStaging при следующем запуске
        }
    }

    /// <summary>
    /// Пишет ответ в файл с лимитом размера (ответ обязан совпасть с подписанным
    /// манифестом) и прогрессом по принятым байтам. ReceiveProgress у Unity в плеере
    /// не вызывается, поэтому считаем сами в ReceiveData.
    /// </summary>
    private sealed class BoundedFileDownloadHandler : DownloadHandlerScript
    {
        private readonly long _expectedSize;
        private readonly Action<long>? _reportPartialBytes;
        private FileStream? _stream;
        private long _received;

        public BoundedFileDownloadHandler(string path, long expectedSize, Action<long>? reportPartialBytes)
            : base(new byte[64 * 1024])
        {
            _expectedSize = expectedSize;
            _reportPartialBytes = reportPartialBytes;
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public bool LimitExceeded { get; private set; }

        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            if (contentLength > (ulong)ProjectRuntimeContracts.Update.MaximumUpdateFileBytes ||
                contentLength > (ulong)_expectedSize)
            {
                LimitExceeded = true;
            }
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (LimitExceeded)
            {
                return false;
            }

            if (_received + dataLength > _expectedSize ||
                _received + dataLength > ProjectRuntimeContracts.Update.MaximumUpdateFileBytes)
            {
                LimitExceeded = true;
                CloseStream();
                return false;
            }

            _stream?.Write(data, 0, dataLength);
            _received += dataLength;
            _reportPartialBytes?.Invoke(_received);
            return true;
        }

        protected override void CompleteContent() => CloseStream();

        /// <summary>Идемпотентное закрытие файла; вызывается и после успеха, и при обрыве.</summary>
        public void Finish() => CloseStream();

        private void CloseStream()
        {
            _stream?.Dispose();
            _stream = null;
        }
    }
}
