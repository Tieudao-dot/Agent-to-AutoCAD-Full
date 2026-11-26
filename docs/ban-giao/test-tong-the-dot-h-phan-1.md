# Bàn giao: Test tổng thể đợt H phần 1 (H1 → H5) (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh`. Nhánh `full/nang-cap-h-tong-the`, tách từ `full/nang-cap-h5-test` @ `9c0c39a` (code
`46b93f2`).
- Đã gộp tài liệu luồng chính từ `origin/claude/inspiring-cray-58xsmt` @ `8fa2af4`: `CLAUDE.md`, kế hoạch, tổng quan, chờ duyệt
  F / G, số tiêu chuẩn H, các báo cáo. Không xóa file nào; các file luồng chính cũ hơn giữ bản trên nhánh code.
- Thread này **giữ nhánh tích hợp** cho các bản sửa sau kiểm tra độc lập.

Chưa thử trên AutoCAD thật.

## 1. Đã rà chéo

| Điểm | Kết quả |
| --- | --- |
| `OtherDrawing.Open` dùng chung (H3 so sánh, H4 học chuẩn + bản chép mẫu, H5 soát / đếm) | Cùng luật đường dẫn (G5), `.dwt` (H4), 200 MB, lời báo khóa / đời mới hơn / hỏng. **TTH-1**: áp chuẩn hàng loạt (H5) đọc file riêng, chưa có kiểm đầu file; đã sửa |
| `company` (H4) qua `setup_standards` / `check_drawing` / `create_sheet` / `batch_files` | Bộ chuẩn đọc ở bước lập kế hoạch (bộ hỏng H4T-1 dừng ngay, rõ lý do); áp chuẩn công ty trên database bên cạnh dùng `ctx.Db` / `ctx.Tr` của file đó (`ApplyLayer`, `CloneFrom`, đơn vị); soát theo chuẩn công ty không lẫn chuẩn VN; `fix_drawing company` từ chối nhóm theo chuẩn |
| `export_drawing` (H2) và thẻ ghi đè F5 | File có sẵn → thẻ (mã theo đường dẫn chuẩn hóa); in PDF hàng loạt: thẻ của lượt nói số file bị thay; Excel tổng hợp H5 có ngày giờ trong tên, không ghi đè |
| `trace_image` đọc PDF (H1) với G5 | PDF mạng đã được chép về máy lúc gắn (KFG-29 / 40), dựng trang dùng bản chép; không có đường mới ra máy khác |
| Skill nâng cấp (H1) với bộ cài | `[InstallDelete]` + `[Files]` `skills-ban-moi`, updater robocopy `/MIR`; file `.ban-moi(-n)` không bị nạp như skill |
| Chặn gói / chế độ / "Chỉ xem" cho 5 lệnh mới | `export_drawing`, `compare_drawings`: Common (FBX, khối 3D theo gói); `company_standard`, `batch_files`, `trace_image`: 2D. Chỉ xem: so (không khoanh), xuất, học chuẩn, soát / đếm / in hàng loạt được; khoanh mây và áp chuẩn hàng loạt bị từ chối. `batch_files` kiểm gói của lệnh con |
| Nạp gọn lệnh (F4) | Cả 5 lệnh hoãn nạp; lệnh nạp sẵn không đổi (19 / 22 / 25); prompt gói 3D không nhắc lệnh 2D (`company_standard`, `batch_files`) |
| UCS (F1) | **TTH-2**: `export_drawing`, `company_standard`, `batch_files` chạy ở chế độ "đổi theo UCS" dù không có tọa độ; đã sửa |
| Nhãn, thẻ, Excel | Đủ nhãn tiếng Việt; 4 lệnh ghi file đều trong `F5_CARD_TOOLS`; tên sheet / file Excel cho `compare_drawings` |
| SelfTest | Có bước cho H2, H3 (2 bước), H4, H5; H1: dòng PDF trong bước G5 |

## 2. Lỗi chéo đã sửa

| Mã | Lỗi | Khách thấy gì trước khi sửa | Sửa | Test |
| --- | --- | --- | --- | --- |
| TTH-1 | `batch_files` áp chuẩn đọc file bằng `ReadDwgFile` riêng, không qua kiểm đầu file của phần đọc chung (H3); sao lưu trước khi biết file đọc được | File lưu bằng AutoCAD đời mới hơn, hoặc file hỏng đổi tên `.dwg`: dòng lỗi "AutoCAD: <mã lỗi>" khó hiểu, và có bản sao lưu thừa của file không ghi được | Tách `OtherDrawing.RequireReadableHeader` (dùng chung cho `Open`); `BatchStandards` gọi nó **trước** sao lưu; lỗi đọc AutoCAD đổi qua `OtherDrawingLogic.ReadFailure` | `TestTongTheDotHTests.Batch_setup_checks_the_header_…`, `Every_reader_of_another_drawing_goes_through_the_shared_reader` |
| TTH-2 | `export_drawing`, `company_standard`, `batch_files` không trong `UcsLogic.WcsTools` → chế độ Ref | Khách xoay UCS rồi xuất / học chuẩn / làm nhiều file: kết quả kèm câu "All points here are in UCS…" vô nghĩa; các lệnh con của `batch_files` chạy cho file khác dưới UCS của bản vẽ đang mở | Thêm ba lệnh vào `WcsTools` | `The_file_tools_of_h_never_turn_numbers_into_the_ucs` |
| TTH-3 (tài liệu) | Checklist H1, H3 và bàn giao H1 ghi lệnh tự kiểm là `CLAUDECAD_SELFTEST` (không có lệnh này) | Gõ theo checklist thì AutoCAD báo không có lệnh | Sửa thành `CLAUDECHATSELFTEST` | — |

**Sửa `Drawing/OtherDrawing.cs`** (phần dùng chung): tách hàm kiểm đầu file, hành vi `Open` không đổi. Sửa một test H3 cũ cho khớp
(thứ tự kiểm trước đọc vẫn được kiểm).

## 3. Token lúc mở chat (ước tính)

Cách đo như test tổng thể đợt F / G: tên + mô tả + schema các lệnh nạp đủ, tên các lệnh hoãn nạp, ToolSearch ~2 000 ký tự, system
prompt; khoảng 3,5 ký tự / token. Cuối đợt G = `547d6af`; cuối đợt H phần 1 = nhánh này. Đo bằng cùng một script cho cả hai
mốc, nên số tuyệt đối có thể lệch chút so với bảng của đợt G (bảng đó tính lại theo cách cũ), còn phần chênh thì so sánh được.

| Gói / chế độ | Cuối G, nạp gọn | Cuối H1, nạp gọn | Cuối G, tắt nạp gọn | Cuối H1, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 12,0k | 12,2k | 44,2k | 46,6k |
| Gói 3D | 17,4k | 17,5k | 43,9k | 45,1k |
| Full · Cả hai | 19,2k | 19,3k | 65,5k | 67,9k |
| Full · chế độ 2D | 14,1k | 14,3k | 46,3k | 48,7k |
| Full · chế độ 3D | 17,4k | 17,5k | 43,8k | 45,1k |

- Số lệnh: 2D 109 → 113, 3D 94 → 96, Full 142 → 146.
- Lệnh nạp sẵn lúc mở chat không đổi (19 / 22 / 25).
- Lúc mở chat chỉ thêm khoảng 0,1 – 0,2k token (4 dòng prompt + tên lệnh hoãn nạp).

## 4. Checklist thử tay

Gộp `checklist-dot-h1.md` … `-h5.md` vào khối **"Đợt H phần 1"** cuối `docs/checklist-tong-the.md` (~3 giờ). Các file riêng thêm
dòng trỏ sang.

- **H0** (làm trước, ~40 phút), gom các điểm rủi ro nhất:
  - bước tự kiểm (gồm dòng PDF của H1);
  - FBX mở bằng Blender / 3ds Max;
  - sao lưu + ghi file hàng loạt;
  - PDF scan thật;
  - skill khi cài đè;
  - khung tên công ty.
- **H6** (chéo): TTH-1, TTH-2, `.dwt` khi so, soát hàng loạt theo chuẩn công ty, máy gói 3D.

## 5. `docs/dot-h-cho-duyet.md`

Mới: gom bảng "Con số chờ chủ dự án duyệt" và các "Quyết định tự chọn" chính của H1 → H5, các lượt rà, sửa CI, test tổng thể.
Đã soát từng số với code:
- `PdfRenderLogic`, `SkillSync`;
- `H2Numbers` / `ExportLogic.Versions`;
- `H3Numbers`, `H4Numbers`, `H5Numbers`, `OtherDrawingNumbers`, `BatchDocSwitches.KeepSeconds`.

Tất cả khớp. Mục "Số tiêu chuẩn phần 2" chỉ trỏ tới `docs/dot-h-so-tieu-chuan.md`.

## 6. Test

- Lõi: thêm `TestTongTheDotHTests.cs` (5 test).
- Trợ lý: không đổi mã.
- Chạy lại sau lần sửa cuối: lõi **1581 / 1581**, trợ lý **436 / 436**, build net48 / net8.0-windows / net10.0-windows qua (xóa `obj` / `bin` giữa các lần).

## 7. CI

Run 45802392263 trên `9612510`: **xanh**, 1 / 2 lượt. Log Windows: trợ lý `# tests 436`, `# pass 436`, `# fail 0`; lõi 1581 / 1581; UI test và build 3 target qua. Commit sau đó chỉ ghi mục này, code giữ nguyên như `9612510`.

## 8. Chưa kiểm được

Mọi mục "chưa kiểm được" của H1 → H5 (cần AutoCAD thật), gom ở H0 của checklist tổng.

## 9. Sau kiểm tra độc lập (sửa KTH, 26/11)

Báo cáo `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md`: 27 lỗi KTH (2 nặng, 13 vừa, 12 nhẹ). Ba phần sửa gộp trên nhánh này:

| Phần | Lỗi | Thread | Nhánh / commit | Bàn giao |
| --- | --- | --- | --- | --- |
| A: H4, prompt | KTH-09 → 14, KTH-27 + 2 nghi vấn H4 | thread này | ngay trên nhánh, `b2e7549` | `nang-cap-h-sua-kth-a.md` |
| B: H1 / H2 / H3 | KTH-15 → 26 + nghi vấn `compare.ts` | Test & sửa F1 | `full/nang-cap-h-sua-kth-b` @ `bf21eee` | `nang-cap-h-sua-kth-b.md` |
| C: H5 | KTH-01 → 08 + 2 nghi vấn (macro, thời gian chờ) | đợt F2 | `full/nang-cap-h-sua-kth-c` @ `5ce5296` | `nang-cap-h-sua-kth-c.md` |

- Gộp C rồi B: không có chỗ chồng.
- `docs/dot-h-cho-duyet.md` thêm mục "Sau kiểm tra độc lập":
  - 5 000 chỗ khác (H3);
  - thời gian chờ 5 phút / 2 phút;
  - các quyết định mới.
- Checklist H4 thêm F9 → F13; các thread B / C đã cập nhật checklist H1 / H5 và khối tương ứng trong checklist tổng.
- Test sau gộp: lõi **1605 / 1605**, trợ lý **441 / 441**, build 3 target qua.
- CI: run 45810078065 trên `0275e38`: **xanh**, 1 / 2 lượt; log Windows `# tests 441`, `# pass 441`, `# fail 0`; lõi 1605 / 1605; UI test và build 3 target qua. Commit sau đó chỉ ghi dòng này, code giữ nguyên như `0275e38`.

