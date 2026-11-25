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
| F2T-4 | Bản vẽ đơn vị **dm**, **inch**, **feet**: kiểu kích thước tên `VN-100-CM` (sai đơn vị trong tên) | nhỏ | `StandardsLogic.UnitSuffix`: `-DM`, `-IN`, `-FT`; mm / cm / m giữ tên cũ | `e65e799` |
| F2T-5 | Màu hiện hành của AutoCAD đang là đỏ (hoặc nét / độ dày hiện hành khác ByLayer) khi chèn block thư viện / tạo trang in: hình trong block (cửa, lavabo…), khung tên, khung bản vẽ, khung nhìn **mang luôn màu / nét đó**, không theo layer (SetDatabaseDefaults chép CECOLOR / CELTYPE / CELWEIGHT) | trung bình | sau mỗi SetDatabaseDefaults của 4 lệnh F2: màu, nét, độ dày = ByLayer; nét đậm khung / khung tên vẫn đặt riêng sau đó | xem dưới |
| F2T-6 | `layer_state` "liệt kê" (tiếng Việt) hoặc không có `action` (từ macro / batch) bị coi là lệnh **ghi**: bị chặn khi bật "Chỉ xem", tạo bản sao phiên bản và ghi vào macro dù chỉ đọc | nhỏ | phần "lệnh đọc" dùng chung hàm chuẩn hóa `LayerStateLogic.NormalizeAction` với lệnh | xem dưới |
| F2T-7 | So tên font: F2T-2 sửa thứ tự (tên font trước), còn trường hợp AutoCAD chỉ có tên file (`arial.ttf`, có thể kèm đường dẫn) | nhỏ | `StandardsLogic.FontKey`: bỏ đường dẫn và đuôi .ttf / .ttc / .otf, so không phân biệt hoa thường; font .shx giữ đuôi | xem dưới |

F2T-1…F2T-4: commit `e65e799` (CI #42). F2T-5…F2T-7 và tiền tố tên layout vào `F2Numbers`: commit sau (CI lượt 2, xem
cuối file).

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
| *(bổ sung)* Tên trang in mặc định **BV-01, BV-02…** | tên tab layout | trước nằm rời trong plugin; **đã dời** vào `F2Numbers.SheetNamePrefix` | tự đặt |
| *(bổ sung)* Kiểu kích thước: chữ trên đường kích thước (DIMTAD 1), số thập phân (DIMLUNIT 2), bỏ số 0 thừa cuối (DIMZIN 8) | 4000 chứ không 4000.00; chữ nằm trên đường | có sẵn từ trước ở `create_dim_style` (`StyleTools.cs`), `setup_standards` dùng lại — không ở `F2Numbers` | thông lệ |
| *(bổ sung)* 5 kiểu nét nạp sẵn CENTER, DASHED, HIDDEN, PHANTOM, DASHDOT | kiểu nét có sẵn trong bản vẽ | đủ, `F2Numbers.Linetypes` | acadiso.lin |
| *(bổ sung)* Layer khách tự đặt tên cho block thư viện (không phải 5 layer mặc định) mà chưa có: tạo màu **7** | màu layer mới | `insert_library_block` (số 7 nằm trong plugin) | tự đặt |
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
| F2T-5: màu / nét của hình mới | ByLayer (theo layer; hình trong block nằm layer 0 nên theo block chèn), như block thư viện thường làm | ByBlock nếu chủ dự án muốn |
| Điểm 1 của lượt rà kia (liệt kê thư viện = Wcs) | giữ cách sửa F2T-1 ở bộ điều phối (mọi lệnh vẽ mới gọi chỉ để đọc), không đổi `ModeOf` | — |
| Bàn giao F2 ghi "mọi số ở `F2Numbers`" | không dời số block vào `F2Numbers` (đụng nhiều, dễ sai); ghi rõ chỗ trong bảng trên | dời nếu chủ dự án muốn sửa số block thường xuyên |

## Đối chiếu 7 phát hiện của lượt rà khác (thread `session_01D56HmJDwuCnKRDvTAFHFXr`, luồng chính chuyển 25/11)

Kiểm từng điểm trên code nhánh này, không dùng bản vá của thread kia.

| # | Phát hiện | Đúng là lỗi? | Xử lý |
| --- | --- | --- | --- |
| 1 | Liệt kê thư viện block (`insert_library_block` không tên) trong UCS khác World báo lỗi "cannot place new objects…" | Đúng | Đã sửa ở F2T-1 (`e65e799`), cách khác: bộ điều phối cho mọi lệnh Create gọi chỉ để đọc chạy thẳng, thay vì đổi `ModeOf` sang Wcs. Test `A_create_tool_called_only_to_read_runs_in_any_ucs` |
| 2 | Hình trong block thư viện / khung tên / khung / khung nhìn lấy màu, nét, độ dày hiện hành | Đúng | Sửa mới F2T-5. Test `New_objects_take_color_linetype_and_lineweight_from_their_layer_not_the_current_ones` |
| 3 | Đọc font bằng tên file trước tên font → chạy lại báo nhầm khác font | Đúng | F2T-2 (`e65e799`, tên font trước) + F2T-7 (chuẩn hóa tên font). Test `Font_names_have_one_key`, `A_style_whose_font_is_only_a_file_name_is_the_same_font` |
| 4 | Bản vẽ inch / dm bị gắn `-CM` | Đúng (feet cũng vậy) | Đã sửa ở F2T-4 (`e65e799`). Test `Dim_style_names_say_the_drawing_unit`, `Every_insunits_gets_a_matching_name` |
| 5 | `create_sheet` không tỉ lệ + UCS xoay: chọn tỉ lệ theo khung WCS | Đúng | Đã sửa ở F2T-3 (`e65e799`). Test `The_sheet_sizes_the_drawing_in_a_turned_ucs` |
| 6 | `layer_state` "liệt kê" / thiếu action bị coi là lệnh ghi | Đúng (ảnh hưởng: Chỉ xem, bản sao phiên bản, macro) | Sửa mới F2T-6. Test `Layer_state_list_in_any_wording_only_reads` |
| 7 | Tiền tố "BV-" rời trong plugin; bảng con số thiếu DIMTAD / DIMLUNIT / DIMZIN, 5 kiểu nét, tên BV-01, màu 7; ghi chú nguồn | Đúng (tài liệu + 1 hằng) | Dời "BV-" vào `F2Numbers.SheetNamePrefix` (test `The_sheet_name_prefix_lives_in_F2Numbers`); thêm 4 dòng vào bảng con số; ghi chú nguồn: dãy chữ ISO 3098 và khung tên 180 mm = ISO 7200 đã ghi; lề 25 / 5 khác ISO 5457 ghi kèm "cần tra lại" vì tớ không chắc số 20 / 10. Không đổi số đề xuất nào |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1048** sau lượt sửa 2 (1035 ở `e65e799`; 1021 + 27 `NangCapF2TestSuaTests.cs`, lượt 2 thêm 13: tên font, màu / nét ByLayer, liệt kê trạng thái layer, tiền tố tên layout. Lượt 1: lệnh vẽ mới gọi chỉ để
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

## CI

- Lượt 1: run **#42** (`45588692165`, `release=false`) trên `e65e799` (F2T-1…F2T-4): **xanh**.
- Lượt 2: run **#43** (`45590233177`, `release=false`) trên `6e9f622` (thêm F2T-5…F2T-7, đối chiếu 7 điểm): **xanh**
  (build 3 target, lõi 1048, trợ lý 322, UI test WPF). Đây là bản code cuối; commit sau chỉ thêm mục này.

## Đã kiểm trước khi báo

- Đọc diff F2 so với `b2e6431`; đối chiếu từng điểm của lượt rà khác trên code nhánh này.
- Sau lần sửa code cuối: build lại 3 target (xóa obj / bin), lõi 1048 xanh, trợ lý 322 xanh, rồi CI #43.
