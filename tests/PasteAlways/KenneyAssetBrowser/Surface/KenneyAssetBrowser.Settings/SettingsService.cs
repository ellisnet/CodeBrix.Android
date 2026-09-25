//
// SettingsService.cs
//
// Copyright (c) 2026 Jeremy Ellis and contributors
//     (a thin app-named facade over the CodeBrix.Platform.AppSettings add-in)
// SPDX-License-Identifier: MIT
//

using System;
using CodeBrix.Platform.AppSettings;

namespace KenneyAssetBrowser.Settings;

/// <summary>
/// The static facade over KenneyAssetBrowser's application settings: every
/// configurable value is read and written through this service, backed by
/// the CodeBrix.Platform.AppSettings store — one portable settings.sqlite
/// with startup auto-backup, corruption recovery, and import/export.
/// </summary>
public static class SettingsService
{
    /// <summary>The application name the settings store is registered under.</summary>
    public const string AppName = "KenneyAssetBrowser";

    /// <summary>Whether <see cref="Initialize()"/> has been called.</summary>
    public static bool IsInitialized => AppSettingsService.IsInitialized;

    /// <summary>
    /// The settings store; only available after <see cref="Initialize()"/>.
    /// </summary>
    public static AppSettingsStore Store => AppSettingsService.Store;

    /// <summary>
    /// The default settings folder: the "settings" subfolder of the
    /// application's per-user CodeBrix-family configuration folder.
    /// </summary>
    public static string DefaultDirectory => AppSettingsService.GetDefaultDirectory(AppName);

    /// <summary>
    /// Opens the settings store in the default folder, running the startup
    /// auto-backup and pruning sequence. Call once, before any UI renders.
    /// </summary>
    public static void Initialize() => AppSettingsService.Initialize(AppName);

    /// <summary>Opens the settings store in the given folder.</summary>
    public static void Initialize(string directoryPath) =>
        AppSettingsService.Initialize(AppName, directoryPath);

    /// <summary>
    /// Closes the store and permits a later <see cref="Initialize()"/> (test hosts).
    /// </summary>
    public static void Shutdown() => AppSettingsService.Shutdown();

    /// <summary>Wraps a setting in a typed <see cref="AppSettingProperty{T}"/> handle.</summary>
    public static AppSettingProperty<T> Wrap<T>(string property, T defaultValue) =>
        AppSettingsService.Wrap(property, defaultValue);

    /// <summary>Whether a value is stored for the given key.</summary>
    public static bool HasValue(string property) => AppSettingsService.HasValue(property);

    /// <summary>Returns the stored value for the key, or the given default when not set.</summary>
    public static T Get<T>(string property, T defaultValue) => AppSettingsService.Get(property, defaultValue);

    /// <summary>Returns the stored value for the key, or the type's default when not set.</summary>
    public static T Get<T>(string property) => AppSettingsService.Get<T>(property);

    /// <summary>Stores a value for the key; a null value removes the key.</summary>
    public static void Set(string key, object val) => AppSettingsService.Set(key, val);

    /// <summary>Registers a handler raised when the given key's value changes.</summary>
    public static void AddPropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.AddSettingHandler(propertyName, handler);

    /// <summary>Removes a handler previously added with <see cref="AddPropertyHandler"/>.</summary>
    public static void RemovePropertyHandler(string propertyName, EventHandler<AppSettingChangedEventArgs> handler) =>
        AppSettingsService.RemoveSettingHandler(propertyName, handler);
}
