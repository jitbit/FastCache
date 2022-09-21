using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Jitbit.Utils
{
	/// <summary>
	/// faster MemoryCache alternative. basically a concurrent dictionary with expiration
	/// </summary>
	public class FastCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
	{
		private readonly ConcurrentDictionary<TKey, TtlValue> _dict = new ConcurrentDictionary<TKey, TtlValue>();

		private readonly Timer _cleanUpTimer;

		/// <summary>
		/// create cache
		/// </summary>
		/// <param name="cleanupJobInterval">cleanup interval in milliseconds, default is 10000</param>
		public FastCache(int cleanupJobInterval = 10000)
		{
			_cleanUpTimer = new Timer(_EvictExpired, null, cleanupJobInterval, cleanupJobInterval);

			void _EvictExpired(object state)
			{
				foreach (var p in _dict)
				{
					if (p.Value.IsExpired())
						_dict.TryRemove(p.Key, out _);
				}
			}
		}

		/// <summary>
		/// Returns total count, including expired items too, if they were not yet cleaned by the eviction job
		/// </summary>
		public int Count => _dict.Count;

		/// <summary>
		/// Adds an item to cache if it does not exist, updates the existing item otherwise. Updating an item resets its TTL.
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="value">The value to add</param>
		/// <param name="ttl">TTL of the item</param>
		public void AddOrUpdate(TKey key, TValue value, TimeSpan ttl)
		{
			var ttlValue = new TtlValue(value, ttl);

			_dict.AddOrUpdate(key, ttlValue, (k, v) => ttlValue);
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
				((ICollection<KeyValuePair<TKey, TtlValue>>)_dict).Remove(kv);

				/* EXPLANATION:
				 * when an item was "found but is expired" - we need to treat as "not found" and discard it.
				 * For that we either need to use a lock
				 * so that the the three steps "exist? expired? remove!" are performed atomically.
				 * Otherwise another tread might chip in, and ADD a non-expired item with the same key while we're evicting it.
				 * And we'll be removing a non-expired key taht was just added
				 * 
				 * OR instead of using locks we can remove by key AND value. So if another thread has just rushed in 
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

		/// <summary>
		/// Adds a key/value pair by using the specified function if the key does not already exist, or returns the existing value if the key exists.
		/// </summary>
		/// <param name="key">The key to add</param>
		/// <param name="valueFactory">The factory function used to generate the item for the key</param>
		/// <param name="ttl">TTL of the item</param>
		public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan ttl)
		{
			if (TryGet(key, out var value))
				return value;

			return _dict.GetOrAdd(key, k => new TtlValue(valueFactory(key), ttl)).Value;
		}

		/// <summary>
		/// Tries to remove item with the specified key
		/// </summary>
		/// <param name="key">The key of the element to remove</param>
		public void Remove(TKey key)
		{
			_dict.TryRemove(key, out _);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			foreach (var kvp in _dict)
			{
				if (!kvp.Value.IsExpired())
					yield return new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		private class TtlValue
		{
			public readonly TValue Value;
			public readonly long TickCountWhenToKill;

			public TtlValue(TValue value, TimeSpan ttl)
			{
				Value = value;
				TickCountWhenToKill = Environment.TickCount64 + (long)ttl.TotalMilliseconds;
			}

			public bool IsExpired()
			{
				var difference = Environment.TickCount64 - TickCountWhenToKill;
				return difference > 0;
			}
		}
	}
}
