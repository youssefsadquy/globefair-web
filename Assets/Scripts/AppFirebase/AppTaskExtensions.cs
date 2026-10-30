using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AppFirebase
{
    /// <summary>
    /// Drop-in replacement for Firebase.Extensions.TaskExtension.ContinueWithOnMainThread.
    ///
    /// Every gameplay script used to call the Firebase SDK's own ContinueWithOnMainThread
    /// (from the Firebase.Extensions / Firebase.TaskExtension assembly). That assembly is
    /// fine on Windows/Android/iOS, but pulling it in also drags along FirebaseApp/
    /// Firebase.Platform native-init code that is not available when building for WebGL.
    ///
    /// This is a pure C# equivalent with the same signature, so call sites only need their
    /// `using Firebase.Extensions;` changed to `using AppFirebase;` - everything else about
    /// the ContinueWithOnMainThread(...) call stays identical.
    ///
    /// IMPORTANT: this used to be implemented with task.ContinueWith(t => RunOnMain(...)).
    /// That silently never ran on WebGL/IL2CPP: Task.ContinueWith(), when called without an
    /// explicit TaskScheduler, schedules its continuation on TaskScheduler.Current (or
    /// TaskScheduler.Default when not already running inside a task), and TaskScheduler.Default
    /// queues onto the .NET ThreadPool. Unity's WebGL/IL2CPP runtime is single-threaded and has
    /// no real thread pool servicing that queue, so the continuation was scheduled but never
    /// actually invoked - with no exception and no log, since scheduling itself succeeds. This
    /// broke every call site that chains work after a bridge call resolves (e.g.
    /// SignupManager.TrySignup's post-signup Firestore write + auto-login, LoginManager's
    /// user/business-user lookup, etc.): the underlying Firebase/JS call completed fine, but the
    /// ContinueWithOnMainThread callback that was supposed to run next just never fired.
    ///
    /// Plain `await` over the same tasks (see AppAuth.cs) was never affected, because `await`
    /// does not go through Task.ContinueWith/TaskScheduler at all - the compiler-generated state
    /// machine registers its resumption via the awaiter's OnCompleted, which is exactly what the
    /// fix below now does explicitly for ContinueWithOnMainThread too.
    /// </summary>
    public static class AppTaskExtensions
    {
        private static SynchronizationContext _unityContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureContext()
        {
            _unityContext = SynchronizationContext.Current;
        }

        public static Task ContinueWithOnMainThread(this Task task, Action<Task> continuation)
        {
            var tcs = new TaskCompletionSource<object>();
            task.GetAwaiter().OnCompleted(() =>
            {
                RunOnMain(() =>
                {
                    try
                    {
                        continuation(task);
                        tcs.TrySetResult(null);
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(e);
                    }
                });
            });
            return tcs.Task;
        }

        public static Task ContinueWithOnMainThread<TResult>(this Task<TResult> task, Action<Task<TResult>> continuation)
        {
            var tcs = new TaskCompletionSource<object>();
            task.GetAwaiter().OnCompleted(() =>
            {
                RunOnMain(() =>
                {
                    try
                    {
                        continuation(task);
                        tcs.TrySetResult(null);
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(e);
                    }
                });
            });
            return tcs.Task;
        }

        private static void RunOnMain(Action action)
        {
            // WebGL is single-threaded already, and if CaptureContext hasn't run yet
            // (very early startup) just invoke inline rather than dropping the callback.
            if (_unityContext == null || _unityContext == SynchronizationContext.Current)
            {
                action();
                return;
            }
            _unityContext.Post(_ => action(), null);
        }
    }
}
