# Bàn giao: Nâng cấp đợt F2 — 2D dùng hằng ngày

Nhánh `full/nang-cap-f2`, tách từ `full/nang-cap-f1-test` @ `b2e6431`. Đợt F chốt 25/11/2026; F2 chạy song song với F3
(3D, nhánh `full/nang-cap-f3`), F4 sẽ gộp hai nhánh. F2 chỉ đụng phần 2D.

Chưa thử trên AutoCAD thật: phần đụng AutoCAD chỉ được kiểm bằng build 3 target, test lõi / trợ lý và CI (UI test WPF).
Bước tự kiểm `CLAUDECHATSELFTEST` có thêm 1 bước F2 để kiểm trên máy thật. Thử tay: [`docs/checklist-dot-f2.md`](../checklist-dot-f2.md).

## Bốn lệnh mới (gộp ít lệnh để đỡ token)

| Lệnh | Gói | Khách nói | Làm gì |
| --- | --- | --- | --- |
| `setup_standards` | 2D, Full | "thiết lập style chuẩn 1:100" | Kiểu chữ `VN-ARIAL` (Arial, rộng 0,85), kiểu kích thước `VN-<tỉ lệ>` (`KT-…` khi Tiêu chuẩn = Kiến trúc, đúng tên skill), nạp 5 nét (CENTER, DASHED, HIDDEN, PHANTOM, DASHDOT), LTSCALE; đặt hiện hành; trả chiều cao chữ nên dùng theo tỉ lệ. Chạy lại: không tạo trùng (báo `unchanged`). Style trùng tên mà khác số: **giữ nguyên** (`kept` + chỗ khác), chỉ ghi đè sau thẻ Đồng ý |
| `create_sheet` | 2D, Full | "tạo trang in A3 1:100, công trình …" | Layout mới khổ A0–A4 (DWG To PDF, full bleed, in 1:1), khung bản vẽ, khung tên (block `KHUNG-TEN-VN` có 10 ô thuộc tính), khung nhìn khóa theo tỉ lệ (không có tỉ lệ: tỉ lệ chuẩn nhỏ nhất vừa bản vẽ). Không bao giờ sửa layout có sẵn. In bằng `plot_pdf` |
| `insert_library_block` | mọi gói (như `insert_block`) | "chèn cửa đi 900", "liệt kê thư viện block" | 15 ký hiệu mặt bằng do code dự án vẽ; tham số `width` / `depth` (mm), `rotation`, `flip`, `positions`. Không có `name` = liệt kê thư viện |
| `layer_state` | mọi gói (như lệnh layer) | "lưu / gọi lại / liệt kê / xóa trạng thái layer …" | Dùng đúng LAYERSTATE của AutoCAD (`LayerStateManager`): nhớ bật/tắt, đóng băng, khóa, in/không in, màu, nét, độ dày nét. Lưu đè tên cũ / xóa: qua thẻ Đồng ý. Gọi lại báo số layer đã đổi |

Code:
- Lõi (không cần AutoCAD, có test): `plugin/ClaudeCAD.Plugin/Core/Standards2D.cs` — `F2Numbers` (**mọi con số khách
  thấy ở một chỗ**), `StandardsLogic`, `SheetLogic` (khổ giấy, khung, khung tên, khung nhìn, tỉ lệ vừa), `LibraryBlocks`
  (thư viện + hình), `LayerStateLogic`, `F2Confirm` (lời từ chối khi chưa qua thẻ).
- Plugin: `Drawing/Standards2DTools.cs` (4 lệnh), đăng ký ở `Drawing/DrawingService.cs` (1 dòng).
- Dòng thêm vào file dùng chung (không sửa / sắp xếp dòng khác): `Core/Plans.cs` (nhãn gói), `Core/Ucs.cs` (kiểu UCS),
  `UI/ToolLabels.cs` (nhãn tiếng Việt), `SelfTest.cs` (bước F2), `agent/src/tools/index.ts` (2 dòng),
  `agent/src/agent/confirmation.ts` (thẻ cho 2 lệnh), `agent/src/agent/systemPrompt.ts` (1 dòng gợi ý 2D + phạm vi gói),
  `bo-cai/README.md` (1 dòng). Trợ lý: `agent/src/tools/standards2d.ts`.
- Skill: `ve-kien-truc` (bước chuẩn bị dùng `setup_standards`; cửa / thiết bị lấy từ thư viện; trang in `create_sheet`),
  `ve-mep`, `ve-ha-tang` (1–2 dòng mỗi file).

### UCS (theo cơ chế F1)

- `insert_library_block`: kiểu **Create** (như `insert_block`): chèn như World rồi cả block được đưa vào UCS. Định nghĩa
  block không bị biến đổi (chỉ hình mới trong model space).
- `create_sheet`: kiểu **Ref**: `view_center` là điểm theo UCS hiện hành (khác `add_viewport` cũ giữ WCS). UCS xoay trên
  mặt bằng (trục Z thẳng đứng) thì khung nhìn xoay theo (giống lệnh PLAN): trục X của UCS nằm ngang trên giấy
  (`TwistAngle = 2π − góc UCS`, tâm nhìn đổi sang hệ hiển thị). UCS nghiêng: khung nhìn nhìn từ trên xuống theo WCS.
- `setup_standards`, `layer_state`: kiểu **Wcs** (cài đặt, không có tọa độ).
- UCS World: y như không có UCS.

### Thẻ xác nhận

`setup_standards` (khi có `handles`) và `layer_state` (lưu đè / xóa) nằm trong `REPLACE_TOOLS` của trợ lý cùng
`find_replace_text`: chỉ nút Đồng ý của thẻ có đúng handle mới cho qua, gõ "đồng ý" không tính; plugin kiểm lại bằng
`user_confirmed`. Handle ở đây là handle của bản ghi style / trạng thái layer (không phải hình): nút "Xem trước" của thẻ
không có gì để tô sáng (báo "không còn trong bản vẽ"), không ảnh hưởng Đồng ý / Hủy.

## Con số chờ chủ dự án duyệt

Tất cả nằm ở `F2Numbers` (`plugin/ClaudeCAD.Plugin/Core/Standards2D.cs`); đổi số nào thì sửa đúng một dòng đó (và số
trong test `NangCapF2Tests.cs` nếu test ghi số). "Theo thông lệ" = cách vẽ phổ biến ở Việt Nam, tớ không chắc là số
của một TCVN cụ thể.

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Chữ kích thước **2,5 mm** khi in | kiểu kích thước `VN-…` / `KT-…` | số kích thước in ra cao 2,5 mm (1:100 → cao 250 trong bản vẽ) | đúng số trong skill `ve-kien-truc` mục 4; theo thông lệ |
| Đầu kích thước **gạch chéo kiến trúc, 1,2 mm** | kiểu kích thước | đầu đường kích thước là nét gạch chéo | skill `ve-kien-truc` |
| Đường gióng cách vật **2 mm**, vượt đường kích thước **1,5 mm**, đường kích thước vượt gióng **1 mm**, chữ cách đường **0,8 mm** | kiểu kích thước | khoảng hở / phần thò của nét kích thước | skill `ve-kien-truc` |
| Số lẻ: **0** (mm), 1 (cm), **2** (m) | kiểu kích thước | 4000 / 4.00 | mm làm tròn; bản vẽ mét ghi 2 số lẻ theo thông lệ |
| Font **Arial**, hệ số rộng **0,85** | kiểu chữ `VN-ARIAL` / `KT-ARIAL`, `VN-KHUNG` | chữ có dấu, hơi hẹp | skill `ve-kien-truc` (KT-ARIAL 0.85) |
| Chiều cao chữ nên dùng: tiêu đề **5**, tên phòng **3,5**, ghi chú **2,5**, chữ nhỏ **1,8** mm khi in | `text_heights` trả cho Claude | Claude ghi chữ theo các cỡ này (1:100 → 500 / 350 / 250 / 180) | 3,5 / 2,5 là skill; 5 và 1,8 theo thông lệ |
| **LTSCALE = mẫu số tỉ lệ × 0,5** (1:100 → 50), PSLTSCALE 0 | `setup_standards` | nét đứt / nét chấm gạch hiện vừa mắt cả ở Model lẫn khung nhìn | theo thông lệ với acadiso.lin; chỉ đặt khi LTSCALE còn là 1 (mặc định) |
| Tỉ lệ mặc định **1:100** | `setup_standards`, `create_sheet` | khi khách không nói tỉ lệ và ô Tỉ lệ để Tự động | thông dụng nhất cho mặt bằng |
| Khổ giấy A0 841×1189, A1 594×841, A2 420×594, A3 297×420, A4 210×297 | `create_sheet` | đúng khổ in | ISO 216 (chắc chắn) |
| Mặc định **A3 ngang**; **A4 đứng**; khổ lớn ngang | `create_sheet` | hướng giấy khi không nói | theo thông lệ |
| Lề khung: trái **25 mm**, ba cạnh còn lại **5 mm** | `create_sheet` | khung bản vẽ cách mép giấy | theo thông lệ (lề đóng gáy) |
| Nét khung bản vẽ và viền khung tên **0,5 mm**, nét trong khung tên **0,25 mm** | `create_sheet` | độ đậm nét in | theo thông lệ |
| Khung tên rộng **180 mm**, 6 dòng cao **8 / 8 / 8 / 12 / 8 / 8** (tổng 52 mm); cột nhãn **30 mm**; 2 dòng dưới chia **22 / 38 / 22 / 38 / 22 / 38** | `create_sheet` | kích thước, vị trí các ô khung tên | 180 vừa A4 đứng (210 − 25 − 5); theo thông lệ |
| Chữ khung tên: nhãn **2 mm**, giá trị **2,5 mm**, tên bản vẽ **3,5 mm** | `create_sheet` | cỡ chữ trong khung tên | theo thông lệ |
| Ô khung tên: ĐƠN VỊ TK, CÔNG TRÌNH, HẠNG MỤC, TÊN BẢN VẼ, NGƯỜI VẼ, KIỂM TRA, NGÀY, TỈ LỆ, SỐ BẢN VẼ, KHỔ GIẤY | `SheetLogic.Fields` | các ô điền được | theo yêu cầu + thông lệ; ngày dạng dd/MM/yyyy |
| Khung nhìn cách khung / khung tên **5 mm**, **khóa** sẵn, viền trên layer không in | `create_sheet` | vùng hình trên giấy | theo thông lệ |
| Layer `KHUNG` (màu 7) cho khung + khung tên, `KHUNG-VP` (màu 8, không in) cho viền khung nhìn | `create_sheet` | tên layer mới | tự đặt |
| Tỉ lệ chuẩn tự chọn: 1:1, 2, 5, 10, 20, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000, 2000, 5000 | `create_sheet` không có tỉ lệ | tỉ lệ được chọn | theo thông lệ |
| Thư viện block: kích thước mặc định (min–max) mm — cửa đi 900 (500–1500); cửa 2 cánh 1400 (1000–3000); cửa trượt 1800 (1000–4000) tường 220; cửa sổ 1200 (300–4000) tường 220 (100–400); bồn cầu 400×700; lavabo 500×420; bồn tắm 1700×750; khay tắm 900×900; giường 1600×2000; sofa 2000×850; bàn 1200×800; ghế 450×450; tủ 1200×600; bếp 2 vùng 700×450; cây đường kính 3000 | `LibraryBlocks.Items` | cỡ ký hiệu khi không nói số | theo thông lệ (tường 220 = skill) |
| Chi tiết hình block: cánh cửa là 1 nét (như skill), cửa trượt cánh dày 40 chồng 50; cửa sổ 3 nét (chia 3 bề dày); két nước bồn cầu = 26 % chiều sâu; gối giường cao 350, cách 100; sofa tay vịn 150, tựa 200, mỗi chỗ ~600; ghế tựa 80; tủ có 2 đường chéo; cây = vòng tán + hình sao 16 đỉnh + gốc | `LibraryBlocks` | hình dáng ký hiệu | tự vẽ theo thông lệ |
| Layer block mặc định: A-DOOR (màu 4), A-WIND (5), A-FIXT (6), A-FURN (8), L-PLNT (3) | `insert_library_block` | layer khi không nói | A-DOOR / A-WIND theo skill; còn lại theo thông lệ; màu chỉ đặt khi layer chưa có |
| Tên block: `CUA-DI-900`, `CUA-DI-2C-…`, `CUA-DI-TRUOT-…`, `CUA-SO-1200` (tường khác 220: `CUA-SO-1200x110`), `TB-…-WxD`, `NT-…-WxD`, `CAY-<đường kính>` | `LibraryBlocks.BlockName` | tên trong danh sách block | `CUA-DI` / `CUA-SO` theo skill, nên `build_3d_from_plan` nhận ra |

## Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Số lệnh | 4 lệnh, mỗi phần 1 lệnh (danh sách thư viện = gọi `insert_library_block` không tên; trạng thái layer 4 việc trong 1 lệnh) | tách thành lệnh riêng nếu Claude hay gọi nhầm |
| Tiêu chuẩn "Kiến trúc" | tên `KT-ARIAL` / `KT-<tỉ lệ>` như skill; Tự động / MEP / Hạ tầng dùng `VN-…`, cùng bộ số | thêm bộ số riêng cho MEP / hạ tầng trong `StandardsLogic` |
| Style trùng tên khác số | giữ nguyên, không đặt hiện hành, báo chỗ khác; ghi đè sau thẻ | — |
| LTSCALE | chỉ đặt khi còn bằng 1 (mặc định AutoCAD), kèm PSLTSCALE 0; khách đã đặt khác thì giữ, báo số nên dùng | cho ghi đè qua thẻ (cần handle giả cho biến hệ thống) |
| Bản vẽ mét / cm | kiểu kích thước riêng `VN-100-M` / `VN-100-CM`, DIMSCALE = tỉ lệ / (mm mỗi đơn vị), số lẻ 2 / 1 | — |
| Multileader style | không tạo (đợt này chỉ chữ, kích thước, nét) | thêm vào `setup_standards` |
| Khung tên | là block có thuộc tính `KHUNG-TEN-VN` (sửa ô bằng `EATTEDIT` hoặc `set_block_attributes`); block trùng tên đã có thì dùng nguyên | đổi hình trong `SheetLogic.BuildTitleBlock` |
| Khung nhìn | nằm trên khung tên (không chồng lên khung tên); khóa sẵn | `ViewportGap` / bỏ khóa bằng `locked: false` |
| `view_center` của `create_sheet` | theo UCS hiện hành (lệnh mới hiểu UCS); khung nhìn xoay theo UCS xoay | — |
| Khổ giấy máy in không có đúng chiều | thử chiều ngược + xoay 90° khi in; không có cả hai thì báo lỗi và xóa layout vừa tạo | — |
| Block thư viện đã có tên trong bản vẽ | dùng nguyên, không định nghĩa lại (có thể là block khách tự vẽ theo skill) | — |
| Hình block | layer 0 trong định nghĩa (theo layer của block chèn); không có thuộc tính (nên gói 3D dùng được) | — |
| Gọi lại trạng thái layer | không cần thẻ (như `set_layer_props`), có Hoàn tác lượt; báo layer đã đổi; layer tạo sau khi lưu giữ nguyên | đòi thẻ khi gọi lại làm mở khóa / bật layer |
| Xóa trạng thái layer | qua thẻ (như ghi đè) | bỏ thẻ |
| Macro | lệnh ghi đè có `handles` chạy lại sẽ bị hỏi thẻ lại (cái "đồng ý" thuộc lượt đó, như `find_replace_text`); lệnh tạo mới chạy lại không tạo trùng | — |

## Số kỹ thuật bên trong (không cần duyệt)

- So style: số khớp trong 1e-6 (tương đối), chữ không phân biệt hoa thường. LTSCALE "còn mặc định" = |LTSCALE − 1| < 1e-9.
- Ô khung tên "có thể quá dài" khi số ký tự × cao chữ × 0,8 × 0,85 > bề rộng ô (chỉ cảnh báo `long_fields`).
- Giới hạn: `positions` ≤ `MaxArrayCopies` như `insert_block`; `handles` ≤ 50; tên trạng thái ≤ 255 ký tự; tỉ lệ 1:1–1:10000.

## Token lúc mở chat

Đo như `test-tong-the-nang-cap.md` / F1: lệnh của gói + 5 lệnh thẻ, tên + mô tả + schema JSON (`z.toJSONSchema`) + system
prompt theo gói, ~3,5 ký tự / token (script chạy lại cả bản `b2e6431`; số "trước" khớp bảng F1).

| Gói | Số lệnh trước → sau | Ký tự trước (`b2e6431`) | Ký tự sau | ≈ token trước → sau | Tăng |
| --- | --- | --- | --- | --- | --- |
| 2D | 100 → 104 | 136 357 | 141 765 | 39,0k → 40,5k | **+1,5k** (+4,0 %) |
| 3D | 88 → 90 | 140 194 | 142 622 | 40,1k → 40,7k | **+0,7k** (+1,7 %) |
| Full (Cả hai) | 131 → 135 | 202 969 | 208 297 | 58,0k → 59,5k | **+1,5k** (+2,6 %) |

Chế độ 2D / 3D của máy Full = gói 2D / 3D + ~220 ký tự (đoạn "Work mode", như F1). Bốn lệnh mới: 5,2k ký tự
(`setup_standards` 1,2k, `create_sheet` 1,5k, `insert_library_block` 1,5k, `layer_state` 0,9k); system prompt +240 ký tự.

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1021** (973 + 48 `NangCapF2Tests.cs`: nhãn gói / chế độ / UCS, tỉ
  lệ, tên style theo tiêu chuẩn và đơn vị, chạy lại không đổi, style khác được liệt kê, 5 khổ giấy (khung, khung tên,
  khung nhìn), tỉ lệ vừa, khung tên đủ ô trong ô, giá trị mặc định ô, 15 block vẽ đúng cỡ ở mặc định / nhỏ nhất / lớn
  nhất, tên tiếng Việt, tên block được `build_3d_from_plan` nhận, trạng thái layer, số nằm ở `F2Numbers`).
- Trợ lý `cd agent && npm ci && npm test`: **319** (311 + 8 `test/nangcapf2.test.ts`: gói / chế độ nạp đúng lệnh, thẻ
  cho ghi đè (kể cả trong batch, gõ "đồng ý" không tính), lệnh tạo / đọc không hỏi, kích thước schema).
- Build 3 target (Linux, `-p:EnableWindowsTargeting=true`, .NET SDK 10 từ kho Ubuntu, xóa obj / bin giữa các lần): 0
  lỗi, 0 cảnh báo.

## Chưa kiểm được (cần AutoCAD thật)

- Mọi lời gọi AutoCAD: `LayerStateManager` (lưu / gọi / xóa), tên font `TypeFace` đọc lại có đúng "Arial" (nếu khác,
  lần chạy lại sẽ báo `kept` — xem checklist A.2), khổ giấy DWG To PDF và xoay giấy, khung nhìn xoay theo UCS, `Layout.Initialize()` +
  bỏ khung nhìn mặc định AutoCAD tự thêm (để trang chỉ có 1 khung nhìn của mình; checklist B.1), căn chữ trong khung tên, block
  thư viện qua `TransformBy` của UCS. Bước `CLAUDECHATSELFTEST` F2 kiểm phần lớn những điểm này.

## CI

- Run **#41** (`45584939266`, `release=false`) trên `75b4232`: **xanh** (build 3 target, test lõi, trợ lý, UI test WPF).
- Commit sau CI chỉ thêm mục này (tài liệu).

## Đã kiểm trước khi báo

- Đọc lại diff so với `b2e6431`; sau lần sửa code cuối (bỏ khung nhìn mặc định của layout mới): build lại 3 target (xóa
  obj / bin), lõi 1021 xanh, trợ lý 319 xanh, rồi CI #41.
