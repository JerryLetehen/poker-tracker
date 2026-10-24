#!/bin/bash
# Installs a local pre-commit hook that blocks committing likely secrets. Run once:
#   ./scripts/install-git-hooks.sh
set -euo pipefail
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
HOOK="$ROOT_DIR/.git/hooks/pre-commit"

cat > "$HOOK" <<'HOOK_EOF'
#!/bin/bash
# Pre-commit secret guard: rejects staged files/content that look like secrets.
set -uo pipefail
staged=$(git diff --cached --name-only --diff-filter=ACM)
[ -z "$staged" ] && exit 0

bad=0
# Block secret-like filenames (allow *.example templates).
while IFS= read -r f; do
  case "$f" in
    *.example) continue;;
  esac
  if echo "$f" | grep -qE '(^|/)\.env($|\.)|appsettings\.(Staging|Production)\.json$|client_secret.*\.json$|poker-data-.*\.json$|\.(pem|key|pfx|p12)$'; then
    echo "pre-commit: refusing to commit secret-like file: $f"
    bad=1
  fi
done <<< "$staged"

# Block secret-like content in staged diffs.
if git diff --cached -U0 | grep -nE '[0-9]{6,}:[A-Za-z0-9_-]{30,}|BEGIN [A-Z ]*PRIVATE KEY' >/dev/null; then
  echo "pre-commit: refusing to commit — staged changes contain a token/private-key pattern."
  bad=1
fi

if [ "$bad" -ne 0 ]; then
  echo "Remove the secret (use an env var / .env file) and try again. Override with 'git commit --no-verify' only if you are sure."
  exit 1
fi
exit 0
HOOK_EOF

chmod +x "$HOOK"
echo "Installed pre-commit secret guard at .git/hooks/pre-commit"
