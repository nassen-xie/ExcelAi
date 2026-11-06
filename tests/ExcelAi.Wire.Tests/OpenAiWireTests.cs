using System.Text.Json;

namespace ExcelAi.Tests;

/// <summary>OpenAI 互換の API（DeepSeek など）を、net48 の HttpClient で小さなサーバーにつなぎ、送る内容と受け取り方を確かめる。</summary>
[Collection("env")]
public sealed class OpenAiWireTests
{
    private static (OpenAiLlm Llm, AiSettings Settings) Create(FakeAnthropicServer server, string provider = "deepseek")
    {
        var settings = new AiSettings { Provider = provider, ApiKey = "sk-deepseek-wiretest", BaseUrl = server.Url, TimeoutSeconds = 20, ConsentAccepted = true };
        return (new OpenAiLlm(() => settings), settings);
    }

    private static LlmRequest Req(string text = "Hello") => new()
    {
        Model = "deepseek-flash",
        System = "Be brief",
        Messages = new[] { new ChatMessage(true, "first"), new ChatMessage(false, "reply"), new ChatMessage(true, text) },
        MaxTokens = 777,
        Effort = "medium",
    };

    private static string Completion(string text, string finish = "stop", int prompt = 30, int completion = 8, int hit = 0) =>
        "{\"id\":\"c1\",\"object\":\"chat.completion\",\"model\":\"deepseek-flash\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":"
        + JsonSerializer.Serialize(text) + "},\"finish_reason\":\"" + finish + "\"}],\"usage\":{\"prompt_tokens\":" + prompt + ",\"completion_tokens\":" + completion
        + ",\"prompt_cache_hit_tokens\":" + hit + ",\"prompt_cache_miss_tokens\":" + (prompt - hit) + "}}";

    [Fact]
    public async Task Complete_Sends_The_Expected_Request_And_Reads_The_Reply()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, Completion("こんにちは", prompt: 30, completion: 8, hit: 10)));
        var (llm, _) = Create(server);

        var result = await llm.CompleteAsync(Req(), default);
        Assert.Equal("こんにちは", result.Text);
        Assert.Equal(20, result.InputTokens);
        Assert.Equal(10, result.CacheReadTokens);
        Assert.Equal(8, result.OutputTokens);
        Assert.Equal("end_turn", result.StopReason);

        var request = server.Requests.Single();
        Assert.Equal("/chat/completions", request.Path);
        Assert.Equal("Bearer sk-deepseek-wiretest", request.Headers["Authorization"]);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("deepseek-flash", root.GetProperty("model").GetString());
        Assert.Equal(777, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("stream", out _));
        Assert.False(root.TryGetProperty("output_config", out _));
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, messages.Select(m => m.GetProperty("role").GetString()).ToArray());
        Assert.Equal("Be brief", messages[0].GetProperty("content").GetString());
        Assert.Equal("Hello", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Base_Url_With_A_Path_And_Trailing_Slash_Is_Handled()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, Completion("ok")));
        var settings = new AiSettings { Provider = "custom", ApiKey = "k", BaseUrl = server.Url + "/v1/", TimeoutSeconds = 20 };
        await new OpenAiLlm(() => settings).CompleteAsync(Req(), default);
        Assert.Equal("/v1/chat/completions", server.Requests.Single().Path);
    }

    [Fact]
    public async Task OpenAi_Provider_Uses_Max_Completion_Tokens()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, Completion("ok")));
        var (llm, _) = Create(server, "openai");
        await llm.CompleteAsync(Req(), default);
        using var body = JsonDocument.Parse(server.Requests.Single().Body);
        Assert.Equal(777, body.RootElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(body.RootElement.TryGetProperty("max_tokens", out _));
    }

    [Fact]
    public async Task OpenAi_Style_Cached_Tokens_Are_Read()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, "{\"choices\":[{\"message\":{\"content\":\"x\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":100,\"completion_tokens\":3,\"prompt_tokens_details\":{\"cached_tokens\":60}}}"));
        var (llm, _) = Create(server, "openai");
        var result = await llm.CompleteAsync(Req(), default);
        Assert.Equal(40, result.InputTokens);
        Assert.Equal(60, result.CacheReadTokens);
    }

    [Fact]
    public async Task Length_Finish_Reason_Is_Truncated_And_Content_Filter_Is_Refusal()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, Completion("cut off", "length")));
        server.Enqueue(new(200, Completion("", "content_filter")));
        var (llm, _) = Create(server);
        Assert.True((await llm.CompleteAsync(Req(), default)).Truncated);
        Assert.Equal("refusal", (await llm.CompleteAsync(Req(), default)).StopReason);
    }

    [Fact]
    public async Task Stream_Delivers_Deltas_And_Final_Usage()
    {
        using var server = new FakeAnthropicServer();
        string sse =
            "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\",\"content\":\"\"}}]}\n\n"
            + "data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"thinking...\"}}]}\n\n"
            + "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n"
            + "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"},\"finish_reason\":\"stop\"}]}\n\n"
            + "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":4,\"prompt_cache_hit_tokens\":2}}\n\n"
            + "data: [DONE]\n\n";
        server.Enqueue(new(200, sse, "text/event-stream"));
        var (llm, _) = Create(server);

        var pieces = new List<string>();
        var result = await llm.StreamAsync(Req(), pieces.Add, default);
        Assert.Equal(new[] { "Hel", "lo" }, pieces.ToArray());
        Assert.Equal("Hello", result.Text);
        Assert.Equal(10, result.InputTokens);
        Assert.Equal(2, result.CacheReadTokens);
        Assert.Equal(4, result.OutputTokens);
        Assert.Equal("end_turn", result.StopReason);
        using var body = JsonDocument.Parse(server.Requests.Single().Body);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.True(body.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean());
    }

    [Theory]
    [InlineData(401, AiErrorKind.Auth)]
    [InlineData(403, AiErrorKind.Auth)]
    [InlineData(402, AiErrorKind.Billing)]
    [InlineData(429, AiErrorKind.RateLimit)]
    [InlineData(503, AiErrorKind.Overloaded)]
    [InlineData(500, AiErrorKind.Overloaded)]
    [InlineData(400, AiErrorKind.BadRequest)]
    [InlineData(404, AiErrorKind.BadRequest)]
    public async Task Http_Status_Is_Mapped_To_An_Error_Kind(int status, AiErrorKind kind)
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(status, "{\"error\":{\"message\":\"server said no\",\"type\":\"x\"}}"));
        var (llm, _) = Create(server);
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(kind, e.Kind);
        Assert.Equal(status, e.Status);
        if (kind == AiErrorKind.BadRequest)
        {
            Assert.Equal("server said no", e.Detail);
        }

        Assert.Equal(kind is AiErrorKind.RateLimit or AiErrorKind.Overloaded, e.IsTransient);
    }

    [Fact]
    public async Task Missing_Key_Is_Reported_Without_Any_Request()
    {
        using var home = new TempHome();
        using var server = new FakeAnthropicServer();
        var settings = new AiSettings { BaseUrl = server.Url };
        var e = await Assert.ThrowsAsync<AiException>(() => new OpenAiLlm(() => settings).CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.NoKey, e.Kind);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Custom_Provider_Without_Base_Url_Is_Rejected()
    {
        var settings = new AiSettings { Provider = "custom", ApiKey = "k" };
        var e = await Assert.ThrowsAsync<AiException>(() => new OpenAiLlm(() => settings).CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.BadRequest, e.Kind);
    }

    [Fact]
    public async Task Unreachable_Server_Is_A_Network_Error()
    {
        var settings = new AiSettings { ApiKey = "k", BaseUrl = "http://127.0.0.1:1", TimeoutSeconds = 10 };
        var e = await Assert.ThrowsAsync<AiException>(() => new OpenAiLlm(() => settings).CompleteAsync(Req(), default));
        Assert.True(e.Kind is AiErrorKind.Network or AiErrorKind.Timeout, e.Kind.ToString());
    }

    [Fact]
    public async Task User_Cancel_Is_Cancelled_Not_Timeout()
    {
        using var server = new FakeAnthropicServer();
        var (llm, _) = Create(server);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), cts.Token));
        Assert.Equal(AiErrorKind.Cancelled, e.Kind);
    }

    [Fact]
    public async Task Router_Picks_The_Implementation_By_Provider()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, Completion("from openai-style")));
        server.Enqueue(new(200, FakeAnthropicServer.MessageJson("from claude-style")));
        var settings = new AiSettings { Provider = "deepseek", ApiKey = "k", BaseUrl = server.Url, TimeoutSeconds = 20 };
        var router = new RoutingLlm(() => settings);

        Assert.Equal("from openai-style", (await router.CompleteAsync(Req(), default)).Text);
        settings.SetKey("anthropic", "k2");
        settings.Provider = "anthropic";
        Assert.Equal("from claude-style", (await router.CompleteAsync(Req(), default)).Text);
        Assert.Equal(new[] { "/chat/completions", "/v1/messages" }, server.Requests.Select(r => r.Path).ToArray());
    }
}
