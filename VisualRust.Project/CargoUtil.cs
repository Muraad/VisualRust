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

            // Get working dir from the selected rust project node
            RustProjectNode rustProj = GetSelectedRustProjectNode();

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
            WaitAllNotNull(RedirectOutputsIfNeeded(taskName, rustProj, process.Item1))
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
                errorTask = ProcessStandardError(process, rustProj, taskName, printBuildOutput);
            if (process.StartInfo.RedirectStandardOutput)
                outputTask = ProcessStandardOutput(process, rustProj, taskName, printBuildOutput);

            return new TasksTask[] { errorTask, outputTask };
        }

        static TasksTask WaitAllNotNull(params TasksTask[] tasks)
        {
            return TasksTask.WhenAll(tasks.Where(t => t != null).ToArray());
        }

        static RustProjectNode GetSelectedRustProjectNode()
        {
            var ivsSolution = (IVsSolution)Package.GetGlobalService(typeof(IVsSolution));
            var dte = (EnvDTE80.DTE2)Package.GetGlobalService(typeof(EnvDTE.DTE));


            //Get first project details
            EnvDTE.Project proj = dte.Solution.Projects.Item(1);
            var containingProj = proj.ProjectItems.ContainingProject;
            OAProject oaProj = containingProj as OAProject;
            RustProjectNode rustProjNode = oaProj.ProjectNode as RustProjectNode;
            return rustProjNode;
        }

        static System.Threading.Tasks.Task ProcessStandardOutput(
            Process process, RustProjectNode rustProjectNode, string category = "BUILD", bool printBuildOutput = true)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = process.StandardOutput.ReadToEnd();
                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);

                if (printBuildOutput)
                    ProjectUtil.PrintToBuild(errorOutput);

                // Clear Task(Error)Message list from last run
                //TaskMessages.Clear();
                int msgCount = 0;
                foreach (var msg in rustcErrors)
                {
                    TaskMessages.QueueRustcMessage("Rust", msg, rustProjectNode, refresh: false);
                    //if(printBuildOutput)
                    //    ProjectUtil.PrintToBuild(msg.ToString());
                    msgCount++;
                }

                TaskMessages.Refresh();
            });
        }

        static System.Threading.Tasks.Task ProcessStandardError(
            Process process, RustProjectNode rustProjectNode, string category = "BUILD", bool printBuildOutput = true)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = process.StandardError.ReadToEnd();
                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);

                if (printBuildOutput)
                    ProjectUtil.PrintToBuild(errorOutput);
                // Clear Task(Error)Message list from last run
                //TaskMessages.Clear();
                foreach (var msg in rustcErrors)
                {
                    TaskMessages.QueueRustcMessage("Rust", msg, rustProjectNode, refresh:false);
                    //if(printBuildOutput)
                    //    ProjectUtil.PrintToBuild(msg.ToString());
                }
                TaskMessages.Refresh();
            });
        }
    }
}
