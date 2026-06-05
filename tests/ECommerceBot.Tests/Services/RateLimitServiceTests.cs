using ECommerceBot.API.Infrastructure.RateLimit;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class RateLimitServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly RateLimitService _sut;

    public RateLimitServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new RateLimitService(_cache);
    }

    [Fact]
    public void IsRateLimited_FirstFiveMessages_NotLimited()
    {
        for (var i = 0; i < 5; i++)
            Assert.False(_sut.IsRateLimited(12345L));
    }

    [Fact]
    public void IsRateLimited_SixthMessage_IsLimited()
    {
        for (var i = 0; i < 5; i++)
            _sut.IsRateLimited(99999L);

        var result = _sut.IsRateLimited(99999L);

        Assert.True(result);
    }

    [Fact]
    public void IsRateLimited_DifferentUsers_IndependentCounters()
    {
        for (var i = 0; i < 5; i++)
            _sut.IsRateLimited(111L);

        // A different user should still be allowed
        Assert.False(_sut.IsRateLimited(222L));
    }

    [Fact]
    public void IsAdminRateLimited_First20Actions_NotLimited()
    {
        for (var i = 0; i < 20; i++)
            Assert.False(_sut.IsAdminRateLimited(555L));
    }

    [Fact]
    public void IsAdminRateLimited_21stAction_IsLimited()
    {
        for (var i = 0; i < 20; i++)
            _sut.IsAdminRateLimited(777L);

        var result = _sut.IsAdminRateLimited(777L);

        Assert.True(result);
    }

    [Fact]
    public void IsRateLimited_UserAndAdminCounters_AreIndependent()
    {
        // Exhaust user rate limit
        for (var i = 0; i < 6; i++)
            _sut.IsRateLimited(888L);

        // Admin rate limit should be fresh
        Assert.False(_sut.IsAdminRateLimited(888L));
    }

    public void Dispose() => _cache.Dispose();
}
