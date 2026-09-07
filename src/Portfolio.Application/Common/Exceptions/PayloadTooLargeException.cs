namespace Portfolio.Application.Common.Exceptions;

public sealed class PayloadTooLargeException(string message) : BaseApplicationException(message);
