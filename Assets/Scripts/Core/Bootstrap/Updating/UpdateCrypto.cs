#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kern.Core;

/// <summary>
/// Криптопримитивы апдейтера: SHA-256 файлов и проверка RSA-подписи манифеста.
/// ImportFromPem в Unity Mono нет, поэтому публичный ключ из PEM (SubjectPublicKeyInfo)
/// разбирается вручную минимальным DER-ридером и заводится через ImportParameters —
/// этот набор API в Mono стабильно работает (см. тест UpdateCryptoTests с фиксированным вектором).
/// </summary>
public static class UpdateCrypto
{
    public static string Sha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        incrementalHash.AppendData(data);
        if (!incrementalHash.TryGetHashAndReset(hashBytes, out int written) || written != 32)
        {
            throw new InvalidOperationException("SHA-256 не уложился в 32 байта.");
        }

        const string hex = "0123456789abcdef";
        Span<char> hexChars = stackalloc char[64];
        for (int i = 0; i < 32; i++)
        {
            byte b = hashBytes[i];
            hexChars[i * 2] = hex[b >> 4];
            hexChars[i * 2 + 1] = hex[b & 0x0F];
        }

        return new string(hexChars);
    }

    /// <summary>SHA-256 файла поточно (билдовые файлы бывают сотнями мегабайт — целиком не читаем).</summary>
    public static string Sha256HexOfFile(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            incrementalHash.AppendData(buffer, 0, read);
        }

        return BytesToHexLowercase(incrementalHash.GetHashAndReset());
    }

    private static string BytesToHexLowercase(byte[] bytes)
    {
        // Convert.ToHexString появился только в .NET 5; под netstandard2.1 — вручную.
        const string hex = "0123456789abcdef";
        var chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[i * 2] = hex[b >> 4];
            chars[i * 2 + 1] = hex[b & 0x0F];
        }

        return new string(chars);
    }

    /// <summary>
    /// Проверяет подпись байт манифеста. signatureBase64 — содержимое заголовка
    /// X-Update-Signature; publicKeyPem — зашитый в сборку ключ (ProjectRuntimeContracts.Update).
    /// Любая проблема (ключ/подпись кривые, подпись не сходится) — false.
    /// </summary>
    public static bool VerifyManifestSignature(byte[] manifestBytes, string? signatureBase64, string publicKeyPem)
    {
        if (manifestBytes is not { Length: > 0 } ||
            string.IsNullOrWhiteSpace(signatureBase64) ||
            string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(ParseSpkiPem(publicKeyPem));
            return rsa.VerifyData(
                manifestBytes,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Разбор PEM "-----BEGIN PUBLIC KEY-----" (SubjectPublicKeyInfo) в RSAParameters.</summary>
    public static RSAParameters ParseSpkiPem(string pem)
    {
        const string header = "-----BEGIN PUBLIC KEY-----";
        const string footer = "-----END PUBLIC KEY-----";
        int start = pem.IndexOf(header, StringComparison.Ordinal);
        int end = pem.IndexOf(footer, StringComparison.Ordinal);
        if (start < 0 || end < 0 || end <= start)
        {
            throw new FormatException("Строка не является PUBLIC KEY PEM.");
        }

        string base64 = pem.Substring(start + header.Length, end - start - header.Length);
        var builder = new StringBuilder(base64.Length);
        foreach (char c in base64)
        {
            // char.IsAsciiLetterOrDigit недоступен под netstandard2.1 — проверяем вручную.
            if (char.IsWhiteSpace(c) || IsAsciiLetterOrDigit(c) || c == '+' || c == '/' || c == '=')
            {
                builder.Append(c);
            }
        }

        byte[] der = Convert.FromBase64String(builder.ToString());
        var reader = new DerReader(der);
        reader.ReadTag(); // SEQUENCE (SubjectPublicKeyInfo)
        var spkiContents = reader.ReadConstructedContents();

        var inner = new DerReader(spkiContents);
        inner.ReadTag(); // SEQUENCE (AlgorithmIdentifier)
        inner.Skip(inner.ReadLength());
        inner.ReadTag(); // BIT STRING (публичный ключ)
        byte[] bitString = inner.ReadValue(inner.ReadLength());
        if (bitString.Length < 2 || bitString[0] != 0)
        {
            throw new FormatException("Неожиданный BIT STRING в SubjectPublicKeyInfo.");
        }

        var key = new DerReader(bitString.AsSpan(1).ToArray());
        key.ReadTag(); // SEQUENCE (RSAPublicKey)
        var integers = new DerReader(key.ReadConstructedContents());
        integers.ReadTag();
        byte[] modulus = TrimLeadingZero(integers.ReadValue(integers.ReadLength()));
        integers.ReadTag();
        byte[] exponent = TrimLeadingZero(integers.ReadValue(integers.ReadLength()));
        return new RSAParameters { Modulus = modulus, Exponent = exponent };
    }

    private static bool IsAsciiLetterOrDigit(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');

    private static byte[] TrimLeadingZero(byte[] value)
        => value.Length > 1 && value[0] == 0 ? value.AsSpan(1).ToArray() : value;

    /// <summary>Минимальный TLV-ридер DER: тег + длина (короткая/длинная форма) + значение.</summary>
    private ref struct DerReader
    {
        private readonly byte[] _data;
        private int _position;

        public DerReader(byte[] data)
        {
            _data = data;
            _position = 0;
        }

        public byte ReadTag()
        {
            if (_position >= _data.Length)
            {
                throw new FormatException("DER обрезан (тег).");
            }

            return _data[_position++];
        }

        public int ReadLength()
        {
            if (_position >= _data.Length)
            {
                throw new FormatException("DER обрезан (длина).");
            }

            byte first = _data[_position++];
            if (first < 0x80)
            {
                return first;
            }

            int byteCount = first & 0x7F;
            if (byteCount is 0 or > 4)
            {
                throw new FormatException("DER: неподдерживаемая длина.");
            }

            int length = 0;
            for (int i = 0; i < byteCount; i++)
            {
                if (_position >= _data.Length)
                {
                    throw new FormatException("DER обрезан (длинная длина).");
                }

                length = (length << 8) | _data[_position++];
            }

            return length;
        }

        public byte[] ReadValue(int length)
        {
            if (length < 0 || _position + length > _data.Length)
            {
                throw new FormatException("DER обрезан (значение).");
            }

            byte[] value = new byte[length];
            Array.Copy(_data, _position, value, 0, length);
            _position += length;
            return value;
        }

        public void Skip(int length)
        {
            if (length < 0 || _position + length > _data.Length)
            {
                throw new FormatException("DER обрезан (skip).");
            }

            _position += length;
        }

        public byte[] ReadConstructedContents() => ReadValue(ReadLength());
    }
}
