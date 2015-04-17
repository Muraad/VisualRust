using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualRust.Shared
{
    public static class CommonUtil
    {
        public static bool Call<T>(this Action<T> action, T value)
        {
            bool isCalled = false;
            if ((isCalled = action != null))
                action(value);
            return isCalled;
        }

        public static U Call<T, U>(this Func<T, U> action, T value)
        {
            U result = default(U);
            if (action != null)
                action(value);
            return result;
        }

        public static bool Call<T, U>(this Action<T, U> action, T value, U uvalue)
        {
            bool isCalled = false;
            if ((isCalled = action != null))
                action(value, uvalue);
            return isCalled;
        }

        public static Tuple<T, Exception> TryCatch<T>(this Func<T> action)
        {
            try
            {
                return Tuple.Create<T, Exception>(action(), null);
            }
            catch (Exception ex)
            {
                return Tuple.Create<T, Exception>(default(T), ex);
            }
        }
    }
}
