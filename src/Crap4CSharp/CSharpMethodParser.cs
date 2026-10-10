using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crap4CSharp;

/// <summary>
/// C# mirror of Java's <c>JavaMethodParser</c>: extracts concrete methods with
/// cyclomatic complexity and 1-based start/end lines. Rule mapping and parity
/// risks: <c>.scratch/csharp-port/research/parser-roslyn-paridad.md</c>.
/// Edge decisions (ticket 03): <c>default</c> counts +1; switch-expression arms,
/// <c>??</c> and <c>?.</c> do not count; local functions count like lambdas.
/// </summary>
internal static class CSharpMethodParser
{
    public static IReadOnlyList<MethodDescriptor> Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        List<MethodDescriptor> result = [];
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // Mirrors getBody()==null / abstract exclusion (covers abstract, extern,
            // partial-without-body, interface declarations, and => expression bodies,
            // which are excluded in v1 per research section 3.8).
            if (method.Body is null)
                continue;
            if (method.Modifiers.Any(SyntaxKind.AbstractKeyword))
                continue;
            // Ticket 09 scope: methods of nested types are out. Constructors,
            // properties, accessors and operators are sibling syntax types and
            // never enter this enumeration (mirrors getReturnType()==null).
            if (method.Parent is not BaseTypeDeclarationSyntax declaringType)
                continue;
            if (declaringType.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any())
                continue;

            var walker = new CcWalker();
            walker.Visit(method.Body);
            // GetLineSpan is 0-based on both ends: +1 like javac's 1-based LineMap.
            // Start comes from the whole method span (modifiers/attributes included),
            // end from the body span (closing '}'), mirroring the oracle.
            var start = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
            var end = tree.GetLineSpan(method.Body.Span).EndLinePosition.Line + 1;
            result.Add(new MethodDescriptor(method.Identifier.ValueText, start, end, walker.Complexity));
        }
        return result;
    }

    internal sealed class CcWalker : CSharpSyntaxWalker
    {
        internal int Complexity = 1;

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

        // Mirrors ComplexityCounter.visitClass -> null: never descend into types
        // declared inside the counted scope. No VisitElseClause override (else is
        // free), no VisitSwitchExpressionArm / VisitCatchFilterClause / coalesce /
        // conditional-access overrides (ticket 03: they do not count).
        public override void VisitClassDeclaration(ClassDeclarationSyntax node) { }
        public override void VisitStructDeclaration(StructDeclarationSyntax node) { }
        public override void VisitRecordDeclaration(RecordDeclarationSyntax node) { }
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) { }
    }
}
