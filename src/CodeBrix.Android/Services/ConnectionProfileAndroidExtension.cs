using CodeBrix.Android.Android;
using Windows.Networking.Connectivity;
using AConnectivityManager = global::Android.Net.ConnectivityManager;
using AContext = global::Android.Content.Context;
using ANetCapability = global::Android.Net.NetCapability;
using APermission = global::Android.Content.PM.Permission;

namespace CodeBrix.Android.Services;

/// <summary>
/// NetworkInformation.GetInternetConnectionProfile().GetNetworkConnectivityLevel() on Android, from the active
/// network's capabilities: INTERNET + VALIDATED = InternetAccess, INTERNET without validation (a captive portal)
/// = ConstrainedInternetAccess, any other connected network = LocalAccess, no network = None.
/// </summary>
/// <remarks>
/// Needs android.permission.ACCESS_NETWORK_STATE in the APP's manifest (a normal permission, granted at
/// install; CodeBrix.Android does not add permissions to apps). Without it the level is reported as
/// InternetAccess - the optimistic answer, so an app that checks before a request still tries it - and a
/// warning is logged once.
/// </remarks>
internal sealed class ConnectionProfileAndroidExtension : IConnectionProfileExtension
{
    private static bool _warned;

    /// <inheritdoc />
    public NetworkConnectivityLevel GetNetworkConnectivityLevel()
    {
        var context = AndroidContext.Current;
        if (context.CheckSelfPermission(global::Android.Manifest.Permission.AccessNetworkState) != APermission.Granted)
        {
            if (!_warned)
            {
                _warned = true;
                ServiceLog.Warn("ACCESS_NETWORK_STATE is not in the app's manifest: the network connectivity level is reported as InternetAccess.");
            }

            return NetworkConnectivityLevel.InternetAccess;
        }

        if (context.GetSystemService(AContext.ConnectivityService) is not AConnectivityManager manager
            || manager.ActiveNetwork is not { } network
            || manager.GetNetworkCapabilities(network) is not { } capabilities)
        {
            return NetworkConnectivityLevel.None;
        }

        return ConnectivityLevels.From(
            capabilities.HasCapability(ANetCapability.Internet),
            capabilities.HasCapability(ANetCapability.Validated));
    }
}
