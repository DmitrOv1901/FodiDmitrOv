#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Kern.Core;

/// <summary>
/// Оркестратор авто-обновления, вызывается из ApplicationBootstrap сразу после
/// инициализации конфига и до любой тяжёлой инициализации: если на сервере есть
/// сборка новее — скачиваем её и перезапускаемся, не тратя время на прогрузку.
/// Любая ошибка — просто лог: старт продолжается, а устаревшего при коннекте
/// отсечёт серверный гейт по версии (OutdatedClientPacket).
/// </summary>
public sealed class UpdateManager
{
    public async UniTask RunIfNeededAsync(
        string serverHost,
        int apiPort,
        bool useDummyConnection,
        BootstrapLoadingScreen loadingScreen,
        CancellationToken cancellationToken)
    {
        try
        {
            if (Application.isEditor)
            {
                Debug.Log("[Update] в редакторе апдейтер отключён.");
                return;
            }

            if (useDummyConnection)
            {
                Debug.Log("[Update] оффлайн-режим (dummy connection) — проверка обновлений пропущена.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ProjectRuntimeContracts.Update.PublicKeyPem))
            {
                Debug.Log("[Update] ключ подписи не задан — апдейтер выключен.");
                return;
            }

            loadingScreen.ShowDirect("Проверка обновлений…");
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            // Гигиена: подтверждаем/инвалидируем состояние прошлой замены и чистим чужой staging.
            UpdateStateStore.ReconcileOnStartup(ProjectRuntimeContracts.Networking.ClientVersion);
            UpdateStateStore.CleanupStaging();

            string baseUrl = UpdateEndpoints.BaseUrl(new ConnectionSettings
            {
                ServerHost = serverHost,
                ApiPort = apiPort,
            });
            UpdateManifest? manifest = await UpdateChecker.FetchManifestAsync(baseUrl + "/manifest", cancellationToken);
            if (manifest is null)
            {
                return;
            }

            InstalledUpdateState? installed = UpdateStateStore.Load();
            UpdateChecker.Decision decision = UpdateChecker.Decide(manifest, installed);
            if (decision.Kind != UpdateChecker.DecisionKind.UpdateAvailable)
            {
                Debug.Log($"[Update] обновление не требуется: {decision.Reason}.");
                return;
            }

            Debug.Log($"[Update] доступна сборка v{manifest.Version} ({manifest.VersionName}), начинаем загрузку.");
            var downloader = new UpdateDownloader();
            var files = downloader.BuildMissingList(manifest, installed);
            if (files.Count == 0)
            {
                // После ReconcileOnStartup состояние про чужую версию уже инвалидировано,
                // так что сюда попасть практически нельзя — но молча продолжать не будем.
                Debug.LogWarning("[Update] дифф пуст при новой версии — неожиданное состояние, пропуск.");
                return;
            }

            if (!downloader.HasEnoughDiskSpace(files))
            {
                Debug.LogWarning("[Update] недостаточно места на диске для загрузки обновления — пропускаем.");
                loadingScreen.SetPhaseText("Недостаточно места на диске для обновления");
                return;
            }

            string phase = $"Загрузка обновления v{manifest.Version}";
            bool downloaded = await downloader.DownloadAsync(
                manifest,
                files,
                baseUrl,
                cancellationToken,
                progress => loadingScreen.SetPhaseText(
                    $"{phase} — {Mathf.RoundToInt(progress.Fraction * 100)}% ({progress.FilesDone}/{progress.FilesTotal})"));
            if (!downloaded)
            {
                UpdateStateStore.CleanupStaging(manifest.Version);
                return;
            }

            string? applyScript = UpdateApplier.PrepareApply(manifest, files);
            if (applyScript is null)
            {
                return;
            }

            // Пишем после подготовки скрипта и до перезапуска: после старта новой сборки
            // ReconcileOnStartup подтвердит это состояние и почистит бэкап. Если скрипт
            // откатится, при следующем старте состояние будет про чужую версию и
            // инвалидируется — дифф пересоберётся полностью.
            UpdateStateStore.Save(UpdateStateStore.Merge(manifest.Version, files));
            Debug.Log($"[Update] обновление v{manifest.Version} готово, перезапускаемся.");
            loadingScreen.SetPhaseText($"Обновление v{manifest.Version} готово, перезапуск…");
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            UpdateApplier.LaunchAndQuit(applyScript);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Приложение закрывается — это не ошибка обновления.
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] проверка/загрузка обновления не удалась: {exception}");
        }
    }
}
