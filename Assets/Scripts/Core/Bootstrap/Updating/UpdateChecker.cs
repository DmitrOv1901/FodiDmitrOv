#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Kern.Core;

/// <summary>
/// Проверка обновлений: GET манифеста, проверка подписи/схемы/канала и решение
/// «нужен ли апдейт». Любой сбой сети или подписи — null (апдейтер просто не
/// мешает старту; устаревшего всё равно отсечёт гейт на ClientHello).
/// </summary>
public static class UpdateChecker
{
    public enum DecisionKind
    {
        /// <summary>Версии совпадают — работать как обычно.</summary>
        UpToDate,
        /// <summary>На сервере сборка новее — нужно обновиться.</summary>
        UpdateAvailable,
        /// <summary>Манифест не подходит (чужой канал, downgrade, битый) — игнор.</summary>
        Ignore,
    }

    public readonly record struct Decision(DecisionKind Kind, string Reason);

    public static async UniTask<UpdateManifest?> FetchManifestAsync(
        string manifestUrl,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        string? signature;
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(ProjectRuntimeContracts.Update.ManifestTimeoutSeconds));
            using UnityWebRequest request = UnityWebRequest.Get(manifestUrl);
            request.timeout = ProjectRuntimeContracts.Update.ManifestTimeoutSeconds;
            await request.SendWebRequest().WithCancellation(timeoutSource.Token);
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Update] манифест недоступен: {request.error} ({request.responseCode})");
                return null;
            }

            bytes = request.downloadHandler.data;
            signature = request.GetResponseHeader("X-Update-Signature");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // закрытие приложения — наверх
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] запрос манифеста не удался: {exception.Message}");
            return null;
        }

        if (bytes is not { Length: > 0 } || bytes.Length > ProjectRuntimeContracts.Update.MaximumManifestBytes)
        {
            Debug.LogWarning("[Update] манифест пуст или превышает лимит размера.");
            return null;
        }

        if (!UpdateCrypto.VerifyManifestSignature(
                bytes,
                signature,
                ProjectRuntimeContracts.Update.PublicKeyPem))
        {
            Debug.LogWarning("[Update] подпись манифеста не сошлась — игнорируем.");
            return null;
        }

        UpdateManifest? manifest = UpdateManifest.Parse(bytes);
        if (manifest is null)
        {
            Debug.LogWarning("[Update] манифест не разобрался.");
            return null;
        }

        if (manifest.Schema != 1 ||
            !string.Equals(manifest.Channel, ProjectRuntimeContracts.Update.Channel, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[Update] манифест чужой: schema={manifest.Schema} channel={manifest.Channel}.");
            return null;
        }

        return manifest;
    }

    /// <summary>Решение по манифесту против версии этой сборки и последней применённой.</summary>
    public static Decision Decide(UpdateManifest manifest, InstalledUpdateState? installed)
    {
        int buildVersion = ProjectRuntimeContracts.Networking.ClientVersion;
        if (manifest.Version > buildVersion)
        {
            return new Decision(DecisionKind.UpdateAvailable, $"сервер: v{manifest.Version}, сборка: v{buildVersion}");
        }

        if (manifest.Version < buildVersion)
        {
            return new Decision(DecisionKind.Ignore,
                $"сервер старше сборки (v{manifest.Version} < v{buildVersion}) — downgrade не выполняем");
        }

        if (installed is not null && installed.Version > buildVersion && !manifest.AllowDowngrade)
        {
            // Теоретически невозможно (установленное новее этой сборки), но подписанный
            // манифест без allowDowngrade права откатиться не даёт.
            return new Decision(DecisionKind.Ignore, "downgrade без allowDowngrade запрещён");
        }

        return new Decision(DecisionKind.UpToDate, $"v{manifest.Version}");
    }
}
