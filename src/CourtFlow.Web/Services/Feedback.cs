using CourtFlow.Domain;
using MudBlazor;

namespace CourtFlow.Web.Services;

/// <summary>The shared snackbar vocabulary: Saved, Deleted, a rejected rule, or a failed call.</summary>
public sealed class Feedback(ISnackbar snackbar)
{
    public const string SavedMessage = "Saved";
    public const string DeletedMessage = "Deleted";

    public void Saved() => snackbar.Add(SavedMessage, Severity.Success);

    public void Deleted() => snackbar.Add(DeletedMessage, Severity.Success);

    public void Rejected(string message) => snackbar.Add(message, Severity.Warning);

    public void Failed(string message) => snackbar.Add(message, Severity.Error);

    /// <summary>
    /// Runs a save or delete. A <see cref="DomainRuleException"/> shows as a rejected warning and an
    /// <see cref="HttpRequestException"/> as a failed error. Returns whether the action succeeded,
    /// so a dialog knows whether to close.
    /// </summary>
    public async Task<bool> RunAsync(Func<Task> action, Action? onSuccess = null)
    {
        try
        {
            await action();
        }
        catch (DomainRuleException exception)
        {
            Rejected(exception.Message);
            return false;
        }
        catch (HttpRequestException exception)
        {
            Failed(exception.Message);
            return false;
        }

        onSuccess?.Invoke();
        return true;
    }
}
