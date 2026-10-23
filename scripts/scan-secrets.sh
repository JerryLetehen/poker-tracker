#!/bin/bash
# Scan the ENTIRE git history (all commits, all branches) for likely secrets, using only
# git plumbing — no external tools required. Reports findings; exits non-zero if any.
#
# Usage: scripts/scan-secrets.sh
#
# What it checks across every blob in history:
#   - Secret-ish FILENAMES that should never be committed (.env, appsettings.Staging/
#     Production.json, credential JSON, private keys, SQLite db files).
#   - Secret-ish CONTENT patterns (Telegram bot tokens, private-key headers, generic
#     api_key/secret/password assignments).
#
# If this script (or a dedicated scanner like gitleaks/trufflehog) reports anything,
# scrub it from history before going public and ROTATE the exposed credential.
set -uo pipefail

RED=$'\033[31m'; GREEN=$'\033[32m'; NC=$'\033[0m'
findings=0

echo "== Scanning history for secret-like FILENAMES =="
# List every path that ever existed in history.
paths=$(git log --all --pretty=format: --name-only --diff-filter=A 2>/dev/null | sort -u | sed '/^$/d')
bad_paths=$(printf '%s\n' "$paths" | grep -E \
  -e '(^|/)\.env($|\.)' \
  -e 'appsettings\.(Staging|Production)\.json$' \
  -e 'client_secret.*\.json$' \
  -e 'poker-data-.*\.json$' \
  -e '\.(db|pem|key|pfx|p12)$' \
  | grep -vE '\.example$' || true)
if [[ -n "$bad_paths" ]]; then
  echo "${RED}Potential secret files found in history:${NC}"
  printf '  %s\n' $bad_paths
  findings=1
else
  echo "${GREEN}No secret-like filenames in history.${NC}"
fi

echo ""
echo "== Scanning history for secret-like CONTENT =="
# Telegram bot token pattern: digits:35-ish base64url. Also private keys and generic creds.
# -G matches added/removed lines across all of history.
patterns=(
  '[0-9]{6,}:[A-Za-z0-9_-]{30,}'          # Telegram bot token
  'BEGIN [A-Z ]*PRIVATE KEY'              # PEM private key
  '(api[_-]?key|secret|password|token)[[:space:]]*[=:][[:space:]]*[A-Za-z0-9/_+-]{12,}'
)
for p in "${patterns[@]}"; do
  hits=$(git log --all -G"$p" --oneline 2>/dev/null | head -20 || true)
  if [[ -n "$hits" ]]; then
    echo "${RED}Pattern matched in history: /$p/${NC}"
    printf '  commit %s\n' $(echo "$hits" | awk '{print $1}')
    findings=1
  fi
done
[[ $findings -eq 0 ]] && echo "${GREEN}No secret-like content patterns in history.${NC}"

echo ""
if [[ $findings -eq 0 ]]; then
  echo "${GREEN}CLEAN: no obvious secrets found in git history.${NC}"
  exit 0
else
  echo "${RED}FINDINGS above. Do NOT make the repo public until these are scrubbed and any real credential is rotated.${NC}"
  echo "To scrub: install git-filter-repo and remove the offending paths/strings from all history, then re-run this scan."
  exit 1
fi
