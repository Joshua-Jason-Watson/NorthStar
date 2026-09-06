using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace NorthStar.Camera
{
    // Opens a CameraDescriptor as an active CameraDevice.
    //
    // The factory is responsible for locating the Windows device
    // represented by the descriptor and transferring ownership of
    // its IMFActivate reference to the resulting CameraDevice.
    public unsafe sealed class WindowsCameraDeviceFactory
    {
        public CameraDevice Open(
            CameraDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(
                    nameof(descriptor));
            }

            PInvoke.MFCreateAttributes(
                out var attributes,
                1);

            try
            {
                ConfigureDeviceEnumeration(
                    attributes);

                PInvoke.MFEnumDeviceSources(
                    attributes,
                    out var devices,
                    out var deviceCount);

                try
                {
                    for (uint i = 0;
                         i < deviceCount;
                         i++)
                    {
                        IMFActivate_unmanaged* device =
                            devices[i];

                        bool ownershipTransferred =
                            false;

                        try
                        {
                            string identifier =
                                GetDeviceString(
                                    device,
                                    PInvoke.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK);

                            if (!string.Equals(
                                    identifier,
                                    descriptor.Identifier,
                                    StringComparison.Ordinal))
                            {
                                continue;
                            }

                            CameraDevice camera =
                                new CameraDevice(
                                    descriptor.Name,
                                    device);

                            ownershipTransferred =
                                true;

                            return camera;
                        }
                        finally
                        {
                            if (!ownershipTransferred &&
                                device != null)
                            {
                                device->Release();
                            }
                        }
                    }
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

            throw new InvalidOperationException(
                $"Camera '{descriptor.Name}' with identifier " +
                $"'{descriptor.Identifier}' could not be found.");
        }

        private static unsafe void ConfigureDeviceEnumeration(
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
    }
}