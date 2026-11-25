# Bàn giao: Nâng cấp đợt F3 — 3D đẹp và đúng hơn

Nhánh `full/nang-cap-f3`, tách từ `full/nang-cap-f1-test` @ `b2e6431`. Chủ dự án chốt đợt F ngày 25/11/2026; luồng chính
giao F3 cho thread này (người viết; phần rà lại do thread khác). Đợt F2 (2D) chạy song song trên `full/nang-cap-f2`;
F3 chỉ đụng phần 3D, các file dùng chung chỉ thêm dòng.

Chưa thử trên AutoCAD thật. Phần chỉ kiểm được trên AutoCAD (khối mái thật sau phép cộng / trừ khối, vật liệu hiện trong
Realistic, góc nhìn, file PNG) nằm trong bước tự kiểm `CLAUDECHATSELFTEST` ("Nâng cấp đợt F3 …") và khối thử tay
`docs/checklist-dot-f3.md`; **bước tự kiểm đó chưa chạy** (CI không có AutoCAD).

## 1. Mái liền khối cho nhà chữ L / T / U

### Khách thấy gì

- Nhà có **mọi góc vuông** (chữ L, T, U, chữ thập, bậc thang…): mái 4 dốc và mái 2 dốc dựng thành **một khối**, có khe mái
  (đường nước chảy) chỗ hai cánh gặp nhau, không chồng thể tích → `volume_schedule` tính đúng; mặt dưới mái vẫn chạm đỉnh
  tường ở mọi bức tường có mái dốc xuống.
- **4 dốc:** mặt mái nào cũng dốc như nhau (30° mặc định), giống mái 4 dốc dựng tay trên nhà chữ L.
- **2 dốc:** cánh **rộng nhất** là mái chính (nóc theo cạnh dài, `ridge: "short"` để đổi); các cánh còn lại có nóc vuông góc
  với nóc chính, chạy vào mái chính và tắt trong mái chính. Đầu hồi để hở như mái 2 dốc chữ nhật.
- Nhà chữ nhật, đa giác lồi (4 dốc) và mái bằng: **kết quả y như trước** (trừ bản mái dốc mặc định 120, mục 4).
- Từ chối rõ ràng (không dựng gì), có gợi ý: đường bao có góc lõm **không vuông** (tường xiên) → mái bằng hoặc vẽ lại góc
  vuông; quá 40 góc hoặc cần quá 12 mảnh mái → mái bằng hoặc dựng từng phần.

### Cách làm

`Core/RoofRightAngled.cs` (mới, phần lõi không cần AutoCAD) + `Core/StairRoof.cs` (rẽ nhánh) + `Drawing/StairRoofTools.cs`.

1. Đường bao (sau khi đua mái) được xoay về hệ theo cạnh đầu tiên; mọi cạnh song song / vuông góc cạnh đó (sai ≤ ~0,1°).
2. Tìm **các hình chữ nhật lớn nhất** nằm trong đường bao (lưới theo tọa độ các góc, hình không nở thêm được về phía nào).
   Hình vuông lớn nhất đặt tâm ở bất kỳ điểm nào trong nhà luôn nằm trong một hình như vậy — nhờ đó mái 4 dốc ghép từ
   chúng trùng đúng mái 4 dốc "chuẩn" của nhà góc vuông (chiều cao mái = khoảng cách tới mép mái theo kiểu ô vuông).
3. Mỗi hình chữ nhật là một mảnh: phần **dưới mặt mái trên** và phần **dưới mặt mái dưới** của mảnh đó (dùng lại mặt nghiêng
   của mái chữ nhật cũ). Mái = hợp các phần dưới mặt trên − hợp các phần dưới mặt dưới. Chỗ hai mảnh gặp nhau, mảnh cao hơn
   giữ lại, mảnh thấp bị cắt bỏ → **độ dày theo phương đứng như nhau ở mọi chỗ**, không chồng thể tích:
   thể tích = diện tích mặt bằng mái × bản mái / cos(dốc). Test kiểm cột vật liệu tại hàng trăm điểm ngẫu nhiên.
4. Mái 2 dốc: mảnh chính = hình chữ nhật rộng nhất; các hình khác bị cắt ở đường nóc chính, phần nằm trong mảnh chính bỏ đi.

### Phía Claude

- Mô tả `build_roof`: "a rectangle, a convex outline (hip) or a right-angled L / T / U house (one solid with valleys …)";
  bỏ câu "báo trước chỗ chồng / bảng khối lượng tính hai lần". Skill `ve-3d-kien-truc`: tương tự.

## 2. Vật liệu + ảnh "có vật liệu"

### Đã cân nhắc

| Cách | Ưu | Nhược | Chọn |
| --- | --- | --- | --- |
| **Vật liệu thường của bản vẽ (Material: màu + độ trong) + ảnh off-screen kiểu Realistic** | Cùng API trên AutoCAD 2022–2027, build được cả 3 target; dùng lại bộ chụp ảnh của `capture_view` (không đổi màn hình khách, không tạo bước hoàn tác) | Không có vân (gạch, gỗ là màu trơn), ánh sáng mặc định | **Chọn** |
| Vật liệu từ thư viện Autodesk (Autodesk Material Library, có vân) | Đẹp | Đường dẫn thư viện / bản cài khác nhau giữa các máy, không có API .NET ổn định để chép vật liệu thư viện | Không |
| Lệnh RENDER (Rapid RT) | Ảnh đẹp nhất | Chạy bất đồng bộ, mở cửa sổ riêng, phụ thuộc cài đặt render, khác nhau giữa các phiên bản; khó biết khi nào xong | Không |

### Khách thấy gì

- `set_material`: vật liệu mẫu (bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá — tạo một lần trong bản vẽ, tên tiếng
  Việt) hoặc tên mới + màu [r, g, b] + độ trong; gán cho khối (handles) và / hoặc cả layer; `ByLayer` / `ByBlock` để trả về
  mặc định; `list: true` xem vật liệu có trong bản vẽ. Đối tượng trên layer khóa: bỏ qua, báo lại (không tự mở khóa).
  Sửa đối tượng có sẵn: luật hỏi trước khi sửa > 20 đối tượng như `set_entity_props`.
- Ảnh: `named_view` `action: "image"` lưu **file PNG** cạnh bản vẽ (hoặc đường dẫn khách đưa), kiểu hiển thị Realistic (hiện
  vật liệu), từ góc nhìn đã lưu hoặc `view` (mặc định `sw_iso`); không đổi màn hình khách; file đã có thì phải `replace: true`.
  Claude tự xem bằng `capture_view visual_style realistic` (đã có).

## 3. Lưu góc nhìn

`named_view`: `save` (góc nhìn hiện tại của Model, tên trùng phải `replace: true`), `restore`, `list`, `delete`, `image`.
Lưu đủ hướng nhìn, tâm, cỡ, xoay, phối cảnh, mặt cắt trước / sau, kiểu hiển thị. Gọi lại / lưu cần đang ở tab Model.

## 4. Số mặc định mới (chủ dự án chốt 25/11/2026)

| Số | Trước | Sau | Ở đâu |
| --- | --- | --- | --- |
| Bản thang + chiếu nghỉ | 150 | **120** | `StairLogic.DefaultWaist`, `solids3.ts` `DefaultWaist`, mô tả `build_stair`, skill |
| Bản mái dốc (2 dốc, 4 dốc) | 150 | **120** | `RoofLogic.DefaultSlopedThickness` (mới), `DefaultSlopedRoofThickness`, mô tả `build_roof`, skill |
| Bản mái bằng | 150 | 150 (giữ, chốt 13/11) | `RoofLogic.DefaultFlatThickness` (tách khỏi `DefaultThickness` cũ) |
| Khe giữa 2 vế thang chữ U; cao bậc, rộng bậc, vế rộng, dốc mái, đua mái | 100; 160, 280, 1000, 30°, 600 | giữ nguyên | |

`Options.Thickness` của mái mặc định `NaN` = "theo loại mái"; tham số `thickness` ghi rõ trong macro cũ (vd 150) vẫn ra
đúng mái như cũ (test). Test cũ có số 150 mặc định được sửa theo số mới (không bỏ test nào).

## Con số chờ chủ dự án duyệt

Số mới của F3 (đề xuất, để ở một chỗ dễ đổi):

| Số | Dùng ở đâu (để đổi) | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Tối đa **40 góc** đường bao cho mái dốc nhà góc vuông | `RoofLogic.MaxRightAngledCorners` (`Core/RoofRightAngled.cs`) | Nhà nhiều góc hơn: báo lỗi, gợi ý mái bằng / dựng từng phần | Nhà ở thường < 20 góc; giữ phép tính nhanh |
| Tối đa **12 mảnh** mái | `RoofLogic.MaxRoofPieces` | Như trên | Mỗi mảnh ~10 phép cộng / trừ khối của AutoCAD; 12 đủ cho chữ L / T / U / H, chữ thập |
| Màu vật liệu mẫu (RGB): bê tông 165,165,160; gạch 170,74,50; vữa trắng 236,232,222; kính 150,200,220; gỗ 150,100,60; ngói 180,80,45; thép 120,130,140; đá 140,135,125 | `MaterialLogic.Presets` (`Core/Materials.cs`) | Màu khối trong kiểu Realistic và ảnh PNG | Theo thông lệ màu các vật liệu này (không có tiêu chuẩn) |
| Kính độ trong **35 %** (đục 35 %, các vật liệu khác 100 %) | `MaterialLogic.Presets` (glass) | Kính nhìn xuyên được | Theo thông lệ: thấy rõ là kính mà vẫn thấy mặt kính |
| Tên vật liệu mẫu: Bê tông, Gạch, Vữa trắng, Kính, Gỗ, Ngói, Thép, Đá | `MaterialLogic.Presets` | Tên trong danh sách vật liệu của AutoCAD | Tên tiếng Việt khách quen |
| Ảnh PNG cạnh dài **1568 px** mặc định (384–1568) | `MaterialLogic.DefaultImageSize`; giới hạn của `Capture` | Cỡ file ảnh | Lớn nhất bộ chụp off-screen làm được hiện nay |
| Ảnh mặc định nhìn **sw_iso**, kiểu **Realistic** | `RenderTools.cs` (`ViewOf(p, "sw_iso")`, `"realistic"`) | Ảnh phối cảnh góc Tây Nam, có vật liệu | Như `set_view_3d` mặc định; Realistic là kiểu duy nhất hiện vật liệu |
| Tên file ảnh mặc định `<tên bản vẽ>-<góc nhìn>.png` | `MaterialLogic.ImagePath` | Tên file cạnh bản vẽ | Như cách đặt tên PDF của `plot_pdf` |

Số có từ đợt E (cầu thang / mái), **chưa được chủ dự án duyệt riêng, F3 không đổi**:

| Số | Ở đâu | Khách thấy gì |
| --- | --- | --- |
| Tay vịn thang cao **900** mm; tay vịn **50 × 50**, trụ **50 × 50**, trụ cách nhau ≤ **1500** | `StairLogic.RailHeight / RailSize / PostSize / MaxPostSpacing` | Lan can của `build_stair railing` |
| Bậc trên cùng chạy lên sàn trên **100** mm | `StairLogic.TopLedge` | Mép thang trên sàn tầng trên |
| Gợi ý tách vế khi quá **18** bậc / vế | `StairLogic.MaxRisersPerFlightHint` | Cảnh báo trong kết quả |
| Giới hạn thang: cao bậc 100–250, rộng bậc 150–500, vế rộng 500–5000, cao 200–20 000, bản thang 50–500, khe ≤ 5000, chiếu nghỉ 600–10 000 mm | `StairLogic.Min* / Max*` | Số ngoài khoảng bị từ chối |
| Giới hạn mái: bản 30–1000 mm, dốc 5–60°, đua ≤ 3000 mm, ≤ 200 điểm đường bao | `RoofLogic.Min* / Max*`, `MaxPoints` | Như trên |
| Mái đặt trên sàn mái khi đáy sàn cách đỉnh tường ≤ **1** mm; gấp khúc ≤ **1** mm trên tường thẳng không tính là góc | `RoofLogic.SlabOnWallTolMm`, `JogTol` | Mái nằm trên sàn mái; nhà chữ nhật vẽ lệch 1 mm vẫn là chữ nhật |

## Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Mái 2 dốc trên nhà chữ L / T: kiểu nào | Một nóc chính theo cánh rộng nhất, các cánh khác có nóc vuông góc tắt vào mái chính | Kiểu "chữ thập" (mọi nóc chạy tới tường ngoài): dùng thẳng các hình chữ nhật lớn nhất, bỏ bước cắt trong `RightAngledPieces` |
| Nhà góc vuông nhưng phức tạp (bậc thang, răng lược) | Dựng nếu ≤ 40 góc và ≤ 12 mảnh, còn lại từ chối kèm gợi ý | Đổi `MaxRightAngledCorners` / `MaxRoofPieces` |
| Đường bao lõm có tường xiên | Từ chối (mái bằng / vẽ lại góc vuông) | Cần thuật toán "khung xương" tổng quát — đợt sau |
| Vật liệu: cách làm | Material thường (màu + độ trong) + ảnh off-screen Realistic (xem bảng ở mục 2) | Thư viện Autodesk / RENDER: đợt sau, cần thử trên máy thật |
| Gộp lệnh | 2 lệnh mới: `set_material` (gán + liệt kê), `named_view` (lưu / gọi / liệt kê / xóa / ảnh) | Tách ảnh thành lệnh riêng nếu Claude hay dùng sai |
| Gói | Cả hai lệnh thuộc nhóm 3D (gói 3D, Full; chế độ "3D" và "Cả hai" của F1); gói / chế độ 2D không thấy | Chuyển `named_view` sang nhóm chung nếu muốn dùng ở 2D |
| UCS (F1) | Hai lệnh không có tọa độ → giữ WCS (`UcsLogic.WcsTools`); mái theo cơ chế F1 như cũ | — |
| Thẻ xác nhận | `set_material` trên đối tượng có sẵn: luật > 20 đối tượng (như `set_entity_props`); xóa / ghi đè góc nhìn tính 1 thay đổi; không phải "luôn hỏi" (không đổi hình khối) | Thêm vào `ALWAYS_CONFIRM` nếu muốn luôn hỏi |
| Chế độ "Chỉ xem" | `named_view restore` bị chặn như mọi lệnh ghi (khác `set_view_3d` được phép); `list` / `image` vẫn chạy | Cho riêng `restore` qua: cần tách thành lệnh riêng để đưa vào `ReadOnlyAllowed` |
| Ghi đè file ảnh / góc nhìn trùng tên | Phải có `replace: true` (Claude hỏi khách trước) | — |
| Vật liệu trên layer khóa | Bỏ qua đối tượng đó, báo lại; không tự mở khóa | — |

## Token lúc mở chat

Đo như `nang-cap-f1.md` (lệnh gửi Claude + 5 lệnh thẻ: tên + mô tả + schema JSON + system prompt, ~3,5 ký tự / token).

| Gói | Số lệnh trước → sau | Ký tự trước (`b2e6431`) | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 105 → 105 | 136 357 | 136 357 | 39,0k (không đổi) |
| 3D | 93 → 95 | 140 194 | 143 720 | 40,1k → 41,1k (+1,0k) |
| Full | 136 → 138 | 202 969 | 206 449 | 58,0k → 59,0k (+1,0k) |

Chế độ 2D / 3D của F1 trên máy Full: như gói 2D / 3D (+ đoạn "Work mode" như trước).

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1001** (973 + 28: `NangCapF3RoofTests.cs` 13 — mái 4 dốc L / T / U khớp
  mái chuẩn ở 250 điểm, độ dày cột đều (không chồng) trên L / T / U / L xoay / T xoay cho cả 4 dốc và 2 dốc, mặt dưới chạm
  đỉnh tường, nóc chính 2 dốc, chữ nhật / lồi / mái bằng giữ như cũ, hình chữ nhật lớn nhất của L / T / U / chữ thập, từ
  chối tường xiên / quá mảnh / quá góc, số mặc định mới và macro ghi số 150; `NangCapF3RenderTests.cs` 15 — vật liệu mẫu,
  kiểm màu / độ trong / tên, góc nhìn, đường dẫn PNG, nhãn gói 3D, chế độ F1, UCS). Test cũ sửa theo quyết định mới: số
  150 → 120 (`NangCapETests`, `NangCapETestSuaTests`), KT2-08 / KT2-09 (nhà chữ L giờ dựng được; tường xiên vẫn từ chối),
  số lệnh 3D 37 → 39 (`CrossPlanTests`, `SolidTests`).
- Trợ lý `cd agent && npm ci && npm test`: **317** (311 + 6 `test/nangcapf3.test.ts`); sửa theo quyết định mới:
  `nangcape.test.ts` (số mặc định tách mái bằng / dốc), `crossplan.test.ts` (2 lệnh 3D mới vào các lớp thẻ xác nhận, 31 → 33).
- Build 3 target: xem mục CI.

## CI

*(điền sau khi chạy)*

## Chưa kiểm được

- Mọi thứ trên AutoCAD thật: AutoCAD cộng / trừ khối các mảnh mái (có thể báo lỗi với hình lạ — khi đó lệnh báo "không
  ghép được các mảnh mái", không dựng gì), vật liệu hiện trong Realistic và trong ảnh off-screen, lưu / gọi góc nhìn, file
  PNG. Bước tự kiểm `CLAUDECHATSELFTEST` mới chưa chạy. Thử tay: `docs/checklist-dot-f3.md`.
