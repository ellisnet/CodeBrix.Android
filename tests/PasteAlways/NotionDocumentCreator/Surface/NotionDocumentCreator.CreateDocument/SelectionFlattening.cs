using System;
using System.Collections.Generic;

namespace NotionDocumentCreator.CreateDocument;

/// <summary>
/// Flattens a page tree into reading order. Selection is fully independent per
/// node, so a caller filters the flattened order by its own checked state — a
/// checked grandchild under an unchecked child still appears, in tree order.
/// </summary>
public static class SelectionFlattening
{
    /// <summary>
    /// Flattens the tree depth-first, top to bottom — the order pages appear in
    /// the treeview, and therefore the order chapters appear in the book.
    /// </summary>
    public static IReadOnlyList<T> FlattenDepthFirst<T>(
        IEnumerable<T> roots, Func<T, IEnumerable<T>> childrenOf)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(childrenOf);

        var flattened = new List<T>();
        void Visit(T node)
        {
            flattened.Add(node);
            foreach (var child in childrenOf(node) ?? []) { Visit(child); }
        }

        foreach (var root in roots) { Visit(root); }
        return flattened;
    }
}
