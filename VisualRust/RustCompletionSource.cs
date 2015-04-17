using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace VisualRust
{
    [Export(typeof(ICompletionSourceProvider))]
    [ContentType("rust")]
    [Name("rustCompletion")]
    internal class RustCompletionSourceProvider : ICompletionSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        [Import]
        internal IGlyphService GlyphService { get; set; }

        public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return new RustCompletionSource(textBuffer, GlyphService);
        }
    }


    internal class RustCompletionSource : ICompletionSource
    {
        // These are returned by racer in the fifths column of a complete call.
        private enum CompletableLanguageElement
        {
            Struct,
            Module,
            Function,
            Crate,
            Let,
            StructField,
            Impl,
            Enum,
            EnumVariant,
            Type,
            FnArg,
            Trait,
            Static,
        }

        private readonly IGlyphService glyphService;
        private readonly ITextBuffer buffer;
        private bool disposed;

        private readonly IEnumerable<Completion> keywordCompletions =
            Utils.Keywords.Select(k => new Completion(k, k + " ", "", null, null));

        public RustCompletionSource(ITextBuffer buffer, IGlyphService glyphService)
        {
            this.buffer = buffer;
            this.glyphService = glyphService;
        }

        private ImageSource GetCompletionIcon(CompletableLanguageElement elType)
        {
            switch (elType)
            {
                case CompletableLanguageElement.Struct:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupStruct, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Module:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphAssembly, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Function:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphExtensionMethod,
                        StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Crate:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphAssembly, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Let:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupConstant,
                        StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.StructField:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupField, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Impl:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupTypedef, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Enum:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupEnum, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.EnumVariant:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupEnumMember,
                        StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Type:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupTypedef, StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Trait:
                    return glyphService.GetGlyph(StandardGlyphGroup.GlyphGroupInterface,
                        StandardGlyphItem.GlyphItemPublic);
                case CompletableLanguageElement.Static:
                    return null;
                case CompletableLanguageElement.FnArg:
                    return null;
                default:
                    ProjectUtil.DebugPrintToOutput("Unhandled language element found in racer autocomplete response: {0}", elType);
                    return null;
            }
        }

        /// <summary>
        ///   Fetches auto complete suggestions and appends to the completion sets of the current completion session.
        /// </summary>
        /// <param name="session">The active completion session, initiated from the completion command handler.</param>
        /// <param name="completionSets">A list of completion sets that may be augmented by this source.</param>
        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);

            ITextSnapshot snapshot = buffer.CurrentSnapshot;
            SnapshotPoint? sp = session.GetTriggerPoint(snapshot);
            if (!sp.HasValue)
                return;

            var triggerPoint = sp.Value;

            var line = triggerPoint.GetContainingLine();
            int col = triggerPoint.Position - line.Start.Position;

            if (line.GetText() == "" || col == 0 || char.IsWhiteSpace(line.GetText()[col - 1]))
            {
                // On empty rows or without a prefix, return only completions for rust keywords.                                
                var location = snapshot.CreateTrackingSpan(col + line.Start.Position, 0, SpanTrackingMode.EdgeInclusive);
                completionSets.Add(new RustCompletionSet("All", "All", location, keywordCompletions, null));
                return;
            }

            // Get token under cursor.
            var tokens = Utils.LexString(line.GetText());
            var activeToken = col == line.Length
                ? tokens.Last()
                : tokens.FirstOrDefault(t => col >= t.StartIndex && col <= t.StopIndex);
            if (activeToken == null)
                return;

            RustTokenTypes tokenType = Utils.LexerTokenToRustToken(activeToken.Text, activeToken.Type);

            // Establish the extents of the current token left of the cursor.
            var extent = new TextExtent(
                new SnapshotSpan(
                    new SnapshotPoint(snapshot, activeToken.StartIndex + line.Start.Position),
                    triggerPoint),
                tokenType != RustTokenTypes.WHITESPACE);

            var span = snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);

            // Fetch racer completions & return in a completion set.
            var completions = GetCompletions(tokenType, activeToken.Text, RunRacer(snapshot, triggerPoint)).ToList();
            completions.AddRange(keywordCompletions);

            completionSets.Add(new RustCompletionSet("All", "All", span, completions, null));
        }

        private static int GetColumn(SnapshotPoint point)
        {
            var line = point.GetContainingLine();
            int col = point.Position - line.Start.Position;
            return col;
        }

        private static string RunRacer(ITextSnapshot snapshot, SnapshotPoint point)
        {
            using (var tmpFile = new TemporaryFile(snapshot.GetText()))
            {
                // Build racer command line: "racer.exe complete lineNo columnNo rustfile
                int lineNumber = point.GetContainingLine().LineNumber;
                int charNumber = GetColumn(point);
                string args = string.Format("complete {0} {1} {2}", lineNumber + 1, charNumber, tmpFile.Path);
                return Racer.AutoCompleter.Run(args);
            }
        }


        // Parses racer output into completions.
        private IEnumerable<Completion> GetCompletions(RustTokenTypes tokenType, string tokenText, string racerResponse)
        {
            // Completions from racer.
            var lines = racerResponse.Split(new[] { '\n' }, StringSplitOptions.None).Where(l => l.StartsWith("MATCH"));
            foreach (var line in lines)
            {
                var tokens = line.Substring(6).Split(',');
                string text = tokens[0];
                string langElemText = tokens[4];
                string description = tokens[5];
                CompletableLanguageElement elType;

                if (!Enum.TryParse(langElemText, out elType))
                {
                    ProjectUtil.DebugPrintToOutput("Failed to parse language element found in racer autocomplete response: {0}", langElemText);
                    continue;
                }

                string insertionText = tokenType == RustTokenTypes.STRUCTURAL ? tokenText + text : text;
                var icon = GetCompletionIcon(elType);

                yield return new Completion(text, insertionText, description, icon, null);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                GC.SuppressFinalize(this);
                disposed = true;
            }
        }


    }

    internal class RustCompletionSet : CompletionSet
    {
        public RustCompletionSet(string moniker, string name, ITrackingSpan applicableTo,
            IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders)
            : base(moniker, name, applicableTo, completions, completionBuilders)
        {
        }

        public override void Filter()
        {
            Filter(CompletionMatchType.MatchInsertionText, false);
        }
    }

}