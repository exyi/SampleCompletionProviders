using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;

namespace SampleCompletionProviders
{
	[ExportCompletionProvider(nameof(ReflectionGetMemberCompletionProvider), LanguageNames.CSharp)]
	class ReflectionGetMemberCompletionProvider : CompletionProvider
	{
		public ReflectionGetMemberCompletionProvider()
		{
		}

		public override async Task ProvideCompletionsAsync(CompletionContext context)
		{
			if (!context.Document.SupportsSemanticModel) return;

			var model = await context.Document.GetSemanticModelAsync();
			var tree = model.SyntaxTree;
			var node = GetCurrentLiteral(tree, context.Position);

            // if in string literal in method arguments
			if (node is LiteralExpressionSyntax && node.IsKind(SyntaxKind.StringLiteralExpression) && node.Parent is ArgumentSyntax && node.Parent.Parent is ArgumentListSyntax)
			{
				var memberReference = (node.Parent.Parent.Parent as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;
				if (memberReference != null)
				{
                    // get possible methods
					var memberSymbol = model.GetSymbolInfo(memberReference);
					var methodName = memberSymbol.CandidateSymbols.Concat(new[] { memberSymbol.Symbol }).OfType<IMethodSymbol>().Select(GetFullName).First(); // it should be single method name, as the symbols are oveloads
					var typeSymbol = TryFindResultType(memberReference.Expression, model);
					if (typeSymbol != null)
					{
						if (methodName == typeof(Type).FullName + ".GetMethod")
						{
							context.AddItems(GetCompletionItems<IMethodSymbol>(typeSymbol, model, context.Position));
						}
						if (methodName == typeof(Type).FullName + ".GetField")
						{
							context.AddItems(GetCompletionItems<IFieldSymbol>(typeSymbol, model, context.Position));
						}
						if (methodName == typeof(Type).FullName + ".GetProperty")
						{
							context.AddItems(GetCompletionItems<IPropertySymbol>(typeSymbol, model, context.Position));
						}
                        if(methodName == typeof(Type).FullName + ".GetMembers")
                        {
                            context.AddItems(GetCompletionItems<ISymbol>(typeSymbol, model, context.Position));
                        }
					}
				}
			}
		}

		public override async Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
		{
			var tree = await document.GetSyntaxTreeAsync();
			var node = GetCurrentLiteral(tree, item.Span.Start);
            // insert the insertion text property
			return CompletionChange.Create(ImmutableArray<TextChange>.Empty.Add(new TextChange(node.Span, item.Properties[InsertionTextKey])));
		}
		const string InsertionTextKey = "InsertionText/914D9212-A782-45C9-970F-405B732E10B5"; // hopefully unique id

		public CompletionItem CreateCompletionItem(string insertionText, string descriptionText) =>
			CompletionItem.Create(descriptionText,
				properties: ImmutableDictionary<string, string>.Empty.Add(InsertionTextKey, insertionText), // add custom insertion text property to be used in GetChangeAsync
				rules: CompletionItemRules.Create(
                    // allow usage of open paren while writing the identifier
                    filterCharacterRules: ImmutableArray<CharacterSetModificationRule>.Empty.Add(CharacterSetModificationRule.Create(CharacterSetModificationKind.Add, '(')),
				    commitCharacterRules: ImmutableArray<CharacterSetModificationRule>.Empty.Add(CharacterSetModificationRule.Create(CharacterSetModificationKind.Remove, '('))
			));

		public ITypeSymbol TryFindResultType(ExpressionSyntax expression, SemanticModel model)
		{
            // try find the value of expression
			if (expression is TypeOfExpressionSyntax) // typeof(...)
			{
				return model.GetSymbolInfo(((TypeOfExpressionSyntax)expression).Type).Symbol as ITypeSymbol;
			}
            // TODO: support Type.GetType(""), ...
			return null;
		}

		public IEnumerable<CompletionItem> GetCompletionItems<TSymbol>(ITypeSymbol type, SemanticModel model, int filePosition)
			where TSymbol : ISymbol
		{
			return GetCompletions<TSymbol>(type).GroupBy(d => d.MemberSymbol.MetadataName)
				.SelectMany(g =>
				{
                    // if only one method of the name, don't have to put arguments here
					if (g.Count() == 1) return new[] { CreateCompletionItem(FormatCompletion(g.First(), false, model, filePosition), GetDisplayText(g.First(), model, filePosition)) };
                    // else put parameters here
					else return g.Select(c => CreateCompletionItem(FormatCompletion(c, true, model, filePosition), GetDisplayText(c, model, filePosition)));
				});
		}

		public string GetDisplayText(ReflectionMemberCompletionData completionData, SemanticModel model, int filePosition)
		{
			return completionData.MemberSymbol.Name + (completionData.ArgumentTypes == null ? "" : "(" + string.Join(", ", completionData.ArgumentTypes.Select(t => t.ToMinimalDisplayString(model, filePosition))) + ")");
		}

		public string FormatCompletion(ReflectionMemberCompletionData completionData, bool includeMethodParameters, SemanticModel model, int filePosition)
		{
			if (includeMethodParameters && completionData.ArgumentTypes == null) throw new ArgumentException("", nameof(includeMethodParameters));

			var parts = new List<string>();
			parts.Add(FormatMemberName(completionData.MemberSymbol, model, filePosition));
			bool hasBindingFlags = completionData.BindingFlags.Except(new[] { "Public", "Instance" }).Count() > 0;
			if (hasBindingFlags) // Public | Instance are implicit
			{
				parts.Add(FormatBindingFlags(completionData.BindingFlags));
			}
			if (includeMethodParameters)
			{
				if (hasBindingFlags)
				{
					parts.Add("null"); // have to put Binder here if includes both binding flags and method parameters
					//parts.Add("CallingConvention.Any"); // this is actualy optional
				}
				parts.Add(FormatParameterTypes(completionData.ArgumentTypes, model, filePosition));
				if (hasBindingFlags)
				{
					parts.Add("null");
				}
			}
			return string.Join(", ", parts);
		}

		public string FormatMemberName(ISymbol symbol, SemanticModel model, int filePosition)
		{
			if (symbol.DeclaredAccessibility == Accessibility.Public && symbol.CanBeReferencedByName)
			{
                // use nameof if the symbol is public
				var displayString = symbol.ToMinimalDisplayString(model, filePosition, format: new SymbolDisplayFormat(memberOptions: SymbolDisplayMemberOptions.IncludeContainingType));
				return $"nameof({displayString})";
			}
			else return "\"" + symbol.Name + "\"";
		}

		public string FormatParameterTypes(ITypeSymbol[] parameters, SemanticModel semanticModel, int filePosition) => parameters.Length == 0 ?
			"Type.EmptyTypes" : // empty array constant
			"new[] { " + string.Join(", ", parameters.Select(p => $"typeof({p.ToMinimalDisplayString(semanticModel, filePosition)})")) + " }";
		
		public string FormatBindingFlags(string[] flags) => string.Join(" | ", flags.Select(f => "BindingFlags." + f));

		public IEnumerable<ReflectionMemberCompletionData> GetCompletions<TSymbol>(ITypeSymbol type)
			where TSymbol : ISymbol
		{
			// type ListAllMembers<TSymbol>() foreach { member -> ... }
			foreach (var member in ListAllMembers<TSymbol>(type))
			{
				var inTheType = member.ContainingType == type;
				if (member.DeclaredAccessibility == Accessibility.Private && !inTheType) continue; // private members not in the type can not be found
				bool isNonPublic;
				bool isPublic;
				switch (member.DeclaredAccessibility)
				{
					case Accessibility.Private:
					case Accessibility.ProtectedAndInternal:
					case Accessibility.Protected:
						isNonPublic = true;
						isPublic = false;
						break;
						
					case Accessibility.Public:
						isNonPublic = false;
						isPublic = true;
						break;
                    case Accessibility.Internal:
                    case Accessibility.ProtectedOrInternal:
                        // internal is complicated, it would probably have to care about [InternalsVisibleToAttribute] -> just include both Public | NonPublic
                    default:
						isNonPublic = true;
						isPublic = true;
						break;
				}
				var methodArguments = member is IMethodSymbol ? ((IMethodSymbol)member).Parameters.Select(p => p.Type).ToArray() : null;

				var bindingFlags = new List<string>();
				if (member.IsStatic) bindingFlags.Add("Static");
				else bindingFlags.Add("Instance");
				if (isPublic) bindingFlags.Add("Public");
				if (isNonPublic) bindingFlags.Add("NonPublic");

				yield return new ReflectionMemberCompletionData { MemberSymbol = member, BindingFlags = bindingFlags.ToArray(), ArgumentTypes = methodArguments };
			}
		}

		public IEnumerable<TResult> ListAllMembers<TResult>(ITypeSymbol type)
			where TResult : ISymbol
		{
            // iterate over basetypes, GetMembers returns only members defined directly in the type
			while (type != null)
			{
				foreach (var m in type.GetMembers())
					if (m is TResult) yield return (TResult)m;
				type = type.BaseType;
			}
		}

		public string GetFullName(IMethodSymbol symbol) =>
			(symbol.ContainingType == null ? "" : GetFullName(symbol.ContainingType) + ".") + symbol.MetadataName;

		public string GetFullName(INamespaceOrTypeSymbol symbol) =>
			((symbol.ContainingType == null && symbol.ContainingNamespace.IsGlobalNamespace) ? "" : GetFullName(symbol?.ContainingSymbol as INamespaceOrTypeSymbol) + ".") + symbol.MetadataName;

		LiteralExpressionSyntax GetCurrentLiteral(SyntaxTree tree, int position)
		{
            // find literal containning current position
			return tree.GetRoot().DescendantNodes(n => n.Span.End >= position && n.Span.Start <= position).OfType<LiteralExpressionSyntax>().FirstOrDefault(n => n.Span.Contains(position));
		}
	}
    public class ReflectionMemberCompletionData
    {
        public ISymbol MemberSymbol { get; set; }
        public string[] BindingFlags { get; set; }
        public ITypeSymbol[] ArgumentTypes { get; set; }
    }
}
