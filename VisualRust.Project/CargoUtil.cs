using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudioTools.Project.Automation;
using Microsoft.Build.Execution;
using System.Collections.Generic;
using System.Linq;

using TasksTask = System.Threading.Tasks.Task;
using System.Threading.Tasks;

using VisualRust.Project;
using VisualRust.Shared;

namespace VisualRust
{
    public static class CargoUtil
    {

        public static void CallCargoProcess(Func<string, Process> cargoFunc, string taskName, bool printBuildOutput = true, Action<int> exitCodeCallBack = null)
        {
            if (printBuildOutput)
            {
                ProjectUtil.PrintToBuild(String.Format("------------------------- Cargo {0} -------------------------\n", taskName));
                ProjectUtil.PrintToBuild(taskName.ToUpper(), String.Format("Starting {0} ...", taskName));
            }

            // Get working dir via selected rust project node
            RustProjectNode rustProj = ProjectUtil.GetSelectedRustProjectNode();

            // Call the cargo function with current working directory as argument
            Tuple<Process, Exception> process = CommonUtil.TryCatch(() => cargoFunc(rustProj.BaseURI.AbsoluteUrl));

            if (process.Item2 != null)   // Exception
            {
                HandleProcessStartException(process, printBuildOutput);
            }
            else if (process.Item1 != null) // No exception, process is there
            {
                HandleProcess(taskName, rustProj, process, printBuildOutput, exitCodeCallBack);
            }

            if(printBuildOutput)
                ProjectUtil.PrintToBuild("-------------------------------------------------------------\n\n");
        }

        static void HandleProcessStartException(Tuple<Process, Exception> process, bool printBuildOutput = true)
        {
            // Something went wrong, print status
            if (printBuildOutput)
            {
                Exception exception = process.Item2;
                ProjectUtil.PrintToBuild("EXCEPTION", exception.Message);
                ProjectUtil.PrintToBuild("EXCEPTION", exception.StackTrace);
                ProjectUtil.ShowExceptionDialog(exception);
            }
        }

        static void HandleProcess(
            string taskName, RustProjectNode rustProj, Tuple<Process, Exception> process, bool printBuildOutput = true, Action<int> exitCodeCallBack = null)
        {
            if(printBuildOutput)
                ProjectUtil.PrintToBuild(taskName.ToUpper(), "Started at " + process.Item1.StartTime.ToLongTimeString());

            // Start redirecting the set Outputs of the process to the build pane
            // Wait for all to complete, then print finish message and check for exceptions
            WaitAllNotNull(RedirectOutputsIfNeeded(taskName, rustProj, process.Item1, printBuildOutput))
            .ContinueWith(
                task =>
                {
                    if (task.Exception != null && printBuildOutput)
                        ProjectUtil.ShowExceptionDialog(task.Exception);

                    // Be sure process is finshed, is needed!
                    // Outputs can be closed but process is still running,
                    // then ExitTime throws an exception
                    process.Item1.WaitForExit();

                    exitCodeCallBack.Call(process.Item1.ExitCode);
                    
                    if (printBuildOutput)
                    {
                        ProjectUtil.PrintToBuild(
                            taskName,
                            "Finished at " + DateTime.Now.ToLongTimeString());
                    }
                });
        }

        static TasksTask[] RedirectOutputsIfNeeded(string taskName, RustProjectNode rustProj, Process process, bool printBuildOutput = true)
        {
            TasksTask errorTask = null;
            TasksTask outputTask = null;

            if (process.StartInfo.RedirectStandardError)
                errorTask = ProcessOutputStreamReader(process.StandardError, rustProj, taskName, printBuildOutput);
            if (process.StartInfo.RedirectStandardOutput)
                outputTask = ProcessOutputStreamReader(process.StandardOutput, rustProj, taskName, printBuildOutput);

            return new TasksTask[] { errorTask, outputTask };
        }

        static System.Threading.Tasks.Task ProcessOutputStreamReader(
            System.IO.StreamReader reader,
            RustProjectNode rustProjectNode, 
            string category = "BUILD", 
            bool printBuildOutput = true, 
            bool printRustcParsedMessages = false)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = reader.ReadToEnd();
                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);

                if (printBuildOutput)
                    ProjectUtil.PrintToBuild(errorOutput);

                foreach (var msg in rustcErrors)
                {
                    TaskMessages.QueueRustcMessage("Rust", msg, rustProjectNode, refresh: false);
                    if (printRustcParsedMessages)
                        ProjectUtil.PrintToBuild(msg.ToString());
                }
                TaskMessages.Refresh();
            });
        }

        static TasksTask WaitAllNotNull(params TasksTask[] tasks)
        {
            return TasksTask.WhenAll(tasks.Where(t => t != null).ToArray());
        }

    }
}
