================================================================================
EXTRAS-README: CodeBrix.Android
Samples, tools and other content in this repository that is not part of a NuGet
package
================================================================================

Nothing listed below is included in the CodeBrix.Android.ApacheLicenseForever
packages.


BUILD TOOLING
=============
    build/intake/CodeBrix.Android.Intake.proj
    build/intake/CodeBrix.Android.IntakeGate/
    build/intake/gates/

The CodeBrix.Platform intake: downloads the pinned CodeBrix.Platform build,
extracts the assemblies and build files CodeBrix.Android re-ships, writes a
manifest and runs the intake gates. The gate tool is a small console program
used only by the intake project. See MAINTAINER-README.txt (INTAKE).


TEST PROJECTS AND SCRIPTS
=========================
    tests/CodeBrix.Android.IntakeGate.Tests/
    tests/CodeBrix.Android.UI.Tests/
    tests/PasteAlways/
    tests/CodeBrix.Android.UIReqs.Device/
    tests/CodeBrix.Android.UIReqs/
    tools/UIReqsFrameCompare/
    build/test-scripts/paste-always-compile.sh
    build/test-scripts/device-smoke.sh
    build/test-scripts/android-uireqs-avd.sh
    build/test-scripts/android-uireqs-run.sh
    build/test-scripts/compare-uireqs-frames.sh

xUnit v3 tests of the intake gate tool and of CodeBrix.Android (host-free); the
paste-always compile heads (one throw-away Android head per corpus page, built
by paste-always-compile.sh); the device smoke test (device-smoke.sh); the
UIReqs suite on an Android emulator (the CodeBrix.Platform UIReqs scenarios,
copied: a scenario app on the emulator plus a Reqnroll host runner, run by
android-uireqs-run.sh, frames compared by compare-uireqs-frames.sh). See
MAINTAINER-README.txt (TESTING, UIREQS).


HELLOPASTE SAMPLE
=================
    samples/HelloPaste/

The first CodeBrix.Android app built from pasted CodeBrix.Platform pages: the
JustBetweenUs and PdfSideBySide main pages from CodeBrix.Samples, unchanged, with
their view models in multi-targeted .Core libraries, shown on Android by the
element handlers (native views at Core's layout), with an on-device self-check. Its README.md lists the source
of every copied file.


STANDALONE DEBUGGING SAMPLES
============================
    samples/SimpleDebugApp_API_36/     (net10.0-android36.1)
    samples/SimpleDebugApp_API_37/     (net11.0-android37.0)

Two plain .NET Android apps (no CodeBrix code) used to check that building,
deploying and debugging Android apps works on a development machine. They are
standalone: they are NOT in CodeBrix.Android.slnx, and the repository-wide
Directory.Build.props / Directory.Packages.props deliberately leave them alone.
Build each one on its own with its own SDK (the API 37 one needs .NET 11); each
folder has its own README.md.
