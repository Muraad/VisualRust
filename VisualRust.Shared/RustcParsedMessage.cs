using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VisualRust.Shared
{

    public enum RustcParsedMessageType
    {
        Error,
        Warning,
        Note,
        Help
    }

    public class RustcParsedMessage
    {
        public RustcParsedMessageType Type;
        public string Message;
        public string ErrorCode;
        public string File;
        public int LineNumber;
        public int ColumnNumber;
        public int EndLineNumber;
        public int EndColumnNumber;
        public bool CanExplain; // TODO: currently we don't do anything with this

        public RustcParsedMessage(RustcParsedMessageType type, string message, string errorCode, string file,
            int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber)
        {
            Type = type;
            Message = message;
            ErrorCode = errorCode;
            File = file;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
            CanExplain = false;
        }

        public bool TryMergeWithFollowing(RustcParsedMessage other)
        {
            if ((other.Type == RustcParsedMessageType.Note || other.Type == RustcParsedMessageType.Help)
                && other.File == this.File && other.LineNumber == this.LineNumber && other.ColumnNumber == this.ColumnNumber &&
                other.EndLineNumber == this.EndLineNumber && other.EndColumnNumber == this.EndColumnNumber)
            {
                var prefix = other.Type == RustcParsedMessageType.Note ? "\nnote: " : "\nhelp: ";
                this.Message += prefix + other.Message;
                return true;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            StringBuilder strBuilder = new StringBuilder();

            strBuilder.AppendLine("RustcParsedMessage");
            strBuilder.AppendLine("  File: " + this.File);
            strBuilder.AppendLine("  Type: " + this.Type + " ErrorCode: " + this.ErrorCode);
            strBuilder.AppendLine("  LineNumber: " + this.LineNumber + "ColumnNumber: " + this.ColumnNumber);
            strBuilder.AppendLine("  EndLineNumber: " + this.EndLineNumber + " EndColumnNumber: " + this.EndColumnNumber);
            strBuilder.AppendLine("  Message: " + this.Message);
            strBuilder.AppendLine();
            return strBuilder.ToString();
        }
    }
}
