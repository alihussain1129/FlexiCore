using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.UnitTests.LicensingTests;

public sealed class LicenseStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flexicore-license-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            if (File.Exists(_dir)) File.Delete(_dir);
        }
        catch (IOException) { }
    }

    private static byte[] SomeBytes(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++) bytes[i] = (byte)(i * 31 + 7);
        return bytes;
    }

    [Fact]
    public void RelativeDirectory_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new FileLicenseStore("relative-dir"));
        Assert.Throws<ArgumentException>(() => new FileLicenseStore(""));
    }

    [Fact]
    public async Task Read_WhenNothingStored_ReturnsNull()
    {
        var store = new FileLicenseStore(_dir);
        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Write_CreatesDirectory_AndRoundTripsExactBytes()
    {
        var store = new FileLicenseStore(_dir);
        var bytes = SomeBytes(300);
        await store.WriteAsync(bytes, CancellationToken.None);
        Assert.Equal(bytes, await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Replace_LeavesOnlyTheLicenseFile()
    {
        var store = new FileLicenseStore(_dir);
        await store.WriteAsync(SomeBytes(10), CancellationToken.None);
        var second = SomeBytes(20);
        await store.WriteAsync(second, CancellationToken.None);

        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray();
        Assert.Equal(FileLicenseStore.FileName, Assert.Single(files));
        Assert.Equal(second, await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OversizedStoredFile_IsReadAsOneByteOverTheLimit()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(Path.Combine(_dir, FileLicenseStore.FileName), new byte[LicenseLimits.MaxFileBytes + 500]);
        var bytes = await new FileLicenseStore(_dir).ReadAsync(CancellationToken.None);
        Assert.Equal(LicenseLimits.MaxFileBytes + 1, bytes!.Length);
    }

    [Fact]
    public async Task Write_WhenDirectoryPathIsAnExistingFile_ThrowsStorageException()
    {
        await File.WriteAllTextAsync(_dir, "not a directory");
        var store = new FileLicenseStore(_dir);
        await Assert.ThrowsAsync<LicenseStorageException>(() => store.WriteAsync(SomeBytes(5), CancellationToken.None));
    }
}