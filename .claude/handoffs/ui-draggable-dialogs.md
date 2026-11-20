# ui-draggable-dialogs: pop-up message boxes can be dragged

**Branch:** `ui-draggable-dialogs` (from `master`)
**Depends on:** nothing
**Test needs:** instant (UI only)
**Test script:** `test-scripts/test-ui-draggable-dialogs.ps1`

## Why it couldn't be dragged
The app opens two pop-up windows (`DialogService`):
- `MessageBoxWindow` sets `ExtendClientAreaToDecorationsHint="True"`. Its content fills the whole window, including the
  title bar area, so on Windows there was nothing left to grab. This is the one the note is about.
- `UpdateInfoView` keeps the normal system title bar, so it was already draggable. Left as is.

## What changed
- New `TwitchLeecher.Gui/Views/WindowDrag.cs`: attached property `views:WindowDrag.IsHandle="True"`.
  A left-button press on the handle calls `Window.BeginMoveDrag`. Presses that land on a `Button`, `TextBox` or
  `ScrollBar` inside the handle are ignored, so those keep working.
- `MessageBoxWindow.axaml`: the root `Border` is the handle, so the whole box (caption, icon, message) drags.
  Buttons and the message scroll bar are excluded by the rule above.
- Nothing focusable was added, so Tab order, keyboard and screen-reader behaviour are unchanged.

## Known limits
- Not compiled on the Mac (no SDK). Watch the first build for `AvaloniaProperty.RegisterAttached<WindowDrag, Control, bool>`
  and the `Changed.AddClassHandler` call.
- On Linux `ExtendClientAreaToDecorationsHint` is ignored, so the box also has a system title bar there; both work.

## Found, not fixed (out of scope)
- `UpdateInfoView`'s constructor never calls `InitializeComponent()`, so the update dialog probably opens empty.

## Merge notes
Touches only `MessageBoxWindow.axaml` (2 lines) and adds one new file. No overlap expected with other branches
unless one restyles `MessageBoxWindow`.
