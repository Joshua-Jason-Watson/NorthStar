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
    // Discovery produces descriptions of cameras. It does not create
    // long-lived CameraDevice instances.
    public sealed class WindowsCameraDiscovery
    {
        private const uint FirstVideoStream =
            unchecked(
                (uint)
                MF_SOURCE_READER_CONSTANTS
                    .MF_SOURCE_READER_FIRST_VIDEO_STREAM);

        private static readonly Guid MediaSourceGuid =
            new Guid(
                "279A808D-AEC7-40C8-9C6B-A6B492C78A66");

        private const int MfENoMoreTypes =
            unchecked((int)0xC00D36B9);

        public unsafe IReadOnlyList<CameraDescriptor> Discover()
        {
            Console.WriteLine(
                "Media Foundation camera discovery");

            PInvoke.MFCreateAttributes(
                out var attributes,
                1);

            try
            {
                ConfigureDiscoveryAttributes(
                    attributes);

                PInvoke.MFEnumDeviceSources(
                    attributes,
                    out var devices,
                    out var deviceCount);

                try
                {
                    Console.WriteLine(
                        $"Found {deviceCount} cameras");

                    List<CameraDescriptor> descriptors =
                        new List<CameraDescriptor>(
                            checked((int)deviceCount));

                    for (uint i = 0;
                         i < deviceCount;
                         i++)
                    {
                        Console.WriteLine(
                            $"Processing camera {i}");

                        IMFActivate_unmanaged* device =
                            devices[i];

                        try
                        {
                            descriptors.Add(
                                DiscoverCamera(
                                    device,
                                    i));
                        }
                        finally
                        {
                            if (device != null)
                            {
                                device->Release();
                            }
                        }
                    }

                    return descriptors;
                }
                finally
                {
                    PInvoke.CoTaskMemFree(
                        devices);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(
                    attributes);
            }
        }

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

        private static unsafe CameraDescriptor DiscoverCamera(
            IMFActivate_unmanaged* device,
            uint index)
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
                DiscoverCapabilities(
                    device);

            Console.WriteLine(
                $"Camera {index}: {name}");

            foreach (
                CameraCapability capability
                in capabilities)
            {
                Console.WriteLine(
                    $"Media type " +
                    $"{capability.MediaTypeIndex}: " +
                    $"{capability}");
            }

            return new CameraDescriptor(
                name,
                identifier,
                capabilities);
        }

        private static unsafe string GetDeviceString(
            IMFActivate_unmanaged* device,
            Guid attributeKey)
        {
            PWSTR value;

            device->GetAllocatedString(
                &attributeKey,
                &value,
                out _);

            try
            {
                return value.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(
                    value.Value);
            }
        }

        private static unsafe IReadOnlyList<CameraCapability> DiscoverCapabilities(
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

                    using RawSourceReader sourceReader =
                        new RawSourceReader(
                            readerPointer);

                    readerPointer = null;

                    return EnumerateCapabilities(
                        sourceReader);
                }
                finally
                {
                    if (readerPointer != null)
                    {
                        ReleaseComPointer(
                            readerPointer);
                    }
                }
            }
            finally
            {
                ReleaseComPointer(
                    sourcePointer);
            }
        }

        private static unsafe IReadOnlyList<CameraCapability> EnumerateCapabilities(
            RawSourceReader sourceReader)
        {
            List<CameraCapability> capabilities =
                new List<CameraCapability>();

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

                capabilities.Add(
                    capability);

                mediaTypeIndex++;
            }

            return capabilities;
        }

        private static unsafe CameraCapability ReadCapability(
            uint mediaTypeIndex,
            RawMediaType mediaType)
        {
            HRESULT hr =
                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_SIZE,
                    out ulong frameSize);

            hr.ThrowOnFailure();

            int width =
                checked(
                    (int)(frameSize >> 32));

            int height =
                checked(
                    (int)(frameSize & 0xFFFFFFFF));

            hr =
                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_RATE,
                    out ulong frameRate);

            hr.ThrowOnFailure();

            uint numerator =
                (uint)(frameRate >> 32);

            uint denominator =
                (uint)(frameRate & 0xFFFFFFFF);

            if (denominator == 0)
            {
                throw new InvalidOperationException(
                    "Media Foundation reported a zero " +
                    "frame-rate denominator.");
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