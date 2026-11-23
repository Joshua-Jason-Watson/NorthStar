
using System;
using System.Threading;

using Windows.Win32;

namespace NorthStar
{
    // Owns one initialization reference to the Media Foundation platform.
    //
    // Must be created before using Media Foundation and disposed during
    // application shutdown, after all Media Foundation resources
    // have been released.
    public sealed class MediaFoundationRuntime : IDisposable
    {
        // ============================================================
        // Configuration
        // ============================================================

        private const uint MediaFoundationVersion =
            0x00020070;


        // ============================================================
        // Lifecycle
        // ============================================================

        private int disposed;


        // ============================================================
        // Constructor
        // ============================================================

        public MediaFoundationRuntime()
        {
            var hr =
                PInvoke.MFStartup(
                    MediaFoundationVersion,
                    0);

            hr.ThrowOnFailure();
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

            // This must run during application shutdown,
            // not from a Media Foundation work-queue thread.
            var hr =
                PInvoke.MFShutdown();

            hr.ThrowOnFailure();

            GC.SuppressFinalize(this);
        }
    }
}
