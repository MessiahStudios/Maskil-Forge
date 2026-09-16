// macOS module lifecycle probe. No factory invocation, component creation, UI, or audio processing.
// Lifecycle reference: Steinberg public.sdk/source/vst/hosting/module_mac.mm.
#include <CoreFoundation/CoreFoundation.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/resource.h>

static FILE *protocol;
static const char *stage = "WorkerStarted";
static void report(const char *status) {
    fprintf(protocol, "{\"protocolVersion\":1,\"stage\":\"%s\",\"status\":\"%s\"}\n", stage, status);
    fflush(protocol);
}
static void completed_stage(const char *value) { stage = value; report("Progress"); }
static int failed(const char *status) { report(status); return 1; }

int main(int argc, char **argv) {
    if (argc != 3) return 2;
    // Keep library logging away from the protocol. The supervisor bounds both streams.
    int descriptor = dup(STDOUT_FILENO);
    if (descriptor < 0 || !(protocol = fdopen(descriptor, "w")) || dup2(STDERR_FILENO, STDOUT_FILENO) < 0) return 2;
    struct rlimit no_core = {0, 0};
    setrlimit(RLIMIT_CORE, &no_core);
    report("Progress");
    CFURLRef url = CFURLCreateFromFileSystemRepresentation(NULL, (const UInt8 *)argv[1], strlen(argv[1]), true);
    CFBundleRef bundle = url ? CFBundleCreate(NULL, url) : NULL;
    if (url) CFRelease(url);
    if (!bundle) return failed("BundleOpenFailed");
    // Confirm CoreFoundation resolved the same executable the parent inspected.
    CFURLRef executable = CFBundleCopyExecutableURL(bundle);
    char resolved[4096];
    bool same = executable && CFURLGetFileSystemRepresentation(executable, true, (UInt8 *)resolved, sizeof(resolved)) && strcmp(resolved, argv[2]) == 0;
    if (executable) CFRelease(executable);
    if (!same) { CFRelease(bundle); return failed("ExecutableChanged"); }
    completed_stage("BundleOpened");
    CFErrorRef error = NULL;
    if (!CFBundleLoadExecutableAndReturnError(bundle, &error)) {
        if (error) CFRelease(error);
        CFRelease(bundle);
        return failed("LoadFailed");
    }
    completed_stage("ModuleLoaded");
    typedef bool (*Entry)(CFBundleRef);
    typedef bool (*Exit)(void);
    Entry entry = (Entry)CFBundleGetFunctionPointerForName(bundle, CFSTR("bundleEntry"));
    Exit exit_bundle = (Exit)CFBundleGetFunctionPointerForName(bundle, CFSTR("bundleExit"));
    void *factory = CFBundleGetFunctionPointerForName(bundle, CFSTR("GetPluginFactory"));
    const char *failure = NULL;
    if (!entry || !exit_bundle || !factory) failure = "MissingEntryPoints";
    else {
        completed_stage("EntryPointsResolved");
        if (!entry(bundle)) failure = "EntryRejected";
        else {
            completed_stage("ModuleEntered");
            if (!exit_bundle()) failure = "ExitRejected";
            else completed_stage("ModuleExited");
        }
    }
    // Unload is covered by the parent's timeout too. A cleanup crash cannot be a success.
    CFBundleUnloadExecutable(bundle);
    CFRelease(bundle);
    if (failure) return failed(failure);
    stage = "ModuleUnloaded";
    report("Completed");
    fclose(protocol);
    return 0;
}
