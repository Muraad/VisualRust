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
using Microsoft.VisualStudioTools;

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
