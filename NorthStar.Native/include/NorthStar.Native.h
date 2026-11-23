#pragma once

#include <stdint.h>

#if defined(_WIN32)
    #if defined(NORTHSTAR_NATIVE_EXPORTS)
        #define NORTHSTAR_NATIVE_API __declspec(dllexport)
    #else
        #define NORTHSTAR_NATIVE_API __declspec(dllimport)
    #endif
    #define NORTHSTAR_NATIVE_CALL __cdecl
#else
    #define NORTHSTAR_NATIVE_API
    #define NORTHSTAR_NATIVE_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

// Opaque handles keep LiteRT's C++ implementation details out of the C ABI.
typedef struct NSN_Runtime NSN_Runtime;
typedef struct NSN_ModelSession NSN_ModelSession;

typedef enum NSN_Status
{
    NSN_STATUS_OK = 0,
    NSN_STATUS_INVALID_ARGUMENT = 1,
    NSN_STATUS_RUNTIME_INITIALIZATION_FAILED = 2,
    NSN_STATUS_MODEL_COMPILATION_FAILED = 3,
    NSN_STATUS_TENSOR_BUFFER_CREATION_FAILED = 4,
    NSN_STATUS_OUT_OF_MEMORY = 5,
    NSN_STATUS_INTERNAL_ERROR = 6,
    NSN_STATUS_TENSOR_BUFFER_WRITE_FAILED = 7,
    NSN_STATUS_TENSOR_BUFFER_READ_FAILED = 8,
    NSN_STATUS_MODEL_INFERENCE_FAILED = 9
} NSN_Status;

// Executes inference using the model's current input buffers.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_RunModel(NSN_ModelSession* session);

// API version 1.0.0 is returned as 0x00010000.
NORTHSTAR_NATIVE_API uint32_t NORTHSTAR_NATIVE_CALL
NSN_GetApiVersion(void);

// Creates a shared LiteRT runtime environment.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_CreateRuntime(NSN_Runtime** out_runtime);

// Destroys the runtime handle. Existing model sessions retain shared ownership
// of the underlying LiteRT environment until those sessions are destroyed.
NORTHSTAR_NATIVE_API void NORTHSTAR_NATIVE_CALL
NSN_DestroyRuntime(NSN_Runtime* runtime);

// Loads a .tflite model using the CPU backend and pre-allocates reusable
// input/output tensor buffers. model_path_utf8 must be UTF-8 encoded.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_LoadModel(
    NSN_Runtime* runtime,
    const char* model_path_utf8,
    NSN_ModelSession** out_session);

NORTHSTAR_NATIVE_API void NORTHSTAR_NATIVE_CALL
NSN_DestroyModelSession(NSN_ModelSession* session);

NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_GetModelInputCount(
    const NSN_ModelSession* session,
    uint32_t* out_count);

NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_GetModelOutputCount(
    const NSN_ModelSession* session,
    uint32_t* out_count);

// Copies raw tensor data into a model's input buffer.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_SetModelInputTensorData(
    NSN_ModelSession* session,
    uint32_t input_index,
    const void* data,
    uint64_t data_size);

// Gets the required byte size of a model's output tensor.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_GetModelOutputTensorDataSize(
    const NSN_ModelSession* session,
    uint32_t output_index,
    uint64_t* out_data_size);

// Copies raw tensor data from a model's output buffer.
NORTHSTAR_NATIVE_API NSN_Status NORTHSTAR_NATIVE_CALL
NSN_GetModelOutputTensorData(
    NSN_ModelSession* session,
    uint32_t output_index,
    void* destination,
    uint64_t destination_size);

// Returns a thread-local UTF-8 error string. Read it immediately after a
// function returns a non-OK status; the pointer remains valid until the next
// NorthStar.Native call on the same thread.
NORTHSTAR_NATIVE_API const char* NORTHSTAR_NATIVE_CALL
NSN_GetLastError(void);

#ifdef __cplusplus
} // extern "C"
#endif
