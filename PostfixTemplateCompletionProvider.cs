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
using System.IO;
using Microsoft.VisualStudio.LanguageServices;

namespace SampleCompletionProviders
{
    public interface IPostfixSnippet
    {
        CompletionItem GetCompletion(CompletionContext context, ExpressionSyntax targetExpression, ITypeSymbol targetType, SemanticModel model);
        SyntaxNode ChangeTree(Document doc, SemanticModel model, MemberAccessExpressionSyntax memberAccess, char? commitKey);
    }

    public interface IWorkspaceUpdatingSnippet : IPostfixSnippet
    {
        void Update(Document doc, ExpressionSyntax targetExpression, char? commitKey);
    }

    [ExportCompletionProvider(nameof(PostfixTemplateCompletionProvider), LanguageNames.CSharp)]
    public class PostfixTemplateCompletionProvider : CompletionProvider
    {
        private const string CurrentSnipperProperty = "CurrentSnippetType[1e730fc3-76c0-45ba-8da0-a60fdf8d3f9d]";

        private IEnumerable<IPostfixSnippet> snippets;
        private readonly VisualStudioWorkspace workspace;

        [ImportingConstructor]
        public PostfixTemplateCompletionProvider(
            [ImportMany] IEnumerable<IPostfixSnippet> snippets,
            VisualStudioWorkspace workspace)
        {
            this.snippets = snippets;
            this.workspace = workspace;
            workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
        }
        Action<Workspace, Solution> ll;
        private void Workspace_WorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            if (ll != null)
            {
                ll.Invoke(workspace, e.NewSolution);
                ll = null;
            }
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

            foreach (var ss in snippets)
            {
                if (ss.GetCompletion(context, target, targetType, model) is CompletionItem ci)
                {
                    if (ci.Tags.Length == 0) ci = ci.AddTag("Snippet");
                    context.AddItem(ci.AddProperty(CurrentSnipperProperty, ss.GetType().ToString()));
                }
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
            var memberAccess = tree.GetRoot().GetCurrentMemberAccess(item.Span.Start);
            if (!item.Properties.TryGetValue(CurrentSnipperProperty, out var currentSnippet)) return CompletionChange.Create(ImmutableArray<TextChange>.Empty);

            if (memberAccess != null)
            {
                var snip = snippets.FirstOrDefault(s => s.GetType().ToString() == currentSnippet);
                var newRoot = snip.ChangeTree(document, model, memberAccess, commitKey);
                string expectedText = null;
                TextSpan nodeSpan = default(TextSpan);
                if (snip is IWorkspaceUpdatingSnippet wsnip)
                {
                    ll = async (workspace, solution) => {
                        var doc = solution.GetDocument(document.Id);
                        if ((await doc.GetTextAsync()).ToString() == expectedText)
                            wsnip.Update(doc, (await doc.GetSyntaxRootAsync()).FindNode(nodeSpan) as ExpressionSyntax, commitKey);
                    };
                }

                //if (newRoot == null) newRoot = root.ReplaceNode(memberAccess, memberAccess.Expression);
                if (newRoot == null) newRoot = root.ReplaceNode(memberAccess, memberAccess.WithName(SyntaxFactory.IdentifierName(item.DisplayText)));
                // format tree
                var newTree = tree.WithRootAndOptions(Formatter.Format(newRoot, Formatter.Annotation, document.Project.Solution.Workspace), tree.Options);
                // return changes done in the new tree
                var changes = newTree.GetChanges(tree).Select(c => TrimWhitespaceChnage(c, tree.GetText())).Select(c => MoveToNode(c, tree.GetText(), memberAccess.Expression.Span)).ToArray();
                ImmutableArray<TextChange> finalChanges = ImmutableArray.Create(MergeChanges(changes, tree.GetText()));
                expectedText = tree.GetText().WithChanges(finalChanges).ToString();
                nodeSpan = MoveSpan(memberAccess.Expression.Span, finalChanges);
                return CompletionChange.Create(finalChanges, includesCommitCharacter: false);
            }
            return await base.GetChangeAsync(document, item, commitKey, cancellationToken);
        }

        private TextSpan MoveSpan(TextSpan span, ImmutableArray<TextChange> changes)
        {
            foreach (var change in changes)
            {
                if (change.Span.End < span.Start) span = new TextSpan(span.Start - (change.Span.Length - change.NewText.Length), span.Length);
                // TODO: else
            }
            return span;
        }

        private TextChange MergeChanges(TextChange[] change, SourceText text)
        {
            if (change.Length == 1) return change[0];
            Array.Sort(change, (a, b) => a.Span.Start.CompareTo(b.Span.Start));
            var span = TextSpan.FromBounds(change.First().Span.Start, change.Last().Span.Start);
            int lastSpan = span.Start;
            var sb = new StringWriter();
            foreach (var c in change)
            {
                text.Write(sb, TextSpan.FromBounds(lastSpan, c.Span.Start));
                sb.Write(c.NewText);
                lastSpan = c.Span.End;
            }
            return new TextChange(span, sb.ToString());
        }

        private TextChange TrimWhitespaceChnage(TextChange change, SourceText text)
        {
            if (change.Span.Length == 0) return change;

            var start = change.Span.Start;
            var end = change.Span.End;
            if (char.IsWhiteSpace(text[start - 1])) while (char.IsWhiteSpace(text[start]) && start < end) start++;
            while (char.IsWhiteSpace(text[end - 1]) && start < end) end--;
            return new TextChange(TextSpan.FromBounds(start, end), change.NewText.Trim());
        }

        private TextChange MoveToNode(TextChange change, SourceText text, TextSpan home)
        {
            if (change.Span.Length > 0) return change;

            var position = change.Span.Start;
            var direction = home.Start.CompareTo(position);
            while (!home.Contains(position) && char.IsWhiteSpace(text[position + direction])) position += direction;
            return new TextChange(new TextSpan(position, 0), change.NewText);
        }
    }
}
