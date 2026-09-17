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
GRUPPE="${GRUPPE:-rg-oslolive}"
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

# Én credential per situasjon vi vil tillate. Bare main, og bare miljøet
# «produksjon» - en pull request fra en fork kan ikke låne denne.
for par in "main:ref:refs/heads/main" "produksjon:environment:produksjon"; do
  cred_navn="${par%%:*}"
  subject="repo:${REPO}:${par#*:}"
  az identity federated-credential create \
    --name "gh-$cred_navn" \
    --identity-name "$mi_navn" \
    -g "$GRUPPE" \
    --issuer "https://token.actions.githubusercontent.com" \
    --subject "$subject" \
    --audiences "api://AzureADTokenExchange" \
    -o none 2>/dev/null || echo "    (gh-$cred_navn fantes allerede)"
  echo "    $subject"
done

echo
echo "==> Rettigheter"
gruppe_id="$(az group show -n "$GRUPPE" --query id -o tsv)"
acr_id="$(az acr show -n "$acr" -g "$GRUPPE" --query id -o tsv)"

# Contributor på gruppen: oppdatere container-appen.
az role assignment create --assignee-object-id "$mi_princ" \
  --assignee-principal-type ServicePrincipal \
  --role "Contributor" --scope "$gruppe_id" -o none 2>/dev/null || true

# AcrPush: bygge og legge opp bildet.
az role assignment create --assignee-object-id "$mi_princ" \
  --assignee-principal-type ServicePrincipal \
  --role "AcrPush" --scope "$acr_id" -o none 2>/dev/null || true

# ---------------------------------------------------------------------------
# 4. Legg verdiene inn i GitHub
# ---------------------------------------------------------------------------
echo
if command -v gh >/dev/null 2>&1; then
  echo "==> Legger inn i GitHub"
  gh secret set AZURE_CLIENT_ID       --repo "$REPO" --body "$mi_client"
  gh secret set AZURE_TENANT_ID       --repo "$REPO" --body "$tenant_id"
  gh secret set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$sub_id"
  echo "    tre verdier satt"
else
  echo "gh mangler. Legg disse inn manuelt under Settings > Secrets > Actions:"
  echo "  AZURE_CLIENT_ID       $mi_client"
  echo "  AZURE_TENANT_ID       $tenant_id"
  echo "  AZURE_SUBSCRIPTION_ID $sub_id"
fi

cat <<SLUTT

Ferdig.

  Kart:  $kart
  Repo:  https://github.com/$REPO

Fra nå av: hver merge til main bygger og ruller ut automatisk.

Gjenstår i GitHub (Settings > Environments):
  Lag miljøet «produksjon». Uten det virker ikke federated credential
  for miljøet, og du får heller ikke lenken på utrullingen.

SLUTT
