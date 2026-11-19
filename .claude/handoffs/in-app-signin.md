# In-app Twitch sign-in + sub-only in one login

Branch: `claude/in-app-signin` (from `master`, own upstream PR). Not compiled yet: the Mac has no .NET SDK.

**Test needs:** a Twitch account (sign-in only). Optional last step: a subscriber-only VOD of a channel you are subscribed to.
Run: `powershell -ExecutionPolicy Bypass -File test-scripts\test-in-app-signin.ps1`

## What changed
- **Sign-in window** (`TwitchLeecher.Gui/Views/TwitchSignInWindow.axaml(.cs)`): a `NativeWebView` opens Twitch's OAuth authorize URL (`Constants.AuthorizeUrl(state)`). On navigation to `http://localhost:9342` it takes `access_token` from the fragment, checks `state`, and cancels the navigation. No listener is used on this path.
- **Sub-only in the same login:** checkbox "Also enable sub-only downloads" on the login screen. When ticked, the window polls the cookie store every second for `auth-token` on `.twitch.tv`. If the cookie is missing after OAuth, it opens `https://www.twitch.tv/` once; if Twitch shows signed-out there, the user can sign in in the same window and polling picks it up. The token goes through `AuthService.ValidateAuthentication(token, true)`. That checks the client id, stores the token and publishes `SubOnlyAuthChangedEvent`. The old `sub_token` path only set it in memory. It now does the same validate-and-store.
- **Sub-only screen:** "Connect sub-only" button, which opens the same window on `twitch.tv/login` and reads only the cookie. The console script is now a collapsed "Manual method". It expands automatically when there is no web view.
- **No web view at runtime** → `IDialogService.ShowTwitchSignIn` returns null → the old browser + localhost listener flow.
- **Security**
  - Tokens in `runtime.xml` are DPAPI-encrypted (CurrentUser) on Windows, marked `Protected="DPAPI"`. `AccessTokenSubOnly` was never saved before, so sub-only was lost on restart; now it is saved, encrypted. Linux keeps plain text, but `runtime.xml` is chmod 600 (`// shortcut:` in `AuthInfo.cs`).
  - Migration: an element without the attribute is read as plain text (old version) and re-saved encrypted on the next save. Startup validation saves, so this happens on the first start. Rollback: an older build reads the encrypted text as a token, validation fails, and the user signs in again. No data is lost.
  - OAuth `state` is a one-time random value (`Constants.NewOneTimeCode`), checked in the window and in the fallback listener.
  - Fallback listener: `IAuthListener.StartListenForToken()` now returns a one-time code. It accepts `access_token` only with `state=code`, and `sub_token` only with `code=code`; anything else gets a 403. The manual script embeds the code.
  - No token is logged anywhere.
- **Sign-out:** `AuthService.RevokeAuthentication` already revokes and clears both tokens, and nothing is saved after that. The web view runs a private session (WebView2 InPrivate; WebKitGTK ephemeral), and the window deletes all its cookies when it closes. This is needed because WebView2 keeps one environment per app run. So no web cookies survive past the window, and sign-out has nothing left to clear.

## Web view choice
- **`Avalonia.Controls.WebView` 11.4.1** (NuGet, MIT, by the Avalonia team; needs Avalonia ≥ 11.1, so it works on 11.3.11).
  - Uses the OS engine: WebView2 on Windows, WebKitGTK on Linux (also WKWebView on macOS).
  - Has `NavigationStarted` with `Cancel`, plus `TryGetCookieManager()`.
  - It used to be the paid "Avalonia Accelerate" WebView; it went MIT in 11.4.
- **Avalonia 12:** the same control continues as `Avalonia.Controls.WebView` 12.x (MIT), still a separate package, not part of core Avalonia. When the app moves to Avalonia 12, bump the package; the API is the same.
- **Rejected:** CefGlue-based web views (bundle Chromium, about 100 MB+), and `WebView.Avalonia` (MicroSugar; last release 11.0.0.1, unmaintained).
- **Download size:** about +1.4 MB (`Avalonia.Controls.WebView.dll`) plus about 30 KB (`System.Security.Cryptography.ProtectedData`). No browser is bundled.
  - The WebView2 runtime ships with Windows 10/11.
  - Linux needs the system package `libwebkit2gtk-4.1` (or 4.0).
- **Linux:** the package's WebKitGTK adapter has no cookie API. Since `auth-token` is not HttpOnly (the old console script reads it with `document.cookie`), the window reads `document.cookie` on a twitch.tv page instead. If WPE is installed, it is skipped and GTK is forced, so one path is tested.
- **Availability check:** `WebViewAdapterInfo.GetAdapterInfo(WebView2 / WebKitGtk).IsInstalled`, checked before opening the window. This matters because a missing native lib inside the control's async init would crash the app.

## Which page sets `auth-token`: NOT verified yet
- I couldn't sign in to Twitch from here.
- What's known: the cookie is on domain `.twitch.tv` and is set by the Twitch website login (passport). The old console method read it on `www.twitch.tv`.
- Whether the OAuth login page on `id.twitch.tv` also sets it is open. The code handles both cases.
- **The test script records it.** In step "auth-token set by OAuth login":
  - y = the window closed straight after OAuth, so the OAuth login sets it;
  - n = it went to the twitch.tv home page first, so only www.twitch.tv sets it.
- Write the answer here after testing.

## Risks / check on the PC
- Not compiled. Likely spots for small compile errors:
  - `TwitchSignInWindow.axaml.cs`: Avalonia.Controls.WebView types, namely `WindowsWebView2EnvironmentRequestedEventArgs` and `WebViewAdapterInfo` in `Avalonia.Platform`, and `WebViewNavigationStartingEventArgs` in `Avalonia.Controls`.
  - The `NativeWebView` XAML element (it is in the default Avalonia xmlns).
- WebView2 `UserDataFolder` = `%AppData%\Twitch Leecher DX\WebView2`. The default (next to the exe) may be read-only.
- Validation runs on the UI thread, a short freeze. The startup check already did this.

## Conflicts
- **`claude/login-page-and-icon-8b7b57`** (friendlier HTML callback page, window to front after sign-in): conflicts in `TwitchLeecher.Services/Services/AuthListener.cs`. When merging:
  - keep this branch's one-time-code checks and its `Respond` helper;
  - put that branch's `WriteSignedInPage()` HTML in place of the success text;
  - that page now only shows in the browser fallback.
  - The bring-to-front still makes sense: it listens to `AuthResultEvent`, which this branch still publishes.
- `AuthView.axaml` / `SubOnlyView.axaml`: may conflict with the UI redesign branches if they touch these views.
- `ui-theme-keys` commit f001555 is cherry-picked here, for `AppBackgroundBrush`, `TextMutedBrush` and `DividerBrush`. If `ui-theme-keys` merges first, git sees the same change and it is a no-op.
