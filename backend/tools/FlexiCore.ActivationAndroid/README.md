# FlexiCore Activation Engine for Android

The Android app is an Avalonia single-view application using the recovered
`FlexiCore.ActivationServices` viewmodels and the existing
`FlexiCore.LicenseTool.Core` and `FlexiCore.Licensing` implementations. It does
not implement a second issuer, envelope, payload schema, or request-code format.

## Owner workflows

The shared shell provides the persisted-data dashboard, customer list derived
from the issue ledger, licenses and issuance history, request-code parsing and
validation, license issue and status, signing keys, key lifecycle management,
settings, and security/help information. License issuance runs the existing
issuer self-check and independently verifies against the exported public-key
set before saving and recording the ledger.

Android Storage Access Framework is used through Avalonia's Android
`StorageProvider`. Selected documents are read through their granted streams
into unique app-private staging directories; document URIs are never treated
as filesystem paths. Exports are first written to app-private staging files,
then streamed to the document returned by the Android save picker. The
`ACTION_CREATE_DOCUMENT` provider creates a new document rather than
overwriting an existing one. Temporary SAF document grants are used only for
the current operation; no URI grant or raw URI is persisted.

SAF actions cover request/public-key/ERP-key/encrypted-key import, signed
license and optional activation-code export, public-key export, encrypted
`.p8` backup, and issue-ledger backup. Imported files are size-limited to 50 MB;
request text imports have a stricter 256 KB limit and encrypted `.p8` files are
limited to 1 MB.

## Shared design system

The Android and desktop Activation Engine shells use the official FlexiCore
primary `#125352`, the shared deep-teal tonal palette, luminous aqua and soft
mint accents, semantic feedback colors, restrained gradients, rounded
surfaces, and teal-tinted elevation from `ActivationDesignSystem.cs`. The
dashboard uses only ledger/key-store values and places them in an adaptive
two-column mobile or expanded desktop layout beneath a branded hero. Android
uses touch-sized bottom navigation with an overflow menu for the remaining
owner pages. A header/sidebar control switches between coordinated light and
dark surfaces.

## Android key security

- The APK contains no owner private keys or signing-key resources.
- The default Android key workflow can generate an ECDSA P-256 key in
  `AndroidKeyStore`. The app refuses a generated key if Android returns
  exportable private-key encoding. Signing uses `SHA256withECDSA`; its DER
  signature is converted to the required fixed 64-byte `r || s` representation
  before the existing license issuer builds the unchanged ES256 envelope.
- Hardware-backed/StrongBox status is **not queried and is not claimed**.
  Android-generated keys are labeled device-bound and non-exportable. They
  cannot be backed up or moved; uninstalling the app or losing the device may
  permanently lose the key.
- Existing imported keys retain the exact PBES2/AES-256-CBC/PBKDF2-SHA-256
  encrypted PKCS#8 `.p8` bytes and existing passphrase semantics. The app
  validates the passphrase and key before import and can export only the
  encrypted `.p8` file. No unencrypted private key is exported.
- Encrypted imported keys, the issue ledger, registry, and public keys are
  stored in Android app-private credential-encrypted storage. `allowBackup` is
  disabled so app-private owner records are not silently restored without
  device Keystore entries. Use explicit SAF backups for encrypted keys and the
  ledger, and store key passphrases separately.
- Passphrases and private-key bytes are not logged. The UI masks passphrases
  and clears them after a signing/import attempt.

The Keystore key is deliberately distinct from an imported encrypted `.p8`:
the first is a device-bound signing key without a portable private-key backup;
the latter is portable only as its existing passphrase-encrypted file.

## Build, install, and launch (development)

Requirements: .NET SDK 10.0.401, the Android workload, JDK 17, Android SDK
platform/build-tools packages, and a connected device or emulator.

```sh
dotnet workload install android
dotnet publish backend/tools/FlexiCore.ActivationAndroid/FlexiCore.ActivationAndroid.csproj \
  -c Debug -p:AndroidPackageFormat=apk
adb install --no-incremental -r \
  backend/tools/FlexiCore.ActivationAndroid/bin/Debug/net10.0-android/com.flexicore.activationengine-Signed.apk
adb shell monkey -p com.flexicore.activationengine 1
```

The Debug APK output is
`backend/tools/FlexiCore.ActivationAndroid/bin/Debug/net10.0-android/com.flexicore.activationengine-Signed.apk`.
Managed assemblies are embedded so the published APK can launch independently
of a development fast-deployment session. It is signed with the Android Debug
certificate for development only. No Release APK is generated unless the owner
supplies and configures a production signing certificate.

Desktop development remains separate:

```sh
dotnet build backend/tools/FlexiCore.ActivationApp/FlexiCore.ActivationApp.csproj -c Debug
dotnet build backend/tools/FlexiCore.ActivationEngine/FlexiCore.ActivationEngine.csproj \
  -c Debug -p:EnableWindowsTargeting=true
```

Shared contract and regression checks:

```sh
dotnet test backend/tests/FlexiCore.ActivationEngine.Tests/FlexiCore.ActivationEngine.Tests.csproj
dotnet test backend/tests/FlexiCore.LicenseTool.Tests/FlexiCore.LicenseTool.Tests.csproj
dotnet test backend/tests/FlexiCore.ArchitectureTests/FlexiCore.ArchitectureTests.csproj
```
