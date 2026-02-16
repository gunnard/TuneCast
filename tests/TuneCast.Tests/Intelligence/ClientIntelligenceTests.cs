using System;
using TuneCast.Intelligence;
using TuneCast.Models;
using TuneCast.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using MediaBrowser.Controller.Session;
using Xunit;

namespace TuneCast.Tests.Intelligence;

/// <summary>
/// Tests for client type resolution from session metadata.
/// </summary>
public class ClientIntelligenceTests
{
    private readonly ClientIntelligenceService _service;

    public ClientIntelligenceTests()
    {
        var dataStore = new Mock<IPluginDataStore>();
        dataStore.Setup(d => d.GetClientModel(It.IsAny<string>())).Returns((ClientModel?)null);
        var logger = new Mock<ILogger<ClientIntelligenceService>>();
        _service = new ClientIntelligenceService(dataStore.Object, logger.Object);
    }

    [Theory]
    [InlineData("Jellyfin Web", "Chrome", ClientType.WebBrowser)]
    [InlineData("Jellyfin-web", "Firefox", ClientType.WebBrowser)]
    [InlineData("Jellyfin for Roku", "Roku Ultra", ClientType.Roku)]
    [InlineData("Swiftfin", "iPhone 15", ClientType.SwiftfinIos)]
    [InlineData("Jellyfin Media Player", "MacBook", ClientType.Desktop)]
    [InlineData("Jellyfin Desktop", "Linux PC", ClientType.Desktop)]
    [InlineData("Kodi", "Shield TV", ClientType.Kodi)]
    public void ResolveClient_IdentifiesClientType(string clientName, string deviceName, ClientType expectedType)
    {
        var session = CreateSession(clientName, deviceName);

        var result = _service.ResolveClient(session);

        Assert.Equal(expectedType, result.ClientType);
    }

    [Fact]
    public void ResolveClient_SwiftfinOnAppleTv_ResolvesToTvos()
    {
        var session = CreateSession("Swiftfin", "Apple TV");

        var result = _service.ResolveClient(session);

        Assert.Equal(ClientType.SwiftfinTvos, result.ClientType);
    }

    [Fact]
    public void ResolveClient_AndroidTvWithFireDevice_ResolvesToFireTv()
    {
        var session = CreateSession("Jellyfin AndroidTV", "Fire TV Stick 4K");

        var result = _service.ResolveClient(session);

        Assert.Equal(ClientType.FireTv, result.ClientType);
    }

    [Fact]
    public void ResolveClient_CachesResult()
    {
        var session = CreateSession("Jellyfin Web", "Chrome");

        var first = _service.ResolveClient(session);
        var second = _service.ResolveClient(session);

        Assert.Same(first, second);
    }

    [Fact]
    public void InvalidateCache_ClearsEntry()
    {
        var session = CreateSession("Jellyfin Web", "Chrome");
        var first = _service.ResolveClient(session);

        _service.InvalidateCache(session.DeviceId);

        var afterInvalidation = _service.GetCachedClient(session.DeviceId);
        Assert.Null(afterInvalidation);
    }

    [Fact]
    public void ResolveClient_SetsBaselineCodecConfidence()
    {
        var session = CreateSession("Jellyfin Web", "Chrome");

        var result = _service.ResolveClient(session);

        Assert.True(result.CodecConfidence.ContainsKey("h264"));
        Assert.True(result.CodecConfidence["h264"] > 0.8);
        Assert.True(result.CodecConfidence.ContainsKey("hevc"));
        Assert.True(result.CodecConfidence["hevc"] < 0.5);
    }

    [Fact]
    public void ResolveClient_DesktopClient_HasHighHevcConfidence()
    {
        var session = CreateSession("Jellyfin Desktop", "MacBook Pro");

        var result = _service.ResolveClient(session);

        Assert.True(result.CodecConfidence["hevc"] >= 0.9);
    }

    private static SessionInfo CreateSession(string clientName, string deviceName)
    {
        return new SessionInfo(null!, null!)
        {
            Client = clientName,
            DeviceName = deviceName,
            DeviceId = $"test-{Guid.NewGuid():N}",
            ApplicationVersion = "1.0.0",
        };
    }
}
