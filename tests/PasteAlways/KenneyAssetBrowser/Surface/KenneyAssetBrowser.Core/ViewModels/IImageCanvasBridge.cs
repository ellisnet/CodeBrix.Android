using System;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// The head-capability bridge for the 2D image viewer: the page fills in how the
/// SkiaSharp canvas is repainted (marshalled to the UI thread). The view model must
/// behave sensibly when the delegate is <c>null</c>.
/// </summary>
public interface IImageCanvasBridge { Action InvalidateImageCanvas { get; set; } }
