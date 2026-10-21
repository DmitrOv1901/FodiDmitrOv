#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kern.Core.Interfaces;
using UnityEngine;
using VContainer;

namespace Kern.Core;

/// <summary>
/// Реализация IMenuUpdateService для главного меню: статус по манифесту сервера
/// (с коротким кэшем) и запуск обновления с прогрессом. Переиспользует конвейер
/// стартового апдейтера (UpdateChecker/UpdateDownloader/UpdateApplier).
/// </summary>
public sealed class MenuUpdateService : IMenuUpdateService
{
    private static readonly TimeSpan StatusCacheTtl = TimeSpan.FromSeconds(30);

    private readonly IClientConfigManager _clientConfig;
    private readonly UpdateDownloader _downloader = new();
    private MenuUpdateStatus? _cachedStatus;
    private DateTime _cachedStatusAt;

    [Inject]
    public MenuUpdateService(IClientConfigManager clientConfig)
    {
        _clientConfig = clientConfig;
    }

    public async UniTask<MenuUpdateStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        int localVersion = ProjectRuntimeContracts.Networking.ClientVersion;
        string localName = LocalVersionName(localVersion);

        ConnectionSettings? connection = _clientConfig?.Config?.Connection;
        if (connection is null || connection.UseDummyConnection)
        {
            // Оффлайн-заглушка: проверки нет, считаем себя актуальными.
            return new MenuUpdateStatus(MenuUpdateKind.UpToDate, localVersion, localName, localVersion, localName, string.Empty);
        }

        if (_cachedStatus is { } cached && DateTime.UtcNow - _cachedStatusAt < StatusCacheTtl)
        {
            return cached;
        }

        MenuUpdateStatus status;
        UpdateManifest? manifest = null;
        try
        {
            manifest = await UpdateChecker.FetchManifestAsync(UpdateEndpoints.ManifestUrl(connection), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] проверка версии в меню не удалась: {exception.Message}");
        }

        if (manifest is null)
        {
            status = new MenuUpdateStatus(MenuUpdateKind.Unavailable, localVersion, localName, localVersion, localName, string.Empty);
        }
        else
        {
            var decision = UpdateChecker.Decide(manifest, UpdateStateStore.Load());
            status = new MenuUpdateStatus(
                decision.Kind == UpdateChecker.DecisionKind.UpdateAvailable
                    ? MenuUpdateKind.UpdateAvailable
                    : MenuUpdateKind.UpToDate,
                manifest.Version,
                manifest.VersionName,
                localVersion,
                localName,
                manifest.Notes);
        }

        _cachedStatus = status;
        _cachedStatusAt = DateTime.UtcNow;
        return status;
    }

    public async UniTask<bool> ApplyUpdateAsync(
        Action<string>? phase,
        Action<float>? fraction,
        CancellationToken cancellationToken = default)
    {
        ConnectionSettings? connection = _clientConfig?.Config?.Connection;
        if (connection is null || connection.UseDummyConnection)
        {
            phase?.Invoke("оффлайн-режим");
            return false;
        }

        UpdateManifest? manifest = await UpdateChecker.FetchManifestAsync(
            UpdateEndpoints.ManifestUrl(connection), cancellationToken);
        if (manifest is null)
        {
            phase?.Invoke("манифест недоступен");
            return false;
        }

        if (UpdateChecker.Decide(manifest, UpdateStateStore.Load()).Kind != UpdateChecker.DecisionKind.UpdateAvailable)
        {
            phase?.Invoke("обновление не требуется");
            return false;
        }

        if (Application.isEditor)
        {
            // Применение подменяет файлы установки — в редакторе его нет.
            phase?.Invoke($"в редакторе авто-обновление отключено (доступна v{manifest.VersionName})");
            return false;
        }

        UpdateStateStore.ReconcileOnStartup(ProjectRuntimeContracts.Networking.ClientVersion);
        var files = _downloader.BuildMissingList(manifest, UpdateStateStore.Load());
        if (files.Count == 0)
        {
            // Версия объявлена (declare), но файлы сборки не опубликованы.
            phase?.Invoke("обновление ещё не опубликовано для скачивания");
            return false;
        }

        if (!_downloader.HasEnoughDiskSpace(files))
        {
            phase?.Invoke("недостаточно места на диске");
            return false;
        }

        phase?.Invoke($"загрузка v{manifest.VersionName}");
        bool downloaded = await _downloader.DownloadAsync(
            manifest,
            files,
            UpdateEndpoints.BaseUrl(connection),
            cancellationToken,
            progress => fraction?.Invoke(progress.Fraction));
        if (!downloaded)
        {
            phase?.Invoke("загрузка не удалась");
            return false;
        }

        string? applyScript = UpdateApplier.PrepareApply(manifest, files);
        if (applyScript is null)
        {
            phase?.Invoke("каталог установки недоступен для записи");
            return false;
        }

        UpdateStateStore.Save(UpdateStateStore.Merge(manifest.Version, files));
        phase?.Invoke("перезапуск");
        UpdateApplier.LaunchAndQuit(applyScript);
        return true;
    }

    /// <summary>Человекочистое имя локальной версии по схеме сервера: 1 → «0.0.1».</summary>
    internal static string LocalVersionName(int version) => $"0.0.{version}";
}

/// <summary>Базовый URL эндпоинтов обновлений — общий для стартового и менюшного апдейтера.</summary>
internal static class UpdateEndpoints
{
    public static string BaseUrl(ConnectionSettings connection) =>
        $"http://{connection.ServerHost}:{connection.ApiPort}/updates/mines3/{ProjectRuntimeContracts.Update.Channel}";

    public static string ManifestUrl(ConnectionSettings connection) => BaseUrl(connection) + "/manifest";
}
