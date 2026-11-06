using System.Text.Json;

namespace ExcelAi.Tests;

/// <summary>本物の SDK（AnthropicLlm）を、Messages API のふりをする小さなサーバーにつなぎ、送る内容と受け取り方を確かめる。</summary>
[Collection("env")]
public sealed class AnthropicWireTests
{
    private static (AnthropicLlm Llm, AiSettings Settings) Create(FakeAnthropicServer server)
    {
        var settings = new AiSettings { Provider = "anthropic", ApiKey = "sk-ant-api03-wiretest", BaseUrl = server.Url, TimeoutSeconds = 20, ConsentAccepted = true };
        return (new AnthropicLlm(() => settings), settings);
    }

    private static LlmRequest Req(string text = "Hello", string system = "Be brief") => new()
    {
        Model = "claude-opus-5-5",
        System = system,
        Messages = new[] { new ChatMessage(true, "first"), new ChatMessage(false, "reply"), new ChatMessage(true, text) },
        MaxTokens = 777,
        Effort = "medium",
    };

    [Fact]
    public async Task Complete_Sends_The_Expected_Request_And_Reads_The_Reply()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, FakeAnthropicServer.MessageJson("こんにちは", 21, 8)));
        var (llm, _) = Create(server);

        var result = await llm.CompleteAsync(Req(), default);
        Assert.Equal("こんにちは", result.Text);
        Assert.Equal(21, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
        Assert.Equal("end_turn", result.StopReason);
        Assert.False(result.Truncated);

        var request = server.Requests.Single();
        Assert.Equal("/v1/messages", request.Path);
        // 公式 SDK は API キーを Authorization: Bearer で送る
        Assert.Equal("Bearer sk-ant-api03-wiretest", request.Headers["Authorization"]);
        Assert.True(request.Headers.ContainsKey("anthropic-version"));
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
        Assert.Equal(777, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("medium", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Contains("Be brief", root.GetProperty("system").ToString());
        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(new[] { "user", "assistant", "user" }, messages.Select(m => m.GetProperty("role").GetString()).ToArray());
        Assert.Contains("Hello", messages[2].GetProperty("content").ToString());
        Assert.False(root.TryGetProperty("temperature", out _));
        Assert.False(root.TryGetProperty("thinking", out _));
    }

    [Fact]
    public async Task Truncated_Reply_Is_Flagged()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, FakeAnthropicServer.MessageJson("cut off", 5, 777, "max_tokens")));
        var (llm, _) = Create(server);
        Assert.True((await llm.CompleteAsync(Req(), default)).Truncated);
    }

    [Fact]
    public async Task Stream_Delivers_Deltas_In_Order_With_Usage()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(200, FakeAnthropicServer.StreamBody(new[] { "Hel", "lo ", "世界" }, 30, 9), "text/event-stream"));
        var (llm, _) = Create(server);
        var parts = new List<string>();

        var result = await llm.StreamAsync(Req(), parts.Add, default);
        Assert.Equal(new[] { "Hel", "lo ", "世界" }, parts);
        Assert.Equal("Hello 世界", result.Text);
        Assert.Equal(30, result.InputTokens);
        Assert.Equal(9, result.OutputTokens);
        Assert.Equal("end_turn", result.StopReason);
        using var body = JsonDocument.Parse(server.Requests.Single().Body);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }

    [Theory]
    [InlineData(401, AiErrorKind.Auth)]
    [InlineData(403, AiErrorKind.Auth)]
    [InlineData(400, AiErrorKind.BadRequest)]
    [InlineData(404, AiErrorKind.BadRequest)]
    public async Task Http_Errors_Map_To_Error_Kinds(int status, AiErrorKind expected)
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(status, "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"nope\"}}"));
        var (llm, _) = Create(server);
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(expected, e.Kind);
    }

    [Fact]
    public async Task Rate_Limit_And_Overload_Are_Transient()
    {
        using var server = new FakeAnthropicServer();
        for (int i = 0; i < 2; i++)
        {
            server.Enqueue(new(429, "{\"type\":\"error\",\"error\":{\"type\":\"rate_limit_error\",\"message\":\"slow down\"}}", "application/json", new() { ["retry-after"] = "0" }));
        }

        var (llm, _) = Create(server);
        var e1 = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.RateLimit, e1.Kind);
        Assert.True(e1.IsTransient);

        for (int i = 0; i < 2; i++)
        {
            server.Enqueue(new(529, "{\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"busy\"}}"));
        }

        var e2 = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.Overloaded, e2.Kind);
    }

    [Fact]
    public async Task Gateway_Over_The_Real_Sdk_Recovers_From_A_Rate_Limit()
    {
        using var home = new TempHome();
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(429, "{\"type\":\"error\",\"error\":{\"type\":\"rate_limit_error\",\"message\":\"slow\"}}", "application/json", new() { ["retry-after"] = "0" }));
        server.Enqueue(new(429, "{\"type\":\"error\",\"error\":{\"type\":\"rate_limit_error\",\"message\":\"slow\"}}", "application/json", new() { ["retry-after"] = "0" }));
        server.Enqueue(new(200, FakeAnthropicServer.MessageJson("recovered")));
        var (llm, settings) = Create(server);
        var gateway = new AiGateway(llm, () => settings, new ResultCache(null), new UsageMeter(null), true, (_, _) => Task.CompletedTask);

        var result = await gateway.CompleteAsync(gateway.NewRequest(null, new[] { new ChatMessage(true, "x") }), default);
        Assert.Equal("recovered", result.Text);
        Assert.True(server.Requests.Count >= 3);
    }

    [Fact]
    public async Task Missing_Key_Is_Reported_Without_Any_Request()
    {
        using var server = new FakeAnthropicServer();
        using var home = new TempHome();
        var settings = new AiSettings { Provider = "anthropic", BaseUrl = server.Url };
        var llm = new AnthropicLlm(() => settings);
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.NoKey, e.Kind);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task Unreachable_Server_Is_A_Network_Error()
    {
        var settings = new AiSettings { ApiKey = "sk-ant-x", BaseUrl = "http://127.0.0.1:1", TimeoutSeconds = 10 };
        var llm = new AnthropicLlm(() => settings);
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.True(e.Kind is AiErrorKind.Network or AiErrorKind.Timeout, e.Kind.ToString());
    }
    [Fact]
    public async Task Bad_Request_Shows_The_Servers_Message_Not_The_Raw_Json()
    {
        using var server = new FakeAnthropicServer();
        server.Enqueue(new(400, "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"max_tokens too large\"}}"));
        var (llm, _) = Create(server);
        var e = await Assert.ThrowsAsync<AiException>(() => llm.CompleteAsync(Req(), default));
        Assert.Equal(AiErrorKind.BadRequest, e.Kind);
        Assert.Equal("max_tokens too large", e.Detail);
        Assert.DoesNotContain("{", e.UserMessage);
    }
}
