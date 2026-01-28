namespace FlyleafLib;

public static partial class Utils
{
    public static long NoTs = AV_NOPTS_VALUE;

    public static int AVERROR_EAGAIN = AVERROR(EAGAIN);

    public static int AVERROR_ENOMEM = AVERROR(ENOMEM);

    public static int AVERROR_EINVAL = AVERROR(EINVAL);

    public static int AV_CODEC_HW_CONFIG_METHOD_HW_DEVICE_CTX = 0x01;

    public static bool HasFlag(this int flags, int flag)
    {
        return (flags & flag) != 0;
    }

    public static bool HasFlag(this ulong flags, ulong flag)
    {
        return (flags & flag) != 0;
    }

#if !NET5_0_OR_GREATER
    public static void Clear<T>(this ConcurrentQueue<T> queue)
    {
        while (queue.TryDequeue(out var _))
        {
            // Do nothing, just dequeue to clear
        }
    }
    public static bool TryDequeue<T>(this Queue<T> queue, out T outval)
    {
        if (queue.Count == 0)
        {
            outval = default;
            return false;
        }
        outval = queue.Dequeue();
        return true;
    }
    public static bool TryPeek<T>(this Queue<T> queue, out T outval)
    {
        if (queue.Count == 0)
        {
            outval = default;
            return false;
        }
        outval = queue.Peek();
        return true;
    }
#endif

    public static AVChannelLayout AV_CHANNEL_LAYOUT_MASK(int nb, ulong channel)
    {
        AVChannelLayout result = default;
        result.order = AVChannelOrder.AV_CHANNEL_ORDER_NATIVE;
        result.nb_channels = nb;
        result.u = new AVChannelLayout_u
        {
            mask = channel
        };
        return result;
    }
    //
    // Summary:
    //     AV_CHANNEL_LAYOUT_STEREO = AV_CHANNEL_LAYOUT_MASK(2, AV_CH_LAYOUT_STEREO)
    public static readonly AVChannelLayout AV_CHANNEL_LAYOUT_STEREO = AV_CHANNEL_LAYOUT_MASK(2, 3uL);

    //
    // Summary:
    //     Link properties exposed to filter code, but not external callers.
    public struct FilterLink
    {
        public AVFilterLink pub;

        //
        // Summary:
        //     Graph the filter belongs to.
        public unsafe AVFilterGraph* graph;

        //
        // Summary:
        //     Current timestamp of the link, as defined by the most recent frame(s), in link
        //     time_base units.
        public long current_pts;

        //
        // Summary:
        //     Current timestamp of the link, as defined by the most recent frame(s), in AV_TIME_BASE
        //     units.
        public long current_pts_us;

        //
        // Summary:
        //     Minimum number of samples to filter at once.
        public int min_samples;

        //
        // Summary:
        //     Maximum number of samples to filter at once. If filter_frame() is called with
        //     more samples, it will split them.
        public int max_samples;

        //
        // Summary:
        //     Number of past frames sent through the link.
        public long frame_count_in;

        //
        // Summary:
        //     Number of past frames sent through the link.
        public long frame_count_out;

        //
        // Summary:
        //     Number of past samples sent through the link.
        public long sample_count_in;

        //
        // Summary:
        //     Number of past samples sent through the link.
        public long sample_count_out;

        //
        // Summary:
        //     Frame rate of the stream on the link, or 1/0 if unknown or variable.
        public AVRational frame_rate;

        //
        // Summary:
        //     For hwaccel pixel formats, this should be a reference to the AVHWFramesContext
        //     describing the frames.
        public unsafe AVBufferRef* hw_frames_ctx;
    }
}

public class AVBuffersrcFlag
{
    public static int None = 0;
    //
    // Summary:
    //     Do not check for format changes.
    public static int NoCheckFormat = 1;
    //
    // Summary:
    //     Immediately push the frame to the output.
    public static int Push = 4;
    //
    // Summary:
    //     Keep a reference to the frame. If the frame if reference-counted, create a new
    //     reference; otherwise copy the frame data.
    public static int KeepRef = 8;
}

public static class CodecFlags
{
    public static int OutputCorrupt = AV_CODEC_FLAG_OUTPUT_CORRUPT;
    public static int LowDelay = AV_CODEC_FLAG_LOW_DELAY;
}

public static class CodecFlags2
{
    public static int Fast = AV_CODEC_FLAG2_FAST;
}

public static class DictReadFlags
{
    public static int IgnoreSuffix = AV_DICT_IGNORE_SUFFIX;
}

public static class PktFlags
{
    public static int Key = AV_PKT_FLAG_KEY;
}

public static class FrameFlags
{
    public static int Key = AV_FRAME_FLAG_KEY;
}
