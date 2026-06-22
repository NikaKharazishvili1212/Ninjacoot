using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

// This partial class is for methods like: this.Invoke2(...);  TimerHandle handle = this.InvokeRepeating2(...);  handle.CancelInvoke2();
public static partial class Utils
{
    // Similar to Invoke, but supports multiple calls in the lambda, safe if object is destroyed/disabled, can be cancelled, more efficient than coroutine
    // Usage: this.Invoke2(2f, () => { action1(); action2(); });
    public static TimerHandle Invoke2(this MonoBehaviour mb, float seconds, UnityAction action) => Runner.Schedule(mb, seconds, action);

    // Repeats action every interval seconds; optionally stops after maxRepeats, then calls optional onComplete
    // Usage: this.InvokeRepeating2(1f, () => { actions() }, 5, () => { onComplete() });
    public static TimerHandle InvokeRepeating2(this MonoBehaviour mb, float interval, UnityAction action, int maxRepeats = -1, UnityAction onComplete = null) => Runner.ScheduleRepeating(mb, interval, action, maxRepeats, onComplete);

    // Both (Invoke2 and InvokeRepeating2) return a TimerHandle that can be saved and cancelled at any time
    // Usage: TimerHandle handle = this.Invoke2(...);  handle.CancelInvoke2();
    public class TimerHandle
    {
        internal bool Cancelled;
        public void CancelInvoke2() => Cancelled = true;
    }

    static UtilsRunner runner;
    static UtilsRunner Runner
    {
        get
        {
            if (runner != null) return runner;
            var go = new GameObject("Utils_Runner");
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

        public TimerHandle Schedule(MonoBehaviour owner, float delay, UnityAction action)
        {
            var handle = new TimerHandle();
            timers.Add(new TimerEntry
            {
                TriggerTime = Time.time + delay,
                Action = action,
                Owner = owner,
                Handle = handle
            });
            return handle;
        }

        public TimerHandle ScheduleRepeating(MonoBehaviour owner, float interval, UnityAction action, int maxRepeats, UnityAction onComplete)
        {
            var handle = new TimerHandle();
            timers.Add(new TimerEntry
            {
                TriggerTime = Time.time + interval,
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