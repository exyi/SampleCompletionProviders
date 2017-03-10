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
using Microsoft.CodeAnalysis.Formatting;

namespace SampleCompletionProviders
{
    [Export(typeof(IPostfixSnippet))]
    public class ReturnSnippet : IPostfixSnippet
    {
        public SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey)
        {
            var statement = memberAccess.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
            if (statement != null)
            {
                // insert return statement after current statement
                SyntaxNode oldNode;
                BlockSyntax block;
                int index;
                if (statement.Parent is BlockSyntax)
                {
                    // after current statement in the block
                    oldNode = block = statement.Parent as BlockSyntax;
                    index = block.Statements.IndexOf(statement) + 1;
                    block = block.ReplaceNode(statement,
                        statement.ReplaceNode(memberAccess, (memberAccess as MemberAccessExpressionSyntax).Expression)).FixStatement();
                }
                else
                {
                    // if it is not in block, insert one
                    // if (...) Method().return ---> if (...) { Method(); return; }
                    oldNode = statement;
                    block = SyntaxFactory.Block(statement.ReplaceNode(memberAccess, (memberAccess as MemberAccessExpressionSyntax).Expression).FixStatement().WithoutTrivia());
                    index = 1;
                }
                // insert return to the found or created block
                SyntaxNode newBlock = block.WithStatements(block.Statements.Insert(index, SyntaxFactory.ReturnStatement()))
                    .WithAdditionalAnnotations(Formatter.Annotation);
                return model.SyntaxTree.GetRoot().ReplaceNode(oldNode, newBlock);
            }
            return null;
        }

        public CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model)
        {
            if (targetType.SpecialType == SpecialType.System_Void)
            {
                // TODO: here it should consider return type of current method, but it is not that easy (yield, anonymous method, async)
                // roslyn does that, but it is internal interface ITypeInferenceService specifically CSharpTypeInferenceService.TypeInferrer.InferTypeForReturnStatement method
                // it is easy to call it by reflection, but this project is an sample how to do stuff nicely ;)
                return CompletionItem.Create("return");
            }
            return null;
        }
    }
}
