namespace Portfolio.Application.Common.Exceptions;

public sealed class ConflictException(string message) : BaseApplicationException(message);
