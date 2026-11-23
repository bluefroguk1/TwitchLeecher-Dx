#!/bin/bash
# Run from Git Bash in the repo.
# ci.sh <ref> <name>: push <ref> (plus the pr/ci-build workflow commit if it has no workflow) as ci-check/<name>,
# so GitHub Actions builds and start-up-checks it. Delete the branch afterwards: git push origin --delete ci-check/<name>
REPO=$(git rev-parse --show-toplevel)
W="${TMPDIR:-/tmp}/tl-wt-ci-$2"
cd $REPO && git worktree remove --force $W 2>/dev/null
git worktree add -q --detach $W "$1" || exit 1
cd $W
if ! git cat-file -e HEAD:.github/workflows/build.yml 2>/dev/null; then git cherry-pick origin/pr/ci-build >/dev/null || { echo "ci pick failed"; exit 1; }; fi
git push -q -f origin HEAD:refs/heads/ci-check/$2 2>&1 | grep -v '^remote'
sha=$(git rev-parse --short HEAD); cd $REPO && git worktree remove --force $W
echo "pushed ci-check/$2 $sha"
