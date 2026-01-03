using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jitbit.Utils
{
	internal static class FastCacheStatics
	{
		internal static readonly SemaphoreSlim GlobalStaticLock = new(1); //moved this static field to separate class, otherwise a static field in a generic class is not a true singleton
	}

	/// <summary>
	/// faster MemoryCache alternative. basically a concurrent dictionary with expiration
	/// </summary>
	public class FastCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IDisposable
	{
		private readonly ConcurrentDictionary<TKey, TtlValue> _dict = new ConcurrentDictionary<TKey, TtlValue>();

		private readonly Lock _lock = new();
		private readonly Timer _cleanUpTimer;
		private readonly EvictionCallback _itemEvicted;

		/// <summary>
		/// Callback (RUNS ON THREAD POOL!) when an item is evicted from the cache.
		/// </summary>
		/// <param name="key"></param>
		public delegate void EvictionCallback(TKey key);

		/// <summary>
		/// Initializes a new empty instance of <see cref="FastCache{TKey,TValue}"/>
		/// </summary>
		/// <param name="cleanupJobInterval">cleanup interval in milliseconds, default is 10000</param>
		/// <param name="itemEvicted">Optional callback (RUNS ON THREAD POOL!) when an item is evicted from the cache</param>
		public FastCache(int cleanupJobInterval = 10000, EvictionCallback itemEvicted = null)
		{
			_itemEvicted = itemEvicted;
			_cleanUpTimer = new Timer(s => { _ = EvictExpiredJob(); }, null, cleanupJobInterval, cleanupJobInterval);
		}

		private async Task EvictExpiredJob()
		{
			//if an applicaiton has many-many instances of FastCache objects, make sure the timer-based
			//cleanup jobs don't clash with each other, i.e. there are no clean-up jobs running in parallel
			//so we don't waste CPU resources, because cleanup is a busy-loop that iterates a collection and does calculations
			//so we use a lock to "throttle" the job and make it serial
			//HOWEVER, we still allow the user to execute eviction explicitly

			//use Semaphore instead of a "lock" to free up thread, otherwise - possible thread starvation

			await FastCacheStatics.GlobalStaticLock.WaitAsync()
				.ConfigureAwait(false);
			try
			{
				EvictExpired();
			}
			finally { FastCacheStatics.GlobalStaticLock.Release(); }
		}

		/// <summary>
		/// Cleans up expired items (dont' wait for the background job)
		/// There's rarely a need to execute this method, b/c getting an item checks TTL anyway.
		/// </summary>
		public void EvictExpired()
		{
			//Eviction already started by another thread? forget it, lets move on
			if (_lock.TryEnter()) //use the new System.Threading.Lock class for faster locking in .NET9+
			{
				List<TKey> evictedKeys = null; // Batch eviction callbacks
				try
				{
					//cache current tick count in a var to prevent calling it every iteration inside "IsExpired()" in a tight loop.
					//On a 10000-items cache this allows us to slice 30 microseconds: 330 vs 360 microseconds which is 10% faster
					//On a 50000-items cache it's even more: 2.057ms vs 2.817ms which is 35% faster!!
					//the bigger the cache the bigger the win
					var currTime = Environment.TickCount64;

					foreach (var p in _dict)
					{
						if (p.Value.IsExpired(currTime)) //call IsExpired with "currTime" to avoid calling Environment.TickCount64 multiple times
						{
							if (_dict.TryRemove(p) && _itemEvicted != null) // collect key for later batch processing (only if callback exists)
							{
								evictedKeys ??= new List<TKey>(); //lazy initialize the list
								evictedKeys.Add(p.Key);
							}
						}
					}
				}
				finally
				{
                    _lock.Exit();
				}

				// Trigger batched eviction callbacks outside the loop to prevent flooding the thread pool
				OnEviction(evictedKeys);
			}
		}

		/// <summary>
		/// Returns total count, including expired items too, if they were not yet cleaned by the eviction job
		/// </summary>
		public int Count => _dict.Count;

		/// <summary>
		/// Removes all items from the cache
		/// </summary>
		public void Clear() => _dict.Clear();

		/// <summary>
		/// Adds an item to cache if it does not exist, updates the existing item otherwise. Updating an item resets its TTL, essentially "sliding expiration".
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="value">The value to add</param>
		/// <param name="ttl">TTL of the item</param>
		public void AddOrUpdate(TKey key, TValue value, TimeSpan ttl)
		{
			var ttlValue = new TtlValue(value, ttl);

			_dict.AddOrUpdate(key, static (_, c) => c, static (_, _, c) => c, ttlValue);
		}

		/// <summary>
		/// Factory pattern overload. Adds an item to cache if it does not exist, updates the existing item otherwise. Updating an item resets its TTL, essentially "sliding expiration".
		/// </summary>
		/// <param name="key">The key to add or update</param>
		/// <param name="addValueFactory">The factory function used to generate the item for the key</param>
		/// <param name="updateValueFactory">The factory function used to update the item for the key</param>
		/// <param name="ttl">TTL of the item</param>
		public void AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory, TimeSpan ttl)
		{
			_dict.AddOrUpdate(key,
				addValueFactory: k => new TtlValue(addValueFactory(k), ttl),
				updateValueFactory: (k, v) => new TtlValue(updateValueFactory(k, v.Value), ttl));
		}

		/// <summary>
		/// Attempts to get a value by key
		/// </summary>
		/// <param name="key">The key to get</param>
		/// <param name="value">When method returns, contains the object with the key if found, otherwise default value of the type</param>
		/// <returns>True if value exists, otherwise false</returns>
		public bool TryGet(TKey key, out TValue value)
		{
			value = default(TValue);

			if (!_dict.TryGetValue(key, out TtlValue ttlValue))
				return false; //not found

			if (ttlValue.IsExpired()) //found but expired
			{
				var kv = new KeyValuePair<TKey, TtlValue>(key, ttlValue);

				//secret atomic removal method (only if both key and value match condition
				//https://devblogs.microsoft.com/pfxteam/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
				//so that we don't need any locks!! woohoo
				_dict.TryRemove(kv);

				/* EXPLANATION:
				 * when an item was "found but is expired" - we need to treat as "not found" and discard it.
				 * One solution is to use a lock
				 * so that the three steps "exist? expired? remove!" are performed atomically.
				 * Otherwise another tread might chip in, and ADD a non-expired item with the same key while we're evicting it.
				 * And we'll be removing a non-expired key that was just added.
				 * 
				 * BUT instead of using locks we can remove by key AND value. So if another thread has just rushed in 
				 * and added another item with the same key - that other item won't be removed.
				 * 
				 * basically, instead of doing this
				 * 
				 * lock {
				 *		exists?
				 *		expired?
				 *		remove by key!
				 * }
				 * 
				 * we do this
				 * 
				 * exists? (if yes returns the value)
				 * expired?
				 * remove by key AND value
				 * 
				 * If another thread has modified the value - it won't remove it.
				 * 
				 * Locks suck becasue add extra 50ns to benchmark, so it becomes 110ns instead of 70ns which sucks.
				 * So - no locks then!!!
				 * 
				 * */

				OnEviction(key);

				return false;
			}

			value = ttlValue.Value;
			return true;
		}

		/// <summary>
		/// Attempts to add a key/value item
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="value">The value to add</param>
		/// <param name="ttl">TTL of the item</param>
		/// <returns>True if value was added, otherwise false (already exists)</returns>
		public bool TryAdd(TKey key, TValue value, TimeSpan ttl)
		{
			if (TryGet(key, out _))
				return false;

			return _dict.TryAdd(key, new TtlValue(value, ttl));
		}

		private TValue GetOrAddCore(TKey key, Func<TValue> valueFactory, TimeSpan ttl)
		{
			bool wasAdded = false; //flag to indicate "add vs get". TODO: wrap in ref type some day to avoid captures/closures
			var ttlValue = _dict.GetOrAdd(
				key,
				(_) =>
				{
					wasAdded = true;
					return new TtlValue(valueFactory(), ttl);
				});

			//if the item is expired, update value and TTL
			//since TtlValue is a reference type we can update its properties in-place, instead of removing and re-adding to the dictionary (extra lookups)
			if (!wasAdded) //performance hack: skip expiration check if a brand item was just added
			{
				if (ttlValue.ModifyIfExpired(valueFactory, ttl))
					OnEviction(key);
			}

			return ttlValue.Value;
		}

		/// <summary>
		/// Adds a key/value pair by using the specified function if the key does not already exist, or returns the existing value if the key exists.
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="valueFactory">The factory function used to generate the item for the key</param>
		/// <param name="ttl">TTL of the item</param>
		public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan ttl)
			=> GetOrAddCore(key, () => valueFactory(key), ttl);

		/// <summary>
		/// Adds a key/value pair by using the specified function if the key does not already exist, or returns the existing value if the key exists.
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="valueFactory">The factory function used to generate the item for the key</param>
		/// <param name="ttl">TTL of the item</param>
		/// <param name="factoryArgument">Argument value to pass into valueFactory</param>
		public TValue GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TValue> valueFactory, TimeSpan ttl, TArg factoryArgument)
			=> GetOrAddCore(key, () => valueFactory(key, factoryArgument), ttl);

		/// <summary>
		/// Adds a key/value pair by using the specified function if the key does not already exist, or returns the existing value if the key exists.
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="value">The value to add</param>
		/// <param name="ttl">TTL of the item</param>
		public TValue GetOrAdd(TKey key, TValue value, TimeSpan ttl)
			=> GetOrAddCore(key, () => value, ttl);

		/// <summary>
		/// Tries to remove item with the specified key
		/// </summary>
		/// <param name="key">The key of the element to remove</param>
		public void Remove(TKey key)
		{
			_dict.TryRemove(key, out _);
		}

		/// <summary>
		/// Tries to remove item with the specified key, also returns the object removed in an "out" var
		/// </summary>
		/// <param name="key">The key of the element to remove</param>
		/// <param name="value">Contains the object removed or the default value if not found</param>
		public bool TryRemove(TKey key, out TValue value)
		{
			bool res = _dict.TryRemove(key, out var ttlValue) && !ttlValue.IsExpired();
			value = res ? ttlValue.Value : default(TValue);
			return res;
		}

		/// <inheritdoc/>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			var currTime = Environment.TickCount64; //save to a var to prevent multiple calls to Environment.TickCount64
			foreach (var kvp in _dict)
			{
				if (!kvp.Value.IsExpired(currTime))
					yield return new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		private void OnEviction(TKey key)
		{
			if (_itemEvicted == null) return;

			Task.Run(() => //run on thread pool to avoid blocking
			{
				try
				{
					_itemEvicted(key);
				}
				catch { } //to prevent any exceptions from crashing the thread
			});
		}

		// same as OnEviction(TKey) but for batching
		private void OnEviction(List<TKey> keys)
		{
			if (keys == null || keys.Count == 0) return;
			if (_itemEvicted == null) return;

			Task.Run(() => //run on thread pool to avoid blocking
			{
				try
				{
					foreach (var key in keys)
					{
						_itemEvicted(key);
					}
				}
				catch { } //to prevent any exceptions from crashing the thread
			});
		}

		private class TtlValue
		{
			public TValue Value { get; private set; }
			private long TickCountWhenToKill;

			public TtlValue(TValue value, TimeSpan ttl)
			{
				Value = value;
				TickCountWhenToKill = Environment.TickCount64 + (long)ttl.TotalMilliseconds;
			}

			public bool IsExpired() => IsExpired(Environment.TickCount64);

			//use an overload instead of optional param to avoid extra IF's
			public bool IsExpired(long currTime) => currTime > TickCountWhenToKill;

			/// <summary>
			/// Updates the value and TTL only if the item is expired
			/// </summary>
			/// <returns>True if the item expired and was updated, otherwise false</returns>
			public bool ModifyIfExpired(Func<TValue> newValueFactory, TimeSpan newTtl)
			{
				var ticks = Environment.TickCount64; //save to a var to prevent multiple calls to Environment.TickCount64
				if (IsExpired(ticks)) //if expired - update the value and TTL
				{
					TickCountWhenToKill = ticks + (long)newTtl.TotalMilliseconds; //update the expiration time first for better concurrency
					Value = newValueFactory();
					return true;
				}
				return false;
			}
		}

		//IDispisable members
		private bool _disposedValue;
		/// <inheritdoc/>
		public void Dispose() => Dispose(true);
		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
					_cleanUpTimer.Dispose();
				}

				_disposedValue = true;
			}
		}
	}
}
