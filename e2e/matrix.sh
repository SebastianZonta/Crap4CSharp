#!/usr/bin/env bash
# End-to-end CLI matrix for crap4csharp against a locally-installed tool build.
#
# The tool under test must already be installed (CI does this from the just-packed
# .nupkg, see .github/workflows/ci.yml `e2e` job). Locally:
#   dotnet pack src/Crap4CSharp/Crap4CSharp.csproj -c Release -p:Version=0.0.0-e2e -o ./nupkg
#   dotnet tool install -g crap4csharp --add-source ./nupkg --version 0.0.0-e2e
#   bash e2e/matrix.sh
#
# Override the binary with CRAP="dotnet tool run crap4csharp --" if needed.
set -uo pipefail

CRAP="${CRAP:-crap4csharp}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
cd "$WORK"

fails=0
pass() { echo "PASS: $1"; }
fail() { echo "FAIL: $1"; fails=$((fails + 1)); }
expect_exit() { # name want got
    if [ "$3" -eq "$2" ]; then pass "$1 (exit $3)"; else fail "$1 (exit $3, want $2)"; fi
}
expect_contains() { # name needle file
    if grep -qF "$2" "$3"; then pass "$1"; else fail "$1 (missing '$2')"; sed 's/^/  | /' "$3"; fi
}

# --- fixture: plain library + sibling tests, deliberately NO solution,
# --- so the no-args run also exercises the sibling-test-project fallback.
dotnet new classlib -n Demo -o src/Demo -f net8.0 --no-restore >/dev/null
dotnet new xunit -n Demo.Tests -o tests/Demo.Tests -f net8.0 --no-restore >/dev/null
rm src/Demo/Class1.cs tests/Demo.Tests/UnitTest1.cs
dotnet add tests/Demo.Tests/Demo.Tests.csproj reference src/Demo/Demo.csproj >/dev/null
dotnet add tests/Demo.Tests/Demo.Tests.csproj package coverlet.collector --version 6.0.0 >/dev/null

cat > src/Demo/Calc.cs <<'EOF'
namespace Demo;
public static class Calc
{
    public static int Compute(int x)
    {
        if (x < 0) return -1;
        if (x == 0) return 0;
        if (x > 100) return 2;
        return 1;
    }
    public static int Add(int a, int b) => a + b;
}
EOF

cat > tests/Demo.Tests/CalcTests.cs <<'EOF'
using Demo;
public class CalcTests
{
    [Theory]
    [InlineData(-5, -1)]
    [InlineData(0, 0)]
    [InlineData(101, 2)]
    [InlineData(50, 1)]
    public void Compute(int x, int expected) => Assert.Equal(expected, Calc.Compute(x));
    [Fact]
    public void Add() => Assert.Equal(3, Calc.Add(1, 2));
}
EOF

run() { # out err -- args...  (runs tool, stores exit in $rc)
    local out=$1 err=$2; shift 2
    # shellcheck disable=SC2086
    $CRAP "$@" >"$out" 2>"$err"; rc=$?
}

# 1. no-args: live coverage run, real scores, progress log on stderr, pure stdout.
run out.txt err.txt
expect_exit "no-args" 0 "$rc"
expect_contains "no-args real coverage" "100.0%" out.txt
expect_contains "no-args worst method" "Compute" out.txt
expect_contains "no-args found summary" "Found 1 C# file(s)." err.txt
expect_contains "no-args command echo" "$ dotnet test" err.txt
expect_contains "no-args analyzing line" "Analyzing src/Demo/Calc.cs" err.txt
if grep -qF "Found" out.txt; then fail "no-args pure stdout"; else pass "no-args pure stdout"; fi

# 2. json format (reuse coverage).
run out.txt err.txt --format json --no-coverage
expect_exit "json format" 0 "$rc"
expect_contains "json array" '"method": "Compute"' out.txt

# 3. threshold gate: max CRAP is 4.0, gate at 1 must fail with exit 2.
run out.txt err.txt --threshold 1 --no-coverage
expect_exit "threshold breach" 2 "$rc"
expect_contains "threshold message" "threshold exceeded" err.txt

# 4. unknown option.
run out.txt err.txt --bogus
expect_exit "unknown option" 1 "$rc"
expect_contains "unknown option message" "Unknown option" err.txt

# 5. --changed outside a git repo: clean exit 1, never a crash.
run out.txt err.txt --changed
expect_exit "changed outside git" 1 "$rc"
expect_contains "changed git message" "git status failed" err.txt

# 6. explicit file, reusing coverage.
run out.txt err.txt src/Demo/Calc.cs --no-coverage
expect_exit "explicit file" 0 "$rc"
expect_contains "explicit file scope" "Compute" out.txt

# 7. version.
run out.txt err.txt --version
expect_exit "version" 0 "$rc"

echo "---"
if [ "$fails" -eq 0 ]; then echo "MATRIX GREEN"; else echo "MATRIX RED: $fails failure(s)"; exit 1; fi
