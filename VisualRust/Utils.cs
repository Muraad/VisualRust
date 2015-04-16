﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Operations;

namespace VisualRust
{
    using RustLexer;

    static class Utils
    {
        private static Dictionary<int, RustTokenTypes> _tt = new Dictionary<int, RustTokenTypes>()
        {
            { RustLexer.EQ, RustTokenTypes.OP },
            { RustLexer.LT, RustTokenTypes.OP },
            { RustLexer.LE, RustTokenTypes.OP },
            { RustLexer.EQEQ, RustTokenTypes.OP },
            { RustLexer.NE, RustTokenTypes.OP },
            { RustLexer.GE, RustTokenTypes.OP },
            { RustLexer.GT, RustTokenTypes.OP },
            { RustLexer.ANDAND, RustTokenTypes.OP },
            { RustLexer.OROR, RustTokenTypes.OP },
            { RustLexer.NOT, RustTokenTypes.OP },
            { RustLexer.TILDE, RustTokenTypes.OP },
            { RustLexer.PLUS, RustTokenTypes.OP },

            { RustLexer.MINUS, RustTokenTypes.OP },
            { RustLexer.STAR, RustTokenTypes.OP },
            { RustLexer.SLASH, RustTokenTypes.OP },
            { RustLexer.PERCENT, RustTokenTypes.OP },
            { RustLexer.CARET, RustTokenTypes.OP },
            { RustLexer.AND, RustTokenTypes.OP },
            { RustLexer.OR, RustTokenTypes.OP },
            { RustLexer.SHL, RustTokenTypes.OP },
            { RustLexer.SHR, RustTokenTypes.OP },
            { RustLexer.BINOP, RustTokenTypes.OP },

            { RustLexer.BINOPEQ, RustTokenTypes.OP },
            { RustLexer.AT, RustTokenTypes.STRUCTURAL },
            { RustLexer.DOT, RustTokenTypes.STRUCTURAL },
            { RustLexer.DOTDOT, RustTokenTypes.STRUCTURAL },
            { RustLexer.DOTDOTDOT, RustTokenTypes.STRUCTURAL },
            { RustLexer.COMMA, RustTokenTypes.STRUCTURAL },
            { RustLexer.SEMI, RustTokenTypes.STRUCTURAL },
            { RustLexer.COLON, RustTokenTypes.STRUCTURAL },

            { RustLexer.MOD_SEP, RustTokenTypes.STRUCTURAL },
            { RustLexer.RARROW, RustTokenTypes.STRUCTURAL },
            { RustLexer.FAT_ARROW, RustTokenTypes.STRUCTURAL },
            { RustLexer.LPAREN, RustTokenTypes.STRUCTURAL },
            { RustLexer.RPAREN, RustTokenTypes.STRUCTURAL },
            { RustLexer.LBRACKET, RustTokenTypes.STRUCTURAL },
            { RustLexer.RBRACKET, RustTokenTypes.STRUCTURAL },

            { RustLexer.LBRACE, RustTokenTypes.STRUCTURAL },
            { RustLexer.RBRACE, RustTokenTypes.STRUCTURAL },
            { RustLexer.POUND, RustTokenTypes.STRUCTURAL },
            { RustLexer.DOLLAR, RustTokenTypes.STRUCTURAL },
            { RustLexer.UNDERSCORE, RustTokenTypes.STRUCTURAL },
            { RustLexer.LIT_CHAR, RustTokenTypes.CHAR },

            { RustLexer.LIT_INTEGER, RustTokenTypes.NUMBER },
            { RustLexer.LIT_FLOAT, RustTokenTypes.NUMBER },
            { RustLexer.LIT_STR, RustTokenTypes.STRING },
            { RustLexer.LIT_STR_RAW, RustTokenTypes.STRING },
            { RustLexer.LIT_BINARY, RustTokenTypes.STRING },

            { RustLexer.LIT_BINARY_RAW, RustTokenTypes.STRING },
            { RustLexer.IDENT, RustTokenTypes.IDENT },
            { RustLexer.LIFETIME, RustTokenTypes.LIFETIME },
            { RustLexer.WHITESPACE, RustTokenTypes.WHITESPACE },
            { RustLexer.DOC_COMMENT, RustTokenTypes.DOC_COMMENT },
            { RustLexer.COMMENT, RustTokenTypes.COMMENT },
            { RustLexer.BLOCK_COMMENT, RustTokenTypes.COMMENT },
            { RustLexer.DOC_BLOCK_COMMENT, RustTokenTypes.DOC_COMMENT },
        };

        // These keywords are from rustc /src/libsyntax/parse/token.rs, module keywords
        private static readonly HashSet<string> _kws = new HashSet<string> {
                "as",
                "box",
                "break",
                "const",
                "continue",
                "crate",
                "else",
                "enum",
                "extern",
                "false",
                "fn",
                "for",
                "if",
                "impl",
                "in",
                "let",
                "loop",
                "match",
                "mod",
                "move",
                "mut",
                "proc",
                "pub",
                "ref",
                "return",
                "self",
                "static",
                "struct",
                "super",
                "true",
                "trait",
                "type",
                "unsafe",
                "use",
                "virtual",
                "where",
                "while"
            };

        public static RustTokenTypes LexerTokenToRustToken(string text, int tok)
        {
            RustTokenTypes ty = _tt[tok];
            if (ty == RustTokenTypes.IDENT)
            {
                if (_kws.Contains(text))
                {
                    ty = RustTokenTypes.KEYWORD;
                }
                else
                {
                    ty = RustTokenTypes.IDENT;
                }
            }
            return ty;
        }

        public static IEnumerable<Antlr4.Runtime.IToken> LexString(string text)
        {
            var lexer = new RustLexer(text);
            while (true)
            {
                var tok = lexer.NextToken();
                if (tok.Type == RustLexer.Eof)
                {
                    yield break;
                }
                else
                {
                    yield return tok;
                }
            }
        }

        public static IEnumerable<string> Keywords
        {
            get { return _kws; }
        }
        
        [Conditional("DEBUG")]
        internal static void DebugPrintToOutput(string s, params object[] args)
        {        
            PrintToOutput("[DEBUG] "+s, args);
        }

        internal static void PrintToOutput(string s, params object[] args)
        {
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = VSConstants.GUID_OutWindowGeneralPane;
            IVsOutputWindowPane pane;
            ErrorHandler.ThrowOnFailure(outWindow.CreatePane(paneGuid, "General", 1, 0));            
            outWindow.GetPane(ref paneGuid, out pane);
            pane.OutputString(string.Format("[VisualRust]: " + s, args) + "\n");
            pane.Activate();
        }

        internal static void ShowMessageBox(string header, string msg)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       header,
                       msg,
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        internal static void PrintToDebug(string category, string text)
        {
            text = "[" + category + "]    ";
            OutputLine(VSConstants.OutputWindowPaneGuid.DebugPane_guid, text);
        }

        internal static void PrintToBuild(string category, string text)
        {
            text = "[" + category + "]    " + text;
            OutputLine(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, text);
        }

        internal static void PrintToGeneral(string category, string text)
        {
            text = "[" + category + "]    ";
            OutputLine(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, text);
        }
        

        internal static void PrintToDebug(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.DebugPane_guid, text);
        }

        internal static void PrintToBuild(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, text);
        }

        internal static void PrintToGeneral(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, text);
        }

        internal static void OutputLine(Guid guidPane, string text)
        {
            OutputString(guidPane, text + Environment.NewLine);
        }

        internal static void OutputString(Guid guidPane, string text)
        {
            IVsOutputWindowPane outputWindowPane = GetOutputWindowPane(guidPane);

            // Output the text
            if (outputWindowPane != null)
            {
                outputWindowPane.Activate();
                outputWindowPane.OutputString(text);
            }
        }

        public static IVsOutputWindowPane GetDebugWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.DebugPane_guid);
        }

        public static IVsOutputWindowPane GetGeneralWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid);
        }

        public static IVsOutputWindowPane GetBuildWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);
        }

        public static IVsOutputWindowPane GetOutputWindowPane(Guid guidPane)
        {
            const int VISIBLE = 1;
            const int DO_NOT_CLEAR_WITH_SOLUTION = 0;

            IVsOutputWindow outputWindow;
            IVsOutputWindowPane outputWindowPane = null;
            int hr;

            // Get the output window
            outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            // The General pane is not created by default. We must force its creation
            if (guidPane == Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid)
            {
                hr = outputWindow.CreatePane(guidPane, "General", VISIBLE, DO_NOT_CLEAR_WITH_SOLUTION);
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            }

            // Get the pane
            hr = outputWindow.GetPane(guidPane, out outputWindowPane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return outputWindowPane;
        }
    }

    public class TemporaryFile : IDisposable
    {
        public string Path { get; private set; }

        /// <summary>
        ///  Creates a new, empty temporary file
        /// </summary>
        public TemporaryFile()
        {
            Path = System.IO.Path.GetTempFileName();            
        }

        /// <summary>
        ///  Creates a new temporary file with the argument string as UTF-8 content.
        /// </summary>
        public TemporaryFile(string content)
            : this()
        {
            using (var sw = new StreamWriter(Path, false, new UTF8Encoding(false)))
            {
                sw.Write(content);
            }
        }

        public void Dispose()
        {
            if (!File.Exists(Path))                      
                File.Delete(Path);            
        }
    }

}
