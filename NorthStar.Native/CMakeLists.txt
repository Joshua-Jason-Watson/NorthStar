
cmake_minimum_required(VERSION 3.22)

project(NorthStar.Native VERSION 1.0.0 LANGUAGES C CXX)

if(NOT WIN32)
    message(FATAL_ERROR "NorthStar.Native currently supports Windows only.")
endif()

if(NOT MSVC)
    message(FATAL_ERROR "NorthStar.Native requires the MSVC compiler toolchain.")
endif()

if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "NorthStar.Native requires a 64-bit toolchain.")
endif()

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

set(LITERT_CC_SDK_DIR "" CACHE PATH "Path to the unpacked LiteRT C++ SDK")
set(LITERT_PREBUILT_DIR "" CACHE PATH "Path to the Windows x64 LiteRT prebuilt libraries")

if(NOT EXISTS "${LITERT_CC_SDK_DIR}/CMakeLists.txt")
    message(FATAL_ERROR "LITERT_CC_SDK_DIR does not contain the LiteRT SDK CMakeLists.txt.")
endif()

if(NOT EXISTS "${LITERT_PREBUILT_DIR}/libLiteRt.dll")
    message(FATAL_ERROR "LITERT_PREBUILT_DIR does not contain libLiteRt.dll.")
endif()

# Stage the SDK into the build tree without modifying the downloaded SDK.
set(_LITERT_SDK_STAGED "${CMAKE_BINARY_DIR}/litert_cc_sdk_staged")

file(MAKE_DIRECTORY "${_LITERT_SDK_STAGED}")

file(COPY "${LITERT_CC_SDK_DIR}/" DESTINATION "${_LITERT_SDK_STAGED}")
file(COPY "${LITERT_PREBUILT_DIR}/libLiteRt.dll" DESTINATION "${_LITERT_SDK_STAGED}")

add_subdirectory(
    "${_LITERT_SDK_STAGED}"
    "${CMAKE_BINARY_DIR}/litert_cc_sdk_build"
    EXCLUDE_FROM_ALL
)

add_library(NorthStar.Native SHARED
    src/NorthStar.Native.cpp
)

target_include_directories(NorthStar.Native
    PUBLIC
        "${CMAKE_CURRENT_SOURCE_DIR}/include"
)

target_compile_definitions(NorthStar.Native
    PRIVATE
        NORTHSTAR_NATIVE_EXPORTS
        NOMINMAX
)

target_compile_features(NorthStar.Native
    PRIVATE cxx_std_20
)

target_link_libraries(NorthStar.Native
    PRIVATE litert_cc_api
)

if(MSVC)
    target_compile_options(NorthStar.Native PRIVATE
        /W4
        /permissive-
        /EHsc
    )
endif()

set_target_properties(NorthStar.Native PROPERTIES
    OUTPUT_NAME "NorthStar.Native"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    WINDOWS_EXPORT_ALL_SYMBOLS OFF
)

# Copy the LiteRT runtime beside the bridge DLL.
add_custom_command(TARGET NorthStar.Native POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
        "${_LITERT_SDK_STAGED}/libLiteRt.dll"
        "$<TARGET_FILE_DIR:NorthStar.Native>/libLiteRt.dll"
    VERBATIM
)

add_executable(NorthStar.Native.SmokeTest
    tests/NativeSmokeTest.cpp
)

target_link_libraries(NorthStar.Native.SmokeTest
    PRIVATE NorthStar.Native
)

target_include_directories(NorthStar.Native.SmokeTest
    PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/include"
)

target_compile_definitions(NorthStar.Native.SmokeTest
    PRIVATE NOMINMAX
)

set_target_properties(NorthStar.Native.SmokeTest PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
)

add_custom_command(TARGET NorthStar.Native.SmokeTest POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
        "${CMAKE_CURRENT_SOURCE_DIR}/tests/models/add.bin"
        "$<TARGET_FILE_DIR:NorthStar.Native.SmokeTest>/add.bin"
    VERBATIM
)
