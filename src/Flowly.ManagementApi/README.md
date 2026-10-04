# Flowly.ManagementApi

An opt-in, machine-oriented HTTP surface for operational actions against a running [Flowly](https://rasmustherkelsen.github.io/flowly/) host — no UI, independent of `Flowly.Dashboard`. This is the start of a general Flowly management surface: message stream checkpoint list/reset is its first capability, and future features register additional `IFlowlyManagementAction` implementations into the same API.

## Quick start

```csharp
builder.Services.AddFlowlyManagementApi(options =>
{
    options.BasePath = "/flowly/management";   // default, configurable
    options.RequireApiKey(apiKey);              // or options.UseOAuth2(...), or options.RequireAuthorizationPolicies(...)
});
```
```csharp
app.UseFlowlyManagementApi();
```

Call `UseFlowlyManagementApi()` after every `Add<Provider>MessageStreamPersistence()` call — it feature-detects registered checkpoint persistence stores at that point and only maps the message stream actions when one exists.

## Authorization

Exactly one authorization mode must be configured, or `UseFlowlyManagementApi()` throws at startup:

- **API key** — `options.RequireApiKey(apiKey)` (single key, both read and write) or `options.RequireApiKey(readApiKey, writeApiKey)` (read/write split). Presented via the `X-Flowly-Api-Key` request header.
- **OAuth2** — `options.UseOAuth2(oauth => { oauth.Authority = "..."; oauth.Audience = "..."; })`, for protecting the API with an Entra ID (or any OIDC) app registration, including the client-credentials flow. The `ReadRequirement`/`WriteRequirement` claim values are checked against both the `roles` claim (Entra App Roles — what an app-only, client-credentials token carries) and the `scp` claim (delegated OAuth scopes — what an interactive sign-in token carries).
- **Existing policies** — `options.RequireAuthorizationPolicies(readPolicyName, writePolicyName)` defers entirely to authorization policies the host already configured.

## Discovery

`UseFlowlyManagementApi()` maps a standard OpenAPI document at `{BasePath}/openapi.json` (protected by the Read policy by default — set `options.ProtectDiscoveryDocument = false` to make it public). Set `options.EnableDocsUi = true` to also serve a minimal interactive explorer at `{BasePath}/docs`.

## Extensibility

Register `IFlowlyManagementAction` implementations into DI to contribute additional actions — `UseFlowlyManagementApi()` discovers and maps every one, applying the authorization policy matching its declared `RequiredCapability`.
