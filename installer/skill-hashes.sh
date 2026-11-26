#!/usr/bin/env bash
# Nâng cấp đợt H1: prints the hashes of every version of every file under skills/ in the git history (all fetched
# branches), as SkillSync.Hash computes them (UTF-8 BOM dropped, CR removed, SHA-256). Paste the lines into
# SkillSync.KnownShipped (plugin/ClaudeCAD.Plugin/Core/SkillSync.cs) when a release is cut, so a machine upgraded from it
# sees its unchanged skills as "never changed by the user". Run from the repository root after `git fetch --all`.
set -euo pipefail
for rev in $(git rev-list --all -- skills/); do
  for f in $(git ls-tree -r --name-only "$rev" skills/); do
    git show "$rev:$f" | sed '1s/^\xEF\xBB\xBF//' | tr -d '\r' | sha256sum | cut -c1-64
  done
done | sort -u | sed 's/.*/            "&",/'
