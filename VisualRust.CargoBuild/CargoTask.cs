﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using Microsoft.Build.Framework;
using VisualRust.Shared;

namespace VisualRust.CargoBuild
{
    public class CargoTask : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string WorkingDirectory { get; set; }

        [Required]
        public string Configuration { get; set; }

        public string OutputType { get; set; }

        public override bool Execute()
        {
            try
            {
                return ExecuteInner();
            }
            catch (Exception ex)
            {
                this.Log.LogErrorFromException(ex, true);
                return false;
            }
        }

        private bool ExecuteInner()
        {
            bool result = true;
            string errorOutput = String.Empty;
            Log.LogMessage("Confing            = " + Configuration);
            Log.LogMessage("WorkingDirectory   = " + WorkingDirectory);
            Log.LogMessage("OutputType         = " + OutputType);
            Process process = null;
            switch(Configuration)
            {
                case "Build":
                    this.Log.LogCommandLine("Starting Cargo build at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Build(workingDir), "Build");
                    break;
                case "Run":
                    Log.LogCommandLine("Starting Cargo run at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Run(workingDir), "Run");
                    break;
                case "Update":
                    Log.LogCommandLine("Starting Cargo update at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Update(workingDir), "Update");
                    break;
                case "Test":
                    this.Log.LogCommandLine("Starting Cargo test at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Test(workingDir), "Test");
                    break;
                case "Bench":
                    this.Log.LogCommandLine("Starting Cargo bench at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Bench(workingDir), "Bench");
                    break;
                case "Release":
                    this.Log.LogCommandLine("Starting Cargo build --release at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Release(workingDir), "Release");
                    break;
                case "Clean":
                    this.Log.LogCommandLine("Starting Cargo clean at " + DateTime.Now.ToLongTimeString());
                    process = CallCargoProcess(workingDir => Cargo.Clean(workingDir), "Clean");
                    break;
                default: 
                    Log.LogCommandLine("Unknown configuration " + DateTime.Now.ToLongTimeString());
                    Log.LogError("Unknown configuration detected :(");
                    break;
                //case "CargoRelease":
                //    HandleProcess(Shared.Cargo.(WorkingDirectory));
                //    break;
            }
            if (process != null)
            {
                finishedEvent.WaitOne();
            }
            return result;
        }

        System.Threading.AutoResetEvent finishedEvent = new System.Threading.AutoResetEvent(false);

        public Process CallCargoProcess(Func<string, Process> cargoFunc, string taskName, bool printBuildOutput = true, Action<int> exitCodeCallBack = null)
        {
            exitCodeCallBack = exitCode =>
            {
                finishedEvent.Set();
            };

            if (printBuildOutput)
            {
                Log.LogCommandLine(String.Format("------------------------- Cargo {0} -------------------------\n", taskName));
                Log.LogCommandLine(String.Format("Starting {0} ...", taskName));
            }

            // Call the cargo function with current working directory as argument
            Tuple<Process, Exception> process = CommonUtil.TryCatch(() => cargoFunc(WorkingDirectory));

            if (process.Item2 != null)   // Exception
            {
                HandleProcessStartException(process, printBuildOutput);
            }
            else if (process.Item1 != null) // No exception, process is there
            {
                HandleProcess(taskName, process, printBuildOutput, exitCodeCallBack);
            }

            if (printBuildOutput)
                Log.LogCommandLine("-------------------------------------------------------------\n\n");
            return process.Item1;
        }

        void HandleProcessStartException(Tuple<Process, Exception> process, bool printBuildOutput = true)
        {
            // Something went wrong, print status
            if (printBuildOutput)
            {
                Exception exception = process.Item2;
                Log.LogErrorFromException(exception, true);
            }
        }

        void HandleProcess(
            string taskName, Tuple<Process, Exception> process, bool printBuildOutput = true, Action<int> exitCodeCallBack = null)
        {
            if (printBuildOutput)
                Log.LogCommandLine("Started at " + process.Item1.StartTime.ToLongTimeString());

            // Start redirecting the set Outputs of the process to the build pane
            // Wait for all to complete, then print finish message and check for exceptions
            WaitAllNotNull(RedirectOutputsIfNeeded(taskName, process.Item1, printBuildOutput))
            .ContinueWith(
                task =>
                {
                    if (task.Exception != null && printBuildOutput)
                        Log.LogErrorFromException(task.Exception, true);

                    // Be sure process is finshed, is needed!
                    // Outputs can be closed but process is still running,
                    // then ExitTime throws an exception
                    process.Item1.WaitForExit();

                    exitCodeCallBack.Call(process.Item1.ExitCode);

                    if (printBuildOutput)
                    {
                        Log.LogCommandLine("Finished at " + DateTime.Now.ToLongTimeString());
                    }
                });
        }

        Task[] RedirectOutputsIfNeeded(string taskName, Process process, bool printBuildOutput = true)
        {
            Task errorTask = null;
            Task outputTask = null;

            if (process.StartInfo.RedirectStandardError)
                errorTask = ProcessOutputStreamReader(process.StandardError, taskName, printBuildOutput);
            if (process.StartInfo.RedirectStandardOutput)
                outputTask = ProcessOutputStreamReader(process.StandardOutput, taskName, printBuildOutput);

            return new Task[] { errorTask, outputTask };
        }

        System.Threading.Tasks.Task ProcessOutputStreamReader(
            System.IO.StreamReader reader,
            string category = "BUILD",
            bool printBuildOutput = true,
            bool printRustcParsedMessages = false)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string output = reader.ReadToEnd();
                var rustMessages = RustcOutputProcessor.ParseOutput(output);

                if (printBuildOutput)
                    Log.LogCommandLine(output);

                foreach (var msg in rustMessages)
                {
                    Log.LogError("Rust", msg.ErrorCode, "", msg.File, msg.LineNumber, msg.ColumnNumber, msg.EndLineNumber, msg.EndColumnNumber, msg.Message);
                    //if (printRustcParsedMessages)
                    //    Log.LogCommandLine(msg.ToString());
                }
            });
        }

        Task WaitAllNotNull(params Task[] tasks)
        {
            return Task.WhenAll(tasks.Where(t => t != null).ToArray());
        }
    }
}
