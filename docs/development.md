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

## Chốt xác nhận thao tác phá hủy

- Tầng 1 (spec §9): system prompt bắt Claude báo số lượng + hỏi trước khi xóa theo điều kiện lọc hoặc sửa > 20 đối tượng.
- Tầng 2 (thêm, `agent/src/agent/confirmation.ts`): trước khi forward, sidecar đếm số đối tượng có sẵn mà call sẽ sửa/xóa (cộng dồn trong `batch_execute`). Nếu > 20 và tin nhắn của người dùng ở lượt hiện tại không phải câu xác nhận ngắn ("đồng ý", "ok", "có, xóa đi"...), call bị trả lỗi `INVALID_PARAMS` "Confirmation required..." mà không chạm tới bản vẽ.
- Tầng 3 (spec §10): plugin từ chối `erase_entities` > 1000 handle (`TOO_MANY_ENTITIES`).

## Fillet / chamfer

- v1 hỗ trợ hai LINE (đúng như lệnh FILLET/CHAMFER với hai đường thẳng): mỗi line giữ đầu xa góc hơn. Toán học nằm trong `Core/Geometry.cs` (có unit test). Polyline: explode trước hoặc vẽ lại bằng bulge.

## Rà soát độc lập (sau phase 3)

Một lượt review riêng phần plugin C# (không chạy được AutoCAD nên đọc theo API doc) đã tìm và sửa:

1. Tool call đến trễ sau khi lượt kết thúc (Stop sau 10 s, timeout 30 s, mất kết nối) có thể mở undo group mới không bao giờ đóng → nay `ResolveDocument` từ chối mọi call khi không có lượt đang chạy.
2. `Acad.Add` gọi lại `SetDatabaseDefaults` làm mất style/chiều cao đã đặt (MText, MLeader, Table, Wipeout, DBText) → thêm `defaults: false`.
3. Không regen sau khi đóng băng/tắt layer trong batch → cờ `NeedsRegen` cộng dồn, batch không xóa cờ.
4. `batch_execute` chỉ đọc vẫn khóa document và mở undo group rỗng → dispatcher quyết định theo các call con.
5. Góc của dynamic block đọc ra radian nhưng ghi vào bằng độ → đọc ra cũng đổi sang độ.
6. Kiểm tra layer khóa sai layer cho offset/fillet/chamfer/join/dim nối tiếp → copy thuộc tính nguồn trước khi thêm.
7. Explode / copy đưa đối tượng ở paper space về model space → giữ nguyên không gian nguồn.
8. Handler nút bấm trên palette có thể ném exception vào AutoCAD → bọc try/catch.
9. Bản sao MText tạm (MLeader.MText, MTextAttribute) không được dispose → `using`.

Thêm: `ping` chạy được cả khi chưa mở bản vẽ; giới hạn 3 lần tự khởi động lại sidecar không còn bị reset khi gửi tin nhắn.

## Rà soát độc lập lần 2 (khung chat v0.4.0)

Review riêng phần palette mới tìm và đã sửa 17 lỗi, các lỗi chính:

1. Đổi model / effort / theme trong ⚙ không được áp dụng (palette và controller dùng chung một đối tượng cài đặt) → palette làm việc trên bản sao; "Mặc định" cũng được gửi (`"default"`).
2. Dán từ Excel bị biến thành ảnh (Excel đặt kèm bitmap vào clipboard) và Ctrl+V có thể ném `COMException` → có chữ thì ưu tiên chữ; bọc try/catch; ảnh chụp màn hình bỏ kênh alpha.
3. Lệnh U gửi từ palette có thể rơi vào lệnh đang chạy → từ chối khi AutoCAD đang có lệnh/prompt, chuyển focus vào bản vẽ; "Sửa và gửi lại" không còn tự gửi nếu U không chạy.
4. "Hoàn tác lượt này" còn hiện sau thay đổi khác → theo dõi thêm `CommandCancelled`/`CommandFailed`, thay đổi database ngoài lượt, lệnh chạy giữa lượt; zoom của palette tắt ghi undo (`Database.DisableUndoRecording`).
5. Cập nhật UI cuối lượt có thể vượt trước text đang stream (hai hàng đợi khác nhau) → kết thúc lượt đi cùng hàng đợi Dispatcher.
6. New chat / đổi bản vẽ giữa lượt làm câu trả lời cũ lọt vào chat mới → bộ đếm thế hệ.
7. Lịch sử bị sửa/lưu từ thread IPC; lưu file có thể làm mất lịch sử → mọi thay đổi trên thread UI, lưu bằng file tạm riêng + `File.Replace` có khóa.
8. Thẻ xác nhận tách đôi câu trả lời → thẻ nằm trong bong bóng của lượt.
9. Popup (⚙, mẫu lệnh `/`) không nhận theme → đưa vào cây logic.
10–17. Cuộn chuột trên tin nhắn người dùng, câu trả lời xác nhận gửi nhầm bản vẽ, sidecar giữ lượt cũ làm palette kẹt (nay bỏ lượt cũ và nhận tin mới), điểm gốc rubber-band sai hệ tọa độ, chọn rất nhiều đối tượng làm treo UI, focus sau khi chọn, đọc text không khóa.
