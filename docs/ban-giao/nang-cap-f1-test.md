# Bàn giao: Test & sửa đợt F1

Nhánh `full/nang-cap-f1-test`, tách từ `full/nang-cap-f1` @ `8110e45`. Rà lại đợt F1 (lệnh hiểu UCS + nút chế độ
"Cả hai / 2D / 3D") bằng đọc toàn bộ diff so với `full/nang-cap-f-goc`, thêm test, sửa lỗi. Chưa thử trên AutoCAD thật.

## Bảng lỗi

| # | Lỗi | Mức | Sửa |
| --- | --- | --- | --- |
| TS-F1-01 | `build_3d_from_plan` với `area_min` / `area_max` khi UCS **xoay một góc không vuông** (vd 30°, 45°): hai góc khung được đổi sang WCS từng góc rồi lấy khung WCS của 2 điểm đó → ra một hình chữ nhật khác (với 45° gần như dẹt), dựng sót hoặc dựng nhầm phần mặt bằng. UCS chỉ dời hoặc xoay 90° thì không sai | Trung bình | Khung được giữ theo số UCS; khung bao của từng hình đổi sang UCS (`UcsFrame.PlanBoxInside`) rồi mới so. World: đúng dòng so sánh cũ |
| TS-F1-02 | `extrude` / `revolve` / `presspull` với `profile_points` khi UCS **nghiêng** (trục Z không thẳng đứng, vd UCS mặt đứng): điểm đã đổi sang WCS nhưng polyline profile dựng phẳng từ x, y WCS → hình dẹt thành một đường (lỗi AutoCAD hoặc khối sai). Thêm nữa chiều "cao dương" của extrude / presspull so với trục Z của WCS thay vì trục Z của UCS | Trung bình | `Acad.MakeProfilePolyline`: ở UCS nghiêng, dựng polyline từ số UCS rồi đưa lên mặt UCS bằng một phép biến đổi; chiều cao dương theo trục Z của UCS (`Normal · UcsZ`; World: `Normal.Z` như cũ). UCS World / chỉ dời-xoay mặt bằng: y như trước |
| TS-F1-03 | Máy **Full ở chế độ 3D**: `match_properties` báo bỏ thuộc tính chữ / kích thước "(this machine's plan has no 2D)"; mô tả lệnh `volume_schedule`, `check_interference`, `define_block`, `insert_block` nói "This machine's plan has no …" → Claude dễ nói với khách "gói của máy không có 2D / không có bảng", trái chốt "lời nhắc là đổi chế độ, không nói gói" | Trung bình | Plugin: `WorkModeLogic.NoTwoDReason` (chế độ 3D: "ngoài chế độ 3D, chế độ 2D hoặc Cả hai có", không nói gói; máy gói 3D: giữ câu cũ). Trợ lý: ghi chú các lệnh bị cắt tham số đổi thành "the tools loaded here have no …" (đúng cho cả gói 3D lẫn chế độ 3D) |
| TS-F1-04 | Chú thích (doc comment) của `IconButton` bị dời lên trên `BuildModeButton` khi chèn code (hai khối `<summary>` liền nhau) | Nhỏ | Trả chú thích về đúng hàm |

Không đổi thứ chủ dự án nhìn thấy ngoài việc sửa đúng các lỗi trên (không có điểm cần chốt).

## Đã rà, không thấy lỗi

- World: mọi lệnh đi thẳng `def.Handler` như trước F1 (`RunHandler`), macro ghi y như trước (`ForMacro` trả lại chính dict cũ).
- Create (đổi hình mới vào UCS bằng một `TransformBy`): chỉ hình thuộc model space; thuộc tính block đi theo block; lệnh có
  handle (cầu thang theo ô thang, dầm theo đường, lan can theo đường, kích thước góc theo 2 đường, mái theo tường) chuyển
  sang Ref đúng.
- Ref: tên tham số là điểm / hướng / hệ số co giãn; khung chọn `list_entities`, `zoom_window`, `stretch_entities` theo trục
  UCS; xoay / mảng quanh trục Z của UCS; số trả về qua `Acad.J / J3 / ToJsonPoint / R3`; khung bao tính lại theo UCS.
- Macro: `_ucs` không bị đổi như handle (`IsHandleKey`), lệnh trong `batch_execute` mang UCS riêng, macro cũ chạy theo
  World; `_ucs` sai bị từ chối (không đoán).
- Nút chế độ: chỉ máy Full (máy 2D / 3D / chưa kích hoạt / gói lạ: không nút, chế độ cũ còn trong cài đặt bị bỏ qua);
  chế độ lấy một lần lúc gửi tin; macro chạy đủ gói thật; skill soạn ghi gói thật; lời từ chối NOT_IN_PLAN theo chế độ
  không có "mua / nâng cấp / liên hệ"; giá trị lạ trong `settings.json` về "Cả hai".

## Giới hạn còn lại (không sửa, ghi để thử tay / đợt sau)

- UCS nghiêng: `loft` với `sections_points` vẫn đòi mỗi mặt cắt nằm ngang theo WCS (báo lỗi rõ, không vẽ sai); hatch theo
  điểm, `build_3d_from_plan`, mái theo tường, cầu thang theo ô thang vẫn làm trên mặt bằng WCS (như bàn giao F1).
- `sweep` với `path_points` đúng ở mọi UCS (đường 3D khi điểm không cùng cao độ WCS).

## Con số (quy tắc "số phải được chủ dự án xác nhận", 25/11/2026)

- **Số khách gặp / làm đổi kết quả, đợt F1 thêm hoặc đổi: không có.** Đổi tọa độ UCS là phép đổi chính xác (không số chọn
  tay); không thêm kích thước, độ dày, chiều cao, góc, cỡ chữ, tỉ lệ, ngưỡng hay giới hạn mới. Chế độ mặc định "Cả hai"
  (không phải số) chủ dự án đã chốt 14/11/2026.
- Thấy được nhưng không đổi kết quả: nút chế độ cao 22 px, rộng tối thiểu 24 px, cách 4 px — theo cỡ các nút sẵn có
  trên thanh trên cùng (22 × 24). Đã báo luồng chính 25/11; chưa có yêu cầu đổi.
- Số kỹ thuật bên trong: UCS coi là World khi lệch ≤ 1e-9; trục bằng 0 khi dài < 1e-12; khung mặt bằng theo UCS so có dung
  sai 1e-6 mm (TS-F1-01, mới); UCS lưu trong macro làm tròn gốc 6 chữ số thập phân, trục 12 chữ số; góc trả về quy về
  0–360°. Giới hạn 1000 điểm của khung đa giác `stretch_entities` có từ trước F1.

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **973** (952 + 21 `NangCapF1TestSuaTests.cs`: khung mặt bằng theo UCS
  xoay 45° / dời, profile trên UCS mặt đứng, dấu chiều cao theo trục Z UCS, lý do bỏ thuộc tính theo chế độ, chế độ cũ trên
  máy 2D / 3D / gói lạ, lời từ chối mọi chế độ, dòng báo đổi chế độ, cài đặt giá trị lạ, `_ucs` sai, chạy lại macro giữ
  UCS đã lưu, UCS gần World coi là World).
- Trợ lý `cd agent && npm ci && npm test`: **311** (307 + 4 `test/nangcapf1-testsua.test.ts`). Test cũ không sửa.
- Build 3 target (xóa obj / bin giữa các lần): net48, net8.0-windows, net10.0-windows: 0 cảnh báo.

## CI

*(điền sau khi chạy)*

## Chưa kiểm được

- Mọi thứ trên AutoCAD thật (như bàn giao F1), thêm khối "F1-d" cuối `docs/checklist-tong-the.md` cho 3 lỗi đã sửa.
