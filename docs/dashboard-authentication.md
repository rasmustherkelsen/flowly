# Dashboard Authentication

By default the Flowly Dashboard allows anonymous access. This guide walks through securing it with OAuth2/OIDC — complete step-by-step setup for **Azure Entra ID** and **Google**, including the identity-provider portal configuration and the Flowly code wiring.

The dashboard registers its own isolated cookie and OIDC authentication schemes (`FlowlyDashboard.Cookies` / `FlowlyDashboard.Oidc`), so enabling authentication does not interfere with any authentication your host application has already configured. Sign-in uses the Authorization Code flow with PKCE; the authorization code is exchanged for tokens server-side, and the browser only ever holds the dashboard's session cookie (`flowly-dashboard`).

## Prerequisites

- A project with the Flowly Dashboard already added (`Flowly.Dashboard` package, `AddFlowlyDashboard()` + `UseFlowlyDashboard()` wired up — see the [Dashboard section of the User Guide](../README.md#dashboard))
- For Azure Entra ID: an Entra tenant where you can create app registrations
- For Google: a Google Cloud project where you can create OAuth credentials

---

## How dashboard access control works

Authentication is enabled by setting `Authentication` in `AddFlowlyDashboard()`:

```csharp
using Flowly.Dashboard.Auth;

builder.Services.AddFlowlyDashboard(options =>
{
    options.Authentication = new OAuthAuthenticationOptions(
        clientId: "your-client-id",
        authority: "https://your-identity-provider",
        clientSecret: builder.Configuration["FlowlyDashboard:ClientSecret"]);
});
```

`OAuthAuthenticationOptions` takes the following constructor parameters:

| Parameter | Required | Description |
|---|---|---|
| `clientId` | Yes | The OAuth2 client identifier registered with the identity provider |
| `authority` | Yes | The OIDC issuer URL used for discovery — `https://login.microsoftonline.com/{tenantId}/v2.0` for Entra ID, `https://accounts.google.com` for Google |
| `clientSecret` | Provider-dependent | Used in the server-side authorization code exchange; required for confidential clients (Entra ID Web registrations), omit for public clients |
| `viewerRoles` | No | Roles that grant view access; `null` means every authenticated user can view |
| `viewerPolicies` | No | Named ASP.NET Core authorization policies that grant view access |
| `submitterRoles` | No | Roles that grant submit (mutation) access; `null` means the viewer restriction also governs submitting |
| `submitterPolicies` | No | Named policies that grant submit access |

### Viewer and Submitter tiers

Access is split into two tiers:

- **Viewer** — gates the entire dashboard: the UI itself and all read APIs (jobs, recurring jobs, dead letters). An authenticated user who does not satisfy the viewer restriction gets a plain `403` and sees nothing.
- **Submitter** — gates every mutating action as one unit: submitting messages, triggering recurring jobs, requeuing/discarding dead letters, and even listing the available message types to submit.

Rules that follow from this model:

- All role and policy lists use **OR logic** — satisfying any single entry in a list grants that tier.
- Submitter is layered **on top of** Viewer, not independent: a user who satisfies the submitter restriction but not the viewer restriction is still blocked by the viewer `403`.
- When no `submitterRoles`/`submitterPolicies` are set, the viewer restriction governs submitting too — every viewer can submit.
- Named policies referenced in `viewerPolicies`/`submitterPolicies` must be registered with `services.AddAuthorization(...)`. Flowly validates this when `UseFlowlyDashboard()` runs and throws `InvalidOperationException` at startup for any unregistered policy name.

### Redirect URIs

The OIDC callback path is `/signin-oidc`, relative to the dashboard's `PathPrefix`. Register these URIs with your identity provider:

| Purpose | URI | Example (embedded, default `PathPrefix = "/flowly"`) | Example (standalone, `PathPrefix = string.Empty`) |
|---|---|---|---|
| Redirect URI | `{baseUrl}{pathPrefix}/signin-oidc` | `https://myapp.com/flowly/signin-oidc` | `https://dashboard.myapp.com/signin-oidc` |
| Post-logout redirect URI (if the provider enforces a list) | `{baseUrl}{pathPrefix}/` | `https://myapp.com/flowly/` | `https://dashboard.myapp.com/` |

### Sign-in, sign-out, and status codes

- Browser navigation to the dashboard while unauthenticated redirects to the identity provider's sign-in page and returns to the dashboard afterwards.
- Requests to the dashboard's `/api/*` endpoints follow standard HTTP semantics: `401 Unauthorized` when unauthenticated, `403 Forbidden` when authenticated but not satisfying the required tier.
- `GET {pathPrefix}/logout` signs out of both the dashboard cookie and the identity provider session, then redirects back to the dashboard root (which triggers a fresh sign-in).

---

## Azure Entra ID — step by step

Entra ID supports app roles, so the role-based parameters (`viewerRoles`/`submitterRoles`) are the natural fit.

### 1. Register the application

1. Open the [Azure Portal](https://portal.azure.com) and go to **Microsoft Entra ID → App registrations → New registration**.
2. Give the registration a name (e.g. `Flowly Dashboard`).
3. Under **Supported account types**, pick **Accounts in this organizational directory only** (single tenant) unless you specifically need multi-tenant sign-in.
4. Skip the redirect URI for now (the next step sets the platform type correctly) and click **Register**.
5. On the app's **Overview** page, note the **Application (client) ID** and **Directory (tenant) ID** — you need both for the code wiring.

### 2. Add the Web platform redirect URI

1. Go to **Authentication → Add a platform → Web**.

   Choose **Web**, *not* Single-page application — the dashboard exchanges the authorization code server-side, and Entra ID treats Web registrations as confidential clients.

2. Enter the redirect URI: `{baseUrl}{pathPrefix}/signin-oidc`. For local development that is:
   - **Embedded dashboard** (default `PathPrefix = "/flowly"`): `https://localhost:7042/flowly/signin-oidc`
   - **Standalone Dashboard project** (`PathPrefix = string.Empty`, as scaffolded by `dotnet new flowlyapp --dashboard`): `https://localhost:7042/signin-oidc` — no `/flowly` segment
3. Optionally add `{baseUrl}{pathPrefix}/` under **Front-channel logout URL** / post-logout redirect URIs.
4. Click **Configure**. Add your production URL as an additional redirect URI when you deploy.

### 3. Create a client secret

Entra ID Web registrations are confidential clients — the code exchange is rejected without a secret.

1. Go to **Certificates & secrets → Client secrets → New client secret**.
2. Give it a description and an expiry, click **Add**, and copy the secret **Value** immediately (it is only shown once).

The secret is used exclusively in the server-to-server token exchange and is never transmitted to the browser. Store it securely — never in source control:

```bash
# Local development — user secrets:
dotnet user-secrets init
dotnet user-secrets set "FlowlyDashboard:ClientSecret" "your-secret-value"
```

In production, use an environment variable or Azure Key Vault.

### 4. Define app roles

1. Go to **App roles → Create app role** and create one role per access tier, e.g.:

   | Display name | Value | Allowed member types |
   |---|---|---|
   | Dashboard Viewer | `dashboard-viewer` | Users/Groups |
   | Dashboard Admin | `dashboard-admin` | Users/Groups |

   The **Value** is the string you pass to `viewerRoles`/`submitterRoles`.

2. Assign the roles to users or groups under **Microsoft Entra ID → Enterprise applications → (your app) → Users and groups → Add user/group**.

### 5. Wire up the dashboard

```csharp
using Flowly.Dashboard.Auth;

builder.Services.AddFlowlyDashboard(options =>
{
    options.Authentication = new OAuthAuthenticationOptions(
        clientId: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",                                       // Application (client) ID
        authority: "https://login.microsoftonline.com/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/v2.0", // Directory (tenant) ID
        clientSecret: builder.Configuration["FlowlyDashboard:ClientSecret"],
        viewerRoles: ["dashboard-viewer", "dashboard-admin"],
        submitterRoles: ["dashboard-admin"]);
});
```

For multi-tenant registrations, use `https://login.microsoftonline.com/common/v2.0` as the authority.

### 6. Verify

1. Run the project and browse to the dashboard URL (e.g. `https://localhost:7042/flowly`).
2. You are redirected to the Microsoft sign-in page; after signing in you land back on the dashboard.
3. A user with only `dashboard-viewer` sees the dashboard but no Submit panel; a user with `dashboard-admin` can also submit messages and trigger jobs.
4. A signed-in user with neither role gets `403` — "You do not have permission to view this dashboard."

---

## Google — step by step

Google does not emit role claims, so instead of roles you register named ASP.NET Core authorization policies that check the user's claims and reference them via `viewerPolicies`/`submitterPolicies`.

### 1. Configure the OAuth consent screen

1. Open the [Google Cloud Console](https://console.cloud.google.com), create or select a project, and go to **APIs & Services → OAuth consent screen**. This opens **Google Auth Platform**; what you see depends on whether the project already has an OAuth configuration:
   - **Not configured yet** — a **Get started** wizard collects the configuration in four short pages: **App information** (the app name shown on the sign-in screen and a user support email), **Audience** (see below), **Contact information** (a developer email Google uses for notifications), and **Finish** (agree to the user data policy and click **Create**).
   - **Already configured** — you land on the **Overview** page (OAuth metrics), and the configuration is edited via the left-nav pages instead: **Branding** (app name, support email, logo), **Audience** (audience type, publishing status, test users), **Clients** (OAuth client IDs), and **Data Access** (scopes).
2. Choose the audience (in the wizard, or later under **Audience**):
   - **Internal** — available only to Google Workspace organizations; restricts sign-in to accounts in your organization. Consumer `@gmail.com` accounts cannot sign in (when you are signed in with a consumer account this option is greyed out).
   - **External** — required when consumer `@gmail.com` accounts should sign in (and for mixed audiences).

**Consumer accounts (External audience) — Testing vs In production:**

An External app starts in **Testing** status, where only explicitly listed test users can sign in — any other Google account is rejected by Google with `Error 403: access_denied` before ever reaching the dashboard.

1. Add every consumer account that should have dashboard access under **Google Auth Platform → Audience → Test users → Add users**. Up to 100 test users are allowed.
2. Test users may see a "Google hasn't verified this app" notice on first sign-in — expected for an app in Testing; continue past it.
3. For an internal operations dashboard, staying in Testing status permanently is a perfectly valid setup — the test-user list acts as a first coarse allow-list in front of Flowly's viewer/submitter restrictions.
4. If you outgrow the test-user list, publish the app (**Publish app** on the **Audience** page). The dashboard only uses the non-sensitive `openid` and `profile` scopes, so no Google verification review is required.

Regardless of Testing or In production status, still configure `viewerPolicies`/`submitterPolicies` (step 4 below, *Register authorization policies and wire up the dashboard*) — otherwise **any** Google account that passes the consent screen can view the dashboard.

### 2. Create the OAuth client ID

1. Go to **Google Auth Platform → Clients → Create client** (also reachable via **APIs & Services → Credentials → Create credentials → OAuth client ID**).
2. Choose application type **Web application**.
3. Under **Authorized redirect URIs**, add `{baseUrl}{pathPrefix}/signin-oidc`. For local development that is:
   - **Embedded dashboard** (default `PathPrefix = "/flowly"`): `https://localhost:7042/flowly/signin-oidc`
   - **Standalone Dashboard project** (`PathPrefix = string.Empty`, as scaffolded by `dotnet new flowlyapp --dashboard`): `https://localhost:7042/signin-oidc` — no `/flowly` segment
4. Click **Create** and copy the **Client ID** and **Client secret** from the confirmation dialog. Google shows the secret only at creation time — copy it (or download the JSON) now; if you lose it, reset the secret on the client's detail page.

### 3. Store the client secret

```bash
dotnet user-secrets init
dotnet user-secrets set "FlowlyDashboard:ClientSecret" "your-secret-value"
```

Google issues a client secret for Web application clients — pass it as `clientSecret`. It can only be omitted for provider configurations that support public clients.

### 4. Register authorization policies and wire up the dashboard

The authority for Google is always `https://accounts.google.com`. Since there are no role claims, key the policies off the user's claims — the stable Google account ID arrives as the name-identifier claim (`sub`):

```csharp
using System.Security.Claims;
using Flowly.Dashboard.Auth;

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DashboardViewers", policy => policy.RequireClaim(ClaimTypes.NameIdentifier, "103984028471234567890", "118273645092837465012"));
    options.AddPolicy("DashboardAdmins", policy => policy.RequireClaim(ClaimTypes.NameIdentifier, "103984028471234567890"));
});

builder.Services.AddFlowlyDashboard(options =>
{
    options.Authentication = new OAuthAuthenticationOptions(
        clientId: "xxxxxxxxxxxx-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.apps.googleusercontent.com",
        authority: "https://accounts.google.com",
        clientSecret: builder.Configuration["FlowlyDashboard:ClientSecret"],
        viewerPolicies: ["DashboardViewers"],
        submitterPolicies: ["DashboardAdmins"]);
});
```

The `sub` values are each user's Google account ID:

- **Workspace organizations** — look the account up in the [Google Admin console](https://admin.google.com); the user's unique ID is the `sub` value.
- **Consumer `@gmail.com` accounts** — there is no admin console to look it up in. The practical approach is a temporary diagnostic endpoint in the host app that reads the dashboard's session cookie and returns the signed-in user's `sub`:

  ```csharp
  using System.Security.Claims;
  using Microsoft.AspNetCore.Authentication;

  app.MapGet("/whoami", async (HttpContext httpContext) =>
  {
      var authenticateResult = await httpContext.AuthenticateAsync("FlowlyDashboard.Cookies");
      return authenticateResult.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Not signed in — visit the dashboard first.";
  });
  ```

  Have each user navigate to the dashboard and complete the Google sign-in (this works even while they are still locked out — the session cookie is issued before the viewer check, so a `403` on the dashboard is fine), then open `/whoami` and send you the value. Add the values to the policies and **remove the endpoint** when done.

> **Note on email-based policies:** a policy on `ClaimTypes.Email` is more readable, but Google only includes the email claim when the `email` scope is granted — verify the claim is present in your tokens before relying on it. The `sub` claim is always present.

The policy names in `viewerPolicies`/`submitterPolicies` must match the names registered with `AddAuthorization` — Flowly validates this at startup and throws `InvalidOperationException` if a name is missing.

### 5. Verify

1. Run the project and browse to the dashboard URL.
2. You are redirected to Google's sign-in page; after signing in you land back on the dashboard. Consumer accounts on an app in Testing status may first see the "Google hasn't verified this app" notice — continue past it.
3. An account matching `DashboardViewers` but not `DashboardAdmins` sees the dashboard without the Submit panel.
4. An account matching neither policy gets `403` — "You do not have permission to view this dashboard."
5. A consumer account that is not on the test-user list (Testing status) is rejected by Google with `Error 403: access_denied` and never reaches the dashboard.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Provider error about redirect URI mismatch (`AADSTS50011`, `redirect_uri_mismatch`) | The registered redirect URI does not exactly match `{baseUrl}{pathPrefix}/signin-oidc` — check scheme (`https`), host, port, and the `PathPrefix` segment. The most common cause is registering the `/flowly/signin-oidc` form for a standalone Dashboard project (which uses `PathPrefix = string.Empty`, so the callback is `/signin-oidc` at the root), or vice versa. The provider's error page shows the exact URI the app sent — Google under "error details", Entra directly in the `AADSTS50011` message |
| `InvalidOperationException: Flowly Dashboard: authorization policy '...' is not registered` at startup | A name in `viewerPolicies`/`submitterPolicies` has no matching `AddPolicy` registration — register it with `services.AddAuthorization(...)` |
| Token exchange fails after sign-in (Entra `AADSTS7000218`: request body must contain `client_assertion` or `client_secret`) | The registration is a confidential client but no `clientSecret` was passed — create a secret and pass it via configuration |
| `403` immediately after a successful sign-in | The user authenticated but satisfies no entry in `viewerRoles`/`viewerPolicies` — assign the role (Entra: Enterprise applications → Users and groups) or add the user's claim to the policy |
| Viewer can see the dashboard but the Submit panel is missing | Expected when `submitterRoles`/`submitterPolicies` are set and the user satisfies none of them — the whole submit surface is gated as one unit |
| Policy on `ClaimTypes.Email` never matches (Google) | The email claim is only present when the `email` scope is granted — key the policy off `ClaimTypes.NameIdentifier` (`sub`) instead |
| Google shows "This app sent an invalid request" and the request details include `response_mode=form_post` | Google does not support the form_post response mode — `Flowly.Dashboard` versions prior to the fix left ASP.NET Core's form_post default in place; update the `Flowly.Dashboard` package |
| Google shows `Error 403: access_denied` before the sign-in form (consumer account) | The consent screen is in Testing status and the account is not a test user — add it under **Test users**, or publish the app |
| Consumer `@gmail.com` account cannot sign in at all | The consent screen audience is **Internal** (Workspace-only) — consumer accounts require **External** |
