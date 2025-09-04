using FFmpeg.AutoGen;
using FlyleafLib.MediaFramework.MediaDecoder;
using FlyleafLib.MediaFramework.MediaDemuxer;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using static FFmpeg.AutoGen.ffmpeg;

namespace FlyleafLib.MediaFramework.MediaStream;

public unsafe class VideoStream : StreamBase
{
    /* TODO
     * Color Primaries (not really required?)
     * Chroma Location (when we add support in renderer)
     */

    public AVRational                   SAR                 { get; set; }
    public ColorRange                   ColorRange          { get; set; }
    public ColorSpace                   ColorSpace          { get; set; }
    public ColorType                    ColorType           { get; set; }
    public AVColorTransferCharacteristic
                                        ColorTransfer       { get; set; }
    public Cropping                     Cropping            { get; set; }
    public VideoFrameFormat             FieldOrder          { get; set; }
    public double                       Rotation            { get; set; }
    public double                       FPS                 { get; set; }
    public long                         FrameDuration       { get ;set; }
    public double                       FPS2                { get; set; } // interlace
    public long                         FrameDuration2      { get ;set; } // interlace
    public uint                         Height              { get; set; }
    public HDRFormat                    HDRFormat           { get; set; }

    public AVComponentDescriptor[]      PixelComps          { get; set; }
    public int                          PixelComp0Depth     { get; set; }
    public AVPixelFormat                PixelFormat         { get; set; }
    public AVPixFmtDescriptor*          PixelFormatDesc     { get; set; }
    public string                       PixelFormatStr      { get; set; }
    public int                          PixelPlanes         { get; set; }
    public bool                         PixelInterleaved    { get; set; }
    public int                          TotalFrames         { get; set; }
    public uint                         Width               { get; set; }
    public bool                         FixTimestamps       { get; set; }
    
    internal CropRect cropStreamRect, cropFrameRect, cropRect; // Stream Crop + Codec Padding + Texture Padding
    bool hasStreamCrop;

    public VideoStream(Demuxer demuxer, AVStream* st) : base(demuxer, st)
        => Type = MediaType.Video;

    /* NOTES
    * Initialize()  during Demuxer.FillInfo      (Analysed or Basic)
    * Refresh()     during Decoder.FillFromCodec (First valid frame + Format Changed)
    * 
    * Some fields might know only during Refresh, should make sure we fill them (especially if we didn't analyse the input)
    * Don't default (eg Color Range) during Initialize, wait for Refresh
    * Priorities: AVFrame => AVCodecContext => AVStream (AVCodecParameters) *Try to keep Color config from stream instead
    */

    // First time fill from AVStream's Codec Parameters | Info to help choosing stream quality mainly (not in use yet)
    public override void Initialize()
    {
        PixelFormat     = (AVPixelFormat)cp->format;
        if (PixelFormat != AVPixelFormat.AV_PIX_FMT_NONE)
            AnalysePixelFormat();

        Width   = (uint)cp->width;
        Height  = (uint)cp->height;
        SAR     = av_guess_sample_aspect_ratio(null, AVStream, null);

        if ((Demuxer.FormatContext->iformat->flags & AVFMT_NOTIMESTAMPS) != 0)
            FixTimestamps = true;

        if (Demuxer.Config.ForceFPS > 0)
            FPS = Demuxer.Config.ForceFPS;
        else
        {
            FPS = av_q2d(av_guess_frame_rate(Demuxer.FormatContext, AVStream, null));
            if (double.IsNaN(FPS) || double.IsInfinity(FPS) || FPS < 0.0)
                FPS = 0.0;
        }

        if (FPS > 0)
        {
            FrameDuration   = (long)(10_000_000 / FPS);
            TotalFrames     = (int)(Duration / FrameDuration);
        }

        FieldOrder      = cp->field_order == AVFieldOrder.AV_FIELD_TT ? VideoFrameFormat.InterlacedTopFieldFirst : (cp->field_order == AVFieldOrder.AV_FIELD_BB ? VideoFrameFormat.InterlacedBottomFieldFirst : VideoFrameFormat.Progressive);
        ColorTransfer   = cp->color_trc;

        if (cp->color_range == AVColorRange.AVCOL_RANGE_MPEG)
            ColorRange = ColorRange.Limited;
        else if (cp->color_range == AVColorRange.AVCOL_RANGE_JPEG)
            ColorRange = ColorRange.Full;

        if (cp->color_space == AVColorSpace.AVCOL_SPC_BT709)
            ColorSpace = ColorSpace.Bt709;
        else if (cp->color_space == AVColorSpace.AVCOL_SPC_BT470BG)
            ColorSpace = ColorSpace.Bt601;
        else if (cp->color_space == AVColorSpace.AVCOL_SPC_BT2020_NCL || cp->color_space == AVColorSpace.AVCOL_SPC_BT2020_CL)
            ColorSpace = ColorSpace.Bt2020;

        if (cp->nb_coded_side_data == 0)
            return;
        
        var rotData = av_packet_side_data_get(cp->coded_side_data, cp->nb_coded_side_data, AVPacketSideDataType.AV_PKT_DATA_DISPLAYMATRIX);
        if (rotData != null && rotData->data != null)
        {
            int_array9 displayMatrix = Marshal.PtrToStructure<int_array9>((nint)rotData->data);
            double rotation = -Math.Round(av_display_rotation_get(displayMatrix));
            Rotation = rotation - (360 * Math.Floor(rotation / 360 + 0.9 / 360));
        }

        var cropData= av_packet_side_data_get(cp->coded_side_data, cp->nb_coded_side_data, AVPacketSideDataType.AV_PKT_DATA_FRAME_CROPPING);
        if (cropData != null && cropData->size == 16)
        {
            var cropByes = new ReadOnlySpan<byte>(cropData->data, 16).ToArray();
            cropStreamRect = new(
                top:    BitConverter.ToUInt32(cropByes, 0),
                bottom: BitConverter.ToUInt32(cropByes, 4),
                left:   BitConverter.ToUInt32(cropByes, 8),
                right:  BitConverter.ToUInt32(cropByes, 12)
                );

            hasStreamCrop = cropStreamRect != CropRect.Empty;
        }
    }

    // >= Second time fill from Decoder / Frame | TBR: We could avoid re-filling it when re-enabling a stream ... when same PixelFormat (VideoAcceleration)
    public void Refresh(VideoDecoder decoder, AVFrame* frame) // TBR: Can be filled multiple times from different Codecs
    {
        var codecCtx= decoder.CodecCtx;
        var format  = decoder.VideoAccelerated && codecCtx->sw_pix_fmt != AVPixelFormat.AV_PIX_FMT_NONE ? codecCtx->sw_pix_fmt : codecCtx->pix_fmt;

        if (PixelFormat != format)
        {
            if (format == AVPixelFormat.AV_PIX_FMT_NONE)
                return;

            PixelFormat = format;
            AnalysePixelFormat();
        }
        else if (format == AVPixelFormat.AV_PIX_FMT_NONE) // Both None (Should be removed from Demuxer's streams?*)
            return;

        ReUpdate();

        if (codecCtx->bit_rate > 0)
            BitRate = codecCtx->bit_rate; // for logging only

        if (SAR.num == 0)
        {
            if (frame->sample_aspect_ratio.num != 0)
                SAR = frame->sample_aspect_ratio;
            else if (codecCtx->sample_aspect_ratio.num != 0)
                SAR = codecCtx->sample_aspect_ratio;
            else if (SAR.num == 0)
                SAR = new() { num = 1, den = 1 };
        }

        cropRect = CropRect.Empty;

        // Stream's Crop
        if (hasStreamCrop)
        {
            Cropping =  Cropping.Stream;

            cropRect.Top    += cropStreamRect.Top;
            cropRect.Bottom += cropStreamRect.Bottom;
            cropRect.Left   += cropStreamRect.Left;
            cropRect.Right  += cropStreamRect.Right;
            
        }
        else
            Cropping = Cropping.None;

        // Codec's Crop (Frame)
        cropFrameRect = new(
            top:    (uint)frame->crop_top,
            bottom: (uint)frame->crop_bottom,
            left:   (uint)frame->crop_left,
            right:  (uint)frame->crop_right
            );

        if (cropFrameRect != CropRect.Empty)
        {
            Cropping |= Cropping.Codec;

            cropRect.Top    += cropFrameRect.Top;
            cropRect.Bottom += cropFrameRect.Bottom;
            cropRect.Left   += cropFrameRect.Left;
            cropRect.Right  += cropFrameRect.Right;
        }

        // HW Texture's Crop
        if (decoder.VideoAccelerated)
        {
            var desc = decoder.textureFFmpeg.Description;

            if (desc.Width > codecCtx->coded_width)
            {
                cropRect.Right += (uint)(desc.Width - codecCtx->coded_width);
                Cropping |= Cropping.Texture;
            }

            if (desc.Height > codecCtx->coded_height)
            {
                cropRect.Bottom += (uint)(desc.Height - codecCtx->coded_height);
                Cropping |= Cropping.Texture;
            }
        }

        Width   = (uint)(frame->width  - (cropRect.Left + cropRect.Right));
        Height  = (uint)(frame->height - (cropRect.Top  + cropRect.Bottom));

        if ((frame->flags & AV_FRAME_FLAG_INTERLACED) != 0)
            FieldOrder = (frame->flags & AV_FRAME_FLAG_TOP_FIELD_FIRST) != 0 ? VideoFrameFormat.InterlacedTopFieldFirst : VideoFrameFormat.InterlacedBottomFieldFirst;
        else
            FieldOrder = codecCtx->field_order == AVFieldOrder.AV_FIELD_TT ? VideoFrameFormat.InterlacedTopFieldFirst : (codecCtx->field_order == AVFieldOrder.AV_FIELD_BB ? VideoFrameFormat.InterlacedBottomFieldFirst : VideoFrameFormat.Progressive);

        if (ColorTransfer == AVColorTransferCharacteristic.AVCOL_TRC_UNSPECIFIED) // TBR: AVStream has AribStdB67 and Frame/CodecCtx has Bt2020_10 (priority to stream?)*
        {
            if (frame->color_trc != AVColorTransferCharacteristic.AVCOL_TRC_UNSPECIFIED)
                ColorTransfer = frame->color_trc;
            else if (codecCtx->color_trc != AVColorTransferCharacteristic.AVCOL_TRC_UNSPECIFIED)
                ColorTransfer = codecCtx->color_trc;
        }

        if (ColorRange == ColorRange.None)
        {
            if (frame->color_range == AVColorRange.AVCOL_RANGE_MPEG)
                ColorRange = ColorRange.Limited;
            else if (frame->color_range == AVColorRange.AVCOL_RANGE_JPEG)
                ColorRange = ColorRange.Full;
            else if (codecCtx->color_range == AVColorRange.AVCOL_RANGE_MPEG)
                ColorRange = ColorRange.Limited;
            else if (codecCtx->color_range == AVColorRange.AVCOL_RANGE_JPEG)
                ColorRange = ColorRange.Full;
            else if (ColorRange == ColorRange.None)
                ColorRange = ColorType == ColorType.YUV && !PixelFormatStr.Contains("j") ? ColorRange.Limited : ColorRange.Full; // yuvj family defaults to full
        }
        
        if (ColorTransfer == AVColorTransferCharacteristic.AVCOL_TRC_ARIB_STD_B67)
            HDRFormat = HDRFormat.HLG;
        else if (ColorTransfer == AVColorTransferCharacteristic.AVCOL_TRC_SMPTE2084)
        {
            if (av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DOVI_METADATA) != null)
                HDRFormat = HDRFormat.DolbyVision;
            else if (av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DYNAMIC_HDR_PLUS) != null)
                HDRFormat = HDRFormat.HDRPlus;
            else
                HDRFormat = HDRFormat.HDR;
        }

        if (HDRFormat != HDRFormat.None) // Forcing BT.2020 with PQ/HLG transfer?
            ColorSpace = ColorSpace.Bt2020;

        if (ColorSpace == ColorSpace.None)
        {
            if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT709)
                ColorSpace = ColorSpace.Bt709;
            else if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT470BG)
                ColorSpace = ColorSpace.Bt601;
            else if (frame->colorspace == AVColorSpace.AVCOL_SPC_BT2020_NCL || frame->colorspace == AVColorSpace.AVCOL_SPC_BT2020_CL)
                ColorSpace = ColorSpace.Bt2020;
            else if (codecCtx->colorspace == AVColorSpace.AVCOL_SPC_BT709)
                ColorSpace = ColorSpace.Bt709;
            else if (codecCtx->colorspace == AVColorSpace.AVCOL_SPC_BT470BG)
                ColorSpace = ColorSpace.Bt601;
            else if (codecCtx->colorspace == AVColorSpace.AVCOL_SPC_BT2020_NCL || codecCtx->colorspace == AVColorSpace.AVCOL_SPC_BT2020_CL)
                ColorSpace = ColorSpace.Bt2020;
            else if (ColorSpace == ColorSpace.None)
                ColorSpace = Height > 576 ? ColorSpace.Bt709 : ColorSpace.Bt601;
        }
        
        // We consider that FPS can't change (only if it was missing we fill it)
        if (FPS == 0.0)
        {
            var newFps      = av_q2d(codecCtx->framerate);
            FPS             = double.IsNaN(newFps) || double.IsInfinity(newFps) || newFps <= 0.0 ? 25 : newFps; // Force default to 25 fps
            FrameDuration   = (long)(10_000_000 / FPS);
            TotalFrames     = (int)(Duration / FrameDuration);
            Demuxer.VideoPackets.frameDuration = FrameDuration;
        }

        // FPS2 / FrameDuration2 (DeInterlace)
        if (FieldOrder != VideoFrameFormat.Progressive)
        {
            FPS2 = FPS;
            FrameDuration2 = FrameDuration;
            FPS /= 2;
            FrameDuration *= 2;
        }
        else
        {
            FPS2 = FPS * 2;
            FrameDuration2 = FrameDuration / 2;
        }

        var rotData = av_frame_get_side_data(frame, AVFrameSideDataType.AV_FRAME_DATA_DISPLAYMATRIX);
        if (rotData != null && rotData->data != null)
        {
            int_array9 displayMatrix = Marshal.PtrToStructure<int_array9>((nint)rotData->data);
            var rotation = -Math.Round(av_display_rotation_get(displayMatrix));
            Rotation = rotation - (360*Math.Floor(rotation/360 + 0.9/360));
        }

        if (CanDebug)
            Demuxer.Log.Debug($"Stream Info (Filled)\r\n{GetDump()}");
    }

    void AnalysePixelFormat()
    {
        PixelFormatStr  = LowerCaseFirstChar(PixelFormat.ToString());
        PixelFormatDesc = av_pix_fmt_desc_get(PixelFormat);
        PixelComps      = PixelFormatDesc->comp.ToArray();
        PixelInterleaved= PixelFormatDesc->log2_chroma_w != PixelFormatDesc->log2_chroma_h;
        ColorType       = PixelComps.Length == 1 ? ColorType.Gray : ((PixelFormatDesc->flags & AV_PIX_FMT_FLAG_RGB) != 0 ? ColorType.RGB : ColorType.YUV);
        PixelPlanes     = 0;
        
        if (PixelComps.Length > 0)
        {
            PixelComp0Depth = PixelComps[0].depth;
            for (int i = 0; i < PixelComps.Length; i++)
                if (PixelComps[i].plane > PixelPlanes)
                    PixelPlanes = PixelComps[i].plane;

            PixelPlanes++;
        }
    }

    public AspectRatio GetDAR()
    {
        int x, y;
        _ = av_reduce(&x, &y, Width * SAR.num, Height * SAR.den, 1024 * 1024);
        return new(x, y);
    }
}
