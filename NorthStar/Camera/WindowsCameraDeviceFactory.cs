
using System;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace NorthStar.Camera
{
    // Locates Windows camera devices and creates CameraDevice instances.
    //
    // The factory owns the enumerated IMFActivate references and
    // transfers exactly one reference to the resulting CameraDevice.
    public sealed unsafe class WindowsCameraDeviceFactory
    {
        // ============================================================
        // Camera opening
        // ============================================================

        public CameraDevice Open(
            CameraDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            PInvoke.MFCreateAttributes(
                out var attributes,
                1);

            try
            {
                ConfigureDeviceEnumeration(attributes);

                IMFActivate_unmanaged** devices = null;

                uint deviceCount = 0;

                try
                {
                    PInvoke.MFEnumDeviceSources(
                        attributes,
                        out devices,
                        out deviceCount);

                    if (devices == null || deviceCount == 0)
                    {
                        throw new InvalidOperationException(
                            "Media Foundation did not discover any camera devices.");
                    }

                    // Find the matching device without transferring
                    // or releasing any activation references yet.
                    uint selectedIndex = uint.MaxValue;

                    for (uint i = 0; i < deviceCount; i++)
                    {
                        IMFActivate_unmanaged* device =
                            devices[i];

                        if (device == null)
                        {
                            continue;
                        }

                        string identifier =
                            GetDeviceString(
                                device,
                                PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK);

                        if (string.Equals(
                            identifier,
                            descriptor.Identifier,
                            StringComparison.Ordinal))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }

                    if (selectedIndex == uint.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Camera '{descriptor.Name}' with identifier " +
                            $"'{descriptor.Identifier}' could not be found.");
                    }

                    // Transfer ownership of the matching activation
                    // reference to CameraDevice.
                    //
                    // Clear the array entry before construction because
                    // CameraDevice consumes the reference on invocation,
                    // including when its initialization fails.
                    IMFActivate_unmanaged* selectedDevice =
                        devices[selectedIndex];

                    devices[selectedIndex] = null;

                    return new CameraDevice(
                        descriptor.Name,
                        selectedDevice);
                }
                finally
                {
                    // Release every activation reference still owned
                    // by the enumeration result.
                    //
                    // The selected device's entry is null because its
                    // reference has been transferred to CameraDevice.
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

                        // Release the allocated activation pointer array.
                        PInvoke.CoTaskMemFree(
                            devices);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(
                    attributes);
            }
        }


        // ============================================================
        // Device enumeration configuration
        // ============================================================

        private static void ConfigureDeviceEnumeration(
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
        // Device identifier retrieval
        // ============================================================

        private static string GetDeviceString(
            IMFActivate_unmanaged* device,
            Guid attributeKey)
        {
            if (device == null)
            {
                throw new ArgumentNullException(
                    nameof(device));
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
                        "Media Foundation returned an empty camera identifier.");
                }

                return value.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(
                    value.Value);
            }
        }
    }
}
