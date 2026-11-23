
namespace NorthStar.Camera;

/// <summary>
/// Contains timing information associated with receiving and
/// preparing one camera frame.
///
/// All values are measured in milliseconds.
/// </summary>
public readonly struct CameraCaptureMetrics
{
    /// <summary>
    /// Time spent waiting for a captured sample to become
    /// available in the continuous capture queue.
    /// </summary>
    public double SampleQueueWaitMilliseconds { get; }

    /// <summary>
    /// Time spent converting the sample into a contiguous
    /// Media Foundation buffer.
    /// </summary>
    public double ContiguousBufferMilliseconds { get; }

    /// <summary>
    /// Time spent locking the Media Foundation buffer.
    /// </summary>
    public double BufferLockMilliseconds { get; }

    /// <summary>
    /// Time spent allocating memory for the copied frame.
    /// </summary>
    public double BufferAllocationMilliseconds { get; }

    /// <summary>
    /// Time spent copying sample data into NorthStar's memory.
    /// </summary>
    public double BufferCopyMilliseconds { get; }

    /// <summary>
    /// Time interval between consecutive Media Foundation
    /// frame timestamps.
    /// </summary>
    public double FrameTimestampIntervalMilliseconds { get; }

    public CameraCaptureMetrics(
        double sampleQueueWaitMilliseconds,
        double contiguousBufferMilliseconds,
        double bufferLockMilliseconds,
        double bufferAllocationMilliseconds,
        double bufferCopyMilliseconds,
        double frameTimestampIntervalMilliseconds)
    {
        SampleQueueWaitMilliseconds =
            sampleQueueWaitMilliseconds;

        ContiguousBufferMilliseconds =
            contiguousBufferMilliseconds;

        BufferLockMilliseconds =
            bufferLockMilliseconds;

        BufferAllocationMilliseconds =
            bufferAllocationMilliseconds;

        BufferCopyMilliseconds =
            bufferCopyMilliseconds;

        FrameTimestampIntervalMilliseconds =
            frameTimestampIntervalMilliseconds;
    }
}
