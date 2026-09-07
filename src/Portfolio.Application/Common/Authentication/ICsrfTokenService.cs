namespace Portfolio.Application.Common.Authentication;

public interface ICsrfTokenService
{
    string Issue(Guid sessionId);
    bool Validate(Guid sessionId, string token);
}
