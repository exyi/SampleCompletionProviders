using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleCompletionProviders
{
	public class TextBufferHandler
	{
		public const string GeneratedCodeRegionName = "generated code";
		private readonly ITextBuffer buffer;
		private readonly Workspace workspace;

		private readonly List<CodeBlock> blocks = new List<CodeBlock>();
		private IEnumerable<ITrackingSpan> trackingspans
		{
			get
			{
				foreach (var b in blocks)
				{
					if (b.CodeSpan != null) yield return b.CodeSpan;
					if (b.GeneratedCodeSpan != null) yield return b.GeneratedCodeSpan;
				}
			}
		}

		public TextBufferHandler(ITextBuffer buffer, Workspace workspace)
		{
			this.buffer = buffer;
			this.workspace = workspace;
			this.RefreshBlocks(buffer.CurrentSnapshot);
			RegenerateDirtyBlocks(buffer.CurrentSnapshot);
			buffer.Changed += Buffer_Changed;
		}
		private bool isRegenerating = false;
		private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
		{
			if (isRegenerating) return;

            bool containsCommenter(ITextSnapshot snapshot, Span span) =>
                snapshot.GetText(Span.FromBounds(Math.Max(0, span.Start - 3), Math.Min(span.End, snapshot.Length))).Contains("//`");
            var isCritical = trackingspans.Select(s => s.GetSpan(e.Before))
                .SelectMany(s => new[] { s.Start, s.End })
                .Any(p => e.Changes.Any(v => v.OldSpan.Contains(p.Position))) ||
                e.Changes.Any(c => containsCommenter(e.After, c.NewSpan) || containsCommenter(e.Before, c.OldSpan));
			if (isCritical)
			{
				RefreshBlocks(e.After);
			}
			else
			{
				foreach (var change in e.Changes)
				{
					foreach (CodeBlock block in blocks)
					{
						if (block.CodeSpan.GetSpan(e.After).Contains(change.NewPosition - 1))
						{
							block.IsDirty = true;
						}
						if (block.GeneratedCodeSpan?.GetSpan(e.After).Contains(change.NewPosition) == true)
						{
							// TODO: don't edit generated code
						}
					}
				}
			}
			RegenerateDirtyBlocks(e.After);
		}

		public static TextBufferHandler FromBuffer(ITextBuffer textBuffer)
		{
			return textBuffer.Properties.GetOrCreateSingletonProperty<TextBufferHandler>(() =>
			{
				Workspace workspace;
				if (!Workspace.TryGetWorkspace(textBuffer.CurrentSnapshot.AsText().Container, out workspace)) return null;
				else return new TextBufferHandler(textBuffer, workspace);
			});
		}

		private void RefreshBlocks(ITextSnapshot snapshot)
		{
			blocks.Clear();
			var lines = snapshot.Lines.ToArray();
			int blockFromLine = -1;
			for (int i = 0; i < lines.Length; i++)
			{
				if (string.IsNullOrWhiteSpace(lines[i].GetText())) continue;
				if (lines[i].GetText().TrimStart().StartsWith("//`", StringComparison.Ordinal))
				{
					if (blockFromLine < 0) blockFromLine = i;
				}
				else
				{
					if (blockFromLine >= 0)
					{
						var codeLineEnd = i;
						if (lines[i].GetText().Trim().Equals("#region " + GeneratedCodeRegionName))
						{
							while (++i < lines.Length && !lines[i].GetText().Trim().Replace(" ", "").Equals("#endregion//" + GeneratedCodeRegionName.Replace(" ", ""))) ;
                            if (i < lines.Length) i++;
						}
						AddBlock(lines.Skip(blockFromLine).Take(codeLineEnd - blockFromLine).ToArray(), lines.Skip(codeLineEnd).Take(i - codeLineEnd).ToArray());
						blockFromLine = -1;
					}
				}
			}
		}

		private void AddBlock(ITextSnapshotLine[] lines, ITextSnapshotLine[] gencodeLines)
		{
			var span = new SnapshotSpan(lines.First().Start, lines.Last().End);
			var gencodeSpan = gencodeLines.Length > 0 ? new SnapshotSpan(gencodeLines.First().Start, gencodeLines.Last().End) : (SnapshotSpan?)null;
			blocks.Add(new CodeBlock
			{
				CodeSpan = span.Snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive),
				Code = GetCodeFromLines(lines.Select(s => s.Extent)),
				GeneratedCode = gencodeSpan?.GetText(),
				GeneratedCodeSpan = gencodeSpan?.Snapshot?.CreateTrackingSpan(gencodeSpan.Value, SpanTrackingMode.EdgePositive),
				IsDirty = true
			});
		}

		private string GetCodeFromLines(IEnumerable<SnapshotSpan> lines) =>
			string.Join("\n", lines.Select(l => TrimStartLine(l.GetText().Trim())));


		string TrimStartLine(string line)
		{
			if (string.IsNullOrWhiteSpace(line)) return line;
			if (!line.StartsWith("//`", StringComparison.Ordinal)) throw new Exception();
			return line.Remove(0, 3);
		}

		public void RegenerateDirtyBlocks(ITextSnapshot snapshot)
		{
			try
			{
				isRegenerating = true;
				foreach (var block in blocks)
				{
					if (block.IsDirty)
						RegenerateBlock(block, snapshot);
				}
			}
			finally
			{
				isRegenerating = false;
			}
		}

        void RegenerateBlock(CodeBlock block, ITextSnapshot snapshot)
        {
            var codeSpan = block.CodeSpan.GetSpan(snapshot);
			block.Code = GetCodeFromLines(snapshot.Lines.Select(l => l.Extent.Intersection(codeSpan)).Where(s => s != null).Select(s => s.Value));
			var generatedCode = TheCodeGenerator.Compile(block.Code, snapshot);
            if (generatedCode != null)
            {
                var oldSnapshot = buffer.CurrentSnapshot;
                ITextSnapshot newSnapshot =
                    block.GeneratedCodeSpan != null ?
                    buffer.Replace(block.GeneratedCodeSpan.GetSpan(buffer.CurrentSnapshot), generatedCode) :
                    newSnapshot = buffer.Insert(codeSpan.End, generatedCode);
                block.GeneratedCode = generatedCode;
                block.GeneratedCodeSpan = newSnapshot.CreateTrackingSpan(block.GeneratedCodeSpan?.GetSpan(oldSnapshot).Start ?? codeSpan.End, generatedCode.Length, SpanTrackingMode.EdgeExclusive);
            }

            block.IsDirty = false;
		}

		class CodeBlock
		{
			public ITrackingSpan CodeSpan { get; set; }
			public ITrackingSpan GeneratedCodeSpan { get; set; }
			public string GeneratedCode { get; set; }
			public string Code { get; set; }
			public bool IsDirty { get; set; }
		}
	}
}
