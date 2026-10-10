# Publicación a NuGet.org (Trusted Publishing, sin API key de larga vida)

CI usa OIDC: el workflow pide una key temporal (1 h) vía `NuGet/login@v1`.
No hay credenciales en el repo: el workflow solo consume el secret
`NUGET_USER` (tu nombre de usuario de nuget.org, no sensible) y el token
OIDC efímero de GitHub Actions.

## Requisito único en nuget.org (lo haces tú una vez)

Sesión iniciada → tu usuario → **Trusted Publishing** → añadir política:

- Repository Owner: el owner del repo en GitHub
- Repository: el nombre del repo
- Workflow File: `publish.yml` (solo el nombre del archivo)
- Environment: vacío (el workflow no usa environments)

Sin esa política, el paso `NuGet login` falla y nada se publica. En repos
privados la política nace temporal (7 días) y queda permanente tras el
primer push exitoso.

## Disparo

Crear un tag `v*` (p. ej. `v0.1.0-beta.1`): la versión del tag —sin la `v`—
es la que se empaqueta (`-p:Version=`), por lo que el tag es la fuente de
verdad y el `Version` del csproj solo rige para builds locales.

## Push manual desde CLI (único uso legítimo de API key)

Las API keys siguen valiendo para línea de comandos: créala en
**Account settings → API Keys** (scope **Push**, glob `crap4csharp*`,
con expiración), cópiala una vez y úsala así:

`dotnet nuget push <nupkg> --api-key <key> --source https://api.nuget.org/v3/index.json`

No la guardes como secret del repo: CI ya no la necesita.
