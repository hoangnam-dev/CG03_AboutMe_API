namespace Portfolio.Application.Common.Exceptions;

public sealed class AuthenticationFailedException()
    : BaseApplicationException("Invalid email or password.");
