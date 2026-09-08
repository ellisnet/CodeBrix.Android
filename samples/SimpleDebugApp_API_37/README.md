# SimpleDebugApp

A small .NET Android application whose only job is to give the debugger something to stop on.
It has no CodeBrix.Android dependency and no package references, so it isolates one question:
can this workstation build, deploy, attach to, and step through a .NET app running on an
Android device or emulator.

## What is in it

Five buttons, each a different debugger scenario, with the handler named after it in
`MainActivity.cs` and the work done in `Calculator.cs` (pure C#, no Android types):

| Button | Handler | What to try |
|---|---|---|
| Count | `OnCountClicked` | A plain breakpoint on the UI thread; inspect `_count` and `description`. |
| Compute primes | `OnComputeClicked` | Step into `Calculator.PrimesBelow`; watch `composite`, `candidate`, `found`. |
| Async delay | `OnAsyncClickedAsync` | A breakpoint after the `await` lands in the continuation. |
| Background work | `OnBackgroundClicked` | Pause inside `Calculator.SumOfSquares` on a worker thread; then the marshalled update. |
| Throw and catch | `OnThrowClicked` | First-chance exception stop in `Calculator.ThrowForDemo`, then the catch block. |

The activity has a fixed Java name, `com.codebrix.simpledebugapp_net11.MainActivity`, so it can be
launched from a shell without looking up a generated class name.

## Prerequisites

The CodeBrix.Android developer prerequisites: the latest Android Studio (for the SDK, emulator,
and Logcat), the latest .NET SDK, and the latest `android` workload. A .NET debugger client is
also needed; Android Studio does not debug .NET code.

## Build and deploy from a shell

Debug builds use fast deployment: the APK carries no managed assemblies, and the SDK's Install
target pushes them. A raw `adb install` of the Debug APK produces an app that exits silently at
startup. Use the Install target:

```
dotnet build -c Debug -t:Install -p:AdbTarget="-s <device serial>"
adb -s <device serial> shell am start -W -n com.codebrix.simpledebugapp_net11/.MainActivity
```

Find the serial with `adb devices -l`. Release builds embed the assemblies and can be installed
either way. To see the screen from a shell:

```
adb -s <device serial> exec-out screencap -p > screen.png
```

Log lines from the app carry the tag `SimpleDebugApp`.

## Debugging

The Debug build is marked debuggable, carries portable symbols, and runs on the Mono runtime,
whose soft debugger is what .NET Android debugger clients attach to. Open the project in a
.NET IDE that supports Android debugging, select the device, put a breakpoint in one of the
handlers, and start a debug session. Attaching from a shell is not covered here.
