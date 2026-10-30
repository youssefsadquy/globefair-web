// WebGL-only helper to trigger a normal browser file download. Used where code previously
// wrote a file with System.IO.File and opened it with System.Diagnostics.Process.Start -
// neither works in a WebGL build: Application.persistentDataPath there is a sandboxed
// virtual store the user has no access to, and System.Diagnostics.Process cannot launch
// anything from inside a browser sandbox at all.
//
// Same convention as Assets/Plugins/WebGL/AppFirebaseBridge.jslib: everything lives inside
// mergeInto(LibraryManager.library, {...}) so the WebGL linker is guaranteed to keep it -
// a helper declared as a plain top-level function outside that block is not guaranteed to
// be kept in the actual build output (see AppFirebaseBridge.jslib's header comment for the
// bug that caused, and how "$name" shared symbols avoid it).

mergeInto(LibraryManager.library, {

    AppBrowser_DownloadTextFile: function (filenamePtr, contentPtr) {
        var filename = UTF8ToString(filenamePtr);
        var content = UTF8ToString(contentPtr);
        var blob = new Blob([content], { type: 'text/plain' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    },

    // Binary-file variant (PDFs, images, etc.) - e.g. used for downloading a candidate's CV,
    // which previously used File.WriteAllBytes + Process.Start (broken on WebGL, see above).
    AppBrowser_DownloadBytesFile: function (filenamePtr, bytesPtr, length, mimeTypePtr) {
        var filename = UTF8ToString(filenamePtr);
        var mimeType = UTF8ToString(mimeTypePtr) || 'application/octet-stream';
        // Copy out of the heap immediately - it can move/resize before the Blob is built.
        var bytesCopy = HEAPU8.slice(bytesPtr, bytesPtr + length);
        var blob = new Blob([bytesCopy], { type: mimeType });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    }

});
