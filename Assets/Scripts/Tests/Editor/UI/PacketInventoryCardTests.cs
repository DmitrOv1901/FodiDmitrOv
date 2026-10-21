#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kern;
using Kern.Core;
using Kern.Core.Interfaces;
using Kern.Networking.Processors;
using Kern.UI;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.GUI.Components;
using MinesServer.Networking.Server.Packets.GUI.Components.Visual;
using MinesServer.Networking.Shared.Packets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kern.Tests.UI;

/// <summary>
/// Карточки инвентаря легаси-окон (вкладка «Ордеры» маркета): мост помечает
/// TextPacket ключами PacketUI.InventoryCard/ItemId, клиент собирает из них
/// кнопку-карточку с иконкой предмета и статусом, а подряд идущие карточки
/// группируются в сетку-перенос. Тесты фиксируют контракт меток и разметку.
/// </summary>
[TestFixture]
public class PacketInventoryCardTests
{
    private sealed class ThrowingAssetLoader : IAssetLoader
    {
        public int PendingAssetCount => 0;

        public int QueuedAssetCount => 0;

        public bool IsKnownMissing(string filename) => false;

        public UniTask<byte[]?> GetAssetBytesAsync(
            string filename,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = ProjectRuntimeContracts.AssetStreaming.AssetRequestTimeoutSeconds) =>
            throw new InvalidOperationException("Card build must not load server assets.");

        public UniTask<string> GetAssetPathAsync(
            string filename,
            CancellationToken cancellationToken = default,
            int timeoutSeconds = ProjectRuntimeContracts.AssetStreaming.AssetRequestTimeoutSeconds) =>
            throw new InvalidOperationException("Card build must not load server assets.");

        public UniTask<Texture2D?> GetTextureAsync(string filename, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Card build must not load server assets.");

        public UniTask<AnimatedSpriteData> GetAnimatedSpritesAsync(
            string filename,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Card build must not load server assets.");
    }

    private sealed class ThrowingOperations : IAsyncOperationSupervisor
    {
        public int ActiveCount => 0;

        public void Run(string operationName, Func<CancellationToken, UniTask> operation) =>
            throw new InvalidOperationException("Card build must not schedule operations.");

        public UniTask StopAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
    }

    private sealed class ThrowingWebLoader : IWebAssetLoader
    {
        public UniTask<Texture2D?> GetTextureAsync(string url, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Card build must not load web assets.");
    }

    private sealed class RecordingCatalog(Texture2D? icon) : IItemCatalog
    {
        public List<ItemType> Requested { get; } = [];

        public IEnumerable<ItemType> AllTypes => [];

        public string GetName(ItemType type) => string.Empty;

        public string GetDescription(ItemType type) => string.Empty;

        public Texture2D? GetIcon(ItemType type)
        {
            Requested.Add(type);
            return icon;
        }
    }

    private static PacketUIBuilder Builder(RecordingCatalog catalog) =>
        new(new ThrowingAssetLoader(), new ThrowingOperations(), new ThrowingWebLoader(), catalog);

    private static TextPacket CardPacket(
        string itemId,
        string text = "1.2KK\nНет спроса",
        string onClickContext = ".")
    {
        return PacketTestFactory.Text(
            text,
            onClickContext,
            new StringPairPacket("PacketUI.InventoryCard", "95"),
            new StringPairPacket("PacketUI.ItemId", itemId));
    }

    [Test]
    public void CardWithAction_BuildsButtonWithIconAndStatus()
    {
        // Texture2D не IDisposable: редактор подчистит его при domain reload.
        var icon = new Texture2D(2, 2);
        var catalog = new RecordingCatalog(icon);
        TextPacket packet = CardPacket(((int)ItemType.GeoRed).ToString());

        VisualElement element = Builder(catalog).Build(packet);

        Assert.That(element, Is.InstanceOf<Button>());
        Assert.That(element.ClassListContains("packet-inventory-card"), Is.True);
        Assert.That(element.style.minHeight.value, Is.EqualTo(95f));

        VisualElement iconElement = element.Q(className: "packet-inventory-card__icon")!;
        Assert.That(iconElement, Is.Not.Null);
        Assert.That(iconElement.style.backgroundImage.value.texture, Is.SameAs(icon));
        Assert.That(iconElement.pickingMode, Is.EqualTo(PickingMode.Ignore));

        // Каждая строка статуса — своя метка (цвет строки красит сервер).
        var statusLines = element.Query<Label>(className: "packet-inventory-card__label").ToList();
        Assert.That(statusLines, Has.Count.EqualTo(2));
        Assert.That(statusLines[0].text, Is.EqualTo("1.2KK"));
        Assert.That(statusLines[1].text, Is.EqualTo("Нет спроса"));

        Assert.That(catalog.Requested, Is.EqualTo(new[] { ItemType.GeoRed }));
    }

    [Test]
    public void ColoredStatusLines_ParseLegacyColorTags()
    {
        var catalog = new RecordingCatalog(icon: null);
        // Формат статуса маркета: цена продажи и цена скупки в legacy-тегах.
        TextPacket packet = CardPacket(
            "1",
            text: "<color=lime>1.2KK</color>\n<color=#FF3333>Нет спроса</color>");

        VisualElement element = Builder(catalog).Build(packet);

        var statusLines = element.Query<Label>(className: "packet-inventory-card__label").ToList();
        Assert.That(statusLines, Has.Count.EqualTo(2));
        Assert.That(statusLines[0].text, Is.EqualTo("1.2KK"));
        Color sell = statusLines[0].style.color.value;
        Assert.That((sell.r, sell.g, sell.b), Is.EqualTo((0f, 1f, 0f)));
        Assert.That(statusLines[1].text, Is.EqualTo("Нет спроса"));
        Color noDemand = statusLines[1].style.color.value;
        Assert.That((noDemand.r, noDemand.g, noDemand.b), Is.EqualTo((1f, 0.2f, 0.2f)));
    }

    [Test]
    public void EmptySlotCard_IsPlainElementNotButton()
    {
        var catalog = new RecordingCatalog(icon: null);
        TextPacket packet = CardPacket("-1", text: "—", onClickContext: "");

        VisualElement element = Builder(catalog).Build(packet);

        Assert.That(element, Is.Not.InstanceOf<Button>());
        Assert.That(element.ClassListContains("packet-inventory-card"), Is.True);
        Assert.That(
            element.Q<Label>(className: "packet-inventory-card__label")!.text,
            Is.EqualTo("—"));
        Assert.That(catalog.Requested, Is.Empty);
    }

    [Test]
    public void SkillIdCard_DoesNotAskItemCatalog()
    {
        var catalog = new RecordingCatalog(icon: null);
        // Инвентарь навыков кладёт в Id код со смещением 2000 — локального
        // спрайта предметов для него нет, как и в правиле старого клиента.
        TextPacket packet = CardPacket("2001");

        VisualElement element = Builder(catalog).Build(packet);

        Assert.That(catalog.Requested, Is.Empty);
        Assert.That(
            element.Q(className: "packet-inventory-card__icon")!.style.backgroundImage.value.texture,
            Is.Null);
    }

    [Test]
    public void ConsecutiveCards_GroupIntoSingleWrapGrid()
    {
        var catalog = new RecordingCatalog(icon: null);
        var cards = new List<IGUIComponentPacket>
        {
            CardPacket("1"),
            CardPacket("2"),
            PacketTestFactory.Text("хвост", ""),
        };

        VisualElement scrollView = Builder(catalog).Build(PacketTestFactory.Scroll([.. cards]));
        VisualElement content = ((ScrollView)scrollView).contentContainer;

        Assert.That(content.childCount, Is.EqualTo(2));
        VisualElement grid = content[0];
        Assert.That(grid.ClassListContains("packet-inventory-grid"), Is.True);
        Assert.That(grid.childCount, Is.EqualTo(2));
        Assert.That(grid[0].ClassListContains("packet-inventory-card"), Is.True);
        Assert.That(grid[1].ClassListContains("packet-inventory-card"), Is.True);
        Assert.That(content[1], Is.InstanceOf<Label>());
    }
}
