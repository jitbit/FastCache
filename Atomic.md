# Atomicness

When it comes ot atomicness, the buggest challenge is to check "item exists" and "item not expired" in one go.

When an item was "found but is expired" - we need to treat this as "not found" and discard it. For that we either need to use a lock
so that the the three steps "exist? expired? remove!" are performed atomically. Otherwise another tread might chip in,
and ADD a non-expired item with the same key while we're evicting it. And we'll be removing a non-expired key taht was just added

OR instead of using locks we can remove _by key AND by value_. So if another thread has just rushed in 
and added another item with the same key - that other item won't be removed.

basically, instead of doing this

```
lock {
	exists?
	expired?
	remove by key!
}
```

we do this

```
exists? (if yes returns the value)
expired?
remove by key AND value
```

If another thread has modified the value - it won't remove it.

Locks suck becasue add extra 50ns to benchmark, so it becomes 110ns instead of 70ns which sucks.
So - no locks then!
