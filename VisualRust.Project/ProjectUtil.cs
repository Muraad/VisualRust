using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
//using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project.Automation;
namespace VisualRust.Project
{
    public static class ProjectUtil
    {
        internal static IVsSolution GetIVsSolution()
        {
            return (IVsSolution)Package.GetGlobalService(typeof(IVsSolution));
        }

        internal static EnvDTE80.DTE2 GetDTE2()
        {
            return (EnvDTE80.DTE2)Package.GetGlobalService(typeof(EnvDTE.DTE));
        }

        internal static void SaveSolution(IVsHierarchy hierarchyDoc)
        {
            var ivsSolution = GetIVsSolution();
            ivsSolution.SaveSolutionElement(0, hierarchyDoc, 0);
        }

        internal static OAProject GetActiveSolutionsProject()
        {
            IVsSolution ivsSolution = GetIVsSolution();
            EnvDTE80.DTE2 dte = GetDTE2();
            var activeSolutions = dte.Solution.DTE.ActiveSolutionProjects as object[];
            OAProject oaProject = null;
            if(activeSolutions != null && activeSolutions.Length > 0)
            {
                oaProject = activeSolutions[0] as OAProject;
                if(oaProject != null)
                {
                    Debug.WriteLine(oaProject.FullName);
                }
            }
            return oaProject;
        }

        internal static RustProjectNode GetActiveRustProject()
        {
            IVsSolution ivsSolution = GetIVsSolution();
            EnvDTE80.DTE2 dte = GetDTE2();
            var activeSolutions = dte.Solution.DTE.ActiveSolutionProjects as object[];
            RustProjectNode rustProject = null;
            if (activeSolutions != null && activeSolutions.Length > 0)
            {
                OAProject oaProject = activeSolutions[0] as OAProject;
                if (oaProject != null && oaProject.FileName.EndsWith(".rsproj"))
                {
                    rustProject = oaProject.Project as RustProjectNode;
                }
            }
            return rustProject;

        }
        internal static RustProjectNode GetSelectedRustProjectNode()
        {
            IVsSolution ivsSolution = GetIVsSolution();
            EnvDTE80.DTE2 dte = GetDTE2();
            GetActiveSolutionsProject();
            //Get first project details
            EnvDTE.Project proj = dte.Solution.Projects.Item(1);
            var containingProj = proj.ProjectItems.ContainingProject;
            OAProject oaProj = containingProj as OAProject;
            RustProjectNode rustProjNode = oaProj.ProjectNode as RustProjectNode;
            return rustProjNode;
        }

        #region Show Dialogs functions

        internal static IServiceProvider PackageServiceProvider = null;

        internal static void ShowExceptionDialog(Exception exc, string msg = "")
        {
            if (PackageServiceProvider != null)
                TaskDialog.ForException(PackageServiceProvider, exc, msg);
        }

        internal static void ShowMessageBox(string header, string msg)
        {
            // Show a Message Box to prove we were here
            IVsUIShell uiShell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
            Guid clsid = Guid.Empty;
            int result;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(uiShell.ShowMessageBox(
                       0,
                       ref clsid,
                       header,
                       msg,
                       string.Empty,
                       0,
                       OLEMSGBUTTON.OLEMSGBUTTON_OK,
                       OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                       OLEMSGICON.OLEMSGICON_INFO,
                       0,        // false
                       out result));
        }

        #endregion

        #region Print to pane functions

        [Conditional("DEBUG")]
        internal static void DebugPrintToOutput(string s, params object[] args)
        {
            PrintToOutput("[DEBUG] " + s, args);
        }

        internal static void PrintToOutput(string s, params object[] args)
        {
            IVsOutputWindow outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            Guid paneGuid = VSConstants.GUID_OutWindowGeneralPane;
            IVsOutputWindowPane pane;
            ErrorHandler.ThrowOnFailure(outWindow.CreatePane(paneGuid, "General", 1, 0));
            outWindow.GetPane(ref paneGuid, out pane);
            pane.OutputString(string.Format("[VisualRust]: " + s, args) + "\n");
            pane.Activate();
        }

        internal static void PrintToDebug(string category, string text)
        {
            text = "[" + category + "]    ";
            OutputLine(VSConstants.OutputWindowPaneGuid.DebugPane_guid, text);
        }

        internal static void PrintToBuild(string category, string text)
        {
            text = "[" + category + "]    " + text;
            OutputLine(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, text);
        }

        internal static void PrintToGeneral(string category, string text)
        {
            text = "[" + category + "]    ";
            OutputLine(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, text);
        }
        

        internal static void PrintToDebug(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.DebugPane_guid, text);
        }

        internal static void PrintToBuild(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid, text);
        }

        internal static void PrintToGeneral(string text)
        {
            OutputLine(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, text);
        }

        internal static void OutputLine(Guid guidPane, string text)
        {
            OutputString(guidPane, text + Environment.NewLine);
        }

        internal static void OutputString(Guid guidPane, string text)
        {
            IVsOutputWindowPane outputWindowPane = GetOutputWindowPane(guidPane);

            // Output the text
            if (outputWindowPane != null)
            {
                outputWindowPane.Activate();
                outputWindowPane.OutputString(text);
            }
        }

        #endregion

        #region GetXYWindowPane functions

        public static IVsOutputWindowPane GetDebugWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.DebugPane_guid);
        }

        public static IVsOutputWindowPane GetGeneralWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid);
        }

        public static IVsOutputWindowPane GetBuildWindowPane()
        {
            return GetOutputWindowPane(VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid);
        }

        public static IVsOutputWindowPane GetOutputWindowPane(Guid guidPane)
        {
            const int VISIBLE = 1;
            const int DO_NOT_CLEAR_WITH_SOLUTION = 0;

            IVsOutputWindow outputWindow;
            IVsOutputWindowPane outputWindowPane = null;
            int hr;

            // Get the output window
            outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            // The General pane is not created by default. We must force its creation
            if (guidPane == Microsoft.VisualStudio.VSConstants.OutputWindowPaneGuid.GeneralPane_guid)
            {
                hr = outputWindow.CreatePane(guidPane, "General", VISIBLE, DO_NOT_CLEAR_WITH_SOLUTION);
                Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            }

            // Get the pane
            hr = outputWindow.GetPane(guidPane, out outputWindowPane);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
            return outputWindowPane;
        }
        #endregion

    }
}
