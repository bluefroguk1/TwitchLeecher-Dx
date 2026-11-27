<!-- title: Remove unused package and framework references -->
## Summary

Drops System.Data.SQLite, System.ComponentModel.Annotations, the Upgrade
Assistant analyzers, stale HintPath references into a local NuGet cache,
and the unused ASP.NET Core framework reference. Nothing in the code uses
them, and the SQLite native files and ASP.NET runtime made builds larger.

## Depends on

—

## Testing

Builds on Windows and Linux (GitHub Actions). Not yet smoke-tested on Windows (sign-in, search, download).
