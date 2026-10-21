#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.GUI.Components;
using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI.Builders;
public class TextPacketBuilder : PacketUIBuilderBase<TextPacket>
{
    public const string ExitButtonLabel = "ВЫЙТИ";

    /// <summary>Метка карточки инвентаря легаси-окна (мост ProtocolWindow).</summary>
    public const string InventoryCardKey = "PacketUI.InventoryCard";

    /// <summary>Метка рамки выбранного слота UP (мост ProtocolWindow).</summary>
    public const string SlotSelectionKey = "PacketUI.SlotSelection";

    /// <summary>
    /// Иконка-лид текста описания (мост ставит на тексте страницы UP):
    /// текст строится карточкой [иконка умения | текст] — <see cref="LeadIconKey"/>.
    /// </summary>
    public const string LeadIconKey = "PacketUI.LeadIcon";

    /// <summary>
    /// Плашка уровня над слотом (мост ставит на цифре в UP-канвасе): значение —
    /// цвет заливки #RRGGBB, сам цвет кольца навыка из старого клиента.
    /// </summary>
    public const string LevelPillKey = "PacketUI.LevelPill";

    // Неверное значение цвета (опечатка автора страницы: <color=while> вместо
    // white) TextCore отрисовывает буквами вместе с тегом. Некорректные
    // значения заменяются белым — дефолт по репорту.
    private static readonly Regex s_invalidColorTag = new(
        @"<color\s*=\s*['""]?([^'""\]>]+?)['""]?\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex s_hexColor = new("^#[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);

    // Полный набор имённых цветов разметки (HTML-палитра, как в legacy-клиенте
    // и TextCore). Отдельно от словаря статусов карточек: там только те имена,
    // которые нужны для разбора одноцветных строк.
    private static readonly HashSet<string> s_markupColorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "aqua", "black", "blue", "brown", "cyan", "darkblue", "fuchsia", "green",
        "gray", "grey", "lightblue", "lime", "magenta", "maroon", "navy", "olive",
        "orange", "purple", "red", "silver", "teal", "white", "yellow",
    };

    /// <summary>
    /// Чинит цветные теги перед отдачей в TextCore: значение, которое не
    /// разбирается (не имя и не #hex), заменяется на white — тег остаётся
    /// валидным и не сыплется в текст буквами.
    /// </summary>
    public static string SanitizeRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? "";
        }

        return s_invalidColorTag.Replace(text, match =>
        {
            string value = match.Groups[1].Value.Trim();
            bool valid = value.StartsWith('#')
                ? s_hexColor.IsMatch(value)
                : s_markupColorNames.Contains(value);
            return valid ? match.Value : "<color=white>";
        });
    }

    // Статус карточки приходит с legacy-разметкой цен (<color=lime>…</color>):
    // из тегов читается только цвет, остальное (size/b/i) вырезается.
    private static readonly Regex s_legacyColor = new(
        @"^<color=(?<value>[^>]+)>(?<text>.*)</color>$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex s_legacyNoise = new(
        @"</?(size|b|i)[^>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex s_legacyAnyTag = new(
        @"</?[A-Za-z][^>]*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Именованные цвета legacy-разметки; шестнадцатеричные (#FF3333) парсит
    // ColorUtility. Порог «Readability» не трогаем: цвета задаёт сервер.
    private static readonly Dictionary<string, Color> s_namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lime"] = new Color(0f, 1f, 0f),
        ["yellow"] = Color.yellow,
        ["white"] = Color.white,
        ["red"] = Color.red,
        ["green"] = Color.green,
        ["cyan"] = Color.cyan,
        ["blue"] = Color.blue,
        ["magenta"] = Color.magenta,
        ["gray"] = Color.gray,
        ["grey"] = Color.gray,
        ["black"] = Color.black,
    };

    /// <summary>
    /// Строка статуса карточки → цвет (null — цвет по умолчанию) и чистый
    /// текст. Формат моста — один цвет-обёртка на строку; при любой другой
    /// разметке остаётся текст без тегов.
    /// </summary>
    internal static (Color? Color, string Text) ParseLegacyColoredLine(string line)
    {
        string cleaned = s_legacyNoise.Replace(line, string.Empty).Trim();
        Match match = s_legacyColor.Match(cleaned);
        if (match.Success && TryLegacyColor(match.Groups["value"].Value.Trim(), out Color color))
        {
            return (color, match.Groups["text"].Value);
        }

        return (null, s_legacyAnyTag.Replace(cleaned, string.Empty));
    }

    private static bool TryLegacyColor(string value, out Color color)
    {
        if (value.StartsWith('#'))
        {
            return ColorUtility.TryParseHtmlString(value, out color);
        }

        return s_namedColors.TryGetValue(value, out color);
    }

    /// <summary>
    /// Кнопка «ВЫЙТИ» легаси-окна: её серверное действие — close() из
    /// Session.SendWin, то есть именно закрытие окна (не выход из аккаунта).
    /// Крестик в углу рамки и ESC шлёт клик по ней же — тот же ElementClickPacket.
    /// </summary>
    public static bool IsExitButton(IGUIComponentPacket packet) =>
        packet is TextPacket text &&
        !string.IsNullOrEmpty(text.OnClickContext) &&
        string.Equals(text.Text.Trim(), ExitButtonLabel, StringComparison.Ordinal);

    protected override VisualElement BuildTyped(TextPacket packet, PacketUIBuilder builder)
    {
        string? imageUri = AttachedProperties.Find(packet, "PacketUI.ImageURI");
        if (imageUri != null)
        {
            return ImagePacketBuilder.BuildUriImage(imageUri, builder);
        }

        if (AttachedProperties.Has(packet, InventoryCardKey))
        {
            return BuildInventoryCard(packet, builder);
        }

        // Рамка выбранного слота UP (мост ProtocolWindow): пустой бортик вокруг
        // ячейки. PickingMode Ignore — клики проходят к слоту под рамкой, арт
        // иконки рамка не перекрывает.
        if (AttachedProperties.Has(packet, SlotSelectionKey))
        {
            var selection = new VisualElement();
            selection.AddToClassList("packet-slot-selection");
            selection.pickingMode = PickingMode.Ignore;
            return selection;
        }

        if (!string.IsNullOrEmpty(packet.OnClickContext))
        {
            var button = new Button { text = SanitizeRichText(packet.Text) };
            button.AddToClassList("packet-window-action");
            if (AttachedProperties.Has(packet, "PacketUI.TabAction"))
            {
                button.AddToClassList("packet-window-tab");
            }

            if (AttachedProperties.Has(packet, "PacketUI.FooterAction"))
            {
                button.AddToClassList("packet-window-footer-action");
            }

            // Кнопка канваса из моста: размер и позицию задаёт сервер
            // (Canvas.X/Y/Width/Height), поэтому фикс-минимум .packet-window-action
            // сброшен; MicroButton дополнительно мельче шрифтом.
            if (AttachedProperties.Has(packet, "PacketUI.CanvasButton"))
            {
                button.AddToClassList("packet-window-action--canvas");
                if (AttachedProperties.Has(packet, "Canvas.Micro"))
                {
                    button.AddToClassList("packet-window-action--micro");
                }
            }

            // Кнопка «ВЫЙТИ» — единственный закрывающий элемент легаси-окна
            // (её серверное действие гасит окно; ESC шлёт клик именно по ней).
            if (string.Equals(packet.Text.Trim(), ExitButtonLabel, StringComparison.Ordinal))
            {
                button.AddToClassList("packet-window-exit");
            }

            return button;
        }

        // Описание с иконкой-лидом (мост: PacketUI.LeadIcon на тексте страницы
        // UP) — карточкой [иконка | текст], а не центральной подписью.
        string? leadIconUri = AttachedProperties.Find(packet, LeadIconKey);
        if (leadIconUri != null)
        {
            return BuildLeadIconCard(leadIconUri, packet.Text, builder);
        }

        var label = new Label(SanitizeRichText(packet.Text));
        label.AddToClassList(AttachedProperties.Has(packet, "PacketUI.Title")
            ? "sci-fi-text-title"
            : "sci-fi-text-body");
        // Активная вкладка легаси-окна: текст без действия, вид задаёт
        // .packet-window-tab-active (как у таб-кнопок, но без клика).
        if (AttachedProperties.Has(packet, "PacketUI.TabActive"))
        {
            label.AddToClassList("packet-window-tab-active");
        }

        label.AddToClassList("fit-wrap");
        // Выравнивание в приёме legacy-клиента: заголовок и текст по центру;
        // текст с префиксом "@@" (две собачки) прижат влево — маркер автора
        // окна. Серверный маркер "Text.Align"="Center" остаётся совместим.
        string text = packet.Text;
        bool leftAligned = text.StartsWith("@@", StringComparison.Ordinal);
        if (leftAligned)
        {
            label.text = SanitizeRichText(text[2..]);
        }

        if (leftAligned)
        {
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
        }
        else
        {
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        // Плашка уровня над слотом (мост ставит на цифре уровня в UP-канвасе):
        // цвет заливки = цвет навыка из старого клиента, цифры тёмные.
        if (AttachedProperties.Find(packet, LevelPillKey) is { } pillColor &&
            ColorUtility.TryParseHtmlString(pillColor, out Color pill))
        {
            label.AddToClassList("packet-level-pill");
            label.style.backgroundColor = new StyleColor(pill);
            // Плашка лежит поверх иконки слота — клики должна пропускать к нему.
            label.pickingMode = PickingMode.Ignore;
        }

        return label;
    }

    /// <summary>
    /// Карточка «иконка умения | описание» (мост ставит PacketUI.LeadIcon на
    /// тексте страницы UP): иконка слева в нативном размере арта (75x75),
    /// текст занимает всё остальное пространство и прижат влево — приём
    /// старого клиента, где upText шёл рядом с upImage.
    /// </summary>
    private static VisualElement BuildLeadIconCard(string iconUri, string text, PacketUIBuilder builder)
    {
        var card = new VisualElement();
        card.AddToClassList("packet-window-lead-card");

        var icon = ImagePacketBuilder.BuildUriImage(iconUri, builder);
        icon.AddToClassList("packet-window-lead-card__icon");
        icon.pickingMode = PickingMode.Ignore;
        card.Add(icon);

        string body = SanitizeRichText(text);
        if (body.StartsWith("@@", StringComparison.Ordinal))
        {
            body = body[2..];
        }

        var label = new Label(body);
        label.AddToClassList("fit-wrap");
        label.AddToClassList("packet-window-lead-card__text");
        label.pickingMode = PickingMode.Ignore;
        card.Add(label);
        return card;
    }

    /// <summary>
    /// Карточка инвентаря легаси-окна — перенос InventoryItemPrefab старого
    /// клиента: спрайт предмета по id из PacketUI.ItemId и мелкий статус под
    /// ним (цены ордеров, количество). Кликабельная карточка — Button, пустой
    /// слот и страница без обработчика — статичный бокс той же геометрии.
    /// </summary>
    private static VisualElement BuildInventoryCard(TextPacket packet, PacketUIBuilder builder)
    {
        bool clickable = !string.IsNullOrEmpty(packet.OnClickContext);
        var button = clickable ? new Button() : null;
        var card = (VisualElement?)button ?? new VisualElement();
        card.AddToClassList("packet-inventory-card");
        // Точка отсчёта для абсолютного бейджа количества в углу клетки.
        card.style.position = Position.Relative;

        // У Button всегда есть собственный пустой TextElement — первым ребёнком.
        // Пустая строка всё равно резервирует высоту шрифта, а карточку должны
        // раскладывать только иконка и статус, поэтому внутренний текст гасится.
        if (button != null &&
            button.childCount > 0 &&
            button[0] is TextElement)
        {
            button[0].style.display = DisplayStyle.None;
        }

        // Значение метки — высота ячейки из css старого клиента (inv-ch).
        if (AttachedProperties.TryGetInt(packet, InventoryCardKey, out int cellHeight) &&
            cellHeight >= 24)
        {
            card.style.minHeight = cellHeight;
        }

        var icon = new VisualElement();
        icon.AddToClassList("packet-inventory-card__icon");
        icon.pickingMode = PickingMode.Ignore;
        // Правило старого клиента: id ≥ 2000 — навык, id > 200 — клан; у тех
        // свои спрайты, локального каталога для них нет. Предметы — 0..200.
        // ItemType : byte, поэтому IsDefined получает боксированный byte:
        // запакованный int Mono отвергает ArgumentException'ом, а строковый
        // вариант ищет по имени значения, а не по числу.
        if (AttachedProperties.TryGetInt(packet, "PacketUI.ItemId", out int itemId) &&
            itemId >= 0 &&
            itemId < 200 &&
            Enum.IsDefined(typeof(ItemType), (byte)itemId))
        {
            Texture2D? texture = builder.ItemCatalog?.GetIcon((ItemType)itemId);
            if (texture != null)
            {
                icon.style.backgroundImage = new StyleBackground(texture);
            }
        }

        if (AttachedProperties.Has(packet, "PacketUI.ItemFaint"))
        {
            icon.AddToClassList("packet-inventory-card__icon--faint");
        }

        card.Add(icon);

        if (packet.Text.Length > 0)
        {
            // Строки статуса живут в общем блоке, прижатом к дну клетки:
            // иконка сверху, цены снизу — раскладка legacy-клетки.
            var statusBlock = new VisualElement();
            statusBlock.AddToClassList("packet-inventory-card__status");
            statusBlock.pickingMode = PickingMode.Ignore;

            // Строка статуса на метку: цены ордеров приходят раскрашенными
            // legacy-тегами, и каждая линия статуса — своим цветом.
            // Однострочный статус — это количество предмета: бейдж в правом
            // нижнем углу клетки (старый клиент); многострочные цены остаются
            // центрированной стопкой.
            var statusLines = new List<(Label Label, Color? LineColor)>();
            foreach (string line in packet.Text.Split('\n'))
            {
                (Color? lineColor, string text) = ParseLegacyColoredLine(line);
                if (text.Length == 0)
                {
                    continue;
                }

                statusLines.Add((new Label(text), lineColor));
            }

            bool amountBadge = statusLines.Count == 1;
            foreach ((Label lineLabel, Color? lineColor) in statusLines)
            {
                lineLabel.AddToClassList(amountBadge
                    ? "packet-inventory-card__amount"
                    : "packet-inventory-card__label");
                if (amountBadge)
                {
                    // Счётчик пришивается к правому нижнему углу клетки напрямую
                    // (абсолютом): не зависит от высоты клетки, блока статуса и
                    // порядка каскада — угол есть угол (репорт: «не прижата»).
                    lineLabel.style.position = Position.Absolute;
                    lineLabel.style.right = 4;
                    lineLabel.style.bottom = 2;
                }

                if (lineColor is { } color)
                {
                    lineLabel.style.color = color;
                }

                lineLabel.pickingMode = PickingMode.Ignore;
                statusBlock.Add(lineLabel);
            }

            card.Add(statusBlock);
        }

        return card;
    }
}
