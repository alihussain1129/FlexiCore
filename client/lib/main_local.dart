// RETIRED. This file used to start the local ERP without the license gate.
// It is kept only so old commands keep working: it now runs the official
// entry point in main.dart, which enforces activation.
import 'main.dart' as official;

void main() => official.main();