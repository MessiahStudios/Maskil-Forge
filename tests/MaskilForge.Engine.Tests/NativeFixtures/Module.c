#include <stdbool.h>
#include <stdio.h>
#include <signal.h>
#include <unistd.h>
#include <stdint.h>
#include <string.h>

__attribute__((constructor)) static void loaded(void) {
    if (MODE == 6) sleep(30);
}
__attribute__((destructor)) static void unloaded(void) {
    if (MODE == 9) raise(SIGKILL);
}
bool bundleEntry(void *bundle) {
    (void)bundle;
    if (MODE == 4) sleep(30);
    if (MODE == 5) raise(SIGKILL);
    if (MODE == 7) { for (int i = 0; i < 100000; i++) fputc('x', stdout); fflush(stdout); sleep(30); }
    return MODE != 2;
}
bool bundleExit(void) {
    if (MODE == 8) sleep(30);
    return MODE != 3;
}
#if MODE == 10 || MODE == 11
typedef int32_t tresult; typedef uint32_t uint32;
typedef struct { unsigned char cid[16]; int32_t cardinality; char category[32]; char name[64]; } PClassInfo;
typedef struct FactoryVTable FactoryVTable; typedef struct { FactoryVTable *vtable; } Factory;
struct FactoryVTable { tresult (*queryInterface)(void *, const unsigned char *, void **); uint32 (*addRef)(void *); uint32 (*release)(void *); tresult (*getFactoryInfo)(void *, void *); int32_t (*countClasses)(void *); tresult (*getClassInfo)(void *, int32_t, PClassInfo *); tresult (*createInstance)(void *, const char *, const char *, void **); };
static int32_t count(void *unused) { (void)unused; return MODE == 10 ? 2 : 1; }
static tresult info(void *unused, int32_t index, PClassInfo *out) { (void)unused; if (index < 0 || index >= count(NULL)) return 1; *out = (PClassInfo){0}; out->cid[15] = (unsigned char)(index + 1); snprintf(out->category, sizeof(out->category), "Audio Module Class"); snprintf(out->name, sizeof(out->name), "Fixture %d", index + 1); return 0; }
#if MODE == 11
typedef struct { int32_t mediaType; int32_t direction; int32_t channelCount; uint16_t name[128]; int32_t busType; uint32 flags; } BusInfo;
typedef struct ComponentVTable ComponentVTable; typedef struct { ComponentVTable *vtable; } Component;
struct ComponentVTable { tresult (*queryInterface)(void *, const unsigned char *, void **); uint32 (*addRef)(void *); uint32 (*release)(void *); tresult (*initialize)(void *, void *); tresult (*terminate)(void *); tresult (*getControllerClassId)(void *, unsigned char *); tresult (*setIoMode)(void *, int32_t); int32_t (*getBusCount)(void *, int32_t, int32_t); tresult (*getBusInfo)(void *, int32_t, int32_t, int32_t, BusInfo *); tresult (*getRoutingInfo)(void *, void *, void *); tresult (*activateBus)(void *, int32_t, int32_t, int32_t, unsigned char); tresult (*setActive)(void *, unsigned char); tresult (*setState)(void *, void *); tresult (*getState)(void *, void *); };
typedef struct { tresult (*queryInterface)(void *, const unsigned char *, void **); uint32 (*addRef)(void *); uint32 (*release)(void *); tresult (*getName)(void *, uint16_t *); } HostVTable;
typedef struct { HostVTable *vtable; } Host;
static const unsigned char component_iid[16] = {0xE8, 0x31, 0xFF, 0x31, 0xF2, 0xD5, 0x43, 0x01, 0x92, 0x8E, 0xBB, 0xEE, 0x25, 0x69, 0x78, 0x02};
static const unsigned char host_iid[16] = {0x58, 0xE5, 0x95, 0xCC, 0xDB, 0x2D, 0x49, 0x69, 0x8B, 0x6A, 0xAF, 0x8C, 0x36, 0xA6, 0x64, 0xE5};
static uint32 component_ref(void *unused) { (void)unused; return 1; }
static tresult component_initialize(void *unused, void *context) { (void)unused; if (!context) return 1; Host *host = context; void *application = NULL; if (host->vtable->queryInterface(host, host_iid, &application) != 0 || !application) return 1; uint16_t name[128] = {0}; tresult result = ((Host *)application)->vtable->getName(application, name); ((Host *)application)->vtable->release(application); return result != 0 || name[0] != 'M'; }
static tresult component_terminate(void *unused) { (void)unused; return 0; }
static int32_t bus_count(void *unused, int32_t media, int32_t direction) { (void)unused; if (media != 0) return 0; return direction == 0 ? 1 : 2; }
static void bus_name(uint16_t *out, const char *value) { while (*value) *out++ = (uint16_t)*value++; }
static tresult bus_info(void *unused, int32_t media, int32_t direction, int32_t index, BusInfo *out) { (void)unused; if (!out || media != 0 || direction < 0 || direction > 1 || index < 0 || index >= bus_count(NULL, media, direction)) return 1; *out = (BusInfo){.mediaType = media, .direction = direction, .channelCount = index == 1 ? 1 : 2, .busType = index == 1 ? 1 : 0, .flags = index == 1 ? 0 : 1}; bus_name(out->name, direction == 0 ? "Input" : index == 0 ? "Main Out" : "Side Out"); return 0; }
static ComponentVTable component_table = {0, component_ref, component_ref, component_initialize, component_terminate, 0, 0, bus_count, bus_info, 0, 0, 0, 0, 0}; static Component component = {&component_table};
static tresult create(void *unused, const char *cid, const char *iid, void **out) { (void)unused; unsigned char expected[16] = {0}; expected[15] = 1; if (!out || memcmp(cid, expected, 16) != 0 || memcmp(iid, component_iid, 16) != 0) return 1; *out = &component; return 0; }
static FactoryVTable table = {0, 0, 0, 0, count, info, create};
#else
static FactoryVTable table = {0, 0, 0, 0, count, info, 0};
#endif
static Factory factory = { &table };
void *GetPluginFactory(void) { return &factory; }
#elif MODE != 1
// The lifecycle probe must find this export without ever invoking it.
void *GetPluginFactory(void) { raise(SIGKILL); return NULL; }
#endif
