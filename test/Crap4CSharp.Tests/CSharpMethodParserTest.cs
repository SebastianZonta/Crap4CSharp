namespace Crap4CSharp.Tests;

// xUnit mirror of Java's JavaMethodParserTest, adapted to C# source fixtures.
// Same expected CC/exclusions; C#-only edges per ticket 03 (default counts;
// switch-expression arms, ?? and ?. do not; local functions count like lambdas).
public sealed class CSharpMethodParserTest
{
    [Fact]
    public void ExtractsConcreteMethodsWithLinesAndComplexity()
    {
        string source = """
            class Sample {
                int Alpha(bool a, bool b) {
                    if (a && b) {
                        return 1;
                    }
                    return 0;
                }

                int Beta(int x) {
                    switch (x) {
                        case 1: return 1;
                        case 2: return 2;
                        default: return 0;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Alpha", 2, 7, 3),
                new("Beta", 9, 15, 4),
            },
            methods);
    }

    [Fact]
    public void IgnoresConstructorsAndAbstractMethods()
    {
        string source = """
            abstract class Sample {
                Sample() {
                }

                abstract int Missing();

                int Present() {
                    return 1;
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Present", 7, 9, 1) }, methods);
    }

    [Fact]
    public void IgnoresMethodsOfNestedTypes()
    {
        string source = """
            class Outer {
                int Top() {
                    return 1;
                }

                class Nested {
                    int Hidden(bool f) {
                        if (f) {
                            return 1;
                        }
                        return 0;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Top", 2, 4, 1) }, methods);
    }

    [Fact]
    public void LocalFunctionsCountTowardOuterButAreNotListedSeparately()
    {
        // C# has no anonymous classes with methods (the Java oracle case); the
        // equivalent method-like thing inside a body is a local function: not a
        // separate descriptor, but its decisions count like a lambda body.
        string source = """
            class Sample {
                int Outer() {
                    int Local() {
                        if (true) {
                        }
                        return 1;
                    }
                    return Local();
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Outer", 2, 9, 2) }, methods);
    }

    [Fact]
    public void ParsesMethodsWithoutResolvingSiblingTypes()
    {
        string source = """
            namespace Demo;

            class Sample {
                Helper GetHelper() {
                    return new Helper();
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("GetHelper", 4, 6, 1) }, methods);
    }

    [Fact]
    public void IgnoresKeywordsInsideCommentsAndStrings()
    {
        string source = """
            class Sample {
                int Stable() {
                    string text = "if && || ? case default catch ?? ?. =>";
                    // if && || ? case default catch
                    /* if && || ? case default catch */
                    return 1;
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Stable", 2, 7, 1) }, methods);
    }

    [Fact]
    public void CountsDecisionNodesFromTheAst()
    {
        string source = """
            class Sample {
                int Score(bool a, bool b, int[] values) {
                    for (int i = 0; i < values.Length; i++) {
                    }
                    foreach (int value in values) {
                    }
                    while (a) {
                        a = false;
                    }
                    do {
                        b = false;
                    } while (b);
                    if (a && b || values.Length > 0) {
                    }
                    try {
                        return a ? 1 : 0;
                    } catch (Exception ex) {
                        return 2;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Score", 2, 20, 10) }, methods);
    }

    [Fact]
    public void VisitsNestedDecisionNodesInsideOtherDecisionNodes()
    {
        string source = """
            class Sample {
                int Nested(bool a, bool b, int[] values) {
                    for (int i = 0; i < values.Length; i++) {
                        if (a) {
                        }
                    }
                    foreach (int value in values) {
                        if (b) {
                        }
                    }
                    while (a) {
                        if (b) {
                        }
                        a = false;
                    }
                    do {
                        if (a) {
                        }
                        b = false;
                    } while (b);
                    try {
                        return a ? (b ? 1 : 0) : 2;
                    } catch (Exception ex) {
                        if (values.Length > 0) {
                            return values[0];
                        }
                        return 3;
                    }
                }

                int Switched(int value) {
                    switch (value) {
                        case 1:
                            if (value > 0) {
                                return 1;
                            }
                            return 0;
                        default:
                            return 2;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Nested", 2, 29, 13),
                new("Switched", 31, 41, 4),
            },
            methods);
    }

    [Fact]
    public void SwitchExpressionArmsAndNullOperatorsDoNotCount()
    {
        string source = """
            class Sample {
                int Kind(int x) {
                    return x switch { 1 => 10, _ => 0 };
                }

                string Name(string? s) {
                    return s?.ToString() ?? "hi";
                }

                bool Both(bool a, bool b) {
                    return a & b | a;
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Kind", 2, 4, 1),
                new("Name", 6, 8, 1),
                new("Both", 10, 12, 1),
            },
            methods);
    }

    [Fact]
    public void ElseForeachDeconstructionAndCatchFilterDoNotCountExtra()
    {
        string source = """
            class Sample {
                int Sign(int x) {
                    if (x > 0) {
                        return 1;
                    } else {
                        return 0;
                    }
                }

                int Total(System.Collections.Generic.List<(int A, int B)> pairs) {
                    int total = 0;
                    foreach (var (a, b) in pairs) {
                        total += a + b;
                    }
                    return total;
                }

                int Guarded(bool flag) {
                    try {
                        return flag ? 1 : 0;
                    } catch (System.Exception ex) when (ex.Message.Length > 0) {
                        return 2;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Sign", 2, 8, 2),
                new("Total", 10, 16, 2),
                new("Guarded", 18, 24, 3),
            },
            methods);
    }
}
