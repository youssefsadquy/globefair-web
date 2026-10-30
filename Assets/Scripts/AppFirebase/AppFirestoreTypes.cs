using System;
using System.Collections.Generic;
using System.Linq;

namespace AppFirebase
{
    /// <summary>
    /// Sentinel values for field updates, mirroring Firebase.Firestore.FieldValue.
    /// Only `Delete` is used anywhere in this project.
    /// </summary>
    public static class AppFieldValue
    {
        /// <summary>Marker instance: put this as a field's value in an UpdateAsync dictionary to remove that field.</summary>
        public static readonly object Delete = new DeleteSentinel();

        public sealed class DeleteSentinel { public override string ToString() => "__delete__"; }

        public static bool IsDelete(object value) => value is DeleteSentinel;
    }

    /// <summary>Platform-neutral stand-in for Firebase.Firestore.DocumentSnapshot.</summary>
    public sealed class AppDocumentSnapshot
    {
        public string Id { get; }
        public bool Exists { get; }
        private readonly Dictionary<string, object> _data;

        public AppDocumentSnapshot(string id, bool exists, Dictionary<string, object> data)
        {
            Id = id;
            Exists = exists;
            _data = data ?? new Dictionary<string, object>();
        }

        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_data);

        public bool ContainsField(string field) => _data.ContainsKey(field);

        public T GetValue<T>(string field)
        {
            TryGetValue<T>(field, out T value);
            return value;
        }

        public bool TryGetValue<T>(string field, out T value)
        {
            if (_data.TryGetValue(field, out object raw) && raw != null)
            {
                if (TryConvert(raw, out value)) return true;
            }
            value = default;
            return false;
        }

        private static bool TryConvert<T>(object raw, out T value)
        {
            try
            {
                if (raw is T typed)
                {
                    value = typed;
                    return true;
                }

                Type targetType = typeof(T);

                if (targetType == typeof(AppTimestamp))
                {
                    if (raw is AppTimestamp ts) { value = (T)(object)ts; return true; }
                    if (raw is double ms) { value = (T)(object)AppTimestamp.FromUnixMilliseconds(ms); return true; }
                    if (raw is long msl) { value = (T)(object)AppTimestamp.FromUnixMilliseconds(msl); return true; }
                    if (raw is DateTime dt) { value = (T)(object)new AppTimestamp(dt); return true; }
                }
                else if (targetType == typeof(long))
                {
                    value = (T)(object)Convert.ToInt64(raw);
                    return true;
                }
                else if (targetType == typeof(int))
                {
                    value = (T)(object)Convert.ToInt32(raw);
                    return true;
                }
                else if (targetType == typeof(double))
                {
                    value = (T)(object)Convert.ToDouble(raw);
                    return true;
                }
                else if (targetType == typeof(float))
                {
                    value = (T)(object)Convert.ToSingle(raw);
                    return true;
                }
                else if (targetType == typeof(bool))
                {
                    value = (T)(object)Convert.ToBoolean(raw);
                    return true;
                }
                else if (targetType == typeof(string))
                {
                    value = (T)(object)raw.ToString();
                    return true;
                }

                value = (T)Convert.ChangeType(raw, targetType);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    /// <summary>Platform-neutral stand-in for Firebase.Firestore.QuerySnapshot.</summary>
    public sealed class AppQuerySnapshot
    {
        public IReadOnlyList<AppDocumentSnapshot> Documents { get; }
        public int Count => Documents.Count;

        public AppQuerySnapshot(IEnumerable<AppDocumentSnapshot> documents)
        {
            Documents = documents?.ToList() ?? new List<AppDocumentSnapshot>();
        }
    }
}
