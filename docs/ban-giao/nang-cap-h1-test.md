# Bàn giao: Test & sửa đợt H1 (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người rà; người viết H1 là thread đợt F1). Nhánh `full/nang-cap-h1-test`, tách từ
`full/nang-cap-h1` @ `8ad7d9d` (code `2fb37b7`). Chạy cùng lúc với rà H2 (thread F2): chỉ sửa file của H1, file dùng chung
chỉ đụng `ChatController.cs` (hàm H1 đã thêm) và thêm 1 test mới. Chưa thử trên AutoCAD thật.

## 1. Đã soát (5 điểm được giao)

| Điểm | Kết quả |
| --- | --- |
| 1. Skill khi nâng cấp | Danh sách `KnownShipped` (47 mã) phủ đủ mọi bản trong lịch sử git của `skills/` (chạy lại `installer/skill-hashes.sh`: 46 mã, đều có trong code; mã thừa không hại). Mã băm bỏ BOM / CR: đúng. `SKILL.ban-moi.md` không bị nạp làm skill (danh sách skill chỉ đọc `SKILL.md`). Bộ cài và bản cập nhật trong app làm giống nhau: robocopy `/PURGE` xóa `skills-ban-moi` cũ rồi `/MIR` chép lại; `[InstallDelete]` + `[Files]` trong bộ cài. Gỡ cài giữ `skills`. **2 lỗi** (H1T-01, H1T-02). |
| 2. Đọc trang PDF bằng Windows | Một đường cho net48 / net8 / net10. PDF mật khẩu, hỏng, trang không có, PowerShell bị chặn: tiến trình thoát lỗi, lui về ảnh chụp AutoCAD, cảnh báo ghi lý do. Không gửi gì ra ngoài. Tỉ lệ: ảnh đặt theo khung góc của PDF lúc gắn, không theo cỡ PNG, nên độ rộng dựng không làm đổi tỉ lệ. **2 lỗi** (H1T-03, H1T-04). |
| 3. KFG-43 | Đúng: hình 3 số (điểm, block) băm theo cả z; hai điểm khác z không trùng, cả 3 số bằng nhau thì trùng (test có sẵn kiểm cả hai). Hình khác (vòng tròn, chữ, cung) vốn đã băm z. Đường thẳng chồng nhiều tầng vẫn băm x, y hai đầu: chỉ chậm khi hàng nghìn đường trùng x, y khác z, hiếm, không sửa. |
| 4. Test chuyển từ bản vá F2 | 14 test lõi + 5 test trợ lý chạy thật, kiểm đúng hàm hiện tại (không chỉ kiểm chuỗi). |
| 5. Bảng con số | Khớp code: 30 giây, 1024–6000 px bậc 256, tên `.ban-moi`. Thêm 1 dòng cho tên mới `.ban-moi-2` (mục 4). |

## 2. Lỗi đã sửa

| Mã | Mức | Lỗi | Sửa | Test |
| --- | --- | --- | --- | --- |
| H1T-01 | **Nặng** | Dòng báo "Skill … có bản mới" không bao giờ hiện: nó được thêm trước khi khung chat nạp lịch sử bản vẽ (`LoadTranscriptFor` → `ClearHistory`), bị xóa ngay; bản ghi đã đánh dấu "đã báo" nên không báo lại | `SyncShippedSkills` trả về dòng báo; `ShowPalette` thêm dòng sau khi nạp lịch sử | `NangCapH1TestSuaTests.The_skill_notice_is_added_after_the_drawing_chat_is_loaded` |
| H1T-02 | Vừa | Khách sửa (gộp dần) trong `SKILL.ban-moi.md`, bản sau ghi đè mất phần khách gộp | File `.ban-moi` chỉ được ghi lại khi vẫn là bản plugin đặt; khách đã sửa thì bản mới sang `SKILL.ban-moi-2.md` (rồi `-3`…), báo một lần | `A_side_file_the_user_edited_is_kept_…`, `A_side_file_left_as_it_was_…`, `Side_names_are_never_loaded_…` |
| H1T-03 | Vừa | Dựng PDF quá 30 giây bị dừng giữa chừng để lại PNG dở dưới tên ảnh lưu; lần xem sau dùng lại ảnh hỏng mãi (tên theo file + trang + độ rộng) | PowerShell ghi vào tên tạm `….dang-ve-<mã>.png`, chỉ đổi sang tên lưu khi thoát tốt; bị dừng / lỗi thì xóa tên tạm | `A_half_render_never_has_the_cache_name`; UI test Windows thêm PDF hỏng + không còn file dở |
| H1T-04 | Nhẹ | Thư mục `%TEMP%\ClaudeCAD\pdf-trang` không bao giờ được dọn (mỗi trang tới 6000 px) | Mỗi lần dựng: xóa ảnh không dùng quá 7 ngày, file dở quá 1 giờ; ảnh dùng lại được làm mới ngày. Chỉ xóa file `pdf-*.png` | `Old_renders_and_half_renders_are_cleaned_and_nothing_else` |

File sửa: `ChatController.cs`, `Core/SkillSync.cs`, `Core/PdfRender.cs`, `Drawing/PdfPageImage.cs`; test mới
`Core.Tests/NangCapH1TestSuaTests.cs` (6), thêm vào `UI.Tests/NangCapH1PdfTests.cs`; `docs/checklist-dot-h1.md` (A.2, A.7,
B.4), bàn giao này.

## 3. Nhận xét, không sửa

- Trang PDF có lề / CropBox khác MediaBox: Windows dựng theo CropBox; nếu AutoCAD dùng khung khác thì ảnh bị kéo nhẹ. Cần
  thử tay (checklist B.3 đã hỏi độ lệch).
- Dựng PDF chạy trên luồng AutoCAD, đứng tối đa 30 giây (quyết định tự chọn của H1, giữ nguyên).
- Hai AutoCAD dựng cùng lúc có thể tranh ghi `pdf-trang.ps1`: bên thua lui về ảnh chụp, không hỏng gì.
- Skill `README.md` ở gốc nếu khách sửa thì được báo là "Skill README có bản mới": đúng ý, hơi lạ chữ.

## 4. Con số chờ chủ dự án duyệt

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| `-2`, `-3`… | `SkillSync.SidePath(path, n)`: tên bản mới khi khách đã sửa cả `SKILL.ban-moi.md` | Thấy `SKILL.ban-moi-2.md` cạnh hai file kia | Không ghi đè phần khách đã gộp; tên vẫn có `.ban-moi` nên dòng báo vẫn đúng |

Số bên trong, khách không thấy (không phải duyệt): ảnh PDF lưu tạm giữ 7 ngày từ lần dùng cuối, file dở xóa sau 1 giờ
(`PdfRenderLogic.CacheKeepDays`, `PartialKeepHours`); tối đa 99 tên `.ban-moi-n`.

## 5. Quyết định tự chọn

- Sửa H1T-01 bằng cách dời chỗ hiện dòng báo, không đổi cách ghi "đã báo" (vẫn chỉ báo một lần mỗi bản mới).
- H1T-02: không đặt tên theo ngày; dùng số tăng dần, dễ đọc và sắp cạnh nhau.

## 6. Test

- Lõi: 1478 / 1478 qua (thêm 6).
- Trợ lý: 412 / 412 qua (`dist-test` cũ của nhánh H2 làm hỏng 4 test giả lúc đầu; xóa thư mục sinh ra rồi chạy lại).
- Build 3 target (net48, net8.0-windows, net10.0-windows): qua. UI.Tests build qua trên Linux.

## 7. CI

- Lượt 1: run 45779651447 trên `67ac8e0`, **xanh**.
  - Lõi 1478 / 1478.
  - UI test 38 / 38, gồm `NangCapH1PdfTests`. Windows dựng trang mẫu trong 3 giây. PDF hỏng trả lý do "Windows could not
    render the page (…)". Lần xem lại dùng đúng ảnh cũ. Không còn file dở.
  - Log trợ lý: `# pass 409`, `# fail 3`. Đó đúng là 3 lỗi cũ đã biết (`F5: multileader of the standard`,
    `G5: looks at a traced picture`, `KT-42: …`), không có lỗi mới.
- Commit này chỉ ghi kết quả CI vào tài liệu, code giữ nguyên như `67ac8e0`.
