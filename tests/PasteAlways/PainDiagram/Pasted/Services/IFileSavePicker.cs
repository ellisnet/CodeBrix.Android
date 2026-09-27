using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace PainDiagram.Services;

/// <summary>
/// The head's native "save PNG" dialog, behind an interface and registered with
/// <c>SimpleServiceResolver</c> at startup, so the page that hands the delegate to the view
/// model's <c>IFileSaveBridge</c> property never builds a picker itself.
/// </summary>
public interface IFileSavePicker
{
    /// <summary>
    /// Shows a "save PNG" dialog seeded with <paramref name="suggestedFileName"/> and returns
    /// the full path the user chose, or <c>null</c> if they cancelled.
    /// </summary>
    /// <param name="suggestedFileName">The file name the dialog opens with.</param>
    /// <returns>The chosen full path, or <c>null</c> when the dialog was cancelled.</returns>
    Task<string> PickSavePngPathAsync(string suggestedFileName);
}
