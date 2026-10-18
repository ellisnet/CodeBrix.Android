; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

; The analyzer's own release line, tracked here independently of the date-stamped package versions (1.0, then 1.1, ...).

## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CBAND0001 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0002 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0003 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0004 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0005 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0006 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0007 | CodeBrix.Android | Warning | CbandDescriptors
CBAND0008 | CodeBrix.Android | Warning | CbandDescriptors

## Release 1.1

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
CBAND0001 | CodeBrix.Android | Info | CodeBrix.Android | Warning | CbandDescriptors: an app ControlTemplate is honoured (shown as written), not ignored; information, and reworded to say so
