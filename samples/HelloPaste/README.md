# HelloPaste

HelloPaste is the first CodeBrix.Android application built from pasted CodeBrix.Platform pages.
It hosts two real sample pages, copied unchanged from the CodeBrix.Samples repository, on
Android: the **JustBetweenUs** main page (text encryption) and the **PdfSideBySide** main page
(two PDF viewers side by side). The pages' XAML, their code-behind and the view models and
libraries behind them are the samples' own files; the app around them is a CodeBrix.Android
head.

In this phase CodeBrix.Android has no per-control handlers yet, so the pages are shown by the
**projection viewer**: after every layout pass the window's visual tree is mirrored as native
Android views at the positions the CodeBrix.Platform layout computed. TextBlock text, TextBox
text and FontIcon glyphs are real text views in the page's fonts; backgrounds and borders are
drawn as boxes; every other control is a labelled placeholder box (type name and key values),
and images are crossed placeholder boxes. Nothing is interactive yet.

## Projects

| Project | Target frameworks | What it is |
|---|---|---|
| `HelloPaste.Android` | net10.0-android36.1 | The Android head: `MainApplication : CodeBrixApplication`, `MainActivity : CodeBrixActivity`, the app's `App.xaml` (the union of the two source apps' resources), the two pasted pages under `Pages/`, and the on-device self-check (`SelfCheck/`). It uses the Svg add-in, which draws the JustBetweenUs page's SVG icons. |
| `JustBetweenUs.Core` | net10.0; net10.0-android36.1 | The JustBetweenUs view model, image button controls and embedded assets, multi-targeted so the desktop heads and the Android head share one library. |
| `JustBetweenUs.Encryption` | net10.0 | The encryption services (a UI-free library an Android app uses as is). |
| `PdfSideBySide.Core` | net10.0; net10.0-android36.1 | The PdfSideBySide view models, multi-targeted like JustBetweenUs.Core. |
| `PdfSideBySide.PdfRender` | net10.0 | The document, paging and zoom logic (UI-free). |

The `.Core` libraries show the application shape for Android: the app's `.Core` library targets
`net10.0` for the desktop heads and `net10.0-android36.1` for the Android head, so the desktop
CodeBrix.Platform package never reaches the APK.

## Build, run and check

```
dotnet build samples/HelloPaste/HelloPaste.Android -c Debug -t:Install "-p:AdbTarget=-s emulator-5554"
adb shell am start -n com.codebrix.hellopaste/com.codebrix.hellopaste.MainActivity --es page jbu
adb shell am start -n com.codebrix.hellopaste/com.codebrix.hellopaste.MainActivity --es page pdf
```

The `page` extra picks the start page (`jbu`, the default, or `pdf`). A Release build is
trimmed. The app writes its visual tree to logcat after each layout change and runs a
self-check 1.5 seconds after it starts: one `SELFCHECK <name> PASS|FAIL <detail>` line per check
(platform contracts, App.xaml resources, Frame navigation to the other page and back,
bindings, commands, the startup dialog, the projected native views) and a final
`SELFCHECK SUMMARY pass=N fail=M`.

`build/test-scripts/device-smoke.sh` does all of it unattended: it starts the emulator,
deploys, runs both pages in portrait and landscape, saves screenshots and tree dumps, and exits
non-zero when a check fails.

## Where the files come from

Copied VERBATIM from CodeBrix.Samples (Apache License, Version 2.0):

| File(s) here | Source in CodeBrix.Samples |
|---|---|
| `HelloPaste.Android/Pages/JustBetweenUs/Views/MainPage.xaml`, `MainPage.xaml.cs` | `JustBetweenUs/CodeBrixPlatform/JustBetweenUs.UI/Views/` |
| `HelloPaste.Android/Pages/PdfSideBySide/Views/MainPage.xaml`, `MainPage.xaml.cs` | `PdfSideBySide/src/PdfSideBySide.UI/Views/` |
| `JustBetweenUs.Core/Controls/*.cs` | `JustBetweenUs/CodeBrixPlatform/JustBetweenUs.Core/Controls/` |
| `JustBetweenUs.Core/ViewModels/MainViewModel.cs`, `EncryptionMode.cs` | `JustBetweenUs/Shared/ViewModels/` |
| `JustBetweenUs.Core/Helpers/HostHelper.cs` | `JustBetweenUs/Shared/Helpers/` |
| `JustBetweenUs.Core/Assets/*.svg`, `star_icon.json` | `JustBetweenUs/Shared/Assets/` |
| `JustBetweenUs.Encryption/RegisterServices.cs`, `Services/*.cs`, `Embedded/DefaultKey.txt` | `JustBetweenUs/JustBetweenUs.Encryption/` |
| `JustBetweenUs.Encryption/Helpers/EmbeddedResourceHelper.cs` | `JustBetweenUs/Shared/Helpers/` |
| `PdfSideBySide.Core/Helpers/`, `Services/`, `ViewModels/` | `PdfSideBySide/src/PdfSideBySide.Core/` |
| `PdfSideBySide.PdfRender/**/*.cs` except `Rendering/PageRenderer.cs` | `PdfSideBySide/src/libs/PdfSideBySide.PdfRender/` |

The project files are new (they follow the source projects; package versions are pinned
centrally in the repository's `Directory.Packages.props`). `App.xaml`, `App.xaml.cs`,
`StartPages.cs`, `MainApplication.cs`, `MainActivity.cs` and `SelfCheck/` are HelloPaste's own.

Replaced by stubs with the same public surface (each file says so in its first lines):

- `PdfSideBySide.PdfRender/Rendering/PageRenderer.cs` - the original rasterizes pages with the
  PDFium natives, which this sample does not ship for Android; the stub renders nothing (no
  document can be opened on Android yet: there is no file picker).
- `HelloPaste.Android/Stubs/Lottie/LottieVisualSource.cs` - the Lottie add-in has no Android
  flavor yet; the animation player on the JustBetweenUs page stays empty.

The JustBetweenUs application is based on, and was inspired by, a code sample provided by
Paul Ainsworth.
