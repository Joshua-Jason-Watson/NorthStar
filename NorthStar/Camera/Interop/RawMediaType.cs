using System;
using Windows.Win32.Foundation;

namespace NorthStar.Camera.Interop
{
    internal unsafe sealed class RawMediaType : IDisposable
    {
        private void* pointer;
        private bool disposed;

        public void* Pointer
        {
            get
            {
                ThrowIfDisposed();

                return pointer;
            }
        }

        public RawMediaType(
            void* pointer)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException(
                    nameof(pointer));
            }

            this.pointer =
                pointer;
        }

        public HRESULT GetUINT32(
            Guid key,
            out uint value)
        {
            ThrowIfDisposed();

            uint result = 0;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    Guid*,
                    uint*,
                    HRESULT>)
                GetMethod(7))(
                    pointer,
                    &key,
                    &result);

            value =
                result;

            return hr;
        }

        public HRESULT GetUINT64(
            Guid key,
            out ulong value)
        {
            ThrowIfDisposed();

            ulong result = 0;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    Guid*,
                    ulong*,
                    HRESULT>)
                GetMethod(8))(
                    pointer,
                    &key,
                    &result);

            value =
                result;

            return hr;
        }

        public HRESULT GetGUID(
            Guid key,
            out Guid value)
        {
            ThrowIfDisposed();

            Guid result =
                Guid.Empty;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*,
                    Guid*,
                    Guid*,
                    HRESULT>)
                GetMethod(10))(
                    pointer,
                    &key,
                    &result);

            value =
                result;

            return hr;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed =
                true;

            if (pointer != null)
            {
                void** vtable =
                    *(void***)pointer;

                ((delegate* unmanaged[Stdcall]<
                    void*,
                    uint>)
                vtable[2])(
                    pointer);

                pointer =
                    null;
            }

            GC.SuppressFinalize(
                this);
        }

        private void* GetMethod(
            int index)
        {
            void** vtable =
                *(void***)pointer;

            return vtable[index];
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);
        }
    }
}