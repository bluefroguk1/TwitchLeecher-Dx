<!-- title: Add named theme colours for the UI refresh -->
## Summary

Every theme now defines one set of named colours (backgrounds, cards,
dividers, inputs, text, accent, status) as Color and SolidColorBrush
resources, so views can use DynamicResource keys instead of fixed hex
values and follow the selected theme. Brushes use literal colours in each
theme: a brush whose Color is a DynamicResource stops resolving after
SetTheme refills the application resources.

## Depends on

—

## Testing

Tested on Windows together with the UI PRs that use it, switching between the New and Original themes.
