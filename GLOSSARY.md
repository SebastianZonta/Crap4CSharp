# Glossary

- **file discovery**: finding the C# files to analyze (all/src, changed, or explicit paths).
- **coverage run**: the `dotnet test` XPlat Code Coverage step per module.
- **analysis**: parsing methods (Roslyn) and scoring CRAP from complexity + Cobertura coverage.
- **progress indicator**: Braille spinner on stderr for discovery/coverage/analysis; stdout stays pipe-clean.
- **violation**: an analyzed method whose numeric CRAP score is strictly greater than the effective threshold (`N/A` rows are never violations).
