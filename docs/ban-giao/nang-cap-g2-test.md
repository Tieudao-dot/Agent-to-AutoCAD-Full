# Bàn giao: Test & sửa đợt G2 (người rà)

Nhánh `full/nang-cap-g2-test`, tách từ `full/nang-cap-g2` @ `8bbc27c` (code `aa91157`). Rà `check_drawing` / `fix_drawing`:
lõi `Core/DrawingCheck.cs`, phần AutoCAD `Drawing/DrawingCheckTools.cs`, trợ lý (`drawingCheck.ts`, `index.ts`,
`confirmation.ts`, system prompt, skill). Chưa thử trên AutoCAD thật.

## Lỗi tìm được và đã sửa

| # | Lỗi | Mức | Sửa |
| --- | --- | --- | --- |
| G2T-1 | **Lạc xa bỏ sót ở bản vẽ ít hình** (10–19 hình): "phần chính" lấy theo 5 % – 95 % có nội suy, nên với một hình lạc xa, mốc 95 % nằm giữa đường tới hình đó → phần chính phình to, hình lạc **không bao giờ** được báo | Trung bình | Mốc làm tròn vào trong, không nội suy về phía hình lạc (`DrawingCheckLogic.RankInward`). Bản vẽ nhiều hình: kết quả như cũ |
| G2T-2 | **Định nghĩa thuộc tính block** (`ATTDEF`) đặt trong mô hình, giá trị mặc định trống, bị tính là "Chữ rỗng" và `fix_drawing empty_text` **xóa luôn** | Trung bình | Bỏ định nghĩa thuộc tính khỏi các nhóm chữ (`Txt`) |
| G2T-3 | **Xref khách chủ động tạm gỡ** (Unload), xref không dùng, ảnh đã gỡ mà file vẫn còn bị báo "Xref / ảnh mất file" (mức nên sửa) | Nhỏ | Chỉ báo khi AutoCAD không tìm thấy file (xref `FileNotFound` / `Unresolved`; ảnh chưa nạp và file không có) (`XrefMissing`, `ImageFileFound`) |
| G2T-4 | `fix_drawing` trên bản vẽ lớn hơn giới hạn quét (20 000) chỉ sửa trong phần đã quét mà không nói (kết quả `check_drawing` thì có nói) | Nhỏ | Kết quả sửa có `scanned_note` "chỉ xem N trên M, soát lại sau khi sửa" |

Không có điểm cần chủ dự án chốt.

## 8 điểm đã rà

1. **Báo nhầm / bỏ sót:** G2T-1, G2T-2, G2T-3 ở trên. Bản vẽ không dùng bộ chuẩn: tên layer / kiểu / cỡ chữ bỏ qua, có ghi
   chú (đúng). Bản vẽ trống: không lỗi, "No problem found". Trùng: so cùng loại, cùng layer, cùng không gian, làm tròn theo
   0,1 mm (hai số sát nhau mà nằm hai bên mốc làm tròn có thể bỏ sót, hiếm; không bao giờ báo nhầm hình lệch quá 0,1 mm).
2. **Thẻ:** mã gồm tên việc sửa + layer đích + đúng tập đối tượng (sắp xếp, không phân biệt hoa thường); đổi việc / layer /
   thêm bớt một đối tượng → mã khác → hỏi lại (test). Thẻ được kiểm **trước** khi mở bất kỳ đối tượng nào để sửa: Hủy = không
   đổi gì (test vị trí trong code). Trợ lý chỉ chuyển mã có trên thẻ đã bấm Đồng ý; "ok" gõ tay không tính (test).
3. **Chỉ đọc / ghi:** `check_drawing` đăng ký là lệnh đọc (chạy khi "Chỉ xem"; xuất Excel vẫn ghi file như 4 bảng của F4);
   `fix_drawing` lệnh ghi (bị chặn khi "Chỉ xem"); layer khóa bỏ qua và liệt kê; sửa trong nhóm hoàn tác của lượt (một U);
   `fix_drawing` không ghi macro (`NotRecorded`).
4. **Gói:** cả hai nhóm 2D: gói 2D / Full có, gói 3D không; chế độ "2D" / "Cả hai" của máy Full có, chế độ "3D" không (test);
   nhãn "Soát bản vẽ", "Sửa lỗi bản vẽ".
5. **Nạp gọn:** hai lệnh không nằm trong nhóm nạp sẵn (chỉ tên lúc mở chat); tắt nạp gọn thì nạp đủ (test); câu system prompt
   chỉ ở 2D / Full và chế độ 2D.
6. **Excel:** thẻ ghi đè của F5 (`RequireOverwriteCard`), tên `<bản vẽ>-soat-ban-ve.xlsx`, sheet "Soát bản vẽ"; không lỗi thì
   không ghi file, có `excel_note`.
7. **Giới hạn:** quét 20 000 (`scanned` / `total` / `complete` + ghi chú), sửa 2 000 / lần (dòng "gọi lại"); thêm G2T-4.
8. **Bảng số / quyết định** (`nang-cap-g2.md` mục 6, 7): khớp code từng dòng (`G2Numbers`, `F2Numbers.StandardScales`). Bổ
   sung một dòng có sẵn trong code mà bảng thiếu: tối đa 30 tên layer / layout mỗi nhóm, 50 handle trong kết quả sửa.

## Con số / quyết định mới

- Không đổi số nào; không thêm số mới (dòng bổ sung ở mục 8 là số có sẵn).
- Quyết định tự chọn thêm: xref "Unloaded" / không dùng và ảnh đã gỡ mà file còn **không** là lỗi (muốn báo thì bỏ
  `XrefMissing` / `ImageFileFound`).

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1255** (1240 + 15 `NangCapG2TestSuaTests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **373** (369 + 4 `test/nangcapg2-testsua.test.ts`). Không sửa / bỏ test cũ.
- Build 3 target (xóa obj / bin giữa các lần): 0 cảnh báo.
- `docs/checklist-dot-g2.md`: thêm mục E (3 bước cho G2T-1 … 3).

## CI

- Run **#58** (`45640452138`, `release=false`) trên `7b9be8f` (commit code cuối): **xanh** (job `windows`: build 3 target, lõi,
  trợ lý, UI test WPF). Dùng 1 / 2 lượt. Commit sau đó chỉ thêm mục này.

## Chưa kiểm được

- AutoCAD thật: trạng thái xref / ảnh (`XrefStatus`, `RasterImageDef.ActiveFileName`), khung nhìn, thuộc tính khung tên, tốc độ
  quét 20 000 đối tượng; bước tự kiểm G2 chưa chạy. Thử tay: `docs/checklist-dot-g2.md`.
