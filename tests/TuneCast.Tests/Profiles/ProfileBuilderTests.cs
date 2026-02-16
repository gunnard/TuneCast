using System.Linq;
using Jellyfin.Data.Enums;
using TuneCast.Models;
using TuneCast.Profiles;
using MediaBrowser.Model.Dlna;
using Xunit;

namespace TuneCast.Tests.Profiles;

/// <summary>
/// Tests for the ProfileBuilder — verifies that playback policies
/// are correctly translated into Jellyfin DeviceProfile structures.
/// </summary>
public class ProfileBuilderTests
{
    // ── Default policy → no profile ────────────────────────────────────

    [Fact]
    public void DefaultPolicy_ReturnsNull()
    {
        var client = Client(ClientType.Desktop);
        var policy = PlaybackPolicy.Default();

        var profile = ProfileBuilder.Build(client, policy);

        Assert.Null(profile);
    }

    // ── Direct play allowed → has DirectPlayProfiles ───────────────────

    [Fact]
    public void DirectPlayAllowed_HasDirectPlayProfiles()
    {
        var client = Client(ClientType.Roku);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            AllowDirectStream = true,
            AllowTranscoding = false,
            Confidence = 0.8,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.NotEmpty(profile!.DirectPlayProfiles);
    }

    [Fact]
    public void DirectPlayDisabled_EmptyDirectPlayProfiles()
    {
        var client = Client(ClientType.WebBrowser);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = false,
            AllowDirectStream = true,
            AllowTranscoding = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.Empty(profile!.DirectPlayProfiles);
    }

    // ── Transcoding profiles ───────────────────────────────────────────

    [Fact]
    public void TranscodingAllowed_HasTranscodingProfiles()
    {
        var client = Client(ClientType.WebBrowser);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.NotEmpty(profile!.TranscodingProfiles);

        var hlsProfile = profile.TranscodingProfiles
            .FirstOrDefault(t => t.Protocol == MediaStreamProtocol.hls);
        Assert.NotNull(hlsProfile);
        Assert.Equal("h264", hlsProfile!.VideoCodec);
    }

    [Fact]
    public void NeitherTranscodeNorDirectStream_EmptyTranscodingProfiles()
    {
        var client = Client(ClientType.Desktop);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            AllowDirectStream = false,
            AllowTranscoding = false,
            Confidence = 0.9,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.Empty(profile!.TranscodingProfiles);
    }

    // ── Bitrate cap ────────────────────────────────────────────────────

    [Fact]
    public void WithBitrateCap_SetsMaxStreamingBitrate()
    {
        var client = Client(ClientType.AndroidMobile);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            BitrateCap = 8_000_000,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.Equal(8_000_000, profile!.MaxStreamingBitrate);
        Assert.Equal(8_000_000, profile.MaxStaticBitrate);
    }

    [Fact]
    public void NoBitrateCap_NullMaxBitrate()
    {
        var client = Client(ClientType.Desktop);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.8,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.Null(profile!.MaxStreamingBitrate);
    }

    // ── Profile name ───────────────────────────────────────────────────

    [Fact]
    public void ProfileName_ContainsClientType()
    {
        var client = Client(ClientType.Roku);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            AllowDirectStream = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        Assert.Contains("Roku", profile!.Name);
    }

    // ── Client-specific codec lists ────────────────────────────────────

    [Fact]
    public void WebBrowser_DirectPlayProfiles_ContainVp9()
    {
        var client = Client(ClientType.WebBrowser);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var videoProfile = profile!.DirectPlayProfiles
            .FirstOrDefault(p => p.Type == DlnaProfileType.Video);
        Assert.NotNull(videoProfile);
        Assert.Contains("vp9", videoProfile!.VideoCodec);
    }

    [Fact]
    public void Roku_DirectPlayProfiles_DoNotContainMkv()
    {
        var client = Client(ClientType.Roku);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var videoProfile = profile!.DirectPlayProfiles
            .FirstOrDefault(p => p.Type == DlnaProfileType.Video);
        Assert.NotNull(videoProfile);
        Assert.DoesNotContain("mkv", videoProfile!.Container);
    }

    [Fact]
    public void Desktop_DirectPlayProfiles_ContainMkv()
    {
        var client = Client(ClientType.Desktop);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.8,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var videoProfile = profile!.DirectPlayProfiles
            .FirstOrDefault(p => p.Type == DlnaProfileType.Video);
        Assert.NotNull(videoProfile);
        Assert.Contains("mkv", videoProfile!.Container);
    }

    [Fact]
    public void Desktop_DirectPlayProfiles_ContainAllMajorCodecs()
    {
        var client = Client(ClientType.Desktop);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.8,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var videoProfile = profile!.DirectPlayProfiles
            .First(p => p.Type == DlnaProfileType.Video);
        Assert.Contains("h264", videoProfile.VideoCodec);
        Assert.Contains("hevc", videoProfile.VideoCodec);
        Assert.Contains("av1", videoProfile.VideoCodec);
    }

    // ── Audio-only profile ─────────────────────────────────────────────

    [Fact]
    public void DirectPlayAllowed_IncludesAudioProfile()
    {
        var client = Client(ClientType.Desktop);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = true,
            Confidence = 0.8,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var audioProfile = profile!.DirectPlayProfiles
            .FirstOrDefault(p => p.Type == DlnaProfileType.Audio);
        Assert.NotNull(audioProfile);
    }

    // ── Audio transcoding fallback ─────────────────────────────────────

    [Fact]
    public void TranscodingAllowed_IncludesAudioTranscodingProfile()
    {
        var client = Client(ClientType.WebBrowser);
        var policy = new PlaybackPolicy
        {
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Confidence = 0.7,
        };

        var profile = ProfileBuilder.Build(client, policy);

        Assert.NotNull(profile);
        var audioTranscode = profile!.TranscodingProfiles
            .FirstOrDefault(t => t.Type == DlnaProfileType.Audio);
        Assert.NotNull(audioTranscode);
    }

    private static ClientModel Client(ClientType type) => new()
    {
        ClientType = type,
        DeviceId = "test-device",
        ClientName = type.ToString(),
    };
}
