# Bàn giao: Sửa KFG phần G4 + G5 + F5 (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5`. Nhánh `full/nang-cap-g-sua-kfg-b`, tách từ `full/nang-cap-g-tong-the` @ `881b12e`.
Nguồn lỗi: `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` (nhánh `claude/inspiring-cray-58xsmt` @ `931d8f8`). Chưa thử trên AutoCAD thật.
Không chạy CI riêng (thread giữ nhánh tích hợp chạy sau khi gộp); không đổi giao diện khung chat.

## 1. Lỗi đã sửa

| Mã | Đã sửa thế nào | Test |
| --- | --- | --- |
| KFG-01 (nặng) | Góc nhìn đã lưu được **sao chép trong transaction** (`NamedViewCopy`, `rec.Clone()`), bản sao dùng cho `SetCurrentView` và hướng nhìn của đường lui, `Dispose` ở mọi nhánh. Bước tự kiểm G4 thêm nhánh có tên: lưu góc nhìn, lấy bản sao, hiện rồi trả lại, xóa. | `SuaKfgBTests.Kfg01_…` |
| KFG-04 | `lighting: auto` khi bản vẽ có đèn / mặt trời: **tắt đèn mặc định trong lúc render** (AutoCAD bỏ qua đèn của khách khi đèn mặc định bật), trả lại sau render như mọi thay đổi tạm. | `Kfg04_…`; test cũ không đổi |
| KFG-27 | Kết quả cuối khi `method: "capture"` bỏ `background`, `lighting`, `changed_for_render`, `estimate_seconds`, `max_wait_seconds`, `quality` của lúc bắt đầu (`finalRenderResult`); render thật giữ nguyên. | `agent/test/suakfgb.test.ts` |
| KFG-10 (F5) | Mã duyệt "vật liệu cho cả layer" = layer + tên vật liệu (`F5Confirm.LayerMaterialToken`); "đổi màu / độ trong vật liệu có sẵn" = vật liệu + màu + độ trong (`MaterialLookToken`). Duyệt "gạch cho A-WALL" không còn cho "kính", duyệt màu đỏ không cho màu khác. | `Kfg10_…`; test F5 cũ sửa đúng một dòng (mã cũ là handle layer) |
| KFG-05 | `attach`: ảnh đặt theo trục UCS (`Orientation` theo `frame.X` / `frame.Y`; PDF xoay theo góc UCS quanh góc dưới trái). `straighten`: góc tính trong UCS (`StraightenAngle(…, ucsAngleDeg)`). `min` / `max` của `attach`, `scale`, `straighten` là hai góc của ảnh (trả về theo UCS). | `Kfg05_…` |
| KFG-06 | `scale` / `straighten` / `view` với `handle`: chỉ nhận ảnh có dấu `CLAUDECAD_TRACE`; ảnh của khách bị từ chối, không mở khóa layer. | `Kfg06_…` |
| KFG-11 | Đếm trang theo `/Count` của cây trang mà `/Root` (bản cuối) chỉ tới; không đọc được thì đếm đối tượng trang, mỗi đối tượng một lần (PDF lưu nối tiếp). Không biết số trang (PDF nén): vẫn gắn trang 1 nhưng kết quả có `pages_note` nói rõ. | `Kfg11_…`; test cũ không đổi |
| KFG-12 | Đường dẫn ảnh trong dòng "Files of the pictures" bọc bằng `q()` như các giá trị khác của máy khách. | `suakfgb.test.ts`; test G5 cũ sửa một dòng (đường dẫn nay có ngoặc kép) |
| KFG-13 | Chỉ gửi đường dẫn / lưu ảnh dán khi gói + chế độ có `trace_image` (2D / Full, chế độ 2D / Cả hai); ảnh dán cũ hơn 7 ngày bị dọn mỗi lần lưu ảnh mới (ảnh dùng lại được làm mới ngày); `attach` ảnh dán có `pasted_note` (ảnh ≤ 1568 px, bản scan lớn nên kéo file gốc); mô tả lệnh nói giới hạn này. Không lưu bản gốc của ảnh dán (phải giữ ảnh gốc trong bộ nhớ khung chat: không rẻ). | `Kfg13_…` |
| KFG-14 (phần Trace.cs) | File AVIF báo đúng là AVIF (trước là HEIC), đuôi `.avif` có hướng dẫn lưu JPG / PNG. | `Kfg14_…` |

## 2. Nghi vấn đã xem

| Nghi vấn | Kết luận | Đã làm |
| --- | --- | --- |
| G4: render chạy tiếp sau khi lệnh kết thúc | Có thể (chưa có AutoCAD để chắc). Chọn cách luồng chính đã chốt. | Thời gian chờ thêm sau khi lệnh kết thúc = `max(20 s, ước lượng của mức + cỡ ảnh)` (`RenderLogic.EndGrace`); "lệnh lạ" vẫn chuyển ngay sang ảnh chụp. Checklist G4 mục G.3. |
| G4: Dừng / quá giờ trả cảnh và xóa file tạm ngay | Đúng là render có thể ghi file tạm sau đó (chưa chắc AutoCAD dừng hẳn). Trả cảnh ngay vẫn giữ (khách cần lại bản vẽ như cũ). | File tạm `claudecad-render-*.png` cũ hơn 1 giờ được dọn trước mỗi lần render. |
| G4: `SetCurrentView` không trong đoạn tắt ghi hoàn tác | Đúng. | Đặt góc nhìn để render và trả góc nhìn đều tắt ghi hoàn tác: U không nhảy góc nhìn. |
| G4: đèn trong block không được đếm | Đúng. | Đếm đèn đang bật trong block (lồng tới 8 cấp, mỗi block đọc một lần). |
| G5: đường dẫn mạng (bảo mật) | Đúng. Chọn cách luồng chính đã chốt. | Chỉ mở: ổ đĩa của máy, ảnh đính kèm trong chat, hoặc đường dẫn mạng cùng máy chủ + thư mục chia sẻ với bản vẽ đang mở (ổ mạng ánh xạ: cùng ổ với bản vẽ). Khác thì từ chối, hướng dẫn chép về máy / kéo vào chat. Kiểm cả đường dẫn gốc và đường dẫn đầy đủ. |
| G5: ảnh xem PDF coi là "trắng" theo màu viền | Đúng: phóng vào giữa trang thì viền cũng là trang. | Dùng màu nền **đã biết** của ảnh chụp ngoài màn hình (RGB 33, 40, 48), không đoán theo viền. |
| G5: so tiền tố thư mục đính kèm thiếu `\` | Đúng (`…\anh-dinh-kem-2\` bị coi là bên trong). | `TraceLogic.IsInside` so cả dấu tách. |
| G5: bản vẽ chưa lưu → ảnh chép vào Documents | Đúng. | Ảnh dán vào bản vẽ chưa lưu: từ chối, nhờ lưu bản vẽ trước. File của khách trên đĩa không bị ảnh hưởng. |
| G5: BigTIFF bị từ chối | Đúng (chặn ở bước đọc đầu file). | Nhận dạng BigTIFF; AutoCAD đọc được hay không thì `RasterImageDef.Load` báo, có lời giải thích. |

## 3. Con số chờ chủ dự án duyệt

| Số | Đề xuất | Ở đâu | Khách thấy | Vì sao |
| --- | --- | --- | --- | --- |
| Giữ ảnh dán trong thư mục khung chat | 7 ngày | `G5Numbers.PastedKeepDays` | ảnh dán cũ hơn bị xóa khỏi `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem` (bản đã gắn vào bản vẽ được chép cạnh bản vẽ nên không mất) | đủ cho một tuần làm việc, không đầy đĩa |
| Chờ thêm sau khi lệnh RENDER kết thúc mà chưa có ảnh | lớn hơn giữa 20 s và thời gian ước lượng (nhanh 20 s, vừa 60 s, đẹp 180 s cho Full HD) | `RenderLogic.EndGrace` | lâu nhất khách chờ trước khi nhận ảnh chụp thay ảnh render khi render không ra file | luồng chính đã chọn |

Số kỹ thuật (khách không thấy): file tạm render cũ hơn 1 giờ bị dọn (`RenderLogic.OldTempHours`); đếm đèn trong block lồng tối đa 8
cấp.

## 4. Quyết định tự chọn

1. KFG-13: **không** lưu bản gốc của ảnh dán (khung chat chỉ giữ bản đã thu nhỏ); thay vào đó nói rõ giới hạn và nhờ kéo file gốc.
2. Ảnh dán vào bản vẽ chưa lưu: **từ chối** (nhờ lưu trước) thay vì chép vào Documents.
3. Dừng / quá giờ: vẫn **trả cảnh ngay**; file tạm có thể còn lại được dọn ở lần render sau.
4. PDF không đếm được trang: vẫn gắn trang 1 (như trước) nhưng **nói rõ** trong kết quả, thay vì từ chối.
5. Đường dẫn mạng: kiểm ở `attach` (lúc đọc file khách đưa); `view` chỉ đọc file đã gắn qua `attach`.

## 5. File

- Sửa: `Drawing/RenderG4.cs`, `Core/Render.cs`, `Drawing/RenderTools.cs` (2 dòng mã duyệt), `Core/F5.cs` (2 hàm mã), `Drawing/TraceTools.cs`,
  `Core/Trace.cs`, `UI/ChatAttachments.cs`, `ChatController.cs` (3 dòng), `agent/src/tools/render.ts`, `agent/src/tools/trace.ts` (mô
  tả), `agent/src/agent/content.ts` (1 dòng), `docs/checklist-dot-g4.md` (mục G), `docs/checklist-dot-g5.md` (mục H).
- Test mới: `plugin/ClaudeCAD.Core.Tests/SuaKfgBTests.cs` (20), `agent/test/suakfgb.test.ts` (4). Test cũ sửa đúng 2 dòng ghi luật cũ
  (mã duyệt layer của F5; đường dẫn không ngoặc kép của G5), không bỏ test nào.

## 6. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1388** (1368 + 20).
- Trợ lý `cd agent && npm ci && npm test`: xem mục cập nhật cuối file.
- Build 3 target (xóa obj / bin giữa các lần): 0 cảnh báo.

## 7. Chưa kiểm được

- AutoCAD thật: `ViewTableRecord.Clone()` + `SetCurrentView` bản sao, tắt ghi hoàn tác khi đổi góc nhìn, `DefaultLightingOn` tắt / bật
  lại, render có chạy tiếp sau khi lệnh kết thúc không, ảnh nền theo UCS xoay (ảnh và PDF), `DriveInfo` của ổ mạng, `IsNamedDrawing`,
  bước tự kiểm G4 (nhánh góc nhìn đã lưu). Thử tay: `docs/checklist-dot-g4.md` mục G, `docs/checklist-dot-g5.md` mục H.
