#!/bin/bash
# Run from Git Bash in the repo.
# make_pr.sh <pr-branch> <target-base-ref> <source-ref> <source-base-ref> <message-file> [ours-paths...]
# Squashes source-base..source into ONE clean commit on top of target-base:
#  - a 3-way cherry-pick, so changes the target already has (shared dependencies) merge cleanly
#  - test scripts, .claude notes and the CI helper are left out
#  - message from message-file, no AI attribution
# Paths listed after the message file take the target's version if they conflict.
set -u
PR=$1; TARGET=$2; SRC=$3; SRCBASE=$4; MSG=$5; shift 5
OURS=("$@")
REPO=$(git rev-parse --show-toplevel)
W="${TMPDIR:-/tmp}/tl-wt-pr"
cd "$REPO" || exit 1
git worktree remove --force "$W" 2>/dev/null
git worktree add -q --detach "$W" "$TARGET" || exit 1
cd "$W" || exit 1

# squash commit: source tree on top of its base
T=$(git commit-tree "$SRC^{tree}" -p "$SRCBASE" -m squash)

if ! git cherry-pick --no-commit "$T" >/dev/null 2>&1; then
  for f in $(git diff --name-only --diff-filter=U); do
    for o in ${OURS[@]+"${OURS[@]}"}; do
      if [ "$f" = "$o" ]; then git checkout -q --ours -- "$f" && git add -- "$f"; fi
    done
  done
  if [ -n "$(git diff --name-only --diff-filter=U)" ]; then
    echo "CONFLICT in $PR:"; git diff --name-only --diff-filter=U
    exit 2
  fi
fi

# keep tooling out of the PR: restore the target's version of these paths (or drop them if new)
for p in test-scripts .claude TwitchLeecher/.claude; do
  if git cat-file -e "HEAD:$p" 2>/dev/null; then
    git rm -r -q --cached --ignore-unmatch "$p"; rm -rf "$p"
    git checkout -q HEAD -- "$p"
  else
    git rm -r -q --cached --ignore-unmatch "$p"; rm -rf "$p"
  fi
done
git reset -q -- test-scripts .claude TwitchLeecher/.claude 2>/dev/null

if git diff --cached --quiet; then echo "EMPTY: $PR has no changes over $TARGET"; exit 3; fi
git commit -q -F "$MSG" || exit 4
n=$(git log -1 -p | grep -ci 'claude\|co-authored\|twitchdownloader\|lay295')
git branch -f "$PR" HEAD
echo "OK $PR $(git log -1 --format='%h %s') files=$(git show --stat --format= HEAD | tail -1) ai-refs=$n"
cd "$REPO" && git worktree remove --force "$W"
