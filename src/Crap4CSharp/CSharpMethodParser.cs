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
        if (node.Body is null)
            return;
        if (node.Modifiers.Any(SyntaxKind.AbstractKeyword))
            return;
        if (node.Parent is not BaseTypeDeclarationSyntax declaringType)
            return;
        for (SyntaxNode? parent = declaringType.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is BaseTypeDeclarationSyntax)
                return;
        }

        _complexity.Reset();
        _complexity.Visit(node.Body);
        int start = _lines.GetLinePosition(node.Span.Start).Line + 1;
        int end = _lines.GetLinePosition(node.Body.Span.End).Line + 1;
        Methods.Add(new MethodDescriptor(node.Identifier.ValueText, start, end, _complexity.Complexity));
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
