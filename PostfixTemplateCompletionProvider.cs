using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using System.Composition;
using Microsoft.CodeAnalysis;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace SampleCompletionProviders
{
    [ExportCompletionProvider(nameof(PostfixTemplateCompletionProvider), LanguageNames.CSharp)]
    public class PostfixTemplateCompletionProvider : CompletionProvider
    {
        public PostfixTemplateCompletionProvider()
        {
        }

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            if (!context.Document.SupportsSemanticModel) return;

            var model = await context.Document.GetSemanticModelAsync();
            var treeRoot = await context.Document.GetSyntaxRootAsync();

            // find the current member access
            var node = treeRoot.GetCurrentMemberAccess(context.Position);
            if (node == null) return;
            var target = node.Expression;
            var targetType = model.GetTypeInfo(target).Type;
            if (targetType == null) return;
            // if type is bool -> 'not' suggestion
            if (targetType.SpecialType == SpecialType.System_Boolean)
            {
                context.AddItem(CompletionItem.Create("not", rules: CompletionItemRules.Create(), tags: ImmutableArray.Create(new[] { "Snippet" })));
            }
            // if void -> 'return' snippet
            else if (targetType.SpecialType == SpecialType.System_Void)
            {
                // TODO: here it should consider return type of current method, but it is not that easy (yield, anonymous method, async)
                // roslyn does that, but it is internal interface ITypeInferenceService specifically CSharpTypeInferenceService.TypeInferrer.InferTypeForReturnStatement method
                // it is easy to call it by reflection, but this project is an sample how to do stuff nicely ;)
                context.AddItem(CompletionItem.Create("return", tags: ImmutableArray.Create(new[] { "Snippet" })));
            }
        }

        public override bool ShouldTriggerCompletion(SourceText text, int caretPosition, CompletionTrigger trigger, OptionSet options)
        {
            if (trigger.Character == '.')
                return true;
            return base.ShouldTriggerCompletion(text, caretPosition, trigger, options);
        }

        public override async Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
        {
            // custom completion logic
            var model = await document.GetSemanticModelAsync();
            var tree = model.SyntaxTree;
            var root = tree.GetRoot();
            SyntaxNode newRoot = null;
            var memberAccess = tree.GetRoot().GetCurrentMemberAccess(item.Span.Start);
            if (memberAccess != null)
            {
                var target = memberAccess.Expression;
                if (item.DisplayText == "not") // not template expansion
                {
                    // when it is commited with '.' take the parent to get to the top node
                    if (commitKey == '.' && memberAccess.Parent is MemberAccessExpressionSyntax) memberAccess = (MemberAccessExpressionSyntax)memberAccess.Parent;
                    ExpressionSyntax newNode = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, target.WithoutTrivia());

                    // insert parenthesis when commited with dot
                    // and copy trivia (whitespaces) from the original node
                    if (commitKey == '.') newNode = SyntaxFactory.ParenthesizedExpression(newNode).WithTriviaFrom(memberAccess);
                    else newNode = newNode.WithTriviaFrom(memberAccess);

                    newRoot = root.ReplaceNode(memberAccess, newNode);
                }
                else if (item.DisplayText == "return") // return teplace expansion
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
                            block = block.ReplaceNode(statement, FixStatement(statement.ReplaceNode(memberAccess, (memberAccess as MemberAccessExpressionSyntax).Expression))).WithTriviaFrom(memberAccess);
                        }
                        else
                        {
                            // if it is not in block, insert one
                            // if (...) Method().return ---> if (...) { Method(); return; }
                            oldNode = statement;
                            block = SyntaxFactory.Block(FixStatement(statement.ReplaceNode(memberAccess, (memberAccess as MemberAccessExpressionSyntax).Expression).WithoutTrivia())).WithTriviaFrom(memberAccess);
                            index = 1;
                        }
                        // insert return to the found or created block
                        SyntaxNode newBlock = block.WithStatements(block.Statements.Insert(index, SyntaxFactory.ReturnStatement()))
                            .WithAdditionalAnnotations(Formatter.Annotation);
                        newRoot = root.ReplaceNode(oldNode, newBlock);
                    }
                }
                if (newRoot == null) newRoot = root.ReplaceNode(memberAccess, target);
                // format tree
                var newTree = tree.WithRootAndOptions(Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace), tree.Options);
                // return changes done in the new tree
                var changes = newTree.GetChanges(tree);
                return CompletionChange.Create(changes.ToImmutableArray(), includesCommitCharacter: false);
            }
            return await base.GetChangeAsync(document, item, commitKey, cancellationToken);
        }
        
        private StatementSyntax FixStatement(StatementSyntax statement)
        {
            // insert missing semicolon to the statement
            if (statement is ExpressionStatementSyntax)
            {
                var est = statement as ExpressionStatementSyntax;
                if (est.SemicolonToken.Span.Length == 0) return est.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            }
            return statement;
        }
    }
}
