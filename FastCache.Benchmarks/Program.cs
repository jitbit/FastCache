using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Jitbit.Utils;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Runtime.Caching;

BenchmarkRunner.Run<BenchMark>();

[ShortRunJob, MemoryDiagnoser]
public class BenchMark
{
	private static FastCache<string, int> _cache = new FastCache<string, int>(600_000);
	private static ConcurrentDictionary<string, int> _dict = new();
	private static HybridCache _hybrid = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>();

	private static DateTime _dtPlus10Mins = DateTime.Now.AddMinutes(10);
	private static HybridCacheEntryOptions _hybrid10Mins = new() { Expiration = TimeSpan.FromMinutes(10), LocalCacheExpiration = TimeSpan.FromMinutes(10) };
	private static HybridCacheEntryOptions _hybrid1Sec = new() { Expiration = TimeSpan.FromSeconds(1), LocalCacheExpiration = TimeSpan.FromSeconds(1) };

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		//add 10000 values
		for (int i = 0; i < 1000; i++)
		{
			_dict.TryAdd("test" + i, i);
			_cache.AddOrUpdate("test" + i, i, TimeSpan.FromMinutes(10));
			MemoryCache.Default.Add("test" + i, i, _dtPlus10Mins);
			await _hybrid.SetAsync("test" + i, i, _hybrid10Mins);
		}
	}

	/*
	[Benchmark]
	public void EvictExpired()
	{
		_cache.EvictExpired();
	}

	[Benchmark]
	public void EvictExpired2()
	{
		_cache.EvictExpiredOptimized();
	}
	*/
	
	[Benchmark]
	public void DictionaryLookup()
	{
		_dict.TryGetValue("test123", out _);
		_dict.TryGetValue("test234", out _);
		_dict.TryGetValue("test673", out _);
		_dict.TryGetValue("test987", out _);
	}

	[Benchmark]
	public void FastCacheLookup()
	{
		_cache.TryGet("test123", out _);
		_cache.TryGet("test234", out _);
		_cache.TryGet("test673", out _);
		_cache.TryGet("test987", out _);
	}

	[Benchmark]
	public void MemoryCacheLookup()
	{
		var x = MemoryCache.Default["test123"];
		x = MemoryCache.Default["test234"];
		x = MemoryCache.Default["test673"];
		x = MemoryCache.Default["test987"];
	}

	// HybridCache has no TryGet - GetOrCreateAsync with a factory that never runs is the lookup path
	[Benchmark]
	public async ValueTask HybridCacheLookup()
	{
		await _hybrid.GetOrCreateAsync("test123", static _ => ValueTask.FromResult(0), _hybrid10Mins);
		await _hybrid.GetOrCreateAsync("test234", static _ => ValueTask.FromResult(0), _hybrid10Mins);
		await _hybrid.GetOrCreateAsync("test673", static _ => ValueTask.FromResult(0), _hybrid10Mins);
		await _hybrid.GetOrCreateAsync("test987", static _ => ValueTask.FromResult(0), _hybrid10Mins);
	}

	[Benchmark]
	public void FastCacheGetOrAdd()
	{
		_cache.GetOrAdd("test123", 123, TimeSpan.FromSeconds(1));
		_cache.GetOrAdd("test234", 124, TimeSpan.FromSeconds(1));
		_cache.GetOrAdd("test673", 125, TimeSpan.FromSeconds(1));
		_cache.GetOrAdd("test987", 126, TimeSpan.FromSeconds(1));
	}

	[Benchmark]
	public void MemoryCacheGetOrAdd()
	{
		MemoryCache.Default.AddOrGetExisting("test123", 123, DateTime.UtcNow.AddSeconds(1));
		MemoryCache.Default.AddOrGetExisting("test234", 124, DateTime.UtcNow.AddSeconds(1));
		MemoryCache.Default.AddOrGetExisting("test673", 125, DateTime.UtcNow.AddSeconds(1));
		MemoryCache.Default.AddOrGetExisting("test987", 126, DateTime.UtcNow.AddSeconds(1));
	}

	[Benchmark]
	public async ValueTask HybridCacheGetOrAdd()
	{
		await _hybrid.GetOrCreateAsync("test123", static _ => ValueTask.FromResult(123), _hybrid1Sec);
		await _hybrid.GetOrCreateAsync("test234", static _ => ValueTask.FromResult(124), _hybrid1Sec);
		await _hybrid.GetOrCreateAsync("test673", static _ => ValueTask.FromResult(125), _hybrid1Sec);
		await _hybrid.GetOrCreateAsync("test987", static _ => ValueTask.FromResult(126), _hybrid1Sec);
	}

	[Benchmark]
	public void FastCacheAddRemove()
	{
		_cache.AddOrUpdate("1111", 42, TimeSpan.FromMinutes(10));
		_cache.Remove("1111");
	}

	[Benchmark]
	public void MemoryCacheAddRemove()
	{
		MemoryCache.Default.Add("1111", 42, _dtPlus10Mins);
		MemoryCache.Default.Remove("1111");
	}

	[Benchmark]
	public async ValueTask HybridCacheAddRemove()
	{
		await _hybrid.SetAsync("1111", 42, _hybrid10Mins);
		await _hybrid.RemoveAsync("1111");
	}
}