<!-- title: Sign in to Twitch inside the app, with sub-only in the same login -->
## Summary

- The Twitch login opens in a window inside the app (web view) instead of
  the browser; the OAuth redirect is caught there, with a one-time state
  check. "Also enable sub-only downloads" reads the twitch.tv session token
  in the same login, so the manual browser-console step is no longer needed.
- Tokens are kept in protected storage; signing out clears the web session.
- The app checks both tokens with Twitch at start-up and every hour, and if
  Twitch rejects one, offers to log out and sign in again (offline is never
  treated as rejected).
- Subscriber-only videos show "This video is for subscribers only."
- The browser flow remains as a fallback where no web view is available,
  with a one-time code on the local listener.

## Depends on

the theme colours PR

Until those are merged this PR also shows their changes; please review the last commit only.

## Testing

Not yet tested on Windows.
