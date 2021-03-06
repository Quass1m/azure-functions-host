﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Management;
using Microsoft.Extensions.Logging;
using Microsoft.WebJobs.Script.Tests;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class FunctionsSyncServiceTests
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly FunctionsSyncService _syncService;
        private readonly Mock<IScriptHostManager> _mockScriptHostManager;
        private readonly Mock<IPrimaryHostStateProvider> _mockPrimaryHostStateProviderMock;
        private readonly Mock<IFunctionsSyncManager> _mockSyncManager;
        private readonly int _testDueTime = 250;

        public FunctionsSyncServiceTests()
        {
            _loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            _mockScriptHostManager = new Mock<IScriptHostManager>(MockBehavior.Strict);
            _mockPrimaryHostStateProviderMock = new Mock<IPrimaryHostStateProvider>(MockBehavior.Strict);
            _mockSyncManager = new Mock<IFunctionsSyncManager>(MockBehavior.Strict);

            _mockPrimaryHostStateProviderMock.Setup(p => p.IsPrimary).Returns(true);
            _mockScriptHostManager.Setup(p => p.State).Returns(ScriptHostState.Running);
            _mockSyncManager.Setup(p => p.TrySyncTriggersAsync(true)).ReturnsAsync(new SyncTriggersResult { Success = true });

            _syncService = new FunctionsSyncService(loggerFactory, _mockScriptHostManager.Object, _mockPrimaryHostStateProviderMock.Object, _mockSyncManager.Object);
            _syncService.DueTime = _testDueTime;
        }

        [Theory]
        [InlineData(true, ScriptHostState.Running, true)]
        [InlineData(false, ScriptHostState.Running, false)]
        [InlineData(true, ScriptHostState.Stopped, false)]
        [InlineData(true, ScriptHostState.Starting, false)]
        [InlineData(false, ScriptHostState.Stopped, false)]
        public void ShouldSyncTriggers_ReturnsExpectedResult(bool isPrimary, ScriptHostState hostState, bool expected)
        {
            _mockPrimaryHostStateProviderMock.Setup(p => p.IsPrimary).Returns(isPrimary);
            _mockScriptHostManager.Setup(p => p.State).Returns(hostState);

            Assert.Equal(expected, _syncService.ShouldSyncTriggers);
        }

        [Fact]
        public async Task StartAsync_PrimaryHost_Running_SyncsTriggers_AfterTimeout()
        {
            await _syncService.StartAsync(CancellationToken.None);
            await Task.Delay(2 * _testDueTime);

            _mockSyncManager.Verify(p => p.TrySyncTriggersAsync(true), Times.Once);

            var logMessage = _loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).Single();
            Assert.Equal("Initiating background SyncTriggers operation", logMessage);
        }

        [Fact]
        public async Task StartAsync_TokenCancelledBeforeTimeout_DoesNotSyncTriggers()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(_testDueTime / 2);
            await _syncService.StartAsync(cts.Token);
            await Task.Delay(2 * _testDueTime);

            _mockSyncManager.Verify(p => p.TrySyncTriggersAsync(true), Times.Never);
        }

        [Fact]
        public async Task StopCalledBeforeTimeout_DoesNotSyncTriggers()
        {
            await _syncService.StartAsync(CancellationToken.None);
            await _syncService.StopAsync(CancellationToken.None);
            await Task.Delay(2 * _testDueTime);

            _mockSyncManager.Verify(p => p.TrySyncTriggersAsync(true), Times.Never);
        }

        [Fact]
        public async Task UnhandledExceptions_HandledInCallback()
        {
            _mockSyncManager.Setup(p => p.TrySyncTriggersAsync(true)).ThrowsAsync(new Exception("Kaboom!"));

            await _syncService.StartAsync(CancellationToken.None);
            await Task.Delay(2 * _testDueTime);

            _mockSyncManager.Verify(p => p.TrySyncTriggersAsync(true), Times.Once);
        }
    }
}
