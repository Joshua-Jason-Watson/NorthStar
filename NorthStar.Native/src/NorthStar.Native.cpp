#include "NorthStar.Native.h"

#include <litert/cc/litert_common.h>
#include <litert/cc/litert_compiled_model.h>
#include <litert/cc/litert_environment.h>
#include <litert/cc/litert_environment_options.h>
#include <litert/cc/litert_tensor_buffer.h>

#include <absl/types/span.h>

#include <cstdio>
#include <exception>
#include <limits>
#include <memory>
#include <new>
#include <string>
#include <utility>
#include <vector>

namespace northstar_native_detail
{
    thread_local char last_error[4096] = {};

    void ClearLastError() noexcept
    {
        last_error[0] = '\0';
    }

    NSN_Status SetFailure(
        NSN_Status status,
        const char* operation,
        const char* message) noexcept
    {
        std::snprintf(
            last_error,
            sizeof(last_error),
            "%s: %s",
            operation != nullptr ? operation : "NorthStar.Native",
            message != nullptr ? message : "Unknown error.");

        return status;
    }

    NSN_Status SetLiteRtFailure(
        NSN_Status status,
        const char* operation,
        const litert::Error& error) noexcept
    {
        return SetFailure(status, operation, error.Message().c_str());
    }

    struct RuntimeState final
    {
        explicit RuntimeState(litert::Environment environment_value)
            : environment(std::move(environment_value))
        {
        }

        // The environment is declared first so it is destroyed after the
        // objects which depend on it.
        litert::Environment environment;
    };
}

struct NSN_Runtime final
{
    explicit NSN_Runtime(
        std::shared_ptr<northstar_native_detail::RuntimeState> state_value)
        : state(std::move(state_value))
    {
    }

    std::shared_ptr<northstar_native_detail::RuntimeState> state;
};

struct NSN_ModelSession final
{
    NSN_ModelSession(
        std::shared_ptr<northstar_native_detail::RuntimeState> runtime_value,
        litert::CompiledModel compiled_model_value,
        std::vector<litert::TensorBuffer> input_buffers_value,
        std::vector<litert::TensorBuffer> output_buffers_value)
        : runtime(std::move(runtime_value)),
          compiled_model(std::move(compiled_model_value)),
          input_buffers(std::move(input_buffers_value)),
          output_buffers(std::move(output_buffers_value))
    {
    }

    // Keep the shared environment alive for at least as long as the compiled
    // model and its tensor buffers.
    std::shared_ptr<northstar_native_detail::RuntimeState> runtime;
    litert::CompiledModel compiled_model;
    std::vector<litert::TensorBuffer> input_buffers;
    std::vector<litert::TensorBuffer> output_buffers;
};

uint32_t NORTHSTAR_NATIVE_CALL NSN_GetApiVersion(void)
{
    return 0x00010000;
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_CreateRuntime(NSN_Runtime** out_runtime)
{
    using namespace northstar_native_detail;
    ClearLastError();

    if (out_runtime == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_CreateRuntime",
            "out_runtime cannot be null.");
    }

    *out_runtime = nullptr;

    try
    {
        // This first bridge milestone deliberately registers CPU only. GPU
        // registration will be added after the CPU model-loading path is tested.
        const std::vector<litert::EnvironmentOptions::Option> environment_options = {
            {
                litert::EnvironmentOptions::Tag::kAutoRegisterAccelerators,
                static_cast<int64_t>(kLiteRtHwAcceleratorCpu)
            }
        };

        const litert::EnvironmentOptions options(
            litert::Span<const litert::EnvironmentOptions::Option>(
                environment_options.data(), environment_options.size()));

        auto environment_result = litert::Environment::Create(options);
        if (!environment_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_RUNTIME_INITIALIZATION_FAILED,
                "LiteRT Environment::Create",
                environment_result.Error());
        }

        auto environment = std::move(environment_result.Value());
        auto state = std::make_shared<RuntimeState>(std::move(environment));
        auto runtime = std::make_unique<NSN_Runtime>(std::move(state));

        *out_runtime = runtime.release();
        return NSN_STATUS_OK;
    }
    catch (const std::bad_alloc&)
    {
        return SetFailure(
            NSN_STATUS_OUT_OF_MEMORY,
            "NSN_CreateRuntime",
            "Memory allocation failed.");
    }
    catch (const std::exception& exception)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_CreateRuntime",
            exception.what());
    }
    catch (...)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_CreateRuntime",
            "An unknown native exception occurred.");
    }
}

void NORTHSTAR_NATIVE_CALL NSN_DestroyRuntime(NSN_Runtime* runtime)
{
    delete runtime;
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_LoadModel(
    NSN_Runtime* runtime,
    const char* model_path_utf8,
    NSN_ModelSession** out_session)
{
    using namespace northstar_native_detail;
    ClearLastError();

    if (out_session == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_LoadModel",
            "out_session cannot be null.");
    }

    *out_session = nullptr;

    if (runtime == nullptr || !runtime->state ||
        model_path_utf8 == nullptr || model_path_utf8[0] == '\0')
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_LoadModel",
            "A valid runtime and non-empty model path are required.");
    }

    try
    {
        auto compiled_model_result = litert::CompiledModel::Create(
            runtime->state->environment,
            std::string(model_path_utf8),
            litert::HwAccelerators::kCpu);

        if (!compiled_model_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_MODEL_COMPILATION_FAILED,
                "LiteRT CompiledModel::Create",
                compiled_model_result.Error());
        }

        auto compiled_model = std::move(compiled_model_result.Value());

        auto input_buffers_result = compiled_model.CreateInputBuffers();
        if (!input_buffers_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_CREATION_FAILED,
                "LiteRT CompiledModel::CreateInputBuffers",
                input_buffers_result.Error());
        }

        auto input_buffers = std::move(input_buffers_result.Value());

        auto output_buffers_result = compiled_model.CreateOutputBuffers();
        if (!output_buffers_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_CREATION_FAILED,
                "LiteRT CompiledModel::CreateOutputBuffers",
                output_buffers_result.Error());
        }

        auto output_buffers = std::move(output_buffers_result.Value());

        auto session = std::make_unique<NSN_ModelSession>(
            runtime->state,
            std::move(compiled_model),
            std::move(input_buffers),
            std::move(output_buffers));

        *out_session = session.release();
        return NSN_STATUS_OK;
    }
    catch (const std::bad_alloc&)
    {
        return SetFailure(
            NSN_STATUS_OUT_OF_MEMORY,
            "NSN_LoadModel",
            "Memory allocation failed.");
    }
    catch (const std::exception& exception)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_LoadModel",
            exception.what());
    }
    catch (...)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_LoadModel",
            "An unknown native exception occurred.");
    }
}

void NORTHSTAR_NATIVE_CALL NSN_DestroyModelSession(NSN_ModelSession* session)
{
    delete session;
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_SetModelInputTensorData(
    NSN_ModelSession* session,
    uint32_t input_index,
    const void* data,
    uint64_t data_size)
{
    using namespace northstar_native_detail;

    ClearLastError();

    if (session == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_SetModelInputTensorData",
            "The model session cannot be null.");
    }

    if (input_index >= session->input_buffers.size())
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_SetModelInputTensorData",
            "The input tensor index is out of range.");
    }

    if (data == nullptr || data_size == 0)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_SetModelInputTensorData",
            "Data cannot be null or empty.");
    }

    if (data_size >
        static_cast<uint64_t>(std::numeric_limits<size_t>::max()))
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_SetModelInputTensorData",
            "The supplied data size exceeds the native size limit.");
    }

    try
    {
        litert::TensorBuffer& input_buffer =
            session->input_buffers[input_index];

        auto packed_size_result =
            input_buffer.PackedSize();

        if (!packed_size_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_WRITE_FAILED,
                "LiteRT TensorBuffer::PackedSize",
                packed_size_result.Error());
        }

        const size_t expected_size =
            packed_size_result.Value();

        if (data_size != static_cast<uint64_t>(expected_size))
        {
            char message[256]{};

            std::snprintf(
                message,
                sizeof(message),
                "Input tensor %u requires exactly %zu bytes, "
                "but %llu bytes were provided.",
                input_index,
                expected_size,
                static_cast<unsigned long long>(data_size));

            return SetFailure(
                NSN_STATUS_INVALID_ARGUMENT,
                "NSN_SetModelInputTensorData",
                message);
        }

        const absl::Span<const uint8_t> input_data(
            static_cast<const uint8_t*>(data),
            static_cast<size_t>(data_size));

        auto write_result =
            input_buffer.Write<uint8_t>(input_data);

        if (!write_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_WRITE_FAILED,
                "LiteRT TensorBuffer::Write",
                write_result.Error());
        }

        return NSN_STATUS_OK;
    }
    catch (const std::bad_alloc&)
    {
        return SetFailure(
            NSN_STATUS_OUT_OF_MEMORY,
            "NSN_SetModelInputTensorData",
            "Memory allocation failed.");
    }
    catch (const std::exception& exception)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_SetModelInputTensorData",
            exception.what());
    }
    catch (...)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_SetModelInputTensorData",
            "An unknown native exception occurred.");
    }
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_GetModelOutputTensorDataSize(
    const NSN_ModelSession* session,
    uint32_t output_index,
    uint64_t* out_data_size)
{
    using namespace northstar_native_detail;

    ClearLastError();

    if (session == nullptr || out_data_size == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorDataSize",
            "session and out_data_size cannot be null.");
    }

    *out_data_size = 0;

    if (output_index >= session->output_buffers.size())
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorDataSize",
            "The output tensor index is out of range.");
    }

    try
    {
        const litert::TensorBuffer& output_buffer =
            session->output_buffers[output_index];

        auto packed_size_result =
            output_buffer.PackedSize();

        if (!packed_size_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_READ_FAILED,
                "LiteRT TensorBuffer::PackedSize",
                packed_size_result.Error());
        }

        *out_data_size =
            static_cast<uint64_t>(packed_size_result.Value());

        return NSN_STATUS_OK;
    }
    catch (const std::exception& exception)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelOutputTensorDataSize",
            exception.what());
    }
    catch (...)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelOutputTensorDataSize",
            "An unknown native exception occurred.");
    }
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_GetModelOutputTensorData(
    NSN_ModelSession* session,
    uint32_t output_index,
    void* destination,
    uint64_t destination_size)
{
    using namespace northstar_native_detail;

    ClearLastError();

    if (session == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorData",
            "The model session cannot be null.");
    }

    if (output_index >= session->output_buffers.size())
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorData",
            "The output tensor index is out of range.");
    }

    if (destination == nullptr || destination_size == 0)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorData",
            "The destination cannot be null or empty.");
    }

    if (destination_size >
        static_cast<uint64_t>(std::numeric_limits<size_t>::max()))
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputTensorData",
            "The destination size exceeds the native size limit.");
    }

    try
    {
        litert::TensorBuffer& output_buffer =
            session->output_buffers[output_index];

        auto packed_size_result =
            output_buffer.PackedSize();

        if (!packed_size_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_READ_FAILED,
                "LiteRT TensorBuffer::PackedSize",
                packed_size_result.Error());
        }

        const size_t expected_size =
            packed_size_result.Value();

        if (destination_size != static_cast<uint64_t>(expected_size))
        {
            char message[256]{};

            std::snprintf(
                message,
                sizeof(message),
                "Output tensor %u requires exactly %zu bytes, "
                "but the destination has %llu bytes.",
                output_index,
                expected_size,
                static_cast<unsigned long long>(destination_size));

            return SetFailure(
                NSN_STATUS_INVALID_ARGUMENT,
                "NSN_GetModelOutputTensorData",
                message);
        }

        const absl::Span<uint8_t> output_data(
            static_cast<uint8_t*>(destination),
            static_cast<size_t>(destination_size));

        auto read_result =
            output_buffer.Read<uint8_t>(output_data);

        if (!read_result.HasValue())
        {
            return SetLiteRtFailure(
                NSN_STATUS_TENSOR_BUFFER_READ_FAILED,
                "LiteRT TensorBuffer::Read",
                read_result.Error());
        }

        return NSN_STATUS_OK;
    }
    catch (const std::bad_alloc&)
    {
        return SetFailure(
            NSN_STATUS_OUT_OF_MEMORY,
            "NSN_GetModelOutputTensorData",
            "Memory allocation failed.");
    }
    catch (const std::exception& exception)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelOutputTensorData",
            exception.what());
    }
    catch (...)
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelOutputTensorData",
            "An unknown native exception occurred.");
    }
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_GetModelInputCount(
    const NSN_ModelSession* session,
    uint32_t* out_count)
{
    using namespace northstar_native_detail;
    ClearLastError();

    if (session == nullptr || out_count == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelInputCount",
            "session and out_count cannot be null.");
    }

    if (session->input_buffers.size() > std::numeric_limits<uint32_t>::max())
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelInputCount",
            "The input tensor count exceeds the native ABI limit.");
    }

    *out_count = static_cast<uint32_t>(session->input_buffers.size());
    return NSN_STATUS_OK;
}

NSN_Status NORTHSTAR_NATIVE_CALL NSN_GetModelOutputCount(
    const NSN_ModelSession* session,
    uint32_t* out_count)
{
    using namespace northstar_native_detail;
    ClearLastError();

    if (session == nullptr || out_count == nullptr)
    {
        return SetFailure(
            NSN_STATUS_INVALID_ARGUMENT,
            "NSN_GetModelOutputCount",
            "session and out_count cannot be null.");
    }

    if (session->output_buffers.size() > std::numeric_limits<uint32_t>::max())
    {
        return SetFailure(
            NSN_STATUS_INTERNAL_ERROR,
            "NSN_GetModelOutputCount",
            "The output tensor count exceeds the native ABI limit.");
    }

    *out_count = static_cast<uint32_t>(session->output_buffers.size());
    return NSN_STATUS_OK;
}

const char* NORTHSTAR_NATIVE_CALL NSN_GetLastError(void)
{
    return northstar_native_detail::last_error;
}
