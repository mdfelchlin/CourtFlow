using CourtFlow.Web.Theming;
using MudBlazor;

namespace CourtFlow.Web.Services;

/// <summary>Modal delete confirmations. Cancel, Escape, and a backdrop click all answer no.</summary>
public sealed class Confirmations(IDialogService dialogs)
{
    public const string DeleteText = "Delete";
    public const string CancelText = "Cancel";

    public async Task<bool> ConfirmDeleteAsync(string itemName, string consequence)
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            BackdropClick = true,
            MaxWidth = CfDefaults.DialogMaxWidth,
        };

        var answer = await dialogs.ShowMessageBox(
            title: $"Delete {itemName}?",
            message: consequence,
            yesText: DeleteText,
            cancelText: CancelText,
            options: options);

        return answer == true;
    }
}
