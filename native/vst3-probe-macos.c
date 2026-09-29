// macOS module lifecycle probe. Optional actions enumerate the factory or inspect one component's audio buses.
// Lifecycle reference: Steinberg public.sdk/source/vst/hosting/module_mac.mm.
#include <CoreFoundation/CoreFoundation.h>
#include <math.h>
#include <stddef.h>
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
typedef struct HandlerVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*beginEdit)(void *, uint32);
    tresult (*performEdit)(void *, uint32, double);
    tresult (*endEdit)(void *, uint32);
    tresult (*restartComponent)(void *, int32_t);
} HandlerVTable;
typedef struct Handler { HandlerVTable *vtable; uint32 references; } Handler;
typedef struct { HostVTable *vtable; uint32 references; Handler *handler; } Host;

static const unsigned char unknown_iid[16] = {0, 0, 0, 0, 0, 0, 0, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0x46};
static const unsigned char host_iid[16] = {0x58, 0xE5, 0x95, 0xCC, 0xDB, 0x2D, 0x49, 0x69, 0x8B, 0x6A, 0xAF, 0x8C, 0x36, 0xA6, 0x64, 0xE5};
static const unsigned char component_iid[16] = {0xE8, 0x31, 0xFF, 0x31, 0xF2, 0xD5, 0x43, 0x01, 0x92, 0x8E, 0xBB, 0xEE, 0x25, 0x69, 0x78, 0x02};
static const unsigned char processor_iid[16] = {0x42, 0x04, 0x3F, 0x99, 0xB7, 0xDA, 0x45, 0x3C, 0xA5, 0x69, 0xE7, 0x9D, 0x9A, 0xAE, 0xC3, 0x3D};
static const unsigned char handler_iid[16] = {0x93, 0xA0, 0xBE, 0xA3, 0x0B, 0xD0, 0x45, 0xDB, 0x8E, 0x89, 0x0B, 0x0C, 0xC1, 0xE4, 0x6A, 0xC6};
static const unsigned char controller_iid[16] = {0xDC, 0xD7, 0xBB, 0xE3, 0x77, 0x42, 0x44, 0x8D, 0xA8, 0x74, 0xAA, 0xCC, 0x97, 0x9C, 0x75, 0x9E};
static const unsigned char connection_iid[16] = {0x70, 0xA4, 0x15, 0x6F, 0x6E, 0x6E, 0x40, 0x26, 0x98, 0x91, 0x48, 0xBF, 0xAA, 0x60, 0xD8, 0xD1};
static const unsigned char event_list_iid[16] = {0x3A, 0x2C, 0x42, 0x14, 0x34, 0x63, 0x49, 0xFE, 0xB2, 0xC4, 0xF3, 0x97, 0xB9, 0x69, 0x5A, 0x44};

static tresult host_query(void *self, const unsigned char *iid, void **object) {
    Host *host = self;
    if (!object) return 2;
    *object = NULL;
    if (memcmp(iid, handler_iid, 16) == 0 && host->handler) {
        *object = host->handler;
        host->handler->references++;
        return 0;
    }
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
static tresult handler_query(void *self, const unsigned char *iid, void **object) {
    Handler *handler = self;
    if (!object) return 2;
    *object = NULL;
    if (memcmp(iid, unknown_iid, 16) != 0 && memcmp(iid, handler_iid, 16) != 0) return -1;
    *object = self;
    handler->references++;
    return 0;
}
static uint32 handler_add_ref(void *self) { return ++((Handler *)self)->references; }
static uint32 handler_release(void *self) {
    Handler *handler = self;
    if (handler->references) handler->references--;
    return handler->references;
}
static tresult handler_ok(void *self, uint32 id) { (void)self; (void)id; return 0; }
static tresult handler_edit(void *self, uint32 id, double value) { (void)self; (void)id; (void)value; return 0; }
static tresult handler_restart(void *self, int32_t flags) { (void)self; (void)flags; return 0; }
static HandlerVTable handler_vtable = {handler_query, handler_add_ref, handler_release, handler_ok, handler_edit, handler_ok, handler_restart};

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
typedef struct ProcessorVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*setBusArrangements)(void *, uint64_t *, int32_t, uint64_t *, int32_t);
    tresult (*getBusArrangement)(void *, int32_t, int32_t, uint64_t *);
    tresult (*canProcessSampleSize)(void *, int32_t);
    uint32 (*getLatencySamples)(void *);
    tresult (*setupProcessing)(void *, void *);
    tresult (*setProcessing)(void *, unsigned char);
    tresult (*process)(void *, void *);
    uint32 (*getTailSamples)(void *);
} ProcessorVTable;
typedef struct { ProcessorVTable *vtable; } Processor;
typedef struct ConnectionVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    tresult (*connect)(void *, void *);
    tresult (*disconnect)(void *, void *);
    tresult (*notify)(void *, void *);
} ConnectionVTable;
typedef struct { ConnectionVTable *vtable; } Connection;
typedef struct NoteOn { int16_t channel; int16_t pitch; float tuning; float velocity; int32_t length; int32_t noteId; } NoteOn;
typedef struct {
    int32_t busIndex; int32_t sampleOffset; double ppqPosition; uint16_t flags; uint16_t type;
    union { NoteOn noteOn; struct { int16_t channel; int16_t pitch; float velocity; int32_t noteId; float tuning; } noteOff; struct { uint32 typeId; int32_t noteId; uint32 textLen; const char16 *text; } text; } data;
} VstEvent;
typedef struct { int32_t numChannels; uint64_t silenceFlags; float **channelBuffers32; } AudioBuffers;
typedef struct {
    int32_t processMode; int32_t symbolicSampleSize; int32_t numSamples; int32_t numInputs; int32_t numOutputs;
    AudioBuffers *inputs; AudioBuffers *outputs; void *inputParameterChanges; void *outputParameterChanges;
    void *inputEvents; void *outputEvents; void *processContext;
} ProcessData;
typedef struct { int32_t processMode; int32_t symbolicSampleSize; int32_t maxSamplesPerBlock; double sampleRate; } ProcessSetup;
typedef struct {
    uint32 state; double sampleRate; int64_t projectTimeSamples; int64_t systemTime; int64_t continousTimeSamples;
    double projectTimeMusic; double barPositionMusic; double cycleStartMusic; double cycleEndMusic; double tempo;
    int32_t timeSigNumerator; int32_t timeSigDenominator; uint8_t keyNote; uint8_t rootNote; int16_t chordMask;
    int32_t smpteOffsetSubframes; uint32 framesPerSecond; uint32 frameFlags; int32_t samplesToNextClock;
} ProcessContext;
typedef struct EventListVTable {
    tresult (*queryInterface)(void *, const unsigned char *, void **);
    uint32 (*addRef)(void *);
    uint32 (*release)(void *);
    int32_t (*getEventCount)(void *);
    tresult (*getEvent)(void *, int32_t, void *);
    tresult (*addEvent)(void *, void *);
} EventListVTable;
typedef struct { EventListVTable *vtable; uint32 references; VstEvent events[2]; int32_t count; } EventList;
_Static_assert(sizeof(VstEvent) == 48, "VST3 event layout");
_Static_assert(offsetof(VstEvent, data) == 24, "VST3 event payload");
_Static_assert(sizeof(AudioBuffers) == 24, "VST3 audio bus layout");
_Static_assert(sizeof(ProcessData) == 80, "VST3 process data layout");
_Static_assert(sizeof(ProcessSetup) == 24, "VST3 process setup layout");
_Static_assert(offsetof(ProcessContext, tempo) == 72, "VST3 process context layout");

static tresult events_query(void *self, const unsigned char *iid, void **object) {
    EventList *list = self;
    if (!object) return 2;
    *object = NULL;
    if (memcmp(iid, unknown_iid, 16) != 0 && memcmp(iid, event_list_iid, 16) != 0) return -1;
    *object = self;
    list->references++;
    return 0;
}
static uint32 events_add(void *self) { return ++((EventList *)self)->references; }
static uint32 events_release(void *self) { EventList *list = self; if (list->references) list->references--; return list->references; }
static int32_t events_count(void *self) { return ((EventList *)self)->count; }
static tresult events_get(void *self, int32_t index, void *event) {
    EventList *list = self;
    if (!event || index < 0 || index >= list->count) return 2;
    memcpy(event, &list->events[index], sizeof(VstEvent));
    return 0;
}
static tresult events_add_event(void *self, void *event) { (void)self; (void)event; return -1; }
static EventListVTable event_vtable = {events_query, events_add, events_release, events_count, events_get, events_add_event};

static void write_u16(FILE *file, uint16_t value) { unsigned char bytes[2] = {value & 255, value >> 8}; fwrite(bytes, 1, 2, file); }
static void write_u32(FILE *file, uint32 value) { unsigned char bytes[4] = {value & 255, (value >> 8) & 255, (value >> 16) & 255, (value >> 24) & 255}; fwrite(bytes, 1, 4, file); }
static VstEvent note_event(int32_t offset, double ppq, uint16_t type, int16_t pitch) {
    VstEvent event = {0};
    event.sampleOffset = offset;
    event.ppqPosition = ppq;
    event.flags = 1;
    event.type = type;
    if (type == 0) {
        event.data.noteOn.pitch = pitch;
        event.data.noteOn.velocity = 0.9f;
        event.data.noteOn.length = 22050;
        event.data.noteOn.noteId = 1;
    } else {
        event.data.noteOff.pitch = pitch;
        event.data.noteOff.noteId = 1;
    }
    return event;
}
static const char *render_note(Component *component, Factory *factory, Host *host, const char *wav_path) {
    enum { kRate = 44100, kBlock = 512, kTotal = 44100, kMaxBuses = 8, kMaxChannels = 2 };
    Processor *processor = NULL;
    Component *controller = NULL;
    Connection *component_connection = NULL;
    Connection *controller_connection = NULL;
    bool connected = false;
    bool active = false;
    bool processing = false;
    const char *failure = NULL;
    if (!component->vtable->queryInterface || !component->vtable->activateBus || !component->vtable->setActive ||
        component->vtable->queryInterface(component, processor_iid, (void **)&processor) != 0 || !processor || !processor->vtable ||
        !processor->vtable->setupProcessing || !processor->vtable->setProcessing || !processor->vtable->process || !processor->vtable->release)
        return "ProcessorUnavailable";
    unsigned char controller_id[16];
    if (factory->vtable->createInstance && component->vtable->getControllerClassId &&
        component->vtable->getControllerClassId(component, controller_id) == 0) {
        if (factory->vtable->createInstance(factory, (const char *)controller_id, (const char *)controller_iid, (void **)&controller) == 0 &&
            controller && controller->vtable && controller->vtable->initialize && controller->vtable->terminate && controller->vtable->release &&
            controller->vtable->initialize(controller, host) == 0) {
            if (component->vtable->queryInterface(component, connection_iid, (void **)&component_connection) == 0 && component_connection &&
                controller->vtable->queryInterface(controller, connection_iid, (void **)&controller_connection) == 0 && controller_connection &&
                component_connection->vtable->connect && controller_connection->vtable->connect &&
                component_connection->vtable->connect(component_connection, controller_connection) == 0 &&
                controller_connection->vtable->connect(controller_connection, component_connection) == 0) connected = true;
        } else if (controller && controller->vtable && controller->vtable->release) {
            controller->vtable->release(controller);
            controller = NULL;
        }
    }
    int32_t inputs = component->vtable->getBusCount(component, 0, 0);
    int32_t outputs = component->vtable->getBusCount(component, 0, 1);
    if (!failure && (inputs < 0 || outputs < 1 || inputs > kMaxBuses || outputs > kMaxBuses)) failure = "ProcessingSetupFailed";
    uint64_t input_arrangement[kMaxBuses], output_arrangement[kMaxBuses];
    int32_t input_channels[kMaxBuses] = {0}, output_channels[kMaxBuses] = {0};
    for (int32_t index = 0; !failure && index < inputs; index++) {
        BusInfo info = {0};
        if (component->vtable->getBusInfo(component, 0, 0, index, &info) != 0 || info.channelCount < 0 || info.channelCount > 32) failure = "ProcessingSetupFailed";
        input_channels[index] = info.channelCount > kMaxChannels ? kMaxChannels : info.channelCount;
        input_arrangement[index] = input_channels[index] <= 1 ? (1ULL << 19) : 3;
        if (!failure && component->vtable->activateBus(component, 0, 0, index, info.flags & 1) != 0 && (info.flags & 1)) failure = "ProcessingSetupFailed";
    }
    for (int32_t index = 0; !failure && index < outputs; index++) {
        BusInfo info = {0};
        if (component->vtable->getBusInfo(component, 0, 1, index, &info) != 0 || info.channelCount < 0 || info.channelCount > 32) failure = "ProcessingSetupFailed";
        output_channels[index] = info.channelCount > kMaxChannels ? kMaxChannels : info.channelCount;
        if (index == 0 && output_channels[0] < 1) output_channels[0] = 2;
        output_arrangement[index] = output_channels[index] <= 1 ? (1ULL << 19) : 3;
        if (!failure && component->vtable->activateBus(component, 0, 1, index, 1) != 0 && index == 0) failure = "ProcessingSetupFailed";
    }
    int32_t event_inputs = !failure ? component->vtable->getBusCount(component, 1, 0) : 0;
    for (int32_t index = 0; !failure && index < event_inputs && index < 4; index++)
        component->vtable->activateBus(component, 1, 0, index, 1);
    if (!failure && processor->vtable->setBusArrangements)
        processor->vtable->setBusArrangements(processor, inputs ? input_arrangement : NULL, inputs, output_arrangement, outputs);
    for (int32_t index = 0; !failure && index < outputs; index++) {
        BusInfo info = {0};
        if (component->vtable->getBusInfo(component, 0, 1, index, &info) != 0) failure = "ProcessingSetupFailed";
        else output_channels[index] = info.channelCount > kMaxChannels ? kMaxChannels : info.channelCount;
    }
    if (!failure && output_channels[0] < 1) failure = "ProcessingSetupFailed";
    if (!failure && processor->vtable->canProcessSampleSize && processor->vtable->canProcessSampleSize(processor, 0) != 0) failure = "ProcessingSetupFailed";
    ProcessSetup setup = {0, 0, kBlock, kRate};
    if (!failure && processor->vtable->setupProcessing(processor, &setup) != 0) failure = "ProcessingSetupFailed";
    if (!failure && component->vtable->setActive(component, 1) != 0) failure = "ProcessingSetupFailed";
    else if (!failure) active = true;
    if (!failure && processor->vtable->setProcessing(processor, 1) != 0) failure = "ProcessingSetupFailed";
    else if (!failure) processing = true;
    FILE *wav = NULL;
    float samples[kMaxBuses * 2 * kMaxChannels * kBlock];
    float *input_ptrs[kMaxBuses][kMaxChannels];
    float *output_ptrs[kMaxBuses][kMaxChannels];
    AudioBuffers input_buses[kMaxBuses], output_buses[kMaxBuses];
    int16_t peak = 0;
    if (!failure) {
        completed_stage("ProcessorPrepared");
        CFRunLoopRunInMode(kCFRunLoopDefaultMode, 0.75, false);
        wav = fopen(wav_path, "wb");
        if (!wav) failure = "PreviewWriteFailed";
        else {
            fwrite("RIFF", 1, 4, wav); write_u32(wav, 36 + kTotal * 4); fwrite("WAVEfmt ", 1, 8, wav);
            write_u32(wav, 16); write_u16(wav, 1); write_u16(wav, 2); write_u32(wav, kRate); write_u32(wav, kRate * 4);
            write_u16(wav, 4); write_u16(wav, 16); fwrite("data", 1, 4, wav); write_u32(wav, kTotal * 4);
        }
    }
    EventList events;
    memset(&events, 0, sizeof(events));
    events.vtable = &event_vtable;
    events.references = 1;
    ProcessContext context = {0};
    context.state = (1 << 1) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 13) | (1 << 17);
    context.sampleRate = kRate;
    context.tempo = 120;
    context.timeSigNumerator = 4;
    context.timeSigDenominator = 4;
    for (int32_t rendered = 0; !failure && rendered < kTotal; rendered += kBlock) {
        int32_t count = kTotal - rendered > kBlock ? kBlock : kTotal - rendered;
        memset(samples, 0, sizeof(samples));
        float *cursor = samples;
        for (int32_t bus = 0; bus < inputs; bus++) {
            input_buses[bus].numChannels = input_channels[bus];
            input_buses[bus].silenceFlags = 0;
            input_buses[bus].channelBuffers32 = input_ptrs[bus];
            for (int32_t channel = 0; channel < input_channels[bus]; channel++) { input_ptrs[bus][channel] = cursor; cursor += kBlock; }
        }
        for (int32_t bus = 0; bus < outputs; bus++) {
            output_buses[bus].numChannels = output_channels[bus];
            output_buses[bus].silenceFlags = 0;
            output_buses[bus].channelBuffers32 = output_ptrs[bus];
            for (int32_t channel = 0; channel < output_channels[bus]; channel++) { output_ptrs[bus][channel] = cursor; cursor += kBlock; }
        }
        for (int32_t frame = 0; inputs > 0 && input_channels[0] > 0 && frame < count; frame++) {
            float sample = 0.2f * sinf((float)(rendered + frame) * 6.2831853f * 440.0f / (float)kRate);
            input_ptrs[0][0][frame] = sample;
            if (input_channels[0] > 1) input_ptrs[0][1][frame] = sample;
        }
        events.count = 0;
        if (rendered == 0) events.events[events.count++] = note_event(0, 0, 0, 40);
        if (rendered <= 22050 && rendered + count > 22050) events.events[events.count++] = note_event(22050 - rendered, 1.0, 1, 40);
        context.projectTimeSamples = rendered;
        context.continousTimeSamples = rendered;
        context.projectTimeMusic = (double)rendered / 22050.0;
        ProcessData data = {0, 0, count, inputs, outputs, inputs ? input_buses : NULL, output_buses, NULL, NULL, &events, NULL, &context};
        if (processor->vtable->process(processor, &data) != 0) failure = "ProcessingFailed";
        else for (int32_t frame = 0; frame < count; frame++) {
            float left = output_channels[0] > 0 ? output_ptrs[0][0][frame] : 0;
            float right = output_channels[0] > 1 ? output_ptrs[0][1][frame] : left;
            if (left > 1) left = 1; if (left < -1) left = -1; if (right > 1) right = 1; if (right < -1) right = -1;
            int16_t left_pcm = (int16_t)(left * 32767);
            int16_t right_pcm = (int16_t)(right * 32767);
            int16_t magnitude = left_pcm < 0 ? -left_pcm : left_pcm;
            int16_t other = right_pcm < 0 ? -right_pcm : right_pcm;
            if (other > magnitude) magnitude = other;
            if (magnitude > peak) peak = magnitude;
            write_u16(wav, (uint16_t)left_pcm);
            write_u16(wav, (uint16_t)right_pcm);
        }
    }
    if (wav) {
        if (!failure && (fflush(wav) != 0 || ferror(wav))) failure = "PreviewWriteFailed";
        fclose(wav);
    }
    if (!failure) {
        fprintf(protocol, "{\"protocolVersion\":1,\"status\":\"Preview\",\"stage\":\"PreviewRendered\",\"frames\":%d,\"sampleRate\":%d,\"peak\":%d,\"channels\":2,\"controllerConnected\":%s}\n",
            kTotal, kRate, peak, connected ? "true" : "false");
        fflush(protocol);
        completed_stage("PreviewRendered");
    }
    if (processing) processor->vtable->setProcessing(processor, 0);
    if (active) component->vtable->setActive(component, 0);
    if (connected) {
        if (component_connection->vtable->disconnect) component_connection->vtable->disconnect(component_connection, controller_connection);
        if (controller_connection->vtable->disconnect) controller_connection->vtable->disconnect(controller_connection, component_connection);
    }
    if (component_connection && component_connection->vtable->release) component_connection->vtable->release(component_connection);
    if (controller_connection && controller_connection->vtable->release) controller_connection->vtable->release(controller_connection);
    if (controller) { controller->vtable->terminate(controller); controller->vtable->release(controller); }
    processor->vtable->release(processor);
    return failure;
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
    if (argc != 3 && argc != 4 && argc != 5 && argc != 6) return 2;
    bool enumerate_factory = argc == 4 && strcmp(argv[3], "--enumerate-factory") == 0;
    bool inspect_component = argc == 5 && strcmp(argv[3], "--inspect-component") == 0;
    bool render_preview = argc == 6 && strcmp(argv[3], "--render-preview") == 0;
    unsigned char requested_id[16];
    if ((argc == 4 && !enumerate_factory) || (argc == 5 && (!inspect_component || !parse_id(argv[4], requested_id))) ||
        (argc == 6 && (!render_preview || !parse_id(argv[4], requested_id) || argv[5][0] != '/'))) return 2;
    if (render_preview) inspect_component = true;
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
                                Handler handler = {&handler_vtable, 1};
                                Host host = {&host_vtable, 1, &handler};
                                if (component->vtable->initialize(component, &host) != 0) failure = "ComponentInitializeFailed";
                                else {
                                    completed_stage("ComponentInitialized");
                                    if (render_preview) {
                                        const char *render_failure = render_note(component, factory, &host, argv[5]);
                                        if (render_failure) failure = render_failure;
                                    } else {
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
