using System;
using System.Threading.Tasks;

#if !UNITY_WEBGL
using Firebase.Storage;
#endif

namespace AppFirebase
{
    /// <summary>
    /// Platform-neutral Storage facade (native Firebase Storage SDK everywhere except WebGL,
    /// Firebase JS SDK storage via AppFirebaseWebGLBridge on WebGL).
    ///
    /// Everything is addressed as a plain object path ("user_logos/uid/file.png", "cvs/uid.pdf")
    /// - GetReferenceFromUrl(gs://bucket/...) just strips the "gs://bucket/" prefix down to that
    /// same plain path, so both call styles used across the project end up on one code path.
    /// </summary>
    public sealed class AppStorageReference
    {
        public string Path { get; }

        internal AppStorageReference(string path) { Path = path; }

        public AppStorageReference Child(string relativePath)
        {
            string combined = Path.TrimEnd('/') + "/" + relativePath.TrimStart('/');
            return new AppStorageReference(combined);
        }

#if !UNITY_WEBGL
        public async Task PutBytesAsync(byte[] bytes)
        {
            var storageRef = FirebaseStorage.DefaultInstance.GetReference(Path);
            await storageRef.PutBytesAsync(bytes);
        }

        public async Task<string> GetDownloadUrlAsync()
        {
            var storageRef = FirebaseStorage.DefaultInstance.GetReference(Path);
            var uri = await storageRef.GetDownloadUrlAsync();
            return uri.ToString();
        }

        public async Task<byte[]> GetBytesAsync(long maxDownloadSizeBytes)
        {
            var storageRef = FirebaseStorage.DefaultInstance.GetReference(Path);
            return await storageRef.GetBytesAsync(maxDownloadSizeBytes);
        }

        public async Task DeleteAsync()
        {
            var storageRef = FirebaseStorage.DefaultInstance.GetReference(Path);
            await storageRef.DeleteAsync();
        }
#else
        public async Task PutBytesAsync(byte[] bytes)
        {
            await AppFirebaseWebGLBridge.StorageUploadBytes(Path, bytes);
        }

        public async Task<string> GetDownloadUrlAsync()
        {
            object result = await AppFirebaseWebGLBridge.StorageGetDownloadUrl(Path);
            var dict = AppMiniJson.AsDict(result);
            return dict.TryGetValue("url", out var url) ? url as string : null;
        }

        /// <summary>
        /// The Firebase JS SDK bridge only exposes getDownloadURL (no raw-bytes call), so on
        /// WebGL this resolves the download URL and then fetches the bytes over plain HTTP
        /// with UnityWebRequest - the same approach Booth.cs already uses for downloading
        /// logo textures from Firebase Storage URLs.
        /// </summary>
        public async Task<byte[]> GetBytesAsync(long maxDownloadSizeBytes)
        {
            string url = await GetDownloadUrlAsync();
            var tcs = new TaskCompletionSource<byte[]>();
            var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    tcs.SetException(new Exception(request.error));
                }
                else
                {
                    tcs.SetResult(request.downloadHandler.data);
                }
                request.Dispose();
            };
            return await tcs.Task;
        }

        public async Task DeleteAsync()
        {
            await AppFirebaseWebGLBridge.StorageDelete(Path);
        }
#endif
    }

    /// <summary>
    /// Instance-shaped like the native FirebaseStorage (DefaultInstance singleton) so call
    /// sites that used to hold `private FirebaseStorage storage; storage = FirebaseStorage.DefaultInstance;`
    /// only need the type name changed to `AppStorage`.
    /// </summary>
    public sealed class AppStorage
    {
        public static AppStorage DefaultInstance { get; } = new AppStorage();
        private AppStorage() { }

        public AppStorageReference GetReference(string path) => new AppStorageReference(path.TrimStart('/'));

        /// <summary>Accepts "gs://bucket/some/path" (bucket portion is ignored - there is only one bucket) or a bare path.</summary>
        public AppStorageReference GetReferenceFromUrl(string gsUrl)
        {
            const string prefix = "gs://";
            if (!gsUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new AppStorageReference(gsUrl.TrimStart('/'));
            }

            string withoutScheme = gsUrl.Substring(prefix.Length); // "bucket/some/path" or just "bucket"
            int firstSlash = withoutScheme.IndexOf('/');
            string path = firstSlash < 0 ? string.Empty : withoutScheme.Substring(firstSlash + 1);
            return new AppStorageReference(path);
        }
    }
}
