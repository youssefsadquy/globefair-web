using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#if !UNITY_WEBGL
using Firebase.Firestore;
#endif

namespace AppFirebase
{
    /// <summary>
    /// Platform-neutral Firestore facade - native Firebase.Firestore SDK everywhere except
    /// WebGL, Firebase JS SDK (firestore-compat) via AppFirebaseWebGLBridge on WebGL.
    ///
    /// Mirrors the native FirebaseFirestore/CollectionReference/DocumentReference/Query surface
    /// this project actually uses (Collection/Document/WhereEqualTo/WhereIn/OrderBy/
    /// GetSnapshotAsync/SetAsync/UpdateAsync/DeleteAsync/AddAsync/Listen) so call sites read the
    /// same as before, just against "App*" types instead of the Firebase.Firestore ones.
    /// </summary>
    public sealed class AppFirestore
    {
        public static AppFirestore DefaultInstance { get; } = new AppFirestore();
        private AppFirestore() { }

        public AppCollectionReference Collection(string path) => new AppCollectionReference(path);
    }

    public sealed class AppCollectionReference
    {
        internal string Path { get; }
        internal AppCollectionReference(string path) { Path = path; }

        public AppDocumentReference Document(string id) => new AppDocumentReference($"{Path}/{id}");

        /// <summary>No-arg overload mirrors native Firestore's CollectionReference.Document() -
        /// generates a fresh client-side random ID up front (same as the native/JS SDKs do)
        /// so the caller can use docRef.Id immediately, before the write is even sent.</summary>
        public AppDocumentReference Document() => Document(GenerateAutoId());

        // A single shared instance, not "new Random()" per call: Random's default seed is
        // time-based (tick count), so creating a fresh instance per call can produce identical
        // IDs when Document() is called more than once within the same tick (e.g. from a loop
        // creating several documents back to back), silently colliding two documents.
        private static readonly Random _autoIdRandom = new Random();

        private static string GenerateAutoId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var sb = new System.Text.StringBuilder(20);
            lock (_autoIdRandom)
            {
                for (int i = 0; i < 20; i++) sb.Append(chars[_autoIdRandom.Next(chars.Length)]);
            }
            return sb.ToString();
        }

        public AppQuery WhereEqualTo(string field, object value) => new AppQuery(Path).WhereEqualTo(field, value);
        public AppQuery WhereIn(string field, IEnumerable<object> values) => new AppQuery(Path).WhereIn(field, values);
        public AppQuery OrderBy(string field) => new AppQuery(Path).OrderBy(field);

        public Task<AppQuerySnapshot> GetSnapshotAsync() => new AppQuery(Path).GetSnapshotAsync();

#if !UNITY_WEBGL
        public async Task<AppDocumentReference> AddAsync(Dictionary<string, object> data)
        {
            var native = FirebaseFirestore.DefaultInstance.Collection(Path);
            DocumentReference docRef = await native.AddAsync(AppFirestoreNativeConvert.ToNative(data));
            return new AppDocumentReference($"{Path}/{docRef.Id}");
        }
#else
        public async Task<AppDocumentReference> AddAsync(Dictionary<string, object> data)
        {
            object result = await AppFirebaseWebGLBridge.DocAdd(Path, data);
            var dict = AppMiniJson.AsDict(result);
            string newId = dict.TryGetValue("id", out var idVal) ? idVal as string : null;
            return new AppDocumentReference($"{Path}/{newId}");
        }
#endif
    }

    public sealed class AppQuery
    {
        private readonly string _collectionPath;
        private readonly List<Dictionary<string, object>> _wheres = new List<Dictionary<string, object>>();
        private string _orderBy;

        internal AppQuery(string collectionPath) { _collectionPath = collectionPath; }

        public AppQuery WhereEqualTo(string field, object value)
        {
            _wheres.Add(new Dictionary<string, object> { { "field", field }, { "op", "==" }, { "value", value } });
            return this;
        }

        public AppQuery WhereIn(string field, IEnumerable<object> values)
        {
            _wheres.Add(new Dictionary<string, object> { { "field", field }, { "op", "in" }, { "value", values.ToList() } });
            return this;
        }

        public AppQuery OrderBy(string field)
        {
            _orderBy = field;
            return this;
        }

#if !UNITY_WEBGL
        public async Task<AppQuerySnapshot> GetSnapshotAsync()
        {
            Query query = FirebaseFirestore.DefaultInstance.Collection(_collectionPath);
            foreach (var w in _wheres)
            {
                string field = (string)w["field"];
                string op = (string)w["op"];
                object value = w["value"];
                query = op == "in"
                    ? query.WhereIn(field, (List<object>)value)
                    : query.WhereEqualTo(field, AppFirestoreNativeConvert.ScalarToNative(value));
            }
            if (_orderBy != null) query = query.OrderBy(_orderBy);

            QuerySnapshot snap = await query.GetSnapshotAsync();
            return new AppQuerySnapshot(snap.Documents.Select(AppFirestoreNativeConvert.FromNative));
        }
#else
        public async Task<AppQuerySnapshot> GetSnapshotAsync()
        {
            var querySpec = new Dictionary<string, object>
            {
                { "wheres", _wheres.Cast<object>().ToList() },
                { "orderBy", _orderBy }
            };
            object result = await AppFirebaseWebGLBridge.CollectionGet(_collectionPath, querySpec);
            return AppFirestoreWebGLConvert.ParseQuerySnapshot(result);
        }
#endif
    }

    public sealed class AppDocumentReference
    {
        internal string Path { get; }
        public string Id => Path.Substring(Path.LastIndexOf('/') + 1);

        internal AppDocumentReference(string path) { Path = path; }

#if !UNITY_WEBGL
        public async Task<AppDocumentSnapshot> GetSnapshotAsync()
        {
            DocumentSnapshot snap = await FirebaseFirestore.DefaultInstance.Document(Path).GetSnapshotAsync();
            return AppFirestoreNativeConvert.FromNative(snap);
        }

        public Task SetAsync(Dictionary<string, object> data) =>
            FirebaseFirestore.DefaultInstance.Document(Path).SetAsync(AppFirestoreNativeConvert.ToNative(data));

        public Task UpdateAsync(Dictionary<string, object> data) =>
            FirebaseFirestore.DefaultInstance.Document(Path).UpdateAsync(AppFirestoreNativeConvert.ToNative(data));

        public Task UpdateAsync(string field, object value) =>
            FirebaseFirestore.DefaultInstance.Document(Path).UpdateAsync(field, AppFirestoreNativeConvert.ScalarToNative(value));

        public Task DeleteAsync() =>
            FirebaseFirestore.DefaultInstance.Document(Path).DeleteAsync();

        public IDisposable Listen(Action<AppDocumentSnapshot> callback) =>
            FirebaseFirestore.DefaultInstance.Document(Path).Listen(snap => callback(AppFirestoreNativeConvert.FromNative(snap)));
#else
        public async Task<AppDocumentSnapshot> GetSnapshotAsync()
        {
            object result = await AppFirebaseWebGLBridge.DocGet(Path);
            return AppFirestoreWebGLConvert.ParseDocumentSnapshot(Id, result);
        }

        public Task SetAsync(Dictionary<string, object> data) => AppFirebaseWebGLBridge.DocSet(Path, data);

        public Task UpdateAsync(Dictionary<string, object> data) => AppFirebaseWebGLBridge.DocUpdate(Path, data);

        public Task UpdateAsync(string field, object value) =>
            AppFirebaseWebGLBridge.DocUpdate(Path, new Dictionary<string, object> { { field, value } });

        public Task DeleteAsync() => AppFirebaseWebGLBridge.DocDelete(Path);

        public IDisposable Listen(Action<AppDocumentSnapshot> callback) =>
            AppFirebaseWebGLBridge.ListenDocument(Path, raw => callback(AppFirestoreWebGLConvert.ParseDocumentSnapshot(Id, raw)));
#endif
    }

#if !UNITY_WEBGL
    /// <summary>Converts between AppFirestore's plain-object shape and the native Firebase.Firestore SDK types.</summary>
    internal static class AppFirestoreNativeConvert
    {
        public static object ScalarToNative(object value)
        {
            if (value is AppTimestamp ts) return Timestamp.FromDateTime(ts.UtcDateTime);
            if (AppFieldValue.IsDelete(value)) return FieldValue.Delete;
            return value;
        }

        public static Dictionary<string, object> ToNative(Dictionary<string, object> data)
        {
            var result = new Dictionary<string, object>();
            foreach (var kvp in data) result[kvp.Key] = ScalarToNative(kvp.Value);
            return result;
        }

        public static AppDocumentSnapshot FromNative(DocumentSnapshot snap)
        {
            Dictionary<string, object> data = null;
            if (snap.Exists)
            {
                data = new Dictionary<string, object>();
                foreach (var kvp in snap.ToDictionary())
                {
                    data[kvp.Key] = kvp.Value is Timestamp t ? (object)new AppTimestamp(t.ToDateTime()) : kvp.Value;
                }
            }
            return new AppDocumentSnapshot(snap.Id, snap.Exists, data);
        }
    }
#else
    /// <summary>Parses the plain JSON-derived payloads that come back from the JS bridge into AppFirestore types.</summary>
    internal static class AppFirestoreWebGLConvert
    {
        public static AppDocumentSnapshot ParseDocumentSnapshot(string id, object raw)
        {
            var dict = AppMiniJson.AsDict(raw);
            bool exists = dict.TryGetValue("exists", out var e) && (bool)e;
            var data = exists ? AppMiniJson.AsDict(dict.TryGetValue("data", out var d) ? d : null) : null;
            string actualId = dict.TryGetValue("id", out var idVal) && idVal is string s ? s : id;
            return new AppDocumentSnapshot(actualId, exists, data);
        }

        public static AppQuerySnapshot ParseQuerySnapshot(object raw)
        {
            var dict = AppMiniJson.AsDict(raw);
            var docsRaw = AppMiniJson.AsList(dict.TryGetValue("documents", out var docs) ? docs : null);
            var snapshots = docsRaw.Select(item =>
            {
                var docDict = AppMiniJson.AsDict(item);
                string id = docDict.TryGetValue("id", out var idVal) ? idVal as string : null;
                var data = AppMiniJson.AsDict(docDict.TryGetValue("data", out var d) ? d : null);
                return new AppDocumentSnapshot(id, true, data);
            });
            return new AppQuerySnapshot(snapshots);
        }
    }
#endif
}
