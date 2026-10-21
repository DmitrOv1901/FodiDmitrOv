#nullable enable

using System;
using MinesServer.Networking.Client.Packets.Actions;
using MinesServer.Networking.Client.Packets.Chat;
using MinesServer.Networking.Client.Packets.GUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.UI.HUD.Player.View;

public sealed class PlayerHUDPopups
{
    // Серверного GUI-пакета «открыть консоль» в протоколе нет: сервер
    // (Session.LocalChatHandler) перехватывает локальное сообщение "console",
    // не рассылая его другим игрокам, и открывает MConsole.OpenWindow -
    // серверное окно с табами ОБЩИЕ/МОДЕРАТОР/АДМИН по правам игрока.
    private const string MConsoleLocalChatCommand = "console";

    private readonly Action<SuicidePacket> _sendSuicide;
    private readonly Action<OpenHelpClickPacket> _sendHelpRequest;
    private readonly Action<BldsClickPacket> _sendBldsRequest;
    private readonly Action<SendLocalChatMessagePacket> _sendLocalChat;
    private VisualElement? _respawnPopup;
    private VisualElement? _buildingsPopup;

    public PlayerHUDPopups(Action<SuicidePacket> sendSuicide, Action<OpenHelpClickPacket> sendHelpRequest, Action<BldsClickPacket> sendBldsRequest, Action<SendLocalChatMessagePacket> sendLocalChat)
    {
        _sendSuicide = sendSuicide;
        _sendHelpRequest = sendHelpRequest;
        _sendBldsRequest = sendBldsRequest;
        _sendLocalChat = sendLocalChat;
    }

    public void Initialize(VisualElement root)
    {
        _respawnPopup = root.Q<VisualElement>("RespawnPopup") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnPopup is missing from PlayerHUD.uxml.");
        Button respawnConfirm = root.Q<Button>("RespawnConfirmButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnConfirmButton is missing from PlayerHUD.uxml.");
        respawnConfirm.clicked += () =>
        {
            _sendSuicide(new SuicidePacket());
            _respawnPopup.style.display = DisplayStyle.None;
        };
        Button respawnCancel = root.Q<Button>("RespawnCancelButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnCancelButton is missing from PlayerHUD.uxml.");
        respawnCancel.clicked += () => _respawnPopup.style.display = DisplayStyle.None;
        Button respawnButton = root.Q<Button>("RespawnButton") ??
            throw new InvalidOperationException("[PlayerHUD] RespawnButton is missing from PlayerHUD.uxml.");
        respawnButton.clicked += () => _respawnPopup.style.display = DisplayStyle.Flex;

        _buildingsPopup = root.Q<VisualElement>("BuildingsPopup") ??
            throw new InvalidOperationException("[PlayerHUD] BuildingsPopup is missing from PlayerHUD.uxml.");
        Button buildingsClose = root.Q<Button>("BuildingsCloseButton") ??
            throw new InvalidOperationException("[PlayerHUD] BuildingsCloseButton is missing from PlayerHUD.uxml.");
        buildingsClose.clicked += () => _buildingsPopup.style.display = DisplayStyle.None;
        // «Мои здания» — серверное окно: кнопка шлёт BldsClickPacket, сервер
        // (Session.BldsHandler → Player.PlayerBuildingsWindow) отвечает окном
        // паков через ServerWindowPresenter. Локальный BuildingsPopup из UXML
        // остаётся заглушкой и больше не открывается.
        Button buildingsButton = root.Q<Button>("BuildingsButton") ??
            throw new InvalidOperationException("[PlayerHUD] BuildingsButton is missing from PlayerHUD.uxml.");
        buildingsButton.clicked += () => _sendBldsRequest(new BldsClickPacket());

        // FAQ — серверная страница: кнопка шлёт OpenHelpClickPacket, сервер
        // (Session.HelpHandler → Settings.FAQWindow) отвечает модальным окном
        // FAQ через ServerWindowPresenter. Локальный FaqPopup из UXML больше
        // не используется.
        Button faqButton = root.Q<Button>("FaqButton") ??
            throw new InvalidOperationException("[PlayerHUD] FaqButton is missing from PlayerHUD.uxml.");
        faqButton.clicked += () => _sendHelpRequest(new OpenHelpClickPacket());

        // MConsole — серверная консоль: открытие через локальный чат "console"
        // (см. MConsoleLocalChatCommand). Окно рендерится существующим
        // ServerWindowPresenter, вкладки собирает сервер по правам игрока.
        Button mconsoleButton = root.Q<Button>("MConsoleButton") ??
            throw new InvalidOperationException("[PlayerHUD] MConsoleButton is missing from PlayerHUD.uxml.");
        mconsoleButton.clicked += () =>
        {
            Debug.Log("[MConsole] Клик: отправляю SendLocalChatMessagePacket(\"console\")");
            _sendLocalChat(new SendLocalChatMessagePacket(MConsoleLocalChatCommand));
        };
    }
}
