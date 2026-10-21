#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Kern.Core;

/// <summary>Итог одной проверки целостности файла.</summary>
public enum MenuFileCheckStatus
{
    /// <summary>Проверка пройдена: файл на месте, структура читается.</summary>
    Ok,
    /// <summary>Работать можно, но состояние неидеально: кэш устареет и перепакуется, качество не совпало.</summary>
    Warning,
    /// <summary>Файл отсутствует или повреждён — без ремонта войти в мир не получится.</summary>
    Failed,
}

/// <param name="LabelKey">Ключ локализации названия проверки; порядок ключей задаёт порядок строк в окне.</param>
/// <param name="Status">Вердикт проверки.</param>
/// <param name="Detail">Языконейтральная деталь для скобок: размеры, счётчики, имена файлов, суммы.</param>
public sealed record MenuFileCheckResult(
    string LabelKey,
    MenuFileCheckStatus Status,
    string Detail);

/// <param name="Checks">Результаты всех проверок, в порядке <see cref="IMenuFileDiagnosticsService.CheckLabelKeys"/>.</param>
/// <param name="AllOk">Ни ошибок, ни замечаний.</param>
public sealed record MenuFileDiagnosticsReport(
    IReadOnlyList<MenuFileCheckResult> Checks,
    bool AllOk);

/// <summary>
/// Проверки целостности файлов клиента для окна «Диагностика и целостность
/// файлов» главного меню: FMOD-банки, дисковый кэш офлайн-карты, коллекция
/// шейдерных состояний. Только диагностика: ничего не пишет и не чинит.
/// </summary>
public interface IMenuFileDiagnosticsService
{
    /// <summary>Ключи локализации названий проверок; индекс совпадает с индексом результата в отчёте.</summary>
    IReadOnlyList<string> CheckLabelKeys { get; }

    /// <summary>
    /// Прогоняет все проверки. <paramref name="activeCheck"/> получает индекс
    /// стартующей проверки, <paramref name="fraction"/> — общий прогресс 0..1;
    /// оба вызываются на главном потоке. <paramref name="forceRefresh"/> —
    /// прогнать заново, игнорируя короткий кэш отчёта.
    /// </summary>
    UniTask<MenuFileDiagnosticsReport> RunAllAsync(
        Action<int>? activeCheck,
        Action<float>? fraction,
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
