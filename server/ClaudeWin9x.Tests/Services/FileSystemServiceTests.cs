using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using ClaudeWin9xServer.Models.Responses;
using ClaudeWin9xServer.Services;
using ClaudeWin9xServer.Services.Interfaces;

namespace ClaudeWin9xServer.Tests.Services;

public class FileSystemServiceTests
{
    private readonly ConcurrentDictionary<string, FileOperation> _pendingFileOps = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<FileOpResult>> _fileOpWaiters = new();
    private readonly IApprovalService _approvalService = Substitute.For<IApprovalService>();
    private readonly ILogger<FileSystemService> _logger = Substitute.For<ILogger<FileSystemService>>();

    private FileSystemService CreateService(TimeSpan? readTimeout = null, TimeSpan? writeTimeout = null) =>
        new(_pendingFileOps, _fileOpWaiters, _approvalService, _logger, readTimeout, writeTimeout);

    [Fact]
    public void PollPendingOperation_WhenPendingOpExists_ReturnsAndDispatchesOp()
    {
        var service = CreateService();
        var op = new FileOperation
        {
            Id = "op1",
            Operation = "read",
            Path = "C:\\test.txt",
            Content = null,
            Status = "pending"
        };
        _pendingFileOps.TryAdd("op1", op);

        var result = service.PollPendingOperation();

        result.ShouldNotBeNull();
        result.Id.ShouldBe("op1");
        result.Operation.ShouldBe("read");
        result.Path.ShouldBe("C:\\test.txt");
        _pendingFileOps["op1"].Status.ShouldBe("dispatched");
    }

    [Fact]
    public void PollPendingOperation_SkipsDispatchedOps()
    {
        var service = CreateService();
        var dispatched = new FileOperation
        {
            Id = "op1",
            Operation = "read",
            Path = "C:\\a.txt",
            Content = null,
            Status = "dispatched"
        };
        var pending = new FileOperation
        {
            Id = "op2",
            Operation = "list",
            Path = "C:\\folder",
            Content = null,
            Status = "pending"
        };
        _pendingFileOps.TryAdd("op1", dispatched);
        _pendingFileOps.TryAdd("op2", pending);

        var result = service.PollPendingOperation();

        result.ShouldNotBeNull();
        result.Id.ShouldBe("op2");
    }

    [Fact]
    public void SubmitResult_AddsResultAndRemovesPendingOp()
    {
        var service = CreateService();
        var op = new FileOperation
        {
            Id = "op1",
            Operation = "read",
            Path = "C:\\test.txt",
            Content = null,
            Status = "dispatched"
        };
        _pendingFileOps.TryAdd("op1", op);

        var result = new FileOpResult
        {
            OpId = "op1",
            Error = null,
            Content = "file content",
            Entries = null
        };
        service.SubmitResult(result);

        _pendingFileOps.ShouldNotContainKey("op1");
    }

    [Fact]
    public async Task ListDirectoryAsync_WhenResultComesBack_ReturnsResult()
    {
        var service = CreateService(readTimeout: TimeSpan.FromSeconds(2));

        var listTask = service.ListDirectoryAsync("C:\\");

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();
        pending.Operation.ShouldBe("list");

        var entries = new List<FileEntry>
        {
            new() { Name = "file1.txt", Type = "file", Size = 1024 },
            new() { Name = "folder", Type = "dir", Size = 0 }
        };
        var result = new FileOpResult
        {
            OpId = pending.Id,
            Error = null,
            Content = null,
            Entries = entries
        };
        service.SubmitResult(result);

        var listResult = await listTask;
        listResult.ShouldNotBeNull();
        listResult.Entries.ShouldNotBeNull();
        listResult.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ReadFileAsync_WhenResultComesBack_ReturnsContent()
    {
        var service = CreateService(readTimeout: TimeSpan.FromSeconds(2));

        var readTask = service.ReadFileAsync("C:\\test.txt");

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();

        service.SubmitResult(new FileOpResult { OpId = pending.Id, Content = "Hello, World!" });

        var readResult = await readTask;
        readResult.ShouldNotBeNull();
        readResult.Value.Content.ShouldBe("Hello, World!");
        readResult.Value.Truncated.ShouldBeFalse();
        readResult.Value.TotalSize.ShouldBe(13);
    }

    [Fact]
    public async Task ReadFileAsync_WhenContentExceedsMaxSize_TruncatesContent()
    {
        var service = CreateService(readTimeout: TimeSpan.FromSeconds(5));

        var readTask = service.ReadFileAsync("C:\\test.txt", maxSize: 5);

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();

        service.SubmitResult(new FileOpResult { OpId = pending.Id, Content = "Hello, World!" });

        var readResult = await readTask;
        readResult.ShouldNotBeNull();
        readResult.Value.Content.ShouldBe("Hello");
        readResult.Value.Truncated.ShouldBeTrue();
        readResult.Value.TotalSize.ShouldBe(13);
    }

    [Fact]
    public async Task WriteFileAsync_WhenSuccess_ReturnsTrue()
    {
        var service = CreateService(writeTimeout: TimeSpan.FromSeconds(2));

        var writeTask = service.WriteFileAsync("C:\\test.txt", "new content");

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();
        pending.Operation.ShouldBe("write");
        pending.Content.ShouldBe("new content");

        service.SubmitResult(new FileOpResult { OpId = pending.Id });

        var writeResult = await writeTask;
        writeResult.ShouldBeTrue();
    }

    [Fact]
    public async Task WriteFileAsync_WhenError_ReturnsFalse()
    {
        var service = CreateService(writeTimeout: TimeSpan.FromSeconds(2));

        var writeTask = service.WriteFileAsync("C:\\readonly.txt", "content");

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();

        service.SubmitResult(new FileOpResult { OpId = pending.Id, Error = "Access denied" });

        var writeResult = await writeTask;
        writeResult.ShouldBeFalse();
    }

    [Fact]
    public async Task WriteFileAsync_WhenApproved_QueuesOperationAndReturnsTrue()
    {
        var sessionId = "session1";

        _approvalService.RequestApprovalAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var service = CreateService(writeTimeout: TimeSpan.FromSeconds(2));
        var writeTask = service.WriteFileAsync("C:\\test.txt", "new content", sessionId);

        var pendingOp = await WaitForPendingOperationAsync(service);
        pendingOp.ShouldNotBeNull();
        pendingOp!.Operation.ShouldBe("write");
        pendingOp.Content.ShouldBe("new content");

        service.SubmitResult(new FileOpResult
        {
            OpId = pendingOp.Id,
            Error = null,
            Content = null,
            Entries = null
        });

        var writeResult = await writeTask;
        writeResult.ShouldBeTrue();

        await _approvalService.Received(1).RequestApprovalAsync(
            sessionId,
            "Write",
            Arg.Is<string>(s => s.Contains("C:\\test.txt")),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteFileAsync_WhenApprovalRejected_ReturnsFalseAndDoesNotQueueOp()
    {
        var sessionId = "session1";

        // Configure mock to reject
        _approvalService.RequestApprovalAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var service = CreateService(writeTimeout: TimeSpan.FromSeconds(2));
        var writeResult = await service.WriteFileAsync("C:\\test.txt", "new content", sessionId);

        writeResult.ShouldBeFalse();
        _pendingFileOps.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadFileAsync_WhenError_ReturnsNull()
    {
        var service = CreateService(readTimeout: TimeSpan.FromSeconds(5));

        var readTask = service.ReadFileAsync("C:\\nonexistent.txt");

        var pending = await WaitForPendingOperationAsync(service);
        pending.ShouldNotBeNull();

        service.SubmitResult(new FileOpResult { OpId = pending.Id, Error = "File not found" });

        var readResult = await readTask;
        readResult.ShouldBeNull();
    }

    [Fact]
    public async Task ListDirectoryAsync_WhenTimeout_ReturnsNull()
    {
        var service = CreateService(readTimeout: TimeSpan.FromMilliseconds(100));

        var result = await service.ListDirectoryAsync("C:\\");

        result.ShouldBeNull();
    }


    private static async Task<FileOperation?> WaitForPendingOperationAsync(FileSystemService service, int attempts = 50, int delayMs = 10)
    {
        for (var i = 0; i < attempts; i++)
        {
            var pending = service.PollPendingOperation();
            if (pending != null)
            {
                return pending;
            }
            await Task.Delay(delayMs);
        }

        return null;
    }

    [Fact]
    public void CreateBundle_WhenValidPath_ReturnsZipInfo()
    {
        var service = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");

        try
        {
            var result = service.CreateBundle(tempDir, "test.zip", Path.GetTempPath());

            result.ShouldNotBeNull();
            result.Value.ZipPath.ShouldEndWith("test.zip");
            result.Value.Size.ShouldBeGreaterThan(0);
            File.Exists(result.Value.ZipPath).ShouldBeTrue();

            File.Delete(result.Value.ZipPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateBundle_WhenPathEscapesBase_ReturnsNull()
    {
        var service = CreateService();
        var baseDir = Path.Combine(Path.GetTempPath(), $"base_{Guid.NewGuid()}");
        var outsideDir = Path.Combine(Path.GetTempPath(), $"outside_{Guid.NewGuid()}");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(outsideDir);

        try
        {
            var result = service.CreateBundle(outsideDir, "escape.zip", baseDir);

            result.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(baseDir, true);
            Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public void CreateBundle_WhenDirectoryDoesNotExist_ReturnsNull()
    {
        var service = CreateService();
        var nonExistent = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}");

        var result = service.CreateBundle(nonExistent, "test.zip", Path.GetTempPath());

        result.ShouldBeNull();
    }

    [Fact]
    public void CreateBundle_SanitizesOutputName()
    {
        var service = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");

        try
        {
            var result = service.CreateBundle(tempDir, "../../../evil.zip", Path.GetTempPath());

            result.ShouldNotBeNull();
            Path.GetFileName(result.Value.ZipPath).ShouldBe("evil.zip");
            result.Value.ZipPath.ShouldStartWith(Path.GetTempPath());

            File.Delete(result.Value.ZipPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CreateBundle_WhenNullOutputName_DefaultsToBundleZip()
    {
        var service = CreateService();
        var tempDir = Path.Combine(Path.GetTempPath(), $"bundle_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "hello");

        try
        {
            var result = service.CreateBundle(tempDir, null, Path.GetTempPath());

            result.ShouldNotBeNull();
            Path.GetFileName(result.Value.ZipPath).ShouldBe("bundle.zip");

            File.Delete(result.Value.ZipPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SubmitResult_WhenNullOpId_DoesNotThrow()
    {
        var service = CreateService();

        Should.NotThrow(() => service.SubmitResult(new FileOpResult { OpId = null }));
        Should.NotThrow(() => service.SubmitResult(new FileOpResult { OpId = "" }));
    }

    [Fact]
    public void PollPendingOperation_WhenNoPendingOps_ReturnsNull()
    {
        var service = CreateService();

        var result = service.PollPendingOperation();

        result.ShouldBeNull();
    }
}
