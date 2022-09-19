# FastCache

6x-10x faster alternative to MemoryCache

[![NuGet version](https://badge.fury.io/nu/Jitbit.FastCache.svg)](https://badge.fury.io/nu/Jitbit.FastCache)
[![.NET](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml/badge.svg)](https://github.com/jitbit/FastCache/actions/workflows/dotnet.yml)

## TL;DR

Bascially it's just a `ConcurrentDictionary` with expiration.

## Why FastCache is better than System.Runtime.Caching.MemoryCache and Microsoft.Extensions.Caching.MemoryCache.

* 6X faster read times than MemeoryCache.
* 10x faster wites than MemoryCache
* Thread safe and atomic writes
* MemoryCache's come with performnce counters that can't be turned off

## Usage

```csharp
var cache = new FastCache<string, int>();
cache.AddOrUpdate("key", 42, TimeSpan.FromMinutes(1));
cache.TeyGet("key", out int value);
cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));

```

## Tradeoffs

FastCache uses `Environment.TickCount` which is 26x times faster tham `DateTime.Now`. But `Environment.TickCount` is limited to `Int32`. Which means it reset to `int.MinValue` once overflowed. In practice this means you cannot cache stuff for more than ~25 days (2.4 billion milliseconds).

Another tradeoff: MemoryCache watches the used memory, and evicts items once it senses memory pressure. **FastCache does not do that** it is up to you. It's just a dictionary.

## Benchmarks

```
|               Method |      Mean |     Error |   StdDev |   Gen0 | Allocated |
|--------------------- |----------:|----------:|---------:|-------:|----------:|
|      FastCacheLookup |  67.15 ns |  2.582 ns | 0.142 ns |      - |         - |
|    MemoryCacheLookup | 426.60 ns | 60.162 ns | 3.298 ns | 0.0200 |     128 B |
|   FastCacheAddRemove |  99.97 ns | 12.040 ns | 0.660 ns | 0.0254 |     160 B |
| MemoryCacheAddRemove | 710.70 ns | 32.415 ns | 1.777 ns | 0.0515 |     328 B |
```
