Excel AI アシスタント / Excel AI 助手 / Excel AI Assistant   v{VERSION}
==========================================================================

【日本語】
Excel の中で AI（DeepSeek が中心。OpenAI 互換のサービスと Claude も使えます）を使うアドインです。数式（=AI(...)）、選択範囲の一括処理、作業ウィンドウのチャットが使えます。
動作環境: Windows 10/11 の Excel 2019 以上のデスクトップ版（32 ビット・64 ビットのどちらも可）。.NET Framework 4.8（Windows に標準で入っています）。
API キーは各自で用意してください（DeepSeek は https://platform.deepseek.com/api_keys で作成。料金は API キーの契約に請求されます）。

  インストール（管理者の権限は不要）
    1. zip を展開し、フォルダー全体を好きな場所に置く（ネットワーク上・ダウンロードのまま実行しない）。
    2. Excel をすべて閉じて、Install.cmd をダブルクリック。
    3. Excel を起動し、[AI] タブ →「設定」で「AI サービス」（既定は DeepSeek）を選び、API キーを入力 →「接続テスト」→ データ送信に同意して「OK」。
       サービスを切り替えても、キーはサービスごとに別々に保存されます。モデル名は一覧にないものも直接入力できます。
  外すとき: Uninstall.cmd をダブルクリック（設定・キャッシュ・ログは残ります。全部消す場合は Uninstall.ps1 -RemoveData）。

  使い方
    ・セルに  =AI("指示", A2)    =AI_TRANSLATE(A2,"English")    =AI_SUMMARIZE(A2)
              =AI_CLASSIFY(A2,"問い合わせ,苦情,その他")    =AI_EXTRACT(A2,"電話番号")
      結果が返るまでは「⏳ 処理中…」と出ます。同じ入力は結果を覚えて、再計算で再送しません（費用を抑えます）。
    ・[AI] タブの「選択範囲を処理」（セルの右クリックメニュー「AI で処理…」からも開けます）: セルを 1 件ずつ処理して右隣などに書き込みます。費用の概算を確認でき、途中から再開できます。
      プロンプトでは {{value}}（セルの値）、{{A}} {{B}}…（同じ行のその列）、{{row}}（行番号）が使えます。
    ・「チャット」: 右側の作業ウィンドウで相談できます（選択範囲を添付できます）。
    ・「テンプレート」: プロンプトのテンプレート（JSON）を編集できます。「使用量」: 今月の呼び出しと費用の概算。

  ご注意（データと費用）
    ・処理するセルの内容と指示は、選んだ AI サービス（既定は DeepSeek）に、あなたの API キーで送信されます。機密情報・個人情報は選ばないでください。
    ・「選択範囲を処理」は実行前に件数と費用の概算を表示します。数式の =AI() は、列を下にコピーすると件数分だけ呼び出されます。
    ・API キーはこの PC のあなたの Windows アカウントでだけ読める形（DPAPI）で %APPDATA%\ExcelAi\settings.json に保存します。

  困ったとき
    ・[AI] タブが出ない: Excel を閉じて Install.cmd をもう一度。それでも出ない場合は [ファイル]→[オプション]→[トラスト センター]→[トラスト センターの設定]→[アドイン]
      で「署名されていないアドインを許可」の設定を確認、または [開発]→[アドイン] に ExcelAi が無効（無効化されたアイテム）として入っていないか確認。
    ・「ブロックされています」: zip を展開する前に zip のプロパティで「許可する」にチェック。または PowerShell で  Unblock-File *.xll
    ・32/64 ビットが合わない: Install.cmd は自動で判定します。判定できない場合は  powershell -File Install.ps1 -Bitness 64
    ・会社のプロキシ・ゲートウェイを使う: 設定の「接続先 URL」。通信できない・時間切れは、ネットワークの管理者に api.deepseek.com（使うサービスの接続先）への接続を確認してください。
    ・「残高が足りません」: サービスの管理画面でチャージを確認してください（DeepSeek は前払い）。
    ・費用の概算は、一覧にあるモデルだけ表示します（一覧にないモデルは「?」）。
    ・ログ: %APPDATA%\ExcelAi\logs（API キーとセルの内容は書きません）。

【中文】
在 Excel 中使用 AI 的加载项（以 DeepSeek 为主，也支持兼容 OpenAI 接口的服务和 Claude）：公式 =AI(...)、批量处理选中区域、任务窗格聊天。
运行环境：Windows 10/11 的 Excel 2019 及以上桌面版（32 位、64 位均可），.NET Framework 4.8（Windows 自带）。
API 密钥需自行准备（DeepSeek 在 https://platform.deepseek.com/api_keys 创建，费用计入该密钥所属账户）。

  安装（无需管理员权限）：1) 解压 zip，把整个文件夹放在本地；2) 关闭所有 Excel，双击 Install.cmd；
  3) 启动 Excel，在 [AI] 选项卡 →“设置”选择“AI 服务”（默认 DeepSeek）并输入 API 密钥 →“连接测试”→ 同意数据发送 →“确定”。各服务的密钥分别保存；模型名可直接输入列表以外的。
  卸载：双击 Uninstall.cmd（保留设置、缓存、日志；全部删除请用 Uninstall.ps1 -RemoveData）。
  用法：=AI("指令",A2)  =AI_TRANSLATE(A2,"English")  =AI_SUMMARIZE(A2)  =AI_CLASSIFY(A2,"咨询,投诉,其他")  =AI_EXTRACT(A2,"电话号码")。
  “处理选中区域”（也可从单元格右键菜单“用 AI 处理…”打开）可逐个处理单元格并写入结果，执行前显示数量和费用估算，可断点续跑；提示词里可用 {{value}}、{{A}} {{B}}…、{{row}}。
  注意：单元格内容和指令会用您的 API 密钥发送到 Anthropic，请勿选择机密或个人信息。API 密钥以仅当前 Windows 用户可读的方式（DPAPI）加密保存。
  问题：看不到 [AI] 选项卡 → 关闭 Excel 后重新运行 Install.cmd，并检查信任中心的加载项设置；提示“已被阻止” → 解压前在 zip 属性中勾选“解除锁定”，或执行 Unblock-File *.xll。

[English]
An add-in that brings AI into Excel (DeepSeek first; any OpenAI-compatible service and Claude also work): the =AI(...) formulas, batch processing of a selection, and a chat task pane.
Requires: Excel 2019 or later (desktop, 32-bit or 64-bit) on Windows 10/11, with .NET Framework 4.8 (built into Windows).
Bring your own API key (for DeepSeek: https://platform.deepseek.com/api_keys; usage is billed to that account).

  Install (no admin rights needed): 1) unzip and keep the whole folder on a local drive; 2) close all Excel windows and double-click Install.cmd;
  3) start Excel, open [AI] tab > Settings, pick the AI service (DeepSeek by default), enter the API key, click "Test connection", accept the data notice and press OK. Keys are stored per service; you can type a model name that is not in the list.
  Uninstall: double-click Uninstall.cmd (settings/cache/logs are kept; use Uninstall.ps1 -RemoveData to delete everything).
  Usage: =AI("instruction",A2)  =AI_TRANSLATE(A2,"English")  =AI_SUMMARIZE(A2)  =AI_CLASSIFY(A2,"inquiry,complaint,other")  =AI_EXTRACT(A2,"phone number").
  "Process selection" (also in the cell right-click menu: "Process with AI…") runs cells one by one and writes results; it shows the item count and estimated cost first and can resume. Placeholders: {{value}}, {{A}} {{B}}…, {{row}}.
  Note: cell content and instructions are sent to Anthropic with your API key. Do not select confidential or personal data. The key is stored encrypted (DPAPI) for your Windows account only.
  Troubleshooting: no [AI] tab -> close Excel and run Install.cmd again, and check the Trust Center add-in settings; "blocked" -> tick "Unblock" in the zip's properties before extracting, or run Unblock-File *.xll.

(c) 2026 株式会社ナッセンシステム  https://nassen.co.jp  contact@nassen.co.jp
Open source under the MIT License (see LICENSE.txt). / オープンソース（MIT ライセンス）/ 开源（MIT 许可证）
Third-party components: see THIRD-PARTY-NOTICES.txt
