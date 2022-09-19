using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Jitbit.Utils
{
	/// <summary>
	/// faster MemoryCache alternative. basically a concurrent dictionary with expiration
	/// </summary>
	public class FastCache<TKey, TValue>
	{
		private readonly ConcurrentDictionary<TKey, TtlValue> _dict = new ConcurrentDictionary<TKey, TtlValue>();

		private Timer _cleanUpTimer;

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
						_dict.TryRemove(p);
				}
			}
		}

		public int Count => _dict.Count;

		public void AddOrUpdate(TKey key, TValue value, TimeSpan ttl)
		{
			var ttlValue = new TtlValue(value, ttl);

			_dict.AddOrUpdate(key, ttlValue, (k, v) => ttlValue);
		}

		public bool TryGet(TKey key, out TValue value)
		{
			value = default(TValue);

			if (!_dict.TryGetValue(key, out TtlValue ttlValue))
				return false; //not found

			if (ttlValue.IsExpired()) //found but expired
			{
				_dict.TryRemove(key, out _);
				return false;
			}

			value = ttlValue.Value;
			return true;
		}

		public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan ttl)
		{
			if (TryGet(key, out var value))
				return value;

			return _dict.GetOrAdd(key, k => new TtlValue(valueFactory(key), ttl)).Value;
		}

		public void Remove(TKey key)
		{
			_dict.TryRemove(key, out _);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			foreach (var kvp in _dict)
			{
				yield return new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value);
			}
		}

		public class TtlValue
		{
			public readonly TValue Value;
			public readonly int TickCountWhenToKill;

			public TtlValue(TValue value, TimeSpan ttl)
			{
				Value = value;
				TickCountWhenToKill = GetTickCount() + (int)ttl.TotalMilliseconds;
			}

			public bool IsExpired()
			{
				//Environment.TickCount is int32. When it reaches 2,4 billion it cycles back to -2.4 billion
				//We can't just compare "Environment.TickCount > item.TickCountWhenToKill"
				//TickCount can be super low, close to int.MinValue, but TickCountWhenToKill will be close to MaxValue
				//and the "Environment.TickCount > item.TickCountWhenToKill" will be false, so the item won't be expired.
				//Or vice versa Item.TickCountWhenToKill can also cycle back to MinValue, and Environment.TickCount > item.TickCountWhenToKill will be true, so item will expire when no needed

				//so we use "Environment.TickCount - item.TickCountWhenToKill" it is positive when "normal" values are used
				//and will be negatve if the difference is too big
				var difference = GetTickCount() - TickCountWhenToKill;
				return difference > 0;
			}
		}

		public static int TickCountShiftForUnitTests = 0;

		private static int GetTickCount()
		{
			return TickCountShiftForUnitTests + Environment.TickCount;
		}
	}
}
