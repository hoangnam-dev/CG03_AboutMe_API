namespace Portfolio.Application.Common.Exceptions;

public sealed class ServiceUnavailableException(string message) : BaseApplicationException(message);
