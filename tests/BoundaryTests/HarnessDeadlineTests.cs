using System.Diagnostics;
using System.Runtime.CompilerServices;

public sealed class HarnessDeadlineTests : IDisposable
{
    readonly Scratch _scratch = new();

    [Fact]
    public async Task The_harness_hands_the_run_deadline_to_a_running_command()
    {
        var repo = Git.Init(_scratch.PathTo("repo"));
        var pid = _scratch.PathTo("git.pid");
        using var env = new EnvironmentScope()
            .PrependPath(_scratch.FakeTool("git", $"echo $$ > {pid}\nsleep 20"));
        var policy = new CommandPolicy(repo, TimeSpan.FromSeconds(60));
        AIAgent agent = new ScriptedChatClient("run_command", "git status")
            .AsHarnessAgent(new HarnessAgentOptions
            {
                AgentModeProviderOptions = new() { DefaultMode = "execute" },
                ToolApprovalAgentOptions = new()
                {
                    AutoApprovalRules =
                    [
                        rule => ValueTask.FromResult(
                            policy.AutoApproves(rule.FunctionCallContent)),
                    ],
                },
                LoopAgentOptions = new() { MaxIterations = 1 },
                DisableWebSearch = true,
                DisableFileMemory = true,
                DisableAgentSkillsProvider = true,
                ChatOptions = new()
                {
                    Tools =
                    [
                        new ApprovalRequiredAIFunction(
                            AIFunctionFactory.Create(policy.RunAsync, "run_command")),
                    ],
                },
            });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var timer = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Runner.RunAsync(agent, await agent.CreateSessionAsync(), "go",
                Approver.FromArgs([]), deadline.Token));

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(6), $"took {timer.Elapsed}");
        Thread.Sleep(200);
        Assert.False(Processes.Alive(Processes.ReadPid(pid)), "git is still running");
    }

    public void Dispose() => _scratch.Dispose();
}

sealed class ScriptedChatClient(string tool, string command) : IChatClient
{
    int _turn;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse(Next()));

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (var update in new ChatResponse(Next()).ToChatResponseUpdates())
            yield return update;
    }

    ChatMessage Next() => _turn++ == 0
        ? new(ChatRole.Assistant,
            [new FunctionCallContent("call-1", tool, new Dictionary<string, object?>
                { ["command"] = command })])
        : new(ChatRole.Assistant, "done");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
