#nullable enable

using MinesServer.Data;
using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI.Builders;
public class LinePacketBuilder : PacketUIBuilderBase<LinePacket>
{
    protected override VisualElement BuildTyped(LinePacket packet, PacketUIBuilder builder)
    {
        // Цвета в LinePacket нет: линия всегда белая (UILine рисует painter2D
        // по LineColor). Тематический циан из packet-line-themed убран вместе
        // с колбэком перекраски - без класса resolvedStyle.color наследовался
        // бы от предка и зависел от окна.
        var line = new UILine
        {
            Direction = packet.Direction,
            LineColor = Color.white,
        };

        if (packet.Style.HasValue)
        {
            if (packet.Style.Value.BorderWidth > 0)
            {
                line.Thickness = packet.Style.Value.BorderWidth;
            }
        }

        // Длинная ось тянется на 100% — это константа и живёт в USS.
        // Инлайном остаётся только толщина: она пришла из пакета.
        switch (packet.Direction)
        {
            case LineDirection.Horizontal:
                line.AddToClassList("packet-line--h");
                line.style.height = line.Thickness;
                break;
            case LineDirection.Vertical:
                line.AddToClassList("packet-line--v");
                line.style.width = line.Thickness;
                break;
            default:
                line.AddToClassList("packet-line--both");
                break;
        }

        return line;
    }
}
