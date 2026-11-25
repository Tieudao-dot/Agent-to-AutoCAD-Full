# Bàn giao: Nâng cấp đợt G5 — vẽ lại từ ảnh / PDF scan (25/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người viết). Nhánh `full/nang-cap-g5`, tách từ `full/nang-cap-g3-test` @ `4061a6c`,
gộp `full/nang-cap-g4-test` @ `fdeb306` (commit gộp `1c2a1c1`, CI xem mục 10). Chưa thử trên AutoCAD thật, chưa thử với Claude
thật.

## 1. Khách thấy gì

- Khách kéo / dán ảnh chụp, bản scan vào khung chat (hoặc nói đường dẫn ảnh / PDF), gõ "vẽ lại bản này". Gói 2D / Full, chế độ
  "2D" / "Cả hai" của máy Full.
- Claude gắn ảnh / trang PDF làm nền trên layer `X-ANH-GOC` (xám, **khóa**, **không in**), bên phải bản vẽ đang có (bản vẽ
  trống: gốc 0,0). Ảnh dán từ clipboard được lưu một lần ở `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem` rồi chép cạnh bản vẽ
  (`<bản vẽ>-anh-goc-<mã>.png`) trước khi gắn; ảnh kéo / chọn từ file dùng chính file đó.
- Lúc mới gắn, ảnh **chưa đúng tỉ lệ** (rộng 10 m chỉ để hiện ra). Claude tìm một kích thước ghi trên bản scan (hoặc hỏi
  khách), bấm hai đầu mút trên ảnh + độ dài thật → ảnh co cho khớp. Không có kích thước thật: Claude **hỏi**, không đoán.
- Ảnh chụp nghiêng: Claude xoay ảnh theo một bức tường cho thẳng (chỉ xoay; méo phối cảnh không sửa được, Claude nói rõ).
- Claude xem ảnh (ảnh nhỏ 1024 px có lưới tọa độ vẽ sẵn, nhãn đúng tọa độ các lệnh vẽ dùng), xem phóng to từng phần, rồi
  vẽ tường, cửa, cửa sổ, tên phòng, kích thước chính bằng các lệnh vẽ thường, xem lại một lần để so. Tối đa **8 lần xem**
  mỗi yêu cầu; kết quả mỗi lần xem ghi đã xem mấy lần.
- Kết quả luôn nói: **bản nháp cần kiểm kích thước**, các chỗ đọc không chắc (chữ mờ, nét đứt, kích thước cộng không khớp),
  số đã tự lấy (vd tường 200 mm khi không đọc được).
- U một lần hoàn tác cả lượt (ảnh nền + nét vẽ). "Chỉ xem": xem / mô tả được (`view`, `info`), không gắn, không vẽ.

## 2. Cách làm

- Một lệnh mới `trace_image` (nhóm 2D, **hoãn nạp**), 5 action: `attach`, `scale`, `straighten`, `view`, `info`. Các bước
  làm nằm trong mô tả lệnh (chỉ nạp khi Claude tìm tới lệnh) và một mục trong skill `ve-kien-truc`; phần luôn nạp chỉ thêm
  một dòng system prompt.
- Ảnh: `RasterImageDef` + `RasterImage` (IMAGEATTACH). PDF: `PdfDefinition` + `PdfReference` (PDFATTACH), chọn trang bằng
  `ItemName`. Đếm trang PDF bằng cách đọc file (`/Type /Page`, `/Count` của cây trang); nhiều trang mà không nói trang → hỏi.
- Đánh dấu ảnh nền: Xrecord `CLAUDECAD_TRACE` trong extension dictionary (file, trang, đã đặt tỉ lệ chưa, độ dài thật).
- Đặt tỉ lệ / xoay: `TransformBy` (co quanh p1 / xoay quanh p1); layer ảnh mở khóa trong lúc đổi rồi khóa lại.
- Xem: ảnh chụp ngoài màn hình của `capture_view` (Capture.Render, mặt bằng, vùng ảnh hoặc `p1` / `p2`) + lưới vẽ đè (WPF):
  bước lưới tròn theo mm (100 … 50 000) để khoảng 10 đường trên cạnh dài; nhãn theo UCS hiện tại (UCS dời / xoay trong mặt bằng
  → lưới nghiêng theo); UCS nghiêng khỏi mặt bằng → từ chối, nhờ `reset_ucs`. Cùng một cách cho ảnh và PDF, và cho lần xem lại
  sau khi vẽ (nét vẽ nằm trên ảnh nền trong cùng ảnh xem).
- Đếm số lần xem ở trợ lý (`agent/src/tools/trace.ts`), đặt lại mỗi tin nhắn mới của khách; quá giới hạn thì lệnh xem bị từ
  chối ngay (không gọi AutoCAD) với lời nhắc hoàn tất bản nháp.
- Đường dẫn ảnh đính kèm: plugin gửi kèm `path` của mỗi ảnh (file gốc, hoặc file đã lưu của ảnh dán); trợ lý ghi một dòng
  "Files of the pictures: 1: …" vào tin nhắn cho Claude.
- Lõi (không cần AutoCAD, có test): `Core/Trace.cs` — `G5Numbers`, `TraceLogic` (loại file, cỡ file, trang PDF, tên file
  chép, đổi đơn vị, hệ số tỉ lệ, góc xoay, chỗ đặt ảnh, bước lưới, đường lưới + cắt theo khung ảnh, câu "bản nháp").

## 3. Đúng với

- Gói 2D / Full; chế độ "2D" / "Cả hai"; gói 3D và chế độ "3D" không có (test). Nhãn: "Vẽ lại từ ảnh".
- Macro: không ghi (`MacroLogic.NotRecorded`, như `fix_drawing`): ảnh và vị trí của một bản vẽ, không phát lại sang bản khác.
- UCS: chế độ Ref của F1 (điểm vào / ra theo UCS hiện tại); lưới theo UCS. Đơn vị mm / cm / m (INSUNITS) cho độ dài thật, bước
  lưới, chỗ đặt và cỡ ban đầu.
- File: không có, rỗng, quá 50 MB, sai loại (webp / heic: nhờ lưu JPG; dwg / dxf: đã là bản vẽ), PDF nhiều trang, PDF có mật
  khẩu, ảnh AutoCAD không đọc được: từ chối có lời giải thích. PDF vector: gắn được, kèm gợi ý `PDFIMPORT`.

## 4. Tự kiểm

Bước "Nâng cấp đợt G5" trong `SelfTest.cs`: tạo ảnh 400 × 200 px, gắn, đặt tỉ lệ rộng 8 m (kiểm số), xem (kiểm ảnh xem có điểm
sáng của ảnh nền: **cho biết AutoCAD có vẽ ảnh trong ảnh chụp ngoài màn hình không**), `info`, kiểm layer khóa + không in, xóa.

## 5. File

- Mới: `plugin/ClaudeCAD.Plugin/Core/Trace.cs`, `Drawing/TraceTools.cs`, `UI/ChatAttachments.cs`, `agent/src/tools/trace.ts`,
  `plugin/ClaudeCAD.Core.Tests/NangCapG5Tests.cs` (40 test), `agent/test/nangcapg5.test.ts` (8 test), `docs/checklist-dot-g5.md`,
  file này.
- Sửa ít dòng: `Core/Plans.cs` (một dòng `Add(ToolGroup.TwoD, "trace_image")`), `Core/Macros.cs`, `UI/ToolLabels.cs`,
  `UI/ImageAttachment.cs` (`SourcePath`), `ChatController.cs` (gửi `path`), `Drawing/DrawingService.cs` (`RegisterTrace`),
  `SelfTest.cs`, `ClaudeCAD.Core.Tests.csproj`, `agent/src/tools/index.ts`, `agent/src/ipc/protocol.ts` (`path`),
  `agent/src/agent/content.ts` (dòng đường dẫn), `agent/src/agent/session.ts` (đặt lại số lần xem), `agent/src/agent/systemPrompt.ts`
  (một dòng), `skills/ve-kien-truc/SKILL.md` (mục mới), `bo-cai/README.md`.

## 6. Con số chờ chủ dự án duyệt

Tất cả ở `Core/Trace.cs` (`G5Numbers`), trừ số lần xem ở `agent/src/tools/trace.ts` (`TRACE_MAX_VIEWS`).

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Số lần xem tối đa mỗi yêu cầu | 8 | quá thì Claude dừng xem, hoàn tất bản nháp, nói phần chưa soát | 1 xem cả ảnh + ~5 phần + 1–2 lần so; mỗi lần ~1k token |
| Cỡ ảnh xem gửi Claude | 1024 px cạnh dài (Claude được xin tới 1568) | — (token) | đủ đọc nét chính; chữ nhỏ thì xem phóng to một phần |
| Bước lưới | tròn theo mm: 100, 200, 250, 500, 1 000, 2 000, 2 500, 5 000, 10 000, 20 000, 50 000; khoảng 10 đường / cạnh dài | lưới trên ảnh xem | dễ đọc, nhãn tròn số |
| Cỡ file tối đa | 50 MB | file lớn hơn bị từ chối, nhờ scan nhỏ hơn | scan 300 dpi khổ A1 khoảng 20–40 MB |
| Loại file | PNG, JPG / JPEG, BMP, TIF / TIFF, GIF, PDF | loại khác bị từ chối có hướng dẫn | các loại AutoCAD gắn được |
| Layer ảnh nền | `X-ANH-GOC`, màu 8 (xám), khóa, không in | tên layer trong bản vẽ | "X-" = tham chiếu, đứng riêng khỏi layer A-* |
| Rộng ban đầu (chưa tỉ lệ) | 10 000 mm | ảnh rộng 10 m lúc mới gắn | cỡ một mặt bằng nhà, dễ nhìn trước khi đặt tỉ lệ |
| Khoảng cách tới bản vẽ đang có | 5 000 mm bên phải | chỗ ảnh nằm | không đè lên hình có sẵn |
| Độ dày tường khi không đọc được | 200 mm (Claude nói rõ đã lấy) | độ dày tường trong bản nháp | tường gạch thường gặp (skill ve-kien-truc dùng 220 / 110 khi bản scan ghi) |
| Góc xoay tối đa khi làm thẳng | 45° | xoay nhiều hơn bị từ chối (chọn sai cặp điểm) | ảnh nghiêng thật hiếm khi quá vài độ |

Số kỹ thuật (khách không thấy): tối đa 400 đường lưới / ảnh; hệ số tỉ lệ ngoài 1e-6 … 1e6 bị từ chối; nhãn lưới cỡ chữ 11 px.

## 7. Quyết định tự chọn

1. **Một lệnh mới, hoãn nạp**, thay vì mở rộng `capture_view` / `insert_*`: gom gắn / tỉ lệ / xoay / xem vào một chỗ, không
   tăng phần luôn nạp.
2. **Các bước làm đặt trong mô tả lệnh** (nạp khi cần) + một mục trong skill `ve-kien-truc`, **không** tạo skill mới: skill mới
   phải thêm vào bộ cài, danh sách skill có sẵn và ô "Chuẩn" của khung chat; máy đã cài sẵn skill cũ không nhận file mới (bộ cài
   không ghi đè skill) nên mô tả lệnh là chỗ chắc chắn tới được Claude.
3. **Xem bằng ảnh chụp ngoài màn hình của AutoCAD** (như `capture_view`) cho cả ảnh và PDF, cả lần xem lại sau khi vẽ; không tự
   đọc file ảnh. Rủi ro: nếu AutoCAD không vẽ ảnh nền trong ảnh chụp ngoài màn hình thì Claude không thấy bản scan — bước tự kiểm
   G5 kiểm đúng điều này.
4. **Đường dẫn ảnh đính kèm** gửi kèm tin nhắn thay vì chép ảnh vào bản vẽ ngay: không đổi gì khi khách chỉ hỏi về ảnh.
5. **Ảnh dán chép cạnh bản vẽ** (thư mục của khung chat có thể bị dọn); ảnh kéo từ file dùng chính file đó (khách tự giữ).
6. **Ảnh chưa tỉ lệ có cỡ 10 m** và kết quả luôn ghi `scaled: false` + nhắc đặt tỉ lệ; không bao giờ tự đặt tỉ lệ theo điểm ảnh.
7. **Đếm số lần xem ở trợ lý, theo tin nhắn của khách**; lần xem lỗi không tính.
8. **UCS nghiêng khỏi mặt bằng bị từ chối** (nhờ `reset_ucs`); UCS dời / xoay trong mặt bằng thì lưới nghiêng theo.
9. **Không ghi macro** (như `fix_drawing`).
10. PDF vector không tự nhận ra; mọi PDF đều gắn được, kết quả kèm gợi ý `PDFIMPORT` cho PDF xuất từ phần mềm CAD.

## 8. Token

- Lúc mở chat: system prompt gói 2D 12 487 → 12 592 ký tự (+105, khoảng 30 token); Full 19 889 → 19 994 (+105). Gói 3D không
  đổi. Phần lệnh nạp sẵn không đổi (43 359 ký tự); danh sách tên lệnh hoãn nạp thêm "trace_image" (+13 ký tự).
- Mô tả `trace_image`: 2 902 ký tự (khoảng 830 token), chỉ khi Claude tìm tới lệnh, hoặc khi tắt "Nạp gọn lệnh".
- Đo bằng số ký tự (khoảng 3,5 ký tự / token), không phải số token chính xác.
- Một lần "vẽ lại" điển hình (ước lượng): 6 lần xem × ~1 050 token (ảnh 1024 × 768; ~1 400 với 1024 × 1024) ≈ 6–8k token ảnh,
  cộng mô tả lệnh `trace_image` khi được nạp (~1k token) và các lệnh vẽ (tường, cửa, chữ, kích thước: như vẽ tay một mặt bằng).
  Ảnh khách đính kèm tin nhắn tính thêm ~1,6k token (ảnh 1568 px như trước).

## 9. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1356** (1316 sau gộp + 40 `NangCapG5Tests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **398** (390 sau gộp + 8 `test/nangcapg5.test.ts`). Không sửa / bỏ test cũ.
- Build 3 target (xóa obj / bin giữa các lần): 0 cảnh báo.

## 10. CI

- Bước gộp: run **#65** (`45662682522`) trên `1c2a1c1`: **xanh**.
- G5: run **#66** (`45665170098`, `release=false`) trên `8a2515c` (code G5 `755445f` + gộp tài liệu G4-test `29a2e6c`): **xanh** (job
  `windows`: build 3 target, lõi, trợ lý, UI test WPF). Dùng 1 / 2 lượt của G5. Commit sau đó chỉ thêm mục này.

## 11. Chưa kiểm được

- AutoCAD thật: `RasterImageDef.Load`, `PdfDefinition.Load`, cỡ PDF ở tỉ lệ 1, ảnh nền có hiện trong ảnh chụp ngoài màn hình
  (Capture.Render) không, layer khóa làm ảnh mờ đi bao nhiêu (LAYLOCKFADECTL), U hoàn tác ảnh nền, đường dẫn có dấu tiếng Việt,
  bước tự kiểm G5.
- Claude thật: đọc tường / chữ / kích thước trên ảnh có lưới tốt tới đâu, số lần xem thực tế, độ lệch kích thước. Thử tay:
  `docs/checklist-dot-g5.md`.
