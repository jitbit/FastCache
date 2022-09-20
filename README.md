# FastCache

6x-10x faster alternative to MemoryCache

[![NuGet version](https://badge.fury.io/nu/jitbit.fastcache.svg)](https://badge.fury.io/nu/jitbit.fastcache)
[![.NET](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml)

## TL;DR

Bascially it's just a `ConcurrentDictionary` with expiration.

## Benchmarks under Windows

|               Method |      Mean |     Error |   StdDev |   Gen0 | Allocated |
|--------------------- |----------:|----------:|---------:|-------:|----------:|
|    FastCacheLookup   | 67.15 ns  |  2.582 ns | 0.142 ns |      - |         - |
|    MemoryCacheLookup | 426.60 ns | 60.162 ns | 3.298 ns | 0.0200 |     128 B |
|   FastCacheAddRemove |  99.97 ns | 12.040 ns | 0.660 ns | 0.0254 |     160 B |
| MemoryCacheAddRemove | 710.70 ns | 32.415 ns | 1.777 ns | 0.0515 |     328 B |

## Benchmarks under Linux

|               Method |        Mean |      Error |    StdDev |   Gen0 | Allocated |
|--------------------- |------------:|-----------:|----------:|-------:|----------:|
|      FastCacheLookup |    94.97 ns |   3.250 ns |  0.178 ns |      - |         - |
|    MemoryCacheLookup | 1,051.69 ns |  64.904 ns |  3.558 ns | 0.0191 |     128 B |
|   FastCacheAddRemove |   148.32 ns |  25.766 ns |  1.412 ns | 0.0253 |     160 B |
| MemoryCacheAddRemove | 1,120.75 ns | 767.666 ns | 42.078 ns | 0.0515 |     328 B |

## How is FastCache better

Compared to `System.Runtime.Caching.MemoryCache` and `Microsoft.Extensions.Caching.MemoryCache` FastCache is

* 7X faster reads than MemoryCache.
* 10x faster writes than MemoryCache
* Thread safe and atomic
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

FastCache uses `Environment.TickCount` to monitor TTL, which is 26x times faster than using `DateTime.Now`. But `Environment.TickCount` is limited to `Int32`. Which means it resets to `int.MinValue` once overflowed. This is not a problem, we do have a workaround for that. However this means you cannot cache stuff for more than ~25 days (2.4 billion milliseconds).

Another tradeoff: MemoryCache watches memory usage, and evicts items once it senses memory pressure. **FastCache does not do that** it is up to you to keep your caches reasonably sized. After all, it's just a dictionary.
