# Публикация сборки клиента в апдейтер сервера.
#
# Зипует билд плеера и POST-ит его на админ-endpoint сервера
# (resources/mines-server/updates/publish, LocalOnly — запускать с той же машины,
# что и сервер). Сервер сам посчитает SHA-256, соберёт и подпишет манифест.
#
# Примеры:
#   powershell -File scripts/publish-update.ps1 -Version 2 -VersionName 0.04
#   powershell -File scripts/publish-update.ps1 -Version 3 -Enforce   # жёсткая раскатка
param(
    # Версия сборки: обязана совпадать с ProjectRuntimeContracts.Networking.ClientVersion нового билда.
    [Parameter(Mandatory = $true)][int]$Version,
    # Человекочитаемое имя (bundleVersion); по умолчанию v{Version}.
    [string]$VersionName = "",
    [string]$Notes = "",
    # Корень билда плеера (папка с Kern.exe); внутри архива допустима одна папка-обёртка — сервер снимет.
    [string]$BuildDir = "Build/Windows",
    # Базовый URL HTTP-апи сервера.
    [string]$ServerUrl = "http://127.0.0.1:5460",
    #Enforce: поднять MinimumClientVersion до Version (старые клиенты получат OutdatedClientPacket).
    [switch]$Enforce,
    [switch]$AllowDowngrade
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$buildPath = Join-Path $repoRoot $BuildDir
if (-not (Test-Path (Join-Path $buildPath "Kern.exe"))) {
    throw "В '$buildPath' нет Kern.exe — соберите плеер (Kern/Build/Windows 64) или укажите -BuildDir."
}

$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "kern-update-v$Version.zip"
if (Test-Path $tempZip) { Remove-Item $tempZip }
Compress-Archive -Path (Join-Path $buildPath "*") -DestinationPath $tempZip
Write-Host "Архив: $tempZip ($([math]::Round((Get-Item $tempZip).Length / 1MB, 2)) МБ)"

# multipart/form-data через .NET, чтобы не зависеть от curl в PATH.
Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$form = [System.Net.Http.MultipartFormDataContent]::new()
$stream = [System.IO.File]::OpenRead($tempZip)
$fileContent = [System.Net.Http.StreamContent]::new($stream)
$fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/zip")
$form.Add($fileContent, "file", "kern-v$Version.zip")
$form.Add([System.Net.Http.StringContent]::new($Version), "version")
$form.Add([System.Net.Http.StringContent]::new($VersionName), "versionName")
$form.Add([System.Net.Http.StringContent]::new($Notes), "notes")
$form.Add([System.Net.Http.StringContent]::new([string][bool]$Enforce), "enforce")
$form.Add([System.Net.Http.StringContent]::new([string][bool]$AllowDowngrade), "allowDowngrade")

try {
    $response = $client.PostAsync("$ServerUrl/resources/mines-server/updates/publish", $form).Result
    $body = $response.Content.ReadAsStringAsync().Result
    if (-not $response.IsSuccessStatusCode) {
        throw "Сервер отклонил публикацию ($([int]$response.StatusCode)): $body"
    }
    Write-Host "Опубликовано: $body"
}
finally {
    $stream.Dispose()
    $client.Dispose()
    Remove-Item $tempZip -ErrorAction SilentlyContinue
}

# Быстрая сверка: манифест теперь указывает на нашу версию.
$manifest = Invoke-RestMethod "$ServerUrl/updates/mines3/stable/manifest"
if ($manifest.version -ne $Version) {
    throw "Манифест отдаёт версию $($manifest.version), ожидалась $Version."
}
Write-Host "Манифест в порядке: v$($manifest.version) '$($manifest.versionName)', файлов: $($manifest.totalFiles)."
