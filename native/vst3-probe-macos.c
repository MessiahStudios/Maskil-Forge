// macOS module lifecycle probe. Factory enumeration is optional; no component creation, UI, or audio processing.
// Lifecycle reference: Steinberg public.sdk/source/vst/hosting/module_mac.mm.
#include <CoreFoundation/CoreFoundation.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/resource.h>

typedef int32_t tresult;
typedef uint32_t uint32;
typedef struct { unsigned char cid[16]; int32_t cardinality; char category[32]; char name[64]; } PClassInfo;
typedef struct FactoryVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*getFactoryInfo)(void *, void *);
    int32_t (*countClasses)(void *);
    tresult (*getClassInfo)(void *, int32_t, PClassInfo *);
    tresult (*createInstance)(void *, const char *, const char *, void **);
} FactoryVTable;
typedef struct { FactoryVTable *vtable; } Factory;
typedef Factory *(*GetFactory)(void);

static FILE *protocol;
static const char *stage = "WorkerStarted";
static void report(const char *status) {
    fprintf(protocol, "{\"protocolVersion\":1,\"stage\":\"%s\",\"status\":\"%s\"}\n", stage, status);
    fflush(protocol);
}
static void completed_stage(const char *value) { stage = value; report("Progress"); }
static int failed(const char *status) { report(status); return 1; }
static void hex_id(const unsigned char *bytes, char *out) {
    static const char digits[] = "0123456789ABCDEF";
    for (int i = 0; i < 16; i++) { out[i * 2] = digits[bytes[i] >> 4]; out[i * 2 + 1] = digits[bytes[i] & 15]; }
    out[32] = '\0';
}
static void json_string(FILE *out, const char *value, size_t capacity) {
    fputc('"', out);
    for (size_t index = 0; index < capacity && value[index]; index++) {
        unsigned char byte = (unsigned char)value[index];
        if (byte == '"' || byte == '\\') { fputc('\\', out); fputc(byte, out); }
        else if (byte >= 32) fputc(byte, out);
    }
    fputc('"', out);
}

int main(int argc, char **argv) {
    if (argc != 3 && argc != 4) return 2;
    bool enumerate_factory = argc == 4 && strcmp(argv[3], "--enumerate-factory") == 0;
    if (argc == 4 && !enumerate_factory) return 2;
    // Keep library logging away from the protocol. The supervisor bounds both streams.
    int descriptor = dup(STDOUT_FILENO);
    if (descriptor < 0 || !(protocol = fdopen(descriptor, "w")) || dup2(STDERR_FILENO, STDOUT_FILENO) < 0) return 2;
    setvbuf(stdout, NULL, _IONBF, 0);
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
    void *factory_export = CFBundleGetFunctionPointerForName(bundle, CFSTR("GetPluginFactory"));
    const char *failure = NULL;
    Factory *factory = NULL;
    if (!entry || !exit_bundle || !factory_export) failure = "MissingEntryPoints";
    else {
        completed_stage("EntryPointsResolved");
        if (!entry(bundle)) failure = "EntryRejected";
        else {
            completed_stage("ModuleEntered");
            if (enumerate_factory) {
                factory = ((GetFactory)factory_export)();
                if (!factory || !factory->vtable || !factory->vtable->countClasses || !factory->vtable->getClassInfo) failure = "FactoryUnavailable";
                else {
                    int32_t count = factory->vtable->countClasses(factory);
                    if (count < 0 || count > 256) failure = "FactoryClassLimit";
                    else {
                        for (int32_t index = 0; index < count; index++) {
                            PClassInfo info = {0};
                            if (factory->vtable->getClassInfo(factory, index, &info) != 0) { failure = "FactoryClassReadFailed"; break; }
                            char id[33]; hex_id(info.cid, id);
                            fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Class\",\"stage\":\"FactoryClass\",\"id\":"); json_string(protocol, id, sizeof(id));
                            fprintf(protocol, ",\"name\":"); json_string(protocol, info.name, sizeof(info.name)); fprintf(protocol, ",\"category\":"); json_string(protocol, info.category, sizeof(info.category)); fprintf(protocol, "}\n"); fflush(protocol);
                        }
                        if (!failure) { fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"FactoryCompleted\",\"stage\":\"FactoryClasses\",\"classCount\":%d}\n", count); fflush(protocol); }
                    }
                }
            }
            if (factory && factory->vtable && factory->vtable->release) factory->vtable->release(factory);
            if (!failure && !exit_bundle()) failure = "ExitRejected";
            else if (!failure) completed_stage("ModuleExited");
        }
    }
    // Unload is covered by the parent's timeout too. A cleanup crash cannot be a success.
    CFBundleUnloadExecutable(bundle);
    CFRelease(bundle);
    if (failure) return failed(failure);
    stage = "ModuleUnloaded";
    if (enumerate_factory) { fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Completed\",\"stage\":\"ModuleUnloaded\"}\n"); fflush(protocol); }
    else report("Completed");
    fclose(protocol);
    return 0;
}
