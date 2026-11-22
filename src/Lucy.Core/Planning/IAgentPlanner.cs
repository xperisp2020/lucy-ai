using Lucy.Core.Tasks;

namespace Lucy.Core.Planning;

public interface IAgentPlanner
{
    Task<AgentPlan> CreatePlanAsync(
        AgentTask task,
        CancellationToken cancellationToken);
}
