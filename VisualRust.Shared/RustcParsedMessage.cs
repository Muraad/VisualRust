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
        Note
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
        }

        public bool TryMergeWithFollowing(RustcParsedMessage other)
        {
            if (other.Type == RustcParsedMessageType.Note && other.File == this.File &&
                other.LineNumber == this.LineNumber && other.ColumnNumber == this.ColumnNumber &&
                other.EndLineNumber == this.EndLineNumber && other.EndColumnNumber == this.EndColumnNumber)
            {
                this.Message += "\nnote: " + other.Message;
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
