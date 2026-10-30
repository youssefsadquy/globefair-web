# GlobeFair (web)

A 3D virtual career fair built in Unity and published to the web with WebGL and Firebase.
Job seekers and businesses sign up, then meet in a shared exhibition hall: businesses take a booth, post listings and answer questions, candidates apply, and both sides can chat.

**Live demo:** <https://globefair-46eb2.web.app/>
Open it in a desktop browser. The first load downloads about 400 MB, so allow a minute. If it does not load, the hosting plan's daily bandwidth allowance may have run out for the day (see [Operating the demo](#operating-the-demo)).

This is the web evolution of my University of Leicester final-year project. The original desktop submission, with its 2023-24 history, is in [globefair](https://github.com/youssefsadquy/globefair).

## What it does

- **Two account types.** Job seekers and businesses each get sign-up, login, password recovery and their own dashboard (Firebase Authentication).
- **Booths.** Businesses choose from the booths that are still free, and the list refreshes as booths are taken or released (stored in Firestore).
- **Listings and applications.** Businesses create, edit and remove listings. Job seekers upload a PDF CV, apply, and track their applications.
- **Candidates.** Businesses see everyone who applied to their listings, with the listing and contact details.
- **Chat.** Direct messages between job seekers and businesses.
- **Analytics.** Businesses see total and unique visitors to their booth and how many people applied.
- **Q&A.** Businesses publish questions and answers for visitors.
- **Avatar customiser and tutorial** panels, **language selection** (English, Spanish, French, Italian) and graphics quality and fullscreen options.

## How it is built

**Unity 2021.3 LTS, C#, Firebase (Auth, Firestore, Storage), WebGL, Firebase Hosting.**

The interesting engineering is the Firebase layer. Unity's official Firebase SDK does not run in WebGL, so the app talks to Firebase through its own platform-neutral facade:

```
UI scripts (dashboards, chat, booths, ...)
        |
  AppAuth / AppFirestore / AppStorage          one API for every platform
        |                         |
 native Firebase SDK        AppFirebaseWebGLBridge.cs
 (Editor, desktop, mobile)         |
                            AppFirebaseBridge.jslib  ->  Firebase JavaScript SDK
                            (WebGL, loaded by the page template)
```

- `Assets/Scripts/AppFirebase/` holds the facade (about 1,400 lines): tasks, document and collection reads, writes and real-time listeners, uploads, and a small JSON reader and writer (`AppMiniJson`) so documents with arbitrary fields can cross the C# to JavaScript boundary.
- `Assets/Plugins/WebGL/AppFirebaseBridge.jslib` exposes 17 functions: sign-in, sign-up, password reset, sign-out, auth state, document get, set, update, delete and add, collection get, document listeners, and storage upload, download URL and delete.
- `Assets/WebGLTemplates/GlobeFair/index.html` is a custom page template that loads the Firebase JavaScript SDK and shows the loading screen.
- `Assets/Editor/OptimizeForWebGL.cs` applies WebGL texture import overrides. Before those, the build's data archive was about 1.4 GB and scenes failed to load in the browser; the deployed build is now about 400 MB.

Data lives in Firestore collections `users`, `bus_users` (businesses), `booths`, `businessListings`, `candidates`, `businessQA` and `feedback`, and CVs and logos in Storage.

About 7,000 lines of C# and 360 lines of JavaScript.

## What is in this repository

Included: all scripts, the WebGL plugin and page template, the Landing and avatar-customiser scenes, UI images, the stand thumbnails, tutorial animations, editor tools and the Unity package list.

Not included, so the project will not open complete from this repository alone:

- The main `GlobeFair` scene (54 MB) and the exhibition models, which come from third-party asset packs, as do UMA, Starter Assets, TextMesh Pro and the audio.
- Anything secret: Firebase configuration and API keys are not committed (see below).

## Running your own copy

1. Install Unity 2021.3.26f1 and open the project (it reads `Packages/manifest.json`).
2. Create a Firebase project with Authentication (email and password), Firestore and Storage, and register a Web app.
3. Copy `Assets/WebGLTemplates/GlobeFair/firebase-config.example.js` to `firebase-config.js` in the same folder (it is git-ignored) and fill in your values.
4. Search for `globefair-46eb2` and replace it with your project ID (storage bucket in `UserDashboardManager.cs`, hosting site in `firebase.json`).
5. Build for WebGL with the `GlobeFair` template, then `firebase deploy --only hosting`.

## Operating the demo

A Firebase web API key is public by design: the browser needs it, so anyone can read it from the deployed page. What protects a public demo is not hiding that key, but these settings in the Firebase and Google Cloud consoles:

- **Restrict the API key** to your hosting domains (HTTP referrers) and to the Firebase APIs the app uses.
- **Turn on App Check** (reCAPTCHA) for Firestore and Storage so only your deployed site can use them.
- **Write strict security rules**: users read and write only their own data, businesses only their own booth, listings and Q&A, and CV uploads are limited by size and type.
- **Watch the free-plan limits.** Firebase Hosting's free plan allows about 360 MB of transfer per day, and one first load of this build is about 400 MB, so a single visitor can use the whole day's allowance. Firestore's free plan allows 50,000 reads and 20,000 writes per day. Set a budget alert, consider the pay-as-you-go plan with a low cap, or host the build files on a service with free outbound traffic.
- **Cut the download size** with texture compression and smaller scenes, or load the fair on demand.

## Timeline

The web version was developed over about four weeks in August 2026 and deployed to Firebase Hosting in September 2026. This repository was published in October 2026: its commits group the existing code by feature and are not a record of when each part was written.

## Licence

MIT for the original code in this repository (see `LICENSE`). Third-party packages and assets are not included and keep their own licences.
