#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Kern.Core.Interfaces;
using Kern.Networking;
using Kern.UI.Binding;
using Kern.UI.Builders;
using MinesServer.Networking.Client.Packets.GUI;
using MinesServer.Networking.Server.Packets.GUI;
using MinesServer.Networking.Server.Packets.GUI.Components;
using MinesServer.Networking.Server.Packets.GUI.Components.Containers;
using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using MinesServer.Networking.Shared.Packets;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace Kern.UI;

public sealed class ServerWindowPresenter : IDisposable
{
    /// <summary>
    /// Пол авто-высоты каркаса: страница без контента (подтверждение «Да/Нет»)
    /// не должна схлопываться в узкую полоску — окно остаётся читаемым.
    /// </summary>
    private const float AutoHeightFloor = 240f;

    private static readonly Regex s_canvasImagePattern = new(
        @"^(?<x>-?\d+x)?(?<y>-?\d+y)?(?<h>\d+h)?(?<w>\d+w)=I#(?<uri>.+)$",
        RegexOptions.CultureInvariant);

    private readonly IAssetLoader _assetLoader;
    private readonly IAsyncOperationSupervisor _operations;
    private readonly IWebAssetLoader _webAssetLoader;
    private readonly UIInputManager _uiInputManager;
    private readonly INetworkService _networkService;
    private readonly UIDocument _document;
    private readonly WindowCommandStream _commands;
    private readonly ModalWindowHandler _modalWindowHandler;
    private readonly IItemCatalog _itemCatalog;
    private readonly List<WindowState> _openWindows = [];

    public ServerWindowPresenter(
        IAssetLoader assetLoader,
        UIInputManager uiInputManager,
        INetworkService networkService,
        UIDocument document,
        WindowCommandStream commands,
        IAsyncOperationSupervisor operations,
        IWebAssetLoader webAssetLoader,
        IItemCatalog itemCatalog)
    {
        _assetLoader = assetLoader;
        _operations = operations;
        _webAssetLoader = webAssetLoader;
        _uiInputManager = uiInputManager;
        _networkService = networkService;
        _document = document;
        _commands = commands;
        _itemCatalog = itemCatalog;
        _modalWindowHandler = new ModalWindowHandler(document, uiInputManager);
        _commands.OpenRequested += Open;
        _commands.CloseRequested += Close;
        _commands.ModalRequested += ShowModal;
        _commands.ElementUpdateRequested += ApplyElementUpdate;

        // Первое окно не платит за разовую инициализацию движка шаблонов
        // (репорт: первое открытие маркета — 3.2 с в биндинге, далее 1 мс).
        WindowBinding.WarmUpInBackground(operations);
    }

    public bool HasOpenWindows => _openWindows.Count > 0;

    public string? TopWindowTag => _openWindows.Count > 0 ? _openWindows[^1].Tag : null;

    public bool IsModalShowing => _modalWindowHandler.IsShowing;

    /// <summary>
    /// ESC: легаси-окна не имеют серверного действия «закрыть по ESC» —
    /// единственный закрывающий элемент окна это кнопка «ВЫЙТИ» (LegacyWindow
    /// вешает на неё close), поэтому ESC шлёт клик по её индексу. Если кнопки
    /// в окне нет, ESC поглощается: клик по случайному элементу (первая
    /// вкладка) хуже бездействия.
    /// </summary>
    public bool TryRequestCloseTopWindow()
    {
        if (_openWindows.Count == 0)
        {
            return false;
        }

        WindowState state = _openWindows[^1];
        for (int index = state.ClickableOrder.Count - 1; index >= 0; index--)
        {
            if (TextPacketBuilder.IsExitButton(state.ClickableOrder[index]))
            {
                _networkService.Send(new ElementClickPacket(state.Tag, index, []));
                return true;
            }
        }

        return true;
    }

    /// <summary>
    /// Закрыть серверное окно. Вызывается из PauseMenu: он владеет Escape в
    /// фазе Update, и модалка должна гаситься там же — иначе нажатие
    /// открывало паузу поверх заблокированного ввода, а окно оставалось.
    /// </summary>
    public void DismissModal() => _modalWindowHandler.Hide();

    public void Dispose()
    {
        _commands.OpenRequested -= Open;
        _commands.CloseRequested -= Close;
        _commands.ModalRequested -= ShowModal;
        _commands.ElementUpdateRequested -= ApplyElementUpdate;

        // Оверлей тоже убирается. Панель документа переживает сцену, и оставшийся
        // элемент копился между входами в игру: новый обработчик создавал второй,
        // а поиск по классу отдавал старый, скрытый. Плюс Dispose снимает
        // подписку на планировщик.
        _modalWindowHandler.Dispose();
        foreach (WindowState state in _openWindows)
        {
            state.Binding.Dispose();
            state.Overlay.RemoveFromHierarchy();
        }

        _openWindows.Clear();
        _commands.SetServerWindowVisibility(false);
    }

    private void Open(OpenWindowPacket packet)
    {
        try
        {
            OpenCore(packet);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[PacketUI] Failed to open window '{packet.WindowTag}' " +
                $"({packet.Width}x{packet.Height}): {exception}");
            throw;
        }
    }

    private void OpenCore(OpenWindowPacket packet)
    {
        bool actionTagWindow = packet.WindowTag == "exit" && packet.Content is ScrollViewerPacket;
        if (actionTagWindow)
        {
            packet = ExpandSerializedRows(packet);
        }

        packet = PromoteTabStrip(packet);
        ValidateWindowPacket(packet);

        // UIDocument може бути вимкненим (BypassGameUI). Серверне вікно потребує
        // активного дерева — вмикаємо документ перед додаванням елемента.
        if (!_document.enabled)
        {
            _document.enabled = true;
        }

        VisualElement element = new();
        element.AddToClassList("packet-window-overlay");
        VisualElement frame = new();
        frame.AddToClassList("packet-window-frame");
        frame.style.width = packet.Width;

        // Высота каркаса по содержимому (мост ставит PacketUI.AutoSizeHeight
        // на корень каждого легаси-окна): точную высоту рендера знает только
        // клиент — шрифты, переносы, размеры иконок. Каркас обнимает контент
        // в границах: пол (AutoHeightFloor) не даёт крошечным страницам
        // («Да/Нет» подтверждения) схлопываться, потолок — Height из пакета
        // и 96% экрана (max-height в .packet-window-frame); за потолком
        // начинается прокрутка.
        if (AttachedProperties.Has(packet.Content, "PacketUI.AutoSizeHeight"))
        {
            frame.style.height = StyleKeyword.Auto;
            frame.style.minHeight = AutoHeightFloor;
            frame.style.maxHeight = packet.Height;
        }
        else
        {
            frame.style.height = packet.Height;
        }

        element.Add(frame);
        var state = new WindowState
        {
            Tag = packet.WindowTag,
            Overlay = element,
            Frame = frame,
            Binding = new WindowBinding(),
            Tree = packet.Content,
            ActionTagWindow = actionTagWindow,
        };

        // Временный замер этапов (репорт: маркет сильно лагает при заходе).
        // Лог показывает, какой именно этап съедает время: сборка дерева,
        // биндинг живых шаблонов или регистрация кликабельных.
        var openWatch = System.Diagnostics.Stopwatch.StartNew();
        BuildWindowContent(state);
        long buildMs = openWatch.ElapsedMilliseconds;
        int elementCount = 0;
        CountTree(element);
        void CountTree(VisualElement e)
        {
            elementCount++;
            foreach (VisualElement child in e.Children())
            {
                CountTree(child);
            }
        }

        // Легаси-GUI — одно окно на игрока: сервер переотрисовывает его на
        // КАЖДОЕ действие (вход, таб, кнопка), раздавая каждого рендеру новый
        // уникальный тег (ProtocolWindow.MakeTag). Поэтому заменяем по тегу
        // нельзя — новый рендер закрывает ВСЕ прежние легаси-окна. Без этого
        // копии окон накапливаются в иерархии весь сеанс: каждая остаётся в
        // layout и модальном стеке под новой, и лаги растут с каждым кликом
        // (репорт: маркет сильно лагает при заходе).
        foreach (WindowState old in _openWindows)
        {
            old.Binding.Dispose();
            _uiInputManager.PopModal(old.Overlay);
            old.Overlay.RemoveFromHierarchy();
        }

        _openWindows.Clear();
        _document.rootVisualElement.Add(element);
        _uiInputManager.PushModal(element);
        _openWindows.Add(state);
        _commands.SetServerWindowVisibility(true);
        Debug.Log($"[PacketUI] Открыто окно '{packet.WindowTag}' ({packet.Width}x{packet.Height}): " +
            $"элементов {elementCount}; сборка и биндинг {buildMs} мс.");
    }

    /// <summary>
    /// Контент окна: сборка дерева из пакета, биндинг живых шаблонов,
    /// регистрация кликабельных, крестик. Вызывается при открытии и при
    /// живом обновлении элемента — и там, и там окно строится с нуля из
    /// актуального дерева, так что индексы кликабельных и обработчики
    /// остаются согласованными между собой.
    /// </summary>
    private void BuildWindowContent(WindowState state)
    {
        state.Frame.Clear();
        VisualElement packetRoot = new PacketUIBuilder(_assetLoader, _operations, _webAssetLoader, _itemCatalog).Build(state.Tree);
        packetRoot.AddToClassList("packet-window-content");
        state.Frame.Add(packetRoot);
        state.Binding.Bind(packetRoot);
        state.ClickableOrder = CollectClickableOrder(state.Tree);
        RegisterClickableElements(packetRoot, packetRoot, state);
        AddExitCloseButton(state.Frame, state.Tag, state.ClickableOrder);
    }

    /// <summary>
    /// Живое обновление элемента (WindowUpdatePacket по "UI.Id"): в сохранённом
    /// дереве пакета элемент заменяется присланным компонентом, после чего
    /// контент окна пересобирается. Пересборка, а не точечная подмена: клик
    /// легаси-элементов адресуется индексом в pre-order списке кликабельных,
    /// и подмена поддерева сдвинула бы индексы у всех, кто стоит дальше.
    /// </summary>
    private void ApplyElementUpdate(WindowUpdatePacket packet)
    {
        WindowState? state = FindByTag(packet.WindowTag);
        if (state == null)
        {
            Debug.LogWarning($"[PacketUI] WindowUpdate for unknown window '{packet.WindowTag}'.");
            return;
        }

        IGUIComponentPacket? updated = ReplaceByElementId(state.Tree, packet.ElementId, packet.Component);
        if (updated == null)
        {
            Debug.LogWarning($"[PacketUI] WindowUpdate: element {packet.ElementId} not found " +
                $"in window '{packet.WindowTag}'.");
            return;
        }

        state.Tree = updated;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        BuildWindowContent(state);
        Debug.Log($"[PacketUI] Окно '{state.Tag}': элемент {packet.ElementId} обновлён " +
            $"({packet.Component.GetType().Name}), пересборка {watch.ElapsedMilliseconds} мс.");
    }

    /// <summary>
    /// Поиск в дереве компонента с attached-свойством "UI.Id" и его замена.
    /// Возвращает новое дерево (заменённые контейнеры клонируются through with),
    /// null — элемента с таким id в дереве нет. Совпадение с корнем возвращает
    /// replacement — он становится новым корнем окна.
    /// </summary>
    private static IGUIComponentPacket? ReplaceByElementId(
        IGUIComponentPacket node,
        uint elementId,
        IGUIComponentPacket replacement)
    {
        string? own = AttachedProperties.Find(node, "UI.Id");
        if (uint.TryParse(own, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) &&
            parsed == elementId)
        {
            return replacement;
        }

        if (node is not ContainerComponentPacket container)
        {
            return null;
        }

        for (int index = 0; index < container.Children.Count; index++)
        {
            IGUIComponentPacket? replaced = ReplaceByElementId(container.Children[index], elementId, replacement);
            if (replaced == null)
            {
                continue;
            }

            var children = new List<IGUIComponentPacket>(container.Children) { [index] = replaced };
            return container with { Children = children };
        }

        return null;
    }

    private WindowState? FindByTag(string tag)
    {
        for (int index = _openWindows.Count - 1; index >= 0; index--)
        {
            if (_openWindows[index].Tag == tag)
            {
                return _openWindows[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Открытое серверное окно: визуальное дерево, биндинг и данные, по которым
    /// живут клики и обновления. Tree — обработанное дерево пакета (после
    /// ExpandSerializedRows/PromoteTabStrip): из него контент пересобирается
    /// при WindowUpdatePacket.
    /// </summary>
    private sealed class WindowState
    {
        public required string Tag { get; init; }
        public required VisualElement Overlay { get; init; }
        public required VisualElement Frame { get; init; }
        public required WindowBinding Binding { get; init; }
        public required IGUIComponentPacket Tree { get; set; }
        public required bool ActionTagWindow { get; init; }

        /// <summary>Пересчитывается при каждой сборке контента (см. CollectClickableOrder).</summary>
        public List<IGUIComponentPacket> ClickableOrder { get; set; } = [];
    }

    private static OpenWindowPacket ExpandSerializedRows(OpenWindowPacket packet)
    {
        var scroll = (ScrollViewerPacket)packet.Content;
        var children = new List<IGUIComponentPacket>();
        int footerStart = scroll.Children.Count;
        while (footerStart > 0 &&
            scroll.Children[footerStart - 1] is TextPacket footerText &&
            !string.IsNullOrEmpty(footerText.OnClickContext))
        {
            footerStart--;
        }

        for (int index = 0; index < scroll.Children.Count;)
        {
            IGUIComponentPacket child = scroll.Children[index];

            if (index >= footerStart && child is TextPacket footerButton)
            {
                bool isExit = footerButton.OnClickContext is "exit" or "exit:0";
                children.Add(footerButton with
                {
                    AttachedProperties = isExit
                        ?
                        [
                            .. footerButton.AttachedProperties,
                            new StringPairPacket("PacketUI.FooterAction", "1"),
                            new StringPairPacket("PacketUI.ExitAction", "1"),
                        ]
                        :
                        [
                            .. footerButton.AttachedProperties,
                            new StringPairPacket("PacketUI.FooterAction", "1"),
                        ],
                });
                index++;
                continue;
            }

            if (index == 0 && child is TextPacket title &&
                string.IsNullOrEmpty(title.OnClickContext))
            {
                children.Add(title with
                {
                    AttachedProperties =
                    [
                        .. title.AttachedProperties,
                        new StringPairPacket("PacketUI.Title", "1"),
                        new StringPairPacket("Text.Align", "Center"),
                    ],
                });
                index++;

                // Полоса вкладок легаси-окна: мост (ProtocolWindow) помечает
                // вкладки PacketUI.TabAction/TabActive и кладёт их сразу за
                // заголовком. Собираем их в горизонтальный DockPanel-ряд.
                int tabCount = CountTabStrip(scroll.Children, index, footerStart);
                if (tabCount > 0)
                {
                    children.Add(BuildTabStrip(scroll.Children, index, tabCount));
                    index += tabCount;
                }

                continue;
            }

            if (child is TextPacket text &&
                (text.Text.Contains("\nbutton\n", StringComparison.Ordinal) ||
                 text.Text.Contains("=I#", StringComparison.Ordinal)))
            {
                ExpandSerializedContent(text, children);
            }
            else
            {
                children.Add(child);
            }

            index++;
        }

        return packet with { Content = scroll with { Children = children } };
    }

    /// <summary>
    /// Полоса вкладок легаси-окна. Мост LegacyWindow кладёт контейнер вкладок
    /// в дерево сразу за заголовком (заголовок → вкладки → текст → входы →
    /// кнопки → списки), дети — TextPacket: активная вкладка без OnClickContext,
    /// кликабельные с контекстом. Меток PacketUI.Tab* мост больше не ставит,
    /// поэтому горизонтальный DockPanel-ряд из вкладок собираем сами — на
    /// контрактной позиции. Экземпляры вкладок сохраняются (with-клоны с
    /// дочерпнутыми свойствами), поэтому кликабельные индексы не сдвигаются.
    /// </summary>
    private static OpenWindowPacket PromoteTabStrip(OpenWindowPacket packet)
    {
        if (packet.Content is not ScrollViewerPacket root)
        {
            return packet;
        }

        // Заголовок окна — первый некликабельный TextPacket: помечается
        // PacketUI.Title (стиль заголовка, центр) — новый мост меток не ставит.
        var children = new List<IGUIComponentPacket>(root.Children);
        if (children.Count > 0 &&
            children[0] is TextPacket titleCandidate &&
            string.IsNullOrEmpty(titleCandidate.OnClickContext) &&
            !AttachedProperties.Has(titleCandidate, "PacketUI.Title"))
        {
            children[0] = titleCandidate with
            {
                AttachedProperties =
                [
                    .. titleCandidate.AttachedProperties,
                    new StringPairPacket("PacketUI.Title", "1"),
                ],
            };
        }

        // Вкладки стоят на позиции 0 (без заголовка) или 1 (за заголовком).
        // Заголовок — некликабельный TextPacket в начале; тело страницы тоже
        // некликабельный TextPacket, но оно стоит уже после вкладок, так что
        // на позицию 0/1 оно не попадает.
        int tabIndex = 0;
        if (children.Count > 0 &&
            children[0] is TextPacket first &&
            string.IsNullOrEmpty(first.OnClickContext))
        {
            tabIndex = 1;
        }

        if (tabIndex >= children.Count ||
            children[tabIndex] is not ScrollViewerPacket container ||
            container.Children.Count == 0)
        {
            return packet with { Content = root with { Children = children } };
        }

        // Контейнер кнопок тоже состоит из TextPacket и стоит близко к началу.
        // Отличаем по кнопке «ВЫЙТИ»: мост добавляет её в кнопки всегда, а во
        // вкладках метки-переключателя быть не может.
        bool allText = true;
        bool anyClickable = false;
        bool hasExit = false;
        foreach (IGUIComponentPacket child in container.Children)
        {
            if (child is not TextPacket tab)
            {
                allText = false;
                break;
            }

            if (!string.IsNullOrEmpty(tab.OnClickContext))
            {
                anyClickable = true;
            }

            if (TextPacketBuilder.IsExitButton(tab))
            {
                hasExit = true;
            }
        }

        if (!allText || !anyClickable || hasExit)
        {
            return packet;
        }

        var tabs = new List<IGUIComponentPacket>(container.Children.Count);
        foreach (IGUIComponentPacket child in container.Children)
        {
            var tab = (TextPacket)child;
            string marker = string.IsNullOrEmpty(tab.OnClickContext)
                ? "PacketUI.TabActive"
                : "PacketUI.TabAction";
            tabs.Add(tab with
            {
                AttachedProperties =
                [
                    .. tab.AttachedProperties,
                    new StringPairPacket(marker, "1"),
                    new StringPairPacket("DockPanel.Dock", "Left"),
                ],
            });
        }

        var strip = new DockPanelPacket
        {
            Children = tabs,
            AttachedProperties = [new StringPairPacket("DockPanel.Dock", "Top")],
        };

        children[tabIndex] = strip;
        return packet with { Content = root with { Children = children } };
    }

    // Вкладка легаси-окна — TextPacket с маркером моста ProtocolWindow.
    private static bool IsTabPacket(IGUIComponentPacket packet) =>
        packet is TextPacket text &&
        (AttachedProperties.Has(text, "PacketUI.TabAction") ||
         AttachedProperties.Has(text, "PacketUI.TabActive"));

    private static int CountTabStrip(IReadOnlyList<IGUIComponentPacket> children, int start, int limit)
    {
        int count = 0;
        while (start + count < limit && IsTabPacket(children[start + count]))
        {
            count++;
        }

        return count;
    }

    private static IGUIComponentPacket BuildTabStrip(
        IReadOnlyList<IGUIComponentPacket> children,
        int start,
        int count)
    {
        var tabs = new List<IGUIComponentPacket>(count);
        for (int i = 0; i < count; i++)
        {
            var tab = (TextPacket)children[start + i];
            tabs.Add(tab with
            {
                AttachedProperties =
                [
                    .. tab.AttachedProperties,
                    new StringPairPacket("DockPanel.Dock", "Left"),
                ],
            });
        }

        return new DockPanelPacket
        {
            Children = tabs,
            AttachedProperties = [new StringPairPacket("DockPanel.Dock", "Top")],
        };
    }

    private static void ExpandSerializedContent(TextPacket source, List<IGUIComponentPacket> children)
    {
        string[] lines = source.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var plain = new StringBuilder();
        for (int index = 0; index < lines.Length;)
        {
            Match canvasImage = s_canvasImagePattern.Match(lines[index]);
            if (canvasImage.Success)
            {
                FlushPlainText(plain, children);
                int x = CanvasNumber(canvasImage, "x");
                int y = CanvasNumber(canvasImage, "y");
                int width = CanvasNumber(canvasImage, "w");
                int height = CanvasNumber(canvasImage, "h");
                if (width <= 0 || height <= 0 || x < 0 || y < 0)
                {
                    throw new InvalidOperationException(
                        $"[PacketUI] Invalid serialized canvas image bounds at line {index + 1}.");
                }

                children.Add(new CanvasPacket
                {
                    AttachedProperties =
                    [
                        new StringPairPacket("PacketUI.CanvasHeight", (y + height).ToString(CultureInfo.InvariantCulture)),
                    ],
                    Children =
                    [
                        new TextPacket
                        {
                            AttachedProperties =
                            [
                                new StringPairPacket("PacketUI.ImageURI", canvasImage.Groups["uri"].Value),
                                new StringPairPacket("Canvas.X", x.ToString(CultureInfo.InvariantCulture)),
                                new StringPairPacket("Canvas.Y", y.ToString(CultureInfo.InvariantCulture)),
                                new StringPairPacket("Canvas.Width", width.ToString(CultureInfo.InvariantCulture)),
                                new StringPairPacket("Canvas.Height", height.ToString(CultureInfo.InvariantCulture)),
                            ],
                        },
                    ],
                });
                index++;
                continue;
            }

            if (index + 1 < lines.Length && lines[index + 1] == "button")
            {
                if (index + 4 >= lines.Length ||
                    lines[index + 2] != lines[index + 4] ||
                    string.IsNullOrWhiteSpace(lines[index + 3]))
                {
                    throw new InvalidOperationException(
                        $"[PacketUI] Malformed serialized button in window text at line {index + 1}.");
                }

                FlushPlainText(plain, children);
                children.Add(new TextPacket
                {
                    Text = lines[index],
                    AttachedProperties = [new StringPairPacket("PacketUI.RowLabel", "1")],
                });
                children.Add(new TextPacket
                {
                    Text = lines[index + 2],
                    OnClickContext = lines[index + 3],
                });
                index += 5;
                continue;
            }

            if (index + 1 < lines.Length && lines[index + 1] == "text")
            {
                FlushPlainText(plain, children);
                children.Add(new TextPacket { Text = lines[index] });
                index += 2;
                continue;
            }

            if (plain.Length > 0)
            {
                plain.Append('\n');
            }

            plain.Append(lines[index]);
            index++;
        }

        FlushPlainText(plain, children);
    }

    private static int CanvasNumber(Match match, string group)
    {
        string value = match.Groups[group].Value;
        return value.Length == 0
            ? 0
            : int.Parse(value[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static void FlushPlainText(StringBuilder text, List<IGUIComponentPacket> children)
    {
        if (text.Length > 0)
        {
            string content = text.ToString().TrimEnd('\n', '\r');
            if (content.Length > 0)
            {
                children.Add(new TextPacket { Text = content });
            }

            text.Clear();
        }
    }

    private static void ValidateWindowPacket(OpenWindowPacket packet)
    {
        if (packet.Content == null || packet.Width == 0 || packet.Height == 0)
        {
            FailWindowPacket(packet, "missing content or zero window dimensions");
        }

        string? format = AttachedProperties.Find(packet.Content, "Popup.Format");
        if (format != null)
        {
            FailWindowPacket(packet,
                $"unsupported string-based Popup.Format='{format}'; expected GUI components");
        }

        if (packet.Content is ScrollViewerPacket scroll)
        {
            foreach (IGUIComponentPacket child in scroll.Children)
            {
                if (child is TextPacket text &&
                    (text.Text.Contains("\nbutton\n", StringComparison.Ordinal) ||
                     text.Text.Contains("\n3card\n", StringComparison.Ordinal) ||
                     text.Text.Contains("\ndrop\n", StringComparison.Ordinal) ||
                     text.Text.Contains("=I#", StringComparison.Ordinal)))
                {
                    FailWindowPacket(packet,
                        "a TextPacket contains serialized controls; expected clickable GUI components");
                }
            }
        }
    }

    [DoesNotReturn]
    private static void FailWindowPacket(OpenWindowPacket packet, string reason)
    {
        string message = $"[PacketUI] Invalid OpenWindowPacket tag='{packet.WindowTag}', " +
            $"size={packet.Width}x{packet.Height}, root={packet.Content?.GetType().Name ?? "null"}: {reason}.";
        Debug.LogError(message);
        throw new InvalidOperationException(message);
    }

    private void Close(CloseWindowPacket packet)
    {
        if (_openWindows.Count == 0)
        {
            return;
        }

        WindowState state = _openWindows[^1];
        state.Binding.Dispose();
        _uiInputManager.PopModal(state.Overlay);
        state.Overlay.RemoveFromHierarchy();
        _openWindows.RemoveAt(_openWindows.Count - 1);
        _commands.SetServerWindowVisibility(HasOpenWindows);
    }

    private void ShowModal(ModalWindowPacket packet)
    {
        _modalWindowHandler.Show(packet);
    }

    /// <summary>
    /// Pre-order обход дерева пакета, собирающий только кликабельные элементы.
    /// Сервер адресует клики индексом в таком же списке (GuiWindowModel.Clickables),
    /// поэтому индекс элемента — это номер в этом порядке, а не позиция в дереве:
    /// контейнеры и некликабельные тексты индексы не получают.
    /// </summary>
    private static List<IGUIComponentPacket> CollectClickableOrder(IGUIComponentPacket packet)
    {
        var order = new List<IGUIComponentPacket>();
        Walk(packet);
        return order;

        void Walk(IGUIComponentPacket current)
        {
            if (!string.IsNullOrEmpty(current.OnClickContext))
            {
                order.Add(current);
            }

            if (current is IContainerComponentPacket container)
            {
                foreach (IGUIComponentPacket child in container.Children)
                {
                    Walk(child);
                }
            }
        }
    }

    private static int ClickableIndexOf(
        List<IGUIComponentPacket> clickableOrder,
        IGUIComponentPacket target)
    {
        for (int index = 0; index < clickableOrder.Count; index++)
        {
            if (ReferenceEquals(clickableOrder[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private void RegisterClickableElements(VisualElement element, VisualElement windowRoot, WindowState state)
    {
        if (element.userData is IGUIComponentPacket componentPacket &&
            !string.IsNullOrEmpty(componentPacket.OnClickContext))
        {
            int elementIndex = ClickableIndexOf(state.ClickableOrder, componentPacket);
            if (elementIndex < 0)
            {
                string message = $"[PacketUI] Clickable component {componentPacket.GetType().Name} " +
                    $"is missing from packet tree for window '{state.Tag}'.";
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            element.RegisterCallback<ClickEvent>(click =>
            {
                click.StopPropagation();
                HandleElementClick(element, windowRoot, elementIndex, state);
            });
        }

        foreach (VisualElement child in element.Children())
        {
            RegisterClickableElements(child, windowRoot, state);
        }
    }

    /// <summary>
    /// Белый крестик в правом верхнем углу рамки — замена кнопки «ВЫЙТИ»
    /// (старый путь: маркер PacketUI.ExitAction; новый мост: текст «ВЫЙТИ»,
    /// см. TextPacketBuilder.IsExitButton). Клик адресуется как у остальных
    /// элементов — по тегу окна и индексу кликабельного: сервер закрывает
    /// окно сам (close из Session.SendWin) после проверки OwnsTag. OnClickContext
    /// кнопки «ВЫЙТИ» в поле тега подставлять нельзя: в новом мосте это путь
    /// сбора инпутов («.», «/n»), а не тег — такие клики сервер отбрасывал
    /// как stale window tag, и крестик не работал.
    /// </summary>
    private void AddExitCloseButton(
        VisualElement frame,
        string windowTag,
        List<IGUIComponentPacket> clickableOrder)
    {
        for (int elementIndex = 0; elementIndex < clickableOrder.Count; elementIndex++)
        {
            if (clickableOrder[elementIndex] is not TextPacket exit ||
                string.IsNullOrEmpty(exit.OnClickContext) ||
                (!AttachedProperties.Has(exit, "PacketUI.ExitAction") &&
                 !TextPacketBuilder.IsExitButton(exit)))
            {
                continue;
            }

            var closeButton = new Button { name = "PacketWindowClose", tooltip = "Закрыть" };
            closeButton.AddToClassList("packet-window-close");
            closeButton.focusable = false;

            // VisualElement.classes в публичном API Unity 6000.6 нет, классы
            // добавляются через AddToClassList.
            var closeBarA = new VisualElement();
            closeBarA.AddToClassList("packet-window-close-bar");
            closeBarA.AddToClassList("packet-window-close-bar--a");
            closeButton.Add(closeBarA);
            var closeBarB = new VisualElement();
            closeBarB.AddToClassList("packet-window-close-bar");
            closeBarB.AddToClassList("packet-window-close-bar--b");
            closeButton.Add(closeBarB);

            closeButton.clicked += () => _networkService.Send(
                new ElementClickPacket(windowTag, elementIndex, []));
            frame.Add(closeButton);
            return;
        }
    }

    private void HandleElementClick(
        VisualElement clickedElement,
        VisualElement windowRoot,
        int elementIndex,
        WindowState state)
    {
        if (clickedElement.userData is not IGUIComponentPacket componentPacket)
        {
            string message = $"[PacketUI] Clicked element {elementIndex} in window '{state.Tag}' " +
                "has no GUI component packet.";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        if (state.ActionTagWindow)
        {
            // Легаси-окна (мост ProtocolWindow): action кнопки живёт в OnClickContext,
            // а значения инпутов окна (слайдеры crys0..crys5, поле "input") собираем
            // со всего окна и отправляем в Context — сервер подставляет их в макросы
            // %M%/%I% action-строки (ProtocolWindow.SubstituteActionMacros) до
            // диспетчеризации. Без значений кнопки типа «Продать:%M%» умирают
            // в long.Parse("%M%") на сервере.
            StringPairPacket[] windowInputValues = ClickContextResolver.CollectInputValues(windowRoot);
            _networkService.Send(new ElementClickPacket(
                componentPacket.OnClickContext,
                elementIndex,
                windowInputValues));
            return;
        }

        VisualElement? inputRoot = ClickContextResolver.ResolveRoot(
            clickedElement,
            windowRoot,
            componentPacket.OnClickContext);
        if (inputRoot == null)
        {
            string message = $"[PacketUI] Invalid OnClickContext '{componentPacket.OnClickContext}' " +
                $"for component {elementIndex} ({componentPacket.GetType().Name}) " +
                $"in window '{state.Tag}'.";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        StringPairPacket[] inputValues = ClickContextResolver.CollectInputValues(inputRoot);
        _networkService.Send(new ElementClickPacket(state.Tag, elementIndex, inputValues));
    }
}
