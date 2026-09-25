using Windows.Networking.Connectivity;

namespace CodeBrix.Android.Services;

/// <summary>The WinRT connectivity level of a connected Android network.</summary>
internal static class ConnectivityLevels
{
    /// <summary>The level of a connected network with the given capabilities.</summary>
    /// <param name="hasInternet">NET_CAPABILITY_INTERNET.</param>
    /// <param name="isValidated">NET_CAPABILITY_VALIDATED (the system reached the internet through it).</param>
    internal static NetworkConnectivityLevel From(bool hasInternet, bool isValidated) =>
        !hasInternet ? NetworkConnectivityLevel.LocalAccess
        : isValidated ? NetworkConnectivityLevel.InternetAccess
        : NetworkConnectivityLevel.ConstrainedInternetAccess;
}
