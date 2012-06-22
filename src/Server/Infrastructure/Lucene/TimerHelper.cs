using System;
using System.Collections.Generic;
using System.Threading;

namespace NuGet.Server.Infrastructure.Lucene
{
    public class TimerHelper : IDisposable
    {
        private static readonly TimeSpan Never = TimeSpan.FromMilliseconds(-1);

        private readonly IDictionary<object, Timer> timers = new Dictionary<object, Timer>();

        public void AddTimer(object key, TimerCallback callback, object state)
        {
            AddTimer(key, callback, state, Never);
        }

        public void AddTimer(object key, TimerCallback callback, object state, TimeSpan dueTime)
        {
            lock (timers)
            {
                AddTimer(key, callback, state, dueTime, false);
            }
        }

        public bool CancelTimer(object key)
        {
            lock (timers)
            {
                Timer timer;
                if (!timers.TryGetValue(key, out timer)) return false;

                timers.Remove(key);
                timer.Dispose();
                return true;
            }
        }

        public void ResetTimer(object key, TimerCallback callback, object state, TimeSpan dueTime)
        {
            lock (timers)
            {
                Timer timer;

                if (timers.TryGetValue(key, out timer))
                {
                    timer.Change(dueTime, Never);
                }
                else
                {
                    AddTimer(key, callback, state, dueTime, true);
                }
            }
        }

        public void ResetTimer(object key, TimeSpan dueTime)
        {
            lock (timers)
            {
                Timer timer;

                if (!timers.TryGetValue(key, out timer))
                {
                    throw new ArgumentException("No timer exists for key " + key, "key");
                }

                timer.Change(dueTime, Never);
            }
        }

        public void Dispose()
        {
            lock (timers)
            {
                foreach (var timer in timers.Values)
                {
                    timer.Dispose();
                }
                timers.Clear();
            }
        }

        // Must aquire lock on timers when calling this method.
        private void AddTimer(object key, TimerCallback callback, object state, TimeSpan dueTime, bool singleUse)
        {
            var stateWrapper = new TimerCallbackState(key, state, callback, singleUse);

            var execSynchronously = dueTime == TimeSpan.Zero;

            timers[key] = new Timer(CallbackWrapper, stateWrapper, execSynchronously ? Never : dueTime, Never);

            if (execSynchronously)
            {
                // Execute synchronously if timeout is zero.
                CallbackWrapper(stateWrapper);
            }
        }

        private void CallbackWrapper(object stateObj)
        {
            var state = (TimerCallbackState)stateObj;

            if (state.SingleUse)
            {
                lock (timers)
                {
                    timers[state.Key].Dispose();
                    timers.Remove(state.Key);
                }
            }

            state.Callback(state.State);
        }

        /// <summary>
        /// For unit testing purposes only.  Should not be used by
        /// code because state can change after lock is released.
        /// </summary>
        public bool ContainsKey(object key)
        {
            lock (timers)
            {
                return timers.ContainsKey(key);
            }
        }

        private class TimerCallbackState
        {
            public readonly object Key;
            public readonly object State;
            public readonly TimerCallback Callback;
            public readonly bool SingleUse;

            public TimerCallbackState(object key, object state, TimerCallback callback, bool singleUse)
            {
                Key = key;
                State = state;
                Callback = callback;
                SingleUse = singleUse;
            }
        }
    }
}