using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crap4CSharp;

internal static class CSharpMethodParser
{
    public static IReadOnlyList<MethodDescriptor> Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source, new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None));
        var collector = new MethodCollector(tree);
        collector.Visit(tree.GetRoot());
        return collector.Methods;
    }
}

internal sealed class MethodCollector : CSharpSyntaxWalker
{
    internal readonly List<MethodDescriptor> Methods = [];
    private readonly TextLineCollection _lines;
    private readonly CcWalker _complexity = new();

    internal MethodCollector(SyntaxTree tree)
    {
        _lines = tree.GetText().Lines;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Body is null && node.ExpressionBody is null)
            return;
        if (node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            return;
        if (node.Parent is not BaseTypeDeclarationSyntax)
            return;

        AddMethod(node.Identifier.ValueText, node.Span.Start, (CSharpSyntaxNode?)node.Body ?? node.ExpressionBody!);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (node.Body is null && node.ExpressionBody is null)
            return;
        if (node.Parent is not BaseTypeDeclarationSyntax)
            return;

        AddMethod(node.Identifier.ValueText, node.Span.Start, (CSharpSyntaxNode?)node.Body ?? node.ExpressionBody!);
    }

    private void AddMethod(string name, int spanStart, CSharpSyntaxNode implementation)
    {
        _complexity.Reset();
        _complexity.Visit(implementation);
        var start = _lines.GetLinePosition(spanStart).Line + 1;
        var end = _lines.GetLinePosition(implementation.Span.End).Line + 1;
        Methods.Add(new MethodDescriptor(name, start, end, _complexity.Complexity));
        foreach (var local in implementation.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
        {
            var body = (CSharpSyntaxNode?)local.Body ?? local.ExpressionBody;
            if (body is null)
            {
                continue;
            }
            var localComplexity = new CcWalker();
            localComplexity.Visit(body);
            var localStart = _lines.GetLinePosition(local.Span.Start).Line + 1;
            var localEnd = _lines.GetLinePosition(body.Span.End).Line + 1;
            Methods.Add(new MethodDescriptor(local.Identifier.ValueText, localStart, localEnd, localComplexity.Complexity));
        }
    }
}

internal sealed class CcWalker : CSharpSyntaxWalker
{
    internal int Complexity = 1;

    internal void Reset()
    {
        Complexity = 1;
    }

    public override void VisitIfStatement(IfStatementSyntax node) { Complexity++; base.VisitIfStatement(node); }
    public override void VisitForStatement(ForStatementSyntax node) { Complexity++; base.VisitForStatement(node); }
    public override void VisitForEachStatement(ForEachStatementSyntax node) { Complexity++; base.VisitForEachStatement(node); }
    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) { Complexity++; base.VisitForEachVariableStatement(node); }
    public override void VisitWhileStatement(WhileStatementSyntax node) { Complexity++; base.VisitWhileStatement(node); }
    public override void VisitDoStatement(DoStatementSyntax node) { Complexity++; base.VisitDoStatement(node); }
    public override void VisitCatchClause(CatchClauseSyntax node) { Complexity++; base.VisitCatchClause(node); }
    public override void VisitConditionalExpression(ConditionalExpressionSyntax node) { Complexity++; base.VisitConditionalExpression(node); }
    public override void VisitCaseSwitchLabel(CaseSwitchLabelSyntax node) { Complexity++; base.VisitCaseSwitchLabel(node); }
    public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node) { Complexity++; base.VisitCasePatternSwitchLabel(node); }
    public override void VisitDefaultSwitchLabel(DefaultSwitchLabelSyntax node) { Complexity++; base.VisitDefaultSwitchLabel(node); }
    public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node) { Complexity++; base.VisitSwitchExpressionArm(node); }
    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) { }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression))
            Complexity++;
        base.VisitBinaryExpression(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) { }
    public override void VisitStructDeclaration(StructDeclarationSyntax node) { }
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node) { }
    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) { }
}