using Microsoft.VisualStudio.Text;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace SampleCompletionProviders
{
	public static class TheCodeGenerator
	{
		public static string Compile(string code, ITextSnapshot snapshot)
		{
			var roslynSourceText = snapshot.AsText();
            var match = Regex.Match(code.Trim(), @"^(?<name>\w+)\((?<args>.*)\)$", RegexOptions.Multiline);

            if (match.Success)
            {
                var name = match.Groups["name"].Value;
                var argsString = match.Groups["args"].Value;
                var aSplit = argsString.Split(',').Select(a => a.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries));
                var args = aSplit.Select(a => (type: a.FirstOrDefault(), name: a.LastOrDefault())).Where(a => a.name != null);
                var result = @"public class {name}
{
    public {name}({ctorArgs})
    {
        {ctorBody}
    }
    {props}
}";
                result = result.Replace("{name}", name);
                result = result.Replace("{ctorArgs}", string.Join(", ",
                    args.Select(a => $"{a.type} {char.ToLower(a.name[0])}{a.name.Substring(1)}")));
                result = result.Replace("{props}", string.Join("\n    ",
                    args.Select(a => $"public {a.type} {char.ToUpper(a.name[0])}{a.name.Substring(1)} {{ get; }}")));
                result = result.Replace("{ctorBody}", string.Join("\n        ",
                    args.Select(a => $"this.{char.ToUpper(a.name[0])}{a.name.Substring(1)} = {char.ToLower(a.name[0])}{a.name.Substring(1)};")));

                return $@"
#region generated code
{result}
#endregion // generated code";

            }
            return null;
		}
	}
}
