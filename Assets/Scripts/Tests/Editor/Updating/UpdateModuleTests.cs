#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kern.Core;
using NUnit.Framework;
using UnityEngine;

namespace Kern.Tests;

/// <summary>
/// Проверки криптографии апдейтера на фиксированном векторе: пара ключей и подпись
/// сгенерированы на .NET 9 тем же кодом, что на сервере (RSA-2048/SHA-256/PKCS#1
/// поверх точных байт манифеста). Тем самым проверяется и DER-парсер SPKI PEM —
/// путь, которым клиент (Unity Mono, без ImportFromPem) заводит публичный ключ.
/// </summary>
public class UpdateCryptoTests
{
    private const string TestPublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyRgo1KK3E/rSqZIxlTu0
uce5i/U/Fb4FiAdBCVDqpSkiOWA8WWpLP9OKJHhEXTPo2VYLqD176dKvPuXEJMtd
qV145A+RnSYBUJJ1GfLzc2hukyiiNKdyu9wyH0jJgiXXOWBQm12W/7ubTxweHHCo
h7thYveeQnbmVtTeldo6cCQcz8JHim7ykS1m+fuENUNvPtvnowwWdYA9KxqG851s
gBhOL+qp2AhZomJvwnbm2dbfR+7ph6ooGnT56uIQo56l82Z/A/7wwXCdZJJN7hS2
ZCcbUgwb8soi6pCuHnrBJdqLtG4M4B5iAzGhsM3EDbTpeDRMFCTl9SSNOO1cRzOB
IQIDAQAB
-----END PUBLIC KEY-----";

    private const string TestPayloadJson =
        "{\"schema\":1,\"channel\":\"stable\",\"version\":2,\"versionName\":\"0.02\"," +
        "\"releaseDate\":\"2026-10-18T12:00:00Z\",\"notes\":\"vector\",\"allowDowngrade\":false," +
        "\"files\":[{\"path\":\"Kern.exe\",\"sha256\":\"aa\",\"size\":3}],\"totalFiles\":1,\"totalSize\":3}";

    private const string TestSignatureBase64 =
        "PnZADrtJlIqGnL3VAkba5jUICIOWQV0ypCoEK45nNFb6kLS5G3A8tWWcectnhwVgPA8OkXCSfU9q5Zz3ZArhzTtL" +
        "+dvUpxFqKpGdlI/RzK1qXXL3pEATS81OIF0e7J9yF9HS6KUTYmy4NyPkrI0wi5uAJB1vzjVjQkf/861Xl9iqfkH" +
        "3tclfj+cQ6yHhF+Vg+Y9yDaxVtok04laFuIG93lYV683RGM2tOO61gWmrPXk8pSS353Ayxr6JsOo1BxGSpP5XOx" +
        "pmPUJgZgHb19oUtDByRNVp4dyU7wKyXz5BpEJkdr0Yo4og37oxexlsoM3Ta8/oo+FGJfMXOpCqreZQVA==";

    [Test]
    public void VerifyManifestSignature_KnownVector_Passes()
    {
        byte[] payload = Encoding.UTF8.GetBytes(TestPayloadJson);
        Assert.That(
            UpdateCrypto.VerifyManifestSignature(payload, TestSignatureBase64, TestPublicKeyPem),
            Is.True, "подпись из фиксированного вектора должна сходиться с зашитым разбором PEM");
    }

    [Test]
    public void VerifyManifestSignature_TamperedBody_Fails()
    {
        byte[] payload = Encoding.UTF8.GetBytes(TestPayloadJson);
        payload[^2] ^= 0x01;
        Assert.That(
            UpdateCrypto.VerifyManifestSignature(payload, TestSignatureBase64, TestPublicKeyPem),
            Is.False, "изменённое тело манифеста не должно проходить проверку");
    }

    [Test]
    public void VerifyManifestSignature_BadInputs_Fail()
    {
        byte[] payload = Encoding.UTF8.GetBytes(TestPayloadJson);
        Assert.That(UpdateCrypto.VerifyManifestSignature(payload, null, TestPublicKeyPem), Is.False);
        Assert.That(UpdateCrypto.VerifyManifestSignature(payload, "не base64!!", TestPublicKeyPem), Is.False);
        Assert.That(UpdateCrypto.VerifyManifestSignature(payload, TestSignatureBase64, "мусор"), Is.False);
        Assert.That(UpdateCrypto.VerifyManifestSignature(payload, TestSignatureBase64, string.Empty), Is.False);
    }

    [Test]
    public void ParseSpkiPem_Garbage_ThrowsFormat()
    {
        Assert.Throws<FormatException>(() => UpdateCrypto.ParseSpkiPem("-----BEGIN PUBLIC KEY-----\n!!!\n-----END PUBLIC KEY-----"));
        Assert.Throws<FormatException>(() => UpdateCrypto.ParseSpkiPem("no pem at all"));
    }

    [Test]
    public void Sha256Hex_MatchesKnownVector()
    {
        // SHA-256("abc") по RFC 4634 / NIST.
        Assert.That(
            UpdateCrypto.Sha256Hex(Encoding.ASCII.GetBytes("abc")),
            Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }
}

public class UpdateManifestTests
{
    [Test]
    public void Parse_ParsesCamelCaseContract()
    {
        const string json = "{\"schema\":1,\"channel\":\"stable\",\"version\":3,\"versionName\":\"0.04\"," +
            "\"releaseDate\":\"2026-10-18T12:00:00Z\",\"notes\":\"n\",\"allowDowngrade\":true," +
            "\"files\":[{\"path\":\"Kern.exe\",\"sha256\":\"ff\",\"size\":10}," +
            "{\"path\":\"Kern_Data\\\\level0\",\"sha256\":\"ee\",\"size\":20}]," +
            "\"totalFiles\":2,\"totalSize\":30}";

        UpdateManifest? manifest = UpdateManifest.Parse(Encoding.UTF8.GetBytes(json));

        Assert.That(manifest, Is.Not.Null);
        Assert.That(manifest!.Schema, Is.EqualTo(1));
        Assert.That(manifest.Channel, Is.EqualTo("stable"));
        Assert.That(manifest.Version, Is.EqualTo(3));
        Assert.That(manifest.AllowDowngrade, Is.True);
        Assert.That(manifest.Files, Has.Count.EqualTo(2));
        Assert.That(manifest.Files[0].Path, Is.EqualTo("Kern.exe"));
        Assert.That(manifest.Files[0].Sha256, Is.EqualTo("ff"));
        Assert.That(manifest.Files[0].Size, Is.EqualTo(10));
        Assert.That(manifest.Files[1].Path, Is.EqualTo("Kern_Data\\level0"));
        Assert.That(manifest.TotalSize, Is.EqualTo(30));
    }

    [Test]
    public void Parse_Garbage_ReturnsNull()
    {
        Assert.That(UpdateManifest.Parse(Encoding.UTF8.GetBytes("{не json")), Is.Null);
        Assert.That(UpdateManifest.Parse(Array.Empty<byte>()), Is.Null);
        Assert.That(UpdateManifest.Parse(null), Is.Null);
    }
}

public class UpdateDecisionTests
{
    private static UpdateManifest Manifest(int version, bool allowDowngrade = false) => new()
    {
        Schema = 1,
        Channel = "stable",
        Version = version,
        VersionName = $"v{version}",
        AllowDowngrade = allowDowngrade,
        Files = new List<UpdateManifestFile> { new() { Path = "Kern.exe", Sha256 = "aa", Size = 1 } },
        TotalFiles = 1,
        TotalSize = 1,
    };

    [Test]
    public void NewerVersion_TriggersUpdate()
    {
        // Сборка клиента — ClientVersion (1 в контракте); манифест на 2 версии вперёд.
        var decision = UpdateChecker.Decide(Manifest(ProjectRuntimeContracts.Networking.ClientVersion + 1), null);
        Assert.That(decision.Kind, Is.EqualTo(UpdateChecker.DecisionKind.UpdateAvailable));
    }

    [Test]
    public void SameVersion_IsUpToDate()
    {
        var decision = UpdateChecker.Decide(
            Manifest(ProjectRuntimeContracts.Networking.ClientVersion),
            new InstalledUpdateState { Version = ProjectRuntimeContracts.Networking.ClientVersion });
        Assert.That(decision.Kind, Is.EqualTo(UpdateChecker.DecisionKind.UpToDate));
    }

    [Test]
    public void OlderServer_IsIgnored()
    {
        var decision = UpdateChecker.Decide(
            Manifest(ProjectRuntimeContracts.Networking.ClientVersion - 1),
            new InstalledUpdateState { Version = ProjectRuntimeContracts.Networking.ClientVersion - 1 });
        Assert.That(decision.Kind, Is.EqualTo(UpdateChecker.DecisionKind.Ignore));
    }

    [Test]
    public void DowngradeWithoutFlag_IsIgnored()
    {
        var decision = UpdateChecker.Decide(
            Manifest(ProjectRuntimeContracts.Networking.ClientVersion - 1, allowDowngrade: false),
            new InstalledUpdateState { Version = ProjectRuntimeContracts.Networking.ClientVersion + 5 });
        Assert.That(decision.Kind, Is.EqualTo(UpdateChecker.DecisionKind.Ignore));
    }
}

public class UpdateDiffTests
{
    private static UpdateManifestFile File(string path, string sha) => new() { Path = path, Sha256 = sha, Size = 1 };

    [Test]
    public void EmptyInstalled_DownloadsEverything()
    {
        var manifest = new UpdateManifest
        {
            Files = new List<UpdateManifestFile> { File("a", "1"), File("b", "2") },
        };

        var missing = new UpdateDownloader().BuildMissingList(manifest, null);
        Assert.That(missing, Has.Count.EqualTo(2));
    }

    [Test]
    public void UnchangedFiles_AreSkipped()
    {
        var manifest = new UpdateManifest
        {
            Files = new List<UpdateManifestFile> { File("a", "1"), File("b", "2"), File("c", "3") },
        };
        var installed = new InstalledUpdateState
        {
            Version = 1,
            Files = new List<UpdateManifestFile> { File("a", "1"), File("b", "OLD"), File("d", "4") },
        };

        var missing = new UpdateDownloader().BuildMissingList(manifest, installed);
        // b изменился (хэш другой), c новый; d есть локально, но в манифесте отсутствует — не качаем.
        Assert.That(missing.ConvertAll(file => file.Path), Is.EquivalentTo(new[] { "b", "c" }));
    }
}

public class UpdateStateStoreTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Application.temporaryCachePath, "update-state-tests", Guid.NewGuid().ToString("N"));
        UpdateStateStore.RootOverride = _root;
    }

    [TearDown]
    public void TearDown()
    {
        UpdateStateStore.RootOverride = null;
        try { Directory.Delete(_root, recursive: true); } catch { /* временная папка */ }
    }

    [Test]
    public void SaveLoad_RoundTrips()
    {
        var state = new InstalledUpdateState
        {
            Version = 7,
            Files = new List<UpdateManifestFile> { new() { Path = "Kern.exe", Sha256 = "aa", Size = 3 } },
        };
        UpdateStateStore.Save(state);

        InstalledUpdateState? loaded = UpdateStateStore.Load();
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Version, Is.EqualTo(7));
        Assert.That(loaded.Files, Has.Count.EqualTo(1));
        Assert.That(loaded.Files[0].Path, Is.EqualTo("Kern.exe"));
    }

    [Test]
    public void Merge_NewVersionOverridesOldPaths()
    {
        UpdateStateStore.Save(new InstalledUpdateState
        {
            Version = 1,
            Files = new List<UpdateManifestFile> { new() { Path = "a", Sha256 = "old-a", Size = 1 }, new() { Path = "b", Sha256 = "old-b", Size = 1 } },
        });

        InstalledUpdateState merged = UpdateStateStore.Merge(2, new List<UpdateManifestFile>
        {
            new() { Path = "a", Sha256 = "new-a", Size = 2 },
            new() { Path = "c", Sha256 = "new-c", Size = 5 },
        });

        Assert.That(merged.Version, Is.EqualTo(2));
        Assert.That(merged.Files, Has.Count.EqualTo(3));
        Assert.That(merged.Files.Find(file => file.Path == "a")!.Sha256, Is.EqualTo("new-a"));
        Assert.That(merged.Files.Find(file => file.Path == "b")!.Sha256, Is.EqualTo("old-b"));
        Assert.That(merged.Files.Find(file => file.Path == "c")!.Sha256, Is.EqualTo("new-c"));
    }

    [Test]
    public void Reconcile_MatchingVersionKeepsState_ForeignVersionInvalidates()
    {
        UpdateStateStore.Save(new InstalledUpdateState { Version = 9, Files = new List<UpdateManifestFile>() });
        UpdateStateStore.ReconcileOnStartup(buildVersion: 9);
        Assert.That(UpdateStateStore.Load(), Is.Not.Null, "совпавшее состояние подтверждается");

        UpdateStateStore.Save(new InstalledUpdateState { Version = 9, Files = new List<UpdateManifestFile>() });
        UpdateStateStore.ReconcileOnStartup(buildVersion: 10);
        Assert.That(UpdateStateStore.Load(), Is.Null, "состояние про чужую версию инвалидируется");
    }
}

public class UpdateApplierScriptTests
{
    [Test]
    public void BuildScript_ContainsBackupCopyRollbackAndLaunch()
    {
        string script = UpdateApplier.BuildScript(
            42,
            installDirectory: @"C:\Games\Kern Build",
            stagingDirectory: @"C:\Users\o'brien\AppData\Update\staging\v42",
            files: new List<UpdateManifestFile>
            {
                new() { Path = "Kern.exe", Sha256 = "aa", Size = 1 },
                new() { Path = @"Kern_Data\level0", Sha256 = "bb", Size = 2 },
            });

        StringAssert.Contains("'C:\\Games\\Kern Build'", script);
        // Апостроф в пути экранируется удвоением — иначе PowerShell не соберёт литерал.
        StringAssert.Contains("o''brien", script);
        StringAssert.Contains(".update_backup", script);
        StringAssert.Contains("Get-Process", script);
        StringAssert.Contains("Move-Item -Force $src $dst", script);
        StringAssert.Contains("applied-version.txt", script);
        StringAssert.Contains("'42'", script);
        StringAssert.Contains("Start-Process", script);
        StringAssert.Contains("} catch {", script);
        StringAssert.Contains("Kern_Data\\level0", script);
    }
}
