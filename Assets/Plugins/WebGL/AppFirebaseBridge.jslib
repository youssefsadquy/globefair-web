// WebGL-only bridge between AppFirebaseWebGLBridge.cs (C#) and the Firebase JS SDK
// (loaded by the WebGL template's index.html, see Assets/WebGLTemplates/GlobeFair/index.html).
//
// Every one-shot call reports back via:
//   window.unityInstance.SendMessage('AppFirebaseBridgeReceiver', 'OnJsResult', jsonEnvelope)
// Live document listeners report back via:
//   window.unityInstance.SendMessage('AppFirebaseBridgeReceiver', 'OnJsListenerEvent', jsonEnvelope)
// Auth state changes report back via:
//   window.unityInstance.SendMessage('AppFirebaseBridgeReceiver', 'OnAuthStateChanged', jsonUserOrNull)
//
// window.unityInstance is assigned by the WebGL template right after createUnityInstance()
// resolves - that's the standard way a .jslib gets a handle to call SendMessage with the
// modern Unity WebGL loader (there is no bare global SendMessage function any more).
//
// IMPORTANT #1: every shared helper below lives *inside* mergeInto() using Unity's "$name"
// shared-symbol convention, and every library function that calls one declares it in
// __deps. Helpers previously declared as plain top-level functions outside mergeInto()
// were silently dropped by Unity's WebGL linker at build time (they are not guaranteed
// to be kept unless the linker can see they're part of the library), which caused
// "ReferenceError: AppFirebaseSendMessage/AppFirebaseSendResult is not defined" at
// runtime - every bridge call (sign-in, sign-up, auth-state-changed, etc.) threw before
// it could report back to C#, so the corresponding TaskCompletionSource on the Unity
// side never resolved and the UI just hung with no visible error.
//
// IMPORTANT #2: a C# `string` argument on an `extern` function crosses into a .jslib
// function as a raw pointer (a number) into the WASM heap - NOT as a real JS string.
// Unity does not auto-decode it for you; every function below must call
// UTF8ToString(ptr) itself before using a string argument. Every function here that
// takes one or more C# strings previously used the raw pointer directly (e.g.
// `firebase.auth().signInWithEmailAndPassword(email, password)` where `email`/`password`
// were still pointers), which sent Firebase/Firestore/Storage the pointer's numeric value
// coerced to a string (e.g. "90469184") instead of the actual text. Firebase's own
// client-side validation then rejected it (surfacing as "auth/invalid-email" for sign-in/
// sign-up, or an equally wrong path/data for Firestore/Storage calls) - the ReferenceError
// fix above was necessary but not sufficient; every call still failed after it, silently
// from the player's point of view. Every string parameter is now decoded with
// UTF8ToString(...) as the first thing each function does.

mergeInto(LibraryManager.library, {

    $AppFirebaseActiveListeners: {},

    $AppFirebaseSendMessage: function (gameObject, method, payload) {
        if (window.unityInstance) {
            window.unityInstance.SendMessage(gameObject, method, payload);
        } else {
            console.error('AppFirebaseBridge: window.unityInstance is not set yet - cannot deliver ' + method);
        }
    },

    $AppFirebaseSendResult__deps: ['$AppFirebaseSendMessage'],
    $AppFirebaseSendResult: function (requestId, success, payloadOrErrorMessage) {
        var envelope = { requestId: requestId, success: success };
        if (success) {
            envelope.payload = payloadOrErrorMessage;
        } else {
            envelope.error = (payloadOrErrorMessage && payloadOrErrorMessage.message) ? payloadOrErrorMessage.message : String(payloadOrErrorMessage);
        }
        AppFirebaseSendMessage('AppFirebaseBridgeReceiver', 'OnJsResult', JSON.stringify(envelope));
    },

    // Recursively converts our {"__ts__": millis} marker objects and the "__delete__" string
    // sentinel (produced by AppMiniJson.Serialize on the C# side) into real Firestore values.
    $AppFirebaseFromJsonValue: function (value) {
        if (value === null || value === undefined) return value;
        if (value === '__delete__') return firebase.firestore.FieldValue.delete();
        if (Array.isArray(value)) return value.map(AppFirebaseFromJsonValue);
        if (typeof value === 'object') {
            if (Object.prototype.hasOwnProperty.call(value, '__ts__')) {
                return firebase.firestore.Timestamp.fromMillis(value.__ts__);
            }
            var out = {};
            for (var key in value) {
                if (Object.prototype.hasOwnProperty.call(value, key)) out[key] = AppFirebaseFromJsonValue(value[key]);
            }
            return out;
        }
        return value;
    },

    // Recursively converts real Firestore values (Timestamp instances in particular) back into
    // our plain-JSON shape so AppMiniJson on the C# side can parse it.
    $AppFirebaseToJsonValue: function (value) {
        if (value === null || value === undefined) return null;
        if (value instanceof firebase.firestore.Timestamp) {
            return { __ts__: value.toMillis() };
        }
        if (Array.isArray(value)) return value.map(AppFirebaseToJsonValue);
        if (typeof value === 'object') {
            var out = {};
            for (var key in value) {
                if (Object.prototype.hasOwnProperty.call(value, key)) out[key] = AppFirebaseToJsonValue(value[key]);
            }
            return out;
        }
        return value;
    },

    $AppFirebaseUserJson: function (user) {
        return user ? JSON.stringify({ uid: user.uid, email: user.email }) : 'null';
    },

    // ---- Auth ---------------------------------------------------------------------------

    AppFirebase_SignIn__deps: ['$AppFirebaseSendResult'],
    AppFirebase_SignIn: function (requestId, email, password) {
        email = UTF8ToString(email);
        password = UTF8ToString(password);
        firebase.auth().signInWithEmailAndPassword(email, password).then(function (cred) {
            AppFirebaseSendResult(requestId, true, { uid: cred.user.uid, email: cred.user.email });
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_SignUp__deps: ['$AppFirebaseSendResult'],
    AppFirebase_SignUp: function (requestId, email, password) {
        email = UTF8ToString(email);
        password = UTF8ToString(password);
        firebase.auth().createUserWithEmailAndPassword(email, password).then(function (cred) {
            AppFirebaseSendResult(requestId, true, { uid: cred.user.uid, email: cred.user.email });
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_SendPasswordReset__deps: ['$AppFirebaseSendResult'],
    AppFirebase_SendPasswordReset: function (requestId, email) {
        email = UTF8ToString(email);
        firebase.auth().sendPasswordResetEmail(email).then(function () {
            AppFirebaseSendResult(requestId, true, {});
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_SignOut: function () {
        firebase.auth().signOut();
    },

    AppFirebase_GetCurrentUser__deps: ['$AppFirebaseUserJson'],
    AppFirebase_GetCurrentUser: function () {
        var json = AppFirebaseUserJson(firebase.auth().currentUser);
        var bufferSize = lengthBytesUTF8(json) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(json, buffer, bufferSize);
        return buffer;
    },

    AppFirebase_RegisterAuthListener__deps: ['$AppFirebaseSendMessage', '$AppFirebaseUserJson'],
    AppFirebase_RegisterAuthListener: function () {
        firebase.auth().onAuthStateChanged(function (user) {
            AppFirebaseSendMessage('AppFirebaseBridgeReceiver', 'OnAuthStateChanged', AppFirebaseUserJson(user));
        });
    },

    // ---- Firestore ------------------------------------------------------------------------

    AppFirebase_DocGet__deps: ['$AppFirebaseSendResult', '$AppFirebaseToJsonValue'],
    AppFirebase_DocGet: function (requestId, path) {
        path = UTF8ToString(path);
        firebase.firestore().doc(path).get().then(function (doc) {
            AppFirebaseSendResult(requestId, true, {
                exists: doc.exists,
                id: doc.id,
                data: doc.exists ? AppFirebaseToJsonValue(doc.data()) : null
            });
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_DocSet__deps: ['$AppFirebaseSendResult', '$AppFirebaseFromJsonValue'],
    AppFirebase_DocSet: function (requestId, path, dataJson) {
        path = UTF8ToString(path);
        dataJson = UTF8ToString(dataJson);
        var data = AppFirebaseFromJsonValue(JSON.parse(dataJson));
        firebase.firestore().doc(path).set(data).then(function () {
            AppFirebaseSendResult(requestId, true, {});
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_DocUpdate__deps: ['$AppFirebaseSendResult', '$AppFirebaseFromJsonValue'],
    AppFirebase_DocUpdate: function (requestId, path, dataJson) {
        path = UTF8ToString(path);
        dataJson = UTF8ToString(dataJson);
        var data = AppFirebaseFromJsonValue(JSON.parse(dataJson));
        firebase.firestore().doc(path).update(data).then(function () {
            AppFirebaseSendResult(requestId, true, {});
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_DocDelete__deps: ['$AppFirebaseSendResult'],
    AppFirebase_DocDelete: function (requestId, path) {
        path = UTF8ToString(path);
        firebase.firestore().doc(path).delete().then(function () {
            AppFirebaseSendResult(requestId, true, {});
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_DocAdd__deps: ['$AppFirebaseSendResult', '$AppFirebaseFromJsonValue'],
    AppFirebase_DocAdd: function (requestId, collectionPath, dataJson) {
        collectionPath = UTF8ToString(collectionPath);
        dataJson = UTF8ToString(dataJson);
        var data = AppFirebaseFromJsonValue(JSON.parse(dataJson));
        firebase.firestore().collection(collectionPath).add(data).then(function (docRef) {
            AppFirebaseSendResult(requestId, true, { id: docRef.id });
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_CollectionGet__deps: ['$AppFirebaseSendResult', '$AppFirebaseFromJsonValue', '$AppFirebaseToJsonValue'],
    AppFirebase_CollectionGet: function (requestId, collectionPath, queryJson) {
        collectionPath = UTF8ToString(collectionPath);
        queryJson = UTF8ToString(queryJson);
        try {
            var spec = JSON.parse(queryJson);
            var ref = firebase.firestore().collection(collectionPath);
            (spec.wheres || []).forEach(function (w) {
                ref = ref.where(w.field, w.op, AppFirebaseFromJsonValue(w.value));
            });
            if (spec.orderBy) {
                ref = ref.orderBy(spec.orderBy);
            }
            ref.get().then(function (snapshot) {
                var documents = snapshot.docs.map(function (d) {
                    return { id: d.id, data: AppFirebaseToJsonValue(d.data()) };
                });
                AppFirebaseSendResult(requestId, true, { documents: documents });
            }).catch(function (err) {
                AppFirebaseSendResult(requestId, false, err);
            });
        } catch (e) {
            AppFirebaseSendResult(requestId, false, e);
        }
    },

    AppFirebase_DocListenStart__deps: ['$AppFirebaseSendMessage', '$AppFirebaseToJsonValue', '$AppFirebaseActiveListeners'],
    AppFirebase_DocListenStart: function (listenerId, path) {
        path = UTF8ToString(path);
        var unsubscribe = firebase.firestore().doc(path).onSnapshot(function (doc) {
            var envelope = {
                listenerId: listenerId,
                payload: {
                    exists: doc.exists,
                    id: doc.id,
                    data: doc.exists ? AppFirebaseToJsonValue(doc.data()) : null
                }
            };
            AppFirebaseSendMessage('AppFirebaseBridgeReceiver', 'OnJsListenerEvent', JSON.stringify(envelope));
        }, function (err) {
            console.error('AppFirebase document listener error for ' + path + ':', err);
        });
        AppFirebaseActiveListeners[listenerId] = unsubscribe;
    },

    AppFirebase_DocListenStop__deps: ['$AppFirebaseActiveListeners'],
    AppFirebase_DocListenStop: function (listenerId) {
        var unsubscribe = AppFirebaseActiveListeners[listenerId];
        if (unsubscribe) {
            unsubscribe();
            delete AppFirebaseActiveListeners[listenerId];
        }
    },

    // ---- Storage --------------------------------------------------------------------------

    AppFirebase_StorageUploadBytes__deps: ['$AppFirebaseSendResult'],
    AppFirebase_StorageUploadBytes: function (requestId, path, bytesPtr, length) {
        path = UTF8ToString(path);
        try {
            // Copy out of the heap immediately - it can move/resize before the async upload finishes.
            var bytesCopy = HEAPU8.slice(bytesPtr, bytesPtr + length);
            var blob = new Blob([bytesCopy]);
            firebase.storage().ref(path).put(blob).then(function () {
                AppFirebaseSendResult(requestId, true, {});
            }).catch(function (err) {
                AppFirebaseSendResult(requestId, false, err);
            });
        } catch (e) {
            AppFirebaseSendResult(requestId, false, e);
        }
    },

    AppFirebase_StorageGetDownloadUrl__deps: ['$AppFirebaseSendResult'],
    AppFirebase_StorageGetDownloadUrl: function (requestId, path) {
        path = UTF8ToString(path);
        firebase.storage().ref(path).getDownloadURL().then(function (url) {
            AppFirebaseSendResult(requestId, true, { url: url });
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    },

    AppFirebase_StorageDelete__deps: ['$AppFirebaseSendResult'],
    AppFirebase_StorageDelete: function (requestId, path) {
        path = UTF8ToString(path);
        firebase.storage().ref(path).delete().then(function () {
            AppFirebaseSendResult(requestId, true, {});
        }).catch(function (err) {
            AppFirebaseSendResult(requestId, false, err);
        });
    }

});
