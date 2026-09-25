// FileDialogFilter.cs
//
// One entry of a file dialog's filter list: the name the dialog shows and the
// extensions it accepts. The format registries own the mapping from a format
// to a filter, so a page never derives it from the descriptors itself.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Pinta.Brix.Engine;

/// <summary>
/// A display name paired with the file extensions it stands for, ready to be
/// handed to a file dialog's filter list.
/// </summary>
public sealed class FileDialogFilter
{
	/// <summary>
	/// The name the dialog shows for this entry, for example
	/// <c>"OpenRaster image (*.ora)"</c>.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The extensions the entry accepts, each including its leading dot and
	/// in lower case, for example <c>".ora"</c>.
	/// </summary>
	public ImmutableArray<string> Extensions { get; }

	/// <param name="name">The name the dialog shows for this entry.</param>
	/// <param name="extensions">
	/// The extensions the entry accepts, each including its leading dot and in
	/// lower case.
	/// </param>
	public FileDialogFilter (string name, IEnumerable<string> extensions)
	{
		Name = name;
		Extensions = [.. extensions];
	}
}
