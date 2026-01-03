using Jitbit.Utils;

namespace UnitTests;

[TestClass]
public class UnitTests
{
	[TestMethod]
	public async Task TestGetSetCleanup()
	{
		using var _cache = new FastCache<int, int>(cleanupJobInterval: 200); //add "using" to stop cleanup timer, to prevent cleanup job from clashing with other tests
		_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(100));
		Assert.IsTrue(_cache.TryGet(42, out int v));
		Assert.AreEqual(42, v);

		await Task.Delay(300);
		Assert.AreEqual(0, _cache.Count); //cleanup job has run?
	}

	[TestMethod]
	public async Task TestEviction()
	{
		var list = new List<FastCache<int, int>>();
		for (int i = 0; i < 20; i++)
		{
			var cache = new FastCache<int, int>(cleanupJobInterval: 200);
			cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(100));
			list.Add(cache);
		}
		await Task.Delay(300);

		for (int i = 0; i < 20; i++)
		{
			Assert.AreEqual(0, list[i].Count); //cleanup job has run?
		}

		//cleanup
		for (int i = 0; i < 20; i++)
		{
			list[i].Dispose();
		}
	}

	[TestMethod]
	public async Task Shortdelay()
	{
		var cache = new FastCache<int, int>();
		cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(500));

		await Task.Delay(50);

		Assert.IsTrue(cache.TryGet(42, out int result)); //not evicted
		Assert.AreEqual(42, result);
	}

	[TestMethod]
	public async Task TestWithDefaultJobInterval()
	{
		var _cache2 = new FastCache<string, int>();
		_cache2.AddOrUpdate("42", 42, TimeSpan.FromMilliseconds(100));
		Assert.IsTrue(_cache2.TryGet("42", out _));
		await Task.Delay(150);
		Assert.IsFalse(_cache2.TryGet("42", out _));
	}

	[TestMethod]
	public void TestRemove()
	{
		var cache = new FastCache<string, int>();
		cache.AddOrUpdate("42", 42, TimeSpan.FromMilliseconds(100));
		cache.Remove("42");
		Assert.IsFalse(cache.TryGet("42", out _));
	}

	[TestMethod]
	public void TestTryRemove()
	{
		var cache = new FastCache<string, int>();
		cache.AddOrUpdate("42", 42, TimeSpan.FromMilliseconds(100));
		var res = cache.TryRemove("42", out int value);
		Assert.IsTrue(res && value == 42);
		Assert.IsFalse(cache.TryGet("42", out _));

		//now try remove non-existing item
		res = cache.TryRemove("blabblah", out value);
		Assert.IsFalse(res);
		Assert.AreEqual(0, value);
	}

	[TestMethod]
	public async Task TestTryRemoveWithTtl()
	{
		var cache = new FastCache<string, int>();
		cache.AddOrUpdate("42", 42, TimeSpan.FromMilliseconds(100));
		await Task.Delay(120); //let the item expire

		var res = cache.TryRemove("42", out int value);
		Assert.IsFalse(res);
		Assert.AreEqual(0, value);
	}

	[TestMethod]
	public async Task TestTryAdd()
	{
		var cache = new FastCache<string, int>();
		Assert.IsTrue(cache.TryAdd("42", 42, TimeSpan.FromMilliseconds(100)));
		Assert.IsFalse(cache.TryAdd("42", 42, TimeSpan.FromMilliseconds(100)));

		await Task.Delay(120); //wait for it to expire

		Assert.IsTrue(cache.TryAdd("42", 42, TimeSpan.FromMilliseconds(100)));
	}

	[TestMethod]
	public async Task TestGetOrAdd()
	{
		var cache = new FastCache<string, int>();
		cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));
		Assert.AreEqual(1024, cache.GetOrAdd("key", k => 1025, TimeSpan.FromMilliseconds(100))); //old value
		Assert.IsTrue(cache.TryGet("key", out int res) && res == 1024); //another way to retrieve
		await Task.Delay(110);

		Assert.IsFalse(cache.TryGet("key", out _)); //expired

		//now try non-factory overloads
		Assert.AreEqual(123321, cache.GetOrAdd("key123", 123321, TimeSpan.FromMilliseconds(100)));
		Assert.AreEqual(123321, cache.GetOrAdd("key123", -1, TimeSpan.FromMilliseconds(100))); //still old value
		await Task.Delay(110);
		Assert.AreEqual(-1, cache.GetOrAdd("key123", -1, TimeSpan.FromMilliseconds(100))); //new value
	}


	[TestMethod]
	public async Task TestGetOrAddExpiration()
	{
		var cache = new FastCache<string, int>();
		cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));

		Assert.AreEqual(1024, cache.GetOrAdd("key", k => 1025, TimeSpan.FromMilliseconds(100))); //old value
		Assert.IsTrue(cache.TryGet("key", out int res) && res == 1024); //another way to retrieve
		
		await Task.Delay(110); //let the item expire

		Assert.AreEqual(1025, cache.GetOrAdd("key", k => 1025, TimeSpan.FromMilliseconds(100))); //new value
		Assert.IsTrue(cache.TryGet("key", out res) && res == 1025); //another way to retrieve
	}

	[TestMethod]
	public async Task TestGetOrAddWithArg()
	{
		var cache = new FastCache<string, int>();
		cache.GetOrAdd("key", (k, arg) => 1024 + arg.Length, TimeSpan.FromMilliseconds(100), "test123");
		Assert.IsTrue(cache.TryGet("key", out int res) && res == 1031);

		//eviction
		await Task.Delay(110);
		Assert.IsFalse(cache.TryGet("key", out _));

		//now try without "TryGet"
		Assert.AreEqual(24, cache.GetOrAdd("key2", (k, arg) => 21 + arg.Length, TimeSpan.FromMilliseconds(100), "123"));
		Assert.AreEqual(24, cache.GetOrAdd("key2", (k, arg) => 2211 + arg.Length, TimeSpan.FromMilliseconds(100), "123"));
		await Task.Delay(110);
		Assert.AreEqual(2214, cache.GetOrAdd("key2", (k, arg) => 2211 + arg.Length, TimeSpan.FromMilliseconds(100), "123"));
	}

	[TestMethod]
	public void TestClear()
	{
		var cache = new FastCache<string, int>();
		cache.GetOrAdd("key", k => 1024, TimeSpan.FromSeconds(100));

		cache.Clear();

		Assert.IsFalse(cache.TryGet("key", out int res));
	}

	[TestMethod]
	public async Task TestTryAddAtomicness()
	{
		int i = 0;
		
		var cache = new FastCache<int, int>();
		cache.TryAdd(42, 42, TimeSpan.FromMilliseconds(50)); //add item with short TTL

		await Task.Delay(100); //wait for tha value to expire

		await TestHelper.RunConcurrently(20, () => {
			if (cache.TryAdd(42, 42, TimeSpan.FromSeconds(1)))
				i++;
		});

		Assert.AreEqual(1, i);
	}

	//this text can occasionally fail becasue factory is not guaranteed to be called only once. only panic if it fails ALL THE TIME
	[TestMethod]
	public async Task TestGetOrAddAtomicNess()
	{
		int i = 0;

		var cache = new FastCache<int, int>();
		
		cache.GetOrAdd(42, 42, TimeSpan.FromMilliseconds(100));

		await Task.Delay(110); //wait for tha value to expire

		await TestHelper.RunConcurrently(20, () => {
			cache.GetOrAdd(42, k => { return ++i; }, TimeSpan.FromSeconds(1));
		});

		//test that only the first value was added
		cache.TryGet(42, out i);
		Assert.AreEqual(1, i);
	}

	[TestMethod]
	public async Task Enumerator()
	{
		var cache = new FastCache<string, int>(); //now with default cleanup interval
		cache.GetOrAdd("key", k => 1024, TimeSpan.FromMilliseconds(100));

		Assert.AreEqual(1024, cache.FirstOrDefault().Value);

		await Task.Delay(110);

		Assert.IsFalse(cache.Any());
	}

	[TestMethod]
	public async Task TestTtlExtended()
	{
		var _cache = new FastCache<int, int>();
		_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(300));

		await Task.Delay(50);
		Assert.IsTrue(_cache.TryGet(42, out int result)); //not evicted
		Assert.AreEqual(42, result);

		_cache.AddOrUpdate(42, 42, TimeSpan.FromMilliseconds(300));

		await Task.Delay(250);

		Assert.IsTrue(_cache.TryGet(42, out int result2)); //still not evicted
		Assert.AreEqual(42, result2);
	}
}