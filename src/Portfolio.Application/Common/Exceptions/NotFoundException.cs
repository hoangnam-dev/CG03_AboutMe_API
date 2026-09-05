namespace Portfolio.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : BaseApplicationException(message);
