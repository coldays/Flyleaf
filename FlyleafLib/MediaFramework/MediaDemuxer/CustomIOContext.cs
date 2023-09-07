﻿using System;
using System.IO;
using System.Runtime.InteropServices;

using FFmpeg.AutoGen;
using static FFmpeg.AutoGen.ffmpeg;

namespace FlyleafLib.MediaFramework.MediaDemuxer;

public unsafe class CustomIOContext
{
    //List<object>    gcPrevent = new List<object>();
    AVIOContext*    avioCtx;
    public Stream   stream;
#if NETFRAMEWORK
    byte[]          buffer;
#endif
    Demuxer         demuxer;

    public CustomIOContext(Demuxer demuxer)
    {
        this.demuxer = demuxer;
    }

    public void Initialize(Stream stream)
    {
        this.stream = stream;
        //this.stream.Seek(0, SeekOrigin.Begin);
#if NETFRAMEWORK
        if (buffer == null)
            buffer  = new byte[demuxer.Config.IOStreamBufferSize]; // NOTE: if we use small buffer ffmpeg might request more than we suggest
#endif

        ioread = IORead;
        ioseek = IOSeek;
        avioCtx = avio_alloc_context((byte*)av_malloc((ulong)demuxer.Config.IOStreamBufferSize), demuxer.Config.IOStreamBufferSize, 0, null, ioread, null, ioseek);            
        demuxer.FormatContext->pb     = avioCtx;
        demuxer.FormatContext->flags |= AVFMT_FLAG_CUSTOM_IO;
    }

    public void Dispose()
    {
        if (avioCtx != null) 
        {
            av_free(avioCtx->buffer); 
            fixed (AVIOContext** ptr = &avioCtx) avio_context_free(ptr);
        }
        avioCtx = null;
        stream = null;
        ioread = null;
        ioseek = null;
    }

    avio_alloc_context_read_packet  ioread;
    avio_alloc_context_seek         ioseek;

    int IORead(void* opaque, byte* buffer, int bufferSize)
    {
        int ret;

        if (demuxer.Interrupter.ShouldInterrupt(null) != 0) return AVERROR_EXIT;
#if NETFRAMEWORK
        ret = demuxer.CustomIOContext.stream.Read(demuxer.CustomIOContext.buffer, 0, bufferSize);
        if (ret < 0) { demuxer.Log.Warn("CustomIOContext Interrupted"); return AVERROR_EXIT; }
        Marshal.Copy(demuxer.CustomIOContext.buffer, 0, (IntPtr) buffer, ret);
#else
        ret = demuxer.CustomIOContext.stream.Read(new Span<byte>(buffer, bufferSize));
#endif

        if (ret > 0)
            return ret;

        if (ret == 0)
            return AVERROR_EOF;

        if (ret < 0)
            { demuxer.Log.Warn("CustomIOContext Interrupted"); return AVERROR_EXIT; }
    }

    long IOSeek(void* opaque, long offset, int whence)
    {
        //System.Diagnostics.Debug.WriteLine($"** S | {decCtx.demuxer.fmtCtx->pb->pos} - {decCtx.demuxer.ioStream.Position}");

        return whence == AVSEEK_SIZE
            ? demuxer.CustomIOContext.stream.Length
            : demuxer.CustomIOContext.stream.Seek(offset, (SeekOrigin) whence);
    }
}