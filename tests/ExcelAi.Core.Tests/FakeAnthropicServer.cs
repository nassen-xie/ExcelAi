using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ExcelAi.Tests;

/// <summary>Anthropic の Messages API のふりをする小さなサーバー（SDK の送る内容と、受け取り方を確かめる）。</summary>
public sealed class FakeAnthropicServer : IDisposable
{
    public sealed record Reply(int Status, string Body, string ContentType = "application/json", Dictionary<string, string>? Headers = null);

    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentQueue<Reply> replies = new();

    public FakeAnthropicServer()
    {
        int port;
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        Url = $"http://127.0.0.1:{port}";
        listener.Prefixes.Add(Url + "/");
        listener.Start();
        _ = Task.Run(Loop);
    }

    public string Url { get; }

    public List<(string Path, string Body, Dictionary<string, string> Headers)> Requests { get; } = new();

    public void Enqueue(Reply reply) => replies.Enqueue(reply);

    public static string MessageJson(string text, int input = 12, int output = 5, string stop = "end_turn") =>
        "{\"id\":\"msg_test\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-opus-5-5\",\"content\":[{\"type\":\"text\",\"text\":" + System.Text.Json.JsonSerializer.Serialize(text)
        + "}],\"stop_reason\":\"" + stop + "\",\"stop_sequence\":null,\"usage\":{\"input_tokens\":" + input + ",\"output_tokens\":" + output + "}}";

    public static string StreamBody(string[] parts, int input = 12, int output = 5)
    {
        var sb = new StringBuilder();
        void Event(string name, string data) => sb.Append("event: ").Append(name).Append("\ndata: ").Append(data).Append("\n\n");
        Event("message_start", "{\"type\":\"message_start\",\"message\":{\"id\":\"msg_s\",\"type\":\"message\",\"role\":\"assistant\",\"model\":\"claude-opus-5-5\",\"content\":[],\"stop_reason\":null,\"stop_sequence\":null,\"usage\":{\"input_tokens\":" + input + ",\"output_tokens\":1}}}");
        Event("content_block_start", "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}");
        foreach (string part in parts)
        {
            Event("content_block_delta", "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":" + System.Text.Json.JsonSerializer.Serialize(part) + "}}");
        }

        Event("content_block_stop", "{\"type\":\"content_block_stop\",\"index\":0}");
        Event("message_delta", "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\",\"stop_sequence\":null},\"usage\":{\"output_tokens\":" + output + "}}");
        Event("message_stop", "{\"type\":\"message_stop\"}");
        return sb.ToString();
    }

    private async Task Loop()
    {
        while (!stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();
            var headers = context.Request.Headers.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => context.Request.Headers[k] ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            lock (Requests)
            {
                Requests.Add((context.Request.Url!.AbsolutePath, body, headers));
            }

            Reply reply = replies.TryDequeue(out var queued) ? queued : new Reply(200, MessageJson("default"));
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = reply.ContentType;
            foreach (var header in reply.Headers ?? new Dictionary<string, string>())
            {
                context.Response.Headers[header.Key] = header.Value;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(reply.Body);
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Close();
    }
}
