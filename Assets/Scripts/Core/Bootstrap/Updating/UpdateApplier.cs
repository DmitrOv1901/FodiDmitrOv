#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Kern.Core;

/// <summary>
/// Применение скачанного обновления. В Unity из-под работающего плеера файлы в
/// install-каталоге заняты, поэтому подмена делает внешний скрипт apply.ps1: он
/// дожидается выхода игры, уносит затрагиваемые файлы в .update_backup, переносит
/// файлы из staging (тот же том — rename) и перезапускает игру; при любой ошибке
/// откатывает бэкап. Скрипт генерируется под конкретный дифф и запускается detached,
/// после чего клиент завершается.
/// </summary>
public static class UpdateApplier
{
    /// <summary>Каталог установки (папка с Kern.exe); в редакторе это корень проекта — апдейтер там не работает.</summary>
    public static string? GetInstallDirectory()
    {
        if (Application.isEditor)
        {
            return null;
        }

        return Path.GetDirectoryName(Application.dataPath);
    }

    public static bool IsInstallDirectoryWritable()
    {
        string? installDirectory = GetInstallDirectory();
        if (installDirectory is null || !Directory.Exists(installDirectory))
        {
            return false;
        }

        try
        {
            string probe = Path.Combine(installDirectory, $".update-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Готовит apply.ps1 рядом со staging и возвращает команду запуска; null — установка
    /// недоступна для записи (папка read-only) или платформа не Windows.
    /// </summary>
    public static string? PrepareApply(UpdateManifest manifest, List<UpdateManifestFile> files)
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Debug.LogWarning("[Update] авто-применение поддержано только на Windows.");
            return null;
        }

        string? installDirectory = GetInstallDirectory();
        if (installDirectory is null || !IsInstallDirectoryWritable())
        {
            Debug.LogWarning("[Update] каталог установки недоступен для записи — авто-обновление невозможно.");
            return null;
        }

        string stagingDirectory = UpdateStateStore.StagingDirectory(manifest.Version);
        string scriptPath = Path.Combine(stagingDirectory, "apply.ps1");
        File.WriteAllText(scriptPath, BuildScript(manifest.Version, installDirectory, stagingDirectory, files),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // BOM: PowerShell читает UTF-8 только с ним
        return scriptPath;
    }

    /// <summary>Запускает apply.ps1 detached и завершает приложение.</summary>
    public static void LaunchAndQuit(string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(startInfo);
        Application.Quit();
    }

    internal static string BuildScript(
        int version,
        string installDirectory,
        string stagingDirectory,
        List<UpdateManifestFile> files)
    {
        string backupDirectory = Path.Combine(installDirectory, ".update_backup");
        string gameName = ProjectRuntimeContracts.Update.GameExecutableName;
        string processName = Path.GetFileNameWithoutExtension(gameName);

        var script = new StringBuilder();
        script.AppendLine("# Сгенерировано авто-апдейтером Kern; ручное изменение бессмысленно — файл пересоздаётся.");
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine($"$install = '{Quote(installDirectory)}'");
        script.AppendLine($"$staging = '{Quote(stagingDirectory)}'");
        script.AppendLine($"$backup = '{Quote(backupDirectory)}'");
        script.AppendLine($"$game = '{Quote(gameName)}'");
        script.AppendLine($"$procName = '{Quote(processName)}'");
        script.AppendLine("$files = @(");
        foreach (UpdateManifestFile file in files)
        {
            script.AppendLine($"  '{Quote(file.Path)}',");
        }

        // PowerShell не любит висячую запятую — добавляем фиктивный элемент.
        script.AppendLine("  '') | Where-Object { $_ -ne '' }");
        script.AppendLine("try {");
        script.AppendLine("  while (Get-Process -Name $procName -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 800 }");
        script.AppendLine("  New-Item -ItemType Directory -Force -Path $backup | Out-Null");
        script.AppendLine("  foreach ($f in $files) {");
        script.AppendLine("    $dst = Join-Path $install $f");
        script.AppendLine("    $bak = Join-Path $backup $f");
        script.AppendLine("    $bakDir = Split-Path $bak -Parent");
        script.AppendLine("    if (-not (Test-Path $bakDir)) { New-Item -ItemType Directory -Force -Path $bakDir | Out-Null }");
        script.AppendLine("    if (Test-Path $dst) { Move-Item -Force $dst $bak }");
        script.AppendLine("    $src = Join-Path $staging $f");
        script.AppendLine("    $dstDir = Split-Path $dst -Parent");
        script.AppendLine("    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }");
        script.AppendLine("    Move-Item -Force $src $dst");
        script.AppendLine("  }");
        script.AppendLine($"  Set-Content -Path (Join-Path $backup 'applied-version.txt') -Value '{version}'");
        script.AppendLine("  Start-Process -FilePath (Join-Path $install $game) -WorkingDirectory $install");
        script.AppendLine("  exit 0");
        script.AppendLine("} catch {");
        script.AppendLine("  foreach ($f in $files) {");
        script.AppendLine("    $dst = Join-Path $install $f");
        script.AppendLine("    $bak = Join-Path $backup $f");
        script.AppendLine("    if (Test-Path $bak) { Move-Item -Force $bak $dst } elseif (Test-Path $dst) { Remove-Item -Force $dst }");
        script.AppendLine("  }");
        script.AppendLine("  Start-Process -FilePath (Join-Path $install $game) -WorkingDirectory $install");
        script.AppendLine("  exit 1");
        script.AppendLine("}");
        return script.ToString();
    }

    /// <summary>Экранирование одиночных кавычек для литерала PowerShell в одинарных кавычках.</summary>
    private static string Quote(string value) => value.Replace("'", "''");
}
