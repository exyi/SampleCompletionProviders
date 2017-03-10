using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace SampleCompletionProviders
{
    [ExportCompletionProvider(nameof(ConfigureAwaitCompletionProvider), LanguageNames.CSharp)]
    public class ConfigureAwaitCompletionProvider : CompletionProvider
    {
        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            // wrap it in try catch, because exception for some reason sometimes crash entire VisualStudio
            try
            {
                // check if semantic model is supported
                if (!context.Document.SupportsSemanticModel) return;

                var syntaxRoot = await context.Document.GetSyntaxRootAsync();
                var semanticModel = await context.Document.GetSemanticModelAsync();

                // find the MemberAccessExpression
                var currentNode = syntaxRoot.GetCurrentMemberAccess(context.Position);
                if (currentNode == null) return;

                var typeOfExpression = semanticModel.GetTypeInfo(currentNode.Expression);
                if (typeOfExpression.Type.Name == "Task" && typeOfExpression.Type.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks") // if memer access on System.Threading.Tasks.Task
                {
                    // add the completion item
                    context.AddItem(CompletionItem.Create("ConfigureAwait(false)", tags: ImmutableArray.Create(new[] { "Method", "Public" })));
                }
            }
            catch(Exception ex)
            {
            }
        }

        public override async Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            return CompletionDescription.Create(ImmutableArray.Create(new TaggedText[] {
                new TaggedText(TextTags.Text, "Just a shortcut for the "),
                new TaggedText(TextTags.Class, "Task"),
                new TaggedText(TextTags.Method, ".ConfigureAwait"),
                new TaggedText(TextTags.Text, " method.")
            }));
        }

        public override Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
        {
            return base.GetChangeAsync(document, item, commitKey, cancellationToken);
        }
    }
}
