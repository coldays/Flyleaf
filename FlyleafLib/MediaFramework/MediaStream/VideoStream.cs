using FlyleafLib.MediaFramework.MediaDemuxer;
using System.Runtime.InteropServices;
using static FFmpeg.AutoGen.ffmpeg;

namespace FlyleafLib.MediaFramework.MediaStream;

public unsafe class VideoStream : StreamBase
{
    public AspectRatio                  AspectRatio         { get; set; }
    public ColorRange                   ColorRange          { get; set; }
    public ColorSpace                   ColorSpace          { get; set; }
    public AVColorTransferCharacteristic
                                        ColorTransfer       { get; set; }
    public DeInterlace                  FieldOrder          { get; set; }
    public double                       Rotation            { get; set; }
    public double                       FPS                 { get; set; }
    public long                         FrameDuration       { get ;set; }
    public double                       FPS2                { get; set; } // interlace
    public long                         FrameDuration2      { get ;set; } // interlace
    public uint                         Height              { get; set; }
    public bool                         IsRGB               { get; set; }
    public HDRFormat                    HDRFormat           { get; set; }

    public AVComponentDescriptor[]      PixelComps          { get; set; }
    public int                          PixelComp0Depth     { get; set; }
    public AVPixelFormat                PixelFormat         { get; set; }
    public AVPixFmtDescriptor*          PixelFormatDesc     { get; set; }
    public string                       PixelFormatStr      { get; set; }
    public int                          PixelPlanes         { get; set; }
    public bool                         PixelSameDepth      { get; set; }
    public bool                         PixelInterleaved    { get; set; }
    public int                          TotalFrames         { get; set; }
    public uint                         Width               { get; set; }
    public bool                         FixTimestamps       { get; set; } // TBR: For formats such as h264/hevc that have no or invalid pts values

    public override string GetDump()
        => $"[{Type} #{StreamIndex}] {Codec} {PixelFormatStr} {Width}x{Height} @ {FPS:#.###} | [Color: {ColorSpace}] [BR: {BitRate}] | {Utils.TicksToTime((long)(AVStream->start_time * Timebase))}/{Utils.TicksToTime((long)(AVStream->duration * Timebase))} | {Utils.TicksToTime(StartTime)}/{Utils.TicksToTime(Duration)}";

    public VideoStream() { }
    public VideoStream(Demuxer demuxer, AVStream* st) : base(demuxer, st)
    {
        Demuxer = demuxer;
        AVStream = st;
        Refresh();
    }

    public void Refresh(AVPixelFormat format = AVPixelFormat.AV_PIX_FMT_NONE, AVFrame* frame = null)
    {
        base.Refresh();
        FieldOrder      = AVStream->codecpar->field_order == AVFieldOrder.AV_FIELD_TT ? DeInterlace.TopField : (AVStream->codecpar->field_order == AVFieldOrder.AV_FIELD_BB ? DeInterlace.BottomField : DeInterlace.Progressive);
        PixelFormat     = format == AVPixelFormat.AV_PIX_FMT_NONE ? (AVPixelFormat)AVStream->codecpar->format : format;
        PixelFormatStr  = PixelFormat.ToString().Replace("AV_PIX_FMT_","").ToLower();
        Width           = (uint)AVStream->codecpar->width;
        Height          = (uint)AVStream->codecpar->height;

        if ((Demuxer.FormatContext->iformat->flags & AVFMT_NOTIMESTAMPS) != 0)
        {
            FixTimestamps = true;

            if (Demuxer.Config.ForceFPS > 0)
                FPS = Demuxer.Config.ForceFPS;
            else
                FPS = av_q2d(av_guess_frame_rate(Demuxer.FormatContext, AVStream, frame));

            if (FPS == 0)
                FPS = 25;
        }
        else
        {
            FixTimestamps = false;
            FPS  = av_q2d(av_guess_frame_rate(Demuxer.FormatContext, AVStream, frame));
        }

        FrameDuration   = FPS > 0 ? (long) (10_000_000 / FPS) : 0;
        TotalFrames     = AVStream->duration > 0 && FrameDuration > 0 ? (int) (AVStream->duration * Timebase / FrameDuration) : (FrameDuration > 0 ? (int) (Demuxer.Duration / FrameDuration) : 0);

        int x, y;
        AVRational sar = av_guess_sample_aspect_ratio(null, AVStream, null);
        if (av_cmp_q(sar, av_make_q(0, 1)) <= 0)
            sar = av_make_q(1, 1);

        av_reduce(&x, &y, Width  * sar.num, Height * sar.den, 1024 * 1024);
        AspectRatio = new AspectRatio(x, y);

        AVPacketSideData* pktSideData;
        if ((pktSideData = av_packet_side_data_get(AVStream->codecpar->coded_side_data, AVStream->codecpar->nb_coded_side_data, AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX)) != null && pktSideData->data != null)
        {
            int_array9 displayMatrix = Marshal.PtrToStructure<int_array9>((nint)pktSideData->data);
            double rotation = -Math.Round(av_display_rotation_get(displayMatrix));
            Rotation = rotation - (360 * Math.Floor(rotation / 360 + 0.9 / 360));
        }

        ColorRange = AVStream->codecpar->color_range == AVColorRange.AVCOL_RANGE_JPEG ? ColorRange.Full : ColorRange.Limited;

        var colorSpace = AVStream->codecpar->color_space;
        if (colorSpace == AVColorSpace.AVCOL_SPC_BT709)
            ColorSpace = ColorSpace.BT709;
        else if (colorSpace == AVColorSpace.AVCOL_SPC_BT470BG)
            ColorSpace = ColorSpace.BT601;
        else if (colorSpace == AVColorSpace.AVCOL_SPC_BT2020_NCL || colorSpace == AVColorSpace.AVCOL_SPC_BT2020_CL)
            ColorSpace = ColorSpace.BT2020;
            
        ColorTransfer = AVStream->codecpar->color_trc;

        // Avoid early check for HDR
        //if (ColorTransfer == AVColorTransferCharacteristic.AribStdB67)
        //    HDRFormat = HDRFormat.HLG;
        //else if (ColorTransfer == AVColorTransferCharacteristic.Smpte2084)
        //{
        //    for (int i = 0; i < AVStream->codecpar->nb_coded_side_data; i++)
        //    {
        //        var csdata = AVStream->codecpar->coded_side_data[i];
        //        switch (csdata.type)
        //        {
        //            case AVPacketSideDataType.DoviConf:
        //                HDRFormat = HDRFormat.DolbyVision;
        //                break;
        //            case AVPacketSideDataType.DynamicHdr10Plus:
        //                HDRFormat = HDRFormat.HDRPlus;
        //                break;
        //            case AVPacketSideDataType.ContentLightLevel:
        //                //AVContentLightMetadata t2 = *((AVContentLightMetadata*)csdata.data);
        //                break;
        //            case AVPacketSideDataType.MasteringDisplayMetadata:
        //                //AVMasteringDisplayMetadata t1 = *((AVMasteringDisplayMetadata*)csdata.data);
        //                HDRFormat = HDRFormat.HDR;
        //                break;
        //        }
        //    }
        //}

        if (frame != null)
        {
            AVFrameSideData* frameSideData;
            if ((frameSideData = av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX)) != null && frameSideData->data != null)
            {
                int_array9 displayMatrix = Marshal.PtrToStructure<int_array9>((nint)frameSideData->data);
                var rotation = -Math.Round(av_display_rotation_get(displayMatrix));
                Rotation = rotation - (360*Math.Floor(rotation/360 + 0.9/360));
            }

            if ((frame->flags & AV_FRAME_FLAG_INTERLACED) != 0)
                FieldOrder = (frame->flags & AV_FRAME_FLAG_TOP_FIELD_FIRST) != 0? DeInterlace.TopField : DeInterlace.BottomField;

            ColorRange = frame->color_range == AVColorRange.AVCOL_RANGE_JPEG ? ColorRange.Full : ColorRange.Limited;

            if (frame->color_trc != AVColorTransferCharacteristic.AVCOL_TRC_UNSPECIFIED)
                ColorTransfer = frame->color_trc;

            if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT709)
                ColorSpace = ColorSpace.BT709;
            else if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT470BG)
                ColorSpace = ColorSpace.BT601;
            else if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT2020_NCL || frame->colorspace == AVColorSpace.AVCOL_SPC_BT2020_CL)
                ColorSpace = ColorSpace.BT2020;

            if (ColorTransfer == AVColorTransferCharacteristic.AVCOL_TRC_ARIB_STD_B67)
                HDRFormat = HDRFormat.HLG;
            else if (ColorTransfer == AVColorTransferCharacteristic.AVCOL_TRC_SMPTE2084)
            {
                var dolbyData = av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DOVI_METADATA);
                if (dolbyData != null)
                    HDRFormat = HDRFormat.DolbyVision;
                else
                {
                    var hdrPlusData = av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DYNAMIC_HDR_PLUS);
                    if (hdrPlusData != null)
                    {
                        //AVDynamicHDRPlus* x1 = (AVDynamicHDRPlus*)hdrPlusData->data;
                        HDRFormat = HDRFormat.HDRPlus;
                    }
                    else
                    {
                        //AVMasteringDisplayMetadata t1;
                        //AVContentLightMetadata t2;

                        //var masterData = av_frame_get_side_data(frame, AVFrameSideDataType.MasteringDisplayMetadata);
                        //if (masterData != null)
                        //    t1 = *((AVMasteringDisplayMetadata*)masterData->data);
                        //var lightData   = av_frame_get_side_data(frame, AVFrameSideDataType.ContentLightLevel);
                        //if (lightData != null)
                        //    t2 = *((AVContentLightMetadata*) lightData->data);

                        HDRFormat = HDRFormat.HDR;
                    }
                }
            }

            if (HDRFormat != HDRFormat.None) // Forcing BT.2020 with PQ/HLG transfer?
                ColorSpace = ColorSpace.BT2020;
            else if (ColorSpace == ColorSpace.None)
                ColorSpace = Height > 576 ? ColorSpace.BT709 : ColorSpace.BT601;
        }

        if (PixelFormat == AVPixelFormat.AV_PIX_FMT_NONE || PixelPlanes > 0) // Should re-analyze? (possible to get different pixel format on 2nd... call?)
            return;

        PixelFormatDesc = av_pix_fmt_desc_get(PixelFormat);
        var comps       = PixelFormatDesc->comp.ToArray();
        PixelComps      = new AVComponentDescriptor[PixelFormatDesc->nb_components];
        for (int i=0; i<PixelComps.Length; i++)
            PixelComps[i] = comps[i];

        PixelInterleaved= PixelFormatDesc->log2_chroma_w != PixelFormatDesc->log2_chroma_h;
        IsRGB           = (PixelFormatDesc->flags & AV_PIX_FMT_FLAG_RGB) != 0;

        PixelSameDepth  = true;
        PixelPlanes     = 0;
        if (PixelComps.Length > 0)
        {
            PixelComp0Depth = PixelComps[0].depth;
            int prevBit     = PixelComp0Depth;
            for (int i=0; i<PixelComps.Length; i++)
            {
                if (PixelComps[i].plane > PixelPlanes)
                    PixelPlanes = PixelComps[i].plane;

                if (prevBit != PixelComps[i].depth)
                    PixelSameDepth = false;
            }

            PixelPlanes++;
        }
    }
}
