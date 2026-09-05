namespace Portfolio.Application.Common.Authentication;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
