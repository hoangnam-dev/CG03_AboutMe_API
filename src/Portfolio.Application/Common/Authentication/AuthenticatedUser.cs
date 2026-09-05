namespace Portfolio.Application.Common.Authentication;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    IReadOnlyCollection<string> Roles);
