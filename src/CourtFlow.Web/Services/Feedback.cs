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

    public const string PublishedMessage = "Published.";
    public const string UnpublishedMessage = "Unpublished.";
    public const string ArchivedMessage = "Archived.";
    public const string RestoredMessage = "Restored.";

    public void Published() => snackbar.Add(PublishedMessage, Severity.Success);

    public void Unpublished() => snackbar.Add(UnpublishedMessage, Severity.Success);

    public void Archived() => snackbar.Add(ArchivedMessage, Severity.Success);

    public void Restored() => snackbar.Add(RestoredMessage, Severity.Success);

    public static string ImportedMessage(int rows) => $"Imported {rows} {(rows == 1 ? "row" : "rows")}.";

    public void Imported(int rows) => snackbar.Add(ImportedMessage(rows), Severity.Success);

    /// <summary>A success with its own wording, such as "Invitation sent."</summary>
    public void Done(string message) => snackbar.Add(message, Severity.Success);

    public void Rejected(string message) => snackbar.Add(message, Severity.Warning);

    public void Failed(string message) => snackbar.Add(message, Severity.Error);

    /// <summary>
    /// Runs a save or delete. A <see cref="DomainRuleException"/> shows as a rejected warning and an
    /// <see cref="HttpRequestException"/> as a failed error. Returns whether the action succeeded,
    /// so a dialog knows whether to close. <paramref name="onRejected"/> sees the rejection message,
    /// for a caller that must react to a particular rule.
    /// </summary>
    public async Task<bool> RunAsync(Func<Task> action, Action? onSuccess = null, Func<string, Task>? onRejected = null)
    {
        try
        {
            await action();
        }
        catch (DomainRuleException exception)
        {
            Rejected(exception.Message);
            if (onRejected is not null)
                await onRejected(exception.Message);

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
