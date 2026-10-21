#nullable enable

using System;
using MinesServer.Networking.Server.Packets.GUI;

namespace Kern.Networking.Processors;

public sealed class WindowPacketProcessor(WindowCommandStream commands) :
    IPacketProcessor<OpenWindowPacket>,
    IPacketProcessor<CloseWindowPacket>,
    IPacketProcessor<WindowUpdatePacket>
{
    public void Process(OpenWindowPacket packet) => commands.PublishOpenWindow(packet);

    public void Process(CloseWindowPacket packet) => commands.PublishCloseWindow(packet);

    public void Process(ModalWindowPacket packet) => commands.PublishModalWindow(packet);

    public void Process(WindowUpdatePacket packet) => commands.PublishElementUpdate(packet);
}
