using System;
using System.Threading.Tasks;

#if !UNITY_WEBGL
using Firebase.Auth;
#endif

namespace AppFirebase
{
    /// <summary>
    /// Platform-neutral Auth facade. On every platform except WebGL this is a thin pass-through
    /// to the native Firebase Auth SDK (Firebase.Auth.FirebaseAuth.DefaultInstance). On WebGL it
    /// talks to the Firebase JS SDK through AppFirebaseWebGLBridge, because the native Firebase
    /// Unity SDK ships no WebGL support at all.
    ///
    /// Deliberately instance-shaped like the native FirebaseAuth (DefaultInstance singleton,
    /// instance methods/properties/event) so call sites that used to hold
    /// `private FirebaseAuth auth; auth = FirebaseAuth.DefaultInstance;` only need the type
    /// name changed to `AppAuth` - every `auth.Xyz(...)` call below still reads the same.
    /// </summary>
    public sealed class AppAuth
    {
        public static AppAuth DefaultInstance { get; } = new AppAuth();
        private AppAuth() { }

        /// <summary>Mirrors Firebase.Auth.FirebaseAuth.StateChanged - fires on sign-in, sign-out, and session restore.</summary>
        public event EventHandler StateChanged;

#if !UNITY_WEBGL
        private static FirebaseAuth Native => FirebaseAuth.DefaultInstance;
        private bool _nativeHooked;

        private void EnsureHook()
        {
            if (_nativeHooked) return;
            _nativeHooked = true;
            Native.StateChanged += (sender, args) => StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public AppUser CurrentUser
        {
            get
            {
                EnsureHook();
                var user = Native.CurrentUser;
                return user == null ? null : new AppUser(user.UserId, user.Email);
            }
        }

        public async Task<AppAuthResult> SignInWithEmailAndPasswordAsync(string email, string password)
        {
            EnsureHook();
            var result = await Native.SignInWithEmailAndPasswordAsync(email, password);
            return new AppAuthResult(new AppUser(result.User.UserId, result.User.Email));
        }

        public async Task<AppAuthResult> CreateUserWithEmailAndPasswordAsync(string email, string password)
        {
            EnsureHook();
            var result = await Native.CreateUserWithEmailAndPasswordAsync(email, password);
            return new AppAuthResult(new AppUser(result.User.UserId, result.User.Email));
        }

        public Task SendPasswordResetEmailAsync(string email)
        {
            EnsureHook();
            return Native.SendPasswordResetEmailAsync(email);
        }

        public void SignOut()
        {
            EnsureHook();
            Native.SignOut();
        }
#else
        private bool _webglHooked;

        private void EnsureHook()
        {
            if (_webglHooked) return;
            _webglHooked = true;
            AppFirebaseWebGLBridge.AuthStateChangedRaw += _ => StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public AppUser CurrentUser
        {
            get
            {
                EnsureHook();
                var raw = AppFirebaseWebGLBridge.GetCurrentUserRaw();
                if (raw == null) return null;
                return new AppUser(raw["uid"] as string, raw.TryGetValue("email", out var e) ? e as string : null);
            }
        }

        public async Task<AppAuthResult> SignInWithEmailAndPasswordAsync(string email, string password)
        {
            EnsureHook();
            object result = await AppFirebaseWebGLBridge.SignIn(email, password);
            return new AppAuthResult(ParseUser(result));
        }

        public async Task<AppAuthResult> CreateUserWithEmailAndPasswordAsync(string email, string password)
        {
            EnsureHook();
            object result = await AppFirebaseWebGLBridge.SignUp(email, password);
            return new AppAuthResult(ParseUser(result));
        }

        public async Task SendPasswordResetEmailAsync(string email)
        {
            EnsureHook();
            await AppFirebaseWebGLBridge.SendPasswordReset(email);
        }

        public void SignOut()
        {
            EnsureHook();
            AppFirebaseWebGLBridge.SignOut();
        }

        private static AppUser ParseUser(object raw)
        {
            var dict = AppMiniJson.AsDict(raw);
            return new AppUser(dict.TryGetValue("uid", out var uid) ? uid as string : null,
                                dict.TryGetValue("email", out var email) ? email as string : null);
        }
#endif
    }
}
