#!/usr/bin/env bash
# Oppretter etikettene og alle issuene i issues/ i GitHub-repoet du står i.
# Krever gh CLI innlogget med tilgang til repoet.
#
# Rekkefølgen er fast, slik at issue-numrene er forutsigbare:
#   1-5   feilene        (P1)
#   6-25  lag og funksjoner (P2 og P3)
#
# Linje 1 i hver fil er tittelen, linje 2 er etikettene (komma mellom),
# linje 3 er tom, og resten er teksten.
#
# Bruk: scripts/opprett-issues.sh [owner/repo]
set -euo pipefail

repo="${1:-}"
repo_flag=()
if [[ -n "$repo" ]]; then
  repo_flag=(--repo "$repo")
fi

here="$(cd "$(dirname "$0")/.." && pwd)"

lag_etikett() {
  gh label create "$1" --color "$2" --description "$3" "${repo_flag[@]}" 2>/dev/null || true
}

lag_etikett agent        0E6D69 "Agenten kan ta denne"
lag_etikett bug          D73A4A "Noe er i stykker"
lag_etikett enhancement  A2EEEF "Ny funksjonalitet"
lag_etikett P1           B60205 "Kartet er i stå. Tas først."
lag_etikett P2           D93F0B "Produktet er ikke ferdig uten."
lag_etikett P3           FBCA04 "Gjør det bedre."

for fil in "$here"/issues/feil-*.md "$here"/issues/feature-*.md; do
  tittel="$(sed -n 1p "$fil")"
  etiketter="$(sed -n 2p "$fil")"
  body="$(tail -n +4 "$fil")"

  etikett_flagg=()
  IFS=',' read -ra deler <<< "$etiketter"
  for e in "${deler[@]}"; do
    e="$(echo "$e" | tr -d '[:space:]')"
    [[ -n "$e" ]] && etikett_flagg+=(--label "$e")
  done

  echo "Oppretter: [$etiketter] $tittel"
  gh issue create --title "$tittel" "${etikett_flagg[@]}" --body "$body" "${repo_flag[@]}"
done

echo
echo "Ferdig. Issue 1-5 er feilene (P1), 6-25 er lag og funksjoner."
