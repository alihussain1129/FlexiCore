# FlexiCore Activation App

The Avalonia desktop and Android applications share the recovered
`FlexiCore.ActivationServices` viewmodels and production licensing services.
The WPF Activation Engine remains the Windows reference application.

The UI design tokens are maintained in `ActivationDesignSystem.cs`, with the
same official FlexiCore deep-teal palette (`#125352`), mint/aqua accents,
semantic states, gradients, elevation, spacing, and light/dark surfaces used
by the Flutter ERP client. Desktop uses a branded sidebar and a responsive
dashboard; compact Android uses bottom navigation with an overflow menu so all
owner pages remain reachable. Dashboard statistics, customer records, and
activity come from the persisted issue ledger and key store; no placeholder
metrics are generated.

Desktop builds target .NET 10 and Avalonia for Windows and Linux:

```sh
dotnet build tools/FlexiCore.ActivationApp/FlexiCore.ActivationApp.csproj -c Debug
dotnet run --project tools/FlexiCore.ActivationApp/FlexiCore.ActivationApp.csproj
```

For Android setup, Storage Access Framework behavior, key security, and the
actual Debug APK location, see [FlexiCore Activation Engine for Android](../FlexiCore.ActivationAndroid/README.md).
