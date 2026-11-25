# Bàn giao: Test & sửa đợt F3 (3D đẹp và đúng hơn)

Nhánh `full/nang-cap-f3-test`, tách từ `full/nang-cap-f3` @ `09ae953` (code F3 `4a26989`, gốc `b2e6431`). Người rà
chưa viết phần F3. Nhánh này **không** có phần F2 (thread F4 gộp sau).

Chưa thử trên AutoCAD thật: kiểm bằng đọc code, test lõi / trợ lý, build 3 target và CI (xem cuối file).

## Đã rà

- **Mái liền khối** (`Core/RoofRightAngled.cs`, `StairRoof.cs`, `Drawing/StairRoofTools.cs`): cách ghép (∪ mặt trên −
  ∪ mặt dưới) cho bản mái dày đều, không chồng; 4 dốc = khung xương đúng (khoảng cách L∞ tới đường bao, nhà góc vuông);
  2 dốc: chọn mái chính, cắt cánh. Thử thêm: chữ U 2 dốc quay 0 / 90 / 180 / 270°, cánh rộng hơn thanh nối, cánh bằng
  nhau, cánh rất hẹp (600 / 1000 mm), đường bao vẽ tay lệch < 1 mm, hình chữ nhật / đa giác lồi giữ kết quả cũ (test F3
  có sẵn), giới hạn 40 góc / 12 mảnh, lời từ chối kèm gợi ý.
- **Vật liệu** (`set_material`, `Core/Materials.cs`, `Drawing/RenderTools.cs`): gán / đổi / gỡ (ByLayer / ByBlock),
  theo khối và layer, layer khóa (bỏ qua, báo), luật > 20 đối tượng của thẻ, chế độ Chỉ xem.
- **Góc nhìn** (`named_view`): lưu / gọi / liệt kê / xóa / ghi đè (`replace: true`), ảnh PNG off-screen (không đổi màn
  hình), tên và chỗ lưu file.
- **Số mặc định mới**: bản thang 120, bản mái dốc 120, mái bằng 150, khe thang U 100 — code (`StairLogic`, `RoofLogic`),
  mô tả lệnh (`solids3.ts`), skill `ve-3d-kien-truc` khớp nhau; số ghi tường minh trong macro cũ vẫn dùng đúng số đó
  (`thickness` / `waist` chỉ lấy mặc định khi không có).
- Chéo: UCS (2 lệnh mới giữ WCS, mái theo cơ chế F1 như cũ), gói 2D / 3D / Full và chế độ F1 (2 lệnh nhóm 3D), thẻ xác
  nhận, bản vẽ mét (mái tính mm rồi chia theo INSUNITS như đợt E), bảng khối lượng (một khối, không tính chồng).

## Lỗi tìm được

| # | Mô tả | Mức | Sửa |
| --- | --- | --- | --- |
| T3-1 | **Mái 2 dốc nhà chữ U**: thanh nối chỉ được cắt ở nóc mái chính (một cánh); ở cánh bên kia, đầu hồi tam giác của thanh nối **nhô ra trên sườn mái cánh** (ở tường ngoài cánh, đỉnh mái 4004 thay vì 3139 mm). Bị ở mọi hướng quay | vừa (hình sai thấy rõ) | Cánh thấp hơn (hẹp hơn) dừng ở nóc **mọi** mái cao hơn mà nó chạy vào, không chỉ mái chính (`RoofLogic.EndInHigherRidges`); bằng nhau thì mái đứng trước (mái chính) thắng. Test `NangCapF3TestSuaTests` (U quay 4 hướng, U cánh bằng thanh). Mô tả `build_roof` + skill sửa câu "cánh chạy vào mái chính" |
| T3-2 | `set_material` với `material: "Global"` + `color` / `opacity` **đổi màu vật liệu Global** — vật liệu mặc định của mọi layer, cả bản vẽ đổi màu trong kiểu Realistic | nhẹ | Từ chối đổi màu / độ trong của Global (`MaterialLogic.IsProtected`); vẫn gán Global cho khối / layer được |

Không thấy lỗi khác. Các điểm đã xem và giữ như F3 chọn (ghi trong "Quyết định tự chọn" của F3): xóa / ghi đè góc nhìn
không qua thẻ (chỉ `replace: true` + Claude hỏi trước, hoàn tác được); `set_material` theo `layers` không đếm số khối
bị ảnh hưởng (luật > 20 chỉ tính `handles`) — luồng chính có thể hỏi chủ dự án có muốn hỏi trước khi gán cả layer không.

## Con số chờ chủ dự án duyệt (đã soát)

Bảng của F3 (`nang-cap-f3.md`) đủ các số đợt F3 thêm / đổi; mỗi số ở một chỗ có tên (`RoofLogic.*`, `StairLogic.*`,
`MaterialLogic.Presets`, `MaterialLogic.DefaultImageSize`). Không ghi tên tiêu chuẩn nào (đều "theo thông lệ") — đúng.
Không đổi số nào.

| Số | Khách thấy gì | Ở đâu | Soát |
| --- | --- | --- | --- |
| Bản thang + chiếu nghỉ **120 mm**, khe giữa 2 vế thang U **100 mm** | độ dày bản thang khi không nói số | `StairLogic.DefaultWaist / DefaultGap` | chủ dự án chốt 25/11; code, mô tả, skill khớp |
| Bản mái dốc **120 mm**, mái bằng **150 mm** | độ dày mái khi không nói số | `RoofLogic.DefaultSlopedThickness / DefaultFlatThickness` | như trên |
| Cao bậc 160, rộng bậc 280, vế rộng 1000, dốc mái 30°, đua mái 600 | số mặc định cầu thang / mái | `StairLogic`, `RoofLogic` | giữ nguyên từ đợt E (theo điểm 6 kế hoạch) |
| Tối đa **40 góc** đường bao, **12 mảnh** mái cho mái dốc nhà góc vuông | nhà phức tạp hơn bị từ chối, gợi ý mái bằng | `RoofLogic.MaxRightAngledCorners / MaxRoofPieces` | đủ; sau sửa T3-1 nhà chữ U dùng 4 mảnh (trước 3), vẫn xa giới hạn |
| Màu vật liệu mẫu (bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá) + kính trong 35 % | màu khối trong kiểu Realistic và ảnh PNG | `MaterialLogic.Presets` | đủ, theo thông lệ |
| Tên vật liệu mẫu tiếng Việt | tên trong danh sách vật liệu AutoCAD | như trên | đủ |
| Ảnh PNG cạnh dài **1568 px** (384–1568), nhìn **sw_iso**, kiểu **Realistic**, tên `<bản vẽ>-<góc nhìn>.png` cạnh bản vẽ | cỡ / góc / tên file ảnh | `MaterialLogic.DefaultImageSize / ImagePath`, `RenderTools.cs` | đủ |
| Tay vịn 900, tay vịn / trụ 50 × 50, trụ cách ≤ 1500, bậc trên gác lên sàn 100, gợi ý tách vế > 18 bậc, các giới hạn thang / mái | lan can thang, cảnh báo, số bị từ chối | `StairLogic.*`, `RoofLogic.*` | đã có trong bảng phụ của F3 (số sẵn có, chưa duyệt) |
| *(bổ sung)* Màu layer mái mới `A-ROOF-3D` (ACI 30, cam) | màu layer mái khi tự tạo | `StairRoofTools.cs` (`EnsureLayer(ctx, layer, 30)`) | số có sẵn từ đợt E, chưa có trong bảng; nằm thẳng trong code (không có tên hằng) — để nguyên, ghi để chủ dự án biết |

## Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Cánh 2 dốc gặp hai mái (thanh nối nhà U) | Dừng ở nóc mọi mái **rộng hơn** nó chạy vào; rộng bằng nhau thì mái chính / mái đứng trước thắng | Đổi điều kiện `higher` trong `EndInHigherRidges` |
| Vật liệu Global | Không cho đổi màu / độ trong; gán được | Bỏ `IsProtected` |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1012** (1001 của F3 + 11 `NangCapF3TestSuaTests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **317** (không thêm; `rm -rf dist-test` trước khi chạy nếu thư mục build còn
  file test của nhánh khác).
- Build 3 target (Linux, .NET SDK 10, xóa obj / bin giữa các lần): 0 lỗi, 0 cảnh báo.

## Chưa kiểm được

- Trên AutoCAD thật: mái chữ U sau sửa (phép cộng / trừ khối của AutoCAD với 4 mảnh), vật liệu trong kiểu Realistic,
  ảnh PNG, lưu / gọi góc nhìn. Thử tay: `docs/checklist-dot-f3.md` (thêm B8, D6).

## CI

*(điền sau khi chạy)*
