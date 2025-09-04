﻿using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace FlyleafLib;

public class FFmpegEngine
{
    public string   Folder          { get; private set; }
    public string   Version         { get; private set; }

    const int           AV_LOG_BUFFER_SIZE = 5 * 1024;
    internal AVRational AV_TIMEBASE_Q;

    internal FFmpegEngine()
    {
        try
        {
            Engine.Log.Info($"Loading FFmpeg libraries from '{Engine.Config.FFmpegPath}'");
            Folder = Utils.GetFolderPath(Engine.Config.FFmpegPath);
            RootPath = Folder;

            uint ver = avformat_version();
            Version = $"{ver >> 16}.{(ver >> 8) & 255}.{ver & 255}";

            SetLogLevel();
            AV_TIMEBASE_Q   = av_get_time_base_q();
            Engine.Log.Info($"FFmpeg Loaded (Location: {Folder}, FmtVer: {Version})");
        } catch (Exception e)
        {
            Engine.Log.Error($"Loading FFmpeg libraries '{Engine.Config.FFmpegPath}' failed\r\n{e.Message}\r\n{e.StackTrace}");
            throw new Exception($"Loading FFmpeg libraries '{Engine.Config.FFmpegPath}' failed");
        }
    }

    internal static void SetLogLevel()
    {
        if (Engine.Config.FFmpegLogLevel != FFmpegLogLevel.Quiet)
        {
            av_log_set_level((int)Engine.Config.FFmpegLogLevel);
            av_log_set_callback(LogFFmpeg);
        }
        else
        {
            av_log_set_level((int)FFmpegLogLevel.Quiet);
            av_log_set_callback(null);
        }
    }

    internal unsafe static av_log_set_callback_callback LogFFmpeg = (p0, level, format, vl) =>
    {
        if (level > av_log_get_level())
            return;

        byte*   buffer = stackalloc byte[AV_LOG_BUFFER_SIZE];
        int     printPrefix = 1;
        av_log_format_line2(p0, level, format, vl, buffer, AV_LOG_BUFFER_SIZE, &printPrefix);
        string  line = BytePtrToStringUTF8(buffer);

        Output($"FFmpeg|{level,-7}|{line.Trim()}");
    };

    internal unsafe static string ErrorCodeToMsg(int error)
    {
        byte* buffer = stackalloc byte[AV_LOG_BUFFER_SIZE];
        av_strerror(error, buffer, AV_LOG_BUFFER_SIZE);
        return BytePtrToStringUTF8(buffer);
    }
}

public enum FFmpegLogLevel
{
    Quiet = -0x08,
    SkipRepeated = 0x01,
    PrintLevel = 0x02,
    Fatal = 0x08,
    Error = 0x10,
    Warning = 0x18,
    Info = 0x20,
    Verbose = 0x28,
    Debug = 0x30,
    Trace = 0x38,
    MaxOffset = 0x40,
}