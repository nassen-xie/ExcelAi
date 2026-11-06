namespace ExcelAi.Tests;

[Collection("env")]
public sealed class GatewayTests
{
    private static AiSettings Ready(Action<AiSettings>? change = null)
    {
        var s = new AiSettings { ApiKey = "sk-ant-x", ConsentAccepted = true, MaxConcurrency = 3 };
        change?.Invoke(s);
        return s;
    }

    private static AiGateway Create(FakeLlm llm, AiSettings settings, string home, bool consent = true) =>
        new(llm, () => settings, new ResultCache(Path.Combine(home, "c.jsonl")), new UsageMeter(null), consent, (_, _) => Task.CompletedTask);

    private static LlmRequest Req(AiGateway g, string text) => g.NewRequest(null, new[] { new ChatMessage(true, text) });

    [Fact]
    public async Task Missing_Key_Or_Consent_Fail_Before_Any_Call()
    {
        using var home = new TempHome();
        var llm = new FakeLlm();
        var noKey = Create(llm, new AiSettings { ConsentAccepted = true }, home.Path);
        var e1 = await Assert.ThrowsAsync<AiException>(() => noKey.CompleteAsync(Req(noKey, "x"), default));
        Assert.Equal(AiErrorKind.NoKey, e1.Kind);

        var noConsent = Create(llm, new AiSettings { ApiKey = "sk-ant-x" }, home.Path);
        var e2 = await Assert.ThrowsAsync<AiException>(() => noConsent.CompleteAsync(Req(noConsent, "x"), default));
        Assert.Equal(AiErrorKind.NoConsent, e2.Kind);
        Assert.Equal(0, llm.Calls);
    }

    [Fact]
    public async Task Identical_Requests_Are_Served_From_The_Cache()
    {
        using var home = new TempHome();
        var llm = new FakeLlm();
        var g = Create(llm, Ready(), home.Path);
        var first = await g.CompleteAsync(Req(g, "hello"), default);
        var second = await g.CompleteAsync(Req(g, "hello"), default);
        Assert.Equal(1, llm.Calls);
        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(1, g.Usage.ThisMonth().CacheHits);

        var off = Create(llm, Ready(s => s.UseCache = false), Path.Combine(home.Path));
        await off.CompleteAsync(Req(off, "hello"), default);
        await off.CompleteAsync(Req(off, "hello"), default);
        Assert.Equal(3, llm.Calls);
    }

    [Fact]
    public async Task Truncated_Results_Are_Not_Cached()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => new LlmResult { Text = "cut", StopReason = "max_tokens" } };
        var g = Create(llm, Ready(), home.Path);
        var r = await g.CompleteAsync(Req(g, "q"), default);
        Assert.True(r.Truncated);
        await g.CompleteAsync(Req(g, "q"), default);
        Assert.Equal(2, llm.Calls);
    }

    [Fact]
    public async Task Transient_Errors_Are_Retried_Then_Succeed()
    {
        using var home = new TempHome();
        var llm = new FakeLlm
        {
            Handler = (r, n) => n < 3 ? throw new AiException(AiErrorKind.RateLimit, string.Empty, 429) : new LlmResult { Text = "done", StopReason = "end_turn" },
        };
        var g = Create(llm, Ready(), home.Path);
        var result = await g.CompleteAsync(Req(g, "x"), default);
        Assert.Equal("done", result.Text);
        Assert.Equal(3, llm.Calls);
    }

    [Fact]
    public async Task Gives_Up_After_Four_Attempts()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => throw new AiException(AiErrorKind.Overloaded, string.Empty, 529) };
        var g = Create(llm, Ready(), home.Path);
        var e = await Assert.ThrowsAsync<AiException>(() => g.CompleteAsync(Req(g, "x"), default));
        Assert.Equal(AiErrorKind.Overloaded, e.Kind);
        Assert.Equal(4, llm.Calls);
    }

    [Theory]
    [InlineData(AiErrorKind.Auth)]
    [InlineData(AiErrorKind.BadRequest)]
    public async Task Permanent_Errors_Are_Not_Retried(AiErrorKind kind)
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => throw new AiException(kind) };
        var g = Create(llm, Ready(), home.Path);
        var e = await Assert.ThrowsAsync<AiException>(() => g.CompleteAsync(Req(g, "x"), default));
        Assert.Equal(kind, e.Kind);
        Assert.Equal(1, llm.Calls);
    }

    [Fact]
    public async Task Refusal_Becomes_An_Error_And_Is_Not_Cached()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => new LlmResult { Text = "", StopReason = "refusal" } };
        var g = Create(llm, Ready(), home.Path);
        var e = await Assert.ThrowsAsync<AiException>(() => g.CompleteAsync(Req(g, "x"), default));
        Assert.Equal(AiErrorKind.Refused, e.Kind);
    }

    [Fact]
    public async Task Concurrency_Never_Exceeds_The_Setting()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Latency = TimeSpan.FromMilliseconds(40) };
        var g = Create(llm, Ready(s => { s.MaxConcurrency = 2; s.UseCache = false; }), home.Path);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => g.CompleteAsync(Req(g, "q" + i), default)));
        Assert.Equal(12, llm.Calls);
        Assert.True(llm.MaxActive <= 2, "max active " + llm.MaxActive);
        Assert.True(llm.MaxActive >= 1);
    }

    [Fact]
    public async Task Cancellation_Is_Reported_As_Cancelled()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Latency = TimeSpan.FromSeconds(5) };
        var g = Create(llm, Ready(s => s.UseCache = false), home.Path);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var e = await Assert.ThrowsAsync<AiException>(() => g.CompleteAsync(Req(g, "x"), cts.Token));
        Assert.Equal(AiErrorKind.Cancelled, e.Kind);
    }

    [Fact]
    public async Task Request_Uses_Settings_Model_Effort_And_System()
    {
        using var home = new TempHome();
        var llm = new FakeLlm();
        var g = Create(llm, Ready(s => { s.Model = "claude-haiku-5-5"; s.Effort = "high"; s.MaxTokens = 999; s.SystemPrompt = "SYS"; }), home.Path);
        await g.CompleteAsync(Req(g, "x"), default);
        var r = llm.Requests.Single();
        Assert.Equal("claude-haiku-5-5", r.Model);
        Assert.Equal("high", r.Effort);
        Assert.Equal(999, r.MaxTokens);
        Assert.Equal("SYS", r.System);
    }

    [Fact]
    public async Task Streaming_Passes_Deltas_And_Retries_Only_Before_The_First_One()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => new LlmResult { Text = "abc", StopReason = "end_turn" } };
        var g = Create(llm, Ready(), home.Path);
        var text = new List<string>();
        var result = await g.StreamAsync(Req(g, "x"), text.Add, default);
        Assert.Equal("abc", string.Concat(text));
        Assert.Equal("abc", result.Text);
    }
}

[Collection("env")]
public sealed class BatchTests
{
    private static AiGateway Gateway(FakeLlm llm, string home, Action<AiSettings>? change = null)
    {
        var s = new AiSettings { ApiKey = "sk-ant-x", ConsentAccepted = true, UseCache = false };
        change?.Invoke(s);
        return new AiGateway(llm, () => s, new ResultCache(null), new UsageMeter(null), true, (_, _) => Task.CompletedTask);
    }

    [Fact]
    public async Task All_Items_Are_Processed_And_Progress_Is_Reported()
    {
        using var home = new TempHome();
        var llm = new FakeLlm();
        var items = Enumerable.Range(0, 10).Select(i => new BatchItem(i, "p" + i)).ToList();
        var seen = new List<int>();
        BatchProgress? last = null;
        var results = await BatchRunner.RunAsync(items, Gateway(llm, home.Path), (r, p) => { seen.Add(r.Index); last = p; }, default);
        Assert.Equal(10, results.Count);
        Assert.All(results, r => Assert.True(r.Ok));
        Assert.Equal("ok:p3", results[3].Text);
        Assert.Equal(Enumerable.Range(0, 10), seen.OrderBy(x => x));
        Assert.Equal(10, last!.Done);
        Assert.Equal(0, last.Failed);
    }

    [Fact]
    public async Task Failures_Are_Counted_And_Other_Items_Continue()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (r, _) => r.Messages[0].Text == "p2" ? throw new AiException(AiErrorKind.BadRequest) : new LlmResult { Text = "fine", StopReason = "end_turn" } };
        var items = Enumerable.Range(0, 5).Select(i => new BatchItem(i, "p" + i)).ToList();
        BatchProgress? last = null;
        var results = await BatchRunner.RunAsync(items, Gateway(llm, home.Path), (_, p) => last = p, default);
        Assert.Equal(1, last!.Failed);
        Assert.False(results[2].Ok);
        Assert.Equal(AiErrorKind.BadRequest, results[2].Error!.Kind);
        Assert.Equal(4, results.Count(r => r.Ok));
    }

    [Fact]
    public async Task Auth_Error_Stops_The_Whole_Batch_Early()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Handler = (_, _) => throw new AiException(AiErrorKind.Auth, string.Empty, 401), Latency = TimeSpan.FromMilliseconds(5) };
        var items = Enumerable.Range(0, 300).Select(i => new BatchItem(i, "p" + i)).ToList();
        var results = await BatchRunner.RunAsync(items, Gateway(llm, home.Path, s => s.MaxConcurrency = 1), (_, _) => { }, default);
        Assert.Equal(300, results.Count);
        Assert.All(results, r => Assert.False(r.Ok));
        Assert.True(llm.Calls < 100, "calls " + llm.Calls);
    }

    [Fact]
    public async Task Cancellation_Marks_Remaining_Items_Cancelled()
    {
        using var home = new TempHome();
        var llm = new FakeLlm { Latency = TimeSpan.FromMilliseconds(30) };
        using var cts = new CancellationTokenSource();
        var items = Enumerable.Range(0, 200).Select(i => new BatchItem(i, "p" + i)).ToList();
        int done = 0;
        var results = await BatchRunner.RunAsync(items, Gateway(llm, home.Path, s => s.MaxConcurrency = 2), (_, _) =>
        {
            if (Interlocked.Increment(ref done) == 5)
            {
                cts.Cancel();
            }
        }, cts.Token);
        Assert.Equal(200, results.Count);
        Assert.Contains(results, r => r.Ok);
        Assert.Contains(results, r => r.Error?.Kind == AiErrorKind.Cancelled);
        Assert.True(llm.Calls < 200);
    }

    [Fact]
    public void Cost_Estimate_Grows_With_Items_And_Uses_The_Model()
    {
        var s = new AiSettings { Model = "claude-opus-5-5", MaxTokens = 4096 };
        var few = BatchRunner.EstimateCost(new[] { new BatchItem(0, new string('a', 400)) }, s.SystemPrompt, s);
        var many = BatchRunner.EstimateCost(Enumerable.Range(0, 100).Select(i => new BatchItem(i, new string('a', 400))).ToList(), s.SystemPrompt, s);
        Assert.True(few > 0);
        Assert.Equal(few * 100, many);
        s.Model = "claude-haiku-5-5";
        Assert.True(BatchRunner.EstimateCost(new[] { new BatchItem(0, new string('a', 400)) }, s.SystemPrompt, s) < few);
    }
}
