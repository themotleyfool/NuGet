using System;
using System.Threading;
using NuGet.Server.Infrastructure.Lucene;
using Xunit;
using Xunit.Sdk;

namespace Server.Test.Lucene
{
    public class TimerHelperTests
    {
        private enum TestMode
        {
            Add,
            AddWithNullState,
            ResetWithCallback,
            ResetWithoutCallback,
        }
        private const string Key = "sample";

        private readonly TimerHelper helper;
        private readonly object[] state;
        private int callCount;
        private bool rescheduleTimerOnCallback;
        private bool wasRescheduled;

        public TimerHelperTests()
        {
            helper = new TimerHelper();
            state = new[] { new object(), null };
            callCount = 0;
            rescheduleTimerOnCallback = false;
            wasRescheduled = false;
        }

        [Fact]
        public void AddDoesNotDisposeTimer()
        {
            helper.AddTimer(Key, Callback, state[0], TimeSpan.FromMilliseconds(-1));

            DoTimerTest(TestMode.ResetWithoutCallback);

            Assert.Same(state[1], state[0]);
        }

        [Fact]
        public void AddTimerDoesNotRemoveAfterSingleUse()
        {
            DoTimerTest(TestMode.Add);

            Assert.True(helper.ContainsKey(Key), "Should retain Timer after firing.");
        }

        [Fact]
        public void CallbackStateIsNullSafe()
        {
            DoTimerTest(TestMode.AddWithNullState);
        }

        [Fact]
        public void ResetTimerCallsWithState()
        {
            DoTimerTest(TestMode.ResetWithCallback);

            Assert.Same(state[1], state[0]);
        }

        [Fact]
        public void ResetTimerRemoves()
        {
            DoTimerTest(TestMode.ResetWithCallback);

            Assert.False(helper.ContainsKey(Key), "Should retain Timer after firing.");
        }

        [Fact]
        public void ResetTimerWithinCallback()
        {
            rescheduleTimerOnCallback = true;

            DoTimerTest(TestMode.ResetWithCallback);

            WaitForCallCount(2);

            Assert.Equal(2, callCount);
        }

        private void Callback(object cbState)
        {
            lock (state)
            {
                callCount++;
                state[1] = cbState;
            }

            if (rescheduleTimerOnCallback && !wasRescheduled)
            {
                // Only reset the timer once.
                wasRescheduled = true;
                helper.ResetTimer(Key, Callback, state, TimeSpan.FromMilliseconds(1));
            }

            lock (this)
            {
                Monitor.Pulse(this);
            }
        }

        private void DoTimerTest(TestMode mode)
        {
            var tmp = mode == TestMode.AddWithNullState ? null : state[0];

            switch (mode)
            {
                case TestMode.AddWithNullState:
                case TestMode.Add:
                    helper.AddTimer(Key, Callback, tmp, TimeSpan.FromMilliseconds(1));
                    break;
                case TestMode.ResetWithCallback:
                    helper.ResetTimer(Key, Callback, tmp, TimeSpan.FromMilliseconds(1));
                    break;
                case TestMode.ResetWithoutCallback:
                    helper.ResetTimer(Key, TimeSpan.FromMilliseconds(1));
                    break;
                default:
                    throw new AssertException("Unsupported TestMode " + mode);
            }

            WaitForCallCount(1);

            if (rescheduleTimerOnCallback)
            {
                Assert.InRange(callCount, 1, int.MaxValue);
            }
            else
            {
                Assert.Equal(1, callCount);
            }
        }

        private void WaitForCallCount(int expectedCallcount)
        {
            var syncRoot = this;

            lock (syncRoot)
            {
                if (callCount < expectedCallcount)
                {
                    Monitor.Wait(syncRoot, 1000);
                }
            }
        }
    }
}