#include <stdbool.h>
#include <stdio.h>
#include <signal.h>
#include <unistd.h>
#include <stdint.h>

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
#if MODE == 10
typedef int32_t tresult; typedef uint32_t uint32;
typedef struct { unsigned char cid[16]; int32_t cardinality; char category[32]; char name[64]; } PClassInfo;
typedef struct FactoryVTable FactoryVTable; typedef struct { FactoryVTable *vtable; } Factory;
struct FactoryVTable { tresult (*queryInterface)(void *, const unsigned char *, void **); uint32 (*addRef)(void *); uint32 (*release)(void *); tresult (*getFactoryInfo)(void *, void *); int32_t (*countClasses)(void *); tresult (*getClassInfo)(void *, int32_t, PClassInfo *); tresult (*createInstance)(void *, const char *, const char *, void **); };
static int32_t count(void *unused) { (void)unused; return 2; }
static tresult info(void *unused, int32_t index, PClassInfo *out) { (void)unused; if (index < 0 || index > 1) return 1; *out = (PClassInfo){0}; out->cid[15] = (unsigned char)(index + 1); snprintf(out->category, sizeof(out->category), "Audio Module Class"); snprintf(out->name, sizeof(out->name), "Fixture %d", index + 1); return 0; }
static FactoryVTable table = {0, 0, 0, 0, count, info, 0}; static Factory factory = { &table };
void *GetPluginFactory(void) { return &factory; }
#elif MODE != 1
// The lifecycle probe must find this export without ever invoking it.
void *GetPluginFactory(void) { raise(SIGKILL); return NULL; }
#endif
