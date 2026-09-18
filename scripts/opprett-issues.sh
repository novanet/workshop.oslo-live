#!/usr/bin/env bash
# Oppretter etikettene og alle issuene i issues/ i GitHub-repoet du står i.
# Krever gh CLI innlogget med tilgang til repoet.
#
# Rekkefølgen er fast, slik at issue-numrene er forutsigbare:
#   1-5    feilene           (P1)
#   6-19   kjernelagene
#   20-    alt annet
#
# Linje 1 i hver fil er tittelen, linje 2 er etikettene (komma mellom),
# linje 3 er tom, og resten er teksten.
#
# Skriptet kan kjøres om igjen. Issuer som finnes fra før blir ikke laget på
# nytt, men får de etikettene fila sier de skal ha - så nye etiketter, som
# poengene, havner også på gamle issuer.
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
  gh label create "$1" --color "$2" --description "$3" --force "${repo_flag[@]}" >/dev/null 2>&1 || true
}

# Ingen felles agent-etikett. Hver agent har sin egen, mini-<navn>, og den
# lages av scripts/registrer-webhooks.sh i kursrepoet.
lag_etikett bug          D73A4A "Noe er i stykker"
lag_etikett enhancement  A2EEEF "Ny funksjonalitet"
lag_etikett P1           B60205 "Kartet er i stå. Tas først."
lag_etikett P2           D93F0B "Produktet er ikke ferdig uten."
lag_etikett P3           FBCA04 "Gjør det bedre."

# Vanskelighet. Så mange poeng får den som lukker issuen.
lag_etikett "1 poeng"    E8F5E9 "Liten og avgrenset. Du ser hvor det er."
lag_etikett "2 poeng"    A5D6A7 "Ett lag eller én endring med en kjent form."
lag_etikett "3 poeng"    66BB6A "Krever at du finner ut av noe først."
lag_etikett "5 poeng"    2E7D32 "Flere kilder, et nytt mønster, eller lagring."
lag_etikett "8 poeng"    1B5E20 "Åpen oppgave. Du må ta en beslutning og begrunne den."

# Nummer og etiketter for det som allerede finnes, nøkkel på tittel.
finnes="$(gh issue list --state all --limit 500 --json number,title \
  --jq '.[] | "\(.number)\t\(.title)"' "${repo_flag[@]}" 2>/dev/null || true)"

for fil in "$here"/issues/feil-*.md "$here"/issues/feature-*.md; do
  tittel="$(sed -n 1p "$fil")"
  etiketter="$(sed -n 2p "$fil")"
  body="$(tail -n +4 "$fil")"

  etikett_flagg=()
  IFS=',' read -ra deler <<< "$etiketter"
  for e in "${deler[@]}"; do
    e="$(echo "$e" | sed 's/^ *//; s/ *$//')"
    [[ -n "$e" ]] && etikett_flagg+=(--add-label "$e")
  done

  nummer="$(printf '%s\n' "$finnes" | awk -F'\t' -v t="$tittel" '$2 == t { print $1; exit }')"

  if [[ -n "$nummer" ]]; then
    gh issue edit "$nummer" "${etikett_flagg[@]}" "${repo_flag[@]}" >/dev/null
    echo "Oppdatert #$nummer: [$etiketter] $tittel"
    continue
  fi

  # gh issue create bruker --label, ikke --add-label.
  opprett_flagg=()
  for ((i = 0; i < ${#etikett_flagg[@]}; i += 2)); do
    opprett_flagg+=(--label "${etikett_flagg[i+1]}")
  done

  echo "Oppretter: [$etiketter] $tittel"
  gh issue create --title "$tittel" "${opprett_flagg[@]}" --body "$body" "${repo_flag[@]}"
done

echo
echo "Ferdig. Issue 1-5 er feilene (P1), 6-19 er kjernelagene, 20 og oppover er resten."
