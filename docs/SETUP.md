# Setup Guide: Entra ID + Azure OpenAI for the MCP Server/Client Sample

This guide explains how to configure everything needed to run this sample end to end:

- An MCP Server protected with Microsoft Entra ID
- An MCP Client that signs in interactively and calls the protected MCP endpoint
- Azure OpenAI for the client-side AI agent

> Important: The current repository contains example secrets in `appsettings.json`. Treat them as compromised and rotate/recreate them before any real usage.

## 1) Prerequisites

- Azure subscription with permission to create resources
- A Microsoft Entra tenant where you can create App registrations
- .NET SDK installed (the solution targets .NET 10 preview)
- Permission to use Azure OpenAI models in your subscription/region

## 2) Understand what this sample expects

From the code:

- **MCP Server** (`src/Abyx.McpServer`)
  - Validates JWT bearer tokens
  - Requires the scope `mcp:tools`
  - Calls Microsoft Graph (`Me`) with delegated `User.Read`
- **MCP Client** (`src/Abyx.McpClient`)
  - Uses `InteractiveBrowserCredential` with redirect URI `http://localhost`
  - Requests the scope configured in `AzureAd:Scopes`
  - Calls Azure OpenAI using endpoint + deployment + API key

This means you need **two Entra applications**:

1. A **Server/API app registration** (protected resource)
2. A **Client/Public app registration** (interactive user sign-in)

---

## 3) Create App Registration #1 (MCP Server API)

Open **Microsoft Entra admin center** → **App registrations** → **New registration**.

### 3.1 Basic registration

- Name: `Abyx MCP Server API` (or similar)
- Supported account types: your preferred option (single-tenant is simplest for this sample)
- Register

Save these values:

- `Application (client) ID` → will be `AzureAd:ClientId` in server settings
- `Directory (tenant) ID` → will be `AzureAd:TenantId`

### 3.2 Expose the API scope

In the Server app: **Expose an API**.

1. Set **Application ID URI** (App ID URI), for example:
   - `api://<SERVER_CLIENT_ID>` (recommended for simplicity)
   - or a custom URI like `https://your-domain/abyx`
2. Add a scope:
   - Scope name: `mcp:tools`
   - Who can consent: Admins and users (or admins only if your policy requires)
   - Display names/descriptions: any clear text
   - State: Enabled

Your final fully qualified scope becomes:

- `<APP_ID_URI>/mcp:tools`

Example:

- `api://11111111-2222-3333-4444-555555555555/mcp:tools`

### 3.3 Add a client secret (required by server for downstream Graph token acquisition)

In the Server app: **Certificates & secrets** → **New client secret**.

- Copy the generated secret value immediately.
- This value goes to `AzureAd:ClientCredentials[0].ClientSecret` in server configuration.

### 3.4 Configure Microsoft Graph delegated permission

In the Server app: **API permissions** → **Add a permission** → **Microsoft Graph** → **Delegated permissions**.

- Add: `User.Read`

Then grant consent based on your organization policy:

- Either user consent at runtime, or
- **Grant admin consent** in advance

---

## 4) Create App Registration #2 (MCP Client Public App)

Open **App registrations** → **New registration**.

### 4.1 Basic registration

- Name: `Abyx MCP Client` (or similar)
- Supported account types: same tenant choice as server is simplest
- Register

Save:

- `Application (client) ID` → will be `AzureAd:ClientId` in client settings
- `Directory (tenant) ID` → will be `AzureAd:TenantId`

### 4.2 Add redirect URI for native/public client

In Client app: **Authentication** → **Add a platform** → **Mobile and desktop applications**.

- Add redirect URI: `http://localhost`

This must match the code in `InteractiveBrowserCredentialOptions`.

### 4.3 Grant delegated permission to your MCP Server API

In Client app: **API permissions** → **Add a permission** → **My APIs**.

- Select your `Abyx MCP Server API` app
- Add delegated permission scope: `mcp:tools`

Grant consent if your tenant policy requires it.

---

## 5) Configure Azure OpenAI

### 5.1 Create Azure OpenAI resource

In Azure portal:

1. Create resource → **Azure OpenAI**
2. Choose subscription, resource group, region, and name
3. Create

### 5.2 Deploy a chat model

Open your Azure OpenAI resource (Azure AI Foundry portal or resource deployment experience):

1. Go to **Model deployments**
2. Create a new deployment for a chat-capable model available in your region
3. Choose a deployment name (example: `gpt-5-mini`)

You need these values for the client appsettings:

- `Endpoint` (for the Azure OpenAI resource)
- `DeploymentName`
- `ApiKey` (from Keys/Endpoints)

---

## 6) Fill `appsettings.json`

Use the template files as base:

- `src/Abyx.McpServer/appsettings.TEMPLATE.json`
- `src/Abyx.McpClient/appsettings.TEMPLATE.json`

and populate runtime files:

- `src/Abyx.McpServer/appsettings.json`
- `src/Abyx.McpClient/appsettings.json`

### 6.1 Server settings mapping

In `src/Abyx.McpServer/appsettings.json`:

- `AzureAd:ClientId` = Server App Registration client ID
- `AzureAd:TenantId` = tenant ID
- `AzureAd:Audience` = App ID URI from **Expose an API**
- `AzureAd:Scope` = array containing `<APP_ID_URI>/mcp:tools`
- `AzureAd:ClientCredentials[0].ClientSecret` = server app client secret
- `DownstreamApis:MicrosoftGraph:Scopes` should include `User.Read`

### 6.2 Client settings mapping

In `src/Abyx.McpClient/appsettings.json`:

- `AzureAd:ClientId` = Client App Registration client ID
- `AzureAd:TenantId` = tenant ID
- `AzureAd:Scopes` = `<APP_ID_URI>/mcp:tools` (exactly matching server scope)
- `AzureOpenAI:Endpoint` = Azure OpenAI endpoint URL
- `AzureOpenAI:DeploymentName` = model deployment name
- `AzureOpenAI:ApiKey` = Azure OpenAI API key

---

## 7) Run and validate

From `src/`:

1. Start server:
   - `dotnet run --project Abyx.McpServer`
2. In a second terminal, run client:
   - `dotnet run --project Abyx.McpClient`
3. Browser sign-in appears for the client app
4. After consent, client calls MCP server with bearer token
5. MCP server calls Microsoft Graph `Me` and returns profile data via tool

If successful, the client prints an answer containing your profile details.

---

## 8) Common issues and fixes

- **401/403 on MCP endpoint**
  - Usually scope mismatch. Ensure client requests exactly `<APP_ID_URI>/mcp:tools` and server expects the same value.
- **AADSTS redirect URI error**
  - Ensure Client app has `http://localhost` under Mobile/Desktop redirect URIs.
- **Graph call fails (`insufficient privileges`)**
  - Ensure Server app has delegated `User.Read` and consent is granted.
- **OpenAI 401/404**
  - Verify `Endpoint`, `ApiKey`, and `DeploymentName` match your Azure OpenAI resource/deployment.

---

## 9) Security recommendations

- Do not commit secrets or API keys into source control.
- Prefer local secret storage (User Secrets, environment variables, Key Vault) over plain `appsettings.json`.
- Rotate any credential that was ever shared in plaintext.
