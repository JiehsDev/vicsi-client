# Adaptive dialogue layout

Request: make dialogue flexible according to its text and readable.

Implementation: use a shared content measurement layout for the upper dialogue and lower tooltip/prompt. Keep the authored width as the maximum line length, reduce short backgrounds to a 240-unit minimum width, wrap at a font size of at least 24, and fit height to the complete text with 24-unit horizontal padding and at least 16 units above/below. Keep the edge facing the companion channel fixed. Normalize the two authored background rectangles to the measured panel, preserving the countdown inside the lower padding. Preserve queue, countdown, dismiss, priority and history semantics.

The old 240-unit height cap is retained only as a hidden serialized legacy setting. Content may grow beyond it instead of drawing the last lines outside the panel. This task does not add scrolling or pagination; unusually large externally supplied passages can therefore create tall panels.

Validation: bounds checks for short/long text, newlines, rich catalog text and unbroken words; repeat-fit stability and shrinking; live dialogue and tooltip/prompt transitions; render a short/long comparison; regress catalog timing/queues and history. Verify saved scene and catalog remain unchanged. Physical headset readability remains a device check.

Placement validation uses DialoguePlacementTest with the real presenters and a standing camera in empty space, then a wall fixture. It releases the identification gate and suspends unrelated guidance producers. The old broad UIComfortInputTest has a stale Evidence Notes component assumption; an initial extracted prefix also reported 18 input/placement failures. Those attempts are retained, its source is restored to the pre-task bytes, and they are not claimed as passing regressions.
