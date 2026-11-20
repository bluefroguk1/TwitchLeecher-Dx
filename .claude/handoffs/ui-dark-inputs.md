# ui-dark-inputs (UI redesign, branch 1)

Branch: `claude/ui-dark-inputs-0028fd`, from `master`.

**Depends on: ui-theme-keys** (commit f001555). This branch is rebased on it and uses its brushes. Merge that one first.

## What changed
- `App.axaml`
  - Global styles for TextBox, ComboBox, NumericUpDown and CalendarDatePicker, using the shared brushes `InputBackgroundBrush`, `InputBorderBrush`, `InputBorderActiveBrush` and `InputForegroundBrush` from ui-theme-keys:
    - 1px border, corner radius 6, MinHeight 36;
    - the active border on hover and focus.
    - **New**: `#16161A` fill, `#44444D` border, purple `#9146FF` active border.
    - **Original**: `#2A2A2A` fill, `#787878` border, amber `#FFC864` active border.
  - SimpleTheme's grey `/template/ Border#border` hover/focus rules are overridden.
  - The inner `PART_TextBox` of NumericUpDown and CalendarDatePicker is made borderless, so there's no double box.
  - CheckBox and RadioButton get the input fill, with the active border on hover and when checked.
  - The input styles sit before the existing class styles (`TlScrollingTextBox`, `TlMultilineTextBox`, `TlTimeUpDown`), so those still win.
  - No hard-coded colours; nothing sits on an accent background, so `AccentForegroundBrush` isn't needed.
  - `GlobalFontEmphasizeSize` changed from 18 to 16 (currently unused).
- Font scale is now 12/14/16/20/24:
  - 21.333 and 21 → 20 (page and section titles);
  - 14.667 → 14 ("Clear" links);
  - 11 → 12 (muted-parts warning);
  - 36 → 24 ("Loading...").
  - The MessageBox icon stays at 32, because it's an icon glyph, not text.

## Contrast (WCAG)
- New: text `#F2F2F5` on `#16161A` is 16.2:1; the purple active border is 3.9:1.
- Original: white on `#2A2A2A` is 14.4:1; the amber active border is 9.4:1.
- The New resting border `#44444D` on `#16161A` is only 1.9:1. It's from the design.
  - Lighten `InputBorderColor` in ui-theme-keys to about `#6A6A75` if strict WCAG 1.4.11 is wanted.

## Risks / not verified
- Not compiled: there's no .NET SDK on the Mac.
- Template part names (`Border#border`, `Ellipse#border`, `PART_TextBox`) come from Avalonia 11 SimpleTheme from memory.
  - A wrong name only means that rule doesn't match, so the grey hover border shows. It doesn't crash.
- The dropdown list (popup) of ComboBox and CalendarDatePicker keeps SimpleTheme's dark popup.
- MinHeight 36 can make some rows a few px taller. That's intended by the design.

## How to test
`powershell -ExecutionPolicy Bypass -File test-scripts\test-ui-dark-inputs.ps1`

**Test needs: instant (UI only)**
