#!/usr/bin/env bash
#
# Fails if a NEW or MODIFIED EF Core migration contains a destructive operation
# in its Up() method (the part that runs on deploy).
#
# Rationale: a migration once wiped production NAMP data via `DELETE FROM ...`
# (incident 2026-06-15). Migrations must be additive — they may add/alter tables
# and columns, but must never DELETE/TRUNCATE rows or DROP a data-bearing table
# on the way up.
#
# Escape hatch: if a destructive change is genuinely required and reviewed, append
# a trailing comment to the offending line:
#     // DESTRUCTIVE-APPROVED: <reason / ticket>
#
# Usage:
#   check-destructive-migrations.sh [BASE_REF] [HEAD_REF]
# Defaults: BASE_REF=HEAD~1, HEAD_REF=HEAD
set -euo pipefail

MIGRATIONS_DIR="src/CRMS.Infrastructure/Persistence/Migrations"
# Table/data-level destructive operations. (DropColumn is intentionally NOT here —
# review column removals carefully, but they are not table/data wipes.)
PATTERN='DELETE[[:space:]]+FROM|TRUNCATE|DROP[[:space:]]+TABLE|DropTable\('
APPROVAL='DESTRUCTIVE-APPROVED'

BASE_REF="${1:-HEAD~1}"
HEAD_REF="${2:-HEAD}"

if git rev-parse --verify --quiet "$BASE_REF" >/dev/null 2>&1; then
  changed=$(git diff --name-only --diff-filter=AM "$BASE_REF" "$HEAD_REF" -- "$MIGRATIONS_DIR" \
              | grep -Ev '\.Designer\.cs$|ModelSnapshot\.cs$' || true)
else
  echo "Base ref '$BASE_REF' not found; falling back to scanning all migration files."
  changed=$(find "$MIGRATIONS_DIR" -name '*.cs' ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' || true)
fi

if [ -z "${changed// }" ]; then
  echo "Migration safety check: no added/modified migrations to scan."
  exit 0
fi

violations=0
for f in $changed; do
  [ -f "$f" ] || continue
  # Only inspect the Up() method body; destructive ops in Down() are expected.
  up_body=$(awk '/void[[:space:]]+Up\(/{flag=1} /void[[:space:]]+Down\(/{flag=0} flag' "$f")
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    # Skip full-line comments (they describe, they don't execute).
    case "$(echo "$line" | sed 's/^[[:space:]]*//')" in
      //*) continue ;;
    esac
    if echo "$line" | grep -Eq "$PATTERN"; then
      if ! echo "$line" | grep -q "$APPROVAL"; then
        trimmed=$(echo "$line" | sed 's/^[[:space:]]*//')
        echo "::error file=$f::Destructive op in migration Up() without $APPROVAL annotation: $trimmed"
        violations=$((violations + 1))
      fi
    fi
  done <<< "$up_body"
done

if [ "$violations" -gt 0 ]; then
  echo ""
  echo "FAILED: found $violations destructive operation(s) in migration Up() method(s)."
  echo "Migrations must be additive. If the change is truly required and reviewed, append"
  echo "  // $APPROVAL: <reason / ticket>"
  echo "to each flagged line. Data cleanup belongs in a separate, manually-run script."
  exit 1
fi

echo "Migration safety check passed: no unapproved destructive operations in changed migrations."
