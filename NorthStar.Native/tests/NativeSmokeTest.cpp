#include "NorthStar.Native.h"

#include <cmath>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <iostream>
#include <limits>
#include <memory>
#include <string>
#include <vector>

namespace
{
    bool CheckStatus(
        NSN_Status status,
        const char* operation)
    {
        if (status == NSN_STATUS_OK)
        {
            return true;
        }

        const char* error = NSN_GetLastError();

        std::cerr
            << operation
            << " failed with status "
            << static_cast<int>(status);

        if (error != nullptr && error[0] != '\0')
        {
            std::cerr << ": " << error;
        }

        std::cerr << '\n';
        return false;
    }

    struct RuntimeDeleter
    {
        void operator()(NSN_Runtime* runtime) const noexcept
        {
            NSN_DestroyRuntime(runtime);
        }
    };

    struct ModelSessionDeleter
    {
        void operator()(NSN_ModelSession* session) const noexcept
        {
            NSN_DestroyModelSession(session);
        }
    };

    bool ValidateFloat32Tensor(
        const NSN_TensorInfo& tensor_info,
        const char* tensor_name)
    {
        if (tensor_info.element_type != NSN_TENSOR_ELEMENT_TYPE_FLOAT32)
        {
            std::cerr << tensor_name << " is not a Float32 tensor.\n";
            return false;
        }

        if (tensor_info.rank == 0 ||
            tensor_info.rank > NSN_MAX_TENSOR_RANK)
        {
            std::cerr << tensor_name << " has an invalid tensor rank.\n";
            return false;
        }

        if (tensor_info.packed_size == 0 ||
            tensor_info.packed_size % sizeof(float) != 0)
        {
            std::cerr << tensor_name << " has an invalid packed size.\n";
            return false;
        }

        if (tensor_info.packed_size / sizeof(float) >
            static_cast<uint64_t>(
                std::numeric_limits<std::size_t>::max()))
        {
            std::cerr
                << tensor_name
                << " exceeds the native addressable memory limit.\n";
            return false;
        }

        return true;
    }

    bool ValidateTensorShape(
        const NSN_TensorInfo& actual,
        const std::int32_t* expected_dimensions,
        std::uint32_t expected_rank,
        const char* tensor_name)
    {
        if (actual.rank != expected_rank)
        {
            std::cerr
                << tensor_name
                << " has an unexpected rank. Expected "
                << expected_rank
                << ", received "
                << actual.rank
                << ".\n";
            return false;
        }

        for (std::uint32_t i = 0; i < expected_rank; ++i)
        {
            if (actual.dimensions[i] != expected_dimensions[i])
            {
                std::cerr
                    << tensor_name
                    << " has an unexpected dimension at index "
                    << i
                    << ". Expected "
                    << expected_dimensions[i]
                    << ", received "
                    << actual.dimensions[i]
                    << ".\n";
                return false;
            }
        }

        return true;
    }
}

int main(int, char* argv[])
{
    // Locate add.bin beside the executable.
    const std::filesystem::path executable_path =
        std::filesystem::absolute(argv[0]);

    const std::filesystem::path model_path =
        executable_path.parent_path() / "add.bin";

    if (!std::filesystem::exists(model_path))
    {
        std::cerr
            << "Test model not found: "
            << model_path.string()
            << '\n';
        return 1;
    }

    const std::string model_path_string = model_path.string();

    // Create the LiteRT runtime.
    NSN_Runtime* runtime_raw = nullptr;

    if (!CheckStatus(
        NSN_CreateRuntime(&runtime_raw),
        "NSN_CreateRuntime"))
    {
        return 1;
    }

    std::unique_ptr<NSN_Runtime, RuntimeDeleter> runtime(runtime_raw);

    // Load and compile the test model.
    NSN_ModelSession* session_raw = nullptr;

    if (!CheckStatus(
        NSN_LoadModel(
            runtime.get(),
            model_path_string.c_str(),
            &session_raw),
        "NSN_LoadModel"))
    {
        return 1;
    }

    std::unique_ptr<NSN_ModelSession, ModelSessionDeleter> session(
        session_raw);

    // Verify the model's input and output counts.
    uint32_t input_count = 0;
    uint32_t output_count = 0;

    if (!CheckStatus(
        NSN_GetModelInputCount(session.get(), &input_count),
        "NSN_GetModelInputCount"))
    {
        return 1;
    }

    if (!CheckStatus(
        NSN_GetModelOutputCount(session.get(), &output_count),
        "NSN_GetModelOutputCount"))
    {
        return 1;
    }

    if (input_count != 1 || output_count != 1)
    {
        std::cerr
            << "Unexpected tensor counts. Inputs: "
            << input_count
            << ", outputs: "
            << output_count
            << '\n';
        return 1;
    }

    // Query input tensor metadata.
    NSN_TensorInfo input_info{};

    if (!CheckStatus(
        NSN_GetModelInputTensorInfo(
            session.get(),
            0,
            &input_info),
        "NSN_GetModelInputTensorInfo"))
    {
        return 1;
    }

    // Query output tensor metadata.
    NSN_TensorInfo output_info{};

    if (!CheckStatus(
        NSN_GetModelOutputTensorInfo(
            session.get(),
            0,
            &output_info),
        "NSN_GetModelOutputTensorInfo"))
    {
        return 1;
    }

    if (!ValidateFloat32Tensor(input_info, "Input tensor") ||
        !ValidateFloat32Tensor(output_info, "Output tensor"))
    {
        return 1;
    }

    // add.bin is expected to use the shape 1x8x8x3 for input and output.
    constexpr std::int32_t expected_dimensions[] = { 1, 8, 8, 3 };
    constexpr std::uint32_t expected_rank = 4;

    if (!ValidateTensorShape(
        input_info,
        expected_dimensions,
        expected_rank,
        "Input tensor"))
    {
        return 1;
    }

    if (!ValidateTensorShape(
        output_info,
        expected_dimensions,
        expected_rank,
        "Output tensor"))
    {
        return 1;
    }

    const std::size_t input_element_count =
        static_cast<std::size_t>(
            input_info.packed_size / sizeof(float));

    const std::size_t output_element_count =
        static_cast<std::size_t>(
            output_info.packed_size / sizeof(float));

    // Supply 192 float values, each containing 3.0.
    std::vector<float> input_data(input_element_count, 3.0F);

    if (!CheckStatus(
        NSN_SetModelInputTensorData(
            session.get(),
            0,
            input_data.data(),
            input_info.packed_size),
        "NSN_SetModelInputTensorData"))
    {
        return 1;
    }

    // Execute inference.
    if (!CheckStatus(
        NSN_RunModel(session.get()),
        "NSN_RunModel"))
    {
        return 1;
    }

    // Confirm the output size reported by the size API matches metadata.
    uint64_t output_size_bytes = 0;

    if (!CheckStatus(
        NSN_GetModelOutputTensorDataSize(
            session.get(),
            0,
            &output_size_bytes),
        "NSN_GetModelOutputTensorDataSize"))
    {
        return 1;
    }

    if (output_size_bytes != output_info.packed_size)
    {
        std::cerr
            << "Output size does not match tensor metadata. Expected "
            << output_info.packed_size
            << " bytes, received "
            << output_size_bytes
            << " bytes.\n";
        return 1;
    }

    std::vector<float> output_data(output_element_count);

    if (!CheckStatus(
        NSN_GetModelOutputTensorData(
            session.get(),
            0,
            output_data.data(),
            output_size_bytes),
        "NSN_GetModelOutputTensorData"))
    {
        return 1;
    }

    if (output_data.size() != input_data.size())
    {
        std::cerr << "Input and output element counts do not match.\n";
        return 1;
    }

    for (std::size_t i = 0; i < output_data.size(); ++i)
    {
        const float expected = input_data[i] * 3.0F;

        if (!std::isfinite(output_data[i]) ||
            std::fabs(output_data[i] - expected) > 0.00001F)
        {
            std::cerr
                << "Incorrect output at index "
                << i
                << ". Expected "
                << expected
                << ", received "
                << output_data[i]
                << '\n';
            return 1;
        }
    }

    std::cout
        << "Model loaded successfully.\n"
        << "Input tensors: " << input_count << '\n'
        << "Output tensors: " << output_count << '\n'
        << "Input element type: Float32\n"
        << "Input rank: " << input_info.rank << '\n'
        << "Input elements: " << input_element_count << '\n'
        << "Input size: " << input_info.packed_size << " bytes\n"
        << "Output size: " << output_info.packed_size << " bytes\n"
        << "Verified output values: " << output_data.size() << '\n'
        << "Expected output: 9.0\n"
        << "Actual output: " << output_data[0] << '\n'
        << "NorthStar.Native inference integration test passed.\n";

    return 0;
}
