using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Application.Common.Authentication;

namespace Portfolio.Infrastructure.Authentication;

public sealed class RefreshTokenProtector(IOptions<RefreshTokenOptions> options)
    : IRefreshTokenProtector
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public GeneratedRefreshToken Generate(Guid tokenId)
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var encodedSecret = Base64UrlEncoder.Encode(secret);
        return new GeneratedRefreshToken(
            tokenId,
            $"{tokenId:D}.{encodedSecret}",
            Hash(secret));
    }

    public ParsedRefreshToken? ParseAndHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var separator = value.IndexOf('.');
        if (separator <= 0 || separator != value.LastIndexOf('.'))
        {
            return null;
        }

        if (!Guid.TryParseExact(value.AsSpan(0, separator), "D", out var tokenId))
        {
            return null;
        }

        try
        {
            var secret = Base64UrlEncoder.DecodeBytes(value[(separator + 1)..]);
            if (secret.Length != 32)
            {
                return null;
            }

            return new ParsedRefreshToken(tokenId, Hash(secret));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public bool FixedTimeEquals(
        ReadOnlySpan<byte> storedHash,
        ReadOnlySpan<byte> candidateHash) =>
        storedHash.Length == candidateHash.Length &&
        CryptographicOperations.FixedTimeEquals(storedHash, candidateHash);

    private byte[] Hash(ReadOnlySpan<byte> secret) =>
        HMACSHA256.HashData(_pepper, secret);
}
