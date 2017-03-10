using Microsoft.VisualStudio.Text;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleCompletionProviders
{
	public static class TheCodeGenerator
	{
		public static string Compile(string code, ITextSnapshot snapshot)
		{
			var roslynSourceText = snapshot.AsText();
			return $@"
#region generated code
// yaha code:
//{code.Replace("\n", "\n//")}
#endregion
			";
		}
	}
}
