using Portfolio.Application.Common.Authentication;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableAccessSessionValidator : IAccessSessionValidator
{
    public Task<bool> IsValidAsync(CurrentAuthSession current, CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
