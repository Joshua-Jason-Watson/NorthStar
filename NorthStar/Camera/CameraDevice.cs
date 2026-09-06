using NorthStar.Camera.Interop;
using NorthStar.Frames;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.MediaFoundation;

namespace NorthStar.Camera
{
    // Represents an opened camera and provides access to the
    // capabilities and frames exposed by Media Foundation.
    //
    // Media Foundation resources are intentionally kept in their
    // native/raw form. CameraDevice is the boundary where raw
    // Media Foundation data becomes managed NorthStar data.
    public unsafe class CameraDevice : ICamera, IDisposable
    {
        // Human-readable name of the camera.
        public string Name { get; }

        // Width of the currently configured camera stream.
        public int Width =>
            currentCapability?.Width ?? 0;

        // Height of the currently configured camera stream.
        public int Height =>
            currentCapability?.Height ?? 0;

        // Frame rate of the currently configured camera stream.
        public double FPS =>
            currentCapability?.FPS ?? 0;

        // Media Foundation activation object owned by this camera.
        private IMFActivate_unmanaged* activate;

        // Raw IMFMediaSource pointer owned by this camera.
        private void* mediaSource;

        // Raw IMFSourceReader wrapper owned by this camera.
        private RawSourceReader? sourceReader;

        // Currently configured camera capability.
        private CameraCapability? currentCapability;

        // Currently configured media type.
        private RawMediaType? currentMediaType;

        // Stride determined when the camera is configured.
        private int currentStride;

        private bool disposed;

        // Media Foundation's special index for the first video stream.
        private const uint FirstVideoStream = 0xFFFFFFFC;

        // Media Foundation flag indicating a stream tick occurred
        // without an accompanying media sample.
        private const uint StreamTickFlag = 0x00000100;

        // Media Foundation flag indicating the stream has ended.
        private const uint EndOfStreamFlag = 0x00000001;

        // HRESULT returned by GetNativeMediaType when there are
        // no more native media types to enumerate.
        private const int MfENoMoreTypes =
            unchecked((int)0xC00D36B9);

        internal CameraDevice(
            string name,
            IMFActivate_unmanaged* activate)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Camera name cannot be empty.",
                    nameof(name));
            }

            if (activate == null)
            {
                throw new ArgumentNullException(
                    nameof(activate));
            }

            Name =
                name;

            this.activate =
                activate;

            try
            {
                Guid mediaSourceGuid =
                    new Guid(
                        "279A808D-AEC7-40C8-9C6B-A6B492C78A66");

                // Activate the camera's native IMFMediaSource.
                mediaSource =
                    activate->ActivateObject(
                        mediaSourceGuid);

                if (mediaSource == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null camera media source.");
                }

                // Create the source reader using the raw
                // IMFMediaSource pointer.
                void* sourceReaderPointer = null;

                HRESULT hr =
                    MediaFoundationNative
                        .MFCreateSourceReaderFromMediaSource(
                            mediaSource,
                            null,
                            &sourceReaderPointer);

                hr.ThrowOnFailure();

                if (sourceReaderPointer == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null source reader.");
                }

                sourceReader =
                    new RawSourceReader(
                        sourceReaderPointer);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public IReadOnlyList<CameraCapability> GetCapabilities()
        {
            ThrowIfDisposed();

            List<CameraCapability> capabilities = new();

            uint mediaTypeIndex = 0;

            while (true)
            {
                void* mediaTypePointer = null;

                HRESULT hr =
                    sourceReader!.GetNativeMediaType(
                        FirstVideoStream,
                        mediaTypeIndex,
                        out mediaTypePointer);

                if (hr.Value == MfENoMoreTypes)
                {
                    break;
                }

                hr.ThrowOnFailure();

                using RawMediaType mediaType =
                    new(mediaTypePointer);

                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_SIZE,
                    out ulong frameSize);

                mediaType.GetUINT64(
                    PInvoke.MF_MT_FRAME_RATE,
                    out ulong frameRate);

                mediaType.GetGUID(
                    PInvoke.MF_MT_SUBTYPE,
                    out Guid subtype);

                int width =
                    (int)(frameSize >> 32);

                int height =
                    (int)(frameSize & 0xFFFFFFFF);

                uint frameRateNumerator =
                    (uint)(frameRate >> 32);

                uint frameRateDenominator =
                    (uint)(frameRate & 0xFFFFFFFF);

                capabilities.Add(
                    new CameraCapability(
                        mediaTypeIndex,
                        width,
                        height,
                        frameRateNumerator,
                        frameRateDenominator,
                        subtype));

                mediaTypeIndex++;
            }

            return capabilities;
        }

        public CameraCapability SelectCapability(
            int width,
            int height,
            double fps)
        {
            ThrowIfDisposed();

            foreach (
                CameraCapability capability
                in GetCapabilities())
            {
                if (capability.Width == width &&
                    capability.Height == height &&
                    Math.Abs(
                        capability.FPS - fps) < 0.01)
                {
                    return capability;
                }
            }

            throw new InvalidOperationException(
                $"Camera does not support " +
                $"{width}x{height} @ {fps:F2} FPS.");
        }

        public void Configure(
            CameraCapability capability)
        {
            ThrowIfDisposed();

            if (capability == null)
            {
                throw new ArgumentNullException(
                    nameof(capability));
            }

            void* mediaTypePointer = null;

            HRESULT hr =
                sourceReader!.GetNativeMediaType(
                    FirstVideoStream,
                    capability.MediaTypeIndex,
                    out mediaTypePointer);

            hr.ThrowOnFailure();

            if (mediaTypePointer == null)
            {
                throw new InvalidOperationException(
                    "Media Foundation returned a null media type.");
            }

            RawMediaType mediaType =
                new RawMediaType(
                    mediaTypePointer);

            try
            {
                // Configure the source reader using the raw
                // IMFMediaType pointer.
                hr =
                    sourceReader.SetCurrentMediaType(
                        FirstVideoStream,
                        mediaType.Pointer);

                hr.ThrowOnFailure();

                // Determine the stride while the media type is
                // still available.
                int stride =
                    DetermineStride(
                        mediaType,
                        capability);

                RawMediaType? previousMediaType =
                    currentMediaType;

                currentMediaType =
                    mediaType;

                currentCapability =
                    capability;

                currentStride =
                    stride;

                // Ownership has transferred to currentMediaType.
                mediaType =
                    null!;

                previousMediaType?.Dispose();
            }
            finally
            {
                // If ownership was not transferred, dispose the
                // temporary media type here.
                mediaType?.Dispose();
            }
        }

        private int DetermineStride(
            RawMediaType mediaType,
            CameraCapability capability)
        {
            Guid strideKey =
                PInvoke.MF_MT_DEFAULT_STRIDE;

            HRESULT hr =
                mediaType.GetUINT32(
                    strideKey,
                    out uint unsignedStride);

            if (hr.Succeeded)
            {
                int stride =
                    unchecked(
                        (int)unsignedStride);

                stride =
                    Math.Abs(stride);

                if (stride > 0)
                {
                    return stride;
                }
            }

            // Ask Media Foundation to calculate the minimum stride
            // for the configured subtype.
            byte[] subtypeBytes =
                capability.Subtype.ToByteArray();

            uint format =
                BitConverter.ToUInt32(
                    subtypeBytes,
                    0);

            hr =
                PInvoke.MFGetStrideForBitmapInfoHeader(
                    format,
                    (uint)capability.Width,
                    out int calculatedStride);

            hr.ThrowOnFailure();

            calculatedStride =
                Math.Abs(
                    calculatedStride);

            if (calculatedStride <= 0)
            {
                throw new InvalidOperationException(
                    $"Media Foundation could not determine a valid " +
                    $"stride for {capability.Width}x{capability.Height} " +
                    $"format {capability.Subtype}.");
            }

            return calculatedStride;
        }

        private NorthStarFrame ReadSample()
        {
            ThrowIfDisposed();

            if (currentCapability == null)
            {
                throw new InvalidOperationException(
                    "The camera has not been configured.");
            }

            if (currentStride <= 0)
            {
                throw new InvalidOperationException(
                    "The configured camera does not have a valid stride.");
            }

            while (true)
            {
                uint actualStreamIndex = 0;
                uint streamFlags = 0;
                long timestamp = 0;

                void* samplePointer = null;

                HRESULT hr =
                    sourceReader!.ReadSample(
                        FirstVideoStream,
                        0,
                        out actualStreamIndex,
                        out streamFlags,
                        out timestamp,
                        out samplePointer);

                hr.ThrowOnFailure();

                IMFSample_unmanaged* sample =
                    (IMFSample_unmanaged*)samplePointer;

                if (sample == null)
                {
                    if ((streamFlags & StreamTickFlag) != 0)
                    {
                        continue;
                    }

                    if ((streamFlags & EndOfStreamFlag) != 0)
                    {
                        throw new InvalidOperationException(
                            "Media Foundation reached the end " +
                            "of the video stream.");
                    }

                    throw new InvalidOperationException(
                        $"Media Foundation did not return a video sample. " +
                        $"Stream: {actualStreamIndex}, " +
                        $"Flags: 0x{streamFlags:X8}, " +
                        $"Timestamp: {timestamp}");
                }

                try
                {
                    sample->ConvertToContiguousBuffer(
                        out IMFMediaBuffer_unmanaged* buffer);

                    try
                    {
                        buffer->Lock(
                            out byte* data,
                            out uint maxLength,
                            out uint currentLength);

                        try
                        {
                            if (currentLength >
                                int.MaxValue)
                            {
                                throw new InvalidOperationException(
                                    "Media Foundation returned a sample " +
                                    "larger than the maximum supported " +
                                    "NorthStar buffer size.");
                            }

                            byte[] dataCopy =
                                new byte[
                                    (int)currentLength];

                            Marshal.Copy(
                                (nint)data,
                                dataCopy,
                                0,
                                (int)currentLength);

                            return new NorthStarFrame(
                                currentCapability.Width,
                                currentCapability.Height,
                                currentStride,
                                timestamp,
                                currentCapability.Subtype,
                                null,
                                dataCopy);
                        }
                        finally
                        {
                            buffer->Unlock();
                        }
                    }
                    finally
                    {
                        buffer->Release();
                    }
                }
                finally
                {
                    sample->Release();
                }
            }
        }

        public NorthStarFrame GetFrame()
        {
            ThrowIfDisposed();

            return ReadSample();
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;

            currentStride =
                0;

            currentCapability =
                null;

            currentMediaType?.Dispose();
            currentMediaType =
                null;

            sourceReader?.Dispose();
            sourceReader =
                null;

            ReleaseComPointer(
                mediaSource);

            mediaSource =
                null;

            if (activate != null)
            {
                activate->Release();

                activate =
                    null;
            }

            GC.SuppressFinalize(
                this);
        }

        private static void ReleaseComPointer(
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

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}