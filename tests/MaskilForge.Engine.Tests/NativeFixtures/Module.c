#include <stdbool.h>
#include <stdio.h>
#include <signal.h>
#include <unistd.h>

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
#if MODE != 1
// The lifecycle probe must find this export without ever invoking it.
void *GetPluginFactory(void) { raise(SIGKILL); return NULL; }
#endif
