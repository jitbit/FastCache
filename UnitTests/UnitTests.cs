using Jitbit.Utils;

[assembly: Parallelize(Workers = 6, Scope = ExecutionScope.MethodLevel)]

namespace UnitTests
{
	[TestClass]
	public class UnitTests
	{
		[TestMethod]
		public async Task TestGetSetCleanup()
		{
			var _cache = new FastCache<int, int>(cleanupJobInterval: 200);
			_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(100));
			Assert.IsTrue(_cache.TryGet(42, out int v));
			Assert.IsTrue(v == 42);

			await Task.Delay(300);
			Assert.IsTrue(_cache.Count == 0); //cleanup job has ran?
		}

		[TestMethod]
		public async Task TestWithDefaultJobInterval()
		{
			var _cache2 = new FastCache<string, int>(); //now with default cleanup interval
			_cache2.AddOrUpdate("42", 42, TimeSpan.FromMilliseconds(100));
			Assert.IsTrue(_cache2.TryGet("42", out _));
			await Task.Delay(150);
			Assert.IsFalse(_cache2.TryGet("42", out _));
		}

		[TestMethod]
		public async Task TestGetOrAdd()
		{
			var cache = new FastCache<string, int>(); //now with default cleanup interval
			cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));
			Assert.IsTrue(cache.TryGet("key", out int res) && res == 1024);
			await Task.Delay(100);

			Assert.IsFalse(cache.TryGet("key", out _));
		}

		[TestMethod]
		public async Task Enumerator()
		{
			var cache = new FastCache<string, int>(); //now with default cleanup interval
			cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));

			Assert.IsTrue(cache.FirstOrDefault().Value == 1024);

			await Task.Delay(110);

			Assert.IsFalse(cache.Any());
		}

		[TestMethod]
		public async Task WhenItemIsUpdatedTtlIsExtended()
		{
			var _cache = new FastCache<int, int>();
			_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(200));

			await Task.Delay(100);
			Assert.IsTrue(_cache.TryGet(42, out int result) && result == 42); //not evicted

			_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(200));

			await Task.Delay(150);

			Assert.IsTrue(_cache.TryGet(42, out result) && result == 42);
		}

		[TestMethod]
		public async Task TestFastCacheOverflowLogic()
		{
			//test int-based overflow logic used in FastCache

			FastCache<int, int>.TickCountShiftForUnitTests = int.MaxValue - Environment.TickCount - 200;
			var ttl = new FastCache<int, int>.TtlValue(123, TimeSpan.FromMilliseconds(300));
			await Task.Delay(400);
			Assert.IsTrue(ttl.IsExpired());

			FastCache<int, int>.TickCountShiftForUnitTests = int.MaxValue - Environment.TickCount - 200;
			ttl = new FastCache<int, int>.TtlValue(123, TimeSpan.FromMilliseconds(300));
			await Task.Delay(100);
			Assert.IsFalse(ttl.IsExpired());

			FastCache<int, int>.TickCountShiftForUnitTests = int.MaxValue - Environment.TickCount - 200;
			ttl = new FastCache<int, int>.TtlValue(123, TimeSpan.FromMilliseconds(50));
			await Task.Delay(100);
			Assert.IsTrue(ttl.IsExpired());

			FastCache<int, int>.TickCountShiftForUnitTests = int.MaxValue - Environment.TickCount - 200;
			ttl = new FastCache<int, int>.TtlValue(123, TimeSpan.FromMilliseconds(50));
			await Task.Delay(300);
			Assert.IsTrue(ttl.IsExpired());
		}
	}
}