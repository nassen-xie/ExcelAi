using System.Net;
using System.Text;

namespace ExcelAi.Tests;

/// <summary>差し替え用のモデル。呼び出しを数え、決めた順に結果・エラーを返す。</summary>
public sealed class FakeLlm : ILlm
{
    public Func<LlmRequest, int, LlmResult> Handler { get; set; } = (r, n) => new LlmResult { Text = "ok:" + r.Messages[^1].Text, InputTokens = 10, OutputTokens = 5, StopReason = "end_turn" };

    private int calls;
    private int active;
    private int maxActive;

    public int Calls => calls;

    public int MaxActive => maxActive;

    public TimeSpan Latency { get; set; } = TimeSpan.Zero;

    public List<LlmRequest> Requests { get; } = new();

    public async Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel)
    {
        int n = Interlocked.Increment(ref calls);
        int now = Interlocked.Increment(ref active);
        int seen;
        while (now > (seen = maxActive))
        {
            Interlocked.CompareExchange(ref maxActive, now, seen);
        }

        try
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            if (Latency > TimeSpan.Zero)
            {
                await Task.Delay(Latency, cancel);
            }

            cancel.ThrowIfCancellationRequested();
            return Handler(request, n);
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    public async Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel)
    {
        var result = await CompleteAsync(request, cancel);
        foreach (char ch in result.Text)
        {
            onDelta(ch.ToString());
        }

        return result;
    }
}

