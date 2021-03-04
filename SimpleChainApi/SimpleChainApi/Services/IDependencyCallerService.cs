using System.Threading.Tasks;

namespace SimpleChainApi.Services
{
    public interface IDependencyCallerService
    {
        Task<DependencyResult> ComputeDependenciesAsync(int depth);
    }
}