// [AP8-S batch 2] The save picker's compatibility path (a created document's StorageFile.Path) writes back.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Reqnroll;
using SilverAssertions;
using AContentValues = Android.Content.ContentValues;
using AUri = Android.Net.Uri;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// Steps for the save compatibility path: a document created for saving (here a MediaStore Downloads entry the app
/// owns, standing in for the one the save picker creates) is wrapped in the framework's ContentDocumentFile exactly
/// as the save picker wraps its result; desktop code then writes the file at its Path with System.IO, and the
/// document must receive the bytes. KenneyAssetBrowser/PainDiagram: the app DELETES the picker's empty placeholder
/// before writing its own file, which used to break the write-back (the document stayed empty).
/// </summary>
[Binding]
public sealed class SaveWriteBackSteps
{
    private AUri? _document;
    private string? _path;

    /// <summary>A document created for saving, wrapped as the save picker wraps it (the framework's own type).</summary>
    [Given("a Downloads document {string} created for saving")]
    public async Task Given_a_document(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var resolver = AppHost.Activity!.ContentResolver!;
            var values = new AContentValues();
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.DisplayName, name);
            values.Put(global::Android.Provider.MediaStore.IMediaColumns.MimeType, "image/png");
            _document = resolver.Insert(global::Android.Provider.MediaStore.Downloads.ExternalContentUri!, values);
            var type = Type.GetType("CodeBrix.Android.Services.ContentDocumentFile, CodeBrix.Android", throwOnError: true)!;
            var file = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                new object?[] { _document, name, "image/png", true }, null)!;
            _path = (string)type.GetProperty("Path")!.GetValue(file)!;
        }).ConfigureAwait(false);
        _document.Should().NotBeNull("the app must be able to create its own Downloads entry");
        File.Exists(_path).Should().BeTrue("the compatibility path is an empty placeholder file");
    }

    /// <summary>
    /// Desktop code deletes the placeholder, then - after its own work (PainDiagram renders the PNG) - writes its file
    /// at the path (System.IO). The pause lets the delete's file-system event arrive before the new file exists.
    /// </summary>
    [When("the app deletes the compatibility file and, a second later, writes {int} bytes to it")]
    public async Task When_delete_and_write(int count)
    {
        File.Delete(_path!);
        await Task.Delay(1000).ConfigureAwait(false);
        var bytes = new byte[count];
        for (var i = 0; i < count; i++)
        {
            bytes[i] = (byte)(i % 251);
        }

        await File.WriteAllBytesAsync(_path!, bytes).ConfigureAwait(false);
    }

    /// <summary>The document holds the bytes within three seconds (the write-back runs on the file observer's thread).</summary>
    [Then("the document holds {int} bytes")]
    public async Task Then_document_holds(int count)
    {
        long length = -1;
        try
        {
            for (var attempt = 0; attempt < 30 && length != count; attempt++)
            {
                await Task.Delay(100).ConfigureAwait(false);
                using var input = AppHost.Activity!.ContentResolver!.OpenInputStream(_document!)!;
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer).ConfigureAwait(false);
                length = buffer.Length;
            }
        }
        finally
        {
            AppHost.Activity!.ContentResolver!.Delete(_document!, null, null);
        }

        length.Should().Be(count, "the bytes written at the compatibility path must reach the document");
    }
}
