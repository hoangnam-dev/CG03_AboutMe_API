namespace Portfolio.Application.Common.Authentication;

public sealed record GeneratedRefreshToken(
    Guid TokenId,
    string Value,
    byte[] SecretHash);

public sealed record ParsedRefreshToken(Guid TokenId, byte[] SecretHash);

public interface IRefreshTokenProtector
{
    GeneratedRefreshToken Generate(Guid tokenId);
    ParsedRefreshToken? ParseAndHash(string value);
    bool FixedTimeEquals(ReadOnlySpan<byte> storedHash, ReadOnlySpan<byte> candidateHash);
}
