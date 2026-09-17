#!/usr/bin/env bash
#
# Engangsoppsett av Oslo Live i Azure. Kjøres av kurslederen, én gang.
#
#   ./scripts/sett-opp-azure.sh
#
# Etterpå ruller GitHub Actions ut automatisk hver gang main endrer seg.
#
# Det som settes opp:
#   - ressursgruppe i Norway East
#   - Container Registry, Log Analytics, Container Apps-miljø og selve kartet
#   - en managed identity som GitHub Actions logger inn som, uten passord
#
# Hvorfor managed identity og ikke en app-registrering: repoet er offentlig,
# så vi vil ikke ha en hemmelighet noe sted. Federated credentials lar GitHub
# bevise hvem den er, og Azure stoler på beviset. Ingen nøkkel å lekke.

set -euo pipefail

ABONNEMENT="${ABONNEMENT:-Novanet}"
GRUPPE="${GRUPPE:-novanet-rg-oslolive}"
STED="${STED:-norwayeast}"
NAVN="${NAVN:-oslolive}"
REPO="${REPO:-novanet/workshop.oslo-live}"

rot="$(cd "$(dirname "$0")/.." && pwd)"
cd "$rot"

echo "Abonnement : $ABONNEMENT"
echo "Gruppe     : $GRUPPE ($STED)"
echo "Repo       : $REPO"
echo

sub_id="$(az account list --query "[?name=='$ABONNEMENT'].id | [0]" -o tsv)"
if [[ -z "$sub_id" ]]; then
  echo "Fant ikke abonnementet «$ABONNEMENT»." >&2
  exit 1
fi
az account set --subscription "$sub_id"
tenant_id="$(az account show --query tenantId -o tsv)"

# ---------------------------------------------------------------------------
# 1. Infrastruktur
# ---------------------------------------------------------------------------
echo "==> Ressursgruppe"
az group create -n "$GRUPPE" -l "$STED" -o none

echo "==> Bicep (tar noen minutter første gang)"
az deployment group create \
  -g "$GRUPPE" \
  -n oslolive \
  -f infra/kart.bicep \
  -p name="$NAVN" location="$STED" \
  -o none

acr="$(az deployment group show -g "$GRUPPE" -n oslolive --query properties.outputs.acrName.value -o tsv)"
app="$(az deployment group show -g "$GRUPPE" -n oslolive --query properties.outputs.appName.value -o tsv)"
echo "    register: $acr"
echo "    app:      $app"

# ---------------------------------------------------------------------------
# 2. Første bygg, så appen kjører ekte kode med en gang
# ---------------------------------------------------------------------------
echo
echo "==> Bygger bildet i skyen"
az acr build --registry "$acr" --image "oslo-live:forste" --image "oslo-live:latest" . -o none

echo "==> Ruller ut"
az containerapp update -n "$app" -g "$GRUPPE" \
  --image "$acr.azurecr.io/oslo-live:forste" -o none

kart="https://$(az containerapp show -n "$app" -g "$GRUPPE" \
  --query properties.configuration.ingress.fqdn -o tsv)"

# ---------------------------------------------------------------------------
# 3. Innlogging for GitHub Actions, uten hemmeligheter
# ---------------------------------------------------------------------------
echo
echo "==> Identitet for GitHub Actions"
mi_navn="id-$NAVN-deploy"
az identity create -n "$mi_navn" -g "$GRUPPE" -l "$STED" -o none

mi_client="$(az identity show -n "$mi_navn" -g "$GRUPPE" --query clientId -o tsv)"
mi_princ="$(az identity show -n "$mi_navn" -g "$GRUPPE" --query principalId -o tsv)"

# Én credential per situasjon vi vil tillate: bare main, og bare miljøet
# «produksjon». En pull request fra en fork kan ikke låne disse.
#
# Novanet-organisasjonen har «unique token claims» slått på, så GitHub
# presenterer seg som repo:novanet@<org-id>/<repo>@<repo-id>:... i stedet for
# med det klassiske navnet. Vi lager begge formene, så oppsettet virker
# uansett hvordan organisasjonen er satt opp.
org_id="$(gh api "orgs/${REPO%%/*}" --jq .id 2>/dev/null || true)"
repo_id="$(gh api "repos/$REPO" --jq .id 2>/dev/null || true)"

repo_navn=("$REPO")
if [[ -n "$org_id" && -n "$repo_id" ]]; then
  repo_navn+=("${REPO%%/*}@${org_id}/${REPO##*/}@${repo_id}")
fi

variant=0
for rn in "${repo_navn[@]}"; do
  for par in "main:ref:refs/heads/main" "produksjon:environment:produksjon"; do
    subject="repo:${rn}:${par#*:}"
    az identity federated-credential create \
      --name "gh-${par%%:*}-${variant}" \
      --identity-name "$mi_navn" \
      -g "$GRUPPE" \
      --issuer "https://token.actions.githubusercontent.com" \
      --subject "$subject" \
      --audiences "api://AzureADTokenExchange" \
      -o none 2>/dev/null || true
    echo "    $subject"
  done
  variant=$((variant + 1))
done

# ---------------------------------------------------------------------------
# 4. Rettigheter
# ---------------------------------------------------------------------------
echo
echo "==> Rettigheter"
gruppe_id="$(az group show -n "$GRUPPE" --query id -o tsv)"
acr_id="$(az acr show -n "$acr" -g "$GRUPPE" --query id -o tsv)"

# «az role assignment create» svarer MissingSubscription på en managed
# identity i dette abonnementet, så vi går rett på ARM i stedet.
tildel_rolle() {
  local scope="$1" rolle="$2" beskrivelse="$3" g body
  g="$(python -c 'import uuid;print(uuid.uuid4())' 2>/dev/null || uuidgen)"
  body="$(printf '{"properties":{"roleDefinitionId":"/subscriptions/%s/providers/Microsoft.Authorization/roleDefinitions/%s","principalId":"%s","principalType":"ServicePrincipal"}}' \
    "$sub_id" "$rolle" "$mi_princ")"

  if az rest --method PUT \
      --url "https://management.azure.com${scope}/providers/Microsoft.Authorization/roleAssignments/${g}?api-version=2022-04-01" \
      --body "$body" \
      -o none 2>/dev/null; then
    echo "    $beskrivelse"
  else
    echo "    $beskrivelse (fantes allerede)"
  fi
}

# Contributor: oppdatere container-appen. AcrPush: legge opp nye bilder.
tildel_rolle "$gruppe_id" "b24988ac-6180-42a0-ab88-20f7382dd24c" "Contributor på ressursgruppen"
tildel_rolle "$acr_id"    "8311e382-0749-4cb8-b61a-304f252e45ec" "AcrPush på registeret"

echo "    (rettigheter bruker et par minutter på å tre i kraft)"

# ---------------------------------------------------------------------------
# 5. Legg verdiene inn i GitHub
# ---------------------------------------------------------------------------
echo
if command -v gh >/dev/null 2>&1; then
  echo "==> Legger inn i GitHub"
  gh secret set AZURE_CLIENT_ID       --repo "$REPO" --body "$mi_client"
  gh secret set AZURE_TENANT_ID       --repo "$REPO" --body "$tenant_id"
  gh secret set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$sub_id"

  # Miljøet må finnes, ellers avvises federated credential for «produksjon».
  gh api -X PUT "repos/$REPO/environments/produksjon" --silent 2>/dev/null \
    && echo "    miljøet «produksjon» er på plass" \
    || echo "    lag miljøet «produksjon» manuelt under Settings > Environments"
else
  echo "gh mangler. Legg disse inn manuelt under Settings > Secrets > Actions:"
  echo "  AZURE_CLIENT_ID       $mi_client"
  echo "  AZURE_TENANT_ID       $tenant_id"
  echo "  AZURE_SUBSCRIPTION_ID $sub_id"
  echo "og lag miljøet «produksjon» under Settings > Environments."
fi

cat <<SLUTT

Ferdig.

  Kart:  $kart
  Repo:  https://github.com/$REPO

Fra nå av bygger og ruller hver merge til main ut automatisk.

Går første Actions-kjøring i vasken med «No subscriptions found», er det
bare rettighetene som ikke har rukket å spre seg. Kjør den på nytt.

SLUTT
