# FastCache

6x-10x faster alternative to MemoryCache

## TL;DR

Bascially it's just a `ConcurrentDictionary` with expiration.

## Why FastCache is better than System.Runtime.Caching.MemoryCache and Microsoft.Extensions.Caching.MemoryCache.

* 6X faster read times than MemeoryCache.
* 10x faster wites than MemoryCache
* Thread safe and atomic writes
* MemoryCache's come with performnce counters that can't be turned off

## Tradeoffs

FastCache uses `Environment.TickCount` which is 26x times faster tham `DateTime.Now`. But `Environment.TickCount` is limited to `Int32`. Which means it reset to `int.MinValue` once overflowed. In practice this means you cannot cache stuff for more than ~25 days (2.4 billion milliseconds).

Another tradeoff: MemoryCache watches the used memory, and evicts items once it senses memory pressure. **FastCache does not do that** it is up to you. It's just a dictionary.

## Benchmarks

```
|               Method |      Mean |     Error |   StdDev |   Gen0 | Allocated |
|--------------------- |----------:|----------:|---------:|-------:|----------:|
|      FastCacheLookup |  71.99 ns | 26.748 ns | 1.466 ns |      - |         - |
|    MemoryCacheLookup | 382.59 ns | 35.983 ns | 1.972 ns | 0.0200 |     128 B |
|   FastCacheAddRemove |  93.03 ns |  8.605 ns | 0.472 ns | 0.0254 |     160 B |
| MemoryCacheAddRemove | 505.47 ns | 76.733 ns | 4.206 ns | 0.0515 |     328 B |
```