namespace Portfolio.Application.Common.Exceptions;

public sealed class ValidationException(
    string message,
    IReadOnlyDictionary<string, string[]> errors)
    : BaseApplicationException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
