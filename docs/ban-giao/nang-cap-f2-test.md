# Bàn giao: Test & sửa đợt F2 — 2D dùng hằng ngày

Nhánh `full/nang-cap-f2-test`, từ `full/nang-cap-f2` @ `bae8262`. Người rà: thread "Nâng cấp đợt F1" (không viết phần
F2), luồng chính giao 25/11/2026. Rà lại diff so với `b2e6431` (20 file, ~2,4k dòng), không dựa vào lời bàn giao F2.

Chưa thử trên AutoCAD thật. Phần thử tay mới: mục **F** cuối `docs/checklist-dot-f2.md`.

## Lỗi tìm được

| # | Lỗi (khách thấy gì) | Mức | Sửa | Commit |
| --- | --- | --- | --- | --- |
| F2T-1 | Khi UCS khác World (xoay / dời hệ trục), nhờ "liệt kê thư viện block" thì **lỗi** "cannot place new objects in the UCS…": lệnh `insert_library_block` không tên chỉ đọc, nhưng bộ điều phối F1 coi mọi lệnh vẽ mới là ghi. Lỗi chéo F1 × F2 | trung bình | `ToolDispatcher.RunHandler`: lệnh kiểu Create gọi chỉ để đọc thì chạy thẳng (không có hình để đưa vào UCS) | xem cuối bảng |
| F2T-2 | Kiểu chữ trùng tên (vd `KT-ARIAL`) khách tạo bằng lệnh STYLE với font Arial: AutoCAD lưu cả tên file `arial.ttf` lẫn tên font `Arial`; F2 so tên file nên coi là **khác**, hỏi thẻ ghi đè không cần thiết và không đặt hiện hành | trung bình | so theo tên font (TypeFace) khi có, tên file khi không có (font .shx) | xem cuối bảng |
| F2T-3 | UCS xoay trên mặt bằng, `create_sheet` không nói tỉ lệ: khung nhìn xoay theo UCS (đúng) nhưng **tỉ lệ tự chọn và tâm nhìn tính theo khung bao WCS** → hình dài theo trục UCS bị chọn tỉ lệ quá nhỏ (hình nhỏ giữa trang) hoặc báo "không vừa" sai | trung bình | khung bao bản vẽ tính lại theo trục UCS (`UcsFrame.BoxFromWcs`) cho tỉ lệ vừa, cảnh báo và tâm mặc định | xem cuối bảng |
| F2T-4 | Bản vẽ đơn vị **dm**, **inch**, **feet**: kiểu kích thước tên `VN-100-CM` (sai đơn vị trong tên) | nhỏ | `StandardsLogic.UnitSuffix`: `-DM`, `-IN`, `-FT`; mm / cm / m giữ tên cũ | xem cuối bảng |

Không tìm thấy lỗi ở: chạy lại không tạo trùng, không ghi đè khi chưa qua thẻ (cả trong batch, gõ "đồng ý" không tính,
thẻ duyệt một phần handle không cho handle còn lại qua — thêm test), 5 khổ giấy / khung / khung tên / ô điền, tỉ lệ
khung nhìn theo đơn vị, xóa layout khi lỗi khổ giấy, hình 15 block tự vẽ (không chép block ngoài), tên block `CUA-DI` /
`CUA-SO` nhận được ở `build_3d_from_plan`, lưu / gọi / liệt kê / xóa trạng thái layer, nhãn gói (2D: style / trang in;
mọi gói: block thư viện, trạng thái layer), chế độ Full 2D / 3D (lời nhắc đổi chế độ, không mời mua gói), kiểu UCS của
4 lệnh, công thức xoay khung nhìn (tâm DCS = điểm chiếu lên trục UCS: đã kiểm lại tay), macro ghi trong UCS.

## Con số chờ chủ dự án duyệt — đã soát

Bảng của bàn giao F2 **đủ** các số khách gặp, trừ vài chi tiết hình block (thêm ở dòng cuối). Không đổi số nào. Chỗ đổi:
`F2Numbers` (`Core/Standards2D.cs`) cho style / trang in / layer; **kích thước và chi tiết từng block nằm ở mục của block
đó trong `LibraryBlocks.Items`** (cùng file, mỗi block một chỗ) — bàn giao F2 ghi "mọi số ở F2Numbers" là chưa chính xác.

| Số đề xuất | Khách thấy gì | Đủ / một chỗ dễ đổi | Tên tiêu chuẩn ghi kèm |
| --- | --- | --- | --- |
| Chữ kích thước 2,5 mm; chữ tiêu đề / tên phòng / ghi chú / chữ nhỏ 5 / 3,5 / 2,5 / 1,8 mm | cỡ chữ in ra | đủ, `F2Numbers` | F2 ghi "skill + thông lệ". Cả 5 số đều nằm trong dãy cỡ chữ chuẩn 1,8 – 2,5 – 3,5 – 5 – 7 của ISO 3098 (TCVN 7284) — có thể ghi thêm cho chắc |
| Đầu kích thước gạch chéo 1,2 mm; gióng cách vật 2, vượt 1,5; đường kích thước vượt gióng 1; chữ cách đường 0,8 mm | nét kích thước | đủ, `F2Numbers` | theo skill `ve-kien-truc`, đúng như ghi |
| Số lẻ 0 (mm) / 1 (cm) / 2 (dm, m) | 4000 / 4.00 | đủ, `StandardsLogic.Decimals` (công thức, không ở `F2Numbers`) | thông lệ |
| Font Arial, rộng 0,85 | chữ có dấu, hơi hẹp | đủ, `F2Numbers` | skill |
| LTSCALE = mẫu số tỉ lệ × 0,5, PSLTSCALE 0, chỉ đặt khi LTSCALE còn 1 | nét đứt vừa mắt | đủ, `F2Numbers.LtScaleFactor` | thông lệ (acadiso.lin) |
| Tỉ lệ mặc định 1:100; dãy tỉ lệ tự chọn 1:1 … 1:5000 | tỉ lệ khi không nói | đủ, `F2Numbers` | thông lệ |
| Khổ A0–A4 (841×1189 … 210×297); A3 ngang, A4 đứng mặc định | khổ in | đủ | ISO 216 — chắc chắn |
| Lề trái 25 mm, 3 cạnh 5 mm | khung cách mép giấy | đủ | thông lệ VN, đúng như ghi. *Lưu ý:* theo tớ nhớ ISO 5457 (TCVN 7285) ghi lề 20 mm bên trái / 10 mm các cạnh khác (cần tra lại bản gốc nếu muốn dẫn); 25 / 5 là khung hay gặp ở VN — **không** nên ghi là TCVN |
| Nét khung 0,5 mm, nét trong khung tên 0,25 mm | độ đậm nét in | đủ | thuộc dãy bề rộng nét ISO 128 (0,25 – 0,35 – 0,5 – 0,7) |
| Khung tên rộng 180 mm, dòng 8/8/8/12/8/8, cột nhãn 30, 2 dòng dưới 22/38 ×3; chữ nhãn 2 / giá trị 2,5 / tên bản vẽ 3,5 mm | khung tên | đủ, `F2Numbers` | 180 mm đúng bề rộng tối đa của khung tên theo ISO 7200 — có thể ghi thêm |
| 10 ô khung tên, ngày dd/MM/yyyy | ô điền | đủ, `SheetLogic.Fields` | — |
| Khung nhìn cách 5 mm, khóa sẵn; layer `KHUNG` (7), `KHUNG-VP` (8, không in) | trang in | đủ, `F2Numbers` | tự đặt |
| Kích thước mặc định + min–max của 15 block | cỡ ký hiệu | đủ, mỗi block ở mục của nó trong `LibraryBlocks.Items`. Bảng F2 chỉ ghi min–max của cửa / cửa sổ; các block khác có min–max trong code (vd bồn cầu rộng 300–600, sâu 500–900) | thông lệ |
| Chi tiết hình block (két nước 26 %, gối 350 cách 100, tay vịn sofa 150, tựa 200…) | hình ký hiệu | đủ trong bảng F2; **thiếu** vài số nhỏ: lòng bồn tắm cách mép 60, bo góc 150; bo góc lavabo / ghế / sofa 40; đầu giường 50 + 80, bo góc gối 60; khay tắm, bếp có số bo / vòng bếp trong code. Đều ở mục block trong `LibraryBlocks.Items` | tự vẽ |
| Layer block A-DOOR (4), A-WIND (5), A-FIXT (6), A-FURN (8), L-PLNT (3) | layer khi không nói | đủ, `F2Numbers` | skill (cửa) + thông lệ |
| Tên block `CUA-DI-900`, `CUA-SO-1200`… | tên block | đủ | theo skill |
| *(mới, F2T-4)* hậu tố đơn vị trong tên kiểu kích thước: không có (mm), `-CM`, `-DM`, `-IN`, `-FT`, `-M` | tên kiểu kích thước | `StandardsLogic.UnitSuffix` | — |

## Quyết định tự chọn

| Tình huống | Đã chọn (rẻ, an toàn, đổi lại được) | Muốn đổi thì |
| --- | --- | --- |
| F2T-1: sửa ở đâu | sửa ở bộ điều phối chung (1 dòng): mọi lệnh "vẽ mới" gọi chỉ để đọc đều chạy được trong UCS, không chỉ `insert_library_block` | — |
| F2T-2: kiểu chữ có cả tên file lẫn tên font | so tên font; font .shx (không có tên font) vẫn so tên file | — |
| F2T-3: UCS nghiêng (mặt đứng) | giữ như F2 (khung nhìn nhìn từ trên xuống theo WCS, tỉ lệ theo khung WCS) | — |
| F2T-4: tên kiểu kích thước | chỉ thêm hậu tố cho dm / inch / feet; mm / cm / m giữ nguyên tên (bản vẽ đã chạy `setup_standards` không bị tạo kiểu mới) | — |
| Bàn giao F2 ghi "mọi số ở `F2Numbers`" | không dời số block vào `F2Numbers` (đụng nhiều, dễ sai); ghi rõ chỗ trong bảng trên | dời nếu chủ dự án muốn sửa số block thường xuyên |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1035** (1021 + 14 `NangCapF2TestSuaTests.cs`: lệnh vẽ mới gọi chỉ để
  đọc chạy trong UCS, so kiểu chữ theo tên font, khung bao theo UCS xoay cho tỉ lệ vừa, tên đơn vị 6 loại + mọi
  INSUNITS, máy Full chế độ 3D với 2 lệnh 2D / 2 lệnh mọi gói ở mọi chế độ, macro block thư viện ghi trong UCS, bản vẽ
  mét, lời từ chối khi chưa qua thẻ). Test F1 `Dispatcher_runs_every_handler_through_the_ucs` đếm `def.Handler(ctx, p)`
  từ 3 thành 4 (thêm đúng dòng sửa F2T-1); không test nào bị bỏ / tắt.
- Trợ lý `cd agent && npm ci && npm test`: **322** (319 + 3 `test/nangcapf2test.test.ts`: thẻ duyệt một phần handle,
  `view_center` của `create_sheet` theo UCS, chế độ 3D không nhắc 2 lệnh 2D).
- Build 3 target (net48, net8.0-windows, net10.0-windows; xóa obj / bin giữa các lần; `-p:EnableWindowsTargeting=true`):
  0 lỗi.

## Chưa kiểm được

- Mọi thứ trên AutoCAD thật (như F2): `LayerStateManager`, tên font AutoCAD đọc lại, khổ giấy DWG To PDF, khung nhìn xoay
  theo UCS, `Layout.Initialize()`, block thư viện qua UCS. Thử tay: mục A–F của `docs/checklist-dot-f2.md`.
