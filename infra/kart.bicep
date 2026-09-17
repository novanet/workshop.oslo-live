// Infrastruktur for Oslo Live-kartet.
//
// Dette er ÉN felles utrulling som viser main slik den er akkurat nå — altså
// summen av alt alle agentene har fått merget. Den er ikke per deltaker.
// Agentenes egen infrastruktur (mottak, kø, jobb) ligger i starter-kit.
//
//   az deployment group create -g <gruppe> -f infra/kart.bicep -p name=oslolive

@description('Kort navn som alle ressursene bygger på.')
param name string = 'oslolive'

@description('Norway East. Kurset kjører i Novanet-abonnementet.')
param location string = 'norwayeast'

@description('Bildet som kjøres. Settes av deploy-skriptet og av GitHub Actions.')
param image string = 'mcr.microsoft.com/k8se/quickstart:latest'

var uniq = uniqueString(resourceGroup().id, name)
var acrName = toLower('acr${name}${take(uniq, 8)}')

// ---------------------------------------------------------------------------
// Identitet. Container-appen henter bildet med denne, så vi slipper
// admin-bruker og passord på registeret.
// ---------------------------------------------------------------------------
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${name}'
  location: location
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, identity.id, 'AcrPull')
  scope: acr
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    // AcrPull
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${name}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource environment_ 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${name}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Selve kartet.
//
// Merk minReplicas: 1. Agentjobben i starter-kit skalerer til null, fordi
// den skal koste null når ingen har merket en issue. Kartet gjør det motsatte:
// det står på storskjermen hele dagen, og en kaldstart midt i en demo er
// verre enn det den replikaen koster.
// ---------------------------------------------------------------------------
resource kart 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${name}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    managedEnvironmentId: environment_.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'oslolive'
          image: image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
          probes: [
            {
              type: 'Readiness'
              httpGet: { path: '/api/helse', port: 8080 }
              initialDelaySeconds: 3
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
        rules: [
          {
            name: 'http'
            http: { metadata: { concurrentRequests: '50' } }
          }
        ]
      }
    }
  }
  dependsOn: [acrPull]
}

output kartUrl string = 'https://${kart.properties.configuration.ingress.fqdn}'
output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output appName string = kart.name
