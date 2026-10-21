#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Kern.Core;

/// <summary>Запись о фактически установленной версии: из чего состоит дифф при следующей проверке.</summary>
public sealed class InstalledUpdateState
{
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("appliedAt")]
    public DateTime AppliedAt { get; set; }

    [JsonProperty("files")]
    public List<UpdateManifestFile> Files { get; set; } = new();
}

/// <summary>
/// Диск-состояние апдейтера под persistentDataPath/Update: state.json (установленная
/// версия со списком хэшей — база диффа), staging/v{version} (загрузка до применения).
/// Бэкап в install-каталоге (.update_backup) подтверждаем при следующем старте: если
/// запустилась сборка с версией из state.json — замена прошла успешно, бэкап можно
/// чистить. Отдельного колбэка от сети для подтверждения не нужно.
/// </summary>
public static class UpdateStateStore
{
    /// <summary>Тестовый шов: подменяет корень состояния, чтобы тесты не трогали persistentDataPath.</summary>
    internal static string? RootOverride;

    public static string RootDirectory => RootOverride ?? Path.Combine(Application.persistentDataPath, "Update");

    public static string StatePath => Path.Combine(RootDirectory, "state.json");

    public static string StagingRoot => Path.Combine(RootDirectory, "staging");

    public static string StagingDirectory(int version) => Path.Combine(StagingRoot, $"v{version}");

    public static InstalledUpdateState? Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<InstalledUpdateState>(File.ReadAllText(StatePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] state.json не читается: {exception.Message}");
            return null;
        }
    }

    public static void Save(InstalledUpdateState state)
    {
        Directory.CreateDirectory(RootDirectory);
        string temp = StatePath + $".tmp-{Guid.NewGuid():N}";
        File.WriteAllText(temp, JsonConvert.SerializeObject(state, Formatting.Indented));
        if (File.Exists(StatePath))
        {
            File.Replace(temp, StatePath, null);
        }
        else
        {
            File.Move(temp, StatePath);
        }
    }

    /// <summary>Чистим staging-папки всех версий, кроме указанной (и указанную — если keepVersion null).</summary>
    public static void CleanupStaging(int? keepVersion = null)
    {
        try
        {
            if (!Directory.Exists(StagingRoot))
            {
                return;
            }

            string keepName = keepVersion is { } version ? $"v{version}" : string.Empty;
            foreach (string directory in Directory.GetDirectories(StagingRoot))
            {
                if (Path.GetFileName(directory) != keepName)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] очистка staging не удалась: {exception.Message}");
        }
    }

    /// <summary>Каталог бэкапа в install-директории; null — нет каталога установки (редактор).</summary>
    public static string? BackupDirectory()
    {
        string? installDirectory = UpdateApplier.GetInstallDirectory();
        return installDirectory is null ? null : Path.Combine(installDirectory, ".update_backup");
    }

    /// <summary>
    /// Сверка состояния при старте, до проверки обновлений:
    /// state совпадает с версией этой сборки — замена прошла, бэкап больше не нужен;
    /// state про другую версию — обновление не взошло (упало до скрипта или скрипт
    /// откатился), state.json инвалидируется, чтобы следующий дифф был полным.
    /// </summary>
    public static void ReconcileOnStartup(int buildVersion)
    {
        InstalledUpdateState? state = Load();
        if (state is null)
        {
            return;
        }

        if (state.Version == buildVersion)
        {
            string? backup = BackupDirectory();
            if (backup is not null && Directory.Exists(backup))
            {
                try
                {
                    Directory.Delete(backup, recursive: true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Update] не удалось удалить бэкап {backup}: {exception.Message}");
                }
            }

            return;
        }

        try
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Update] не удалось инвалидировать state.json: {exception.Message}");
        }
    }

    /// <summary>
    /// Состояние после применения диффа: новые файлы поверх прежних. Слитое описание
    /// соответствует фактическому содержимому install-каталога и служит базой следующего диффа.
    /// </summary>
    public static InstalledUpdateState Merge(int version, List<UpdateManifestFile> appliedFiles)
    {
        Dictionary<string, UpdateManifestFile> byPath = new(StringComparer.OrdinalIgnoreCase);
        InstalledUpdateState? previous = Load();
        if (previous is not null)
        {
            foreach (UpdateManifestFile file in previous.Files)
            {
                byPath[file.Path] = file;
            }
        }

        foreach (UpdateManifestFile file in appliedFiles)
        {
            byPath[file.Path] = file;
        }

        return new InstalledUpdateState
        {
            Version = version,
            AppliedAt = DateTime.UtcNow,
            Files = byPath.Values.ToList(),
        };
    }
}
