# FastCache

6x-10x faster alternative to MemoryCache

##TL;DR

Bascially it's just a `ConcurrentDictionary` with expiration.

## Why FastCache is better than System.Runtime.Caching.MemoryCache and Microsoft.Extensions.Caching.MemoryCache.

* 6X faster read times than MemeoryCache.
* 10x faster wites than MemoryCache
* Thread safe and atomic writes
* MemoryCache's come with performnce counters that can't be turned off

## Tradeoffs

This lib uses `Environment.TickCount` which is 26x times faster tham `DateTime.Now`. But `TickCount` is limited to `Int32` overflow. That is why you cannot cache stuff for more than ~25 days (2.4 billion milliseconds).
