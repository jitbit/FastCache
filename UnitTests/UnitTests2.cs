using Jitbit.Utils;

//some more unit tests. Thanks Claude! :))

namespace UnitTests
{
    [TestClass]
    public class UnitTests2
    {
        [TestMethod]
        public void AddOrUpdate_NewItem_AddsSuccessfully()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            
            // Act
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));
            bool exists = cache.TryGet("key1", out int value);

            // Assert
            Assert.IsTrue(exists);
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void AddOrUpdate_ExistingItem_UpdatesSuccessfully()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));

            // Act
            cache.AddOrUpdate("key1", 43, TimeSpan.FromMinutes(1));
            bool exists = cache.TryGet("key1", out int value);

            // Assert
            Assert.IsTrue(exists);
            Assert.AreEqual(43, value);
        }

        [TestMethod]
        public async Task TryGet_ExpiredItem_ReturnsFalse()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMilliseconds(100));

            // Act
            await Task.Delay(200); // Wait for expiration
            bool exists = cache.TryGet("key1", out int value);

            // Assert
            Assert.IsFalse(exists);
            Assert.AreEqual(default(int), value);
        }

        [TestMethod]
        public void TryAdd_NewItem_ReturnsTrue()
        {
            // Arrange
            var cache = new FastCache<string, int>();

            // Act
            bool added = cache.TryAdd("key1", 42, TimeSpan.FromMinutes(1));

            // Assert
            Assert.IsTrue(added);
            Assert.IsTrue(cache.TryGet("key1", out int value));
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void TryAdd_ExistingItem_ReturnsFalse()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));

            // Act
            bool added = cache.TryAdd("key1", 43, TimeSpan.FromMinutes(1));

            // Assert
            Assert.IsFalse(added);
            Assert.IsTrue(cache.TryGet("key1", out int value));
            Assert.AreEqual(42, value); // Original value should remain
        }

        [TestMethod]
        public void GetOrAdd_NewItem_AddsAndReturnsValue()
        {
            // Arrange
            var cache = new FastCache<string, int>();

            // Act
            int value = cache.GetOrAdd("key1", k => 42, TimeSpan.FromMinutes(1));

            // Assert
            Assert.AreEqual(42, value);
            Assert.IsTrue(cache.TryGet("key1", out int retrieved));
            Assert.AreEqual(42, retrieved);
        }

        [TestMethod]
        public void GetOrAdd_ExistingNonExpiredItem_ReturnsExistingValue()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));

            // Act
            int value = cache.GetOrAdd("key1", k => 43, TimeSpan.FromMinutes(1));

            // Assert
            Assert.AreEqual(42, value); // Should return existing value
        }

        [TestMethod]
        public async Task GetOrAdd_ExistingExpiredItem_ReturnsNewValue()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMilliseconds(100));
            await Task.Delay(200); // Wait for expiration

            // Act
            int value = cache.GetOrAdd("key1", k => 43, TimeSpan.FromMinutes(1));

            // Assert
            Assert.AreEqual(43, value); // Should return new value
        }

        [TestMethod]
        public void GetOrAddWithArg_NewItem_AddsAndReturnsValue()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            int multiplier = 2;

            // Act
            int value = cache.GetOrAdd("key1", (k, m) => 21 * m, TimeSpan.FromMinutes(1), multiplier);

            // Assert
            Assert.AreEqual(42, value);
        }

        [TestMethod]
        public void Remove_ExistingItem_RemovesSuccessfully()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));

            // Act
            cache.Remove("key1");

            // Assert
            Assert.IsFalse(cache.TryGet("key1", out _));
        }

        [TestMethod]
        public void TryRemove_ExistingItem_RemovesAndReturnsValue()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));

            // Act
            bool removed = cache.TryRemove("key1", out int value);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(42, value);
            Assert.IsFalse(cache.TryGet("key1", out _));
        }

        [TestMethod]
        public void Clear_RemovesAllItems()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));
            cache.AddOrUpdate("key2", 43, TimeSpan.FromMinutes(1));

            // Act
            cache.Clear();

            // Assert
            Assert.AreEqual(0, cache.Count);
            Assert.IsFalse(cache.TryGet("key1", out _));
            Assert.IsFalse(cache.TryGet("key2", out _));
        }

        [TestMethod]
        public void Enumeration_ReturnsOnlyNonExpiredItems()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMinutes(1));
            cache.AddOrUpdate("key2", 43, TimeSpan.FromMilliseconds(1));
            Thread.Sleep(50); // Wait for second item to expire

            // Act
            var items = cache.ToList();

            // Assert
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(42, items[0].Value);
            Assert.AreEqual("key1", items[0].Key);
        }

        [TestMethod]
        public async Task EvictExpired_RemovesExpiredItems()
        {
            // Arrange
            var cache = new FastCache<string, int>();
            cache.AddOrUpdate("key1", 42, TimeSpan.FromMilliseconds(100));
            cache.AddOrUpdate("key2", 43, TimeSpan.FromMinutes(1));

            // Act
            await Task.Delay(200); // Wait for first item to expire
            cache.EvictExpired();

            // Assert
            Assert.IsFalse(cache.TryGet("key1", out _));
            Assert.IsTrue(cache.TryGet("key2", out int value));
            Assert.AreEqual(43, value);
        }
    }
}