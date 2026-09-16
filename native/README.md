# macOS VST3 module probe

`vst3-probe-macos.c` is a short-lived native executable supervised by `Vst3ProbeProcess` in the .NET service. On macOS, building `MaskilForge.Api` compiles it with Apple clang and CoreFoundation and copies it to `native/maskil-vst3-probe` alongside the service and into publish output. Xcode Command Line Tools are required (`xcode-select --install` if absent). Linux and Windows builds do not compile this worker; filesystem discovery and header inspection still work there.

The implementation follows the lifecycle contract in [Steinberg's macOS module loader](https://github.com/steinbergmedia/vst3_public_sdk/blob/master/source/vst/hosting/module_mac.mm): load the bundle, resolve `bundleEntry`, `bundleExit`, and `GetPluginFactory`, call entry and exit, then release the module. This probe checks that the factory export exists but never calls it. It uses the platform API directly and adds no VST SDK dependency.

## Boundary and protocol

Each explicit local request creates a fresh worker. The service allows one at a time, caps each output stream at 16 KiB, applies a ten-second deadline, and kills the process tree on cancellation, timeout, or excess output. Worker stderr is drained and discarded, not returned to the browser. The environment is reduced to HOME, TMPDIR, LANG, and a system PATH. Core dumps are disabled in the worker. This is crash containment, **not a security sandbox**: installed plugin code retains the user's filesystem and OS access, can perform initialization side effects, and may spawn processes. Only trusted installed plugins should be checked.

The worker takes two separate arguments, the rediscovered bundle path and expected executable path. Before loading, CoreFoundation's resolved executable must equal the expected path. Progress is flushed as newline-delimited JSON with exactly `protocolVersion`, `stage`, and `status`. Protocol version 1 stages are `WorkerStarted`, `BundleOpened`, `ModuleLoaded`, `EntryPointsResolved`, `ModuleEntered`, `ModuleExited`, and `ModuleUnloaded`. Success requires the full ordered sequence, a final `Completed` record, and exit code zero. Cleanup is part of the supervised operation. A crash or hang reports the deepest completed stage instead of declaring a plugin broken or compatible.

The API accepts only loopback clients with a loopback Host and a same-origin browser request (or the existing localhost Vite development origins). It resolves an exact location name and relative candidate path by rediscovering configured VST3 folders. It requires an unchanged XML plist declaration and a matching Mach-O header, checks links, and fingerprints the declared executable (maximum 512 MiB) before and after execution. Changed executable or declaration evidence invalidates the result. These checks do not freeze bundle files against concurrent replacement or attest dependencies/resources; they are not a defense against a malicious local installer or plugin.

## Validation

`dotnet test MaskilForge.sln` builds native fixtures on macOS and exercises success, missing exports, rejected entry/exit, hangs during load/entry/exit, crashes during entry/cleanup, excessive logging, cancellation, and a subsequent healthy worker. Other tests reject incomplete or malformed protocol output, stale declarations, arbitrary paths, linked candidates, overlapping checks, and remote or untrusted-origin requests. The macOS CI job runs these fixtures without requiring commercial plugins.

The deepest completed real-plugin stage on the development Mac is module cleanup for Deelay. The next step is factory creation and class enumeration inside the worker. Component creation, processor initialization, audio buses, rendering, editors, presets, and production-role substitution remain later work.
