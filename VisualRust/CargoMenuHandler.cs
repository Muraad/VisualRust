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
            CargoUtil.CallCargoProcess(workingDir => Cargo.Run(workingDir), "run");
        }

        private void BuildItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Build(workingDir), "build");
        }

        private void UpdateItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Update(workingDir), "update");
        }

        private void TestItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Test(workingDir), "test");
        }

        private void BenchItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Bench(workingDir), "bench");
        }

        private void ReleaseItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Release(workingDir), "release");
        }
    }
}
