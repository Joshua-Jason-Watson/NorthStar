using System;
using Windows.Win32;

namespace NorthStar
{
    public sealed class MediaFoundationRuntime : IDisposable
    {
        private const uint MediaFoundationVersion =
            0x00020070;

        private bool disposed;

        public MediaFoundationRuntime()
        {
            var hr =
                PInvoke.MFStartup(
                    MediaFoundationVersion,
                    0);

            hr.ThrowOnFailure();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            PInvoke.MFShutdown();

            GC.SuppressFinalize(
                this);
        }
    }
}