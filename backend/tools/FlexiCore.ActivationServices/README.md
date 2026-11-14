# FlexiCore Activation Services

This `net10.0` library contains the recovered Activation Engine services and viewmodels. It reuses the existing
`FlexiCore.Licensing` verifier and the `FlexiCore.LicenseTool.Core` issuer/key/ledger implementation. The
owner-only command-line app is a thin shell over that core library. The WPF application remains in
`FlexiCore.ActivationEngine`; only its Windows dialog adapter stays in that project. Owner workflows are selected
explicitly with `ActivationEngineOptions`, rather than inferred from a WPF implementation type.

## Development checks

Run from the repository root with the required .NET SDK from `global.json`:

```sh
dotnet test backend/tests/FlexiCore.ActivationEngine.Tests/FlexiCore.ActivationEngine.Tests.csproj
dotnet build backend/tools/FlexiCore.ActivationEngine/FlexiCore.ActivationEngine.csproj
dotnet build backend/tools/FlexiCore.ActivationApp/FlexiCore.ActivationApp.csproj
dotnet publish backend/tools/FlexiCore.ActivationAndroid/FlexiCore.ActivationAndroid.csproj -c Debug
```

The original Activation Engine tests target `net10.0` and exercise the real services and viewmodels without
requiring Windows. The WPF reference app still targets `net10.0-windows`; build it on Windows, or set
`EnableWindowsTargeting=true` on a host with the Windows desktop targeting pack. Executing it still requires
Windows and the Windows Desktop runtime.

## Current security and migration boundaries

- The existing license envelope, ES256/P-256 signing, self-check, independent verification, protected PKCS#8 key
  files, duplicate-ID checks, ledger format, and non-overwriting file operations remain owned by the existing
  licensing/tool projects.
- The portable services do not introduce a signing format or a private-key store of their own.
- `OwnerPaths` still uses the platform's .NET local-application-data location unless `FLEXICORE_ENGINE_HOME` is
  explicitly set. No customer database is introduced; customer rows remain derived from persisted ledger records.
-   - The Avalonia desktop app and Android app share the same owner shell and viewmodels. Android uses SAF streams
    for document imports/exports and an Android Keystore adapter for non-exportable P-256 signing keys.
  - Imported `.p8` keys keep the existing passphrase-encrypted PKCS#8 format. They remain exportable only as
    encrypted files; Android-generated Keystore keys cannot be exported or restored on another device.
  - Android Keystore hardware backing is not queried or claimed. Android app backups are disabled; the owner
    must explicitly export ledger and encrypted-key backups through SAF.
  - The retained WPF UI continues to provide the Windows reference workflow. The Android app is development-mode
    source; no release APK signing credentials or production packages are included.

Platform references:

- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms)
- [Avalonia storage provider](https://docs.avaloniaui.net/docs/services/storage/storage-provider)
- [Android Keystore](https://developer.android.com/privacy-and-security/keystore)
