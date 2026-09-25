using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.AppSettings.Portable;
using CodeBrix.Platform.AppSettings;
using CodeBrix.Platform.AppSettings.Contracts;
using CodeBrix.Platform.Foundation.Extensibility;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.AppSettings.Tests.Portable;

[Collection("AppSettingsStore")]
public sealed class SqliteAppSettingsStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "codebrix-android-appsettings-tests", Guid.NewGuid().ToString("N"));
    private DateTime _now = new(2026, 9, 24, 8, 0, 0);

    public SqliteAppSettingsStorageTests() => TestStoragePlatform.EnsureRegistered();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void A_value_set_in_one_session_is_read_back_in_the_next()
    {
        //Arrange
        using (var store = Open("settings"))
        {
            store.Set("Theme", "Dark");
            store.Set("Count", 42);
        }

        //Act
        using var reopened = Open("settings");

        //Assert
        reopened.Get<string>("Theme").Should().Be("Dark");
        reopened.Get<int>("Count").Should().Be(42);
        reopened.WasCreatedFresh.Should().BeFalse();
    }

    [Fact]
    public void The_first_open_creates_a_fresh_database_file()
    {
        //Arrange
        //Act
        using var store = Open("settings");

        //Assert
        store.WasCreatedFresh.Should().BeTrue();
        File.Exists(store.DatabaseFilePath).Should().BeTrue();
        Path.GetFileName(store.DatabaseFilePath).Should().Be(SqliteAppSettingsStorage.DatabaseFileName);
    }

    [Fact]
    public void A_corrupt_file_is_quarantined_and_the_newest_auto_backup_restored()
    {
        //Arrange
        using (var first = Open("settings"))
        {
            first.Set("Keep", "me");
        }

        _now = _now.AddMinutes(1);
        string databaseFile;
        using (var second = Open("settings"))
        {
            databaseFile = second.DatabaseFilePath;
        }

        File.WriteAllText(databaseFile, "this is not a SQLite database");
        _now = _now.AddMinutes(1);

        //Act
        using var recovered = Open("settings");

        //Assert
        recovered.WasRestoredFromBackup.Should().BeTrue();
        recovered.Get<string>("Keep").Should().Be("me");
        Directory.GetFiles(Folder("settings"), "settings_corrupt_*.sqlite").Should().HaveCount(1);
    }

    [Fact]
    public void Auto_backups_are_pruned_to_the_retention_count()
    {
        //Arrange
        for (var i = 0; i < 6; i++)
        {
            _now = _now.AddMinutes(1);
            using var store = Open("settings");
            store.AutoBackupRetention = 3;
            store.Set("Round", i);
        }

        //Act
        _now = _now.AddMinutes(1);
        using (var last = Open("settings"))
        {
            last.AutoBackupRetention = 3;
        }

        //Assert
        Directory.GetFiles(Folder("settings"), SqliteAppSettingsStorage.AutoBackupPrefix + "*.sqlite").Length.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void An_exported_file_imported_into_another_store_replaces_its_settings_at_the_next_open()
    {
        //Arrange
        var export = Path.Combine(_root, "exported", "my-settings.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(export));
        using (var source = Open("source"))
        {
            source.Set("Server", "example.org");
            source.ExportToFile(export);
        }

        using (var target = Open("target"))
        {
            target.Set("Server", "old.example");
            target.StageIncomingFile(export);
        }

        //Act
        using var adopted = Open("target");

        //Assert
        adopted.WasReplacedByImport.Should().BeTrue();
        adopted.Get<string>("Server").Should().Be("example.org");
        Directory.GetFiles(Folder("target"), "settings_old_*.sqlite").Should().HaveCount(1);
    }

    [Fact]
    public void Exporting_into_the_settings_folder_itself_is_refused()
    {
        //Arrange
        using var store = Open("settings");

        //Act
        var export = () => store.ExportToFile(Path.Combine(store.DirectoryPath, "copy.sqlite"));

        //Assert
        export.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Staging_a_file_that_is_not_a_settings_database_is_refused()
    {
        //Arrange
        var notSettings = Path.Combine(_root, "notes.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(notSettings, "not a database");
        using var store = Open("settings");

        //Act
        var stage = () => store.StageIncomingFile(notSettings);

        //Assert
        stage.Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(store.DirectoryPath, SqliteAppSettingsStorage.IncomingFileName)).Should().BeFalse();
    }

    private string Folder(string name) => Path.Combine(_root, name);

    private AppSettingsStore Open(string name) => new(name, Folder(name), () => _now);

    /// <summary>The storage contract the Android platform registers, with folders under the temporary folder.</summary>
    private sealed class TestStoragePlatform : IAppSettingsStoragePlatform
    {
        private static readonly object Gate = new();
        private static bool _registered;

        internal static void EnsureRegistered()
        {
            lock (Gate)
            {
                if (_registered)
                {
                    return;
                }

                ApiExtensibility.Register(typeof(IAppSettingsStoragePlatform), _ => new TestStoragePlatform());
                _registered = true;
            }
        }

        public string GetDefaultDirectory(string appName) => Path.Combine(Path.GetTempPath(), "codebrix-android-appsettings-tests", appName);

        public IAppSettingsStorage Open(string appName, string directoryPath, IDictionary<string, string> values, Func<DateTime> clock) =>
            new SqliteAppSettingsStorage(appName, directoryPath, values, clock, Path.GetTempPath());
    }
}
