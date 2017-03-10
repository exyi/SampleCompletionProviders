using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;

namespace SampleCompletionProviders
{
    public static class CompletionHelpers
    {
        // unfortunately current node is not in the CompletionContext, we have to find it ourselves
        public static MemberAccessExpressionSyntax GetCurrentMemberAccess(this SyntaxNode node, int currentPosition)
        {
            var allNodes = node.DescendantNodes(n => n.FullSpan.Contains(currentPosition - 1)); // all nodes that contain currentPosition
            return allNodes.OfType<MemberAccessExpressionSyntax>().FirstOrDefault(m => m.OperatorToken.FullSpan.Contains(currentPosition - 1)) ?? // member access expression witch ends here
                allNodes.OfType<SimpleNameSyntax>().FirstOrDefault(m => m.Span.Contains(currentPosition - 1))?.Parent as MemberAccessExpressionSyntax; // or parent of identifier which contains currentPosition
        }

        public static T FixStatement<T>(this T statement)
            where T : StatementSyntax
        {
            // insert missing semicolon to the statement
            if (statement is ExpressionStatementSyntax)
            {
                var est = statement as ExpressionStatementSyntax;
                if (est.SemicolonToken.Span.Length == 0) return (T)(StatementSyntax)est.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            return statement;
        }
    }
}
