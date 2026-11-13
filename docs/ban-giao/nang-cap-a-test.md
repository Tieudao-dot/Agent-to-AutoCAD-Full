# Bàn giao: Test & sửa Nâng cấp đợt A (thread con của "Nâng cấp đợt A")

*13/11/2026 · thread cha "Nâng cấp đợt A" giao. Nhánh `full/nang-cap-a-test`, xuất phát `full/nang-cap-a` @ `fc68469`.
**Chưa thử trên AutoCAD thật.** Thử tay: mục **N** của [`checklist-thu-that.md`](../checklist-thu-that.md)
(thêm N17–N19 ở bước này).*

Máy cloud không có AutoCAD: "test" = rà code 6 việc của đợt A, viết test tự động, sửa lỗi, build 3 bản, 1 lượt CI.

## Đã rà

| Phần | File | Kết quả |
| --- | --- | --- |
| A1 gộp 6 lệnh dựng khối | `agent/src/tools/solids.ts`, `Drawing/SolidTools.cs`, `Core/Solids.cs` | Không lỗi. 6 tên cũ vẫn đăng ký trong plugin, nhãn 3D (macro cũ chạy ở gói 3D / Full, bị chặn ở gói 2D); Claude chỉ thấy `draw_solid`. Tham số của hình khác bị từ chối có lý do |
| A2 cổng layer | `agent/src/agent/confirmation.ts`, `session.ts`, `PlanTo3dTools.cs` (`LayerGuesses`) | **1 lỗi (NA-1)**. Còn lại đúng: thẻ khác / gõ "ok" không có thẻ / Hủy không mở layer; tên layer có dấu cách, dấu chấm, ngoặc, `$`, dấu tiếng Việt khớp đúng cả tên; mẫu quá rộng (`*`, `T*`, `A-*`) không bao giờ được duyệt |
| A3 cột | `PlanTo3dTools.cs` (`ReadPlan`, `BuildColumns`), `FloorPlanLogic.ColumnRerun` | Không lỗi. Cột chỉ so dựng lại với cột; tầng dưới / tầng trên không tính |
| A4 nhiều tầng | `FloorPlanLogic.Storeys`, `Build3dFromPlan`, `CopyLevel` | **1 lỗi (NA-3)**. Cao độ, tường = tầng − sàn, sàn mái, đơn vị mét, thứ tự handle từng tầng: đúng |
| A5 lan can | `Core/Railing.cs`, `BuildRailing` | **2 lỗi (NA-2, NA-4)**. Khe song ≤ 100 với mọi nhịp 20–6000 mm, mọi mảnh nằm trong chiều cao, vòng kín / điểm lặp / đoạn gãy nhỏ: đúng |
| A6 `array_rect` số tầng | `ModifyTools.cs`, `PlanLogic.Blocked3dParam`, `PLAN_HIDDEN_PARAMS` | Không lỗi. Gói 2D: `levels > 1` bị chặn (cả khi truyền dạng chữ), mảng thường vẫn chạy; tham số ẩn với gói 2D |
| Nhãn gói | `Core/Plans.cs`, test bắt buộc nhãn | `draw_solid`, `build_railing`, 6 bí danh đều nhãn 3D; test bắt buộc nhãn vẫn xanh |
| Thẻ xác nhận 3D | `confirmation.ts` | Không lỗi. Khối dựng trong lượt bằng lệnh mới không cần thẻ; khối có trước lượt vẫn chỉ nút Đồng ý của thẻ có đúng handle mới mở, kể cả khi layer đã được duyệt (có test) |
| Hành vi 3D đã sửa (L17–L28, X1–X5) | test cũ của đợt 1–3 và test tổng thể | Vẫn xanh, không sửa test cũ nào. Một tầng đi đúng đường cũ |
| Self-test | `SelfTest.cs` | Số mong đợi 2 bước mới đã tính lại tay: đúng (tường 2 tầng 15,204 m³, cột 0,9159 m³, sàn 6,048 m³, lan can 0,03139 m³) |
| Thời gian chờ | `agent/src/ipc/rpc.ts` | Dựng thật 120 s (kể cả trong `batch_execute`), chạy thử và `draw_solid` giữ 30 s: đúng |

## Lỗi tìm được và cách sửa

| # | Lỗi | Hậu quả với người dùng | Sửa |
| --- | --- | --- | --- |
| NA-1 | Cổng layer chỉ xét tên layer truyền dạng danh sách. Plugin lại nhận cả dạng một chuỗi (`"wall_layers": "TUONG"`), và trong `batch_execute` không có gì chặn dạng đó | Claude có thể dựng theo layer tự đoán mà **không qua thẻ xác nhận** (lách luật "hỏi trước khi dựng") | `planLayersOf` đọc cả dạng chuỗi (`confirmation.ts`) |
| NA-2 | Lan can: giới hạn 2000 mảnh chỉ kiểm sau khi đã xếp xong mọi mảnh. Đường dài hàng km (gõ nhầm số, nhầm đơn vị) tạo hàng triệu mảnh trước khi báo lỗi | AutoCAD đứng máy hoặc hết bộ nhớ, có thể mất việc chưa lưu | Kiểm giới hạn ngay trong lúc xếp, trước khi tạo mảnh (`RailingLogic.Build`); câu báo ghi chiều dài đường và nhắc kiểm đơn vị |
| NA-3 | Dựng nhiều tầng khi mặt bằng không có đường bao sàn: tường mỗi tầng vẫn = cao tầng − dày sàn nhưng không có sàn nào | Các tầng **hở nhau một khe bằng dày sàn** (150 mm); dựng lại thì bị báo "đã dựng rồi" | Từ chối trước khi dựng, bảo Claude hỏi người dùng: sàn theo khung bao tường (`bbox`), chỉ đường bao sàn, hay không sàn (`none`, tường cao hết tầng). Chạy thử thì ghi chú (`FloorPlanLogic.StoreysWithoutSlab`) |
| NA-4 | Mảnh lan can lỗi thì câu báo ghi tên lệnh `draw_box` | Claude có thể hiểu nhầm lệnh nào lỗi | Truyền đúng tên `build_railing` |

Commit: xem `git log full/nang-cap-a-test` (commit "Test & sửa đợt A…").

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **485** test (439 trước bước này). `NangCapATestSuaTests.cs`: lan can quá
  dài dừng ngay (10 km … 1e300 mm, dưới 2 giây), lan can dài hợp lệ vẫn dựng, đoạn gãy nhỏ, điểm lặp, mọi kiểu song /
  tấm nằm trong chiều cao, khe song mọi nhịp; hỏi sàn khi nhiều tầng; `wall_height` thấp hơn; cột tầng dưới / trên không
  tính dựng lại; đoán layer có dấu tiếng Việt và 13 tên không được đoán; tham số từng hình; nhãn 2D / 3D / Full của lệnh
  mới và 6 tên cũ; `levels` dạng chữ.
- Trợ lý: `cd agent && npm ci && npm test` — **245** test (232 trước). `test/nangcapa-testsua.test.ts`: NA-1 (chuỗi, trong
  batch), chỉ `dry_run: true` thật mới được đọc không hỏi, thẻ 3D khác / gõ "ok" không thẻ / Hủy, tên layer ký tự lạ, mẫu
  `*`, luật thẻ 3D với lệnh mới, tham số theo gói, thời gian chờ.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: xem mục "CI" cuối file.

## Token mô tả lệnh (đo lại, cùng script cho hai bản; ký tự tên + mô tả + schema JSON)

| | Trước đợt A (`2a84e24`) | Sau (`full/nang-cap-a-test`) |
| --- | --- | --- |
| 6 lệnh dựng khối → `draw_solid` | 6 547 | 2 625 (**−3 922 ký tự ≈ −1,1k token**) |
| Tổng mô tả mọi lệnh (Full) | 153 134 | 152 549 (−585; bản này +108 cho câu nhắc sàn khi nhiều tầng) |
| System prompt (Full) | 16 210 | 16 956 (+746) |
| `build_3d_from_plan` / `build_railing` / `array_rect` | 6 862 / — / 1 164 | 8 468 / 1 435 / 1 460 |

Kết luận: gộp 6 lệnh **có giảm thật ~1,1k token**, đúng như bàn giao đợt A (không phải ~2k như ước ban đầu). Các tính
năng mới của đợt A dùng lại gần hết khoản đó, nên tổng token mỗi phiên **gần như không đổi** (chênh dưới 0,1k).

## Giới hạn còn lại (không sửa được ở mức code, hoặc cần chủ dự án quyết)

- Cổng layer không chặn được việc Claude tự gom handle trên layer đoán rồi truyền `wall_handles`: người dùng chọn nét
  rồi bảo "dựng tường từ các nét này" là việc hợp lệ, trợ lý không phân biệt được hai trường hợp. Chỉ có luật trong
  prompt và câu từ chối nhắc không làm vậy.
- Thẻ xác nhận về việc khác nhưng có ghi tên layer (vd. "Xóa 30 đối tượng trên layer TUONG?") mà người dùng Đồng ý thì
  layer đó cũng được coi là đã nêu. Hậu quả nhỏ (chỉ dựng thêm khối mới, hoàn tác được).
- Lan can 2000 mảnh cộng lần lượt thành một khối có kịp 120 giây không: chỉ biết trên AutoCAD thật (N19). Chậm thì hạ
  `RailingLogic.MaxBoxes`.
- `wall_height` cho thấp hơn (cao tầng − dày sàn) thì tường không chạm sàn tầng trên: giữ theo số người dùng cho.

## Việc còn phải thử tay (mục N của checklist)

Quan trọng nhất: **N0** (self-test, 2 bước mới), **N4, N5** (thẻ hỏi layer), **N10** (3 tầng một lượt), **N17** (hỏi sàn
trước khi dựng nhiều tầng). N18, N19: lan can quá dài / dài.
