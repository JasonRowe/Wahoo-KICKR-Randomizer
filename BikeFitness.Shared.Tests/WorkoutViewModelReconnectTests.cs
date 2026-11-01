using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitness.Shared.Services;
using BikeFitness.Shared.ViewModels;

namespace BikeFitnessApp.UnitTests
{
    /// <summary>
    /// The ride screen's half of reconnect-with-backoff: a retry in progress keeps the
    /// workout alive and says so, and a recovered link drops the rider back into the ride.
    /// </summary>
    [TestClass]
    public class WorkoutViewModelReconnectTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        private MockBluetoothService _inner = null!;
        private MockStravaService _strava = null!;
        private MockUserInterfaceService _ui = null!;
        private MockPowerManagementService _power = null!;
        private TaskCompletionSource<bool> _gate = null!;
        private ReconnectingBluetoothService _service = null!;
        private WorkoutViewModel _viewModel = null!;

        [TestInitialize]
        public void Setup()
        {
            _inner = new MockBluetoothService { ConnectDelayMs = 0 };
            _strava = new MockStravaService();
            _ui = new MockUserInterfaceService();
            _power = new MockPowerManagementService();
            _gate = new TaskCompletionSource<bool>();
            _service = new ReconnectingBluetoothService(
                _inner, 3, TimeSpan.FromMilliseconds(10), _ => _gate.Task);
            _viewModel = new WorkoutViewModel(_service, _strava, _ui, _power);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gate.TrySetResult(true);
            _viewModel.Dispose();
        }

        [TestMethod]
        public async Task Dropout_WhileRiding_ShowsReconnectingAndKeepsTheSessionRunning()
        {
            await _service.ConnectAsync(0x1234);
            _viewModel.StartCommand.Execute(null);

            _inner.FireConnectionLost();

            var deadline = DateTime.UtcNow + TestTimeout;
            while (_viewModel.Status == "WORKOUT ACTIVE" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.AreEqual("RECONNECTING (1)", _viewModel.Status);
            Assert.IsTrue(_viewModel.IsWorkoutActive, "The ride must survive a dropout that is still being retried");
            Assert.AreEqual(0, _power.AllowSleepCalls);
        }

        [TestMethod]
        public async Task RecoveredLink_ReturnsToActiveRide()
        {
            await _service.ConnectAsync(0x1234);
            _viewModel.StartCommand.Execute(null);
            _inner.FireConnectionLost();

            var deadline = DateTime.UtcNow + TestTimeout;
            while (!_service.IsReconnecting && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            _gate.SetResult(true);

            deadline = DateTime.UtcNow + TestTimeout;
            while (_viewModel.Status != "WORKOUT ACTIVE" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.AreEqual("WORKOUT ACTIVE", _viewModel.Status);
            Assert.AreEqual("Status: Trainer reconnected.", _viewModel.Log);
            Assert.IsTrue(_viewModel.IsWorkoutActive);
        }

        [TestMethod]
        public async Task ExhaustedRetries_StillTearTheRideDownAsBefore()
        {
            _service = new ReconnectingBluetoothService(
                _inner, 2, TimeSpan.Zero, _ => Task.CompletedTask);
            _viewModel.Dispose();
            _power = new MockPowerManagementService();
            _viewModel = new WorkoutViewModel(_service, _strava, _ui, _power);
            _inner.ConnectFailuresRemaining = 99;

            await _service.ConnectAsync(0x1234);
            _viewModel.StartCommand.Execute(null);
            _inner.FireConnectionLost();

            var deadline = DateTime.UtcNow + TestTimeout;
            while (_viewModel.IsWorkoutActive && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.IsFalse(_viewModel.IsWorkoutActive);
            Assert.AreEqual("DISCONNECTED", _viewModel.Status);
            Assert.AreEqual(1, _power.AllowSleepCalls);
        }
    }
}
