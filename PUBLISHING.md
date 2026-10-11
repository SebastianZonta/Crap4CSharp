# Publishing to NuGet.org (Trusted Publishing, no long-lived API key)

CI uses OIDC: the workflow requests a short-lived key (1 h) via
`NuGet/login@v1`. There are no credentials in the repo: the workflow only
consumes the `NUGET_USER` secret (your nuget.org username, not sensitive)
plus GitHub Actions' ephemeral OIDC token.

## One-time setup on nuget.org (you do this once)

Signed in → your username → **Trusted Publishing** → add a policy:

- Repository Owner: the GitHub repo owner
- Repository: the repo name
- Workflow File: `ci.yml` (file name only — publish is a job in the CI
  workflow so it only runs after build + e2e pass)
- Environment: empty (the workflow uses no environments)

Without that policy, the `NuGet login` step fails and nothing is
published. On private repos the policy starts temporarily active
(7 days) and becomes permanent after the first successful push.

## Trigger

Create a `v*` tag (e.g. `v0.1.0-beta.1`): the tag version —without the
`v`— is what gets packed (`-p:Version=`), so the tag is the source of
truth and the csproj `Version` only applies to local builds.

## Manual push from the CLI (the only legitimate API-key use)

API keys still work for the command line: create one under
**Account settings → API Keys** (**Push** scope, `crap4csharp*` glob,
with expiry), copy it once, and use it like this:

`dotnet nuget push <nupkg> --api-key <key> --source https://api.nuget.org/v3/index.json`

Do not store it as a repo secret: CI no longer needs it.
