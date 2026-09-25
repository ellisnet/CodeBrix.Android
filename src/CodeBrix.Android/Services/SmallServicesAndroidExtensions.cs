using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Notifications;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace CodeBrix.Android.Services;

/// <summary>ApplicationView.TryResizeView on Android: the system sizes app windows, so it answers false.</summary>
internal sealed class ApplicationViewAndroidExtension : IApplicationViewExtension
{
    /// <inheritdoc />
    public bool TryResizeView(Size size) => false;
}

/// <summary>Badge updates on Android (plan D-O10): accepted and ignored - launcher badges follow notifications.</summary>
internal sealed class BadgeUpdaterAndroidExtension : IBadgeUpdaterExtension
{
    /// <inheritdoc />
    public void SetBadge(int? value)
    {
    }
}

/// <summary>
/// ContactPicker on Android (plan D-O10, proposal "NotImplemented with diagnostics"): not supported -
/// IsSupportedAsync answers false and picking returns no contact.
/// </summary>
internal sealed class ContactPickerAndroidExtension : IContactPickerExtension
{
    /// <inheritdoc />
    public Task<bool> IsSupportedAsync(CancellationToken token) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<Contact[]> PickContactsAsync(bool multiple, CancellationToken token) => Task.FromResult(System.Array.Empty<Contact>());
}
