using System;
using Windows.Win32.Foundation;

namespace NorthStar.Camera.Interop
{
    internal unsafe sealed class RawSourceReader : IDisposable
    {
        private void* pointer;
        private bool disposed;

        public RawSourceReader(
            void* pointer)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException(
                    nameof(pointer));
            }

            this.pointer = pointer;
        }

        public HRESULT GetNativeMediaType(
            uint streamIndex,
            uint mediaTypeIndex,
            out void* mediaType)
        {
            ThrowIfDisposed();

            void* result = null;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    uint,
                    uint,
                    void**,
                    HRESULT>)
                GetMethod(5))(
                    pointer,
                    streamIndex,
                    mediaTypeIndex,
                    &result);

            mediaType = result;

            return hr;
        }

        public HRESULT SetCurrentMediaType(
            uint streamIndex,
            void* mediaType)
        {
            ThrowIfDisposed();

            return
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    uint,
                    uint*,
                    void*,
                    HRESULT>)
                GetMethod(7))(
                    pointer,
                    streamIndex,
                    null,
                    mediaType);
        }

        public HRESULT ReadSample(
            uint streamIndex,
            uint controlFlags,
            out uint actualStreamIndex,
            out uint streamFlags,
            out long timestamp,
            out void* sample)
        {
            ThrowIfDisposed();

            uint actualStreamIndexValue = 0;
            uint streamFlagsValue = 0;
            long timestampValue = 0;
            void* sampleValue = null;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    uint,
                    uint,
                    uint*,
                    uint*,
                    long*,
                    void**,
                    HRESULT>)
                GetMethod(9))(
                    pointer,
                    streamIndex,
                    controlFlags,
                    &actualStreamIndexValue,
                    &streamFlagsValue,
                    &timestampValue,
                    &sampleValue);

            actualStreamIndex =
                actualStreamIndexValue;

            streamFlags =
                streamFlagsValue;

            timestamp =
                timestampValue;

            sample =
                sampleValue;

            return hr;
        }

        private void* GetMethod(
            int index)
        {
            void** vtable =
                *(void***)pointer;

            return vtable[index];
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (pointer != null)
            {
                void** vtable =
                    *(void***)pointer;

                ((delegate* unmanaged[Stdcall]<
                    void*,
                    uint>)
                vtable[2])(
                    pointer);

                pointer = null;
            }

            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}