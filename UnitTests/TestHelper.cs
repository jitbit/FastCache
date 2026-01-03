[assembly: Parallelize(Workers = 3, Scope = ExecutionScope.ClassLevel)]

namespace UnitTests;

internal class TestHelper
{
	public static async Task RunConcurrently(int numThreads, Action action)
	{
		var tasks = new Task[numThreads];
		ManualResetEvent m = new ManualResetEvent(false);

		for (int i = 0; i < numThreads; i++)
		{
			tasks[i] = Task.Run(() =>
			{
				m.WaitOne(); //dont start just yet
				action();
			});
		}

		m.Set(); //off we go

		await Task.WhenAll(tasks);
	}
}
