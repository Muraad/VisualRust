using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;

using System.Threading;
using VisualRust.Project;
using VisualRust.Shared;

namespace VisualRust
{
    public static class TaskMessages
    {
        static RwLock<ErrorListProvider> errorListProvider = null;
        static IServiceProvider serviceProvider = null;

        public static void Init(IServiceProvider serviceProvider)
        {
            errorListProvider = RwLock.New(new ErrorListProvider(serviceProvider));
            TaskMessages.serviceProvider = serviceProvider;
        }

        public static bool Contains(Microsoft.VisualStudio.Shell.Task shellTask)
        {
            return errorListProvider.ReadLocked(elp => elp.Tasks.Contains(shellTask));
        }

        public static void Show()
        {
            errorListProvider.ReadLocked(elp => elp.Show());
        }

        public static void Refresh()
        {
            errorListProvider.ReadLocked(elp => elp.Refresh());
        }

        public static void Clear()
        {
            errorListProvider.WriteLocked(elp => elp.Tasks.Clear());
        }

        public static void RemoveAllFromErrorCategory(string subCategory, TaskErrorCategory errorCategory)
        {
            List<DocumentTask> toBeRemoved = new List<DocumentTask>();

            ForEachDocumentTask(docTask =>
                {
                    if (docTask.ErrorCategory == errorCategory)
                        toBeRemoved.Add(docTask);
                }, null);

            toBeRemoved.ForEach(dt => errorListProvider.Value.Tasks.Remove(dt));
        }
        public static void QueueRustcMessage(string subCategory, RustcParsedMessage rustcMsg, IVsHierarchy hierarchy, bool refresh = true)
        {
            if (rustcMsg.Type == RustcParsedMessageType.Error)
            {
                QueueMessage(subCategory,
                    rustcMsg.ErrorCode,
                    rustcMsg.File,
                    rustcMsg.Message,
                    rustcMsg.LineNumber, rustcMsg.ColumnNumber,
                    rustcMsg.EndLineNumber, rustcMsg.EndColumnNumber,
                    hierarchy, true, "", "", refresh);
            }
            else
            {
                QueueMessage(subCategory,
                    rustcMsg.ErrorCode,
                    rustcMsg.File,
                    rustcMsg.Message,
                    rustcMsg.LineNumber, rustcMsg.ColumnNumber,
                    rustcMsg.EndLineNumber, rustcMsg.EndColumnNumber,
                    hierarchy, false, "", "", refresh);
            }
        }

        public static bool Contains(
            string subCategory,
            string errorCode,
            string file, string msg,
            int line, int column,
            IVsHierarchy hierarchy,
            bool isError = true,
            string helpKeyword = "",
            string senderName = "")
        {
            bool result = false;

            ForEachDocumentTask(null, breakAction: docTask =>
                {
                    if (docTask.Line == line - 1
                        && docTask.Column == column - 1
                        && docTask.Document.EndsWith(file)
                        && isError ? docTask.ErrorCategory == TaskErrorCategory.Error : docTask.ErrorCategory == TaskErrorCategory.Warning
                        && docTask.HelpKeyword == helpKeyword
                        && docTask.HierarchyItem == hierarchy
                        && docTask.Text == msg)
                    {
                        result = true;
                    }
                    return result;
                });

            return result;
        }

        private static void ForEachDocumentTask(Action<DocumentTask> action, Func<DocumentTask, bool> breakAction)
        {
            foreach (Microsoft.VisualStudio.Shell.Task t in errorListProvider.Value.Tasks)
            {
                DocumentTask docTask = t as DocumentTask;
                if (docTask != null)
                {
                    action.Call(docTask);
                    if (breakAction != null && breakAction.Call(docTask))
                        break;
                }
            }
        }

        public static void QueueMessage(
            string subCategory,
            string errorCode,
            string file, string msg,
            int line, int column, int endLine, int endColumn,
            IVsHierarchy hierarchy,
            bool isError = true,
            string helpKeyword = "",
            string senderName = "",
            bool refresh = true)
        {
            if (Contains(subCategory, errorCode, file, msg, line, column, hierarchy, isError, helpKeyword, senderName))
                return;

            // This enqueues a function that will later be run on the main (UI) thread
            TextSpan span;
            string filePath;
            MARKERTYPE marker;
            TaskErrorCategory category;

            VisualRust.Project.RustProjectNode rustProject = hierarchy as VisualRust.Project.RustProjectNode;
            var ivsSolution = (IVsSolution)Package.GetGlobalService(typeof(IVsSolution));
            var dte = (EnvDTE80.DTE2)Package.GetGlobalService(typeof(EnvDTE.DTE));

            span = new TextSpan();
            // spans require zero-based indices
            span.iStartLine = line - 1;
            span.iEndLine = endLine - 1;
            span.iStartIndex = column - 1;
            span.iEndIndex = endColumn - 1;
            filePath = Path.Combine(Path.GetDirectoryName(rustProject.GetCanonicalName()), file);

            if (isError)
            {
                marker = MARKERTYPE.MARKER_CODESENSE_ERROR; // red squiggles
                category = TaskErrorCategory.Error;
            }
            else
            {
                marker = MARKERTYPE.MARKER_COMPILE_ERROR; // red squiggles
                category = TaskErrorCategory.Warning;
            }

            if (span.iEndLine == -1) span.iEndLine = span.iStartLine;
            if (span.iEndIndex == -1) span.iEndIndex = span.iStartIndex;

            IVsUIShellOpenDocument openDoc = serviceProvider.GetService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            if (openDoc == null)
                throw new NotImplementedException(); // TODO

            IVsWindowFrame frame;
            IOleServiceProvider sp;
            IVsUIHierarchy hier;
            uint itemid;
            Guid logicalView = Microsoft.VisualStudio.VSConstants.LOGVIEWID_Code;

            IVsTextLines buffer = null;

            // Notes about acquiring the buffer:
            // If the file physically exists then this will open the document in the current project. It doesn't matter if the file is a member of the project.
            // Also, it doesn't matter if this is a Rust file. For example, an error in Microsoft.Common.targets will cause a file to be opened here.
            // However, opening the document does not mean it will be shown in VS. 
            if (!Microsoft.VisualStudio.ErrorHandler.Failed(openDoc.OpenDocumentViaProject(file, ref logicalView, out sp, out hier, out itemid, out frame)) && frame != null)
            {
                object docData;
                frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocData, out docData);

                // Get the text lines
                buffer = docData as IVsTextLines;

                if (buffer == null)
                {
                    IVsTextBufferProvider bufferProvider = docData as IVsTextBufferProvider;
                    if (bufferProvider != null)
                    {
                        bufferProvider.GetTextBuffer(out buffer);
                    }
                }
            }

            DocumentTask task = new DocumentTask(serviceProvider, buffer, marker, span, file);
            task.ErrorCategory = category;
            task.Document = file;
            task.Line = span.iStartLine;
            task.Column = span.iStartIndex;
            task.Priority = category == TaskErrorCategory.Error ? TaskPriority.High : TaskPriority.Normal;
            task.Text = msg;
            task.Category = TaskCategory.BuildCompile;
            task.HierarchyItem = hierarchy;

            Add(task);
            if(refresh)
                Refresh();

            // NOTE: Unlike output we dont want to interactively report the tasks. So we never queue
            // call ReportQueuedTasks here. We do this when the build finishes.
        }

        public static void Add(Microsoft.VisualStudio.Shell.Task task)
        {
            errorListProvider.ReadLocked(elp => elp.Tasks.Add(task));
        }

        public static void AddError(
            string message,
            string file,
            int line,
            int endLine,
            int column,
            int endColumn,
            IVsHierarchy hierarchy,
            TaskCategory category = TaskCategory.BuildCompile,
            TaskPriority priority = TaskPriority.High)
        {
            AddTask(message, file, line, endLine, column, endColumn, hierarchy, category, TaskErrorCategory.Error, priority);
        }

        public static void AddWarning(
            string message,
            string file,
            int line,
            int endLine,
            int column,
            int endColumn,
            IVsHierarchy hierarchy,
            TaskCategory category = TaskCategory.BuildCompile,
            TaskPriority priority = TaskPriority.High)
        {
            AddTask(message, file, line, endLine, column, endColumn, hierarchy, category, TaskErrorCategory.Warning, priority);
        }

        public static void AddMessage(
            string message,
            string file,
            int line,
            int endLine,
            int column,
            int endColumn,
            Microsoft.VisualStudio.Shell.Interop.IVsHierarchy hierarchy,
            TaskCategory category = TaskCategory.BuildCompile,
            TaskPriority priority = TaskPriority.High)
        {
            AddTask(message, file, line, endLine, column, endColumn, hierarchy, category, TaskErrorCategory.Message, priority);
        }

        public static void AddTask(
            string message, 
            string file,
            int line,
            int endLine,
            int column,
            int endColumn,
            IVsHierarchy hierarchy,
            TaskCategory category, 
            TaskErrorCategory errorCategory, 
            TaskPriority priority = TaskPriority.Normal)
        {
            //errorListProvider.Tasks.Clear();

            TextSpan span = new TextSpan()
            { 
                iStartLine = line, 
                iStartIndex = column, 
                iEndLine = endLine, 
                iEndIndex = endColumn 
            };

            VisualRust.Project.RustProjectNode rustProject = hierarchy as VisualRust.Project.RustProjectNode;

            Add(new ErrorTask() //DocumentTask(serviceProvider, null!!!, MARKERTYPE.MARKER_COMPILE_ERROR, span, file)
                {
                    Category = TaskCategory.BuildCompile, 
                    ErrorCategory = errorCategory, 
                    Line = line,
                    Column = column,
                    Priority = priority, 
                    Text = message, 
                    HierarchyItem = hierarchy
                });


            Refresh(); 		// make sure it is visible
        }
    }
}
