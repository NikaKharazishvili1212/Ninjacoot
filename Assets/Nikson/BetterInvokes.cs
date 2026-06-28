using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace Nikson
{
    // Returned by WaitRepeating; save it and call CancelWait() to stop the repeat early
    // Usage: TimerHandle handle = this.WaitRepeating(...);  handle.CancelWait();
    public class TimerHandle
    {
        internal bool Cancelled;
        public void CancelWait() => Cancelled = true;
    }

    // This class is for methods like: this.Wait(...);  TimerHandle handle = this.WaitRepeating(...);  handle.CancelWait();
    // Note: add "using Nikson;" in every script you are going to use these methods
    public static class BetterInvokes
    {
        // Similar to Invoke, but supports multiple calls in the lambda, safe if object is destroyed/disabled, more efficient than coroutine
        // Usage: this.Wait(2f, () => { action1(); action2(); });
        public static void Wait(this MonoBehaviour mb, float seconds, UnityAction action) => Runner.Schedule(mb, seconds, action);

        // Repeats action every interval seconds, first call happens after firstDelay; optionally stops after maxRepeats, then calls optional onComplete
        // Set firstDelay to 0 for an instant first call (e.g. healing potion: heal now + heal again every X seconds)
        // Usage: this.WaitRepeating(0f, 1f, () => { actions() }, 5, () => { onComplete() });
        public static TimerHandle WaitRepeating(this MonoBehaviour mb, float firstDelay, float interval, UnityAction action, int maxRepeats = -1, UnityAction onComplete = null) => Runner.ScheduleRepeating(mb, firstDelay, interval, action, maxRepeats, onComplete);

        static UtilsRunner runner;
        static UtilsRunner Runner
        {
            get
            {
                if (runner != null) return runner;
                var go = new GameObject("BetterInvokes_Runner");
                Object.DontDestroyOnLoad(go); // Survives scene loads
                runner = go.AddComponent<UtilsRunner>();
                return runner;
            }
        }

        class UtilsRunner : MonoBehaviour
        {
            struct TimerEntry
            {
                public float TriggerTime;
                public float Interval;
                public int RepeatCount;     // -1 = infinite, 0 = one-shot
                public UnityAction Action;
                public UnityAction OnComplete;
                public MonoBehaviour Owner;
                public TimerHandle Handle;  // null for one-shot Wait
            }

            readonly List<TimerEntry> timers = new();

            public void Schedule(MonoBehaviour owner, float delay, UnityAction action)
            {
                timers.Add(new TimerEntry
                {
                    TriggerTime = Time.time + delay,
                    Action = action,
                    Owner = owner,
                    Handle = null
                });
            }

            public TimerHandle ScheduleRepeating(MonoBehaviour owner, float firstDelay, float interval, UnityAction action, int maxRepeats, UnityAction onComplete)
            {
                var handle = new TimerHandle();
                timers.Add(new TimerEntry
                {
                    TriggerTime = Time.time + firstDelay,
                    Interval = interval,
                    RepeatCount = maxRepeats,
                    Action = action,
                    OnComplete = onComplete,
                    Owner = owner,
                    Handle = handle
                });
                return handle;
            }

            void Update()
            {
                for (int i = timers.Count - 1; i >= 0; i--)
                {
                    var t = timers[i];

                    if (t.Handle != null && t.Handle.Cancelled) { RemoveAt(i); continue; }
                    if (Time.time < t.TriggerTime) continue;
                    if (t.Owner) t.Action?.Invoke();

                    if (t.RepeatCount == -1)        // infinite
                    {
                        t.TriggerTime = Time.time + t.Interval;
                        timers[i] = t;
                    }
                    else if (t.RepeatCount > 1)     // more repeats left
                    {
                        t.TriggerTime = Time.time + t.Interval;
                        t.RepeatCount--;
                        timers[i] = t;
                    }
                    else                            // done
                    {
                        if (t.Owner) t.OnComplete?.Invoke();
                        RemoveAt(i);
                    }
                }
            }

            void RemoveAt(int i)
            {
                timers[i] = timers[timers.Count - 1];
                timers.RemoveAt(timers.Count - 1);
            }
        }
    }
}