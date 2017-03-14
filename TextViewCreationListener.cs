using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TextManager.Interop;

namespace SampleCompletionProviders
{
	[Export(typeof(IVsTextViewCreationListener))]
	[Name(nameof(TextViewCreationListener))]
	[ContentType("any")]
	[TextViewRole(PredefinedTextViewRoles.Interactive)]
	public class TextViewCreationListener : IVsTextViewCreationListener
	{
		[Import]
		private IVsEditorAdaptersFactoryService adapterService { get; set; }


		public void TextViewCreated(IWpfTextView textView)
		{
			var bh = TextBufferHandler.FromBuffer(textView.TextBuffer);
		}

		public void VsTextViewCreated(IVsTextView textViewAdapter)
		{
			var textView = adapterService.GetWpfTextView(textViewAdapter);
			TextViewCreated(textView);
		}
	}
}
