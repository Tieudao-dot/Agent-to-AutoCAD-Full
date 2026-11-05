# Ghi chú phát triển và quyết định thiết kế

## Build trên Linux (cloud) vs Windows

- Sidecar build và test được trên mọi OS (`npm run build`, `npm test`).
- Plugin C# target .NET Framework 4.8 build được trên Linux bằng .NET SDK 8 nhờ `Microsoft.NETFramework.ReferenceAssemblies.net48` + package NuGet `AutoCAD.NET 24.1.51000` (reference assemblies của AutoCAD 2022). Điều này chỉ kiểm chứng **biên dịch**: chạy thật cần Windows + AutoCAD 2022.
- UI viết bằng WPF thuần code (không XAML) để không cần markup compiler — build được ở mọi nơi; trên Windows dùng Visual Studio 2022 bình thường.

## Luồng thread trong plugin

- Pipe đọc/ghi async trên thread pool (`IpcServer`), không chặn UI.
- Tool call → `ToolDispatcher.HandleAsync` → `MainThread.InvokeAsync`: `BeginInvoke` lên một `Control` ẩn tạo trên main thread của AutoCAD, rồi vào application context bằng `DocumentManager.ExecuteInApplicationContext` (nếu chưa ở đó).
- Tool ghi: `DocumentLock` + một `Transaction` (của `Document.TransactionManager`); tool đọc: transaction không khóa document rồi `Abort`.
- Mọi exception trong handler bị bắt, transaction bị dispose không commit (= rollback), trả error envelope. Không exception nào thoát ra AutoCAD.
- `batch_execute`: một top transaction, mỗi call con một nested transaction — call lỗi chỉ rollback phần của nó, call sau vẫn chạy (spec §7.8). Lỗi hệ thống (`FatalToolException`: mất bản vẽ...) dừng cả batch.

## Undo theo lượt chat

- `UndoScope` dùng COM `AcadDocument.StartUndoMark()` / `EndUndoMark()` (tương đương `UNDO BEGIN/END`), gọi qua `dynamic` từ `Document.GetAcadDocument()` — không cần interop assembly.
- Group được mở **lười**: chỉ ngay trước tool ghi đầu tiên của lượt. Lượt chỉ đọc không tạo group rỗng (tránh U "hoàn tác hụt").
- Group đóng khi `turn_end`, Stop, mất kết nối, hay chuyển bản vẽ. Mỗi tool call vẫn có `DocumentLock` riêng; AutoCAD lồng các bản ghi undo đó vào group đang mở → **một U hoàn tác cả lượt** (cần kiểm chứng trên AutoCAD thật — kịch bản 8).

## Bản vẽ bị đóng / đổi giữa lượt

- Lượt gắn với document đang active lúc gửi chat. `DocumentToBeDestroyed` / `DocumentActivated` sang bản vẽ khác → plugin gửi `cancel`, đánh dấu lượt không hợp lệ; mọi tool call sau đó trả `ACAD_ERROR` dặn Claude dừng.

## Sidecar và Agent SDK

- `@anthropic-ai/claude-agent-sdk` 0.3.x (Claude Code 2.1.x), API được tra trực tiếp từ `sdk.d.ts` của package đã cài.
- Một `query()` với **streaming input** (`AsyncIterable<SDKUserMessage>`) cho mỗi cửa sổ chat → giữ ngữ cảnh nhiều lượt. New → `close()` query, phiên mới khi có message tiếp. Tiến trình Claude chết bất ngờ → lượt kết thúc có lỗi, message sau `resume` lại đúng session id.
- Tool: in-process MCP server `createSdkMcpServer({name: "claudecad"})` + `tool()` với schema zod; thân tool chỉ forward qua pipe và chờ kết quả.
- Quyền: `tools: ["Read", "Skill"]` (tắt Bash, Write, Web...), `disallowedTools` liệt kê thêm, `allowedTools: ["mcp__claudecad", "Read", "Skill"]`, `permissionMode: "dontAsk"` + `permissionPrompts: "none"` → bộ tool ClaudeCAD chạy không hỏi, mọi thứ khác bị từ chối. Xác nhận thao tác nguy hiểm nằm ở system prompt (spec §9).
- Cách ly: `settingSources: []`, `strictMcpConfig: true` — không đọc `~/.claude/settings.json`, CLAUDE.md hay `.mcp.json` của người dùng. Đăng nhập OAuth (`claude login`) hoặc `ANTHROPIC_API_KEY` vẫn dùng bình thường.
- Stream: `includePartialMessages: true`; `content_block_delta/text_delta` → `agent_text`; `result` → `turn_end`.
- Hủy: `Query.interrupt()`; nếu SDK không trả `result` trong 3 s thì sidecar tự phát `turn_end {cancelled}`; plugin cũng tự mở khóa sau 10 s.
- Model mặc định `claude-opus-5-5`, dự phòng `claude-sonnet-5-5` (đổi bằng `CLAUDECAD_MODEL`).

## Skill

- SDK load skill từ plugin directory (`<plugin>/skills/<name>/SKILL.md`). Mỗi lần bắt đầu phiên, sidecar copy thư mục `skills/` của bundle vào `%LocalAppData%\ClaudeCAD\workspace\claudecad-skills\skills` và tạo `.claude-plugin/plugin.json`, rồi truyền `plugins: [{type: "local", path}]`, `skills: "all"`. Thêm/sửa skill = sửa thư mục `skills/` + bấm New. Không cần symlink/junction (không cần quyền admin).

## JSON trong plugin

- Tự viết `JsonCodec` (parser/serializer nhỏ) thay vì Newtonsoft.Json: AutoCAD và các plugin khác nạp bản Newtonsoft riêng → dễ xung đột phiên bản trong acad.exe. Có unit test (`plugin/ClaudeCAD.Core.Tests`).
