using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SampleCompletionProviders
{
    [Export(typeof(IPostfixSnippet))]
    public class NotSnippet : IPostfixSnippet
    {
        public SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey)
        {
            if (commitKey == '.' && memberAccess.Parent is MemberAccessExpressionSyntax) memberAccess = (MemberAccessExpressionSyntax)memberAccess.Parent;
            ExpressionSyntax newNode = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, memberAccess.Expression.WithoutTrivia());

            // insert parenthesis when commited with dot
            // and copy trivia (whitespaces) from the original node
            if (commitKey == '.') newNode = SyntaxFactory.ParenthesizedExpression(newNode);

            return model.SyntaxTree.GetRoot().ReplaceNode(memberAccess, newNode);
        }

        public CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model)
        {
            if (targetType.SpecialType == SpecialType.System_Boolean)
            {
                return CompletionItem.Create("not");
            }
            return null;
        }
    }

    [Export(typeof(IPostfixSnippet))]
    public class AwaitSnippet : IPostfixSnippet
    {
        public SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey)
        {
            if (commitKey == '.' && memberAccess.Parent is MemberAccessExpressionSyntax) memberAccess = (MemberAccessExpressionSyntax)memberAccess.Parent;
            ExpressionSyntax newNode = SyntaxFactory.AwaitExpression(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.AwaitKeyword, SyntaxFactory.ParseTrailingTrivia(" ")), memberAccess.Expression.WithoutTrivia());

            // insert parenthesis when commited with dot
            // and copy trivia (whitespaces) from the original node
            if (commitKey == '.') newNode = SyntaxFactory.ParenthesizedExpression(newNode);

            return model.SyntaxTree.GetRoot().ReplaceNode(memberAccess, newNode);
        }

        public CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model)
        {
            if (targetType.GetMembers("GetAwaiter").Any())
            {
                return CompletionItem.Create("await");
            }
            return null;
        }
    }

    [Export(typeof(IPostfixSnippet))]
    public class TypeofSnippet : IPostfixSnippet
    {
        public SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey)
        {
            if (commitKey == '.' && memberAccess.Parent is MemberAccessExpressionSyntax) memberAccess = (MemberAccessExpressionSyntax)memberAccess.Parent;
            ExpressionSyntax newNode = SyntaxFactory.TypeOfExpression(memberAccess.Expression.WithoutTrivia() as TypeSyntax);

            // insert parenthesis when commited with dot
            // and copy trivia (whitespaces) from the original node
            //if (commitKey == '.') newNode = SyntaxFactory.ParenthesizedExpression(newNode);

            return model.SyntaxTree.GetRoot().ReplaceNode(memberAccess, newNode);
        }

        public CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model)
        {
            //var operation = model.GetOperation(targetExpression);
            if (targetExpression is TypeSyntax && targetType.Kind != SymbolKind.ErrorType)
            {
                return CompletionItem.Create("typeof");
            }
            return null;
        }
    }

    [Export(typeof(IPostfixSnippet))]
    public class ForeachSnippet : IWorkspaceUpdatingSnippet
    {
        public SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey)
        {
            return null;
        }

        public CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model)
        {
            if (targetType.SpecialType == SpecialType.System_Boolean)
            {
                return CompletionItem.Create("foreach");
            }
            return null;
        }

        public void Update(Document doc, ExpressionSyntax targetExpression, char? commitKey)
        {
            
        }
    }
}
