namespace Crap4CSharp.Tests;

// xUnit mirror of Java's JavaMethodParserTest, adapted to C# source fixtures.
// Same expected CC/exclusions; C#-only edges per ticket 03 (default counts;
// switch-expression arms, ?? and ?. do not; local functions count like lambdas).
public sealed class CSharpMethodParserTest
{
    [Fact]
    public void ExtractsConcreteMethodsWithLinesAndComplexity()
    {
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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
        var source = """
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

    [Fact]
    public void CountsPatternCaseLabels()
    {
        var source = """
            class Sample {
                int Classify(object value) {
                    switch (value) {
                        case int i:
                            return 1;
                        case string s:
                            return 2;
                        default:
                            return 0;
                    }
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Classify", 2, 11, 4),
            },
            methods);
    }

    [Fact]
    public void SkipsAbstractMethodWithBody()
    {
        var source = """
            abstract class Sample {
                abstract int Missing() {
                    return 1;
                }

                int Present() {
                    return 2;
                }
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Present", 6, 8, 1) }, methods);
    }

    [Fact]
    public void SkipsMethodDeclarationOutsideAnyType()
    {
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class Sample {}\n");
        var collector = new MethodCollector(tree);
        var orphan = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.MethodDeclaration(
            Microsoft.CodeAnalysis.CSharp.SyntaxFactory.PredefinedType(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Token(
                    Microsoft.CodeAnalysis.CSharp.SyntaxKind.IntKeyword)),
            "Orphan")
            .WithBody(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Block());

        collector.VisitMethodDeclaration(orphan);

        Assert.Empty(collector.Methods);
    }

    [Fact]
    public void ArrowBodiedMethodIsReportedWithComplexityOne()
    {
        var source = """
            class Sample {
                public static int Add(int a, int b) => a + b;
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Add", 2, 2, 1) }, methods);
    }

    [Fact]
    public void ArrowBodiedMethodCountsDecisionNodes()
    {
        var source = """
            class Sample {
                int Pick(bool a, bool b, int x) => a && b || x > 0 ? 1 : 0;
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(new List<MethodDescriptor> { new("Pick", 2, 2, 4) }, methods);
    }

    [Fact]
    public void BlockBodiedMethodAlongsideArrowIsUnchanged()
    {
        var source = """
            class Sample {
                int Block(int x) {
                    return x;
                }

                int Arrow(int x) => x;
            }
            """;

        var methods = CSharpMethodParser.Parse(source);

        Assert.Equal(
            new List<MethodDescriptor>
            {
                new("Block", 2, 4, 1),
                new("Arrow", 6, 6, 1),
            },
            methods);
    }

    [Fact]
    public void NestedTypeDeclarationsDoNotChangeComplexity()
    {
        var walker = new CcWalker();
        var classNode = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class Local {}\n")
            .GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().Single();
        var structNode = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("struct Local {}\n")
            .GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>().Single();
        var recordNode = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("record Local(int X);\n")
            .GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.RecordDeclarationSyntax>().Single();
        var interfaceNode = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("interface Local {}\n")
            .GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax>().Single();

        walker.VisitClassDeclaration(classNode);
        walker.VisitStructDeclaration(structNode);
        walker.VisitRecordDeclaration(recordNode);
        walker.VisitInterfaceDeclaration(interfaceNode);

        Assert.Equal(1, walker.Complexity);
    }
}