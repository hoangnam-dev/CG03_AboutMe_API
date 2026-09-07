using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Application.Common.Authentication;

namespace Portfolio.Infrastructure.Authentication;

public sealed class SessionCsrfTokenService(IOptions<RefreshTokenOptions> options)
    : ICsrfTokenService
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public string Issue(Guid sessionId) => Base64UrlEncoder.Encode(Sign(sessionId));

    public bool Validate(Guid sessionId, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var supplied = Base64UrlEncoder.DecodeBytes(token);
            var expected = Sign(sessionId);
            return supplied.Length == expected.Length &&
                   CryptographicOperations.FixedTimeEquals(supplied, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private byte[] Sign(Guid sessionId)
    {
        var message = Encoding.UTF8.GetBytes($"csrf:{sessionId:N}");
        return HMACSHA256.HashData(_key, message);
    }
}
