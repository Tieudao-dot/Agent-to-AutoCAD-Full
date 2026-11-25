# Kiểm tra độc lập đợt F (F1 → F4)

*Báo cáo của thread "Kiểm tra độc lập đợt F" (`session_01D56HmHz4EXXJceBgkmjcap`, agent mới, chưa viết hay test phần nào),
gửi về luồng chính 25/11/2026 19h57; luồng chính lưu nguyên văn. Thread này chỉ ghi lỗi, không sửa, không commit, không chạy
CI.*

Nhánh `full/nang-cap-f-tong-the` @ `afdaa68` (đợt F = `2dbeee2..afdaa68`). Test thử viết tạm rồi đã xóa.

**Tóm tắt:** 8 lỗi (1 nặng, 3 vừa, 4 nhẹ). Phân việc sửa và kết quả soát lại: xem mục cuối file.

## Test đã chạy

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: 1132 / 1132 đạt.
- Trợ lý `npm ci && npm test`: 355 / 355 đạt.
- Build net48 / net8.0-windows / net10.0-windows (Release, xóa obj / bin giữa các lần): 0 lỗi, 0 cảnh báo.
- 4 test thử chứng minh KTF-01, KTF-03, KTF-04 (đã xóa).
- Máy chưa có .NET: đã cài dotnet-sdk-10.0 + runtime 8 từ apt Ubuntu.

## Bảng lỗi

**KTF-01 — Nặng — F3** — `Core/RoofRightAngled.cs` 35–48 (`IsRightAngled` dung sai 2e-3), 71–74
(`MaximalRectangles` / `Unique` 0,5 mm), 62 (`Inside` 1e-6), 125–153, 147, 239.

Mái 2 dốc nhà L / T / U có đường bao lệch nhỏ (~1 mm, hay gặp khi vẽ tay hoặc xoay rồi làm tròn) bị hỏng. Phép thử góc
vuông nhận lệch tới ~20 mm / 10 m, nhưng chỉ gộp tọa độ trong 0,5 mm. Kết quả: ra hình chữ nhật gần trùng, một bản "rộng
hơn chút" thành cánh có nóc chạy ngang mái chính.

- Test: nhà T (thanh 12 × 4 m, chân rộng 4 m), `RoofLogic.Build(T, 3000, gable)`.
  - Đúng góc: 2 mảnh, TopZ 4293.
  - Dời 1 góc 1 mm (12000,10000 → 12000,10001): 5 mảnh, TopZ **6603**, đầu trái 3716.
- Xoay rồi làm tròn 1 mm (agent rà đo):
  - T 37° / 133°: bị từ chối "cần 14 / 15 mảnh".
  - U 13° / 133°: TopZ 6025.
  - L cánh hẹp 37°: TopZ 5159, đúng phải 3485.
- Mái 4 dốc giữ đúng cao nhưng thành 4–7 mảnh có mảnh mỏng < 1 mm (dễ lỗi boolean AutoCAD, chưa chứng minh).
- Góc 0 / 90 / 180 / 270°: đúng.

Đúng phải là: lệch dưới ngưỡng thì ra như đường bao vuông. Gợi ý: nắn đường bao về góc vuông (gộp x / y cục bộ với dung
sai bằng ngưỡng góc vuông, vd max(5 mm, 2e-3 × kích thước)) trước khi chia hình chữ nhật; thêm test T lệch 1 mm và T xoay
+ làm tròn.

**KTF-02 — Vừa — F4** — `Core/Util2D.cs` 480–484 (`M2` thay '.' bằng ','), `Core/Solids3.cs` 885–886 (`Num`, dùng cho
VolumeTable / ClashTable), `Core/Xlsx.cs` 47 + 65–67 (`CellValue` giữ "1,5" là chữ), `Drawing/ExcelExport.cs` 31.

Cột diện tích m², thể tích m³, thể tích va chạm trong file Excel là **chữ**, Sum không cộng được; chỉ STT và số lượng là số.

- Tái hiện: `XlsxWriter.SheetXml(AreaTable(...12,5 m²...))` ra `<c t="inlineStr">…12,50…</c>`.
- Lệch lời hứa "số cộng được" (bàn giao F4 dòng 73, `bo-cai/README.md` 128). Mục F5-1 của checklist tổng sẽ không đạt.

Đúng: `<v>12.5</v>`. Gợi ý: xuất Excel từ số thực, hoặc đổi "d,dd" của chính các cột số thành số.

**KTF-03 — Vừa — F1** — `Core/Ucs.cs` `BoxFromWcs` / `PlanBoxInside`; dùng ở `Drawing/QueryTools.cs` 83–95
(`list_entities` window) và `Drawing/PlanTo3dTools.cs` 172–174 (`build_3d_from_plan` area_min / area_max).

UCS xoay góc không vuông (vd 30°): chọn theo khung bỏ sót hình nằm trọn trong khung, vì code lấy khung bao WCS rồi bao lại
theo UCS nên khung phình to.

- Test: đường dài 100 dọc trục X của UCS 30° có khung UCS (0; −43,3)…(100; 43,3). `PlanBoxInside` với khung
  (−10,−10)…(110,10) trả **False**.
- Crossing thì ngược lại: chọn thừa hình ngoài khung.
- Mục F7-8 có thể sót tường sát mép khung.

Gợi ý: lấy khung bao của chính hình trong UCS (bản sao biến đổi WCS → UCS rồi `GeometricExtents`, hoặc
`GeometryExtentsBestFit(matrix)`).

**KTF-04 — Vừa — F1** — `Core/Ucs.cs` `ConvertNumber` (`Height`: `v + O[2]`); `PlanTo3dTools.cs` 495–496 (`base_z` mm),
`StairRoofTools.cs` 349 (`wall_top_z` mm).

Cao độ tuyệt đối tính bằng mm bị cộng thẳng gốc z UCS tính bằng **đơn vị bản vẽ**. Bản vẽ mét / cm có UCS nâng lên tầng 2
thì dựng sai cao độ.

- Test: UCS gốc z = 3,3 (bản vẽ mét), `base_z: 0` đọc ra **3,3 mm** thay vì 3300 mm. Bản vẽ mm đúng.

Gợi ý: cộng `O[2] × mm/đơn vị`.

**KTF-05 — Nhẹ — F1** — `Core/Params.cs` 64 (`Num(name, def)` trả mặc định không qua `ConvertNumber`); `Ucs.cs`
`HeightParams`. Hai chỗ không nhất quán (đọc code):

- (a) Bỏ trống `base_z` thì dựng ở z WCS 0, còn ghi `base_z: 0` thì dựng ở gốc z UCS.
- (b) Bản Ref của `build_railing` (path_handle), `build_stair` (well_handle), `build_beam` (path_handles) không cộng gốc z
  UCS vào base_z / top_z; bản Create của cùng lệnh thì có.

Gợi ý: mặc định cũng qua `ConvertNumber`; thêm các tham số trên vào `HeightParams` (hoặc ghi rõ trong mô tả lệnh).

**KTF-06 — Nhẹ — F4** — `ExcelExport.cs` 24 (`data.Count <= 1`), `Util2D.cs` 281–300 (`BlockTable` luôn có dòng Tổng),
`Util2DTools.cs` 188.

- `block_schedule` không có block nào vẫn ghi file Excel chỉ có dòng "Tổng 0", báo `excel.rows = 1`. Bàn giao F4 ghi
  "không có dòng → báo, không tạo file".
- `rows` luôn tính cả dòng Tổng (3 block báo 4).

Gợi ý: kiểm `rows.Count == 0`; `rows` = `data.Count − 2`.

**KTF-07 — Nhẹ — Tài liệu / checklist (F2)** — `docs/dot-f-cho-duyet.md` 40 và checklist tổng 299 (F3-5) ghi bản vẽ mét
hiện "4.00". Code `Drawing/StyleTools.cs` 131 đặt `Dimzin = 8` (bỏ số 0 thừa) nên khách thấy "4": làm theo F3-5 sẽ báo
hỏng sai.

Gợi ý: sửa ví dụ thành "4" / "4.5" (muốn hiện 4.00 thì đổi DIMZIN, cần chủ dự án duyệt).

**KTF-08 — Nhẹ — Skill (F2)** — `skills/ve-mep/SKILL.md` 18–19: có hai bước "4.". Đổi bước cũ thành 5.

## Nghi vấn chưa chứng minh

- **F4 — tiến trình Claude bị bỏ lại** (`agent/src/index.ts` 145–152): AutoCAD chết mà không đóng pipe thì
  `process.exit(0)` chạy mà không `session.close()`. Có từ trước, nhưng từ F4 có tiến trình chạy sẵn nên dễ còn sót trên
  Windows.
- **F4 — khởi động sẵn sau khi chuyển bản vẽ hay bị phí:** dùng skill của chat trước, plugin không gửi prewarm mới khi
  chuyển bản vẽ; tài liệu chờ duyệt lại ghi có.
- **F4 — mất file gốc khi ghi đè:** ghi đè Excel = `File.Delete` rồi `Move`, không nguyên tử; nên dùng `File.Replace`.
- **F4 — chữ trông như số:** chuỗi số dài / "1.10" bị đổi thành số (phần lớn là quyết định đã ghi).
- **F3 — ảnh từ góc nhìn đã lưu** (`RenderTools.cs` 238–250): bỏ zoom / độ xoay / phối cảnh đã lưu, khung cả bản vẽ.
- **F3 — tên vật liệu mẫu đè vật liệu của khách:** khách có vật liệu tên "Glass", "Metal", "tile", "granite" thì bị thay
  bằng vật liệu mẫu (`RenderTools.cs` 57–68, `Materials.cs` 31–38).
- **F3 — đổi màu vật liệu có sẵn không hỏi:** đổi color / opacity của vật liệu khách đã có thì đổi cho mọi khối dùng nó,
  không thẻ (chỉ Global được chặn).
- **F2 — tên block thư viện không ghi đơn vị** (`CUA-DI-900`): block cùng tên vẽ ở đơn vị khác được dùng lại sai cỡ.
- **F2 — block thư viện lấy màu hiện hành:** `insert_library_block` không đặt ByLayer cho block reference, nên Properties
  hiện màu hiện hành (hình bên trong vẫn ByLayer).
- **F2 — hoàn tác trạng thái layer** đã xóa / ghi đè: chưa kiểm được.
- **F1 — bảng của 4 lệnh thống kê** trong UCS xoay không xoay theo (đã ghi ở giới hạn F1).

## Đã kiểm, không thấy lỗi

- **F1 UCS:** phân loại đủ 143 lệnh Create / Ref / Wcs; nhánh đọc hình có sẵn đều là Ref; hình trên layout không bị dời;
  thuộc tính block đi theo block; điểm bấm chọn; zoom_window / stretch theo khung UCS; macro `_ucs` và macro cũ; batch
  theo UCS từng lệnh; World đi y đường cũ.
- **F1 chế độ:** chỉ có hiệu lực trên máy Full; lượt đang chạy giữ chế độ cũ; lời chặn nói "đổi chế độ", không mời mua
  gói; prompt đúng phần; macro không bị giới hạn (đúng quyết định).
- **Gói:** setup_standards / create_sheet thuộc 2D; insert_library_block / layer_state thuộc mọi gói; set_material /
  named_view thuộc 3D; Excel bảng 3D chạy trên gói 3D.
- **F4 nạp gọn:** lọc theo gói / chế độ trước khi nạp, nên ToolSearch không lộ lệnh ngoài gói; vẫn chặn NOT_IN_PLAN; đủ 25
  tên; thẻ xác nhận và Chỉ xem không phụ thuộc việc hoãn; công tắc tắt chạy hai chiều, đường dẫn khớp tài liệu.
- **F4 khởi động sẵn:** chỉ khi máy được phép và chat rảnh; tin đầu khác gói / chế độ / skill thì khởi động lại có resume;
  tin gửi trong 3 giây chờ vẫn đúng; tối đa 3 lần dựng lại; đóng pipe thì close.
- **F4 Excel:**
  - thoát ký tự XML (cả emoji, surrogate), tên sheet, cột sau Z đều đúng;
  - zip hợp lệ, openpyxl mở được;
  - xuất lỗi thì bảng trong bản vẽ cũng hủy;
  - luật ghi đè / file đang mở như STL;
  - bản vẽ chưa lưu thì file vào Documents;
  - macro bỏ excel / overwrite (cả trong batch);
  - không có va chạm thì không tạo file.
- **F2:**
  - đơn vị đúng (DIMSCALE, chữ, LTSCALE, tỉ lệ khung nhìn, hậu tố -M / -CM / -DM / -IN / -FT);
  - khung tên 180 × 52 cộng đúng, A4 đứng vừa;
  - không ghi đè thầm (style, layout, layer state);
  - khung nhìn xoay theo UCS (đọc code).
- **F3:** 120 / 150 / 120 mm khớp giữa code, mô tả lệnh, skill và tài liệu; mái L / T / U đúng góc vuông ở 10 góc xoay,
  cùng / ngược chiều kim đồng hồ, điểm trùng, cánh hẹp; Global được chặn; PNG / góc nhìn trùng tên cần replace / overwrite.
- **Tài liệu:** dot-f-cho-duyet khớp code trừ KTF-07; bo-cai README khớp; checklist đợt F chạy được theo code trừ F5-1
  (KTF-02), F3-5 (KTF-07) và có thể F7-8 (KTF-03).

## Chưa kiểm được (cần AutoCAD / Claude / Excel thật)

- Mọi phần chạy trên AutoCAD (dời vào UCS, khung nhìn, layer state, PNG, vật liệu, boolean mái, CLAUDECHATSELFTEST).
- UI WPF (chỉ CI Windows, thread này không chạy CI).
- Claude thật (ToolSearch, token tin đầu, khởi động sẵn không gọi Claude).
- Excel thật (Sum, khóa file khi đang mở).

## Thời gian, chi phí

- Khoảng 1 giờ theo thread (luồng chính đo: mở 19h45, báo cáo 19h57), $9,95.
- Tốn nhất: hai agent con rà song song F2 + F3 và F4 (~7 phút, ~200k token mỗi agent), cài .NET SDK.

## Phân việc sửa (luồng chính, 25/11 20h10)

| Mã / nghi vấn | Thread sửa | Nhánh |
| --- | --- | --- |
| KTF-02, 03, 04, 05, 06; nghi vấn F4 (tiến trình bị bỏ lại, khởi động sẵn khi chuyển bản vẽ, ghi đè dùng `File.Replace`) | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (đã viết F1 và F4) | `full/nang-cap-f-sua-ktf` (từ `afdaa68`) |
| KTF-01, 07, 08; nghi vấn F3 (tên vật liệu mẫu đè vật liệu của khách, ảnh từ góc nhìn đã lưu) và F2 (màu block reference, tên block không ghi đơn vị, mục thử tay hoàn tác trạng thái layer) | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (giữ nhánh tích hợp) | `full/nang-cap-f-tong-the` |
| Nghi vấn F3 "đổi màu vật liệu có sẵn không hỏi" | Thread đợt F5 `session_01D56HmFrL65iF9oJokvqXw5` (đang làm thẻ xác nhận) | `full/nang-cap-f5` |
| Không sửa, đưa chủ dự án xem: "chữ trông như số" trong Excel; bảng thống kê không xoay theo UCS | — | `docs/dot-f-cho-duyet.md` |

Thread giữ nhánh tích hợp gộp các bản sửa và chạy CI; thread kiểm tra độc lập soát lại từng lỗi trên commit đó.

## Soát lại lượt 1 (25/11 20h20, trên `f037b79`)

Bản sửa: `4399fa0` (thread F1: KTF-02…06 và 3 nghi vấn F4) + `9743c18` (thread F2: KTF-01, 07, 08 và nghi vấn F2 / F3), gộp ở
`f037b79`. Người soát chạy lại: lõi 1163 / 1163, trợ lý 355 / 355, build `net8.0-windows` 0 lỗi.

| Mã | Kết luận |
| --- | --- |
| KTF-01 | **Sửa chưa hết** (chi tiết dưới) |
| KTF-02 | Đúng: cả 4 bảng có cột (m²) / (m³) thành số, dòng Tổng cũng là số; chữ ("chưa đo", "3 cặp") giữ là chữ |
| KTF-03 | Đúng theo code: window và crossing dùng khung bao của chính hình trong UCS; cần AutoCAD thật để chạy |
| KTF-04 | Đúng: UCS gốc z 3,3 m / 330 cm / 3300 mm đều cho `base_z 0` = 3300 mm |
| KTF-05 | Đúng: bỏ trống `base_z` = `base_z: 0`; `top_z` dầm Ref theo UCS; lan can / cầu thang Ref chỉ đổi khi có ghi `base_z`. Phụ: góc hatch mặc định giờ theo UCS, đúng như lệnh HATCH |
| KTF-06 | Đúng: bảng block rỗng không tạo file; `rows` bỏ dòng Tổng |
| KTF-07 | Đúng: tài liệu và checklist ghi "4" / "4.5" |
| KTF-08 | Đúng: skill MEP đánh số 1–5 |
| 2 test cũ bị sửa | Giữ đúng ý test |

Nghi vấn đã xử lý, kết luận theo đọc code (chưa chạy AutoCAD): trợ lý đóng Claude trước khi thoát; prewarm sau chuyển bản
vẽ / Chat mới; ghi đè Excel bằng `File.Replace`; block reference ByLayer; vật liệu khách trùng tên vật liệu mẫu không bị đè;
block cùng tên khác cỡ thêm đuôi `-TV`; mô tả `named_view` ghi rõ ảnh không giữ zoom — đều đúng.

**KTF-01 còn sai:** đã đúng khi xoay + làm tròn 1 mm (T / L / L cánh hẹp / U, 2 dốc và 4 dốc, 11 góc), riêng T 2 dốc ở 30°
ra 3 mảnh thay vì 2. Còn sai khi nhà vẽ tay lệch 1–2 mm: nhà T mỗi góc lệch ngẫu nhiên ±2 mm thì 13 / 30 lần ra nóc
6602–6603 (đúng 4293); nhà L 12 × 12 m có bậc 2 mm ở một cạnh trong ra nóc 6603 (đúng 4871), bậc 3 / 5 / 10 / 20 mm cũng
vậy; mái 4 dốc cùng ca nóc đúng nhưng 3 mảnh thay vì 2. Luồng chính giao thread F2 sửa tiếp (20h22), kèm thử các bậc thật
nhưng nhỏ 50–500 mm.

Ghi chú nhẹ: block đuôi `-TV` có sẵn được dùng lại mà không kiểm cỡ (hiếm); `list_entities` theo khung trong UCS xoay có
thể chậm hơn trên bản vẽ rất nhiều khối 3D (chưa đo); checklist F6-12 đứng trước F6-11.

## Soát lượt cuối (25/11 20h54, trên `fc92cd6`, nhánh `full/nang-cap-f5-test`)

`fc92cd6` = nhánh tích hợp @ `03bf163` (KTF-01 lần 2) + đợt F5 + bản sửa của người rà F5. Người soát chạy lại: lõi
1212 / 1212, trợ lý 364 / 364, build `net8.0-windows` 0 lỗi.

**KTF-01: đã sửa đúng.** 7 hình (T, L, L cánh hẹp 1,5 m, U, L cánh mảnh, Z, chữ thập) × mái 2 dốc và 4 dốc, mỗi hình 120
lần (nhiễu ±1 / ±2 / ±3 mm mỗi góc, hạt ngẫu nhiên mới, xoay 8 góc kèm làm tròn 1 mm, gốc tọa độ lớn): **sai 0 / 1680**
(cùng các ca này trên `f037b79`: T 2 dốc sai 103 / 120). Hình đúng góc vuông cho kết quả giống hệt bản trước khi sửa. Bậc
thật trên nhà L 12 × 12 ở 3 vị trí: bậc 2 / 10 mm được gộp; bậc 50–1000 mm thì nóc 2 dốc = nóc 4 dốc của cùng nhà.

**KTF-09 — Nhẹ — F3 (có từ F3)** — `Core/RoofRightAngled.cs` `IsRightAngled` (dung sai góc 2e-3 tính theo từng cạnh). Nhà
vẽ tay lệch vài mm bị từ chối "không phải góc vuông" khi có cạnh ngắn: L cánh hẹp 1,5 m nhiễu ±2–3 mm bị từ chối 22 / 120
lần (2 dốc), 14 / 120 (4 dốc); T, chữ thập 6–8 / 120. Kết quả không sai (báo rõ, gợi ý mái bằng), chỉ khó dùng. Gợi ý: cho
phép tối thiểu theo khoảng cách tuyệt đối (lệch ngang ≤ max(5 mm, 2e-3 × cạnh)).

**Đợt F5: không thấy lỗi chứng minh được.** Đã kiểm: ô ⚙ "Nạp gọn lệnh" (mặc định, lưu theo máy, biến môi trường / file
thắng ô); thẻ xác nhận không lách được bằng tham số cũ, `user_confirmed` tự ghi, batch, mã của file / việc khác; Hủy thì
không đổi gì; "Chỉ xem"; ghép với các bản sửa KTF; kiểu ghi chú mũi tên theo đơn vị / tỷ lệ; checklist F5 khớp code.

**Nghi vấn chưa chứng minh (cần Claude thật):** tắt ô "Nạp gọn lệnh" giữa cuộc chat sau khi Claude đã dùng ToolSearch;
cuộc chat nối tiếp với ToolSearch đã tắt trong khi lịch sử còn các khối tham chiếu lệnh, có thể bị từ chối.

**Xử lý (luồng chính, 20h56):** giao thread giữ nhánh tích hợp sửa KTF-09 và đổi ô ⚙ sang "cuộc chat đã có lượt nào thì giữ
nguyên kiểu nạp tới Chat mới" (cách an toàn, tránh hẳn tình huống trên), rồi chạy lượt CI cuối; người soát kiểm nhanh hai
điểm này.

Tổng chi phí thread kiểm tra độc lập tới lượt này: ~$13,3.
