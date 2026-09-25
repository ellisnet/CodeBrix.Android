using System;

// ReSharper disable once CheckNamespace
namespace PolyHavenBrowser.ViewModels;

/// <summary>
/// The head-capability bridge for the Model View: the view model invokes this as the view
/// opens, and the page does what only it can - ask its 3D preview canvas whether OpenGL
/// initialization failed. The view model must behave sensibly when the delegate is
/// <c>null</c>.
/// </summary>
public interface IModelViewBridge { Action ModelViewOpened { get; set; } }
