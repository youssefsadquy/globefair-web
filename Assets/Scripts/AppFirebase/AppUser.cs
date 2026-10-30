using System;

namespace AppFirebase
{
    /// <summary>
    /// Platform-neutral stand-in for Firebase.Auth.FirebaseUser.
    /// Value-equal on UserId so "auth.CurrentUser != currentUser" change-detection
    /// patterns used throughout the app keep working without needing the same
    /// underlying native/JS object instance.
    /// </summary>
    public sealed class AppUser : IEquatable<AppUser>
    {
        public string UserId { get; }
        public string Email { get; }

        public AppUser(string userId, string email)
        {
            UserId = userId;
            Email = email;
        }

        public bool Equals(AppUser other)
        {
            if (ReferenceEquals(other, null)) return false;
            if (ReferenceEquals(this, other)) return true;
            return UserId == other.UserId;
        }

        public override bool Equals(object obj) => Equals(obj as AppUser);

        public override int GetHashCode() => UserId != null ? UserId.GetHashCode() : 0;

        public static bool operator ==(AppUser a, AppUser b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) return true;
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
            return a.Equals(b);
        }

        public static bool operator !=(AppUser a, AppUser b) => !(a == b);
    }

    /// <summary>Result of a sign-in/sign-up call - mirrors Firebase's AuthResult.User shape.</summary>
    public sealed class AppAuthResult
    {
        public AppUser User { get; }
        public AppAuthResult(AppUser user) { User = user; }
    }
}
