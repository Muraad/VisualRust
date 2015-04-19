using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using VisualRust.Shared;

namespace VisualRust.Project
{
    public class ProjectWorkPool : WorkerPool, IDisposable
    {
        #region Default tasks that are running periodically (Static List)

        /// <summary>
        /// A Tuple consisting of an Action that is called and as second item 
        /// the period in milliseconds.
        /// </summary>
        public static readonly List<Tuple<Action<TimerWork>, int>> DefaultProjectTasks = new List<Tuple<Action<TimerWork>, int>>()
        {
            //Start silent cargo build every 5 seconds
            Tuple.Create<Action<TimerWork>, int>(
                tw =>
                {
                    if(Interlocked.CompareExchange(ref IsCargoBuildRunning, 1, 0) == 0)
                    { 
                        CargoUtil.CallCargoProcess(
                            cargoFunc: workingDir => Shared.Cargo.Build(workingDir, false),
                            taskName: "",
                            printBuildOutput: false,
                            exitCodeCallBack: ec => exitCallback(tw, ec));
                    }
                }, 60000)
        };

        static int IsCargoBuildRunning = 0;

        // If this build was successfull set timer work item multiplier
        // else set it back to 1.0
        static Action<TimerWork, int> exitCallback = (tw, exitCode) =>
        {
            if (exitCode == 0)   // successfull (first time), lets wait 4 times as long for the next silent build
            {
                TaskMessages.RemoveAllFromTaskErrorCategory("Rust", Microsoft.VisualStudio.Shell.TaskErrorCategory.Error);
                if(tw.Multiplier == 1.0)
                    tw.Multiplier = 1.5;
            }
            if (exitCode != 0 && tw.Multiplier != 1.0)   // error exit code
                tw.Multiplier = 1.0;
            Interlocked.Decrement(ref IsCargoBuildRunning);
        };

        #endregion

        Microsoft.VisualStudioTools.Project.CommonProjectNode projNode = null;

        internal ProjectWorkPool(
            Microsoft.VisualStudioTools.Project.CommonProjectNode node = null, 
            int numberOfThreads = 2, bool autoStart = false, bool sendStatusMessages = true, int timerPeriod = 250) 
            : base(numberOfThreads, autoStart, sendStatusMessages, timerPeriod)
        {
            this.projNode = node;
            foreach (var tuple in DefaultProjectTasks)
                this.ScheduleWork(tuple.Item1, tuple.Item2);
        }

    }
}
