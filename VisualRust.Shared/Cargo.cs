using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.IO;
using System.Threading;



namespace VisualRust.Shared
{
    public static class Cargo
    {
        public static readonly string RUST_PATH = Environment.FindInstallPath("default");

        public static Process Run(string workingDir, bool createWindow = true)
        {
            Debug.WriteLine("Cargo.Run(" + workingDir + ")");
            return Start(
                workingDir,
                "run",
                createWindow,
                useShellExecute: true,
                redirectStandardError: false, 
                redirectStandardOutput:false);
        }

        public static Process Build(string workingDir, bool printBuildOutput = true)
        {
            Debug.WriteLine("Cargo.Build(" + workingDir + ")");
            return Start(workingDir, "build", false, false, true, true);
        }

        public static Process New(string workingDir)
        {
            Debug.WriteLine("Cargo.New(" + workingDir + ")");
            return Start(workingDir, "new");
        }

        public static Process Update(string workingDir)
        {
            Debug.WriteLine("Cargo.Update(" + workingDir + ")");
            return Start(workingDir, "update");
        }

        public static Process Test(string workingDir)
        {
            Debug.WriteLine("Cargo.Test(" + workingDir + ")");
            return Start(workingDir, "test");
        }

        public static Process Bench(string workingDir)
        {
            Debug.WriteLine("Cargo.Bench(" + workingDir + ")");
            return Start(workingDir, "bench");
        }

        public static Process Release(string workingDir)
        {
            Debug.WriteLine("Cargo.Release(" + workingDir + ")");
            return Start(workingDir, "build --release");
        }

        public static Process Clean(string workingDir)
        {
            Debug.WriteLine("Cargo.Clean(" + workingDir + ")");
            return Start(workingDir, "clean");

        }

        public static Process Start(
            string workingDir,
            string arguments,
            bool createWindow = false,
            bool useShellExecute = false,
            bool redirectStandardError = true,
            bool redirectStandardOutput = true)
        {
            Debug.WriteLine("Cargo.Start(" + workingDir + ", " + arguments + ")");
            return Start(CreateStartInfo(workingDir, arguments, createWindow, useShellExecute, redirectStandardError, redirectStandardOutput));
        }

        public static Process Start(ProcessStartInfo startInfo)
        {
            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();
            return process; 
        }

        private static ProcessStartInfo CreateStartInfo(
            string workingDir,
            string arguments,
            bool createWindow = false,
            bool useShellExecute = false,
            bool redirectStandardError = true,
            bool redirectStandardOutput = false)
        {
            Debug.WriteLine("Cargo.CreateStartInfo(" + workingDir + ", " + arguments + ")");
            return new ProcessStartInfo()
            {
                CreateNoWindow = !createWindow,
                FileName = Path.Combine(RUST_PATH, "cargo.exe"),
                UseShellExecute = useShellExecute,
                WorkingDirectory = workingDir,
                Arguments = arguments + " --verbose",
                RedirectStandardError = redirectStandardError,
                RedirectStandardOutput = redirectStandardOutput
            };
        }
    }

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
