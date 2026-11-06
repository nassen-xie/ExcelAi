using System;
using System.Linq;
using System.Text.Json;
using ExcelDna.Integration;

namespace ExcelAi.Addin
{
    /// <summary>
    /// 自動テスト用（tools\smoke-test.ps1 が Application.Run で呼ぶ）。選択範囲の処理を、画面なしで実行する。
    /// 非表示の関数で、数式のセルからは呼ばない。
    /// </summary>
    public static class SelfTest
    {
        [ExcelFunction(Name = "AI_SELFTEST_PROCESS", IsMacroType = true, IsHidden = true)]
        public static string Process(string optionsJson)
        {
            var options = JsonSerializer.Deserialize<ProcessOptions>(optionsJson) ?? new ProcessOptions();
            var selection = ExcelHost.ReadSelection(out string? error);
            if (selection is null)
            {
                return "ERROR " + error;
            }

            var settings = SettingsStore.Current;
            var plan = ProcessEngine.Build(selection, options, settings, forEstimate: false);
            if (plan.Error is not null)
            {
                return "ERROR " + plan.Error;
            }

            var results = BatchRunner.RunAsync(plan.Items, AiGateway.Default, (_, _) => { }, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            int written = 0, failed = 0;
            foreach (var result in results)
            {
                string? text = ProcessEngine.ResultText(result);
                if (text is null)
                {
                    continue;
                }

                var (row, col) = plan.Targets[result.Index];
                if (ExcelHost.WriteCell(selection.Sheet, row, col, text))
                {
                    written++;
                }

                if (!result.Ok)
                {
                    failed++;
                }
            }

            return $"items={plan.Items.Count} written={written} failed={failed}";
        }
    }
}
