// macOS module lifecycle probe. Optional actions enumerate the factory or inspect one component's audio buses.
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
typedef uint16_t char16;
typedef struct { unsigned char cid[16]; int32_t cardinality; char category[32]; char name[64]; } PClassInfo;
typedef struct {
    int32_t mediaType;
    int32_t direction;
    int32_t channelCount;
    char16 name[128];
    int32_t busType;
    uint32 flags;
} BusInfo;
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

typedef struct ComponentVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*initialize)(void *, void *);
    tresult (*terminate)(void *);
    tresult (*getControllerClassId)(void *, unsigned char *);
    tresult (*setIoMode)(void *, int32_t);
    int32_t (*getBusCount)(void *, int32_t, int32_t);
    tresult (*getBusInfo)(void *, int32_t, int32_t, int32_t, BusInfo *);
    tresult (*getRoutingInfo)(void *, void *, void *);
    tresult (*activateBus)(void *, int32_t, int32_t, int32_t, unsigned char);
    tresult (*setActive)(void *, unsigned char);
    tresult (*setState)(void *, void *);
    tresult (*getState)(void *, void *);
} ComponentVTable;
typedef struct { ComponentVTable *vtable; } Component;

typedef struct HostVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*getName)(void *, char16 *);
    tresult (*createInstance)(void *, const unsigned char *, const unsigned char *, void **);
} HostVTable;
typedef struct { HostVTable *vtable; uint32 references; } Host;

static const unsigned char unknown_iid[16] = {0, 0, 0, 0, 0, 0, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46};
static const unsigned char host_iid[16] = {0x58, 0xE5, 0x95, 0xCC, 0xDB, 0x2D, 0x49, 0x69, 0x8B, 0x6A, 0xAF, 0x8C, 0x36, 0xA6, 0x64, 0xE5};
static const unsigned char component_iid[16] = {0xE8, 0x31, 0xFF, 0x31, 0xF2, 0xD5, 0x43, 0x01, 0x92, 0x8E, 0xBB, 0xEE, 0x25, 0x69, 0x78, 0x02};

static tresult host_query(void *self, const unsigned char *iid, void **object) {
    Host *host = self;
    if (!object) return 2;
    *object = NULL;
    if (memcmp(iid, unknown_iid, 16) != 0 && memcmp(iid, host_iid, 16) != 0) return -1;
    *object = self;
    host->references++;
    return 0;
}
static uint32 host_add_ref(void *self) { return ++((Host *)self)->references; }
static uint32 host_release(void *self) {
    Host *host = self;
    if (host->references) host->references--;
    return host->references;
}
static tresult host_name(void *self, char16 *name) {
    (void)self;
    static const char value[] = "Maskil Forge";
    if (!name) return 2;
    memset(name, 0, sizeof(char16) * 128);
    for (size_t index = 0; index < sizeof(value) - 1; index++) name[index] = (char16)value[index];
    return 0;
}
static tresult host_create(void *self, const unsigned char *cid, const unsigned char *iid, void **object) {
    (void)self; (void)cid; (void)iid;
    if (object) *object = NULL;
    return -1;
}
static HostVTable host_vtable = {host_query, host_add_ref, host_release, host_name, host_create};

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
static void json_char16_string(FILE *out, const char16 *value, size_t capacity) {
    fputc('"', out);
    for (size_t index = 0; index < capacity && value[index]; index++) {
        uint32 code = value[index];
        if (code >= 0xD800 && code <= 0xDBFF && index + 1 < capacity && value[index + 1] >= 0xDC00 && value[index + 1] <= 0xDFFF)
            code = 0x10000 + ((code - 0xD800) << 10) + (value[++index] - 0xDC00);
        else if (code >= 0xD800 && code <= 0xDFFF) code = 0xFFFD;
        if (code == '"' || code == '\\') { fputc('\\', out); fputc((int)code, out); }
        else if (code < 32) fprintf(out, "\\u%04X", code);
        else if (code < 0x80) fputc((int)code, out);
        else if (code < 0x800) { fputc(0xC0 | (int)(code >> 6), out); fputc(0x80 | (int)(code & 0x3F), out); }
        else if (code < 0x10000) { fputc(0xE0 | (int)(code >> 12), out); fputc(0x80 | (int)((code >> 6) & 0x3F), out); fputc(0x80 | (int)(code & 0x3F), out); }
        else { fputc(0xF0 | (int)(code >> 18), out); fputc(0x80 | (int)((code >> 12) & 0x3F), out); fputc(0x80 | (int)((code >> 6) & 0x3F), out); fputc(0x80 | (int)(code & 0x3F), out); }
    }
    fputc('"', out);
}
static int hex_digit(char value) {
    if (value >= '0' && value <= '9') return value - '0';
    if (value >= 'A' && value <= 'F') return value - 'A' + 10;
    if (value >= 'a' && value <= 'f') return value - 'a' + 10;
    return -1;
}
static bool parse_id(const char *value, unsigned char *out) {
    if (!value || strlen(value) != 32) return false;
    for (int index = 0; index < 16; index++) {
        int high = hex_digit(value[index * 2]);
        int low = hex_digit(value[index * 2 + 1]);
        if (high < 0 || low < 0) return false;
        out[index] = (unsigned char)((high << 4) | low);
    }
    return true;
}
static bool report_bus(Component *component, int32_t direction, int32_t index) {
    BusInfo info = {0};
    if (component->vtable->getBusInfo(component, 0, direction, index, &info) != 0 || info.mediaType != 0 ||
        info.direction != direction || info.channelCount < 0 || info.channelCount > 1024 || (info.busType != 0 && info.busType != 1)) return false;
    fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Bus\",\"stage\":\"AudioBus\",\"direction\":\"%s\",\"index\":%d,\"name\":", direction == 0 ? "Input" : "Output", index);
    json_char16_string(protocol, info.name, 128);
    fprintf(protocol, ",\"channelCount\":%d,\"busType\":\"%s\",\"defaultActive\":%s,\"controlVoltage\":%s}\n",
        info.channelCount, info.busType == 0 ? "Main" : "Aux", (info.flags & 1) ? "true" : "false", (info.flags & 2) ? "true" : "false");
    fflush(protocol);
    return true;
}

int main(int argc, char **argv) {
    if (argc != 3 && argc != 4 && argc != 5) return 2;
    bool enumerate_factory = argc == 4 && strcmp(argv[3], "--enumerate-factory") == 0;
    bool inspect_component = argc == 5 && strcmp(argv[3], "--inspect-component") == 0;
    unsigned char requested_id[16];
    if ((argc == 4 && !enumerate_factory) || (argc == 5 && (!inspect_component || !parse_id(argv[4], requested_id)))) return 2;
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
            if (enumerate_factory || inspect_component) {
                factory = ((GetFactory)factory_export)();
                if (!factory || !factory->vtable || !factory->vtable->countClasses || !factory->vtable->getClassInfo) failure = "FactoryUnavailable";
                else {
                    int32_t count = factory->vtable->countClasses(factory);
                    if (count < 0 || count > 256) failure = "FactoryClassLimit";
                    else if (enumerate_factory) {
                        for (int32_t index = 0; index < count; index++) {
                            PClassInfo info = {0};
                            if (factory->vtable->getClassInfo(factory, index, &info) != 0) { failure = "FactoryClassReadFailed"; break; }
                            char id[33]; hex_id(info.cid, id);
                            fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Class\",\"stage\":\"FactoryClass\",\"id\":"); json_string(protocol, id, sizeof(id));
                            fprintf(protocol, ",\"name\":"); json_string(protocol, info.name, sizeof(info.name)); fprintf(protocol, ",\"category\":"); json_string(protocol, info.category, sizeof(info.category)); fprintf(protocol, "}\n"); fflush(protocol);
                        }
                        if (!failure) { fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"FactoryCompleted\",\"stage\":\"FactoryClasses\",\"classCount\":%d}\n", count); fflush(protocol); }
                    } else {
                        PClassInfo selected = {0};
                        bool found = false;
                        for (int32_t index = 0; index < count; index++) {
                            PClassInfo info = {0};
                            if (factory->vtable->getClassInfo(factory, index, &info) != 0) { failure = "FactoryClassReadFailed"; break; }
                            if (memcmp(info.cid, requested_id, 16) == 0) { selected = info; found = true; break; }
                        }
                        if (!failure && !found) failure = "ComponentClassUnavailable";
                        else if (!failure && strncmp(selected.category, "Audio Module Class", sizeof(selected.category)) != 0) failure = "ComponentClassUnsupported";
                        else if (!failure && !factory->vtable->createInstance) failure = "FactoryUnavailable";
                        else if (!failure) {
                            Component *component = NULL;
                            if (factory->vtable->createInstance(factory, (const char *)selected.cid, (const char *)component_iid, (void **)&component) != 0 ||
                                !component || !component->vtable || !component->vtable->initialize || !component->vtable->terminate ||
                                !component->vtable->getBusCount || !component->vtable->getBusInfo || !component->vtable->release) failure = "ComponentCreateFailed";
                            else {
                                completed_stage("ComponentCreated");
                                Host host = {&host_vtable, 1};
                                if (component->vtable->initialize(component, &host) != 0) failure = "ComponentInitializeFailed";
                                else {
                                    completed_stage("ComponentInitialized");
                                    int32_t inputs = component->vtable->getBusCount(component, 0, 0);
                                    int32_t outputs = component->vtable->getBusCount(component, 0, 1);
                                    if (inputs < 0 || outputs < 0 || inputs > 64 || outputs > 64 || inputs + outputs > 128) failure = "AudioBusLimit";
                                    for (int32_t index = 0; !failure && index < inputs; index++) if (!report_bus(component, 0, index)) failure = "AudioBusReadFailed";
                                    for (int32_t index = 0; !failure && index < outputs; index++) if (!report_bus(component, 1, index)) failure = "AudioBusReadFailed";
                                    if (!failure) {
                                        char id[33]; hex_id(selected.cid, id);
                                        fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"ComponentCompleted\",\"stage\":\"AudioBuses\",\"classId\":\"%s\",\"inputBusCount\":%d,\"outputBusCount\":%d}\n", id, inputs, outputs);
                                        fflush(protocol);
                                        completed_stage("BusesInspected");
                                    }
                                    if (component->vtable->terminate(component) != 0) { if (!failure) failure = "ComponentTerminateFailed"; }
                                    else if (!failure) completed_stage("ComponentTerminated");
                                }
                                component->vtable->release(component);
                            }
                        }
                    }
                }
            }
            if (factory && factory->vtable && factory->vtable->release) factory->vtable->release(factory);
            bool exited = exit_bundle();
            if (!failure && !exited) failure = "ExitRejected";
            else if (!failure) completed_stage("ModuleExited");
        }
    }
    // Unload is covered by the parent's timeout too. A cleanup crash cannot be a success.
    CFBundleUnloadExecutable(bundle);
    CFRelease(bundle);
    if (failure) return failed(failure);
    stage = "ModuleUnloaded";
    if (enumerate_factory || inspect_component) { fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Completed\",\"stage\":\"ModuleUnloaded\"}\n"); fflush(protocol); }
    else report("Completed");
    fclose(protocol);
    return 0;
}
