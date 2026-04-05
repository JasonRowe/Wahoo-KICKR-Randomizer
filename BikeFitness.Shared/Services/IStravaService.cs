using System.Threading.Tasks;

namespace BikeFitness.Shared.Services
{
    public interface IStravaService
    {
        bool IsAuthorized { get; }
        Task<bool> AuthorizeAsync();
        Task<bool> UploadActivityAsync(string filePath, string activityName);
        void Deauthorize();
    }
}
