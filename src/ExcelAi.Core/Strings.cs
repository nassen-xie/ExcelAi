namespace ExcelAi
{
    public static partial class Loc
    {
        private static void Register()
        {
            // ---- 全般 ----
            K("app.name", "Excel AI アシスタント", "Excel AI 助手", "Excel AI Assistant");
            K("ok", "OK", "确定", "OK");
            K("cancel", "キャンセル", "取消", "Cancel");
            K("close", "閉じる", "关闭", "Close");
            K("yes", "はい", "是", "Yes");
            K("no", "いいえ", "否", "No");

            // ---- エラー ----
            K("err.nokey", "API キーが未設定です。[AI] タブの「設定」で入力してください。", "尚未设置 API 密钥。请在 [AI] 选项卡的“设置”中输入。", "No API key is set. Enter it in [AI] tab > Settings.");
            K("err.noconsent", "データ送信への同意がありません。[AI] タブの「設定」を開いて確認してください。", "尚未同意数据发送。请打开 [AI] 选项卡的“设置”确认。", "Data sending has not been accepted. Open [AI] tab > Settings to confirm.");
            K("err.auth", "API キーが正しくないか、権限がありません。", "API 密钥无效或没有权限。", "The API key is invalid or lacks permission.");
            K("err.billing", "残高が足りません。サービスの管理画面でチャージ（支払い）を確認してください。", "账户余额不足。请到服务商控制台充值或检查付款。", "Insufficient balance. Top up or check billing in the provider's console.");
            K("err.ratelimit", "呼び出しが多すぎます（制限）。少し待って再実行してください。", "调用过于频繁（限流）。请稍候重试。", "Too many requests (rate limit). Wait a moment and retry.");
            K("err.overloaded", "サービスが混み合っています。少し待って再実行してください。", "服务繁忙。请稍候重试。", "The service is busy. Wait a moment and retry.");
            K("err.network", "通信できません。ネットワークとプロキシを確認してください。", "无法连接。请检查网络和代理。", "Cannot connect. Check the network and proxy.");
            K("err.timeout", "時間切れです。設定のタイムアウトを延ばすか、入力を短くしてください。", "超时。请在设置中延长超时时间或缩短输入。", "Timed out. Increase the timeout in Settings or shorten the input.");
            K("err.badrequest", "依頼が受け付けられませんでした。", "请求未被接受。", "The request was rejected.");
            K("err.refused", "安全上の理由でモデルが回答を断りました。", "出于安全原因，模型拒绝回答。", "The model declined to answer for safety reasons.");
            K("err.cancelled", "キャンセルしました。", "已取消。", "Cancelled.");
            K("err.other", "エラーが発生しました。", "发生错误。", "An error occurred.");
            K("warn.truncated", "[最大トークンで中断]", "[因达到最大 token 数而中断]", "[stopped at the max tokens]");
            K("udf.prefix", "#AI!", "#AI!", "#AI!");

            // ---- 同意 ----
            K("consent.title", "データの送信について", "关于数据发送", "About sending data");
            K("consent.body",
                "このアドインは、処理するセルの内容と入力した指示を、あなたの API キーを使って 選択した AI サービス（既定は DeepSeek。設定で変更できます）に送信します。\r\n\r\n"
                + "・機密情報・個人情報を含むセルを選ばないでください。\r\n・送信の可否は、会社の規則に従ってください。\r\n・料金はあなたの API キーの契約に請求されます。\r\n\r\n同意して続けますか？",
                "此加载项会使用您的 API 密钥，将要处理的单元格内容和您输入的指令发送到您选择的 AI 服务（默认 DeepSeek，可在设置中更改）。\r\n\r\n"
                + "· 请勿选择包含机密信息或个人信息的单元格。\r\n· 是否允许发送，请遵守公司规定。\r\n· 费用将计入您的 API 密钥所属账户。\r\n\r\n是否同意并继续？",
                "This add-in sends the content of the cells you process and your instructions to the AI service you selected (DeepSeek by default; change it in Settings) using your API key.\r\n\r\n"
                + "- Do not select cells containing confidential or personal information.\r\n- Follow your company's rules on what may be sent.\r\n- Charges go to the account of your API key.\r\n\r\nAccept and continue?");
            K("consent.accept", "同意して続ける", "同意并继续", "Accept and continue");

            // ---- リボン ----
            K("rb.group", "AI アシスタント", "AI 助手", "AI Assistant");
            K("rb.settings", "設定", "设置", "Settings");
            K("rb.chat", "チャット", "聊天", "Chat");
            K("rb.process", "選択範囲を処理", "处理选中区域", "Process selection");
            K("rb.templates", "テンプレート", "模板", "Templates");
            K("rb.usage", "使用量", "用量", "Usage");
            K("rb.help", "ヘルプ", "帮助", "Help");
            K("tpl.sample.name", "(サンプル) 問い合わせの分類", "(示例) 咨询分类", "(sample) Classify inquiries");
            K("tpl.sample.prompt", "次の問い合わせを「不具合」「要望」「質問」「その他」のどれか 1 つに分類してください。分類名だけを出力してください。", "请把下面的咨询分类为“故障”“需求”“提问”“其他”之一，只输出分类名。", "Classify the following inquiry as exactly one of: Bug, Request, Question, Other. Output only the category name.");
            K("rb.about", "バージョン情報", "关于", "About");
            K("rb.about.tip", "バージョン・作者・ライセンスを表示します", "显示版本、作者和许可证", "Show version, author and license");
            K("about.title", "Excel AI アシスタントについて", "关于 Excel AI 助手", "About Excel AI Assistant");
            K("about.version", "バージョン {0}", "版本 {0}", "Version {0}");
            K("about.website", "ウェブサイト", "网站", "Website");
            K("about.contact", "お問い合わせ", "联系", "Contact");
            K("about.license", "ライセンス：{0}（オープンソース）", "许可证：{0}（开源）", "License: {0} (open source)");
            K("about.disclaimer", "このアドインは、利用者ご自身の API キーで AI サービスに接続します。DeepSeek・OpenAI・Anthropic などの各サービスとは無関係の、独立したソフトウェアです。", "此加载项使用您自己的 API 密钥连接 AI 服务。它是独立的软件，与 DeepSeek、OpenAI、Anthropic 等服务商没有关联。", "This add-in connects to AI services with your own API key. It is independent software and is not affiliated with DeepSeek, OpenAI, Anthropic or any other provider.");
            K("about.datafolder", "データフォルダーを開く", "打开数据文件夹", "Open data folder");
            K("about.logs", "ログを開く", "打开日志", "Open logs");
            K("rb.settings.tip", "API キー・モデルなどを設定します", "设置 API 密钥、模型等", "Set the API key, model and options");
            K("rb.chat.tip", "作業ウィンドウでチャットします（選択範囲を渡せます）", "在任务窗格中聊天（可附带选中区域）", "Chat in a task pane (can attach the selection)");
            K("rb.process.tip", "選択したセルを 1 件ずつ AI で処理して書き込みます", "逐个用 AI 处理选中的单元格并写入结果", "Process each selected cell with AI and write the results");
            K("rb.templates.tip", "プロンプトのテンプレート（JSON）を開きます", "打开提示词模板（JSON）", "Open the prompt templates (JSON)");
            K("rb.usage.tip", "今月の使用量と費用の概算", "本月用量和费用估算", "This month's usage and estimated cost");

            // ---- 設定 ----
            K("set.title", "AI アシスタントの設定", "AI 助手设置", "AI Assistant settings");
            K("set.apikey", "API キー", "API 密钥", "API key");
            K("set.apikey.hint", "{0} の自分のキー（取得先：{1}）。サービスごとに別々に、この PC のあなたのアカウントでだけ読める形で暗号化して保存します。", "您自己的 {0} 密钥（获取地址：{1}）。按服务分别保存，以仅当前 Windows 用户可读取的方式加密。", "Your own {0} key (get it at: {1}). Kept separately per service, encrypted so only your Windows account on this PC can read it.");
            K("set.provider", "AI サービス", "AI 服务", "AI service");
            K("set.provider.hint", "DeepSeek を中心に、OpenAI 互換のサービスと Claude を、自分のキーで使えます。", "以 DeepSeek 为主，也可用自己的密钥使用兼容 OpenAI 接口的服务和 Claude。", "DeepSeek first; any OpenAI-compatible service and Claude also work with your own key.");
            K("set.model.hint", "一覧にない名前も直接入力できます。", "列表中没有的模型名也可以直接输入。", "You can type a model name that is not in the list.");
            K("set.show", "表示", "显示", "Show");
            K("set.test", "接続テスト", "连接测试", "Test connection");
            K("set.testing", "テスト中…", "测试中…", "Testing…");
            K("set.test.ok", "接続できました（{0}）。", "连接成功（{0}）。", "Connected ({0}).");
            K("set.model", "モデル", "模型", "Model");
            K("set.effort", "考える深さ", "思考深度", "Effort");
            K("set.effort.hint", "low：速く安い（セルの処理向き）／ high・max：難しい作業向き", "low：快且便宜（适合单元格处理）／ high・max：适合困难任务", "low: fast and cheap (good for cells) / high, max: for hard tasks");
            K("set.maxtokens", "最大トークン数", "最大 token 数", "Max tokens");
            K("set.concurrency", "同時に呼ぶ数", "并发调用数", "Concurrent calls");
            K("set.timeout", "タイムアウト（秒）", "超时（秒）", "Timeout (sec)");
            K("set.cache", "結果をキャッシュする（同じ入力は再送しない）", "缓存结果（相同输入不重复发送）", "Cache results (do not resend the same input)");
            K("set.cachedays", "キャッシュの保存日数", "缓存保留天数", "Cache days");
            K("set.clearcache", "キャッシュを消す", "清除缓存", "Clear cache");
            K("set.cache.cleared", "キャッシュを消しました。", "已清除缓存。", "Cache cleared.");
            K("set.cellchars", "1 件あたりの最大文字数", "每条最大字符数", "Max characters per item");
            K("set.language", "表示言語", "显示语言", "Display language");
            K("set.baseurl", "接続先 URL（上級）", "连接地址 URL（高级）", "Base URL (advanced)");
            K("set.baseurl.hint", "空のままで既定（{0}）。社内ゲートウェイ・プロキシ、または Custom の場合に入力します。", "留空则使用默认地址（{0}）。仅在使用公司网关/代理或 Custom 时填写。", "Leave empty for the default ({0}). Only for a company gateway/proxy or Custom.");
            K("set.system", "既定のシステムプロンプト", "默认系统提示词", "Default system prompt");
            K("set.confirmbatch", "一括処理の前に件数と費用の概算を確認する", "批量处理前确认数量和费用估算", "Confirm count and estimated cost before batch runs");
            K("set.consent", "データ送信に同意する", "同意数据发送", "I agree to the data being sent");
            K("set.saved", "設定を保存しました。", "设置已保存。", "Settings saved.");
            K("lang.auto", "自動（Windows に従う）", "自动（跟随 Windows）", "Auto (follow Windows)");
            K("model.dsflash", "高速・低価格（既定）", "快速、低价（默认）", "fast and cheap (default)");
            K("model.dspro", "高精度（難しい作業向き）", "高精度（适合困难任务）", "most accurate (harder tasks)");
            K("model.other", "料金はサービスの案内を確認", "价格请查看服务商说明", "see the provider for prices");
            K("rb.ctx", "AI で処理…", "用 AI 处理…", "Process with AI…");
            K("rb.ctx.tip", "選択したセルを AI で処理します", "用 AI 处理所选单元格", "Process the selected cells with AI");
            K("model.opus", "高精度・既定", "高精度・默认", "most accurate · default");
            K("model.sonnet", "速さと精度のバランス", "速度与精度平衡", "balanced");
            K("model.haiku", "最も安く速い（大量処理向き）", "最便宜最快（适合大批量）", "cheapest and fastest (bulk work)");
            K("model.fable", "最高性能（高価）", "最高性能（较贵）", "most capable (expensive)");

            // ---- 一括処理 ----
            K("proc.title", "選択範囲を AI で処理", "用 AI 处理选中区域", "Process selection with AI");
            K("proc.template", "テンプレート", "模板", "Template");
            K("proc.prompt", "指示（プロンプト）", "指令（提示词）", "Instruction (prompt)");
            K("proc.system", "システムプロンプト（任意）", "系统提示词（可选）", "System prompt (optional)");
            K("proc.placeholders", "差し込み：{{value}} = セルの値 / {{A}} {{B}}… = 同じ行のその列 / {{row}} = 行番号", "可用占位符：{{value}} = 单元格值 / {{A}} {{B}}… = 同一行的该列 / {{row}} = 行号", "Placeholders: {{value}} = cell value / {{A}} {{B}}… = that column in the same row / {{row}} = row number");
            K("proc.range", "対象の範囲", "处理范围", "Range");
            K("proc.unit", "処理の単位", "处理单位", "Unit");
            K("proc.unit.cell", "セル 1 つずつ", "逐个单元格", "Each cell");
            K("proc.unit.row", "行ごと（{{A}} などで列を使う）", "按行（用 {{A}} 等引用列）", "Each row (use {{A}} etc.)");
            K("proc.output", "結果の書き込み先", "结果写入位置", "Output");
            K("proc.out.right", "選択範囲の右隣", "选中区域右侧", "Right of the selection");
            K("proc.out.column", "指定した列", "指定列", "A specified column");
            K("proc.out.replace", "元のセルを置き換える", "替换原单元格", "Replace the original cells");
            K("proc.skipempty", "空のセルは処理しない", "跳过空单元格", "Skip empty cells");
            K("proc.skipdone", "結果がすでにあるセルは処理しない（途中から再開）", "跳过已有结果的单元格（断点续跑）", "Skip rows that already have a result (resume)");
            K("proc.concurrency", "同時に呼ぶ数", "并发调用数", "Concurrent calls");
            K("proc.estimate", "対象 {0} 件・費用の概算 約 ${1}（{2}）", "共 {0} 条 · 费用估算约 ${1}（{2}）", "{0} items · estimated cost about ${1} ({2})");
            K("proc.estimate.note", "概算です。実際は結果の長さなどで変わります。", "仅为估算，实际会因结果长度等而变化。", "Rough estimate; the real cost varies with output length.");
            K("proc.start", "実行", "开始", "Run");
            K("proc.stop", "中止", "停止", "Stop");
            K("proc.progress", "{0} / {1} 件（失敗 {2}）", "{0} / {1} 条（失败 {2}）", "{0} / {1} done ({2} failed)");
            K("proc.done", "完了：{0} 件成功、{1} 件失敗、使用 約 ${2}", "完成：成功 {0} 条，失败 {1} 条，约花费 ${2}", "Done: {0} succeeded, {1} failed, about ${2} used");
            K("proc.noselection", "処理するセルを選んでください。", "请先选择要处理的单元格。", "Select the cells to process.");
            K("proc.toomany", "対象が多すぎます（{0} 件）。上限は {1} 件です。範囲を分けてください。", "对象过多（{0} 条）。上限为 {1} 条，请分批处理。", "Too many items ({0}). The limit is {1}. Split the range.");
            K("proc.confirm", "{0} 件を AI に送信します。\r\n費用の概算は約 ${1} です。\r\n実行しますか？", "将向 AI 发送 {0} 条。\r\n费用估算约 ${1}。\r\n是否执行？", "{0} items will be sent to the AI.\r\nEstimated cost is about ${1}.\r\nRun?");
            K("proc.replace.warn", "元のセルを置き換えます。元に戻せません。実行しますか？", "将替换原单元格，无法撤销。是否执行？", "This replaces the original cells and cannot be undone. Continue?");
            K("proc.column.invalid", "列の指定が正しくありません（例：C）。", "列指定无效（例如：C）。", "Invalid column (e.g. C).");
            K("proc.prompt.empty", "指示を入力してください。", "请输入指令。", "Enter an instruction.");
            K("proc.writefail", "セルに書き込めませんでした（セルの編集中ではありませんか？）。", "无法写入单元格（是否正在编辑单元格？）。", "Could not write to the cell (is a cell being edited?).");

            // ---- チャット ----
            K("chat.title", "AI チャット", "AI 聊天", "AI Chat");
            K("chat.send", "送信 (Ctrl+Enter)", "发送 (Ctrl+Enter)", "Send (Ctrl+Enter)");
            K("chat.stop", "停止", "停止", "Stop");
            K("chat.clear", "クリア", "清空", "Clear");
            K("chat.attach", "選択範囲を添付", "附带选中区域", "Attach selection");
            K("chat.insert", "セルに入れる", "写入单元格", "Insert into cell");
            K("chat.copy", "コピー", "复制", "Copy");
            K("chat.you", "あなた", "你", "You");
            K("chat.ai", "AI", "AI", "AI");
            K("chat.hint", "下の欄に入力して Ctrl+Enter で送信します。「選択範囲を添付」にチェックがあると、選んでいるセルの内容も一緒に送ります。", "在下方输入后按 Ctrl+Enter 发送。勾选“附带选中区域”时，会一并发送所选单元格的内容。", "Type below and press Ctrl+Enter to send. With \"Attach selection\" checked, the selected cells are sent along.");
            K("chat.system", "You are a helpful assistant working inside Microsoft Excel. The user may attach cells copied from their worksheet (tab-separated). Be concise. When the user asks for values to put into cells, give them in plain text, one value per line unless told otherwise.", "You are a helpful assistant working inside Microsoft Excel. The user may attach cells copied from their worksheet (tab-separated). Be concise. When the user asks for values to put into cells, give them in plain text, one value per line unless told otherwise.", "You are a helpful assistant working inside Microsoft Excel. The user may attach cells copied from their worksheet (tab-separated). Be concise. When the user asks for values to put into cells, give them in plain text, one value per line unless told otherwise.");
            K("chat.attached", "（選択範囲 {0} を添付）", "（已附带选中区域 {0}）", "(attached selection {0})");
            K("chat.nothingtoinsert", "入れる回答がまだありません。", "还没有可写入的回答。", "There is no answer to insert yet.");

            // ---- 使用量 ----
            K("usage.title", "使用量", "用量", "Usage");
            K("usage.body", "今月（{0}）\r\n  呼び出し：{1} 回\r\n  入力：{2} トークン\r\n  出力：{3} トークン\r\n  キャッシュで省いた呼び出し：{4} 回\r\n  費用の概算：約 ${5}\r\n\r\n※ 概算です。正確な請求額は各サービスの管理画面で確認してください（料金が不明なモデルは 0）。",
                "本月（{0}）\r\n  调用：{1} 次\r\n  输入：{2} token\r\n  输出：{3} token\r\n  缓存命中节省的调用：{4} 次\r\n  费用估算：约 ${5}\r\n\r\n※ 仅为估算，准确账单请在各服务的控制台查看（价格未知的模型按 0 计）。",
                "This month ({0})\r\n  Calls: {1}\r\n  Input: {2} tokens\r\n  Output: {3} tokens\r\n  Calls saved by cache: {4}\r\n  Estimated cost: about ${5}\r\n\r\n* Rough estimate. Check the provider's console for the exact bill (models with unknown prices count as 0).");

            // ---- テンプレート名 ----
            K("tpl.translate_en", "翻訳（英語へ）", "翻译（译成英文）", "Translate to English");
            K("tpl.translate_ja", "翻訳（日本語へ）", "翻译（译成日文）", "Translate to Japanese");
            K("tpl.translate_zh", "翻訳（中国語へ）", "翻译（译成中文）", "Translate to Chinese");
            K("tpl.summarize", "要約", "摘要", "Summarize");
            K("tpl.proofread", "校正（誤字・文法）", "校对（错别字・语法）", "Proofread");
            K("tpl.classify", "分類（カテゴリを指定）", "分类（指定类别）", "Classify");
            K("tpl.extract", "抽出（項目を指定）", "提取（指定项目）", "Extract");
            K("tpl.sentiment", "感情分析", "情感分析", "Sentiment");
            K("tpl.normalize_address", "住所の整形", "地址规范化", "Normalize address");
            K("tpl.custom", "（自由入力）", "（自定义）", "(custom)");
            K("tpl.opened", "テンプレートのファイルを開きました。保存後、次に開く画面から使えます。", "已打开模板文件。保存后在下次打开的窗口中生效。", "Opened the templates file. Saved changes apply the next time a window opens.");

            // ---- ヘルプ ----
            K("help.text",
                "【使い方】\r\n1. [AI] タブの「設定」で API キーを入力し、データ送信に同意します。\r\n2. セルに  =AI(\"指示\", A2)  と入力すると結果が返ります。\r\n"
                + "   =AI_TRANSLATE(A2,\"English\")  =AI_SUMMARIZE(A2)  =AI_CLASSIFY(A2,\"問い合わせ,苦情,その他\")  =AI_EXTRACT(A2,\"電話番号\")\r\n"
                + "3. 「選択範囲を処理」で、選んだセルを 1 件ずつ処理して書き込みます（途中から再開できます。セルの右クリックメニュー「AI で処理…」からも開けます）。\r\n4. 「チャット」で作業ウィンドウから相談できます。\r\n\r\n"
                + "【ご注意】数式の AI は再計算のたびに呼ばれるため、同じ入力は自動でキャッシュします（設定で切り替え）。大量のセルには「選択範囲を処理」を使うと費用を確認できます。",
                "【用法】\r\n1. 在 [AI] 选项卡的“设置”中输入 API 密钥并同意数据发送。\r\n2. 在单元格输入  =AI(\"指令\", A2)  即可返回结果。\r\n"
                + "   =AI_TRANSLATE(A2,\"English\")  =AI_SUMMARIZE(A2)  =AI_CLASSIFY(A2,\"咨询,投诉,其他\")  =AI_EXTRACT(A2,\"电话号码\")\r\n"
                + "3. 用“处理选中区域”逐个处理选中的单元格并写入结果（可断点续跑；也可在单元格右键菜单“用 AI 处理…”中打开）。\r\n4. 用“聊天”在任务窗格中咨询。\r\n\r\n"
                + "【注意】公式中的 AI 每次重算都会被调用，因此相同输入会自动缓存（可在设置中开关）。大量单元格请使用“处理选中区域”，可先确认费用。",
                "[How to use]\r\n1. In [AI] tab > Settings, enter your API key and accept the data notice.\r\n2. Type  =AI(\"instruction\", A2)  in a cell to get the result.\r\n"
                + "   =AI_TRANSLATE(A2,\"English\")  =AI_SUMMARIZE(A2)  =AI_CLASSIFY(A2,\"inquiry,complaint,other\")  =AI_EXTRACT(A2,\"phone number\")\r\n"
                + "3. \"Process selection\" runs the selected cells one by one and writes the results (can resume; also in the cell right-click menu: \"Process with AI…\").\r\n4. \"Chat\" opens a task pane.\r\n\r\n"
                + "[Notes] Formulas recalculate often, so identical inputs are cached automatically (toggle in Settings). For many cells use \"Process selection\" to see the cost first.");
            K("udf.working", "⏳ 処理中…", "⏳ 处理中…", "⏳ working…");
            K("proc.singlearea", "1 つの連続した範囲を選んでください（複数の範囲は処理できません）。", "请选择一个连续的区域（不支持多个区域）。", "Select a single contiguous range (multiple areas are not supported).");
            K("udf.version", "Excel AI アシスタント {0}", "Excel AI 助手 {0}", "Excel AI Assistant {0}");
        }
    }
}
