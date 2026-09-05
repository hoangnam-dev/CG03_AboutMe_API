namespace Portfolio.Application.Common.Exceptions;

public sealed class ForbiddenException(string message) : BaseApplicationException(message);
