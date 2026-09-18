using BikeFitness.Shared.Services;

namespace BikeFitnessApp.UnitTests
{
    public class MockPowerManagementService : IPowerManagementService
    {
        public int PreventSleepCalls { get; private set; }
        public int AllowSleepCalls { get; private set; }

        public void PreventSleep() => PreventSleepCalls++;
        public void AllowSleep() => AllowSleepCalls++;
    }
}
