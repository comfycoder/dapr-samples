@description('The location to deploy our resources to. Default is location of resource group')
param location string = resourceGroup().location

@description('The name of the Azure Container Registry')
param containerRegistryName string = 'acr${uniqueString(resourceGroup().id)}'

@description('The name of the Log Analytics workspace to deploy')
param logAnalyticsWorkspaceName string = 'law${uniqueString(resourceGroup().id)}'

@description('The name of the App Insights workspace')
param appInsightsName string = 'appins${uniqueString(resourceGroup().id)}'

@description('The name of the Container App Environment')
param containerEnvironmentName string = 'env${uniqueString(resourceGroup().id)}'

var frontendName = 'myfrontend'
var backendName = 'mybackend'

var tags = {
  DemoName: 'ACA-Dapr-Service-Invocation-Demo'
  Language: 'C#'
  Environment: 'Production'
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  tags: tags
  properties: {
   retentionInDays: 30
   features: {
    searchVersion: 1
   }
   sku: {
    name: 'PerGB2018'
   } 
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2022-02-01-preview' = {
  name: containerRegistryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource env 'Microsoft.App/managedEnvironments@2022-06-01-preview' = {
  name: containerEnvironmentName
  location: location
  tags: tags
  properties: {
   daprAIConnectionString: appInsights.properties.ConnectionString
   daprAIInstrumentationKey: appInsights.properties.InstrumentationKey
   appLogsConfiguration: {
    destination: 'log-analytics'
    logAnalyticsConfiguration: {
      customerId: logAnalytics.properties.customerId
      sharedKey: logAnalytics.listKeys().primarySharedKey
    }
   } 
  }
}

resource frontend 'Microsoft.App/containerApps@2022-06-01-preview' = {
  name: frontendName
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        transport: 'http'
        targetPort: 80
        allowInsecure: false
      }
      dapr: {
        enabled: true
        appPort: 80
        appId: frontendName
        enableApiLogging: true
      }
      secrets: [
        {
          name: 'container-registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
      registries: [
        {
          server: '${containerRegistry.name}.azurecr.io'
          username: containerRegistry.listCredentials().username
          passwordSecretRef: 'container-registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          name: frontendName
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Development'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 5
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource backend 'Microsoft.App/containerApps@2022-06-01-preview' = {
  name: backendName
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: false
        transport: 'http'
        targetPort: 80
        allowInsecure: false
      }
      dapr: {
        enabled: true
        appPort: 80
        appId: backendName
        enableApiLogging: true
      }
      secrets: [
        {
          name: 'container-registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
      registries: [
        {
          server: '${containerRegistry.name}.azurecr.io'
          username: containerRegistry.listCredentials().username
          passwordSecretRef: 'container-registry-password'
        }
      ]
    }
    template: {
      containers: [
        {
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
          name: backendName
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Development'
            }
          ]
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 5
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}
