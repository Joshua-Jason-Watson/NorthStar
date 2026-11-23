
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.Win32.Foundation;

namespace NorthStar.Camera.Interop
{
    // Owns the native IMFSourceReader and IMFSourceReaderEx
    // interfaces and exposes the operations required by NorthStar.
    //
    // Continuous sample acquisition and frame buffering are handled
    // by SourceReaderCallback.
    internal unsafe sealed class RawSourceReader : IDisposable
    {
        // ============================================================
        // Native interfaces
        // ============================================================

        private void* pointer;

        private void* extendedPointer;

        private int disposed;

        private static readonly Guid SourceReaderExIid =
            new("7B981CF0-560E-4116-9875-B099895F23D7");


        // ============================================================
        // Constructor
        // ============================================================

        public RawSourceReader(void* pointer)
        {
            if (pointer == null)
            {
                throw new ArgumentNullException(nameof(pointer));
            }

            this.pointer = pointer;

            void* queriedPointer = null;

            try
            {
                Guid iid = SourceReaderExIid;

                void** vtable = *(void***)pointer;

                HRESULT hr =
                    ((delegate* unmanaged[Stdcall]<
                        void*, Guid*, void**, HRESULT>)
                    vtable[0])(
                        pointer,
                        &iid,
                        &queriedPointer);

                hr.ThrowOnFailure();

                if (queriedPointer == null)
                {
                    throw new InvalidOperationException(
                        "Media Foundation returned a null IMFSourceReaderEx interface.");
                }

                extendedPointer = queriedPointer;
            }
            catch
            {
                ReleaseComPointer(queriedPointer);

                ReleaseComPointer(this.pointer);

                this.pointer = null;

                throw;
            }
        }


        // ============================================================
        // Native media type configuration
        // ============================================================

        public HRESULT SetNativeMediaType(
            uint streamIndex,
            void* mediaType,
            out uint streamFlags)
        {
            ThrowIfDisposed();

            if (mediaType == null)
            {
                throw new ArgumentNullException(nameof(mediaType));
            }

            uint result = 0;

            void** vtable =
                *(void***)extendedPointer;

            HRESULT hr =
                ((delegate* unmanaged[Stdcall]<
                    void*, uint, void*, uint*, HRESULT>)
                vtable[13])(
                    extendedPointer,
                    streamIndex,
                    mediaType,
                    &result);

            streamFlags = result;

            return hr;
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
                    void*, uint, uint, void**, HRESULT>)
                GetMethod(5))(
                    pointer,
                    streamIndex,
                    mediaTypeIndex,
                    &result);

            mediaType = result;

            return hr;
        }


        // ============================================================
        // Continuous capture
        // ============================================================

        // Starts continuous asynchronous sample acquisition.
        //
        // SourceReaderCallback immediately requests subsequent samples
        // when Media Foundation delivers each completed sample.
        public void StartContinuousCapture(
            uint streamIndex,
            SourceReaderCallback callback)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(callback);

            callback.StartContinuousCapture(
                () => RequestSample(streamIndex, 0),
                streamIndex);
        }


        // ============================================================
        // Native asynchronous sample request
        // ============================================================

        // Requests another sample from Media Foundation.
        //
        // Output parameters must be null when using the asynchronous
        // Source Reader callback mechanism.
        private HRESULT RequestSample(
            uint streamIndex,
            uint controlFlags)
        {
            ThrowIfDisposed();

            return
                ((delegate* unmanaged[Stdcall]<
                    void*, uint, uint,
                    uint*, uint*, long*, void**, HRESULT>)
                GetMethod(9))(
                    pointer,
                    streamIndex,
                    controlFlags,
                    null,
                    null,
                    null,
                    null);
        }


        // ============================================================
        // Sample consumption
        // ============================================================

        // Retrieves the next available sample from the callback's
        // bounded queue.
        //
        // Does not initiate a new native read. Capture is already
        // running independently.
        public Task<SourceReaderSampleResult> TakeSampleAsync(
            SourceReaderCallback callback)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(callback);

            return callback.TakeSampleAsync();
        }


        // ============================================================
        // Flush
        // ============================================================

        // Initiates an asynchronous Source Reader flush.
        //
        // Completion is reported through IMFSourceReaderCallback.OnFlush.
        public Task FlushAsync(
            uint streamIndex,
            SourceReaderCallback callback)
        {
            ThrowIfDisposed();

            ArgumentNullException.ThrowIfNull(callback);

            Task completion =
                callback.PrepareFlush();

            try
            {
                HRESULT hr =
                    FlushNative(streamIndex);

                if (hr.Value < 0)
                {
                    throw Marshal.GetExceptionForHR(hr.Value)
                        ?? new COMException(
                            "Media Foundation could not flush the source reader.",
                            hr.Value);
                }
            }
            catch (Exception exception)
            {
                callback.FailFlush(exception);
            }

            return completion;
        }


        private HRESULT FlushNative(uint streamIndex)
        {
            ThrowIfDisposed();

            return
                ((delegate* unmanaged[Stdcall]<
                    void*, uint, HRESULT>)
                GetMethod(10))(
                    pointer,
                    streamIndex);
        }


        // ============================================================
        // COM helpers
        // ============================================================

        private void* GetMethod(int index)
        {
            void** vtable =
                *(void***)pointer;

            return vtable[index];
        }


        private static void ReleaseComPointer(void* pointer)
        {
            if (pointer == null)
            {
                return;
            }

            void** vtable =
                *(void***)pointer;

            ((delegate* unmanaged[Stdcall]<void*, uint>)
                vtable[2])(pointer);
        }


        // ============================================================
        // Disposal
        // ============================================================

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            // The owner must complete the Source Reader flush
            // before disposing this object.
            ReleaseComPointer(extendedPointer);

            extendedPointer = null;

            ReleaseComPointer(pointer);

            pointer = null;

            GC.SuppressFinalize(this);
        }


        // ============================================================
        // Validation
        // ============================================================

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref disposed) != 0,
                this);
        }
    }
}
