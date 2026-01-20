namespace FlyleafLib;

// This file is made for easier integration against net framework 48 and ffmpeg.autogen

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

    public static class Math2
    {
        public static double Clamp(double value, double min, double max)
        {
#if NET5_0_OR_GREATER
            return Math.Clamp(value, min, max);
#else
            return value < min ? min : (value > max ? max : value);
#endif
        }
    }

    [Flags]
    public enum DispositionFlags
    {
        None = 0,
        //
        // Summary:
        //     AV_DISPOSITION_DEFAULT
        Default = 1,
        //
        // Summary:
        //     AV_DISPOSITION_DUB
        Dub = 2,
        //
        // Summary:
        //     AV_DISPOSITION_ORIGINAL
        Original = 4,
        //
        // Summary:
        //     AV_DISPOSITION_COMMENT
        Comment = 8,
        //
        // Summary:
        //     AV_DISPOSITION_LYRICS
        Lyrics = 0x10,
        //
        // Summary:
        //     AV_DISPOSITION_KARAOKE
        Karaoke = 0x20,
        //
        // Summary:
        //     AV_DISPOSITION_FORCED
        Forced = 0x40,
        //
        // Summary:
        //     AV_DISPOSITION_HEARING_IMPAIRED
        HearingImpaired = 0x80,
        //
        // Summary:
        //     AV_DISPOSITION_VISUAL_IMPAIRED
        VisualImpaired = 0x100,
        //
        // Summary:
        //     AV_DISPOSITION_CLEAN_EFFECTS
        CleanEffects = 0x200,
        //
        // Summary:
        //     AV_DISPOSITION_ATTACHED_PIC
        AttachedPic = 0x400,
        //
        // Summary:
        //     AV_DISPOSITION_TIMED_THUMBNAILS
        TimedThumbnails = 0x800,
        //
        // Summary:
        //     AV_DISPOSITION_NON_DIEGETIC
        NonDiegetic = 0x1000,
        //
        // Summary:
        //     AV_DISPOSITION_CAPTIONS
        Captions = 0x10000,
        //
        // Summary:
        //     AV_DISPOSITION_DESCRIPTIONS
        Descriptions = 0x20000,
        //
        // Summary:
        //     AV_DISPOSITION_METADATA
        Metadata = 0x40000,
        //
        // Summary:
        //     AV_DISPOSITION_DEPENDENT
        Dependent = 0x80000,
        //
        // Summary:
        //     AV_DISPOSITION_STILL_IMAGE
        StillImage = 0x100000,
        //
        // Summary:
        //     AV_DISPOSITION_MULTILAYER
        Multilayer = 0x200000
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
    //
    // Summary:
    //     @{
    [Flags]
    public enum AVBuffersrcFlag
    {
        None = 0,
        //
        // Summary:
        //     Do not check for format changes.
        NoCheckFormat = 1,
        //
        // Summary:
        //     Immediately push the frame to the output.
        Push = 4,
        //
        // Summary:
        //     Keep a reference to the frame. If the frame if reference-counted, create a new
        //     reference; otherwise copy the frame data.
        KeepRef = 8
    }
}
