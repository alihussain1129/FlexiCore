using System.Diagnostics;
using System.Globalization;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

/// <summary>Lock behavior between genuinely separate operating-system processes.</summary>
public sealed class LedgerLockCrossProcessTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(60);

    private sealed class LockChild : IDisposable
    {
        private readonly Process _process;

        private LockChild(Process process, string firstLine)
        {
            _process = process;
            FirstLine = firstLine;
        }

        public string FirstLine { get; }

        public static async Task<LockChild> StartAsync(string ledgerPath, int timeoutMs)
        {
            var helper = Path.Combine(AppContext.BaseDirectory, "FlexiCore.LedgerLockHelper.dll");
            Assert.True(File.Exists(helper), "Helper not found next to the tests: " + helper);

            var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            info.ArgumentList.Add(helper);
            info.ArgumentList.Add(ledgerPath);
            info.ArgumentList.Add(timeoutMs.ToString(CultureInfo.InvariantCulture));

            var process = Process.Start(info)
                ?? throw new InvalidOperationException("Could not start the helper process.");
            try
            {
                var first = await process.StandardOutput.ReadLineAsync().WaitAsync(Wait);
                if (first is null)
                    throw new InvalidOperationException(
                        "The helper exited without output: " + await process.StandardError.ReadToEndAsync());
                return new LockChild(process, first);
            }
            catch
            {
                Kill(process);
                process.Dispose();
                throw;
            }
        }

        public async Task ReleaseAsync()
        {
            await _process.StandardInput.WriteLineAsync("release");
            await _process.StandardInput.FlushAsync();
            Assert.Equal("RELEASED", await _process.StandardOutput.ReadLineAsync().WaitAsync(Wait));
            await _process.WaitForExitAsync().WaitAsync(Wait);
        }

        public async Task KillAsync()
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(Wait);
        }

        public async Task<int> ExitCodeAsync()
        {
            await _process.WaitForExitAsync().WaitAsync(Wait);
            return _process.ExitCode;
        }

        public void Dispose()
        {
            Kill(_process);
            _process.Dispose();
        }

        private static void Kill(Process process)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process had already exited and been reaped: nothing left to stop.
            }
        }
    }

    [Fact]
    public async Task A_second_process_cannot_take_the_lock_while_the_first_holds_it_and_can_after_release()
    {
        using var o = new Owner();
        var ledger = o.Paths.LedgerPath;
        using var child = await LockChild.StartAsync(ledger, 10_000);
        Assert.Equal("LOCKED", child.FirstLine);

        var ex = Assert.Throws<LicenseToolException>(
            () => LedgerFileLock.Acquire(ledger, TimeSpan.FromMilliseconds(300)));
        Assert.Contains("Timed out waiting for the issue ledger lock", ex.Message);

        await child.ReleaseAsync();

        using var after = LedgerFileLock.Acquire(ledger, TimeSpan.FromSeconds(5));
        after.EnsureFor(ledger);
    }

    [Fact]
    public async Task A_waiting_process_gets_the_lock_as_soon_as_the_holder_releases()
    {
        using var o = new Owner();
        var ledger = o.Paths.LedgerPath;
        using var child = await LockChild.StartAsync(ledger, 10_000);

        var waiter = Task.Run(() => LedgerFileLock.Acquire(ledger, TimeSpan.FromSeconds(30)));
        await Task.Delay(500);
        Assert.False(waiter.IsCompleted);

        await child.ReleaseAsync();

        using var acquired = await waiter.WaitAsync(TimeSpan.FromSeconds(20));
        acquired.EnsureFor(ledger);
    }

    [Fact]
    public async Task The_lock_is_released_by_the_operating_system_when_the_holder_is_killed()
    {
        using var o = new Owner();
        var ledger = o.Paths.LedgerPath;
        using var child = await LockChild.StartAsync(ledger, 10_000);
        Assert.Equal("LOCKED", child.FirstLine);

        await child.KillAsync();

        // The lock file is left behind on purpose; its existence alone never means "locked".
        Assert.True(File.Exists(ledger + ".lock"));
        using var after = LedgerFileLock.Acquire(ledger, TimeSpan.FromSeconds(5));
        after.EnsureFor(ledger);
    }

    [Fact]
    public async Task A_child_process_times_out_with_a_clear_message_while_this_process_holds_the_lock()
    {
        using var o = new Owner();
        var ledger = o.Paths.LedgerPath;
        using var parent = LedgerFileLock.Acquire(ledger);

        using var child = await LockChild.StartAsync(ledger, 300);

        Assert.StartsWith("FAILED:", child.FirstLine, StringComparison.Ordinal);
        Assert.Contains("Timed out waiting for the issue ledger lock", child.FirstLine);
        Assert.Equal(2, await child.ExitCodeAsync());
    }

    [Fact]
    public async Task A_child_process_waits_for_this_process_and_then_acquires_the_lock()
    {
        using var o = new Owner();
        var ledger = o.Paths.LedgerPath;
        var parent = LedgerFileLock.Acquire(ledger);
        Task<LockChild>? starting = null;
        try
        {
            starting = LockChild.StartAsync(ledger, 30_000);
            await Task.Delay(1500);
            Assert.False(starting.IsCompleted);
        }
        finally
        {
            parent.Dispose();
        }

        using var child = await starting!;
        Assert.Equal("LOCKED", child.FirstLine);
    }
}
