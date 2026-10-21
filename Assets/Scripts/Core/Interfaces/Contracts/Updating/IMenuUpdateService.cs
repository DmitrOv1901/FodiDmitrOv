#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kern.Core;

/// <summary>Состояние версии клиента относительно сервера для UI главного меню.</summary>
public enum MenuUpdateKind
{
    /// <summary>Установлена актуальная версия — баннер пишет «обновление не требуется».</summary>
    UpToDate,
    /// <summary>На сервере версия новее — баннер пишет «требуется обновление».</summary>
    UpdateAvailable,
    /// <summary>Проверить не удалось (нет сети / манифест недоступен / подпись не сошлась).</summary>
    Unavailable,
}

/// <summary>Результат проверки версий: что на сервере, что стоит локально.</summary>
public sealed record MenuUpdateStatus(
    MenuUpdateKind Kind,
    int ServerVersion,
    string ServerVersionName,
    int LocalVersion,
    string LocalVersionName,
    string Notes);

/// <summary>
/// Доступ из главного меню к конвейеру обновлений: статус версии (манифест сервера
/// против версии сборки) и запуск обновления с прогрессом. Реализация живёт в
/// Kern.Bootstrap (Assets/Scripts/Core/Bootstrap/Updating) и переиспользует
/// UpdateChecker/UpdateDownloader/UpdateApplier из стартового апдейтера.
/// </summary>
public interface IMenuUpdateService
{
    /// <summary>Проверяет версию; результат кэшируется на короткое время, чтобы переоткрытия модалки не долбили сервер.</summary>
    UniTask<MenuUpdateStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Скачивает и применяет обновление (progress: 0..1). True — применение запущено,
    /// игра сейчас перезапустится; False — обновления нет или применить не удалось
    /// (phase объясняет причину).
    /// </summary>
    UniTask<bool> ApplyUpdateAsync(
        System.Action<string>? phase,
        System.Action<float>? fraction,
        CancellationToken cancellationToken = default);
}
