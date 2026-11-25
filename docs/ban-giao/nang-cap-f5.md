# Bàn giao: Nâng cấp đợt F5 — ba việc nhỏ còn sót của đợt F

Nhánh `full/nang-cap-f5`, tách từ `full/nang-cap-f-tong-the` @ `afdaa68`. Người viết; phần rà lại do thread khác. Chưa thử
trên AutoCAD / Claude thật. Phần chỉ kiểm được trên AutoCAD: bước tự kiểm `CLAUDECHATSELFTEST` mới ("Nâng cấp đợt F5 …",
**chưa chạy**) và `docs/checklist-dot-f5.md`.

## 1. Ô "Nạp gọn lệnh" trong ⚙

- ⚙ → "CÁCH CLAUDE LÀM VIỆC" → ô **Nạp gọn lệnh**, mặc định bật, nhớ theo máy (`settings.json` `defer_tools`). Dòng giải
  thích: "Bật: mở chat nhanh và nhẹ hơn, Claude tự tìm lệnh ít dùng khi cần. Tắt: nạp đủ mô tả mọi lệnh như trước (tốn token
  hơn). Áp dụng từ cuộc chat mới (bấm Chat mới); cuộc chat đã có tin nhắn giữ cách nạp lúc bắt đầu."
- Cách chạy: mỗi tin (và tin khởi động sẵn) mang `defer_tools: false` khi cách nạp **của cuộc chat** là tắt.
  - **Sửa ở bản chốt đợt F (25/11, sau rà F5):** cuộc chat đã có tin nhắn **giữ nguyên cách nạp tới khi bấm "Chat mới"**
    (cách nạp lưu trong file chat của bản vẽ, `ChatTranscript.DeferTools`, `ChatDeferLogic`). Lý do: nếu Claude đã dùng
    ToolSearch trong cuộc chat rồi khách tắt ô, lượt sau nối tiếp cuộc chat mà không còn ToolSearch, lịch sử còn các khối
    tham chiếu lệnh nên có thể bị từ chối. Ô đổi vẫn được lưu và áp dụng cho cuộc chat mới.
  - Cuộc chat chưa có tin nào (mới khởi động sẵn): áp dụng ngay, khởi động sẵn lại cho khớp.
  - Đổi bản vẽ: mỗi cuộc chat dùng cách nạp của chính nó (trợ lý khởi động lại, nối tiếp, khi hai cuộc khác nhau). File
    chat lưu trước bản này mà đã có cuộc chat: coi là bật (trước F5 mọi cuộc chat đều nạp gọn).
- Biến môi trường `CLAUDECAD_DEFER_TOOLS=0` / file `tool-search-off` (Test & sửa F4) **thắng ô này**: khi có, mọi lệnh nạp
  đủ dù ô bật, và ô không làm khởi động lại. Công tắc thắng **cả cách nạp của cuộc chat đang dở** (bản chốt đợt F không đổi
  điều này): bật / tắt công tắc rồi mở lại AutoCAD thì nên bấm **Chat mới**; nối tiếp cuộc chat cũ có lỗi không thì cần Claude
  thật mới biết (checklist tổng F9-2).
- Code: `PaletteSettings.DeferTools`, `UI/ChatControl.cs` (`_deferTools`), `ChatController.cs` (`PlanContext`,
  `OnSettingsChanged`), `agent/src/ipc/protocol.ts` (`defer_tools`), `agent/src/agent/session.ts` (`applyPaletteDefer`).

## 2. Thẻ xác nhận cho đồng bộ

| Việc | Trước | Sau | "Mã" trên thẻ |
| --- | --- | --- | --- |
| Xuất PDF (`plot_pdf`) vào file đã có | ghi đè không hỏi | thẻ Đồng ý / Hủy | mã của file (từ đường dẫn) |
| Xuất STL, ảnh PNG (`named_view image`), Excel (4 bảng) vào file đã có | Claude hỏi trong chat rồi `overwrite` / `replace` | thẻ | mã của file |
| Ghi đè / xóa góc nhìn đã lưu (`named_view save` trùng tên, `delete`) | `replace` / không hỏi | thẻ | handle của góc nhìn |
| Gán vật liệu cho cả layer (`set_material layers`) | không hỏi | thẻ | handle của từng layer |
| Đổi màu / độ trong của vật liệu **đã có** trong bản vẽ (`set_material` + `color` / `opacity`; mọi khối / layer dùng nó đổi theo; kiểm tra độc lập đợt F) | không hỏi (chỉ Global bị chặn) | thẻ | handle của vật liệu; tạo vật liệu mới thì không hỏi |

- Cách chạy (như style / trạng thái layer của F2): plugin từ chối, không đổi gì, lời từ chối mở đầu "Confirmation required"
  và liệt kê mã → Claude gọi `request_confirmation` với các mã đó → khách bấm Đồng ý → Claude gọi lại với
  `confirm_handles`; trợ lý chỉ chuyển cho plugin (thành `user_confirmed`) các mã có trên thẻ khách đã bấm Đồng ý. Chữ
  "ok" khách gõ không tính. File chưa có: không hỏi gì.
- Mã của file: 16 chữ số hex tạo từ đường dẫn đầy đủ (không phân biệt hoa thường, `/` = `\`), cùng dạng handle nên đi được
  qua thẻ (nút "Xem trước" không tìm thấy đối tượng nào cho mã file: không sao).
- Giữ luật cũ: "Chỉ xem", gói / chế độ không đổi. **Macro chạy lại** không hiện thẻ (không ai bấm được): `overwrite` /
  `replace` cũ quyết như trước; Excel không bao giờ ghi lại khi chạy macro (F4). Tham số `overwrite` / `replace` vẫn nhận
  (skill / macro cũ không lỗi tham số); ngoài macro chúng không thay được thẻ.
- Code: `Core/F5.cs` (`F5Confirm`), `Drawing/ConfirmF5.cs` (`RequireCard`, `RequireOverwriteCard`), `ITurnHost.MacroRunning`;
  `LayoutTools.cs`, `Solid3Tools.cs`, `ExcelExport.cs`, `RenderTools.cs`; trợ lý `confirmation.ts` (`F5_CARD_TOOLS`),
  `common.ts` (`confirm_handles`).

## 3. Ghi chú có mũi tên (multileader)

- Lệnh có sẵn `add_mleader` (điểm đầu mũi tên `arrow_point`, điểm đặt chữ `text_point`, nội dung `text`; hiểu UCS và đơn vị
  như các lệnh ghi chú khác, gói 2D / Full) — **không thêm lệnh mới**, nằm ngoài nhóm nạp sẵn như trước.
- `setup_standards` tạo thêm kiểu ghi chú mũi tên **`KT-ML-<tỷ lệ>`** (`VN-ML-…`; bản vẽ mét `-M`…), chữ của bộ (`KT-ARIAL`),
  kích thước trên giấy nhân tỷ lệ như kiểu kích thước, đặt làm hiện hành. Kiểu cùng tên đã có mà khác: giữ nguyên, báo
  trong `kept_handles` (thẻ như F2).
- `add_mleader` không ghi `style` / `height`: theo kiểu hiện hành; nếu đó là kiểu của bộ chuẩn thì chữ lấy kiểu chữ và cỡ chữ
  của kiểu (2,5 mm × tỷ lệ). Bản vẽ không dùng bộ chuẩn: chữ như trước F5.

## Con số chờ chủ dự án duyệt

| Số / chữ | Ở đâu (để đổi) | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Chữ ghi chú mũi tên cao **2,5 mm** trên giấy (× tỷ lệ, vd 1:100 → 250) | `F5Numbers.MLeaderTextHeight` (= cỡ chữ ghi chú của bộ F2) | cỡ chữ ghi chú | khớp chữ ghi chú và chữ kích thước của bộ |
| Mũi tên **2,5 mm**, kiểu mũi tên đặc | `F5Numbers.MLeaderArrowSize`, `SetMLeaderStyle` | cỡ đầu mũi tên | theo thông lệ (mặc định của AutoCAD cho kiểu mét); mũi tên kích thước của bộ là gạch chéo 1,2 nên không dùng lại |
| Khoảng hở chữ – đường dẫn **1 mm**, đoạn gãy ngang **2,5 mm** | `F5Numbers.MLeaderLandingGap`, `MLeaderDoglegLength` | khoảng cách chữ tới mũi | theo thông lệ |
| Tên kiểu `KT-ML-100` / `VN-ML-100` | `MLeaderStyleLogic.Name` | tên trong danh sách kiểu | cạnh tên kiểu kích thước `KT-100` |
| Ô ⚙ "Nạp gọn lệnh" + dòng giải thích (mục 1) | `F5Numbers.DeferToolsLabel / DeferToolsTip` | chữ trong ⚙ | ngắn, nói rõ bật / tắt được gì |
| Chữ trên thẻ: Claude tự viết một câu tiếng Việt nói cái gì bị ghi đè / xóa (vd "Ghi đè file phong.xlsx đã có?") | lời từ chối `F5Confirm.CardMessage` | câu trên thẻ | như các thẻ ghi đè F2 |

## Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Khi nào ô ⚙ có hiệu lực | **(sửa ở bản chốt đợt F)** từ cuộc chat mới; cuộc chat đã có tin giữ cách nạp lúc bắt đầu tới khi bấm "Chat mới"; cuộc chat chưa có tin thì áp dụng ngay | cho đổi giữa cuộc chat: chỉ khi đã thử trên Claude thật rằng nối tiếp không bị từ chối; bỏ dòng gán `DeferTools` trong `ChatController.Send` |
| Ô ⚙ hay biến môi trường thắng | biến / file thắng (đường lui của người hỗ trợ) | đảo thứ tự trong `AgentSession.deferTools` |
| Mã thẻ cho file (file không có handle) | mã 16 hex từ đường dẫn | — |
| `overwrite` / `replace` cũ | vẫn nhận; chỉ thay được thẻ khi chạy macro | bỏ hẳn: cần sửa macro / skill cũ |
| Gán vật liệu cho từng khối | giữ luật > 20 đối tượng (không thẻ mỗi lần) | thêm vào thẻ nếu muốn chặt hơn |
| Ghi chú mũi tên | tham số / kiểu cho lệnh `add_mleader` có sẵn, không thêm lệnh | — |
| Cỡ chữ theo kiểu | chỉ với kiểu của bộ chuẩn (`…-ML-…`), bản vẽ khác giữ như trước F5 | áp cho mọi kiểu: bỏ điều kiện `IsStandardName` |

## Token lúc mở chat

Mô tả nạp đủ (đo như F3 / F4, mọi lệnh đầy đủ, ~3,5 ký tự / token): 2D 142 991 → 144 354 ký tự (+0,4k token), 3D 147 465 →
149 406 (+0,6k), Full 213 981 → 217 285 (+0,9k). Phần thêm nằm ở các lệnh bị hoãn (`plot_pdf`, `export_stl`, 4 bảng,
`named_view`, `set_material`, `add_mleader`, `setup_standards`) và 2 câu system prompt, nên khi nạp gọn (mặc định) tin đầu
hầu như không tăng.

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1148** (1132 + 16 `NangCapF5Tests.cs`). Sửa theo quyết định mới 2 test cũ
  kiểm chuỗi "!p.Bool(\"overwrite\", false)" (`NangCapF4Tests`, `TongTheDotFTests`): nay kiểm thẻ + luật cũ cho macro.
- Trợ lý `cd agent && npm ci && npm test`: **364** (355 + 9 `test/nangcapf5.test.ts`). Không sửa test cũ.
- Build 3 target: xem CI.

## CI

- Run **#52** (`45622402883`, `release=false`) trên `99f1bf6` (commit code cuối): **xanh** (job `windows`: build 3 target, lõi,
  trợ lý, UI test WPF). Run #51 (`4b21509`) bị hủy giữa chừng vì đã có bản sửa mới (bổ sung thẻ đổi vật liệu có sẵn). Trước đó
  trên Linux, sau lần sửa cuối: build 3 target 0 cảnh báo, lõi 1148, trợ lý 364, dự án UI test build được. Commit sau CI chỉ
  thêm mục này.

## Chưa kiểm được

- AutoCAD thật: thẻ hiện và ghi đè sau Đồng ý (PDF đang mở trong trình xem, STL, PNG, Excel), góc nhìn, vật liệu layer; kiểu
  ghi chú mũi tên (`PostMLeaderStyleToDb`, cỡ chữ của ghi chú trên kiểu); ô ⚙ trên khung chat thật. Bước tự kiểm F5 chưa chạy.
- Claude thật: đọc lời từ chối và dùng đúng `confirm_handles`; đổi ô ⚙ giữa cuộc chat.
