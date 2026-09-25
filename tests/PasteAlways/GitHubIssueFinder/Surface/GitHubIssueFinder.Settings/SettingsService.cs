using CodeBrix.Platform.AppSettings;
using System;

namespace GitHubIssueFinder.Settings;

/// <summary>
/// The static facade over GitHubIssueFinder's application settings: every configurable value
/// is read and written through this service, backed by the CodeBrix.Platform.AppSettings
/// store, one portable settings.sqlite with startup auto-backup, corruption recovery and
/// import/export.
/// </summary>
public static class SettingsService
{
    /// <summary>The application name the settings store is registered under.</summary>
    public const string AppName = "GitHubIssueFinder";

    /// <summary>Whether <see cref="Initialize()"/> has been called.</summary>
    public static bool IsInitialized => AppSettingsService.IsInitialized;

    /// <summary>The settings store; only available after <see cref="Initialize()"/>.</summary>
    public static AppSettingsStore Store => AppSettingsService.Store;

    /// <summary>
    /// The default settings folder: the "settings" subfolder of the application's per-user
    /// CodeBrix-family configuration folder.
    /// </summary>
    public static string DefaultDirectory => AppSettingsService.GetDefaultDirectory(AppName);

    /// <summary>
    /// Opens the settings store in the default folder, running the startup auto-backup and
    /// pruning sequence. Call once, before any UI renders.
    /// </summary>
    public static void Initialize() => AppSettingsService.Initialize(AppName);

    /// <summary>Opens the settings store in the given folder.</summary>
    /// <param name="directoryPath">The folder the store lives in.</param>
    public static void Initialize(string directoryPath) =>
        AppSettingsService.Initialize(AppName, directoryPath);

    /// <summary>Closes the store and permits a later <see cref="Initialize()"/> (test hosts).</summary>
    public static void Shutdown() => AppSettingsService.Shutdown();

    /// <summary>Wraps a setting in a typed <see cref="AppSettingProperty{T}"/> handle.</summary>
    /// <typeparam name="T">The type the setting is read and written as.</typeparam>
    /// <param name="property">The key the setting is stored under.</param>
    /// <param name="defaultValue">The value the handle reads when nothing is stored.</param>
    /// <returns>The typed handle.</returns>
    public static AppSettingProperty<T> Wrap<T>(string property, T defaultValue) =>
        AppSettingsService.Wrap(property, defaultValue);

    /// <summary>Whether a value is stored for the given key.</summary>
    /// <param name="property">The key to look for.</param>
    /// <returns>True when a value is stored.</returns>
    public static bool HasValue(string property) => AppSettingsService.HasValue(property);

    /// <summary>Returns the stored value for the key, or the given default when not set.</summary>
    /// <typeparam name="T">The type the setting is read as.</typeparam>
    /// <param name="property">The key to read.</param>
    /// <param name="defaultValue">The value returned when nothing is stored.</param>
    /// <returns>The stored value, or <paramref name="defaultValue"/>.</returns>
    public static T Get<T>(string property, T defaultValue) => AppSettingsService.Get(property, defaultValue);

    /// <summary>Returns the stored value for the key, or the type's default when not set.</summary>
    /// <typeparam name="T">The type the setting is read as.</typeparam>
    /// <param name="property">The key to read.</param>
    /// <returns>The stored value, or the default of <typeparamref name="T"/>.</returns>
    public static T Get<T>(string property) => AppSettingsService.Get<T>(property);

    /// <summary>Stores a value for the key; a null value removes the key.</summary>
    /// <param name="key">The key to write.</param>
    /// <param name="val">The value to store, or null to remove the key.</param>
    public static void Set(string key, object val) => AppSettingsService.Set(key, val);

    /// <summary>Registers a handler raised when the given key's value changes.</summary>
    /// <param name="propertyName">The key to watch.</param>
    /// <param name="handler">The handler to call.</param>
    public static void AddPropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.AddSettingHandler(propertyName, handler);

    /// <summary>Removes a handler previously added with <see cref="AddPropertyHandler"/>.</summary>
    /// <param name="propertyName">The key the handler watches.</param>
    /// <param name="handler">The handler to remove.</param>
    public static void RemovePropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.RemoveSettingHandler(propertyName, handler);
}
