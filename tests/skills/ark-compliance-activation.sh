#!/usr/bin/env bash
set -euo pipefail

skill_file="${1:-agents-plugins/ark-csharp/.apm/skills/ark-compliance/SKILL.md}"

grep -Eq '^description:.*ARKPII\*.*(MSBuild|analyzer)' "$skill_file"
