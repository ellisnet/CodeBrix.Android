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
used by the intake project and, in its "packages" mode, by the pack driver (the
package gates). See MAINTAINER-README.txt (INTAKE, PACKAGING / PUBLISHING).


PACKAGING TOOLING
=================
    build/pack.sh
    build/nuget/CodeBrix.Android.Pack.proj
    build/nuget/CodeBrix.Android.PackInfo.targets
    build/nuget/pack-shim/
    build/nuget/package-dependency-owners.txt

The pack driver and its helpers: build/pack.sh builds the solution and packs
every package (the framework nuspec build/nuget/CodeBrix.Android.ApacheLicenseForever.nuspec
and one generated nuspec per add-in) into artifacts/packages/, then runs the
three package gates (the gate tool's "packages" mode). See MAINTAINER-README.txt
(PACKAGING / PUBLISHING). The consumer build logic under build/nuget/buildTransitive/
IS shipped, inside the framework package.


APPLICATION TEMPLATE HEAD
=========================
    templates/AndroidHead/
    templates/TEMPLATE_INTEGRATION.md

The Android head of the CodeBrix.Platform application template (the files a new
application's src/<Name>.Android/ gets, plus the two-target-framework .Core
project) and the list of what CodeBrix.Develop's template and the application
skill need to offer it. Not built by the solution.


TEST PROJECTS AND SCRIPTS
=========================
    tests/CodeBrix.Android.IntakeGate.Tests/
    tests/CodeBrix.Android.UI.Tests/
    tests/PasteAlways/
    tests/CodeBrix.Android.UIReqs.Device/
    tests/CodeBrix.Android.UIReqs/
    tests/CodeBrix.Android.Analyzers.Tests/
    tests/CodeBrix.Android.ParityScore.Tests/
    tools/UIReqsFrameCompare/
    tools/CodeBrix.Android.ParityScore/
    build/test-scripts/paste-always-compile.sh
    build/test-scripts/parity-score.sh
    build/test-scripts/device-smoke.sh
    build/test-scripts/android-uireqs-avd.sh
    build/test-scripts/android-uireqs-run.sh
    build/test-scripts/compare-uireqs-frames.sh

xUnit v3 tests of the intake gate tool and of CodeBrix.Android (host-free); the
paste-always compile heads (one throw-away Android head per corpus app, every
page, built by paste-always-compile.sh, which also counts the CBAND warnings);
the parity-score tool and its tests (parity-score.sh; the report goes to
artifacts/parity/); the tests of the CBAND analyzer; the device smoke test (device-smoke.sh); the
UIReqs suite on an Android emulator (the CodeBrix.Platform UIReqs scenarios,
copied: a scenario app on the emulator plus a Reqnroll host runner, run by
android-uireqs-run.sh, frames compared by compare-uireqs-frames.sh). See
MAINTAINER-README.txt (TESTING, UIREQS).


SAMPLES
=======
    samples/README.md

The samples folder's map (the samples below).


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
