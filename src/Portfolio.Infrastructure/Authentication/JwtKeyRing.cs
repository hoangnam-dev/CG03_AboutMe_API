using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Portfolio.Infrastructure.Authentication;

public sealed class JwtKeyRing : IDisposable
{
    private readonly List<X509Certificate2> _certificates = [];

    public JwtKeyRing(IOptions<JwtOptions> options)
    {
        var settings = options.Value;
        var signingCertificate = X509CertificateLoader.LoadPkcs12FromFile(
            settings.SigningCertificatePath,
            settings.SigningCertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
        using (var signingRsa = signingCertificate.GetRSAPrivateKey())
        {
            if (signingRsa is null || signingRsa.KeySize < 2048)
            {
                signingCertificate.Dispose();
                throw new InvalidOperationException(
                    "The JWT signing certificate must contain an RSA private key of at least 2048 bits.");
            }
        }

        _certificates.Add(signingCertificate);

        var signingKey = new X509SecurityKey(signingCertificate)
        {
            KeyId = settings.ActiveKeyId,
        };
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var validationKeys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal)
        {
            [settings.ActiveKeyId] = signingKey,
        };
        foreach (var (keyId, path) in settings.ValidationCertificatePaths)
        {
            if (validationKeys.ContainsKey(keyId))
            {
                continue;
            }

            var certificate = X509CertificateLoader.LoadCertificateFromFile(path);
            using (var validationRsa = certificate.GetRSAPublicKey())
            {
                if (validationRsa is null || validationRsa.KeySize < 2048)
                {
                    certificate.Dispose();
                    throw new InvalidOperationException(
                        "Every JWT validation certificate must contain an RSA public key of at least 2048 bits.");
                }
            }

            _certificates.Add(certificate);
            validationKeys[keyId] = new X509SecurityKey(certificate) { KeyId = keyId };
        }

        ValidationKeys = validationKeys;
    }

    private JwtKeyRing(X509Certificate2 certificate, string keyId)
    {
        _certificates.Add(certificate);
        var key = new X509SecurityKey(certificate) { KeyId = keyId };
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        ValidationKeys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal)
        {
            [keyId] = key,
        };
    }

    public static JwtKeyRing CreateEphemeral()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=portfolio-development-ephemeral",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        return new JwtKeyRing(certificate, "development-ephemeral");
    }

    public SigningCredentials SigningCredentials { get; }
    public IReadOnlyDictionary<string, SecurityKey> ValidationKeys { get; }

    public void Dispose()
    {
        foreach (var certificate in _certificates)
        {
            certificate.Dispose();
        }
    }
}
