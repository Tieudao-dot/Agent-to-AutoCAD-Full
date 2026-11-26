# Bàn giao: Nâng cấp đợt H1 (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết). Nhánh `full/nang-cap-h1`, tách từ `full/nang-cap-g-tong-the` @
`547d6af`, chạy cùng lúc với H2 (`full/nang-cap-h2`). Chưa thử trên AutoCAD thật. Phần đọc PDF chỉ chạy được trên Windows:
test của phần này chạy trên Windows CI (mục 9).

File dùng chung chỉ thêm dòng, không sắp lại: `ChatController.cs` (một hàm gọi trước `InitSkills`), `UpdateLogic.cs` (một
lệnh robocopy), `ClaudeCAD.Core.Tests.csproj` (2 file liên kết), `installer/ClaudeCAD.iss` (2 dòng). H1 không đụng
`agent/src/tools/index.ts`, `systemPrompt.ts`, `ToolDispatcher.cs` hay `docs/checklist-tong-the.md`.

## 1. Skill trên máy nâng cấp (chủ dự án chốt 26/11: "cập nhật, giữ bản khách đã sửa")

Trước đây bộ cài chép skill với cờ `onlyifdoesntexist`, nên máy nâng cấp **không bao giờ** nhận skill mới. Bản cập nhật trong
app thì ghi đè cả skill khách đã sửa.

**Khách thấy gì:**

- Skill khách chưa sửa: được thay bằng bản mới, không thông báo gì.
- Skill khách đã sửa: giữ nguyên. Bản mới nằm cạnh nó với tên có `.ban-moi` (vd `ve-kien-truc\SKILL.ban-moi.md`).
- Khung chat báo **một lần** cho mỗi bản mới: "Skill ve-kien-truc có bản mới. Bản cậu đã sửa được giữ nguyên; bản mới nằm
  cạnh nó (tên có ".ban-moi", vd SKILL.ban-moi.md) để cậu tự gộp khi muốn."
- Không xóa file nào của khách. Gỡ cài vẫn giữ thư mục skill (`uninsneveruninstall` giữ nguyên).

**Cách làm:**

- Bộ cài và bản cập nhật trong app chép skill của bản mới vào `Contents\skills-ban-moi`. Bộ cài: một dòng `[Files]` +
  `[InstallDelete]`. Bản cập nhật: một dòng robocopy `/MIR` sau phần skill trong script.
- Lúc plugin khởi động, `SkillSync.Sync` (`Core/SkillSync.cs`) so từng file trước khi nạp skill:
  - chưa có thì chép vào;
  - trùng nội dung thì chỉ ghi nhận;
  - "khách chưa sửa" thì ghi đè;
  - còn lại thì ghi file `.ban-moi` cạnh nó và báo.
- **"Khách chưa sửa"** nghĩa là mã băm SHA-256 của file khớp một trong hai nguồn:
  - bản ghi lần cài trước, lưu trong `skills\.ban-da-cai.json`;
  - một bản từng phát hành (`SkillSync.KnownShipped`, 47 mã từ toàn bộ lịch sử git của `skills/`, cho máy đang ở bản cũ
    chưa có bản ghi).
- Mã băm bỏ BOM và CR, nên đổi kiểu xuống dòng không bị coi là "đã sửa".
- `installer/skill-hashes.sh` in lại danh sách mã. Khi ra bản mới thì chạy lại và dán vào `KnownShipped`; chưa dán cũng
  không sao, vì từ bản này trở đi bản ghi `.ban-da-cai.json` đã đủ.
- Lỗi ở bước đồng bộ chỉ ghi log, không chặn mở khung chat.

## 2. trace_image đọc thẳng trang PDF

Trước đây ảnh xem của PDF scan là ảnh chụp ngoài màn hình của AutoCAD. AutoCAD có lúc bỏ PDF underlay ra khỏi ảnh này, nên
Claude thấy nền trắng.

**Cách làm:**

- Plugin dựng trang PDF thành PNG bằng **bộ đọc PDF có sẵn của Windows 10 / 11** (`Windows.Data.Pdf`), gọi qua Windows
  PowerShell 5.1 có sẵn trong Windows.
  - Không thêm thư viện nào, không đổi target. net48 / net8 / net10 dùng chung một đường.
  - Bộ cài **không tăng dung lượng** (0 MB).
  - File của khách không ra khỏi máy.
- Script ghi vào `%TEMP%\ClaudeCAD\pdf-trang\pdf-trang.ps1` và chạy với `-NoProfile -NonInteractive -ExecutionPolicy Bypass`.
- PNG được lưu tạm ở cùng thư mục, đặt tên theo đường dẫn, cỡ, ngày sửa, trang và độ rộng. Lần xem sau của cùng trang dùng
  lại file này.
- Độ rộng dựng = đủ nét cho phần đang xem, tròn bậc 256, trong khoảng 1024–6000 px. Ảnh xem vẫn ghép lưới tọa độ như cũ,
  báo `from: "pdf_page"`.
- **Đường lui giữ nguyên:** nếu Windows cũ, PowerShell bị khóa, PDF có mật khẩu, hoặc quá 30 giây, ảnh xem dùng ảnh chụp
  AutoCAD như trước. Khi ảnh vẫn trắng, cảnh báo ghi thêm lý do không đọc được PDF.
- SelfTest (`CLAUDECAD_SELFTEST`) thêm một bước: tạo PDF 1 trang, gắn, xem, rồi báo "PDF: Windows đọc thẳng trang PDF
  được" hoặc "KHÔNG đọc thẳng được (lý do)".
- File: `Core/PdfRender.cs` (phần không cần AutoCAD, có test), `Drawing/PdfPageImage.cs` (chạy tiến trình), `Drawing/TraceTools.cs`.

## 3. KFG-43: soát trùng chậm khi nhiều điểm cùng x, y khác z

Bảng băm của `DuplicateIndices` (Core/DrawingCheck.cs) chỉ dùng x, y. Vì vậy hàng nghìn điểm / block chồng theo z (nhiều tầng)
rơi vào cùng một ô và bị so từng cặp.

**Sửa:** hình có đúng 3 số (điểm, block) thì băm theo cả z. Kết quả không đổi: hai điểm khác z vốn không trùng.

**Test:** 20 000 điểm cùng x, y khác z soát xong dưới 1 giây.

## 4. Bản vá cũ của thread con F2 (`docs/ban-giao/f2-test-thread-con-ban-va.md`)

**Điểm 1** (liệt kê block thư viện dưới UCS xoay): **xác nhận đúng, không cần sửa thêm.**

- `insert_library_block` không có `name` được đăng ký là lệnh đọc (`readsWhen: p => !p.Has("name")`), nên chạy trong giao
  dịch chỉ đọc.
- Trong `RunHandler`, dòng `if (!ctx.Writable) return def.Handler(ctx, p);` đứng trước chỗ từ chối "cannot place new objects
  in the UCS without a transaction".
- Trong một batch có lệnh ghi khác, lệnh liệt kê chạy ở chế độ Create nhưng không thêm đối tượng nào, nên cũng không sao.
- Test đã có: `NangCapF2TestSuaTests.A_create_tool_called_only_to_read_runs_in_any_ucs`.

**Test đã chuyển sang** (bản F2 đã gộp chưa có), chạy với code hiện tại, không phải chỉnh API:

- `plugin/ClaudeCAD.Core.Tests/NangCapH1F2PatchTests.cs`, 14 test, đều qua:
  - chuỗi tỉ lệ ở biên, lỗi tỉ lệ có tên lệnh;
  - LTSCALE chỉ đặt khi còn mặc định;
  - kiểu kích thước khác, mỗi chỗ khác báo một lần;
  - tỉ lệ tự chọn lui về tỉ lệ lớn nhất và không mịn hơn cần;
  - ô khung tên khách đưa thay mặc định, giá trị quá dài được báo;
  - tên giấy / hướng giấy ở biên, khung nhìn không chồng khung tên;
  - block thư viện nằm trong cỡ của nó ở mọi cỡ hợp lệ (40 cỡ ngẫu nhiên mỗi block);
  - cỡ thư viện ở biên;
  - tên trạng thái layer ở biên;
  - thẻ từ chối ghi đè không đổi gì và ghi mã đối tượng.
- `agent/test/nangcaph1f2.test.ts`, 5 test, đều qua:
  - lời từ chối của plugin dừng lượt như của trợ lý (cả trong batch);
  - mọi tham số plugin đọc đều có trong schema;
  - schema nhận các câu trong checklist, từ chối giấy / action sai;
  - batch có một ghi đè đã duyệt và một chưa duyệt vẫn bị chặn;
  - batch chỉ tạo mới không bao giờ bị hỏi.

**Bỏ vì trùng ý** với test đã có:

- liệt kê dưới UCS;
- màu theo layer;
- so font theo typeface, font chỉ là tên file;
- hậu tố đơn vị kiểu kích thước, mọi `INSUNITS`;
- khung nhìn xoay;
- `layer_state list` là lệnh đọc;
- tên layout mặc định trong `F2Numbers`.

Phần checklist của bản vá: các mục A.7 / A.8 / B.8 / B.9 / C.11 / C.12 đã có trong checklist F2 hiện tại (do người rà F2
thêm), nên không chép lại.

## 5. File

- Mới:
  - `Core/SkillSync.cs`, `Core/PdfRender.cs`, `Drawing/PdfPageImage.cs`, `installer/skill-hashes.sh`;
  - test: `Core.Tests/NangCapH1Tests.cs`, `Core.Tests/NangCapH1F2PatchTests.cs`, `UI.Tests/NangCapH1PdfTests.cs`,
    `agent/test/nangcaph1f2.test.ts`;
  - tài liệu: `docs/checklist-dot-h1.md`, bàn giao này.
- Sửa: `installer/ClaudeCAD.iss`, `Core/UpdateLogic.cs`, `ChatController.cs`, `Drawing/TraceTools.cs`,
  `Core/DrawingCheck.cs`, `Core.Tests/ClaudeCAD.Core.Tests.csproj`, `bo-cai/README.md`.

## 6. Con số chờ chủ dự án duyệt

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 30 giây | `PdfRenderLogic.TimeoutSeconds`: chờ Windows dựng một trang PDF | Trang PDF quá nặng thì sau tối đa 30 giây ảnh xem lui về ảnh chụp AutoCAD; trong lúc chờ AutoCAD đứng | Trang scan A1 thường dựng 1–5 giây; 30 giây đủ cho máy chậm mà không treo quá lâu |
| 1024–6000 px, bậc 256 | `PdfRenderLogic.MinWidthPx / MaxWidthPx`, `RenderWidth`: độ rộng trang PDF dựng | Độ nét khi Claude xem phóng to một phần bản scan; 6000 px giới hạn bộ nhớ (~100 MB một trang) | 1024 = cỡ ảnh xem; bậc 256 để dùng lại ảnh đã dựng |
| `.ban-moi` | Tên file bản mới cạnh skill đã sửa (`SKILL.ban-moi.md`) | Khách thấy file này trong thư mục skill | Tiếng Việt không dấu, dễ hiểu, sắp cạnh file gốc |

Số chỉ trong test (không phải duyệt): 20 000 điểm < 1 s (KFG-43); 40 cỡ ngẫu nhiên mỗi block.

## 7. Quyết định tự chọn

- So "khách đã sửa chưa" ở plugin (C#, có test) lúc mở khung chat, không viết bằng Pascal trong bộ cài. Một cách chạy chung
  cho bộ cài và bản cập nhật trong app.
- Chỉ báo một lần mỗi bản mới: lưu mã đã báo trong `.ban-da-cai.json`. Bản mới khác nữa thì báo lại.
- File `.ban-moi` cũ được ghi đè bằng bản mới nhất (vẫn là file của plugin, không phải của khách).
- Skill bản mới không còn trong bộ cài (bị bỏ): không xóa ở máy khách.
- PDF đọc bằng Windows.Data.Pdf qua PowerShell, không thêm thư viện (PDFium / Docnet…). Lý do: 0 MB, không đổi target,
  không có giấy phép phải theo. Đổi lại: không chạy trên Windows 7 / 8, và chính sách nhóm (GPO) chặn PowerShell thì lui về
  ảnh chụp như cũ.
- Script chạy đồng bộ trên luồng của AutoCAD (tối đa 30 giây), giống ảnh chụp cũ, để khỏi phải sửa luồng gọi lệnh.
- KFG-43 chỉ băm thêm z cho hình đúng 3 số. Hình nhiều số vẫn băm như lượt 3 để không đổi kết quả.

## 8. Token

Không thêm lệnh, không đổi mô tả lệnh hay system prompt, nên không đổi token.

## 9. Test

- Lõi: 1472 / 1472 qua (gồm test H1 trong `NangCapH1Tests.cs` và 14 test bản vá F2).
- Trợ lý: 412 / 412 qua (thêm 5).
- Build 3 target (net48, net8.0-windows, net10.0-windows): qua.
- UI test Windows: `NangCapH1PdfTests` dựng trang PDF mẫu (đường dẫn có dấu cách và tiếng Việt) thành PNG 1024 px. Trang
  không có thì trả lý do.

## 10. CI

- Lượt 1: run 45775374409 trên `87d159e`, **đỏ**. Test lõi `Overwrite_refusals_never_change_anything_and_name_the_handles`
  tìm chuỗi có `\n` trong mã nguồn, nhưng bản checkout trên Windows dùng CRLF. Đã sửa: test đổi CRLF về LF trước khi tìm.
- Lượt 2: run 45776040553 trên `2fb37b7`, **xanh**. Lõi 1472 / 1472; UI test `NangCapH1PdfTests` qua (Windows dựng trang PDF mẫu thành PNG 1024 px trong 3 giây). Commit này chỉ thêm kết quả CI vào tài liệu, code giữ nguyên như `2fb37b7`.
- **Lỗi có sẵn, không thuộc H1** (báo luồng chính): trên Windows CI, 3 test trợ lý cũ hỏng từ trước
  (`nangcapf5.test.ts` "setup_standards makes it…", `nangcapg5.test.ts` "a new user message starts the count again",
  `plan-fixes.test.ts` "KT-42"). Run xanh 45710924398 của nhánh tích hợp đợt G cũng hỏng đúng 3 test này.
  - Hai test dựng đường dẫn từ `new URL(...).pathname`, ra `D:\D:\...`.
  - Một test dùng regex có `\n` trong khi file có CRLF.
  - Bước CI vẫn xanh vì PowerShell chỉ lấy mã thoát của lệnh cuối (`npm run build`), nên `npm test` hỏng không chặn CI.
  - H1 không sửa vì ngoài phạm vi; cần một thread sửa cả 3 test và cho bước CI dừng khi `npm test` hỏng.

## 11. Chưa kiểm được

- Chưa thử trên AutoCAD thật:
  - cài đè lên máy có skill đã sửa;
  - trace_image với PDF scan thật;
  - dòng PDF của SelfTest.
- Khung PDF underlay của AutoCAD có thể tính lề trang khác bản dựng của Windows, làm lưới lệch vài mm ở bản scan có lề. Xem
  checklist mục B.3.
- Phần mềm diệt virus có thể chặn PowerShell chạy script trong `%TEMP%`. Khi đó ảnh xem lui về ảnh chụp, SelfTest báo dòng
  "KHÔNG đọc thẳng được".
