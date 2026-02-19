using System.Threading.Tasks;
using BikeFitnessApp.Services;

namespace BikeFitnessApp.UnitTests
{
    public class MockStravaService : IStravaService
    {
        public bool IsAuthorized { get; set; } = false;

        public Task<bool> AuthorizeAsync()
        {
            IsAuthorized = true;
            return Task.FromResult(true);
        }

        public Task<bool> UploadActivityAsync(string filePath, string activityName)
        {
            return Task.FromResult(true);
        }

        public void Deauthorize()
        {
            IsAuthorized = false;
        }
    }
}
