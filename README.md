# FastCache

7x-10x faster alternative to MemoryCache. A high-performance, lighweight (8KB dll) and [thread-safe](Atomic.md) memory cache for .NET.

[![NuGet version](https://badge.fury.io/nu/Jitbit.FastCache.svg)](https://badge.fury.io/nu/Jitbit.FastCache)
[![.NET](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml)

## TL;DR

Basically it's just a `ConcurrentDictionary` with expiration.

## Benchmarks

Windows:

|               Method |      Mean |     Error |   StdDev |   Gen0 | Allocated |
|--------------------- |----------:|----------:|---------:|-------:|----------:|
|     DictionaryLookup |  65.38 ns |  1.594 ns | 0.087 ns |      - |         - |
|    FastCacheLookup   |  67.15 ns |  2.582 ns | 0.142 ns |      - |         - |
|    MemoryCacheLookup | 426.60 ns | 60.162 ns | 3.298 ns | 0.0200 |     128 B |
|    FastCacheGetOrAdd |  80.44 ns |  1.170 ns | 0.064 ns |      - |         - |
|  MemoryCacheGetOrAdd | 826.85 ns | 36.609 ns | 2.007 ns | 0.1879 |    1184 B |
|   FastCacheAddRemove |  99.97 ns | 12.040 ns | 0.660 ns | 0.0063 |      80 B |
| MemoryCacheAddRemove | 710.70 ns | 32.415 ns | 1.777 ns | 0.0515 |     328 B |

Linux (Ubuntu, Docker):

|               Method |        Mean |      Error |    StdDev |   Gen0 | Allocated |
|--------------------- |------------:|-----------:|----------:|-------:|----------:|
|      FastCacheLookup |    94.97 ns |   3.250 ns |  0.178 ns |      - |         - |
|    MemoryCacheLookup | 1,051.69 ns |  64.904 ns |  3.558 ns | 0.0191 |     128 B |
|   FastCacheAddRemove |   148.32 ns |  25.766 ns |  1.412 ns | 0.0076 |      80 B |
| MemoryCacheAddRemove | 1,120.75 ns | 767.666 ns | 42.078 ns | 0.0515 |     328 B |

## How is FastCache better

Compared to `System.Runtime.Caching.MemoryCache` and `Microsoft.Extensions.Caching.MemoryCache` FastCache is

* 7X faster reads (11X under Linux!)
* 10x faster writes
* Thread safe and [atomic](https://www.jitbit.com/alexblog/fast-memory-cache/#perf)
* Generic (strongly typed keys and values) to avoid boxing/unboxing primitive types
* MemoryCache uses string keys only, so it allocates strings for keying
* MemoryCache comes with performance counters that can't be turned off
* MemoryCache uses heuristic and black magic to evict keys under memory pressure
* MemoryCache uses more memory, can crash during a key scan

## Usage

Install via nuget

```
Install-Package Jitbit.FastCache
```

Then use

```csharp
var cache = new FastCache<string, int>();

cache.AddOrUpdate(
	key: "answer",
	value: 42,
	ttl: TimeSpan.FromMinutes(1));

cache.TeyGet("answer", out int value); //value is "42"

cache.GetOrAdd(
	key: "answer",
	valueFactory: k => 42,
	ttl: TimeSpan.FromMilliseconds(100));

```

## Tradeoffs

FastCache uses `Environment.TickCount` to monitor items' TTL. `Environment.TickCount` is 104x times faster than using `DateTime.Now` and 26x times faster than `DateTime.UtcNow`.

~~But `Environment.TickCount` is limited to `Int32`. Which means it resets to `int.MinValue` once overflowed. This is not a problem, we do have a workaround for that. However this means you cannot cache stuff for more than 25 days (2.4 billion milliseconds).~~

The above is no longer valid, we have switched to .NET 6 targeting and now use `TickCount64` which is free of this problem.

Another tradeoff: MemoryCache watches memory usage, and evicts items once it senses memory pressure. **FastCache does not do any of that** it is up to you to keep your caches reasonably sized. After all, it's just a dictionary.
