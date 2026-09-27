# CodeBrix.Android samples

| Sample | What it shows |
|---|---|
| [HelloPaste](HelloPaste/) | Two CodeBrix.Platform application pages - the JustBetweenUs and PdfSideBySide main pages from the CodeBrix.Samples repository - pasted unchanged into an Android app. Every element is a native Android or Material Components view; the view models live in `.Core` libraries that target both the desktop and Android, the application shape a CodeBrix.Platform app uses to add an Android head. Its README lists where every file comes from and how to build, deploy and check it. |
| [SimpleDebugApp_API_36](SimpleDebugApp_API_36/), [SimpleDebugApp_API_37](SimpleDebugApp_API_37/) | Two plain .NET Android apps with no CodeBrix code, used to check that building, deploying and debugging an Android app works on a development machine. They are not part of `CodeBrix.Android.slnx` and build on their own. |

A new application gets its Android head from the CodeBrix.Platform application template: the head's
files are in [`templates/AndroidHead`](../templates/AndroidHead/), and a minimal head is shown in the
repository's [README.md](../README.md) and in `AGENT-README.txt` (APP SHAPE).

Every sample and test app in this repository that uses CodeBrix.Android consumes it from the repository
itself (`build/inrepo/`), exactly as an app outside it consumes the `CodeBrix.Android.ApacheLicenseForever`
package.
