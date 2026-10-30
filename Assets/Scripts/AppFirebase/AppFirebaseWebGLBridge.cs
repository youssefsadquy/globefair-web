#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace AppFirebase
{
    /// <summary>
    /// Low-level plumbing that talks to Assets/Plugins/WebGL/AppFirebaseBridge.jslib (which
    /// in turn talks to the Firebase JS SDK loaded by the WebGL template's index.html).
    ///
    /// AppAuth / AppFirestore / AppStorage are the classes gameplay scripts actually call;
    /// this class is only their WebGL transport. Every JS call is async (a browser network
    /// call), so each one gets a requestId and a TaskCompletionSource that is resolved when
    /// JS calls back into AppFirebaseBridgeReceiver.
    ///
    /// DllImport("__Internal") only links inside an actual built-and-served WebGL player -
    /// it cannot run in Editor Play Mode (there is no browser/JS runtime), so every call
    /// short-circuits with a clear NotSupportedException there instead of crashing.
    /// </summary>
    internal static class AppFirebaseWebGLBridge
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AppFirebase_SignIn(int requestId, string email, string password);
        [DllImport("__Internal")] private static extern void AppFirebase_SignUp(int requestId, string email, string password);
        [DllImport("__Internal")] private static extern void AppFirebase_SendPasswordReset(int requestId, string email);
        [DllImport("__Internal")] private static extern void AppFirebase_SignOut();
        [DllImport("__Internal")] private static extern string AppFirebase_GetCurrentUser();
        [DllImport("__Internal")] private static extern void AppFirebase_RegisterAuthListener();

        [DllImport("__Internal")] private static extern void AppFirebase_DocGet(int requestId, string path);
        [DllImport("__Internal")] private static extern void AppFirebase_DocSet(int requestId, string path, string dataJson);
        [DllImport("__Internal")] private static extern void AppFirebase_DocUpdate(int requestId, string path, string dataJson);
        [DllImport("__Internal")] private static extern void AppFirebase_DocDelete(int requestId, string path);
        [DllImport("__Internal")] private static extern void AppFirebase_DocAdd(int requestId, string collectionPath, string dataJson);
        [DllImport("__Internal")] private static extern void AppFirebase_CollectionGet(int requestId, string collectionPath, string queryJson);
        [DllImport("__Internal")] private static extern void AppFirebase_DocListenStart(int listenerId, string path);
        [DllImport("__Internal")] private static extern void AppFirebase_DocListenStop(int listenerId);

        [DllImport("__Internal")] private static extern void AppFirebase_StorageUploadBytes(int requestId, string path, byte[] bytes, int length);
        [DllImport("__Internal")] private static extern void AppFirebase_StorageGetDownloadUrl(int requestId, string path);
        [DllImport("__Internal")] private static extern void AppFirebase_StorageDelete(int requestId, string path);
#endif

        private static readonly Dictionary<int, TaskCompletionSource<object>> Pending = new Dictionary<int, TaskCompletionSource<object>>();
        private static readonly Dictionary<int, Action<Dictionary<string, object>>> Listeners = new Dictionary<int, Action<Dictionary<string, object>>>();
        private static int _nextRequestId = 1;
        private static int _nextListenerId = 1;

        private const string ReceiverObjectName = "AppFirebaseBridgeReceiver";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureReceiver()
        {
            if (GameObject.Find(ReceiverObjectName) != null) return;
            var go = new GameObject(ReceiverObjectName);
            go.AddComponent<AppFirebaseBridgeReceiver>();
            UnityEngine.Object.DontDestroyOnLoad(go);
#if !UNITY_EDITOR
            AppFirebase_RegisterAuthListener();
#endif
        }

        /// <summary>Raised whenever the JS SDK's onAuthStateChanged fires (sign-in, sign-out, session restore on page load).</summary>
        internal static event Action<Dictionary<string, object>> AuthStateChangedRaw;

        internal static void RaiseAuthStateChanged(Dictionary<string, object> userOrNull)
        {
            AuthStateChangedRaw?.Invoke(userOrNull);
        }

        // ---- generic async request plumbing -------------------------------------------------

        private static Task<object> Request(Action<int> invokeJs)
        {
#if UNITY_EDITOR
            var tcs = new TaskCompletionSource<object>();
            tcs.SetException(new NotSupportedException(
                "This Firebase call only works in an actual built-and-served WebGL player, not Editor Play Mode " +
                "(there is no browser/JavaScript runtime to bridge to here). Build and run it in a browser to test this flow."));
            return tcs.Task;
#else
            int id = _nextRequestId++;
            var tcs = new TaskCompletionSource<object>();
            Pending[id] = tcs;
            try
            {
                invokeJs(id);
            }
            catch (Exception e)
            {
                Pending.Remove(id);
                tcs.SetException(e);
            }
            return tcs.Task;
#endif
        }

        // Invoked by AppFirebaseBridgeReceiver when JS finishes a one-shot call.
        internal static void CompleteRequest(int requestId, bool success, object payloadOrError)
        {
            if (!Pending.TryGetValue(requestId, out var tcs)) return;
            Pending.Remove(requestId);
            if (success) tcs.SetResult(payloadOrError);
            else tcs.SetException(new Exception(payloadOrError?.ToString() ?? "Unknown Firebase WebGL bridge error"));
        }

        // ---- realtime listener plumbing --------------------------------------------------------

        internal static IDisposable ListenDocument(string path, Action<Dictionary<string, object>> onChange)
        {
#if UNITY_EDITOR
            Debug.LogWarning("AppFirestore.Listen is a no-op in Editor Play Mode under the WebGL target - test it in a built WebGL player.");
            return new NoopDisposable();
#else
            int listenerId = _nextListenerId++;
            Listeners[listenerId] = onChange;
            AppFirebase_DocListenStart(listenerId, path);
            return new ListenerHandle(listenerId);
#endif
        }

        // Invoked by AppFirebaseBridgeReceiver on every snapshot update for a live listener.
        internal static void DispatchListener(int listenerId, Dictionary<string, object> payload)
        {
            if (Listeners.TryGetValue(listenerId, out var cb)) cb(payload);
        }

#if !UNITY_EDITOR
        private sealed class ListenerHandle : IDisposable
        {
            private readonly int _listenerId;
            private bool _disposed;
            public ListenerHandle(int listenerId) { _listenerId = listenerId; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Listeners.Remove(_listenerId);
                AppFirebase_DocListenStop(_listenerId);
            }
        }
#endif

        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }

        // ---- Auth ---------------------------------------------------------------------------

        internal static Task<object> SignIn(string email, string password) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_SignIn(id, email, password);
#endif
        });

        internal static Task<object> SignUp(string email, string password) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_SignUp(id, email, password);
#endif
        });

        internal static Task<object> SendPasswordReset(string email) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_SendPasswordReset(id, email);
#endif
        });

        internal static void SignOut()
        {
#if !UNITY_EDITOR
            AppFirebase_SignOut();
#endif
        }

        /// <summary>Synchronous, matching FirebaseAuth.CurrentUser. Firebase JS SDK keeps this cached client-side.</summary>
        internal static Dictionary<string, object> GetCurrentUserRaw()
        {
#if UNITY_EDITOR
            return null;
#else
            string json = AppFirebase_GetCurrentUser();
            if (string.IsNullOrEmpty(json) || json == "null") return null;
            return AppMiniJson.AsDict(AppMiniJson.Parse(json));
#endif
        }

        // ---- Firestore ------------------------------------------------------------------------

        internal static Task<object> DocGet(string path) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_DocGet(id, path);
#endif
        });

        internal static Task<object> DocSet(string path, Dictionary<string, object> data) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_DocSet(id, path, AppMiniJson.Serialize(data));
#endif
        });

        internal static Task<object> DocUpdate(string path, Dictionary<string, object> data) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_DocUpdate(id, path, AppMiniJson.Serialize(data));
#endif
        });

        internal static Task<object> DocDelete(string path) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_DocDelete(id, path);
#endif
        });

        internal static Task<object> DocAdd(string collectionPath, Dictionary<string, object> data) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_DocAdd(id, collectionPath, AppMiniJson.Serialize(data));
#endif
        });

        internal static Task<object> CollectionGet(string collectionPath, Dictionary<string, object> querySpec) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_CollectionGet(id, collectionPath, AppMiniJson.Serialize(querySpec));
#endif
        });

        // ---- Storage --------------------------------------------------------------------------

        internal static Task<object> StorageUploadBytes(string path, byte[] bytes) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_StorageUploadBytes(id, path, bytes, bytes.Length);
#endif
        });

        internal static Task<object> StorageGetDownloadUrl(string path) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_StorageGetDownloadUrl(id, path);
#endif
        });

        internal static Task<object> StorageDelete(string path) => Request(id =>
        {
#if !UNITY_EDITOR
            AppFirebase_StorageDelete(id, path);
#endif
        });
    }

    /// <summary>
    /// GameObject-attached receiver for SendMessage callbacks coming from
    /// Assets/Plugins/WebGL/AppFirebaseBridge.jslib. Must stay named exactly
    /// "AppFirebaseBridgeReceiver" - the jslib calls SendMessage by that string name.
    /// </summary>
    internal sealed class AppFirebaseBridgeReceiver : MonoBehaviour
    {
        // jslib calls: SendMessage('AppFirebaseBridgeReceiver', 'OnJsResult', json)
        // json shape: {"requestId":N,"success":true,"payload":<any>} or {"requestId":N,"success":false,"error":"msg"}
        public void OnJsResult(string json)
        {
            var envelope = AppMiniJson.AsDict(AppMiniJson.Parse(json));
            int requestId = Convert.ToInt32(envelope["requestId"]);
            bool success = envelope.TryGetValue("success", out var s) && (bool)s;
            object payload = success
                ? (envelope.TryGetValue("payload", out var p) ? p : null)
                : (envelope.TryGetValue("error", out var e) ? e : "Unknown error");
            AppFirebaseWebGLBridge.CompleteRequest(requestId, success, payload);
        }

        // jslib calls: SendMessage('AppFirebaseBridgeReceiver', 'OnJsListenerEvent', json)
        // json shape: {"listenerId":N,"payload":{...}}
        public void OnJsListenerEvent(string json)
        {
            var envelope = AppMiniJson.AsDict(AppMiniJson.Parse(json));
            int listenerId = Convert.ToInt32(envelope["listenerId"]);
            var payload = AppMiniJson.AsDict(envelope.TryGetValue("payload", out var p) ? p : null);
            AppFirebaseWebGLBridge.DispatchListener(listenerId, payload);
        }

        // jslib calls: SendMessage('AppFirebaseBridgeReceiver', 'OnAuthStateChanged', json)
        // json shape: {"uid":"...","email":"..."} or "null"
        public void OnAuthStateChanged(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
            {
                AppFirebaseWebGLBridge.RaiseAuthStateChanged(null);
                return;
            }
            AppFirebaseWebGLBridge.RaiseAuthStateChanged(AppMiniJson.AsDict(AppMiniJson.Parse(json)));
        }
    }
}
#endif
