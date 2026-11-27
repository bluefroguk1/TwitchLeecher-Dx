Subject: Twitch Leecher DX PRs: what's open and what's next

Hi Manuel,

Thanks for merging #110–#119 so quickly, and for the nice words!

Quick update:
- #104–#109 are rebased. #107 now builds the JSON as an object with constants, and #109 has a toggle (on by default).
- #104 also fixes the new issue #120.
- I opened 7 small PRs (#121–#127): second launch fix, draggable dialogs, Avalonia patch update, unused packages, a GitHub Actions build, a Linux security pin and Open folder (#85). Each is independent.

#104–#109 touch some of the same files, so when you merge one I'll rebase the others. If that's annoying, I can also put them together in one PR.

Still waiting on my side, and I won't send any of it until most of the open ones are done:
- a UI refresh
- features that build on the open PRs (results toolbar, settings redesign, chat export, compression, tray, watched searches)
- code quality (Ninject to Microsoft DI, remove ReactiveUI, Native AOT, Avalonia 12, a command-line tool for #36)

That's about 26 small PRs. If fewer, bigger PRs are easier for you, I can group them into about 6 instead:
1. UI refresh
2. Search and download features
3. Background features (tray, watched searches, schedule)
4. Export and processing (chat export, compression, performance)
5. Code quality
6. Native AOT and Avalonia 12, as two separate PRs, so you can say no to either on its own

Or I keep them small, whatever suits you.

The full list and a combined build are in my comment on #104.

Thanks again,
[your name]
