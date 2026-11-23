
using NorthStar.Camera.Interop;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace NorthStar.Camera
{
    // Discovers cameras exposed through Windows Media Foundation.
    //
    // Discovery produces immutable camera descriptions.
    // It does not create or retain long-lived CameraDevice instances.
    public sealed class WindowsCameraDiscovery
    {
        // ============================================================
        // Media Foundation constants
        // ============================================================

        private const uint FirstVideoStream =
            unchecked(
                (uint)
                MF_SOURCE_READER_CONSTANTS
                    .MF_SOURCE_READER_FIRST_VIDEO_STREAM);

        private static readonly Guid MediaSourceGuid =
            new("279A808D-AEC7-40C8-9C6B-A6B492C78A66");

        private const int MfENoMoreTypes =
            unchecked((int)0xC00D36B9);


        // ============================================================
        // Camera discovery
        // ============================================================

        public unsafe IReadOnlyList<CameraDescriptor> Discover()
        {
            PInvoke.MFCreateAttributes(
                out var attributes,
                1);

            try
            {
                ConfigureDiscoveryAttributes(attributes);

                IMFActivate_unmanaged** devices = null;

                uint deviceCount = 0;

                try
                {
                    PInvoke.MFEnumDeviceSources(
                        attributes,
                        out devices,
                        out deviceCount);

                    List<CameraDescriptor> descriptors =
                        new(checked((int)deviceCount));

                    if (devices == null || deviceCount == 0)
                    {
                        return descriptors.AsReadOnly();
                    }

                    for (uint i = 0; i < deviceCount; i++)
                    {
                        IMFActivate_unmanaged* device =
                            devices[i];

                        if (device == null)
                        {
                            continue;
                        }

                        descriptors.Add(
                            DiscoverCamera(device));
                    }

                    return descriptors.AsReadOnly();
                }
                finally
                {
                    // Every activation reference returned by
                    // MFEnumDeviceSources must be released.
                    //
                    // Cleanup happens even if discovery of an
                    // individual camera throws an exception.
                    if (devices != null)
                    {
                        for (uint i = 0; i < deviceCount; i++)
                        {
                            IMFActivate_unmanaged* device =
                                devices[i];

                            if (device != null)
                            {
                                device->Release();
                            }
                        }

                        PInvoke.CoTaskMemFree(devices);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(attributes);
            }
        }


        // ============================================================
        // Device enumeration configuration
        // ============================================================

        private static unsafe void ConfigureDiscoveryAttributes(
            IMFAttributes attributes)
        {
            Guid sourceTypeKey =
                PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE;

            Guid videoCaptureType =
                PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;

            attributes.SetGUID(
                &sourceTypeKey,
                &videoCaptureType);
        }


        // ============================================================
        // Camera descriptor creation
        // ============================================================

        private static unsafe CameraDescriptor DiscoverCamera(
            IMFActivate_unmanaged* device)
        {
            string name =
                GetDeviceString(
                    device,
                    PInvoke.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME);

            string identifier =
                GetDeviceString(
                    device,
                    PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK);

            IReadOnlyList<CameraCapability> capabilities =
                DiscoverCapabilities(device);

            return new CameraDescriptor(
                name,
                identifier,
                capabilities);
        }


        // ============================================================
        // Device information
        // ============================================================

        private static unsafe string GetDeviceString(
            IMFActivate_unmanaged* device,
            Guid attributeKey)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            PWSTR value = default;

            device->GetAllocatedString(
                &attributeKey,
                &value,
                out _);

            try
            {
                if (value.Value == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned an empty camera attribute.");
                }

                return value.ToString();
            }
            finally
            {
                if (value.Value != null)
                {
                    PInvoke.CoTaskMemFree(value.Value);
                }
            }
        }


        // ============================================================
        // Camera capability discovery
        // ============================================================

        private static unsafe IReadOnlyList<CameraCapability>
            DiscoverCapabilities(
                IMFActivate_unmanaged* device)
        {
            void* sourcePointer =
                device->ActivateObject(
                    MediaSourceGuid);

            if (sourcePointer == null)
            {
                throw new InvalidOperationException(
                    "Media Foundation returned a null camera media source.");
            }

            try
            {
                void* readerPointer = null;

                try
                {
                    HRESULT hr =
                        MediaFoundationNative
                            .MFCreateSourceReaderFromMediaSource(
                                sourcePointer,
                                null,
                                &readerPointer);

                    hr.ThrowOnFailure();

                    if (readerPointer == null)
                    {
                        throw new InvalidOperationException(
                            "Media Foundation returned a null source reader.");
                    }

                    // RawSourceReader assumes ownership of the
                    // native pointer, including on constructor failure.
                    void* ownedReaderPointer =
                        readerPointer;

                    readerPointer = null;

                    using RawSourceReader sourceReader =
                        new RawSourceReader(
                            ownedReaderPointer);

                    return EnumerateCapabilities(sourceReader);
                }
                finally
                {
                    // Releases the pointer only if ownership
                    // was not successfully transferred.
                    if (readerPointer != null)
                    {
                        ReleaseComPointer(readerPointer);
                    }
                }
            }
            finally
            {
                // Each discovery operation creates a temporary
                // media source. It must not survive discovery.
                ReleaseComPointer(sourcePointer);
            }
        }


        // ============================================================
        // Native media type enumeration
        // ============================================================

        private static unsafe IReadOnlyList<CameraCapability>
            EnumerateCapabilities(
                RawSourceReader sourceReader)
        {
            List<CameraCapability> capabilities = new();

            uint mediaTypeIndex = 0;

            while (true)
            {
                void* mediaTypePointer = null;

                HRESULT hr =
                    sourceReader.GetNativeMediaType(
                        FirstVideoStream,
                        mediaTypeIndex,
                        out mediaTypePointer);

                if (hr.Value == MfENoMoreTypes)
                {
                    break;
                }

                hr.ThrowOnFailure();

                if (mediaTypePointer == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null media type.");
                }

                using RawMediaType mediaType =
                    new RawMediaType(
                        mediaTypePointer);

                CameraCapability capability =
                    ReadCapability(
                        mediaTypeIndex,
                        mediaType);

                capabilities.Add(capability);

                mediaTypeIndex++;
            }

            return capabilities.AsReadOnly();
        }


        // ============================================================
        // Capability extraction
        // ============================================================

        private static CameraCapability ReadCapability(
            uint mediaTypeIndex,
            RawMediaType mediaType)
        {
            HRESULT hr =
                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_SIZE,
                    out ulong frameSize);

            hr.ThrowOnFailure();

            int width =
                checked((int)(frameSize >> 32));

            int height =
                checked((int)(frameSize & 0xFFFFFFFF));

            hr =
                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_RATE,
                    out ulong frameRate);

            hr.ThrowOnFailure();

            uint numerator =
                (uint)(frameRate >> 32);

            uint denominator =
                (uint)(frameRate & 0xFFFFFFFF);

            if (numerator == 0 || denominator == 0)
            {
                throw new InvalidOperationException(
                    "Media Foundation reported an invalid frame rate.");
            }

            hr =
                mediaType.GetGUID(
                    PInvoke.MF_MT_SUBTYPE,
                    out Guid subtype);

            hr.ThrowOnFailure();

            return new CameraCapability(
                mediaTypeIndex,
                width,
                height,
                numerator,
                denominator,
                subtype);
        }


        // ============================================================
        // COM reference management
        // ============================================================

        private static unsafe void ReleaseComPointer(
            void* pointer)
        {
            if (pointer == null)
            {
                return;
            }

            void** vtable =
                *(void***)pointer;

            ((delegate* unmanaged[Stdcall]<
                void*,
                uint>)
            vtable[2])(
                pointer);
        }
    }
}
