# Duplicate closeout question fix ? 2026-10-23

Research workspace, master branch. The satisfaction question was presented first through GameDialogue.SayLine and then repeated in SOCOCloseout's confirmation panel. The panel now owns the question and opens directly. Existing custody, final-survey and explicit-confirmation gates remain in place. Cancelling closes the panel without confirming; reopening shows the unanswered question again without queuing a narration copy.

Validation: SOCOCloseoutTest passed 76 checks with zero failures in a fresh Unity editor. Added assertions check direct panel presentation, no preceding dialogue request, duplicate open rejection, cancellation/reopening, explicit confirmation and existing once-only final milestones. Report: closeout-checks.txt. The test uses the project's isolated offline sandbox; it is not a claimed manual headset playthrough.

The initial editor run exercised stale compiled code and is not credited as validation of the fix. The next headless run crashed in an existing screenshot helper's Camera.Render call; the helper now skips captures on a Null graphics device. The final run completed successfully with all behavior assertions active. Other authored question catalog entries were reviewed; no other same text-to-confirmation sequence was changed.

Prior register UI work remains saved. No competitive changes were merged into research. This fix is local and uncommitted; the installed headset APK has not been rebuilt. Raw logs and captures remain local in ignored Temp files.
