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
