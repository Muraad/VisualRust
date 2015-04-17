using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace VisualRust.Shared
{
    public static class Work
    {
        public const int CONCURRENCY_LEVEL = 4;
        public const bool AUTO_START = false;
        public const bool SEND_WORKER_STATUS_MESSAGES = false;

        static readonly WorkerPool Instance = new WorkerPool(CONCURRENCY_LEVEL, AUTO_START, SEND_WORKER_STATUS_MESSAGES);

        public static void StartWorkerPool(int concurrencyLevel = CONCURRENCY_LEVEL, int timerPeriod = 0)
        {
            Instance.Start(0);
        }

        public static Task<T> Run<T>(Func<T> work)
        {
            return Instance.Run<T>(work);
        }

        public static Task Run(Action work)
        {
            return Instance.Run(work);
        }

        public static TimerWork Schedule(Action<TimerWork> work, int period)
        {
            return Instance.ScheduleWork(work, period);
        }
    }

    public class TimerWork
    {
        public const int SUCCESSFULL_MULTIPLIER = 3;

        public Action Action { get; set; }
        public int PeriodInMs { get; set; }
        public double Multiplier { get; set; }
        public int TimerTicksSinceLastCall { get; set; }
        public Action Unsubscribe { get; set; }

        public TimerWork(int periodMs)
        {
            this.PeriodInMs = periodMs;
            this.Multiplier = 1.0;
        }
    }

    public class WorkerPool : TaskScheduler, IDisposable
    {
        public readonly List<WorkerThread> Workers = null;

        RwLock<bool> runFlag = RwLock.New(false);
        TaskFactory taskFactory = null;
        BlockingCollection<WorkerStatusMessage> ThreadStatusReceiver = null;
        Timer timer = null;
        LinkedList<TimerWork> timerWorkItems = new LinkedList<TimerWork>();
        int timerPeriod = 0;

        public WorkerPool(int numberOfThreads = 1, bool autoStart = false, bool sendStatusMessages = false, int timerPeriod = 100)
        {
            this.timerPeriod = timerPeriod;

            if (sendStatusMessages)
                ThreadStatusReceiver = new BlockingCollection<WorkerStatusMessage>();

            Workers = (from i in Enumerable.Range(0, numberOfThreads)
                       let channel = new Channel(ThreadStatusReceiver)
                       select new WorkerThread(i, channel, sendStatusMessages, "Worker " + i, 0, true)).ToList();

            taskFactory = new TaskFactory(this);

            if (autoStart)
                Start(timerPeriod);
        }

        public Action<Exception> OnException { get; set; }

        public void Start(int timerPeriod)
        {
            foreach (var worker in Workers)
                worker.Start();

            timer = new Timer(_ => TimerCallback(), null, 0, timerPeriod);

            Debug.WriteLine("Processor started with " + Workers.Count + " worker threads...");
            Debug.WriteLine("Timer period is " + timerPeriod);
        }

        public void Stop()
        {
            // send id Action to be sure the thread is woken up
            foreach (var worker in Workers)
                worker.Stop();

            Workers.Clear();
        }

        public Task<T> Run<T>(Func<T> work)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            Action workItem = () =>
            {
                try
                {
                    tcs.SetResult(work());
                }
                catch (Exception exc)
                {
                    tcs.TrySetException(exc);
                }

            };
            var blockingCollection = Scheduler(Workers);
            if (blockingCollection != null)
                blockingCollection.Add(workItem);
            return tcs.Task;
        }

        public Task Run(Action work)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            Action workItem = () =>
            {
                try
                {
                    work();
                    tcs.SetResult(42);
                }
                catch (Exception exc)
                {
                    tcs.TrySetException(exc);
                }

            };
            var blockingCollection = Scheduler(Workers);
            if (blockingCollection != null)
                blockingCollection.Add(workItem);
            return tcs.Task;
        }

        public TimerWork ScheduleWork(Action<TimerWork> work, int period)
        {
            TimerWork timerWork = new TimerWork(period);
            timerWork.Action = () => work(timerWork);

            LinkedListNode<TimerWork> node = timerWorkItems.AddLast(timerWork);

            timerWork.Unsubscribe = () => timerWorkItems.Remove(node);
            return timerWork;
        }

        protected override void QueueTask(Task task)
        {
            Run<int>(() =>
            {
                task.RunSynchronously();
                return 42;
            });
        }

        public Func<List<WorkerThread>, BlockingCollection<Action>> Scheduler = workerList =>
        {
            // simply get the one with smallest work channel count
            return (from w in workerList
                    orderby w.Channel.Work.Count ascending
                    select w.Channel.Work).FirstOrDefault();
        };

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotImplementedException();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        void TimerCallback()
        {
            CheckWorkerThreads();

            // schedule timer work items
            var items = from twi in timerWorkItems
                        where ( ++twi.TimerTicksSinceLastCall * (timerPeriod * twi.Multiplier)) >= twi.PeriodInMs
                        select twi;

            foreach (TimerWork work in items)
            {
                work.TimerTicksSinceLastCall = 0;
                Run(work.Action);
            }
        }

        void CheckWorkerThreads()
        {

        }

        public void Dispose()
        {
            this.Stop();
            timer.Dispose();
            timerWorkItems.Clear();
        }
    }

    public enum WorkerState
    {
        Working,
        FinishedItem
    }

    public class WorkerStatusMessage
    {
        public WorkerState State { get; private set; }

        public WorkerStatusMessage(WorkerState state)
        {
            State = state;
        }
    }

    public sealed class Channel
    {
        public BlockingCollection<Action> Work { get; set; }
        public BlockingCollection<WorkerStatusMessage> Status { get; set; }

        public Channel(BlockingCollection<WorkerStatusMessage> statusChannel)
        {
            Work = new BlockingCollection<Action>();
            Status = statusChannel;
        }

        public void SendWorkerState(WorkerState state)
        {
            SendWorkerStatusMsg(new WorkerStatusMessage(state));
        }

        public void SendWorkerStatusMsg(WorkerStatusMessage statusMsg)
        {
            if(Status != null)
                Status.Add(statusMsg);
        }
    }

    public class WorkerThread
    {
        public const int MAX_WORK_ITEMS = 150;

        public readonly RwLock<bool> IsRunning = RwLock.New(false);
        public RwLock<System.Threading.Thread> Thread { get; private set; }
        public readonly Channel Channel = null;
        public int WorkerId { get; set; }

        string threadName = "";
        int maxStackSize = 0;
        bool isBackground = true;
        bool sendStatusMessages = false;

        public WorkerThread(
            int id,
            Channel channel,
            bool sendStatusMessages = false,
            string threadName = "",
            int maxStackSize = 0,
            bool isBackground = true)
        {
            this.Channel = channel;
            this.WorkerId = id;
            this.threadName = threadName;
            this.maxStackSize = maxStackSize;
            this.isBackground = isBackground;
            this.sendStatusMessages = sendStatusMessages;
            SetUpThread();
        }

        private void SetUpThread()
        {
            IsRunning.WriteLocked(_ => IsRunning.Value = false);
            Thread = RwLock.New(new Thread(new ThreadStart(WorkLoop), maxStackSize));
            Thread.WriteLocked(t =>
            {
                t.Name = threadName;
                t.IsBackground = isBackground;
            });
        }

        public Action<WorkerThread, Exception> OnException = (wt, exc) =>
        {
            if (!wt.Thread.ReadLocked(t => t.IsAlive))    // if thread is not alive
                wt.Restart();
        };

        public void Start()
        {
            //Debug.WriteLine("Starting worker thread: " + thread.Name);
            IsRunning.WriteLocked(_ => IsRunning.Value = true);
            Thread.Value.Start();
        }

        public void Restart()
        {
            Reset();
            Start();
        }

        public void Reset()
        {
            bool isAlive = Thread.ReadLocked(t => t.IsAlive);
            if (isAlive)
                Thread.WriteLocked(t => t.Abort());
            SetUpThread();
        }

        public void Stop()
        {
            //Debug.WriteLine("Stopping worker thread: " + thread.Name);
            IsRunning.WriteLocked(_ => IsRunning.Value = false);
            Channel.Work.CompleteAdding();
        }

        void WorkLoop()
        {
            try
            {
                while (IsRunning.ReadLocked(rf => rf))
                {
                    foreach (var workitem in Channel.Work.GetConsumingEnumerable())
                    {
                        Channel.SendWorkerState(WorkerState.Working);
                        workitem();
                        Channel.SendWorkerState(WorkerState.FinishedItem);
                    }
                    System.Threading.Thread.Yield();
                }
            }
            catch (ThreadAbortException)
            {
                Debug.WriteLine("Thread " + Thread.Value.Name + " aborted ");
            }
            catch (Exception exc)
            {
                IsRunning.WriteLocked(_ => IsRunning.Value = false);
                OnException.Call(this, exc);
            }
        }
    }
}
