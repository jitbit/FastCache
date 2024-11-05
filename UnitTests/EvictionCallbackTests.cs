using System;
using Jitbit.Utils;

namespace UnitTests;

[TestClass]
public class EvictionCallbackTests
{
    private List<string> _evictedKeys;
    private FastCache<string, string> _cache;

    [TestInitialize]
    public void Setup()
    {
        _evictedKeys = new List<string>();
        _cache = new FastCache<string, string>(
            cleanupJobInterval: 100, 
            itemEvicted: key => _evictedKeys.Add(key));
    }

    [TestMethod]
    public async Task WhenItemExpires_EvictionCallbackFires()
    {
        // Arrange
        var key = "test-key";
        _cache.AddOrUpdate(key, "value", TimeSpan.FromMilliseconds(1));

        // Act
        await Task.Delay(110); // Wait for expiration

        // Assert
        Assert.AreEqual(1, _evictedKeys.Count);
        Assert.AreEqual(key, _evictedKeys[0]);
    }

    [TestMethod]
    public async Task WhenMultipleItemsExpire_CallbackFiresForEach()
    {
        // Arrange
        var keys = new[] { "key1", "key2", "key3" };
        foreach (var key in keys)
        {
            _cache.AddOrUpdate(key, "value", TimeSpan.FromMilliseconds(1));
        }

        // Act
		await Task.Delay(5); // Wait for 1ms expiration
        _cache.EvictExpired();
		await Task.Delay(5); // Wait for callback to finish on another thread

        // Assert
        CollectionAssert.AreEquivalent(keys, _evictedKeys);
    }

    [TestMethod]
    public void WhenItemNotExpired_CallbackDoesNotFire()
    {
        // Arrange
        _cache.AddOrUpdate("key", "value", TimeSpan.FromMinutes(1));

        // Act
        _cache.EvictExpired();

        // Assert
        Assert.AreEqual(0, _evictedKeys.Count);
    }

    [TestMethod]
    public async Task AutomaticCleanup_FiresCallback()
    {
        // Arrange
        _cache.AddOrUpdate("key", "value", TimeSpan.FromMilliseconds(1));

        // Act
        await Task.Delay(110); // Wait for cleanup job

        // Assert
        Assert.AreEqual(1, _evictedKeys.Count);
    }
}