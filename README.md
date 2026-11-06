# Excel AI アシスタント / Excel AI 助手 / Excel AI Assistant

DeepSeek を中心に、OpenAI 互換のサービスと Claude を Excel 2019+ デスクトップ（32/64 ビット）で使うアドイン。API キーは各ユーザーが入力します。
以 DeepSeek 为主（也支持兼容 OpenAI 接口的服务和 Claude）的 Excel 加载项，API 密钥由每位用户自行填写。 / An Excel add-in for DeepSeek (plus OpenAI-compatible services and Claude); each user supplies their own API key.

## 使う人へ / For users
`dist/ExcelAI-<version>/README.txt`（ja/zh/en）を参照。インストール: `Install.cmd`。

## ビルド / Build
Requirements: .NET SDK 8+ (tested with 10), Windows, .NET Framework 4.8.

```
powershell -File build.ps1              # build + tests + dist\ExcelAI-<version>.zip
powershell -File build.ps1 -SkipTests
powershell -File build.ps1 -Smoke       # also drives a separate real Excel (fake API server) and tests the installer
```

## 構成 / Layout
- `src/ExcelAi.Core` — settings (DPAPI, keys per provider), gateway (cache, retry, usage), `OpenAiLlm` (Chat Completions: DeepSeek and other compatible services) / `AnthropicLlm` (Claude SDK) behind `RoutingLlm`, templates, batch runner, i18n. netstandard2.0 + net8.0.
- `src/ExcelAi.Addin` — Excel-DNA add-in (net48): UDFs, ribbon, forms, chat pane.
- `tests/ExcelAi.Core.Tests` (net10), `tests/ExcelAi.Wire.Tests` (net48, HTTP wire tests against a fake server).
- `tools/` — smoke (`-Provider deepseek|anthropic`) / ribbon / right-click menu / UI / installer test scripts, `UiPreview`.
- `assets/` — app icon (`ai-*.png`, from `ai.png`) used for the ribbon buttons, the cell right-click menu entry and the dialogs.
- `installer/` — `Install.ps1`/`Uninstall.ps1` (+ .cmd).

## 注意 / Notes
- Data directory: `%APPDATA%\ExcelAi` (override with `EXCELAI_HOME`). Fallback key per provider from `DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, … (see `Providers` in `Models.cs`).
- Providers: DeepSeek (default), OpenAI, Anthropic Claude, Qwen, Moonshot, Zhipu, SiliconFlow, OpenRouter, Custom (any OpenAI-compatible base URL). The model name is free text. Prices are estimates and only known for DeepSeek/OpenAI/Claude models.
- Test scripts start their own Excel with `EXCELAI_HOME` pointing to a temp folder (`tools/TestExcel.ps1`), so they never touch your real settings or running Excel.
- Cell contents are sent to the selected AI service (DeepSeek by default) with the user's key; the add-in asks for consent on first use.
- Tests use a fake server; no real API calls are made (the DeepSeek / OpenAI-compatible wire format is checked against the published API shape, not against the live service). The 64-bit .xll is built but has not been run on a 64-bit Excel.
- Third-party licenses: `docs/THIRD-PARTY-NOTICES.txt`.
- Promo / landing page (ja / zh / en, auto language): `docs/site/index.html`; `build.ps1` assembles it into `dist/site` with the download zip.

## Sponsor section (landing page)
The "Support this project" section stays hidden until `docs/site/sponsor.json` has values. Put your own QR images in `docs/site/sponsor/` (e.g. `paypay.png`, `wechat.png`) and set `paypayQr` / `wechatQr` (paths like `sponsor/paypay.png`) and optionally `paypayUrl` (a PayPay payment link), then run `build.ps1`.

## Author / License
(c) 2026 株式会社ナッセンシステム — https://nassen.co.jp — contact@nassen.co.jp

Open source under the [MIT License](LICENSE). The interface follows Excel's language (ja / zh / en, English otherwise); see `Loc` in `src/ExcelAi.Core`.
