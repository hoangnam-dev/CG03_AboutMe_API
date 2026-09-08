using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Storage;
using Xunit;

namespace Portfolio.UnitTests.Certificates;

public sealed class CertificateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateRejectsExpirationBeforeIssueDate()
    {
        var service = CreateService(new RepositoryStub(), new StorageStub());

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateCertificateAsync(
            Request() with { ExpirationDate = new DateOnly(2025, 12, 31) },
            TestContext.Current.CancellationToken));

        Assert.Contains("on or after", error.Errors["expirationDate"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishingRequiresBothTranslations()
    {
        var service = CreateService(new RepositoryStub(), new StorageStub());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCertificateAsync(
            Request() with
            {
                Translations = new Dictionary<string, CertificateTranslationRequest>
                {
                    ["en"] = new("Cloud Certificate"),
                },
            },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRejectsDuplicateAndUnknownTechnologies()
    {
        var id = Guid.NewGuid();
        var service = CreateService(new RepositoryStub(), new StorageStub());
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateCertificateAsync(
            Request() with { TechnologyIds = [id, id] },
            TestContext.Current.CancellationToken));

        var repository = new RepositoryStub { TechnologiesExist = false };
        service = CreateService(repository, new StorageStub());
        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateCertificateAsync(
            Request(), TestContext.Current.CancellationToken));

        Assert.Contains("exist", error.Errors["technologyIds"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicResponseOmitsHiddenCredentialAndComputesExpired()
    {
        var projection = new CertificatePublicProjection(
            Guid.NewGuid(), "Issuer", new DateOnly(2024, 1, 1), new DateOnly(2026, 9, 7),
            "SECRET", false, "https://example.com/verify", 0, "Cloud Certificate", null, null, []);
        var service = CreateService(
            new RepositoryStub { PublicItems = [projection] }, new StorageStub());

        var result = Assert.Single(await service.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken));

        Assert.Null(result.CredentialId);
        Assert.True(result.IsExpired);
        Assert.Null(result.DownloadUrl);
    }

    [Fact]
    public async Task PublicEvidenceUsesFiveMinuteSignedUrl()
    {
        var projection = new CertificatePublicProjection(
            Guid.NewGuid(), "Issuer", new DateOnly(2026, 1, 1), null,
            null, false, null, 0, "Cloud Certificate", "certificates/id/evidence.pdf", null, []);
        var storage = new StorageStub();
        var service = CreateService(new RepositoryStub { PublicItems = [projection] }, storage);

        var result = Assert.Single(await service.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken));

        Assert.Equal("https://storage.example/signed", result.DownloadUrl?.AbsoluteUri);
        Assert.Equal(Now.AddMinutes(5), result.DownloadUrlExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(5), storage.SignedLifetime);
    }

    [Fact]
    public async Task ReplaceEvidenceCommitsNewKeyBeforeDeletingOldObject()
    {
        var certificate = CertificateWithEvidence("certificates/id/old.pdf");
        var events = new List<string>();
        var repository = new RepositoryStub { Existing = certificate, Events = events };
        var storage = new StorageStub { Events = events };
        var service = CreateService(repository, storage);
        await using var content = new MemoryStream("%PDF-test"u8.ToArray());

        var response = await service.UploadEvidenceAsync(
            certificate.Id,
            new CertificateEvidenceUpload(content, "proof.pdf", "application/pdf", content.Length),
            TestContext.Current.CancellationToken);

        Assert.Equal(["upload", "save", "delete:certificates/id/old.pdf", "sign"], events);
        Assert.StartsWith($"certificates/{certificate.Id}/", certificate.FileUrl, StringComparison.Ordinal);
        Assert.Null(certificate.ImageUrl);
        Assert.Equal("https://storage.example/signed", response.DownloadUrl?.AbsoluteUri);
    }

    [Fact]
    public async Task ReplaceEvidenceDeletesNewObjectWhenPersistenceFails()
    {
        var certificate = CertificateWithEvidence("certificates/id/old.pdf");
        var storage = new StorageStub();
        var service = CreateService(
            new RepositoryStub { Existing = certificate, SaveException = new InvalidOperationException("db") },
            storage);
        await using var content = new MemoryStream("%PDF-test"u8.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadEvidenceAsync(
            certificate.Id,
            new CertificateEvidenceUpload(content, "proof.pdf", "application/pdf", content.Length),
            TestContext.Current.CancellationToken));

        Assert.Single(storage.DeletedKeys);
        Assert.NotEqual("certificates/id/old.pdf", storage.DeletedKeys[0]);
    }

    [Fact]
    public async Task UploadingPdfPreservesExistingImageEvidence()
    {
        var certificate = CertificateWithEvidence("certificates/id/old.pdf");
        certificate.ImageUrl = "certificates/id/badge.png";
        var storage = new StorageStub();
        var service = CreateService(new RepositoryStub { Existing = certificate }, storage);
        await using var content = new MemoryStream("%PDF-test"u8.ToArray());

        await service.UploadEvidenceAsync(
            certificate.Id,
            new CertificateEvidenceUpload(content, "proof.pdf", "application/pdf", content.Length),
            TestContext.Current.CancellationToken);

        Assert.Equal("certificates/id/badge.png", certificate.ImageUrl);
        Assert.DoesNotContain("certificates/id/badge.png", storage.DeletedKeys);
    }

    [Fact]
    public async Task AdminResponseSignsFileAndImageEvidenceIndependently()
    {
        var certificate = CertificateWithEvidence("certificates/id/proof.pdf");
        certificate.ImageUrl = "certificates/id/badge.png";
        var storage = new StorageStub();
        var service = CreateService(new RepositoryStub { Existing = certificate }, storage);

        var result = await service.GetCertificateAsync(
            certificate.Id, TestContext.Current.CancellationToken);

        Assert.Equal("https://storage.example/signed", result.DownloadUrl?.AbsoluteUri);
        Assert.Equal("https://storage.example/signed", result.ImageUrl?.AbsoluteUri);
        Assert.Equal(
            ["certificates/id/proof.pdf", "certificates/id/badge.png"],
            storage.SignedKeys);
    }

    private static CertificateService CreateService(
        ICertificateRepository repository,
        IFileStorage storage) =>
        new(
            repository,
            storage,
            new CertificateEvidenceSettings("certificate-files", 10 * 1024 * 1024, TimeSpan.FromMinutes(5)),
            new FixedTimeProvider(Now),
            NullLogger<CertificateService>.Instance);

    private static CertificateWriteRequest Request() => new(
        "Example Authority",
        new DateOnly(2026, 1, 1),
        null,
        "ABC-123",
        false,
        "https://example.com/verify/ABC-123",
        0,
        true,
        new Dictionary<string, CertificateTranslationRequest>
        {
            ["en"] = new("Cloud Certificate"),
            ["vi"] = new("Chung chi Cloud"),
        },
        [Guid.NewGuid()]);

    private static Certificate CertificateWithEvidence(string key)
    {
        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            Issuer = "Issuer",
            IssuedDate = new DateOnly(2026, 1, 1),
            FileUrl = key,
        };
        certificate.Translations.Add(new CertificateTranslation
        {
            CertificateId = certificate.Id,
            LocaleCode = "en",
            Name = "Cloud Certificate",
        });
        return certificate;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RepositoryStub : ICertificateRepository
    {
        public bool TechnologiesExist { get; init; } = true;
        public Certificate? Existing { get; init; }
        public IReadOnlyList<CertificatePublicProjection>? PublicItems { get; init; } = [];
        public Exception? SaveException { get; init; }
        public List<string>? Events { get; init; }

        public Task<IReadOnlyList<CertificatePublicProjection>?> GetPublicAsync(string slug, string locale, CancellationToken token) => Task.FromResult(PublicItems);
        public Task<CertificateEntityPage> GetCertificatesAsync(CertificateAdminQuery query, CancellationToken token) => Task.FromResult(new CertificateEntityPage([], 0, query.Page, query.PageSize));
        public Task<Certificate?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult(Existing?.Id == id ? Existing : null);
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(TechnologiesExist);
        public Task AddAsync(Certificate certificate, CancellationToken token) => Task.CompletedTask;
        public void Remove(Certificate certificate) { }
        public Task SaveChangesAsync(CancellationToken token)
        {
            Events?.Add("save");
            return SaveException is null ? Task.CompletedTask : Task.FromException(SaveException);
        }
    }

    private sealed class StorageStub : IFileStorage
    {
        public List<string>? Events { get; init; }
        public List<string> DeletedKeys { get; } = [];
        public List<string> SignedKeys { get; } = [];
        public TimeSpan? SignedLifetime { get; private set; }

        public Task<StorageObject> UploadAsync(StorageUpload upload, CancellationToken token)
        {
            Events?.Add("upload");
            return Task.FromResult(new StorageObject(upload.Bucket, upload.ObjectKey));
        }

        public Task DeleteIfExistsAsync(string bucket, string objectKey, CancellationToken token)
        {
            Events?.Add($"delete:{objectKey}");
            DeletedKeys.Add(objectKey);
            return Task.CompletedTask;
        }

        public Uri GetPublicReadUrl(string bucket, string objectKey) => throw new NotSupportedException();

        public Task<Uri> CreateSignedReadUrlAsync(string bucket, string objectKey, TimeSpan lifetime, CancellationToken token)
        {
            Events?.Add("sign");
            SignedKeys.Add(objectKey);
            SignedLifetime = lifetime;
            return Task.FromResult(new Uri("https://storage.example/signed"));
        }
    }
}
