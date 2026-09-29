using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

/// <summary>
/// AP1.12: the path rules and existence answers of the Android package-files contract (IApplicationPackageFilesPlatform,
/// WPE1-13) over a fake APK asset tree; on the device the same lookup runs over the AssetManager.
/// </summary>
public class PackageFileLookupTests
{
    private sealed class FakeAssets
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal)
        {
            ["Assets/pulse.json"] = "{\"v\":\"5.7\"}",
            ["Assets/Images/logo.scale-200.png"] = "png",
            ["CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf"] = "ttf",
            ["top.txt"] = "top",
        };

        internal List<string> Opened { get; } = new();

        internal int Listings { get; private set; }

        internal Stream Open(string path)
        {
            Opened.Add(path);
            return _files.TryGetValue(path, out var text) ? new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false) : null;
        }

        internal string[] List(string folder)
        {
            Listings++;
            var prefix = folder.Length == 0 ? string.Empty : folder + "/";
            return _files.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .Select(k => k.Substring(prefix.Length).Split('/')[0])
                .Distinct()
                .ToArray();
        }
    }

    private static (PackageFileLookup Lookup, FakeAssets Assets) Create()
    {
        var assets = new FakeAssets();
        return (new PackageFileLookup(assets.Open, assets.List), assets);
    }

    [Theory]
    [InlineData("Assets/pulse.json")]
    [InlineData("/Assets/pulse.json")]
    [InlineData("Assets\\pulse.json")]
    [InlineData("Assets/Images/logo.scale-200.png")]
    [InlineData("CodeBrix.Platform.Fonts.RobotoMono/Fonts/RobotoMono.ttf")]
    [InlineData("top.txt")]
    public void A_packaged_file_exists_and_opens(string path)
    {
        //Arrange
        var (lookup, _) = Create();

        //Act
        var exists = lookup.FileExists(path);
        using var stream = lookup.OpenRead(path);

        //Assert
        exists.Should().BeTrue();
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void Opening_reads_the_file_content()
    {
        //Arrange
        var (lookup, _) = Create();

        //Act
        using var reader = new StreamReader(lookup.OpenRead("Assets/pulse.json"));

        //Assert
        reader.ReadToEnd().Should().Be("{\"v\":\"5.7\"}");
    }

    [Theory]
    [InlineData("Assets/missing.json")]
    [InlineData("assets/pulse.json")]
    [InlineData("codebrix.platform.fonts.robotomono/Fonts/RobotoMono.ttf")]
    [InlineData("Nowhere/pulse.json")]
    public void A_missing_file_does_not_exist_and_opens_as_null_and_the_package_is_case_sensitive(string path)
    {
        //Arrange
        var (lookup, _) = Create();

        //Act
        var exists = lookup.FileExists(path);
        var stream = lookup.OpenRead(path);

        //Assert
        exists.Should().BeFalse();
        stream.Should().BeNull();
    }

    [Theory]
    [InlineData("Assets")]
    [InlineData("Assets/Images")]
    [InlineData("CodeBrix.Platform.Fonts.RobotoMono")]
    public void A_folder_is_not_a_file(string path)
    {
        //Arrange
        var (lookup, _) = Create();

        //Act
        var exists = lookup.FileExists(path);
        var stream = lookup.OpenRead(path);

        //Assert
        exists.Should().BeFalse();
        stream.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("/")]
    [InlineData("Assets//pulse.json")]
    [InlineData("Assets/./pulse.json")]
    [InlineData("Assets/../top.txt")]
    public void A_path_that_cannot_name_a_package_file_is_refused_without_touching_the_assets(string path)
    {
        //Arrange
        var (lookup, assets) = Create();

        //Act
        var exists = lookup.FileExists(path);
        var stream = lookup.OpenRead(path);

        //Assert
        exists.Should().BeFalse();
        stream.Should().BeNull();
        assets.Opened.Should().BeEmpty();
        assets.Listings.Should().Be(0);
    }

    [Fact]
    public void A_missing_file_is_answered_from_the_folder_listing_without_an_open()
    {
        //Arrange
        var (lookup, assets) = Create();

        //Act
        lookup.OpenRead("Assets/missing.json");

        //Assert
        assets.Opened.Should().BeEmpty();
    }

    [Fact]
    public void Folder_listings_are_cached()
    {
        //Arrange
        var (lookup, assets) = Create();
        lookup.FileExists("Assets/pulse.json");
        var listings = assets.Listings;

        //Act
        lookup.FileExists("Assets/pulse.json");
        lookup.OpenRead("Assets/pulse.json")?.Dispose();

        //Assert
        assets.Listings.Should().Be(listings);
    }

    [Fact]
    public void The_Core_contract_has_the_two_members_the_Android_platform_implements()
    {
        //Arrange
        var contract = typeof(Windows.Storage.StorageFile).Assembly.GetType("CodeBrix.Platform.Contracts.IApplicationPackageFilesPlatform");

        //Act
        var members = contract?.GetMethods(BindingFlags.Instance | BindingFlags.Public).Select(m => m.Name).OrderBy(n => n).ToArray();

        //Assert
        contract.Should().NotBeNull();
        members.Should().BeEquivalentTo(new[] { "FileExists", "OpenRead" });
    }
}
