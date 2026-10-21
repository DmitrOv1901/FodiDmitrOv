#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kern.Core;

/// <summary>
/// Манифест обновления с сервера (GET /updates/mines3/{channel}/manifest).
/// Байты ответа подписаны сервером RSA-2048/SHA-256 (заголовок X-Update-Signature),
/// поэтому парсим строго после проверки подписи; поля — зеркало серверного
/// ClientUpdatePublisher.UpdateManifest (camelCase).
/// </summary>
public sealed class UpdateManifest
{
    [JsonProperty("schema")]
    public int Schema { get; set; }

    [JsonProperty("channel")]
    public string Channel { get; set; } = string.Empty;

    /// <summary>Версия сборки; тот же счётчик, что ClientHello.ClientVersion на сервере.</summary>
    [JsonProperty("version")]
    public int Version { get; set; }

    [JsonProperty("versionName")]
    public string VersionName { get; set; } = string.Empty;

    [JsonProperty("releaseDate")]
    public DateTime ReleaseDate { get; set; }

    [JsonProperty("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonProperty("allowDowngrade")]
    public bool AllowDowngrade { get; set; }

    [JsonProperty("files")]
    public List<UpdateManifestFile> Files { get; set; } = new();

    [JsonProperty("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonProperty("totalSize")]
    public long TotalSize { get; set; }

    public static UpdateManifest? Parse(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<UpdateManifest>(System.Text.Encoding.UTF8.GetString(bytes));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Один файл сборки в манифесте: относительный путь, SHA-256 (hex) и размер.</summary>
public sealed class UpdateManifestFile
{
    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonProperty("size")]
    public long Size { get; set; }
}
