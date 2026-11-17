public sealed class DeadlinePauseTests
{
    [Fact]
    public void Waiting_for_a_person_does_not_spend_the_run_budget()
    {
        using var deadline = new Deadline(TimeSpan.FromMilliseconds(300));

        Deadline.WhileHumanDecides(() => { Thread.Sleep(600); return "y"; });
        Assert.False(deadline.Token.IsCancellationRequested, "the pause spent the budget");

        Thread.Sleep(600);
        Assert.True(deadline.Token.IsCancellationRequested, "the budget never ran out");
    }
}
