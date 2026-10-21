using CourtFlow.Web.Theming;
using MudBlazor;

namespace CourtFlow.Web.Services;

/// <summary>Modal confirmations. Cancel, Escape, and a backdrop click all answer no.</summary>
public sealed class Confirmations(IDialogService dialogs)
{
    public const string DeleteText = "Delete";
    public const string CancelText = "Cancel";

    public Task<bool> ConfirmDeleteAsync(string itemName, string consequence) =>
        ConfirmAsync($"Delete {itemName}?", consequence, DeleteText);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        ConfirmAsync(title, message, confirmText, CancelText);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            BackdropClick = true,
            MaxWidth = CfDefaults.DialogMaxWidth,
        };

        var answer = await dialogs.ShowMessageBoxAsync(
            title: title,
            message: message,
            yesText: confirmText,
            cancelText: cancelText,
            options: options);

        return answer == true;
    }
}
