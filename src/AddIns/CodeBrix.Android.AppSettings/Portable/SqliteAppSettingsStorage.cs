using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CodeBrix.Platform.AppSettings;
using CodeBrix.Platform.AppSettings.Contracts;
using CodeBrix.Sqlite;
using Microsoft.Data.Sqlite;

namespace CodeBrix.Android.AppSettings.Portable;

/// <summary>
/// One open settings database (<see cref="IAppSettingsStorage"/>): settings.sqlite in the store's folder, one row per
/// key (table Setting: Key, Value = the JSON), opened through CodeBrix.Sqlite. The lifecycle is the one every
/// CodeBrix.Platform head has (the Platform storage, ported behaviour for behaviour, with the same file names, messages
/// and log lines): at open a staged import (settings_incoming.sqlite) is adopted first - the previous file kept as
/// settings_old_&lt;time&gt;.sqlite; a file that cannot be opened or fails PRAGMA integrity_check is quarantined as
/// settings_corrupt_&lt;time&gt;.sqlite and the newest settings_auto_backup_&lt;time&gt;.sqlite restored, else the store
/// starts fresh; auto-backups are SQLite online backups, pruned to a retain count; an export is a backup to a file
/// outside the settings folder; an import is validated (opens, integrity ok, has a readable Setting table) and staged.
/// Pure .NET (no Android API): the Android platform only chooses the folders.
/// </summary>
internal sealed class SqliteAppSettingsStorage : IAppSettingsStorage
{
    /// <summary>The settings database file name.</summary>
    internal const string DatabaseFileName = "settings.sqlite";

    /// <summary>The staged import's file name (adopted at the next open).</summary>
    internal const string IncomingFileName = "settings_incoming.sqlite";

    /// <summary>The prefix of an automatic backup's file name.</summary>
    internal const string AutoBackupPrefix = "settings_auto_backup_";

    /// <summary>The time stamp format of the kept, quarantined and backup files.</summary>
    internal const string TimeStampFormat = "yyyy-MM-dd_HH-mm-ss";

    private static readonly string[] SidecarSuffixes = ["-wal", "-shm", "-journal"];

    private readonly string _appName;
    private readonly IDictionary<string, string> _values;
    private readonly Func<DateTime> _clock;
    private readonly string _stagingRoot;
    private SqliteDatabase _database;

    /// <summary>Opens (or creates) the settings database of an app and loads every value into <paramref name="values"/>.</summary>
    /// <param name="appName">The app name (the store's identity; used for the staging folder).</param>
    /// <param name="directoryPath">The settings folder (created when missing).</param>
    /// <param name="values">The store's in-memory values (cleared and filled from the database).</param>
    /// <param name="clock">The clock the file time stamps come from.</param>
    /// <param name="stagingRoot">Where an import is copied for validation (a temporary folder).</param>
    internal SqliteAppSettingsStorage(string appName, string directoryPath, IDictionary<string, string> values, Func<DateTime> clock, string stagingRoot)
    {
        _appName = appName;
        _values = values;
        _clock = clock;
        _stagingRoot = stagingRoot ?? Path.GetTempPath();
        DirectoryPath = Path.GetFullPath(directoryPath);
        DatabaseFilePath = Path.Combine(DirectoryPath, DatabaseFileName);
        Directory.CreateDirectory(DirectoryPath);
        AdoptIncomingFile();
        OpenWithRecovery();
    }

    /// <inheritdoc />
    public string DirectoryPath { get; }

    /// <inheritdoc />
    public string DatabaseFilePath { get; }

    /// <inheritdoc />
    public bool WasCreatedFresh { get; private set; }

    /// <inheritdoc />
    public bool WasRestoredFromBackup { get; private set; }

    /// <inheritdoc />
    public bool WasReplacedByImport { get; private set; }

    private SqliteDatabase Database => _database ?? throw new ObjectDisposedException("AppSettingsStore");

    /// <inheritdoc />
    public void Write(string key, string json) =>
        Execute(Database.Connection, "INSERT INTO Setting (Key, Value) VALUES (@key, @newJson) ON CONFLICT (Key) DO UPDATE SET Value = @newJson",
            ("@key", key), ("@newJson", json));

    /// <inheritdoc />
    public void Delete(string key) => Execute(Database.Connection, "DELETE FROM Setting WHERE Key = @key", ("@key", key));

    /// <inheritdoc />
    public void CreateAutoBackupAndPrune(int retainCount)
    {
        CreateAutoBackup();
        PruneAutoBackups(retainCount);
    }

    /// <inheritdoc />
    public string GetExportDestination(string destinationFilePath)
    {
        if (string.IsNullOrEmpty(destinationFilePath))
        {
            throw new ArgumentException("A destination file path is required", nameof(destinationFilePath));
        }

        var fullPath = Path.GetFullPath(destinationFilePath);
        var folder = Path.GetDirectoryName(fullPath);
        if (string.Equals(folder, DirectoryPath, StringComparison.Ordinal)
            || (folder ?? string.Empty).StartsWith(DirectoryPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Settings cannot be exported into the settings folder itself; please choose another location.");
        }

        return fullPath;
    }

    /// <inheritdoc />
    public void ExportTo(string fullPath) => Database.BackupToFile(fullPath);

    /// <inheritdoc />
    public void StageIncomingFile(string sourceFilePath)
    {
        if (string.IsNullOrEmpty(sourceFilePath))
        {
            throw new ArgumentException("A source file path is required", nameof(sourceFilePath));
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("The selected file does not exist.", sourceFilePath);
        }

        var staging = Path.Combine(_stagingRoot, "CodeBrix", _appName, Path.GetRandomFileName());
        Directory.CreateDirectory(staging);
        try
        {
            var copy = Path.Combine(staging, DatabaseFileName);
            File.Copy(sourceFilePath, copy);
            foreach (var suffix in SidecarSuffixes)
            {
                if (File.Exists(sourceFilePath + suffix))
                {
                    File.Copy(sourceFilePath + suffix, copy + suffix);
                }
            }

            using var candidate = new SqliteDatabase(copy, null, new SqliteDatabaseOptions());
            try
            {
                candidate.SafeOpen();
                if (!string.Equals(candidate.ExecuteScalar("PRAGMA integrity_check", false) as string, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The file failed the SQLite integrity check.");
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("The file could not be opened as a SQLite database: " + e.Message, e);
            }

            if (candidate.ExecuteScalar("SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'Setting'", false) == null)
            {
                throw new InvalidDataException("The file is a SQLite database, but does not contain the Setting table a settings file holds.");
            }

            try
            {
                _ = ReadAll(candidate.Connection).Count;
            }
            catch (Exception e)
            {
                throw new InvalidDataException("The file's Setting table could not be read: " + e.Message, e);
            }

            candidate.BackupToFile(Path.Combine(DirectoryPath, IncomingFileName));
            AppSettingLoggingService.LogInfo("Settings file " + sourceFilePath + " staged as " + IncomingFileName);
        }
        finally
        {
            try
            {
                Directory.Delete(staging, true);
            }
            catch (Exception)
            {
                // A staging folder that cannot be removed is left for the system to clean (it is under the temporary folder).
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _database?.Dispose();
        _database = null;
    }

    private static string Stamp(DateTime time) => time.ToString(TimeStampFormat, CultureInfo.InvariantCulture);

    private void AdoptIncomingFile()
    {
        var incoming = Path.Combine(DirectoryPath, IncomingFileName);
        if (!File.Exists(incoming))
        {
            return;
        }

        try
        {
            if (File.Exists(DatabaseFilePath))
            {
                var kept = Path.Combine(DirectoryPath, "settings_old_" + Stamp(_clock()) + ".sqlite");
                File.Move(DatabaseFilePath, kept, true);
                foreach (var suffix in SidecarSuffixes)
                {
                    var sidecar = DatabaseFilePath + suffix;
                    if (File.Exists(sidecar))
                    {
                        File.Move(sidecar, kept + suffix, true);
                    }
                }

                AppSettingLoggingService.LogInfo("Previous settings kept as " + Path.GetFileName(kept));
            }
            else
            {
                foreach (var suffix in SidecarSuffixes)
                {
                    var sidecar = DatabaseFilePath + suffix;
                    if (File.Exists(sidecar))
                    {
                        File.Delete(sidecar);
                    }
                }
            }

            File.Move(incoming, DatabaseFilePath);
            WasReplacedByImport = true;
            AppSettingLoggingService.LogInfo("Imported settings file " + IncomingFileName + " adopted as " + DatabaseFileName);
        }
        catch (Exception e)
        {
            AppSettingLoggingService.LogError("The imported settings file could not be adopted", e);
        }
    }

    private void OpenWithRecovery()
    {
        try
        {
            WasCreatedFresh = !File.Exists(DatabaseFilePath);
            OpenAndLoad();
            return;
        }
        catch (Exception e)
        {
            AppSettingLoggingService.LogError("The settings file '" + DatabaseFilePath + "' could not be opened; quarantining it", e);
            QuarantineCorruptFile();
        }

        if (TryRestoreNewestAutoBackup())
        {
            try
            {
                OpenAndLoad();
                WasRestoredFromBackup = true;
                return;
            }
            catch (Exception e)
            {
                AppSettingLoggingService.LogError("The restored settings backup could not be opened either; starting fresh", e);
                QuarantineCorruptFile();
            }
        }

        WasCreatedFresh = true;
        OpenAndLoad();
    }

    private void OpenAndLoad()
    {
        var database = new SqliteDatabase(DatabaseFilePath, null, new SqliteDatabaseOptions());
        try
        {
            database.SafeOpen();
            if (!string.Equals(database.ExecuteScalar("PRAGMA integrity_check", false) as string, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("PRAGMA integrity_check did not report 'ok'");
            }

            database.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS Setting (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL)", false);
            _values.Clear();
            foreach (var (key, value) in ReadAll(database.Connection))
            {
                _values[key] = value;
            }
        }
        catch
        {
            database.Dispose();
            throw;
        }

        _database = database;
    }

    private void QuarantineCorruptFile()
    {
        _database?.Dispose();
        _database = null;
        _values.Clear();
        if (!File.Exists(DatabaseFilePath))
        {
            return;
        }

        var quarantined = Path.Combine(DirectoryPath, "settings_corrupt_" + Stamp(_clock()) + ".sqlite");
        File.Move(DatabaseFilePath, quarantined, true);
        foreach (var suffix in SidecarSuffixes)
        {
            var sidecar = DatabaseFilePath + suffix;
            if (File.Exists(sidecar))
            {
                File.Move(sidecar, quarantined + suffix, true);
            }
        }
    }

    private bool TryRestoreNewestAutoBackup()
    {
        var newest = EnumerateAutoBackups().OrderByDescending(backup => backup.Time).FirstOrDefault();
        if (newest.Path == null)
        {
            return false;
        }

        try
        {
            File.Copy(newest.Path, DatabaseFilePath, true);
            AppSettingLoggingService.LogInfo("Settings restored from backup " + Path.GetFileName(newest.Path));
            return true;
        }
        catch (Exception e)
        {
            AppSettingLoggingService.LogError("Could not restore settings backup " + newest.Path, e);
            return false;
        }
    }

    private void CreateAutoBackup()
    {
        var path = Path.Combine(DirectoryPath, AutoBackupPrefix + Stamp(_clock()) + ".sqlite");
        Database.BackupToFile(path);
        AppSettingLoggingService.LogInfo("Settings auto-backup created: " + Path.GetFileName(path));
    }

    private void PruneAutoBackups(int retainCount)
    {
        foreach (var backup in EnumerateAutoBackups().OrderByDescending(backup => backup.Time).Skip(retainCount))
        {
            try
            {
                File.Delete(backup.Path);
                AppSettingLoggingService.LogInfo("Settings auto-backup pruned: " + Path.GetFileName(backup.Path));
            }
            catch (Exception e)
            {
                AppSettingLoggingService.LogWarning("Could not prune settings auto-backup " + backup.Path + ": " + e.Message);
            }
        }
    }

    private IEnumerable<(string Path, DateTime Time)> EnumerateAutoBackups()
    {
        foreach (var file in Directory.EnumerateFiles(DirectoryPath, AutoBackupPrefix + "*.sqlite"))
        {
            var name = System.IO.Path.GetFileName(file);
            var stamp = name.Substring(AutoBackupPrefix.Length, name.Length - AutoBackupPrefix.Length - ".sqlite".Length);
            if (DateTime.TryParseExact(stamp, TimeStampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                yield return (file, time);
            }
        }
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, string Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static List<(string Key, string Value)> ReadAll(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Setting";
        using var reader = command.ExecuteReader();
        var rows = new List<(string, string)>();
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }
}
