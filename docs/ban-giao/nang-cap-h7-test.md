# Bàn giao: Test & sửa đợt H7 — ống MEP 3D (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người rà). Nhánh `full/nang-cap-h7-test`, tách từ `full/nang-cap-h7` @ `d545308`
(thread viết đã sửa sẵn: dốc mặc định theo cỡ, ống gió không xoắn ở đoạn đứng, `mep_report` chỉ đếm khối 3D, khe hở ống thẳng đo
từ trục). Không đụng nhánh H6. Chưa chạy trên AutoCAD thật. Thử tay thêm: `docs/checklist-dot-h7.md` (các dòng "Test & sửa H7").

## 1. Lỗi đã sửa

| Mã | Mức | Lỗi | Đã sửa |
| --- | --- | --- | --- |
| H7T-01 | Nặng | Bản sửa "ống gió không xoắn" xoay hướng bề rộng theo từng co. Tuyến ngang theo X → lên đứng → ngang theo Y: đoạn ngang trên bị đặt **bề rộng đứng** (ống 600 × 300 thành cao 600), dễ chạm dầm và sai cỡ khách nói | Đoạn không thẳng đứng luôn giữ bề rộng nằm ngang; đoạn đứng mang hướng của co trước. Khi hai đoạn ngang đi hai hướng khác nhau, tiết diện xoay ở đầu ống đứng (`MepPiece.Twist`) và kết quả cảnh báo (thực tế cần đoạn chuyển tiếp) |
| H7T-02 | Vừa | Ống thoát cho điểm x, y, z: cảnh báo dốc dưới mức tối thiểu tính theo dốc mặc định, không theo cao độ khách cho; ống dốc ngược (chảy lên) không báo | Tính độ dốc thật từng đoạn (bỏ đoạn đứng), báo đoạn phẳng nhất dưới mức tối thiểu, báo đoạn dốc ngược; trả `least_slope_percent` |
| H7T-03 | Vừa | Dầm của hai tầng cùng nằm trên tuyến: `under_beams` lấy dầm thấp nhất (có thể là dầm tầng dưới) mà không báo | Báo khi đáy các dầm chênh nhau hơn 1 m, kể các cao độ, nhờ cho `elevation` nếu ống ở tầng khác |
| H7T-04 | Nhẹ | Góc rẽ cực nhỏ (vài phần triệu radian) vẫn tạo co rất ngắn, lệnh xoay khối của AutoCAD có thể lỗi | Góc dưới 0,001 rad (≈ 0,06°) coi như thẳng (`MepLogic.MinTurn`) |
| H7T-05 | Nhẹ | Tuyến cho theo mặt bằng (x, y + cao độ) dưới UCS nghiêng: cao độ bị thay trên mặt phẳng nghiêng, ống sai chỗ | Từ chối, nhờ cho x, y, z hoặc đặt lại UCS (như `trace_image`) |

## 2. Soát lại bản sửa của thread viết

- Dốc mặc định = max(2 %, mức tối thiểu theo cỡ) (chốt 26/11): đúng, D60 2,5 %, D110 2 %, D40 2 % (dưới bảng). Mô tả lệnh, skill khớp.
- Ống gió không xoắn ở đoạn đứng: đúng với tuyến lên rồi đi tiếp cùng hướng; **sai** với tuyến đổi hướng (H7T-01).
- `mep_report` / `under_beams` chỉ đếm khối 3D: đúng.
- Khe hở ống thẳng đo từ trục (tới hộp bao dầm / cột, hoặc trục ống khác): đúng; ống chéo xa cột không còn báo "sát 0 mm" (test).
  Co / tê vẫn đo bằng hộp bao (thiên về an toàn); ống gió lấy nửa đường chéo tiết diện (thiên về an toàn).

## 3. Đã soát, không sửa

- Gói: `draw_mep`, `mep_report` ở nhóm 3D (gói 2D bị chặn), hoãn nạp; thẻ F5 chỉ cho file Excel của `mep_report`.
- Cao độ dưới dầm: đỉnh ống = đáy dầm − 100 mm (không dầm: đáy sàn − 300 mm), ống gió theo nửa chiều cao: đúng.
- Độ dốc: điểm đầu là đầu cao, rơi theo chiều dài mặt bằng: đúng như mô tả.
- Thống kê theo hệ + cỡ, co / tê đếm cái, cột hao hụt tắt mặc định (`WasteColumnByDefault = false`), `waste_percent` thêm cột: đúng.
- Soát chạm / sát mặc định 50 mm: đúng; cùng tuyến, nhánh với ống chính không bị báo.
- U: mỗi lần gọi là một nhóm hoàn tác (lệnh ghi qua dispatcher như các lệnh vẽ khác).
- **Nhẹ, để lại:** tê ghi cỡ ống nhánh trong thống kê (khối vẽ theo cỡ ống chính); `IsStructureLayer` nhận layer có "S-", "COL",
  "COT" khá rộng (vd layer "M-DAMPER" chứa "DAM" bị coi là dầm). Muốn chặt hơn: danh sách layer kết cấu rõ ràng.

## 4. Test

- Lõi: **1629 / 1629** (thêm 5 ở `NangCapH7TestSuaTests.cs`; sửa 1 dòng test của thread viết: co ở đỉnh ống đứng được phép xoay
  tiết diện khi `Twist`).
- Trợ lý: **446 / 446**.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi; 1 cảnh báo CA2022 ở `TraceTools.cs` có sẵn.
- CI: mục 5.

## 5. CI

*(ghi sau khi chạy)*
