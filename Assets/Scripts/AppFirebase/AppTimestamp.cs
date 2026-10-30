using System;

namespace AppFirebase
{
    /// <summary>
    /// Platform-neutral stand-in for Firebase.Firestore.Timestamp.
    /// Backed by UTC DateTime so it round-trips cleanly through both the native
    /// Firestore SDK (Timestamp) and the Firestore JS SDK (JS Date / millis-since-epoch).
    /// </summary>
    public struct AppTimestamp : IComparable<AppTimestamp>, IEquatable<AppTimestamp>
    {
        public DateTime UtcDateTime { get; }

        public AppTimestamp(DateTime utcDateTime)
        {
            UtcDateTime = utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : utcDateTime.ToUniversalTime();
        }

        public static AppTimestamp GetCurrentTimestamp() => new AppTimestamp(DateTime.UtcNow);

        public static AppTimestamp FromUnixMilliseconds(double millis) =>
            new AppTimestamp(DateTimeOffset.FromUnixTimeMilliseconds((long)millis).UtcDateTime);

        public double ToUnixMilliseconds() =>
            new DateTimeOffset(UtcDateTime).ToUnixTimeMilliseconds();

        public int CompareTo(AppTimestamp other) => UtcDateTime.CompareTo(other.UtcDateTime);

        public bool Equals(AppTimestamp other) => UtcDateTime == other.UtcDateTime;

        public override bool Equals(object obj) => obj is AppTimestamp other && Equals(other);

        public override int GetHashCode() => UtcDateTime.GetHashCode();

        public static bool operator ==(AppTimestamp a, AppTimestamp b) => a.Equals(b);
        public static bool operator !=(AppTimestamp a, AppTimestamp b) => !a.Equals(b);
        public static bool operator <(AppTimestamp a, AppTimestamp b) => a.CompareTo(b) < 0;
        public static bool operator >(AppTimestamp a, AppTimestamp b) => a.CompareTo(b) > 0;

        public override string ToString() => UtcDateTime.ToString("u");
    }
}
