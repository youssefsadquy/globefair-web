#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// Small WebGL-only helper to trigger a browser file download (see
/// Assets/Plugins/WebGL/AppBrowserDownload.jslib). Used in place of code that used to write a
/// file with System.IO.File and open it with System.Diagnostics.Process.Start - neither of
/// those works in a WebGL build (there is no real OS filesystem to write to -
/// Application.persistentDataPath there is a sandboxed virtual store the user can't reach -
/// and no process to launch a viewer with), so anything relying on that pattern (e.g. the
/// "export analytics report" flow) was silently broken in the web build.
/// </summary>
public static class AppBrowserDownload
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void AppBrowser_DownloadTextFile(string filename, string content);
    [DllImport("__Internal")] private static extern void AppBrowser_DownloadBytesFile(string filename, byte[] bytes, int length, string mimeType);
#endif

    /// <summary>Triggers a normal browser "Save As" download of the given text content.
    /// No-op outside a built-and-served WebGL player (nothing to do on other platforms -
    /// callers there should keep using System.IO.File directly).</summary>
    public static void DownloadTextFile(string filename, string content)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AppBrowser_DownloadTextFile(filename, content);
#else
        Debug.LogWarning("AppBrowserDownload.DownloadTextFile called outside a built WebGL player - no-op.");
#endif
    }

    /// <summary>Triggers a normal browser "Save As" download of the given binary content
    /// (PDFs, images, etc). No-op outside a built-and-served WebGL player (nothing to do on
    /// other platforms - callers there should keep using System.IO.File directly).</summary>
    public static void DownloadBytesFile(string filename, byte[] bytes, string mimeType = "application/octet-stream")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        AppBrowser_DownloadBytesFile(filename, bytes, bytes.Length, mimeType);
#else
        Debug.LogWarning("AppBrowserDownload.DownloadBytesFile called outside a built WebGL player - no-op.");
#endif
    }
}
