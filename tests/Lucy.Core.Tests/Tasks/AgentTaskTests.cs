using Lucy.Core.Tasks;

namespace Lucy.Core.Tests.Tasks;

public sealed class AgentTaskTests
{
    [Fact]
    public void Create_TrimsGoal_AndStartsPending()
    {
        var now = DateTimeOffset.Parse("2026-11-22T12:00:00Z");

        var task = AgentTask.Create("  inspect repository  ", now);

        Assert.Equal("inspect repository", task.Goal);
        Assert.Equal(AgentTaskStatus.Pending, task.Status);
        Assert.Equal(now, task.CreatedAt);
        Assert.Equal(now, task.UpdatedAt);
    }

    [Fact]
    public void TransitionTo_AdvancesThroughValidLifecycle()
    {
        var now = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
        var task = AgentTask.Create("run a bounded task", now);

        var planning = task.TransitionTo(AgentTaskStatus.Planning, now.AddSeconds(1));
        var running = planning.TransitionTo(AgentTaskStatus.Running, now.AddSeconds(2));
        var completed = running.TransitionTo(AgentTaskStatus.Completed, now.AddSeconds(3));

        Assert.Equal(AgentTaskStatus.Pending, task.Status);
        Assert.Equal(AgentTaskStatus.Completed, completed.Status);
        Assert.Equal(task.Id, completed.Id);
    }

    [Fact]
    public void TransitionTo_RejectsInvalidTransition()
    {
        var task = AgentTask.Create("invalid transition");

        Assert.Throws<InvalidOperationException>(
            () => task.TransitionTo(AgentTaskStatus.Completed));
    }

    [Fact]
    public void TransitionTo_RequiresFailureReason()
    {
        var task = AgentTask.Create("failure path")
            .TransitionTo(AgentTaskStatus.Planning);

        Assert.Throws<ArgumentException>(
            () => task.TransitionTo(AgentTaskStatus.Failed));
    }

    [Fact]
    public void Create_RejectsEmptyGoal()
    {
        Assert.Throws<ArgumentException>(() => AgentTask.Create("  "));
    }

    [Fact]
    public void TransitionTo_RejectsBackwardsTimestamp()
    {
        var now = DateTimeOffset.Parse("2026-11-22T12:00:00Z");
        var task = AgentTask.Create("timestamp guard", now);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => task.TransitionTo(AgentTaskStatus.Planning, now.AddTicks(-1)));
    }
}
