namespace Portfolio.Application.Common.Authentication;

public interface IAccessSessionValidator
{
    Task<bool> IsValidAsync(
        CurrentAuthSession current,
        CancellationToken cancellationToken);
}
