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
    public class CargoMenuHandler : IDisposable
    {
        int currentToolbarCmd; // The currently selected menu controller command
        System.IServiceProvider serviceProvider;
        List<MenuCommand> commands = new List<MenuCommand>();
        OleMenuCommandService mcs;

        string[] comboCommands = { "cargo", "rustc" };
        string currentCmd = "cargo";
        string currentCmdArgs = "";

        public void Init(OleMenuCommandService mcs, System.IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.mcs = mcs;
            // Add our command handlers for menu (commands must exist in the .vsct file)
            if (null != mcs)
            {
                // Init the rust project right click menu buttons
                InitCargoProjectButtons(mcs);

                // Init the cargo editor toolbar
                InitCargoToolbar(mcs);

                // Init the cargo editor toolbar command combo box
                // Can be used to start custom rustc or cargo commands
                InitCmdComboBox(mcs);
            }
        }

        private void InitCargoProjectButtons(OleMenuCommandService mcs)
        {
            // Cargo run
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoRun, new EventHandler(RunItemCallback)));

            // cargo build
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoBuild, new EventHandler(BuildItemCallback)));

            // cargo clean
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoClean, new EventHandler(CleanItemCallback)));

            // cargo update
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoUpdate, new EventHandler(UpdateItemCallback)));

            // cargo test
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoTest, new EventHandler(TestItemCallback)));

            // cargo bench
            commands.Add(AddMenuCommand(mcs, PkgCmdIDList.cmdidCargoBench, new EventHandler(BenchItemCallback)));
        }

        private void InitCargoToolbar(OleMenuCommandService mcs)
        {
            // cargo build
            OleMenuCommand mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoBuild, 
                new EventHandler(BuildItemCallback), 
                new EventHandler(OnToolbarItemQueryStatus));

            // The first item is, by default, checked. 
            mc.Checked = true;
            this.currentToolbarCmd = PkgCmdIDList.cmdidToolbarCargoBuild;
            commands.Add(mc);

            // cargo run
            mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoRun,
                new EventHandler(RunItemCallback),
                new EventHandler(OnToolbarItemQueryStatus));
            commands.Add(mc);

            // cargo clean
            mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoClean,
                new EventHandler(CleanItemCallback),
                new EventHandler(OnToolbarItemQueryStatus));
            commands.Add(mc);

            // cargo update
            mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoUpdate,
                new EventHandler(UpdateItemCallback),
                new EventHandler(OnToolbarItemQueryStatus));
            commands.Add(mc);

            // cargo test
            mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoTest,
                new EventHandler(TestItemCallback),
                new EventHandler(OnToolbarItemQueryStatus));
            commands.Add(mc);

            // cargo bench
            mc = AddOleMenuCommand(
                mcs, PkgCmdIDList.cmdidToolbarCargoBench,
                new EventHandler(BenchItemCallback),
                new EventHandler(OnToolbarItemQueryStatus));
            commands.Add(mc);
        }

        private void InitCmdComboBox(OleMenuCommandService mcs)
        {
            // --- Initialize the command "cargo/rust" combo box 
            CommandID comboCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidToolbarCmdCombo);
            OleMenuCommand comboCommand = new OleMenuCommand(new EventHandler(OnCmdCombo), comboCommandID);
            comboCommand.ParametersDescription = "$";
            mcs.AddCommand(comboCommand);
            commands.Add(comboCommand);

            // --- Initialize the “GetList” command for cmd combo
            CommandID comboGetListCommandId = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidToolbarCargoCustomComboGetList);
            MenuCommand comboGetListCommand = new OleMenuCommand(new EventHandler(OnCmdComboGetList), comboGetListCommandId);
            mcs.AddCommand(comboGetListCommand);
            commands.Add(comboCommand);

            // --- Initialize the MRUCombo cmd args combo
            comboCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidToolbarCmdArgsCombo);
            comboCommand = new OleMenuCommand(new EventHandler(OnCmdArgsCombo), comboCommandID);
            comboCommand.ParametersDescription = "$";
            mcs.AddCommand(comboCommand);
            commands.Add(comboCommand);
        }

        #region Command combo box handler

        private void OnCmdCombo(object sender, EventArgs e)
        {
            if (e == EventArgs.Empty)
            {
                throw (new ArgumentException());
            }
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null)
            {
                string newChoice = eventArgs.InValue as string;
                IntPtr vOut = eventArgs.OutValue;
                if (vOut != IntPtr.Zero && newChoice != null)
                {
                    // Both in out params illegal
                    throw (new ArgumentException());
                }
                else if (vOut != IntPtr.Zero)
                {
                    Marshal.GetNativeVariantForObject(this.currentCmd, vOut);
                }
                else if (newChoice != null)
                {
                    bool validInput = false;
                    int indexInput = -1;
                    for (indexInput = 0; indexInput < comboCommands.Length; indexInput++)
                    {
                        if (String.Compare(comboCommands[indexInput], newChoice, StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            validInput = true;
                            break;
                        }
                    }
                    if (validInput)
                    {
                        currentCmd = comboCommands[indexInput];
                        Project.ProjectUtil.PrintToBuild("Cmd: " + currentCmd);
                    }
                    else
                    {
                        throw (new ArgumentException());
                    }
                }
                else
                {
                    throw (new ArgumentException());
                }
            }
            else
            {
                throw (new ArgumentException());
            }
        }

        private void OnCmdComboGetList(object sender, EventArgs e)
        {
            var eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null)
            {
                // Note: works only for dynamic- and dropdown- combos
                IntPtr pOutValue = eventArgs.OutValue;
                if (pOutValue != IntPtr.Zero)
                {
                    Marshal.GetNativeVariantForObject(comboCommands, pOutValue);
                }
            }
        }

        private void OnCmdArgsCombo(object sender, EventArgs e)
        {
            // --- Some checks omitted
            OleMenuCmdEventArgs eventArgs = e as OleMenuCmdEventArgs;
            if (eventArgs != null)
            {
                object input = eventArgs.InValue;
                IntPtr vOut = eventArgs.OutValue;
                if (vOut != IntPtr.Zero && input != null)
                {
                    throw (new ArgumentException());
                }
                else if (vOut != IntPtr.Zero)
                {
                    // --- The IDE requests for the current value
                    Marshal.GetNativeVariantForObject(currentCmdArgs, vOut);
                }
                else if (input != null)
                {
                    // --- New zoom value was selected or typed in
                    currentCmdArgs = input.ToString();
                }
            }
        }

        #endregion

        #region Command handler

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

        private void CleanItemCallback(object sender, EventArgs e)
        {
            CargoUtil.CallCargoProcess(workingDir => Cargo.Update(workingDir), "clean");
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

        #endregion

        private OleMenuCommand AddOleMenuCommand(OleMenuCommandService mcs, int commandId, EventHandler cmdHandler, EventHandler itemQueryHandler)
        {
            CommandID cmdID = new CommandID(GuidList.guidVSPackage1CmdSet, commandId);
            OleMenuCommand mc = new OleMenuCommand(new EventHandler(cmdHandler), cmdID);
            mc.BeforeQueryStatus += new EventHandler(OnToolbarItemQueryStatus);
            mcs.AddCommand(mc);
            return mc;
        }

        private MenuCommand AddMenuCommand(OleMenuCommandService mcs, int commandId, EventHandler cmdHandler)
        {
            CommandID menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, commandId);
            MenuCommand menuItem = new MenuCommand(cmdHandler, menuCommandID);
            mcs.AddCommand(menuItem);
            return menuItem;
        }

        private void OnToolbarItemQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand mc = sender as OleMenuCommand;
            if (null != mc)
            {
                mc.Checked = (mc.CommandID.ID == this.currentToolbarCmd);
            }
        }

        public void Dispose()
        {
            if(commands.Count > 0)
            {
                foreach (var cmd in commands)
                    mcs.RemoveCommand(cmd);
            }
        }
    }
}
