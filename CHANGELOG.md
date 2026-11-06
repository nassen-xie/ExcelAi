# Changelog

## 1.2.1
- New app icon (ribbon buttons, right-click menu entry, dialogs).
- Language "Auto" now follows Excel's own UI language first (ja / zh / en, English otherwise), then Windows.
- New About dialog (ribbon button): version, author (株式会社ナッセンシステム, contact@nassen.co.jp, https://nassen.co.jp), MIT license and third-party notices, shortcuts to the data and log folders. Author/company/copyright are also set in the file properties.
- Worksheet function descriptions are now in English; the built-in sample template follows the UI language.
- Open source: MIT License (LICENSE).
- New landing page (docs/site, built into dist/site by build.ps1).

## 1.1.2
- Fix: dialog and chat-pane buttons no longer cut off their text ("送信 (Ctrl+Enter)"); buttons size to their text and wrap in narrow task panes.

## 1.1.1
- Fix: inside Excel the add-in could not connect to DeepSeek / OpenAI / Claude ("通信できません") because Excel runs .NET Framework with TLS 1.0/1.1 only; TLS 1.2/1.3 are now enabled at start-up. Network errors now show the underlying reason and the log keeps the exception chain.

## 1.1.0
- DeepSeek is now the default AI service (OpenAI-compatible Chat Completions, streaming, cache-hit token accounting). OpenAI, Qwen, Moonshot, Zhipu, SiliconFlow, OpenRouter and any other OpenAI-compatible endpoint (Custom) work with the user's own key; Claude stays available.
- API keys are stored per service; settings from 1.0.0 (Claude key) keep working and are switched to Claude automatically.
- Model name is free text; cost estimate shows `?` for models with unknown prices. New error: insufficient balance (HTTP 402).
- DeepSeek whale icon on the ribbon buttons, dialogs, and a new cell right-click menu entry "Process with AI…" (also in tables).
- Test scripts now run in an isolated Excel instance with a temp data folder.

## 1.0.0
- `=AI`, `AI_TRANSLATE`, `AI_SUMMARIZE`, `AI_CLASSIFY`, `AI_EXTRACT`, `AI_USAGE`, `AI_VERSION` worksheet functions (async, cached).
- Ribbon [AI] tab: process selection (cell/row mode, right/column/replace output, placeholders, resume, cost estimate), chat task pane (streaming), prompt templates, usage, settings, help.
- Per-user API key (DPAPI-encrypted), consent dialog, model selection, monthly usage meter, retry/backoff on rate limits and 5xx.
- UI languages: ja / zh / en.
- 32-bit and 64-bit packed .xll; PowerShell installer (no admin rights).
