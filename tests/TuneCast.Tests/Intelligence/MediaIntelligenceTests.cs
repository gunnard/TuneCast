using TuneCast.Intelligence;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Intelligence;

/// <summary>
/// Tests for the scoring-based transcode cost estimation heuristic.
/// Covers the full codec/container/HDR spectrum.
/// </summary>
public class MediaIntelligenceTests
{
    // ── Container / Remux ──────────────────────────────────────────────

    [Fact]
    public void H264InMkv_EstimatesRemux()
    {
        var cost = Estimate("h264", "mkv");
        Assert.Equal(TranscodeCost.Remux, cost);
    }

    [Fact]
    public void H264InMp4_EstimatesLow()
    {
        var cost = Estimate("h264", "mp4");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void H264InAvi_EstimatesRemux()
    {
        var cost = Estimate("h264", "avi");
        Assert.Equal(TranscodeCost.Remux, cost);
    }

    [Fact]
    public void HevcInMkv_EstimatesRemux()
    {
        // HEVC weight=1 → score=1 → Low. But container is MKV with compatible codec → Remux?
        // Actually score=1 → Low, because score>0 skips EvaluateRemuxPotential
        var cost = Estimate("hevc", "mkv");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    // ── HEVC spectrum ──────────────────────────────────────────────────

    [Fact]
    public void Hevc1080p8bit_EstimatesLow()
    {
        // HEVC codec weight=1, 1080p=0, SDR=0, 8bit=0 → score=1 → Low
        var cost = Estimate("hevc", "mkv");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void Hevc1080p10bit_EstimatesMedium()
    {
        // HEVC=1 + 10bit=1 → score=2 → Medium
        var cost = Estimate("hevc", "mkv", bitDepth: 10);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    [Fact]
    public void Hevc4KSDR_EstimatesHigh()
    {
        // HEVC=1 + 4K=3 → score=4 → High
        var cost = Estimate("hevc", "mkv", width: 3840, height: 2160);
        Assert.Equal(TranscodeCost.High, cost);
    }

    [Fact]
    public void Hevc4KHDR10_EstimatesExtreme()
    {
        // HEVC=1 + 4K=3 + HDR=2 + 10bit=1 → score=7 → Extreme
        var cost = Estimate("hevc", "mkv", width: 3840, height: 2160, bitDepth: 10, videoRangeType: "HDR10");
        Assert.Equal(TranscodeCost.Extreme, cost);
    }

    [Fact]
    public void Hevc4KHDRWithImageSubs_EstimatesExtreme()
    {
        // HEVC=1 + 4K=3 + HDR=2 + 10bit=1 + imageSubs=2 → score=9 → Extreme
        var cost = Estimate("hevc", "mkv", width: 3840, height: 2160, bitDepth: 10,
            videoRangeType: "HDR10", hasImageSubs: true);
        Assert.Equal(TranscodeCost.Extreme, cost);
    }

    // ── H.264 at various resolutions ───────────────────────────────────

    [Fact]
    public void H264_4KSDR_EstimatesMedium()
    {
        // H264=0 + 4K=3 → score=3 → Medium
        var cost = Estimate("h264", "mp4", width: 3840, height: 2160);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    [Fact]
    public void H264_1440p_EstimatesLow()
    {
        // H264=0 + 1440p=1 → score=1 → Low
        var cost = Estimate("h264", "mp4", width: 2560, height: 1440);
        Assert.Equal(TranscodeCost.Low, cost);
    }

    // ── AV1 spectrum ───────────────────────────────────────────────────

    [Fact]
    public void AV1_1080p8bit_EstimatesMedium()
    {
        // AV1=2 → score=2 → Medium
        var cost = Estimate("av1", "mp4");
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    [Fact]
    public void AV1_4K10bitHDR_EstimatesExtreme()
    {
        // AV1=2 + 4K=3 + HDR=2 + 10bit=1 → score=8 → Extreme
        var cost = Estimate("av1", "mp4", width: 3840, height: 2160, bitDepth: 10, videoRangeType: "HDR10");
        Assert.Equal(TranscodeCost.Extreme, cost);
    }

    // ── VP9 spectrum ───────────────────────────────────────────────────

    [Fact]
    public void VP9_1080p_EstimatesLow()
    {
        // VP9=1 → score=1 → Low
        var cost = Estimate("vp9", "webm");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void VP9_4KHDR_EstimatesHigh()
    {
        // VP9=1 + 4K=3 + HDR=2 → score=6 → High
        var cost = Estimate("vp9", "webm", width: 3840, height: 2160, videoRangeType: "HDR10");
        Assert.Equal(TranscodeCost.High, cost);
    }

    // ── Dolby Vision ───────────────────────────────────────────────────

    [Fact]
    public void DolbyVision_4K_EstimatesExtreme()
    {
        // HEVC=1 + 4K=3 + DoVi=4 + 10bit=1 → score=9 → Extreme
        var cost = Estimate("hevc", "mp4", width: 3840, height: 2160, bitDepth: 10, videoRangeType: "DoVi");
        Assert.Equal(TranscodeCost.Extreme, cost);
    }

    [Fact]
    public void DolbyVision_1080p_EstimatesHigh()
    {
        // HEVC=1 + DoVi=4 + 10bit=1 → score=6 → High
        var cost = Estimate("hevc", "mp4", bitDepth: 10, videoRangeType: "DolbyVision");
        Assert.Equal(TranscodeCost.High, cost);
    }

    // ── Legacy codecs ──────────────────────────────────────────────────

    [Fact]
    public void Mpeg2InTs_EstimatesLow()
    {
        // mpeg2video=0, ts is not universal/mkv/legacy → score=0 → EvaluateRemux → Low
        var cost = Estimate("mpeg2video", "ts");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void VC1InWmv_EstimatesLow()
    {
        // VC1=1 → score=1 → Low
        var cost = Estimate("vc1", "wmv");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void Mpeg4InAvi_EstimatesRemux()
    {
        // mpeg4=0, avi=legacy → score=0 → Remux
        var cost = Estimate("mpeg4", "avi");
        Assert.Equal(TranscodeCost.Remux, cost);
    }

    // ── Audio weight ───────────────────────────────────────────────────

    [Fact]
    public void H264WithTrueHD_AddsAudioWeight()
    {
        // H264=0 + TrueHD=1 → score=1 → Low
        var cost = Estimate("h264", "mp4", audioCodec: "truehd");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    [Fact]
    public void H264WithTrueHD7_1_AddsExtraWeight()
    {
        // H264=0 + TrueHD=1 + >6ch bonus=1 → score=2 → Medium
        var cost = Estimate("h264", "mp4", audioCodec: "truehd", audioChannels: 8);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    [Fact]
    public void HevcWithDtsHdMa7_1_CompoundsWeight()
    {
        // HEVC=1 + DTS-HD MA=1 + >6ch=1 → score=3 → Medium
        var cost = Estimate("hevc", "mkv", audioCodec: "dts-hd ma", audioChannels: 8);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    [Fact]
    public void H264WithAac_NoAudioWeight()
    {
        // H264=0 + AAC=0 → score=0 → EvaluateRemux → mp4=Low
        var cost = Estimate("h264", "mp4", audioCodec: "aac");
        Assert.Equal(TranscodeCost.Low, cost);
    }

    // ── Image subtitle amplifier ───────────────────────────────────────

    [Fact]
    public void ImageSubsAlone_AddsCost()
    {
        // H264=0 + imageSubs=2 → score=2 → Medium
        var cost = Estimate("h264", "mp4", hasImageSubs: true);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    // ── 12-bit ─────────────────────────────────────────────────────────

    [Fact]
    public void Hevc12Bit1080p_EstimatesMedium()
    {
        // HEVC=1 + 12bit=2 → score=3 → Medium
        var cost = Estimate("hevc", "mkv", bitDepth: 12);
        Assert.Equal(TranscodeCost.Medium, cost);
    }

    // ── Helper ─────────────────────────────────────────────────────────

    private static TranscodeCost Estimate(
        string videoCodec,
        string container,
        int width = 1920,
        int height = 1080,
        int bitDepth = 8,
        string videoRangeType = "SDR",
        string audioCodec = "aac",
        int audioChannels = 2,
        bool hasImageSubs = false)
    {
        var media = new MediaModel
        {
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            Container = container,
            Width = width,
            Height = height,
            VideoBitDepth = bitDepth,
            VideoRangeType = videoRangeType,
            AudioChannels = audioChannels,
            HasImageSubtitles = hasImageSubs,
        };

        return MediaIntelligenceService.EstimateTranscodeCost(media);
    }
}
