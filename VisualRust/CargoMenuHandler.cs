using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.ComponentModel.Composition;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
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
    public class CargoMenuHandler
    {
        public void Init(OleMenuCommandService mcs)
        {
            // Add our command handlers for menu (commands must exist in the .vsct file)
            if (null != mcs)
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoRun);
                MenuCommand menuItem = new MenuCommand(RunItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoBuild);
                menuItem = new MenuCommand(BuildItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoUpdate);
                menuItem = new MenuCommand(UpdateItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoTest);
                menuItem = new MenuCommand(TestItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoBench);
                menuItem = new MenuCommand(BenchItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void RunItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Run(workingDir), "run");
        }

        private void BuildItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Build(workingDir), "build");
        }

        private void UpdateItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Update(workingDir), "update");
        }

        private void TestItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Test(workingDir), "test");
        }

        private void BenchItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Bench(workingDir), "bench");
        }

        private void ReleaseItemCallback(object sender, EventArgs e)
        {
            GenericItemCallback(workingDir => Cargo.Release(workingDir), "release");
        }

        private void GenericItemCallback(Func<string, Process> cargoFunc, string taskName)
        {
            Utils.PrintToBuild(String.Format("------------------------- Cargo {0} -------------------------\n", taskName));

            Utils.PrintToBuild(taskName.ToUpper(), String.Format("Starting {0} ...", taskName));

            // Clear Error list from last run
            TaskMessages.Clear();

            // Get working dir from the selected rust project node
            RustProjectNode rustProj = GetSelectedRustProjectNode();


            // Call the cargo function with current working directory as argument
            Tuple<Process, Exception> process = CommonUtil.TryCatch(() => cargoFunc(rustProj.BaseURI.AbsoluteUrl));

            if(process.Item2 != null)   // Exception
            {
                Exception exception = process.Item2;
                Utils.ShowMessageBox("Exception", exception.Message);
                Utils.PrintToBuild("EXCEPTION", exception.Message);
                Utils.PrintToBuild("EXCEPTION", exception.StackTrace);
            }
            else if (process.Item1 != null) // No exception, process is there
            {
                Utils.PrintToBuild(taskName.ToUpper(), "Started at " + process.Item1.StartTime.ToLongTimeString());

                WaitAllNotNull(RedirectOutputsIfNeeded(taskName, rustProj, process))
                .ContinueWith(
                    _ => Utils.PrintToBuild(
                            taskName, 
                            "Finished at " + 
                            process.Item1 == null 
                            ? DateTime.Now.ToLongTimeString() 
                            : process.Item1.ExitTime.ToLongTimeString()));

            }

            Utils.PrintToBuild("-------------------------------------------------------------\n\n");
        }

        private TasksTask[] RedirectOutputsIfNeeded(string taskName, RustProjectNode rustProj, Tuple<Process, Exception> process)
        {
            TasksTask errorTask = null;
            TasksTask outputTask = null;

            if (process.Item1.StartInfo.RedirectStandardError)
                errorTask = ProcessStandardError(process.Item1, rustProj, taskName);
            if (process.Item1.StartInfo.RedirectStandardOutput)
                outputTask = ProcessStandardOutput(process.Item1, rustProj, taskName);

            return new TasksTask[] { errorTask, outputTask };
        }

        private TasksTask WaitAllNotNull(params TasksTask[] tasks)
        {
            return TasksTask.WhenAll(tasks.Where(t => t != null).ToArray());
        }

        private RustProjectNode GetSelectedRustProjectNode()
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

        private System.Threading.Tasks.Task ProcessStandardOutput(Process process, RustProjectNode rustProjectNode, string category = "BUILD")
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = process.StandardOutput.ReadToEnd();
                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);

                foreach (var msg in rustcErrors)
                {
                    TaskMessages.QueueRustcMessage("Rust", msg, rustProjectNode);
                    Utils.PrintToBuild(msg.ToString());
                }
            });
        }

        private System.Threading.Tasks.Task ProcessStandardError(Process process, RustProjectNode rustProjectNode, string category = "BUILD")
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                string errorOutput = process.StandardError.ReadToEnd();
                var rustcErrors = RustcOutputProcessor.ParseOutput(errorOutput);

                foreach (var msg in rustcErrors)
                {
                    TaskMessages.QueueRustcMessage("Rust",msg, rustProjectNode);
                    Utils.PrintToBuild(msg.ToString());
                }
            });
        }
    }
}
