using System;

// ReSharper disable once CheckNamespace
namespace KenneyAssetBrowser.ViewModels;

/// <summary>
/// The head-capability bridge for the Viewer View: the view model invokes this as the viewer
/// opens, and the page does what only it can - ask its 3D preview canvas whether OpenGL
/// initialization failed. The view model must behave sensibly when the delegate is <c>null</c>.
/// </summary>
public interface IViewerPaneBridge { Action ViewerOpened { get; set; } }
