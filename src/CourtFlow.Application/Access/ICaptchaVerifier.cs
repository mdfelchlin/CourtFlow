namespace CourtFlow.Application.Access;

public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(string? token, CancellationToken cancellationToken);
}
