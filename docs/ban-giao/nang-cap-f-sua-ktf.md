# Bàn giao: sửa lỗi kiểm tra độc lập đợt F — phần F1 (UCS) và F4 (Excel, khởi động sẵn)

Nhánh `full/nang-cap-f-sua-ktf`, từ `full/nang-cap-f-tong-the` @ `afdaa68`. Người sửa: thread đã viết F1 / F4
(`session_01D56HmFAo4dZ7c8sbeNB9eh`), luồng chính giao 25/11/2026. Báo cáo kiểm tra độc lập: thread
`session_01D56HmHz4EXXJceBgkmjcap`. Phần F2 / F3 / tài liệu do thread giữ nhánh tích hợp sửa. Theo giao việc: không chạy CI ở
nhánh này (thread tích hợp gộp rồi chạy CI). Chưa thử trên AutoCAD thật.

## Lỗi đã sửa

| Mã | Lỗi (khách thấy gì) | Sửa | Test giữ lại |
| --- | --- | --- | --- |
| KTF-02 (vừa, F4) | File Excel: cột diện tích m², thể tích m³, thể tích va chạm là **chữ** "12,50" (bảng trong bản vẽ dùng dấu phẩy), Excel không cộng được | `XlsxLogic.ForExcel`: trong cột có đơn vị "(m²)", "(m³)", "(m)" ở tiêu đề, ô dạng "12,50" thành số 12.5 trước khi ghi; ô chữ ("chưa đo được", "3 cặp") giữ nguyên. Bảng trong bản vẽ **không đổi** | `Ktf02_*` (4 test): diện tích, khối lượng, va chạm, ô `<v>12.5</v>` |
| KTF-03 (vừa, F1) | UCS xoay góc không vuông (vd 30°): chọn theo khung (`list_entities` window, `build_3d_from_plan` area_min / area_max) **bỏ sót** hình nằm trọn trong khung (crossing thì chọn thừa), vì khung bao WCS bao lại theo UCS bị phình | Lấy khung bao của **chính hình** trong UCS: bản sao hình xoay vào UCS rồi đo (`Acad.ExtentsInUcs`); không sao được thì về cách cũ. Lõi: `UcsFrame.BoxOfPoints`, `UcsFrame.UcsBoxInside` | `Ktf03_*` (2 test): đường dài 100 dọc trục X của UCS 30° nằm trong khung (−10,−10)…(110,10) |
| KTF-04 (vừa, F1) | Bản vẽ đơn vị mét, UCS dời lên z = 3,3: `base_z: 0` (mm) đọc ra 3,3 mm thay vì 3300 mm (cộng thẳng gốc z đơn vị bản vẽ vào số mm) | `UcsScope` biết mm mỗi đơn vị của lệnh đang chạy (bộ điều phối truyền từ INSUNITS); cao độ = số mm + gốc z × mm/đơn vị | `Ktf04_a_metre_drawing_adds_the_ucs_origin_z_in_mm` |
| KTF-05 (nhẹ, F1) | (a) Bỏ trống `base_z` dựng ở z WCS 0, ghi `base_z: 0` dựng ở gốc z UCS. (b) Bản theo hình có sẵn của lan can (`path_handle`), cầu thang (`well_handle`), dầm (`path_handles`) không cộng gốc z UCS vào `base_z` / `top_z` | (a) `Params.Num(name, mặc định)` cho mặc định qua cùng phép đổi UCS như số được ghi. (b) Thêm `build_railing.base_z`, `build_stair.base_z`, `build_beam.top_z` vào `UcsLogic.HeightParams` (bản vẽ mới của các lệnh này vốn đã theo UCS qua phép biến đổi, không bị cộng hai lần) | `Ktf05_*` (4 test) |
| KTF-06 (nhẹ, F4) | `block_schedule` không có block vẫn ghi file Excel chỉ có dòng "Tổng 0"; `excel.rows` tính cả dòng Tổng (3 block báo 4) | `XlsxLogic.DataRows`: số dòng dữ liệu (bỏ tiêu đề và dòng "Tổng"); 0 dòng → báo, không tạo file | `Ktf06_*` |

## Nghi vấn đã kiểm

| Nghi vấn | Kết luận | Xử lý |
| --- | --- | --- |
| AutoCAD chết mà không đóng pipe: trợ lý thoát mà không đóng tiến trình Claude chạy sẵn | Đúng (nhánh "parent process gone" gọi `process.exit(0)` ngay) | Gọi `session.close()` trước, thoát sau 200 ms như khi pipe đóng. Test `Without_autocad_the_assistant_closes_claude_before_exiting` |
| "Chuyển bản vẽ thì khởi động sẵn lại" nhưng plugin không gửi prewarm mới và prewarm dùng skill của chat trước | Đúng một phần: trợ lý có tự khởi động sẵn lại sau `switch_session` / `new_session`, nhưng với skill của chat trước (tin đầu khác skill thì khởi động lại, mất phần lợi) | Plugin gửi prewarm (gói / chế độ / **skill của chat mới**) sau "chuyển bản vẽ" và "Chat mới" — code khớp tài liệu. Test `Another_drawing_or_a_new_chat_is_prewarmed_with_its_own_skills` |
| Ghi đè Excel = `File.Delete` rồi `Move`, hỏng giữa chừng mất file gốc | Đúng | `File.Replace(temp, path, null)` (một bước; lỗi thì file gốc còn nguyên); file chưa có thì `Move` như cũ. Chỉ sửa đúng chỗ ghi file (`XlsxWriter.Write`), không đụng luồng hỏi ghi đè mà đợt F5 đang đổi. Test `An_overwritten_excel_file_is_replaced_in_one_step` |
| "Chuỗi số dài / 1.10 bị đổi thành số"; "bảng thống kê không xoay theo UCS" | Theo giao việc: không sửa (quyết định chờ chủ dự án duyệt / giới hạn F1 đã ghi) | — |

## Quyết định tự chọn

| Tình huống | Đã chọn (rẻ, an toàn, đổi lại được) |
| --- | --- |
| KTF-02: đổi số ở đâu | ở bước xuất Excel, chỉ các cột có đơn vị trong tiêu đề; bảng trong bản vẽ giữ dấu phẩy như cũ |
| KTF-03: cách lấy khung bao trong UCS | bản sao hình xoay vào UCS rồi lấy khung (đúng cho mọi loại hình AutoCAD đo được); lỗi thì về khung cũ (chọn thừa / sót như trước, không hỏng lệnh) |
| KTF-05 (a) | bỏ trống = 0 theo UCS (cùng nghĩa với ghi 0), như AutoCAD hiểu cao độ theo UCS hiện hành |
| Test cũ `NangCapF1TestSuaTests.Build_3d_from_plan_tests_its_area_in_the_ucs` | 2 dòng kiểm chữ trong code (`PlanBoxInside(`, `InArea(EntityInfo.Extents(e), …)`) khóa đúng cách cũ đang sửa: đổi sang cách mới (`UcsBoxInside(…)`, `InArea(e, …)`); ý của test (vùng chọn thử theo UCS, World giữ nguyên) giữ nguyên. Không test nào bị bỏ / tắt |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1150** (1132 ở `afdaa68` + 18 `NangCapFSuaKtfTests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **355** (không đổi số; phần trợ lý sửa được kiểm bằng test nguồn trong lõi).
- Build 3 target (net48, net8.0-windows, net10.0-windows; xóa obj / bin giữa các lần; `-p:EnableWindowsTargeting=true`): 0 lỗi.
- CI: không chạy ở nhánh này (theo giao việc).

## Chưa kiểm được

- Trên AutoCAD thật: khung bao qua bản sao xoay (`GetTransformedCopy`) với mọi loại hình (block, hatch, chữ…), cao độ trong bản
  vẽ mét có UCS dời, mở file Excel bằng Excel, tiến trình Claude không còn sót khi AutoCAD bị tắt ngang.
