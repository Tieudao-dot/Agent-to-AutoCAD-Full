# Bàn giao Test & sửa đợt G4: render thật

*Người rà: thread `session_01D56HmFcYrzsme7GoBm8tNC` (khác người viết). Nhánh `full/nang-cap-g4-test` tách từ
`full/nang-cap-g4` @ `7641203` (code `7ddbf12`). 25/11/2026. Không ai ở đây chạy được AutoCAD: mọi kết luận về cách AutoCAD
hỏi / trả lời là suy từ code, chỗ nào không chắc ghi rõ dưới đây và trong `docs/checklist-dot-g4.md` mục F.*

## 1. Lỗi tìm được và đã sửa

| Mã | Lỗi | Sửa | Test |
| --- | --- | --- | --- |
| G4T-1 (nặng) | **Đường dẫn file tạm có dấu cách / dấu tiếng Việt** (tên người dùng Windows như `C:\Users\Nguyen Van A\AppData\Local\Temp`): ở dòng lệnh AutoCAD, dấu cách thường kết thúc câu trả lời, phần sau bị **gõ thành lệnh khác**; dấu tiếng Việt có thể không gõ đúng | File tạm chỉ đặt ở thư mục có đường dẫn ASCII chữ / số / `\ / : . _ - ~` (`RenderLogic.SafeTempDir`): `%TEMP%`, không được thì `C:\ProgramData\ClaudeCAD\render`; không có thư mục nào hợp lệ thì **không gõ -RENDER**, lưu ảnh chụp ngay (`method: capture`, nói rõ lý do). File của khách (có dấu cách / dấu) vẫn được, vì plugin chép file tạm sang | lõi |
| G4T-2 (vừa) | **Thứ tự câu hỏi khác → khách chờ tới hết thời gian tối đa** (5 phút mức vừa, 15 phút mức đẹp) dù lệnh đã thoát: câu trả lời lọt ra dòng lệnh, hoặc lệnh RENDER xong mà không có file | Hai đường lui sớm: (a) AutoCAD báo "lệnh không có" cho **một câu trả lời của mình** (`1920`, `Y`, `_High`, đường dẫn file…) trong lúc render → biết -RENDER hỏi khác thứ tự, dừng ngay, ảnh chụp (`RenderLogic.IsOurAnswer`, sự kiện `UnknownCommand`); (b) lệnh RENDER đã kết thúc (`CommandEnded`) mà sau 20 s chưa có file / file rỗng → ảnh chụp (`State.NoFile`, `G4Numbers.EndGraceSeconds`). Còn một trường hợp vẫn phải chờ tới tối đa: lệnh **đứng chờ nhập** (hỏi thêm câu sau tên file) — không phân biệt được với đang render; khách bấm Dừng / Esc để thôi sớm | lõi |
| G4T-3 (vừa) | **Trợ lý bỏ cuộc khi AutoCAD bận render**: mỗi lần hỏi `render_status` chỉ chờ 30 s; nếu AutoCAD render ở luồng chính (không trả lời trong lúc render), lần hỏi hết giờ → trợ lý trả lỗi cho Claude, còn render vẫn chạy và nền / ánh sáng tạm **không được trả lại** tới lần render sau | Hỏi hết giờ (`TIMEOUT`) thì hỏi tiếp (dòng trạng thái "AutoCAD đang bận render, chờ thêm…"), vẫn trong giới hạn tối đa + 60 s; quá thì `render_stop`. Plugin giữ **câu trả lời cuối** của render xong lúc không ai hỏi (`_lastRenderResult`) để lần hỏi sau vẫn nhận đúng "render" / "capture" | trợ lý 2 + lõi |
| G4T-4 (nhẹ) | **File tạm không được dọn** khi Dừng / Esc / lỗi / quá giờ (chỉ dọn khi render xong) | `FinishRender` xóa file tạm ở mọi nhánh trừ render xong (chép xong mới xóa) | lõi (mã nguồn) |
| G4T-5 (nhẹ) | **Góc nhìn có thể không trở lại** nếu bước trả nền / ánh sáng gặp lỗi (cùng một khối `try`); id nền tạm sau giao dịch bị hủy có thể không hợp lệ | Trả góc nhìn trong khối riêng (luôn chạy); kiểm `IsValid` trước khi xóa nền tạm | lõi (mã nguồn) |
| G4T-6 (nhẹ) | **Vật liệu "thư viện" chỉ là khớp theo tên**: vật liệu khách tự tạo tên "Brick wall" cũng được chọn khi gán "gạch", mà kết quả nói "Autodesk library material" | Câu báo nói rõ tên vật liệu đã dùng, "khớp theo tên (thường là vật liệu thư viện Autodesk)", vật liệu **dùng nguyên, không đổi** | lõi |

## 2. Đã rà 9 điểm

1. **Chuỗi trả lời `-RENDER`** (`RenderLogic.CommandScript`): `_-RENDER`, `_<preset>`, `_R`, rộng, cao, `_Y`, tên file — mỗi câu
   một dòng. Không tra được tài liệu Autodesk (mạng chặn), không chạy được AutoCAD: **không chắc**. Những gì xảy ra khi thứ
   tự khác (suy từ cách dòng lệnh AutoCAD xử lý chữ gõ vào):
   - lệnh thoát sớm → các câu còn lại gõ ở `Command:`: `_R` = lệnh REDRAW (vô hại), số / `_Y` / tên preset / đường dẫn = "lệnh
     không có" → nay dừng ngay, ảnh chụp (G4T-2a). Không câu nào trùng lệnh có hại (không có ERASE, U, QUIT…);
   - một câu hỏi Có / Không nhận nhầm số → AutoCAD hỏi lại, câu sau bị nuốt; cuối cùng hoặc thoát (như trên) hoặc đứng chờ;
   - lệnh đứng chờ nhập: chờ tới thời gian tối đa (2 / 5 / 15 phút), rồi `^C^C` hủy lệnh, ảnh chụp; Dừng / Esc thôi sớm
     được; dòng lệnh không còn lệnh dở;
   - không bao giờ gõ thêm Enter thừa (Enter trống ở `Command:` lặp lại lệnh trước = render lần nữa).
   Mục F của checklist nhờ cậu chép lại câu hỏi thật của `-RENDER` để sửa `CommandScript` nếu khác.
2. **Đường lui**: RENDER không có (sự kiện `UnknownCommand` của RENDER) / lỗi (`CommandFailed`) / quá giờ / lệnh xong không có
   file / file rỗng / câu trả lời lọt ra dòng lệnh / không có thư mục tạm an toàn → đều `method: "capture"` kèm `reason`; bị
   dừng → `cancelled`, không file. `"method": "render"` chỉ có ở **một** chỗ: sau khi file tạm có, cỡ không đổi 1,5 s và đã
   chép sang file khách (test đếm đúng một chỗ). Ảnh chụp ghi vào đúng file khách đã đồng ý.
3. **Trả lại trạng thái**: nền, đèn mặc định, mặt trời, góc nhìn được trả ở mọi nhánh (xong, dừng, Esc, lỗi, quá giờ, render
   cũ bị dọn); nay góc nhìn trả riêng (G4T-5). Đổi tạm khi tắt ghi hoàn tác. "Chỉ xem": nền / ánh sáng của bản vẽ, không đổi
   gì. **Chưa chắc**: `-RENDER` có ghi preset hiện hành / cỡ ảnh cuối vào bản vẽ không (biến của AutoCAD, không đổi lại được
   từ plugin) — ghi trong "chưa kiểm được".
4. **Tiến độ và dừng**: trợ lý hỏi mỗi 2 s, không vô hạn (tối đa + 60 s, kể cả khi mỗi lần hỏi hết giờ — G4T-3); dòng trạng
   thái khung chat cập nhật một dòng; Dừng (khung chat: `StopRender` + `render_stop`), Esc (sự kiện hủy lệnh RENDER). Hai
   lệnh render chồng nhau: lần hai bị từ chối "a render is already running" (một render một lúc, kể cả hai bản vẽ).
5. **File**: thẻ ghi đè F5 xin **trước** khi đổi bất cứ gì (Hủy = không đổi, không ghi); file tạm tên không dấu, nay ở thư
   mục an toàn (G4T-1) và được dọn (G4T-4); file khách có dấu / dấu cách được (chép bằng .NET).
6. **Vật liệu**: bảng từ khóa khớp nguyên từ trong tên; không đổi vật liệu khách đã có (chỉ gán); câu báo nói rõ (G4T-6);
   `library: false` = màu đơn giản; kết quả có `source` library / simple.
7. **Gói / chế độ / macro / UCS**: `named_view` nhóm ThreeD (gói 3D / Full, chế độ 3D / Cả hai); render là lệnh đọc → không ghi
   macro (như `image`); `named_view` nhóm WCS của F1 (UCS xoay không đổi gì; render góc nhìn hiện tại).
8. **Nạp gọn**: `named_view`, `set_material` vẫn nạp khi cần (test cũ); phần luôn nạp chỉ thêm câu system prompt (+180 ký tự
   ≈ 50 token) như đã báo; tắt nạp gọn vẫn chạy (cùng lệnh, đủ mô tả). Lần rà này không đổi mô tả lệnh.
9. **Bảng con số / quyết định** (`nang-cap-g4.md` mục 6, 7): khớp `G4Numbers`, `RenderLogic.Qualities`, `MaterialLibraryLogic.Words`
   từng dòng. Bổ sung số kỹ thuật mới (mục 3 dưới) vào ghi chú của mục 6. Checklist thêm mục F (thứ tự câu hỏi `-RENDER`,
   đường lui sớm, máy có tên người dùng có dấu cách, file tạm được dọn).

## 3. Con số / quyết định mới

| Số / quyết định | Ở đâu | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Lệnh RENDER đã xong mà chưa có file sau **20 s** → ảnh chụp | `G4Numbers.EndGraceSeconds` (kỹ thuật) | đường lui tới sớm (thay vì 2 – 15 phút) | đủ cho AutoCAD ghi xong file sau khi lệnh kết thúc |
| Thư mục file tạm: `%TEMP%`, không được thì `C:\ProgramData\ClaudeCAD\render`; không có thì ảnh chụp ngay | `RenderG4.cs` `RenderTempDir`, `RenderLogic.SafeTempDir` | — | đường dẫn gõ ở dòng lệnh phải không dấu cách / dấu |
| Quyết định tự chọn: thứ tự câu hỏi khác → bỏ render ngay khi biết (không thử đoán thứ tự khác) | `OnRenderUnknown` | ảnh chụp kèm lý do | an toàn hơn đoán; sửa `CommandScript` khi có câu hỏi thật |

## 4. Test thêm

- Lõi `plugin/ClaudeCAD.Core.Tests/NangCapG4TestSuaTests.cs` (5 test): lệnh xong không file → đường lui sau thời gian đệm, không
  phải tối đa; câu trả lời lọt ra dòng lệnh được nhận ra (và chữ khác thì không); thư mục tạm an toàn; câu báo vật liệu thư
  viện; mã nguồn plugin (sự kiện `CommandEnded` gắn / gỡ, dọn file tạm, trả góc nhìn riêng, giữ câu trả lời cuối, "render"
  chỉ ở một chỗ).
- Trợ lý `agent/test/nangcapg4test.test.ts` (2 test): hỏi hết giờ thì hỏi tiếp và vẫn nhận "render"; nhưng không vô hạn, và
  không bao giờ báo "render".
- Test cũ của G4 giữ nguyên, vẫn xanh (thêm một tham số cho `StateOf` dưới dạng hàm mới, hàm cũ giữ).
- Sau lần sửa cuối: lõi 1299 / 1299, trợ lý 387 / 387; build `net48`, `net8.0-windows`, `net10.0-windows` 0 lỗi 0 cảnh báo.

## 5. Chưa kiểm được (cần AutoCAD thật)

- **Thứ tự câu hỏi thật của `-RENDER`** trên 2022 – 2027 (checklist mục F1); tên preset trong bản mới (Low / Medium / High có
  còn không).
- AutoCAD có trả lời plugin trong lúc render không (render ở luồng chính hay nền); `CommandEnded` của RENDER đến lúc nào so với
  lúc file được ghi (nếu file ghi muộn hơn 20 s sau lệnh xong thì đường lui sớm sẽ nhầm — đổi `EndGraceSeconds`).
- AutoCAD báo "lệnh không có" cho số / `Y` gõ ở `Command:` qua sự kiện `UnknownCommand` như dự đoán không.
- `-RENDER` có ghi preset / cỡ ảnh vào bản vẽ không (một thay đổi nhỏ không trả lại được, kể cả khi "Chỉ xem").
- Bước tự kiểm G4 (không chạy trên CI); thư mục `C:\ProgramData` có ghi được không trên máy khóa quyền.

## 6. CI

Run 45661671404 (#64) trên `fdeb306` (commit code cuối): **xanh**. Dùng 1 / 2 lượt. Commit sau đó chỉ ghi kết quả CI vào file này.
