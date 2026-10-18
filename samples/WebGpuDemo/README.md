# WebGpuDemo

WebGpuDemo shows the WebGpu add-in on Android. It is the CodeBrix.Platform WebGpuDemo sample's four pages in an
Android head: every page hosts a `WebGpuRenderView`, which renders on its own WebGPU device (Vulkan) and shows each
frame through the SkiaSharp.Views canvas (the Readback render path) - or, with the direct GPU rendering opt-in, presents
it straight to a SurfaceView under the canvas (the DirectSurface render path; see below).

* **Triangle** - the raw WebGPU binding (CodeBrix.WebGpu.Native): a WGSL pipeline created in `DeviceReady`, an animated
  triangle recorded into the frame's encoder in `Render`, released in `DeviceLost`. The add-in is the only dependency.
* **Models** - CodeBrix.WebGpu.Graphics drawing glTF models straight into the view (the view is an
  `IWebGpuDeviceSource` and an `IWebGpuTextureTarget`): three Cube Pets animals and the Avian round a treasure chest, on
  a grass plane built in memory with CodeBrix.Graphics3D.Gltf2, lit by a procedural sky; a slider turns the camera.
* **Renderer class** - an `IWebGpuRenderer` declared as a XAML resource and handed to the view's `Renderer` property,
  with its own SkiaSharp overlay label.
* **Controls** - the frame driver (Continuous, OnDemand, Manual with a "Render one frame" button), Invalidate, Pause,
  the target frame rate, the render path, what the device is (adapter, features, limits) and a SkiaSharp HUD drawn
  in the `Overlay` event.

## Project

| Project | Target framework | What it is |
|---|---|---|
| `WebGpuDemo.Android` | net10.0-android36.1 | The Android head: `MainApplication : CodeBrixApplication`, `MainActivity : CodeBrixActivity`, the app's `App.xaml`, the copied pages under `Pages/Views/`, the copied renderers under `Rendering/`, the models under `Assets/models/`, `StartPages` (the launch intent's start page), `SampleAssets` (copies the models out of the APK on first use - the model loader reads files) and `FrameRateLog` (one logcat line a second). |

The WebGpu add-in comes from this repository, exactly as an app outside it takes the
`CodeBrix.Android.WebGpu.ApacheLicenseForever` package. The wgpu engine's native library (arm64-v8a and x86_64) comes
with it and is packed into the APK's `lib/<abi>/` folders by the build. The Models page's CodeBrix.WebGpu.Graphics and
CodeBrix.Graphics3D.Gltf2 are the app's own package references - never the add-in's.

## Build, run and check

```
dotnet build samples/WebGpuDemo/WebGpuDemo.Android -c Release
adb -s <serial> install -r samples/WebGpuDemo/WebGpuDemo.Android/bin/Release/net10.0-android36.1/com.codebrix.webgpudemo-Signed.apk
adb -s <serial> shell am start -n com.codebrix.webgpudemo/com.codebrix.webgpudemo.MainActivity --es page models
adb -s <serial> logcat -s WebGpuDemo
adb -s <serial> shell am force-stop com.codebrix.webgpudemo
```

The `page` extra picks the start page: `triangle` (the default), `models`, `renderer` or `controls`. The APK carries
both ARM64 and x64 code. `MainActivity` is single-task, as every CodeBrix main activity must be: opening the app from
the launcher after an `am start -n` (or another `am start` while it runs) shows the running app, and
`MainApplication.OnLaunchIntent` logs the later launch as `LAUNCH INTENT action=... page=...`. Built with
`-p:WebGpuDemoStandardLaunchMode=true` the activity is standard instead - only to check the host's safety net for apps
that forgot the launch mode (logcat then shows the host's single-task warning, and still the running app). `adb -s
<serial> shell monkey -p com.codebrix.webgpudemo -c android.intent.category.LAUNCHER 1` starts the app as the launcher
does. Once a second the app writes a line like

```
FRAMES page=Triangle path=Readback driver=Continuous size=1872x788 adapter=Intel(R) Xe Graphics (TGL GT2) backend=Vulkan frames=727 fps=53.5
```

and the add-in itself logs `WebGPU device ready: <adapter> (Vulkan), render path Readback.` when a view gets its device.
The Readback path copies each whole frame back from the GPU, so its frame rate falls as the view's pixel size grows;
on some phone GPUs a full-screen view renders only a few frames a second. Under the Continuous driver (the demo's
default) that copy overlaps the next frame and the picture on screen is one frame behind; the OnDemand and Manual
drivers show each frame before it ends.

## Direct GPU rendering (EXPERIMENTAL)

`MainApplication` carries the opt-in, OFF by default: `UseDirectGpuRenderingMode => true` is compiled in only when the
app is built with `-p:WebGpuDemoDirectGpu=true` (or when you uncomment the override):

```
dotnet build samples/WebGpuDemo/WebGpuDemo.Android -c Release -p:WebGpuDemoDirectGpu=true
```

Every page then renders on the DirectSurface path (`path=DirectSurface` in the FRAMES line, and the add-in logs
`DirectSurface: presenting to the window surface at <size> ...`): no frame is read back, the frame rate is the target
frame rate on the phones too, and the Controls page's HUD is drawn by the canvas above the surface. Going to the home
screen and back, and turning the device, keep the same device and continue at once.

## Where the files come from

Copied from the CodeBrix.Platform repository's `samples/CodeBrixPlatform/WebGpuDemo/` (Apache License, Version 2.0;
sample-app duplicates are intentional):

| File(s) here | Source in CodeBrix.Platform |
|---|---|
| `WebGpuDemo.Android/Pages/Views/*.xaml`, `*.xaml.cs`, `DevicePlaceholder.cs` | `src/WebGpuDemo.UI/Views/` |
| `WebGpuDemo.Android/Rendering/*.cs` | `src/WebGpuDemo.Core/Rendering/` |
| `WebGpuDemo.Android/Assets/models/**` | `src/WebGpuDemo.Core/Assets/models/` |

The Android changes are marked `Android sample:` in the files: the main page's start page comes from `StartPages`; the
main page's button row and the Controls page's first row scroll sideways on a phone held upright; the Models page loads
its models from the folder `SampleAssets` copies them to. `App.xaml` / `App.xaml.cs` are this app's (the source app's
without its generic host and console logger: on Android the head routes logging to logcat).

## Credits

The models are public domain (CC0): Cube Pets and Survival Kit by Kenney (www.kenney.nl), and the Avian from Crystals
and Avian by Jeremy Ellis. Each pack's licence file is beside its models in `WebGpuDemo.Android/Assets/models/` (and
inside the APK's assets).
