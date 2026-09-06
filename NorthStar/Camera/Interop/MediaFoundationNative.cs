using System;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace NorthStar.Camera.Interop
{
    internal static unsafe class MediaFoundationNative
    {
        [DllImport(
            "MFReadWrite.dll",
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        internal static extern HRESULT MFCreateSourceReaderFromMediaSource(
            void* pMediaSource,
            void* pAttributes,
            void** ppSourceReader);
    }
}