# Bàn giao: sửa KTH phần H1 / H2 / H3 (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5`. Nhánh `full/nang-cap-h-sua-kth-b`, tách từ `full/nang-cap-h-tong-the` @ `aa5753c`.

- Lỗi lấy từ báo cáo kiểm tra độc lập `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md` (KTH-15 → KTH-26), cộng mục nghi vấn về
  `compare.ts`.
- Không đụng `agent/src/ipc/rpc.ts`, phần H4, phần H5.
- Không chạy CI: thread đợt F1 gộp nhánh và chạy CI.
- Chưa thử trên AutoCAD thật.

## 1. Từng lỗi

| Mã | Sửa gì | Test (`Core.Tests/NangCapHSuaKthBTests.cs`) |
| --- | --- | --- |
| KTH-15 | Polyline kín được đọc theo một cách cố định: bắt đầu từ đỉnh có x nhỏ nhất (rồi y), đi ngược chiều kim đồng hồ, độ cong (bulge) đi theo cạnh (`DrawingCompareLogic.CanonicalLoop`). Phòng vẽ ngược chiều hay bắt đầu ở góc khác: không khác | `Kth15_a_room_drawn_the_other_way…`, `Kth15_the_bulges_follow…` |
| KTH-16 | Điểm neo của polyline là đỉnh đầu (x, y, cao độ) (`CmpItem.AnchorAt`, `AltAnchorAt`), không còn là x0, y0, x1. Kéo giãn phòng: một dòng "đổi" | `Kth16_a_stretched_room_is_one_change…` |
| KTH-17 | Cung so theo tâm, bán kính, điểm đầu, điểm cuối, trong dung sai. Góc xoay của chữ / chữ nhiều dòng / block so trong dung sai tại cạnh xa của khung bao: lệch góc × độ dài khung ≤ dung sai (`CmpItem.Angles`, `Reach`, `SameAngles`). 0° và 360° là một | `Kth17_a_turn_is_compared_where_it_shows` |
| KTH-18 | Block động: thêm `Props["dynamic"]` = các cặp tên = giá trị của thuộc tính động (rộng cửa, visibility, lật…). Bỏ `Origin`; số làm tròn 3 số lẻ, đổi theo đơn vị nếu là khoảng cách | `Kth18_19_20_…` (soi mã nguồn) |
| KTH-19 | Thêm loại đối tượng:<br>• MLeader: chữ (`MText.Contents`) hoặc tên block;<br>• Table: chữ các ô, đọc **trước** nhánh block vì Table là một loại BlockReference;<br>• Spline: điểm điều khiển, bậc, kín / hở; mở thì đọc được cả hai chiều;<br>• Ellipse: tâm, trục, điểm đầu / cuối;<br>• Leader: các đỉnh | như trên |
| KTH-20 | Hatch: tỉ lệ mẫu (so theo tỉ lệ phần trăm như diện tích) và góc mẫu. Kích thước: tên kiểu kích thước | như trên + so tỉ lệ mẫu |
| KTH-21 | Hằng số mới `H3Numbers.MaxRowsForClouds = 5000`. Tách hai lời báo: quá 5 000 chỗ khác ("nothing was drawn", ghi số chỗ khác), và quá 500 đám mây (ghi số đám mây) | `Kth21_…` |
| KTH-22 | Dòng "đổi" nhớ cả khung bao cũ (`DiffRow.OldMin` / `OldMax`). Đám mây khoanh cả chỗ cũ lẫn chỗ mới; dời ít thì hai khung chạm nhau, gộp thành một | `Kth22_a_moved_object_is_ringed…` |
| KTH-23 | `ExportLogic.Unshift` trả `double[]`. `FbxWriter.FromTriangles` có thêm bản nhận `double[]`; bản `float[]` cũ vẫn giữ. Đỉnh ở 500 000 012,3 giữ đúng, không còn bị làm tròn về bước 32 mm | `Kth23_far_corners_keep…` |
| KTH-24 | `bundle/make-bundle.mjs` đưa sẵn `Contents\skills-ban-moi` vào gói cài, nên script cập nhật của plugin **cũ** (chép mọi thứ trừ `skills`) cũng mang skill mới tới ngay lần đầu. Bộ cài Inno loại thư mục này khỏi dòng chép chung (đã có dòng riêng). Tài liệu: `bo-cai/README.md`, `nang-cap-h1.md` §1 | `Kth24_the_bundle_itself_carries…` |
| KTH-25 | `SkillSync.Sync`: file đã từng cài (có trong `.ban-da-cai.json` với đúng mã băm của bản mới) mà nay không còn thì coi là khách xóa: không chép lại (`Result.LeftDeleted`). Chỉ chép lại khi có bản mới của file đó (mã băm khác) | `Kth25_a_skill_the_user_deleted…` |
| KTH-26 | `docs/checklist-dot-h1.md` và khối H1 trong `docs/checklist-tong-the.md`: thư mục skill đúng là `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills`. Sửa thêm bước A.6 (cập nhật trong app từ bản cũ), thêm A.8 (skill đã xóa không quay lại) | — (tài liệu) |
| Nghi vấn `compare.ts` | Đúng là lỗi: lệnh có thể vẽ đám mây và ghi Excel, nên đổi `readOnly: true` → `false`. Lý do: lệnh khai "chỉ đọc" có thể được chạy cùng lúc với lệnh khác | `Compare_is_not_declared_read_only…` |

Test cũ không phải sửa: không test nào trông vào việc skill đã xóa tự quay lại hay vào góc cung so khít.

## 2. File

- Sửa:
  - plugin: `Core/DrawingCompare.cs`, `Drawing/CompareTools.cs`, `Core/Export.cs`, `Drawing/ExportTools.cs`, `Core/SkillSync.cs`;
  - trợ lý, bộ cài, đóng gói: `agent/src/tools/compare.ts`, `bundle/make-bundle.mjs`, `installer/ClaudeCAD.iss`;
  - tài liệu: `bo-cai/README.md`, `docs/ban-giao/nang-cap-h1.md`, `docs/checklist-dot-h1.md`, `docs/checklist-tong-the.md` (khối H1).
- Mới: `Core.Tests/NangCapHSuaKthBTests.cs` (11 test), bàn giao này.

## 3. Con số chờ chủ dự án duyệt

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 5 000 chỗ khác | `H3Numbers.MaxRowsForClouds`: quá số này thì không khoanh đám mây | So hai bản khác nhau quá nhiều: Claude nói không vẽ đám mây, đưa danh sách / Excel | Số đã có trong code (`MaxClouds × 10`), nay thành hằng số riêng. 5 000 chỗ khác là hai bản gần như khác hẳn nhau; tính đám mây lâu mà không đọc được |

Số bên trong, khách không thấy (không phải duyệt):

- giá trị thuộc tính động làm tròn 3 số lẻ;
- góc mẫu hatch làm tròn 0,001°;
- tối đa 5 000 ô chữ đọc mỗi bảng.

## 4. Quyết định tự chọn

- KTH-15: polyline kín bắt đầu từ đỉnh x nhỏ nhất. Hai đỉnh có x lệch nhau dưới dung sai có thể làm hai bản bắt đầu ở hai đỉnh
  khác nhau. Hiếm, và chỉ ra "bớt + thêm" như trước khi sửa.
- KTH-17: độ dài để đo góc là đường chéo khung bao (tối thiểu 1 đơn vị). Chữ ngắn hay block nhỏ cần xoay nhiều hơn mới bị báo.
- KTH-22: chỗ cũ và chỗ mới của một đối tượng tính là **một** chỗ khác, dù có thể ra hai đám mây.
- KTH-25: skill đã xóa quay lại khi bản cài sau có bản mới của nó. Muốn bỏ hẳn thì tắt skill trong ⚙, đừng xóa.

## 5. Test

- Lõi: 1592 / 1592 qua (thêm 11).
- Trợ lý: 436 / 436 qua.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: qua.
- Chưa chạy CI (thread đợt F1 chạy sau khi gộp).

## 6. Chưa kiểm được

- AutoCAD thật: đọc thuộc tính động, ô bảng, MLeader, Spline từ file khác.
- Gói cập nhật trong app từ bản cũ lên (checklist H1 A.6).
