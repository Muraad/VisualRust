using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace VisualRust.Shared
{
    public class RwLock<T>
    {
        public T Value { get; set; }
        public ReaderWriterLockSlim Lock { get; private set; }

        public RwLock(T value)
        {
            Value = value;
            Lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }
    }

    public static class RwLock
    {
        public static RwLock<T> New<T>(T value)
        {
            return new RwLock<T>(value);
        }

        public static void ReadLocked<T>(this RwLock<T> rwLock, Action<T> action, Action<Exception> errorAction = null)
        {
            try
            {
                rwLock.Lock.EnterReadLock();
                action(rwLock.Value);
            }
            catch (Exception exc)
            {
                if (!errorAction.Call(exc))
                    throw exc;
            }
            finally
            {
                if (rwLock.Lock.IsReadLockHeld)
                    rwLock.Lock.ExitReadLock();
            }
        }

        public static U ReadLocked<T, U>(this RwLock<T> rwLock, Func<T, U> action, Action<Exception> errorAction = null)
        {
            U result = default(U);
            try
            {
                rwLock.Lock.EnterReadLock();
                result = action(rwLock.Value);
            }
            catch (Exception exc)
            {
                if (!errorAction.Call(exc))
                    throw exc;
            }
            finally
            {
                if (rwLock.Lock.IsReadLockHeld)
                    rwLock.Lock.ExitReadLock();
            }
            return result;
        }

        public static void WriteLocked<T>(this RwLock<T> rwLock, Action<T> action, Action<Exception> errorAction = null)
        {
            try
            {
                rwLock.Lock.EnterWriteLock();
                action(rwLock.Value);
            }
            catch (Exception exc)
            {
                if (!errorAction.Call(exc))
                    throw exc;
            }
            finally
            {
                if (rwLock.Lock.IsWriteLockHeld)
                    rwLock.Lock.ExitWriteLock();
            }
        }

        public static U WriteLocked<T, U>(this RwLock<T> rwLock, Func<T, U> action, Action<Exception> errorAction = null)
        {
            U result = default(U);
            try
            {
                rwLock.Lock.EnterWriteLock();
                result = action(rwLock.Value);
            }
            catch (Exception exc)
            {
                if (!errorAction.Call(exc))
                    throw exc;
            }
            finally
            {
                if (rwLock.Lock.IsWriteLockHeld)
                    rwLock.Lock.ExitWriteLock();
            }
            return result;
        }
    }
}
