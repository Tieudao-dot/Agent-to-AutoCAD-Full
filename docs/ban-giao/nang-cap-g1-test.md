# Bàn giao: Test & sửa đợt G1 — ra bộ hồ sơ (25/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà; người viết G1 là thread Nâng cấp đợt F2). Nhánh
`full/nang-cap-g1-test`, tách từ `full/nang-cap-g1` @ `dd29c9b` (code `28ca6c2`).

## 1. Lỗi tìm thấy và đã sửa

| Mã | Lỗi | Sửa |
| --- | --- | --- |
| TG1-1 | Mô hình hơn 10 tầng: bộ chỉ lấy 10 tầng dưới, **không nói** tầng nào bị bỏ. | `DrawingSetLogic.StoreyCount` đếm đủ tầng. Kết quả có ghi chú "mô hình có N tầng, bộ có mặt bằng của 10 tầng dưới". |
| TG1-2 | Khách cho tỉ lệ quá nhỏ (vd 1:20 trên A4), hoặc công trình lớn tới mức 1:5000 vẫn không vừa tờ: tờ chỉ thấy phần giữa hình, kết quả **không nói**. | `DrawingSetLogic.NotFitting` liệt kê các hình không vừa. Kết quả có ghi chú cho từng hình, gợi ý tỉ lệ / khổ lớn hơn. |
| TG1-3 | Bộ không có tờ in (máy gói 3D, chế độ 3D) thì mã thẻ "thay bộ cũ" giống hệt nhau với mọi bản vẽ, mọi lần chạy (chỉ tính từ tên layout, mà danh sách layout rỗng). | Mã thẻ tính thêm handle các hình bộ cũ đã vẽ (`ReplaceToken(layouts, handles)`). Bộ có tờ in: mã vẫn khác theo hình. |
| TG1-4 | Khách đưa một ô khung tên lạ trong `fields`: mọi `create_sheet` đều lỗi **sau khi** bộ cũ đã bị xóa, nên mất bộ cũ mà không có bộ mới. | Kiểm ô khung tên **trước** mọi thay đổi (`DrawingSetLogic.UnknownFields`); sai thì từ chối, bản vẽ không đổi. |

## 2. Đã rà 9 điểm, đúng, không phải sửa

1. **Gói / chế độ:**
   - Plugin dùng gói hiệu lực theo chế độ (`ChatController.Plan` = `EffectivePlan`) và chế độ đang giới hạn.
   - Máy 2D: chỉ tờ mặt bằng 2D (+ PDF).
   - Máy 3D: chỉ các hình trong Model.
   - Full "Cả hai": đủ.
   - Full chế độ 2D / 3D: phần thiếu nói "đổi chế độ sang Cả hai", không nói gói.
   - Phần thiếu luôn nằm trong `missing`, kèm ghi chú.
2. **Tầng:**
   - Nhà 1 tầng ghi tiêu đề "MẶT BẰNG"; nhiều tầng ghi "MẶT BẰNG TẦNG n".
   - Không có khối tường: một mặt bằng ở đáy mô hình. Không có khối 3D: mặt bằng 2D.
   - Mảnh lanh tô không tính là tầng.
   - Quá 30 tờ: từ chối, chưa làm gì. 10 tầng: xem TG1-1.
3. **Tỉ lệ:**
   - Mặc định 1:100; to hơn khi không vừa khung.
   - Chung một tỉ lệ cho các mặt bằng, một tỉ lệ cho mặt đứng + mặt cắt.
   - Bản vẽ mm / cm / m ra cùng tỉ lệ (test mới).
   - Hình rất lớn: xem TG1-2. Hình rất nhỏ vẫn 1:100 (theo bảng số của G1).
4. **Chạy lại:**
   - Thay bộ cũ chỉ sau thẻ (mã của bộ + mã file PDF). Hủy thì không đổi gì: từ chối xảy ra trước mọi thay đổi.
   - Không đụng layout của khách; layout trùng tên thì từ chối và gợi ý tiền tố.
   - Bộ cũ là layout duy nhất: tạo `Layout1` trước khi xóa.
5. **Chế độ, macro, UCS:**
   - "Chỉ xem" chặn (lệnh ghi).
   - Macro không thay bộ đã có.
   - UCS xoay: lệnh lớp Ref; điểm cắt và vùng mặt bằng đọc thô rồi đổi một lần, không đổi hai lần.
   - UCS không nằm ngang: dùng trục thế giới, có ghi chú.
6. **Khung tên:**
   - Tên, số bản vẽ, tỉ lệ, khổ do plugin điền và thắng giá trị khách đưa.
   - Phối cảnh ghi tỉ lệ "—".
   - Chính tả tiêu đề đúng (test mới).
   - Ô lạ: xem TG1-4.
7. **PDF:**
   - Một file `<bản vẽ>-bo-ho-so.pdf`, trang theo thứ tự KT-01…
   - Ghi đè dùng cùng mã file như `plot_pdf` (F5), nên thẻ ở lệnh này cũng mở được bước in.
8. **Nạp gọn:** lệnh ngoài nhóm nạp sẵn (test của G1); tắt nạp gọn vẫn có lệnh.
9. **Bảng số và quyết định:** từng dòng ở mục 3, 4 của `nang-cap-g1.md` khớp code. Thiếu hai giới hạn khách có thể gặp, bổ sung ở
   mục 3 dưới đây.

## 3. Con số chờ chủ dự án duyệt (bổ sung)

| Số | Ở đâu | Khách thấy gì | Vì sao |
| --- | --- | --- | --- |
| Tối đa **8** đường cắt khách chỉ, **10** vùng mặt bằng 2D mỗi lần | `agent/src/tools/drawingset.ts` (`sections .max(8)`, `plan_areas .max(10)`) | nhờ nhiều hơn thì Claude chia lần | có sẵn trong code G1, chưa ghi trong bảng |
| Mặt bằng / mặt đứng / mặt cắt dùng tối đa **500** khối 3D (giới hạn `section_view`), có ghi chú khi nhiều hơn | `SectionLogic.MaxSolids` | nhà rất nhiều khối thì hình thiếu khối, có ghi chú | G1 xếp vào "số kỹ thuật", nhưng khách thấy được hình thiếu nên đưa vào bảng |

Không đổi số nào của G1.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Hình không vừa tờ ở tỉ lệ khách cho | vẫn làm theo tỉ lệ khách cho, ghi chú để Claude báo | tự nâng tỉ lệ (bỏ qua ý khách): sửa `ChooseScales` |
| Quá 10 tầng | vẫn ra 10 tầng dưới, ghi chú | từ chối cả bộ |

## 5. Test

- Thêm `plugin/ClaudeCAD.Core.Tests/NangCapG1TestSuaTests.cs` (9 test):
  - 4 lỗi trên;
  - Full ở chế độ 2D / 3D;
  - bản vẽ không có khối 3D / không có gì;
  - giới hạn 30 tờ;
  - đơn vị mm / cm / m;
  - chính tả tiêu đề, tên PDF.
- Sửa 1 dòng test mã nguồn trong `NangCapG1Tests.cs` theo mã thẻ mới (ý của test giữ nguyên).
- `docs/checklist-dot-g1.md`: thêm C9 (tỉ lệ quá nhỏ).
- Kết quả sau lần sửa cuối và CI: xem mục 6.

## 6. CI

Xem báo cáo gửi luồng chính.

## 7. Chưa kiểm được

- Chưa chạy trên AutoCAD thật. Thử tay theo `docs/checklist-dot-g1.md`:
  - hình cắt / chiếu, phối cảnh, khung nhìn xoay theo UCS, layer đóng băng theo khung;
  - PDF, thẻ chạy lại, macro;
  - bước tự kiểm G1.
- Tốc độ với nhà nhiều khối.
- Lỗi lạ (không phải `ToolException`) giữa chừng sau khi đã xóa bộ cũ: bộ cũ đã mất, bộ mới làm dở. U một lần hoàn tác cả
  lượt. Không sửa vì cần AutoCAD thật để tái hiện.
