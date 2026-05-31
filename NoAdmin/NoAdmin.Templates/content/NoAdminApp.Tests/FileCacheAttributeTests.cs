using System.IO;
using System.Threading;
using NoAdmin.Blazor.Attributes;
using Xunit;

public class FileCacheAttributeTests
{
	[Fact]
	public void FileCache_should_return_cached_value_when_arguments_are_same()
	{
		var target = new SampleCacheTarget();

		var first = target.GetValue(1, "abc");
		var second = target.GetValue(1, "abc");

		Assert.Equal(first, second);
		Assert.Equal(1, target.BuildCount);
	}

	[Fact]
	public void FileCache_should_rebuild_when_arguments_change()
	{
		var target = new SampleCacheTarget();

		var first = target.GetValue(1, "abc");
		var second = target.GetValue(2, "abc");

		Assert.NotEqual(first, second);
		Assert.Equal(2, target.BuildCount);
	}
}

public sealed class SampleCacheTarget
{
	private int _buildCount;

	public int BuildCount => _buildCount;

	public SampleCacheTarget()
	{
		var cacheDirectory = Path.Combine(AppContext.BaseDirectory, "Cache", "FileCache.Tests");
		if (Directory.Exists(cacheDirectory))
		{
			Directory.Delete(cacheDirectory, recursive: true);
		}
	}

	[FileCache(60, CacheDirectory = "Cache/FileCache.Tests")]
	public string GetValue(int page, string keyword)
	{
		Interlocked.Increment(ref _buildCount);
		return $"{page}:{keyword}:{_buildCount}";
	}
}
