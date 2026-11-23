#include "NorthStar.Native.h"

#include <cmath>
#include <cstdint>
#include <filesystem>
#include <iostream>
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

    // Load the path as a narrow string.
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

    // This test model has a fixed input shape of 1x8x8x3.
    constexpr std::size_t input_element_count = 1 * 8 * 8 * 3;

    // Supply 192 float values, each containing 3.0.
    std::vector<float> input_data(input_element_count, 3.0F);

    const std::uint64_t input_size_bytes =
        static_cast<std::uint64_t>(
            input_data.size() * sizeof(float));

    if (!CheckStatus(
        NSN_SetModelInputTensorData(
            session.get(),
            0,
            input_data.data(),
            input_size_bytes),
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

    // Ask the native bridge how large the output is.
    std::uint64_t output_size_bytes = 0;

    if (!CheckStatus(
        NSN_GetModelOutputTensorDataSize(
            session.get(),
            0,
            &output_size_bytes),
        "NSN_GetModelOutputTensorDataSize"))
    {
        return 1;
    }

    std::vector<float> output_data(input_element_count);

    const std::uint64_t expected_output_size =
        static_cast<std::uint64_t>(
            output_data.size() * sizeof(float));

    if (output_size_bytes != expected_output_size)
    {
        std::cerr
            << "Unexpected output size. Expected "
            << expected_output_size
            << " bytes, received "
            << output_size_bytes
            << " bytes.\n";

        return 1;
    }

    // Retrieve the inference results.
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

    // Verify every output value.
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
        << "Verified output values: " << output_data.size() << '\n'
        << "Expected output: 9.0\n"
        << "Actual output: " << output_data[0] << '\n'
        << "NorthStar.Native inference integration test passed.\n";

    return 0;
}