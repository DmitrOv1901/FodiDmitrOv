#nullable enable

using System;
using System.Collections.Generic;
using MinesServer.Networking.Server.Packets.GUI;
using MinesServer.Networking.Server.Packets.GUI.Components;
using MinesServer.Networking.Server.Packets.GUI.Components.Containers;
using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using UnityEngine.UIElements;

namespace Kern.UI.Builders;
public class ScrollViewerPacketBuilder : PacketUIBuilderBase<ScrollViewerPacket>
{
    protected override VisualElement BuildTyped(ScrollViewerPacket packet, PacketUIBuilder builder)
    {
        // Страница без прокрутки (мост ставит PacketUI.FixedPage на UP-окне):
        // контент строится в плоский контейнер целиком — скролла нет вообще,
        // вся отрисовка остаётся на месте (репорт: в UP скролла быть не должно).
        bool fixedPage = AttachedProperties.Has(packet, "PacketUI.FixedPage");
        ScrollView? scrollView = null;
        VisualElement container;
        if (fixedPage)
        {
            container = new VisualElement { name = packet.Name };
            container.AddToClassList("packet-window--fixed");
        }
        else
        {
            scrollView = new ScrollView
            {
                name = packet.Name,
                horizontalScrollerVisibility = MapScrollVisibility(packet.HorizontalScrollBar),
                verticalScrollerVisibility = MapScrollVisibility(packet.VerticalScrollBar),
            };
            scrollView.verticalScroller.AddToClassList("packet-scrollbar");
            scrollView.verticalScroller.AddToClassList("packet-scrollbar--vertical");
            scrollView.horizontalScroller.AddToClassList("packet-scrollbar");
            scrollView.horizontalScroller.AddToClassList("packet-scrollbar--horizontal");
            container = scrollView;
        }

        VisualElement content = scrollView != null ? scrollView.contentContainer : container;
        var footer = new VisualElement();
        footer.AddToClassList("packet-window-footer");

        // Строки слайдера (метки моста ProtocolWindow: PacketUI.SliderRow*): лиды копятся
        // до слайдера, суффикс пристраивается после него — все в одном горизонтальном ряду,
        // как в legacy-клиенте: [иконка][цена][текущее]───дорожка───[/вместимость].
        List<VisualElement>? pendingRowLeads = null;
        VisualElement? activeSliderRow = null;
        // Карточки инвентаря (PacketUI.InventoryCard) копятся в сетку-перенос —
        // перенос GridLayoutGroup старого клиента: ширина окна сама режет ряды.
        List<VisualElement>? pendingCards = null;
        void FlushPendingCards()
        {
            if (pendingCards == null)
            {
                return;
            }

            if (pendingCards.Count > 0)
            {
                var grid = new VisualElement();
                grid.AddToClassList("packet-inventory-grid");
                foreach (VisualElement card in pendingCards)
                {
                    grid.Add(card);
                }

                content.Add(grid);
            }

            pendingCards = null;
        }
        void FlushPendingLeads()
        {
            FlushPendingCards();
            if (pendingRowLeads == null)
            {
                return;
            }

            foreach (VisualElement lead in pendingRowLeads)
            {
                content.Add(lead);
            }

            pendingRowLeads = null;
        }

        for (int index = 0; index < packet.Children.Count; index++)
        {
            IGUIComponentPacket child = packet.Children[index];
            if (AttachedProperties.Has(child, "PacketUI.InventoryCard"))
            {
                VisualElement card = builder.Build(child);
                (pendingCards ??= new List<VisualElement>()).Add(card);
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.ExitAction") ||
                TextPacketBuilder.IsExitButton(child))
            {
                // Кнопка «ВЫЙТИ» не строится ни в старом (маркер ExitAction),
                // ни в новом мосту (текст «ВЫЙТИ»): окно закрывает белый
                // крестик в углу рамки, который добавляет ServerWindowPresenter,
                // и он же ловит ESC. Пакет остаётся в кликабельном порядке
                // (ServerWindowPresenter.CollectClickableOrder), поэтому
                // индексы элементов для сервера не сдвигаются.
                FlushPendingCards();
                pendingRowLeads = null;
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.FooterAction"))
            {
                FlushPendingLeads();
                VisualElement action = builder.Build(child);
                if (footer.childCount > 0)
                {
                    action.AddToClassList("packet-window-footer-action--spaced");
                }

                footer.Add(action);
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.RowLabel"))
            {
                FlushPendingLeads();
                if (index + 1 >= packet.Children.Count ||
                    string.IsNullOrEmpty(packet.Children[index + 1].OnClickContext))
                {
                    throw new InvalidOperationException("[PacketUI] Serialized row has no action component.");
                }

                var row = new VisualElement();
                row.AddToClassList("packet-window-row");
                VisualElement label = builder.Build(child);
                label.AddToClassList("packet-window-row-label");
                row.Add(label);
                row.Add(builder.Build(packet.Children[++index]));
                content.Add(row);
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.FormRowLead"))
            {
                // Строка ввода richlist из моста: [подпись]────[поле] в одном
                // горизонтальном ряду, как в legacy-клиенте (UInt32/DropDown/
                // Boolean: подпись слева, инпут прижат вправо).
                FlushPendingLeads();
                if (index + 1 >= packet.Children.Count ||
                    !AttachedProperties.Has(packet.Children[index + 1], "PacketUI.FormRowInput"))
                {
                    throw new InvalidOperationException("[PacketUI] Form row lead has no input component.");
                }

                var formRow = new VisualElement();
                formRow.AddToClassList("packet-window-form-row");
                VisualElement rowLabel = builder.Build(child);
                rowLabel.AddToClassList("packet-window-form-row-label");
                formRow.Add(rowLabel);
                VisualElement rowInput = builder.Build(packet.Children[++index]);
                rowInput.AddToClassList("packet-window-form-row-input");
                formRow.Add(rowInput);
                content.Add(formRow);
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.SliderRowLead"))
            {
                VisualElement lead = builder.Build(child);
                // Картинка-лид — иконка кристалла: живёт на инлайн-размере из
                // пакета. Минимальная ширина числовой колонки вытянула бы фон
                // по X (репорт: текстура кристалла расплющена).
                lead.AddToClassList(child is ImagePacket
                    ? "packet-window-slider-row-lead-icon"
                    : "packet-window-slider-row-lead");
                (pendingRowLeads ??= new List<VisualElement>()).Add(lead);
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.SliderRow"))
            {
                var sliderRow = new VisualElement();
                sliderRow.AddToClassList("packet-window-slider-row");
                if (pendingRowLeads != null)
                {
                    foreach (VisualElement lead in pendingRowLeads)
                    {
                        sliderRow.Add(lead);
                    }

                    pendingRowLeads = null;
                }

                VisualElement slider = builder.Build(child);
                slider.AddToClassList("packet-window-slider-row-slider");
                sliderRow.Add(slider);
                content.Add(sliderRow);
                activeSliderRow = sliderRow;
                continue;
            }

            if (AttachedProperties.Has(child, "PacketUI.SliderRowSuffix"))
            {
                VisualElement suffix = builder.Build(child);
                suffix.AddToClassList("packet-window-slider-row-suffix");
                if (AttachedProperties.Has(child, "PacketUI.PriceColumn"))
                {
                    suffix.AddToClassList("packet-window-slider-row-price");
                }

                if (activeSliderRow != null)
                {
                    activeSliderRow.Add(suffix);
                }
                else
                {
                    content.Add(suffix);
                }

                continue;
            }

            // Контейнер кнопок окна (мост помечает PacketUI.Buttons): строится
            // не вложенным ScrollView, а центрированным горизонтальным рядом —
            // приём нижних кнопок legacy-клиента и главного меню.
            if (child is IContainerComponentPacket buttonsContainer &&
                AttachedProperties.Has(child, "PacketUI.Buttons"))
            {
                FlushPendingLeads();
                var actionsRow = new VisualElement();
                actionsRow.AddToClassList("packet-window-actions-row");
                foreach (IGUIComponentPacket buttonPacket in buttonsContainer.Children)
                {
                    // «ВЫЙТИ» не строится и здесь: фильтр основного цикла видит
                    // только сам контейнер, но не его детей. Крестик в углу рамки
                    // шлёт тот же клик — контейнер не мешает ни кликабельному
                    // порядку (CollectClickableOrder спускается в детей), ни
                    // индексам ElementClickPacket.
                    if (AttachedProperties.Has(buttonPacket, "PacketUI.ExitAction") ||
                        TextPacketBuilder.IsExitButton(buttonPacket))
                    {
                        continue;
                    }

                    actionsRow.Add(builder.Build(buttonPacket));
                }

                content.Add(actionsRow);
                continue;
            }

            FlushPendingLeads();
            content.Add(builder.Build(child));
        }

        // Инвентарь часто стоит последним элементом страницы: хвост сетки
        // карточек никто не вытолкнет из цикла.
        FlushPendingCards();

        if (footer.childCount == 0)
        {
            return container;
        }

        var layout = new VisualElement();
        layout.AddToClassList("packet-window-body-layout");
        scrollView?.AddToClassList("packet-window-body-scroll");
        layout.Add(container);
        layout.Add(footer);
        return layout;
    }

    private static ScrollerVisibility MapScrollVisibility(ScrollbarVisibility visibility)
    {
        return visibility switch
        {
            ScrollbarVisibility.Hidden => ScrollerVisibility.Hidden,
            ScrollbarVisibility.Auto => ScrollerVisibility.Auto,
            ScrollbarVisibility.Visible => ScrollerVisibility.AlwaysVisible,
            _ => throw new InvalidOperationException(
                $"Invalid ScrollbarVisibility value {(int)visibility}."),
        };
    }
}
