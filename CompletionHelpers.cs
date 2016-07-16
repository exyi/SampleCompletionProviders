using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleCompletionProviders
{
    public static class CompletionHelpers
    {
        public static MemberAccessExpressionSyntax GetCurrentMemberAccess(this SyntaxNode node, int currentPosition)
        {
            var allNodes = node.DescendantNodes(n => n.FullSpan.Contains(currentPosition - 1)); // all nodes that contain currentPosition
            return allNodes.OfType<MemberAccessExpressionSyntax>().FirstOrDefault(m => m.OperatorToken.FullSpan.Contains(currentPosition - 1)) ?? // member access expression witch ends here
                allNodes.OfType<SimpleNameSyntax>().FirstOrDefault(m => m.Span.Contains(currentPosition - 1))?.Parent as MemberAccessExpressionSyntax; // or parent of identifier which contains currentPosition
        }
    }
}
