using System.Security.Cryptography;
using System.Text;

namespace Linkpearl.Net;

// Linkpearl room seal: P-256 ECDH wrap + AES-GCM bodies. Prefix lp1. so other public-key
// encodings are refused instead of being treated as plaintext. Pearlgate stores these blobs
// opaquely (EncVersion, CommitmentTag, /keys).
internal static class ChatSeal
{
    public const string Prefix = "lp1.";
    public const string ClosedBody = "Sealed — this handset does not have the room key.";
    public const string UnknownBody = "Sealed — this handset cannot open this format.";

    private static readonly byte[] WrapInfo = "linkpearl-wrap-v1"u8.ToArray();
    private static readonly byte[] BodyInfo = "linkpearl-body-v1"u8.ToArray();

    public static bool IsSealedBlob(string? value) =>
        value is not null &&
        value.Length > Prefix.Length &&
        value.StartsWith(Prefix, StringComparison.Ordinal);

    public static Identity CreateIdentity()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdh.ExportParameters(true);
        return new Identity(Prefix + Encode(PublicPoint(parameters)), Encode(parameters.D ?? []));
    }

    public static bool TryCreateIdentity(out Identity identity)
    {
        try
        {
            identity = CreateIdentity();
            return identity.PublicKey.Length > 0 && identity.PrivateD.Length > 0;
        }
        catch (CryptographicException)
        {
            identity = default;
            return false;
        }
    }

    public static bool TryParsePublic(string? packed, out byte[] x, out byte[] y)
    {
        x = [];
        y = [];
        if (!TryDecode(packed, out var bytes) || bytes.Length != 65 || bytes[0] != 0x04)
        {
            return false;
        }

        x = bytes.AsSpan(1, 32).ToArray();
        y = bytes.AsSpan(33, 32).ToArray();
        return true;
    }

    public static string WrapRoomKey(string recipientPublic, byte[] roomKey)
    {
        if (roomKey.Length != 32)
        {
            throw new ArgumentOutOfRangeException(nameof(roomKey));
        }

        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var recipient = ImportPublic(recipientPublic);
        var wrapKey = Derive(ephemeral, recipient, WrapInfo);
        var sealedKey = Seal(wrapKey, roomKey);
        var eph = PublicPoint(ephemeral.ExportParameters(false));
        var packed = new byte[65 + sealedKey.Length];
        eph.CopyTo(packed, 0);
        sealedKey.CopyTo(packed, 65);
        CryptographicOperations.ZeroMemory(wrapKey);
        return Prefix + Encode(packed);
    }

    public static bool TryUnwrapRoomKey(string wrapped, Identity identity, out byte[] roomKey)
    {
        roomKey = [];
        if (!TryDecode(wrapped, out var bytes) || bytes.Length < 65 + 12 + 16 + 32)
        {
            return false;
        }

        try
        {
            using var self = ImportPrivate(identity);
            using var ephemeral = ImportPublicRaw(bytes.AsSpan(0, 65));
            var wrapKey = Derive(self, ephemeral, WrapInfo);
            var opened = Open(wrapKey, bytes.AsSpan(65));
            CryptographicOperations.ZeroMemory(wrapKey);
            if (opened.Length != 32)
            {
                return false;
            }

            roomKey = opened;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static (string Body, string Commitment) SealBody(byte[] roomKey, string plaintext)
    {
        var utf8 = Encoding.UTF8.GetBytes(plaintext);
        var body = Prefix + Encode(Seal(roomKey, utf8));
        var tag = Encode(HMACSHA256.HashData(roomKey, utf8));
        return (body, tag);
    }

    public static bool TryOpenBody(byte[] roomKey, string body, out string plaintext)
    {
        plaintext = string.Empty;
        if (!TryDecode(body, out var bytes))
        {
            return false;
        }

        try
        {
            var utf8 = Open(roomKey, bytes);
            plaintext = Encoding.UTF8.GetString(utf8);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string HonestBody(string? body, int encVersion)
    {
        if (encVersion <= 0)
        {
            return body ?? string.Empty;
        }

        return IsSealedBlob(body) ? ClosedBody : UnknownBody;
    }

    private static byte[] Derive(ECDiffieHellman local, ECDiffieHellman remote, byte[] info)
    {
        var secret = local.DeriveRawSecretAgreement(remote.PublicKey);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, salt: null, info);
        CryptographicOperations.ZeroMemory(secret);
        return key;
    }

    private static byte[] Seal(byte[] key, ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag, BodyInfo);
        var packed = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(packed, 0);
        ciphertext.CopyTo(packed, nonce.Length);
        tag.CopyTo(packed, nonce.Length + ciphertext.Length);
        return packed;
    }

    private static byte[] Open(byte[] key, ReadOnlySpan<byte> packed)
    {
        if (packed.Length < 12 + 16)
        {
            throw new CryptographicException();
        }

        var nonce = packed[..12];
        var tag = packed[^16..];
        var ciphertext = packed[12..^16];
        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, 16);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext, BodyInfo);
        return plaintext;
    }

    private static ECDiffieHellman ImportPublic(string packed)
    {
        if (!TryParsePublic(packed, out var x, out var y))
        {
            throw new FormatException();
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = { X = x, Y = y },
        });
    }

    private static ECDiffieHellman ImportPublicRaw(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 65 || bytes[0] != 0x04)
        {
            throw new FormatException();
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = { X = bytes.Slice(1, 32).ToArray(), Y = bytes.Slice(33, 32).ToArray() },
        });
    }

    private static ECDiffieHellman ImportPrivate(Identity identity)
    {
        if (!TryParsePublic(identity.PublicKey, out var x, out var y) || !TryDecodeRaw(identity.PrivateD, out var d) ||
            d.Length == 0)
        {
            throw new FormatException();
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = d,
            Q = { X = x, Y = y },
        });
    }

    private static byte[] PublicPoint(ECParameters parameters)
    {
        var x = parameters.Q.X ?? [];
        var y = parameters.Q.Y ?? [];
        var packed = new byte[65];
        packed[0] = 0x04;
        x.CopyTo(packed, 1 + (32 - x.Length));
        y.CopyTo(packed, 33 + (32 - y.Length));
        return packed;
    }

    private static bool TryDecode(string? packed, out byte[] bytes)
    {
        bytes = [];
        if (!IsSealedBlob(packed))
        {
            return false;
        }

        return TryDecodeRaw(packed![Prefix.Length..], out bytes);
    }

    private static bool TryDecodeRaw(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(Pad(value.Replace('-', '+').Replace('_', '/')));
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Pad(string value)
    {
        var pad = (4 - (value.Length % 4)) % 4;
        return pad == 0 ? value : value + new string('=', pad);
    }

    internal readonly record struct Identity(string PublicKey, string PrivateD);
}
