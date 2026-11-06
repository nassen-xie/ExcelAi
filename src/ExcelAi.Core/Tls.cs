using System;
using System.Net;

namespace ExcelAi
{
    /// <summary>
    /// Excel の中では .NET Framework が古い既定（TLS 1.0 / 1.1 まで）で動くことがあり、TLS 1.2 以上しか受け付けない
    /// サービス（DeepSeek・OpenAI・Anthropic など）に接続できない。使える TLS を足す（足すだけで、ほかのアドインの設定は減らさない）。
    /// </summary>
    public static class Tls
    {
        private const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        private const SecurityProtocolType Tls13 = (SecurityProtocolType)12288;

        public static void Enable()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= Tls12 | Tls13;
            }
            catch (NotSupportedException)
            {
                // TLS 1.3 を知らない環境では、1.2 だけ足す
                try
                {
                    ServicePointManager.SecurityProtocol |= Tls12;
                }
                catch (NotSupportedException)
                {
                }
            }
        }
    }
}
