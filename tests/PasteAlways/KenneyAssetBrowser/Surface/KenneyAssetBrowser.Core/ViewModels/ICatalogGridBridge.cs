using System;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// The head-capability bridge for the browsing grid: the page fills in how the catalog is
/// scrolled back to its top, which the view model asks for whenever it swaps in a new cell
/// collection. The view model must behave sensibly when the delegate is <c>null</c>.
/// </summary>
public interface ICatalogGridBridge { Action ScrollCatalogToTop { get; set; } }
