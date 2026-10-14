Feature: The save picker's compatibility path writes back
	Android-only. A document the save picker creates is a content URI; desktop code writes the StorageFile's Path with
	System.IO, so the framework gives it a cache file whose content is copied back to the document whenever the app
	finishes writing it. Code written for the desktop heads often deletes the picker's empty placeholder first (so its
	own "replace?" prompt does not fire), or writes a temporary file and renames it into place.

# [AP8-S batch 2] PainDiagram: the saved PNG never reached Downloads (0 bytes) - the watch was on the deleted placeholder.
Scenario: A compatibility file deleted and written again still reaches its document
	Given a Downloads document "ap8s2_writeback.png" created for saving
	When the app deletes the compatibility file and, a second later, writes 4096 bytes to it
	Then the document holds 4096 bytes

# [2026-10-12 LAUNCH] InannaOctaquetra: "Open" on the "Report saved" panel did nothing - the app launched the saved
# report's compatibility path as a file:// URI, which Android refuses to hand to another app.
Scenario: Launching a saved document's compatibility path opens the document itself
	Given a Downloads document "launch_report.pdf" created for saving
	When the app asks the launcher to open the compatibility path as a file URI
	Then the launcher views the document's content URI with read permission granted

Scenario: Launching one of the app's own files shares it through the CodeBrix file provider
	Given an app file "launch_notes.txt" holding 64 bytes
	When the app asks the launcher to open that file as a file URI
	Then the launcher views a CodeBrix file provider URI with read permission granted
