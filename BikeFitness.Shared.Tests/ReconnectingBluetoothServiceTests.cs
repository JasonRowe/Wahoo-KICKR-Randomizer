using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared.Services;

namespace BikeFitnessApp.UnitTests
{
    /// <summary>
    /// Covers the reconnect-with-backoff wrapper: a dropped trainer link is retried before
    /// the ride is told it is gone, and the loss is only reported once the retries run out.
    /// </summary>
    [TestClass]
    public class ReconnectingBluetoothServiceTests
    {
        private static readonly TimeSpan NoWait = TimeSpan.Zero;
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        private MockBluetoothService _inner = null!;
        private List<TimeSpan> _delays = null!;

        [TestInitialize]
        public void Setup()
        {
            _inner = new MockBluetoothService { ConnectDelayMs = 0 };
            _delays = new List<TimeSpan>();
        }

        private ReconnectingBluetoothService CreateWrapper(int maxAttempts = 3, TimeSpan? initialBackoff = null)
        {
            return new ReconnectingBluetoothService(
                _inner,
                maxAttempts,
                initialBackoff ?? TimeSpan.FromMilliseconds(10),
                delay =>
                {
                    lock (_delays) _delays.Add(delay);
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        public async Task ConnectionLost_MidRide_ReconnectsWithoutReportingTheRideAsLost()
        {
            var service = CreateWrapper();
            var reconnected = new TaskCompletionSource<bool>();
            var lost = new TaskCompletionSource<bool>();
            service.Reconnected += () => reconnected.TrySetResult(true);
            service.ConnectionLost += () => lost.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            _inner.FireConnectionLost();

            await reconnected.Task.WaitAsync(TestTimeout);

            Assert.IsFalse(lost.Task.IsCompleted, "A recovered drop must not tear the ride down");
            Assert.AreEqual(2, _inner.ConnectCalls, "The wrapper should have reconnected once");
            Assert.IsFalse(service.IsReconnecting, "Retrying should be finished once the link is back");
            Assert.AreEqual("Trainer reconnected", service.CurrentStatus);
        }

        [TestMethod]
        public async Task ConnectedLink_IsReinitialised_SoTheRideCanResumeSendingResistance()
        {
            var service = CreateWrapper();
            var reconnected = new TaskCompletionSource<bool>();
            service.Reconnected += () => reconnected.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            _inner.FireConnectionLost();
            await reconnected.Task.WaitAsync(TestTimeout);

            Assert.AreEqual(1, _inner.SendInitCommandCalls, "A fresh link needs the init command again");
        }

        [TestMethod]
        public async Task Backoff_DoublesEachAttempt()
        {
            var service = CreateWrapper(maxAttempts: 3, initialBackoff: TimeSpan.FromMilliseconds(250));
            var reconnected = new TaskCompletionSource<bool>();
            service.Reconnected += () => reconnected.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            _inner.ConnectFailuresRemaining = 2;
            _inner.FireConnectionLost();
            await reconnected.Task.WaitAsync(TestTimeout);

            CollectionAssert.AreEqual(
                new[] { 250, 500, 1000 },
                _delays.Select(d => (int)d.TotalMilliseconds).ToArray(),
                "Retries should wait 250ms, 500ms, then 1000ms");
        }

        [TestMethod]
        public async Task ExhaustedRetries_ReportsConnectionLost()
        {
            _inner.ConnectFailuresRemaining = 99;
            var service = CreateWrapper(maxAttempts: 3);
            var attempts = new List<int>();
            var lost = new TaskCompletionSource<bool>();
            service.ReconnectAttempting += attempts.Add;
            service.ConnectionLost += () => lost.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            _inner.FireConnectionLost();

            await lost.Task.WaitAsync(TestTimeout);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, attempts, "Every attempt should be announced");
            Assert.AreEqual(4, _inner.ConnectCalls, "One initial connect plus one per attempt");
            Assert.IsFalse(service.IsConnected);
            Assert.IsFalse(service.IsReconnecting);
        }

        [TestMethod]
        public async Task RepeatedLossEventsDuringRetry_DoNotStartASecondLoop()
        {
            var gate = new TaskCompletionSource<bool>();
            var service = new ReconnectingBluetoothService(
                _inner, 3, NoWait, _ => gate.Task);
            var lost = new TaskCompletionSource<bool>();
            service.ConnectionLost += () => lost.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            _inner.FireConnectionLost();
            _inner.FireConnectionLost();
            _inner.FireConnectionLost();

            gate.SetResult(true);

            var deadline = DateTime.UtcNow + TestTimeout;
            while (service.IsReconnecting && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.IsFalse(lost.Task.IsCompleted);
            Assert.AreEqual(2, _inner.ConnectCalls, "One reconnect, not one per duplicate loss event");
        }

        [TestMethod]
        public async Task ExplicitDisconnect_CancelsRetries_AndIsNotTreatedAsALoss()
        {
            var gate = new TaskCompletionSource<bool>();
            var service = new ReconnectingBluetoothService(
                _inner, 3, NoWait, _ => gate.Task);
            var attempts = 0;
            var lost = new TaskCompletionSource<bool>();
            service.ReconnectAttempting += _ => attempts++;
            service.ConnectionLost += () => lost.TrySetResult(true);

            await service.ConnectAsync(0x1234);
            await service.DisconnectAsync();
            gate.SetResult(true);

            await Task.Delay(100);

            Assert.AreEqual(1, _inner.ConnectCalls, "A user disconnect must not be undone by a retry");
            Assert.AreEqual(0, attempts);
            Assert.IsFalse(lost.Task.IsCompleted, "A requested disconnect is not a lost connection");
        }

        [TestMethod]
        public async Task ConnectionLostBeforeAnyConnect_IsPassedStraightThrough()
        {
            var service = CreateWrapper();
            var lost = new TaskCompletionSource<bool>();
            service.ConnectionLost += () => lost.TrySetResult(true);

            _inner.FireConnectionLost();

            await lost.Task.WaitAsync(TestTimeout);
            Assert.AreEqual(0, _inner.ConnectCalls, "Nothing to retry when we never connected");
        }

        [TestMethod]
        public async Task RetryInProgress_ReportsAttemptStatus()
        {
            var gate = new TaskCompletionSource<bool>();
            var service = new ReconnectingBluetoothService(
                _inner, 3, NoWait, _ => gate.Task);
            var statuses = new List<string>();
            service.StatusChanged += statuses.Add;

            await service.ConnectAsync(0x1234);
            _inner.FireConnectionLost();

            var deadline = DateTime.UtcNow + TestTimeout;
            while (!service.IsReconnecting && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.IsTrue(service.IsReconnecting);
            Assert.IsTrue(statuses.Any(s => s.Contains("reconnecting (attempt 1/3)")),
                $"Expected an attempt status, got: {string.Join(" | ", statuses)}");

            gate.SetResult(true);
        }
    }
}
