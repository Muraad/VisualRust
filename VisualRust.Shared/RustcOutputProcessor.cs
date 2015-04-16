using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Build.Utilities;

namespace VisualRust.Shared
{
    public static class RustcOutputProcessor
    {
        private static readonly Regex defectRegex = new Regex(@"^([^\n:]+):(\d+):(\d+):\s+(\d+):(\d+)\s+(.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        // FIXME: This currently does not handle errors with descriptions, e.g. "unreachable pattern [E0001] (pass `--explain E0001` to see a detailed explanation)"
        private static readonly Regex errorCodeRegex = new Regex(@"\[([A-Z]\d\d\d\d)\]$", RegexOptions.CultureInvariant);

        public static void LogRustcMessage(RustcParsedMessage msg, TaskLoggingHelper log)
        {
            Debug.WriteLine(msg.ToString());
            if (msg.Type == RustcParsedMessageType.Warning)
            {
                log.LogWarning(null, msg.ErrorCode, null, msg.File, msg.LineNumber, msg.ColumnNumber, msg.EndLineNumber, msg.EndColumnNumber, msg.Message);
            }
            else if (msg.Type == RustcParsedMessageType.Note)
            {
                log.LogWarning(null, msg.ErrorCode, null, msg.File, msg.LineNumber, msg.ColumnNumber, msg.EndLineNumber, msg.EndColumnNumber, "note: " + msg.Message);
            }
            else
            {
                log.LogError(null, msg.ErrorCode, null, msg.File, msg.LineNumber, msg.ColumnNumber, msg.EndLineNumber, msg.EndColumnNumber, msg.Message);
            }
        }

        public static IEnumerable<RustcParsedMessage> ParseOutput(string output)
        {
            MatchCollection errorMatches = defectRegex.Matches(output);

            RustcParsedMessage previous = null;
            foreach (Match match in errorMatches)
            {
                Match errorMatch = errorCodeRegex.Match(match.Groups[6].Value);
                string errorCode = errorMatch.Success ? errorMatch.Groups[1].Value : null;
                int line = Int32.Parse(match.Groups[2].Value, System.Globalization.NumberStyles.None);
                int col = Int32.Parse(match.Groups[3].Value, System.Globalization.NumberStyles.None);
                int endLine = Int32.Parse(match.Groups[4].Value, System.Globalization.NumberStyles.None);
                int endCol = Int32.Parse(match.Groups[5].Value, System.Globalization.NumberStyles.None);

                if (match.Groups[6].Value.StartsWith("warning: "))
                {
                    string msg = match.Groups[6].Value.Substring(9, match.Groups[6].Value.Length - 9 - (errorCode != null ? 8 : 0));
                    if (previous != null) yield return previous;
                    previous = new RustcParsedMessage(RustcParsedMessageType.Warning, msg, errorCode, match.Groups[1].Value,
                        line, col, endLine, endCol);
                }
                else if (match.Groups[6].Value.StartsWith("note: "))
                {
                    string msg = match.Groups[6].Value.Substring(6, match.Groups[6].Value.Length - 6 - (errorCode != null ? 8 : 0));
                    RustcParsedMessage note = new RustcParsedMessage(RustcParsedMessageType.Note, msg, errorCode, match.Groups[1].Value,
                        line, col, endLine, endCol);

                    if (previous != null)
                    {
                        // try to merge notes with a previous message (warning or error where it belongs to), if the span is the same
                        if (previous.TryMergeWithFollowing(note))
                        {
                            continue; // skip setting new previous, because we successfully merged the new note into the previous message
                        }
                        else
                        {
                            yield return previous;
                        }
                    }
                    previous = note;
                }
                else
                {
                    bool startsWithError = match.Groups[6].Value.StartsWith("error: ");
                    string msg = match.Groups[6].Value.Substring((startsWithError ? 7 : 0), match.Groups[6].Value.Length - (startsWithError ? 7 : 0) - (errorCode != null ? 8 : 0));
                    if (previous != null) yield return previous;
                    previous = new RustcParsedMessage(RustcParsedMessageType.Error, msg, errorCode, match.Groups[1].Value,
                        line, col, endLine, endCol);
                }
            }

            if (previous != null) yield return previous;
        }

        /*private static RustcParsedMessage ParseWarning(string match, string errorCode, string file, int line, int col, int endLine, int endCol)
        {
            string msg = match.Substring(9, match.Length - 9 - (errorCode != null ? 8 : 0));
            return new RustcParsedMessage(RustcParsedMessageType.Warning, msg, errorCode, file,
                line, col, endLine, endCol);
        }*/
    }
}
