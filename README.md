# CodeBrix.Android

Native Android apps from CodeBrix.Platform C# and XAML: the pages you write for CodeBrix.Platform compile unchanged for Android and render with real Android and Material Components views. CodeBrix.Android is provided as .NET 10 libraries and the associated `CodeBrix.Android.ApacheLicenseForever` NuGet packages.

CodeBrix.Android is desktop-first: it targets Android desktops such as Googlebooks and docked phones in desktop mode, and treats Android phones and tablets as first-class targets too, adapting each page to the window size it runs in.

CodeBrix.Android supports applications and assemblies that target Microsoft .NET version 10.0 and later.
Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028.
Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

## Installation

```
dotnet add package CodeBrix.Android.ApacheLicenseForever
```

Note that the NuGet package ID and the namespace are different - there is no package named plain `CodeBrix.Android`:

* NuGet package ID: `CodeBrix.Android.ApacheLicenseForever`
* Assemblies and primary namespaces: `CodeBrix.Android` and `CodeBrix.Android.UI` - your XAML and code-behind keep using the CodeBrix.Platform namespaces (`Microsoft.UI.Xaml`, ...)

XML documentation (IntelliSense) ships alongside the assemblies.

The package carries everything an Android head needs from CodeBrix.Platform, and depends on the .NET bindings of Material Components for Android and AndroidX. An Android head never references the CodeBrix.Platform desktop packages.

Each CodeBrix.Platform add-in has an Android package of its own, named after the add-in's CodeBrix.Platform package: `CodeBrix.Android.<AddIn>.ApacheLicenseForever` (for example `CodeBrix.Android.Svg.ApacheLicenseForever` for the Svg add-in). `AGENT-README.txt` lists them.

## CodeBrix.Android supports:

* Compiling CodeBrix.Platform XAML and code-behind unchanged for Android ("paste always"), with the same XAML dialect as a CodeBrix.Platform desktop build
* Hosting a CodeBrix.Platform app in an Android activity: the app's `Application`, `OnLaunched`, `Window` and `Frame` navigation run unchanged, edge to edge, with resizing, docking and theme changes handled without restarting the activity
* The CodeBrix.Platform object model on Android: dependency properties, bindings, resources, styles, templates, Frame and Page, laid out by the same layout engine as on the desktop
* Native Android views for every element: Material 3 buttons, check boxes, switches, sliders, progress indicators, text boxes, lists and grids (RecyclerView), tabs, navigation (bottom bar, rail, drawer), app bars, dialogs, menus, flyouts, date, time and colour pickers, info bars, badges and more, with shapes, borders and gradients drawn natively
* Adaptive layout by window size class: a NavigationView becomes a bottom bar, a rail or a drawer, dialogs and menus change form, and the app switches live when a phone is docked to a desktop or a window is resized
* Material 3 theming from the app's own Fluent resources, light and dark themes, dynamic colour, and the user's font scale
* Touch, mouse, stylus and hardware keyboard input through the CodeBrix.Platform routed events, keyboard accelerators, focus, and the Android back button and predictive back gesture
* The soft keyboard: the window pans to keep the focused text field in view (the default), or the page is laid out again above the keyboard - one app-wide setting
* File pickers (the Storage Access Framework), the clipboard, sharing, the launcher, connectivity and haptics
* Build-time warnings (the CBAND diagnostics, never errors) for the CodeBrix.Platform constructs Android accepts and ignores, at the C# or XAML line that uses them
* `ApplicationData` folders and settings, preferred languages, `SoftwareBitmap` and PNG/JPEG encoding, and device and display information backed by Android
* App and library assets addressed by `ms-appx:///` URIs, including the fonts of the CodeBrix font packages
* The app's own fonts everywhere: a font family named by a page resolves to the app's default font file, never to a system font
* Application logging to logcat through Microsoft.Extensions.Logging
* Android 13 (API 33) and later

## Sample Code

### An Android head for an existing CodeBrix.Platform app

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-android36.1</TargetFramework>
    <SupportedOSPlatformVersion>33</SupportedOSPlatformVersion>
    <OutputType>Exe</OutputType>
    <ApplicationId>com.example.myapp</ApplicationId>
  </PropertyGroup>
  <!-- The app's shared XAML (App.xaml, Views/) is imported, not copied. -->
  <Import Project="..\MyApp.UI\MyApp.UI.projitems" Label="Shared" />
  <ItemGroup>
    <PackageReference Include="CodeBrix.Android.ApacheLicenseForever" />
  </ItemGroup>
</Project>
```

### The Android application and activity

```csharp
using Android.App;
using Android.Runtime;
using CodeBrix.Android.UI.Hosting;

[Application]
public class MainApplication : CodeBrixApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }

    protected override Microsoft.UI.Xaml.Application CreateApp() => new App();
}

[Activity(MainLauncher = true, Theme = "@style/Theme.Material3.DayNight.NoActionBar",
          ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
public class MainActivity : CodeBrixActivity
{
}
```

## Documentation

The NuGet package includes `AGENT-README.txt`, a complete API reference and usage guide written for AI coding agents - point your agent at that file when it is writing code against this library.

Additional sample code and usage examples are available in the samples folder:
https://github.com/ellisnet/CodeBrix.Android/tree/main/samples

## License

CodeBrix.Android is licensed under the Apache License 2.0 - see the
[LICENSE](https://github.com/ellisnet/CodeBrix.Android/blob/main/LICENSE) file.

For licensing and provenance information about the open source code included in
this package, see [THIRD-PARTY-NOTICES.txt](https://github.com/ellisnet/CodeBrix.Android/blob/main/THIRD-PARTY-NOTICES.txt).
