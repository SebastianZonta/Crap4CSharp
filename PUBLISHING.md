# Secret de publicación a NuGet.org

## Nombre

`NUGET_API_KEY` (referenciado como `${{ secrets.NUGET_API_KEY }}` en
`.github/workflows/publish.yml`). No hay credenciales en el repo: el workflow
solo consume este secret.

## Scope mínimo

API key de NuGet.org con scope **Push** (o **Push new packages and package
versions**), limitada al paquete `crap4csharp` si la cuenta usa keys por
paquete. No necesita scope de administrado ni de firma.

## Dónde configurarlo

Repo en GitHub → **Settings → Secrets and variables → Actions** →
**New repository secret** → nombre `NUGET_API_KEY`, valor la key creada en
NuGet.org (**Account settings → API Keys → Create**). Sin este secret, el job
`publish` falla solo en el paso final de `dotnet nuget push`; todo lo anterior
(compilar, tests, harness, pack, validación local) corre igual.

## Disparo

Crear un tag `v*` (p. ej. `v0.1.0-beta.1`): la versión del tag —sin la `v`—
es la que se empaqueta (`-p:Version=`), por lo que el tag es la fuente de
verdad y el `Version` del csproj solo rige para builds locales.
