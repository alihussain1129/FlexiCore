# FlexiCore Flutter client

This Flutter application provides the FlexiCore authentication, Local activation and organization/company workspace flows on Android, Windows and Linux. It currently does not contain transactional ERP modules.

For current implementation status, supported spreadsheet/document exchange, setup, and backup/recovery instructions, see the repository [README](../README.md).

## Development

Run the Windows client from this directory:

```powershell
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5265
```

Run the Linux client from this directory (Clang, CMake, Ninja, GTK 3 and libsecret development/runtime libraries, a desktop keyring service, and the Flutter Linux desktop toolchain are required):

```bash
flutter run -d linux --dart-define=API_BASE_URL=http://localhost:5265
```

Run on an attached Android device or emulator with the API URL reachable from that device:

```bash
flutter run -d <device-id> --dart-define=API_BASE_URL=<reachable-api-url>
```

The workspace UI uses responsive Flutter layouts: a persistent sidebar on wide windows, bottom navigation for the real Overview and Directory destinations on compact screens, wrapping summary cards, stacked workspace selectors on narrow widths, and horizontally scrollable data tables. The Overview has an active-workspace hero and counts only authorized records returned by the API. No financial or operational dashboard metrics are shown because those modules and records do not exist yet.

Run client checks from this directory:

```powershell
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
```

These are development checks only; this project does not build release packages as part of this workflow.

## GUI modernization status

The shared Flutter design system is centered on the official deep-teal brand `#125352` and defines the complete tonal palette, semantic colors, light/dark surfaces, gradients, elevation, spacing, radii, typography, component states and reduced-motion-aware animation durations. The workspace uses the brand hero, real create actions, count-up summaries, adaptive desktop navigation and mobile bottom navigation. Existing API-backed organization/company counts remain identified as loaded/partial rather than presented as invented totals.

Remaining GUI work: user and membership administration; search, filter and pagination refinements for very large datasets; localization and assistive-technology audits; device testing across Android form factors; Windows and Linux interaction testing with real APIs; and future API-backed ERP modules before financial or operational dashboards can be designed. The active-company selector remains in the workspace overview rather than the global header so it stays usable on touch screens.
