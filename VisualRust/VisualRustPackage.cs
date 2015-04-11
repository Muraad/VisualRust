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
using VisualRust.Project;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudioTools.Project.Automation;

namespace VisualRust
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideLanguageService(typeof(RustLanguage), "Rust", 100, 
        CodeSense = true, 
        DefaultToInsertSpaces = true,
        EnableCommenting = true,
        MatchBraces = true,
        MatchBracesAtCaret = true,
        ShowCompletion = false,
        ShowMatchingBrace = true,
        QuickInfo = false, 
        AutoOutlining = true,
        ShowSmartIndent = true, 
        EnableLineNumbers = true, 
        EnableFormatSelection = true,
        SupportCopyPasteOfHTML = false
    )]
    [ProvideProjectFactory(
        typeof(RustProjectFactory),
        "Rust",
        "Rust Project Files (*.rsproj);*.rsproj",
        "rsproj",
        "rsproj",
        ".\\NullPath",
        LanguageVsTemplate="Rust")]
    [ProvideLanguageExtension(typeof(RustLanguage), ".rs")]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidVisualRustPkgString)]
    [ProvideObject(typeof(Project.Forms.ApplicationPropertyPage))]
    [ProvideObject(typeof(Project.Forms.BuildPropertyPage))]
    public class VisualRustPackage : CommonProjectPackage
    {
        private RunningDocTableEventsListener docEventsListener;

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public VisualRustPackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
            Microsoft.VisualStudioTools.UIThread.InitializeAndAlwaysInvokeToCurrentThread();
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members
        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        ///
        protected override void Initialize()
        {
            base.Initialize();
            docEventsListener = new RunningDocTableEventsListener((IVsRunningDocumentTable)GetService(typeof(SVsRunningDocumentTable)));
            Racer.AutoCompleter.Init();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoRun);
                MenuCommand menuItem = new MenuCommand(MenuItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidVSPackage1CmdSet, (int)PkgCmdIDList.cmdidCargoBuild);
                menuItem = new MenuCommand(MenuItemCallback, menuCommandID);
                mcs.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        public void MenuItemCallback(object sender, EventArgs e)
        {
            try
            {
                OutputString(Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.DebugPane_guid, "Hello World in Debug pane");
                OutputString(Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, "Hello World in Build pane");
                OutputString(Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "Hello World in General pane");
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.ToString());
            }

            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       "VSPackage1",
                       string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.ToString()),
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        private void OutputString(Guid guidPane, string text)
        {
            const int VISIBLE = 1;
            const int DO_NOT_CLEAR_WITH_SOLUTION = 0;

            IVsOutputWindow outputWindow;
            IVsOutputWindowPane outputWindowPane = null;
            int hr;

            // Get the output window
            outputWindow = base.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            // The General pane is not created by default. We must force its creation
            if (guidPane == Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid)
            {
                hr = outputWindow.CreatePane(guidPane, "General", VISIBLE, DO_NOT_CLEAR_WITH_SOLUTION);
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            }

            // Get the pane
            hr = outputWindow.GetPane(guidPane, out outputWindowPane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);

            // Output the text
            if (outputWindowPane != null)
            {
                outputWindowPane.Activate();
                outputWindowPane.OutputString(text);
            }
        }

        protected override void Dispose(bool disposing)
        {
            docEventsListener.Dispose();
            base.Dispose(disposing);
        }

        #endregion

        public override ProjectFactory CreateProjectFactory()
        {
            return new RustProjectFactory(this);
        }

        public override CommonEditorFactory CreateEditorFactory()
        {
            return null;
        }

        public override uint GetIconIdForAboutBox()
        {
            throw new NotImplementedException();
        }

        public override uint GetIconIdForSplashScreen()
        {
            throw new NotImplementedException();
        }

        public override string GetProductName()
        {
            throw new NotImplementedException();
        }

        public override string GetProductDescription()
        {
            throw new NotImplementedException();
        }

        public override string GetProductVersion()
        {
            throw new NotImplementedException();
        }
    }
}
