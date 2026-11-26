# Bàn giao: Test & sửa đợt H6 — hạ tầng (`terrain`) (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà). Nhánh `full/nang-cap-h6-test`, tách từ `full/nang-cap-h6` @ `71f861d` (code
`6d71e40`). Không đụng nhánh H7. Chưa thử trên AutoCAD thật.

## 1. Đã soát

| Điểm | Kết quả |
| --- | --- |
| Đọc file CSV / TXT / .xlsx: dấu tiếng Việt, dấu phẩy thập phân, cột lẫn thứ tự, dòng trống / hỏng, file lớn | Tiêu đề Việt / Anh, phẩy / chấm phẩy / tab / khoảng trắng, ngoặc kép, phẩy thập phân khi không dùng phẩy làm dấu tách, dòng hỏng ghi lý do, 20 MB / 10 000 điểm: đúng. **Sửa H6T-1, H6T-2** |
| VN-2000, đổi trục, m → đơn vị, tọa độ rất lớn, UCS xoay | X Bắc → y, Y Đông → x đúng. Tọa độ thật: hỏi gốc ("real" / "auto" / [X, Y]). Lưới tam giác dời về tâm trước khi tính (không mất chính xác ở 10⁹ mm). Lệnh giữ WCS (`Ucs.cs`). **Sửa H6T-3** (đơn vị bản vẽ) |
| Đồng mức | Bowyer–Watson đúng với điểm trùng (bỏ < 1 mm), thẳng hàng (báo lỗi rõ), ít điểm (< 3: báo). Đường cái mỗi 5 (kể cả mức âm). Ghi số đọc xuôi. Vùng lõm ra đường kín. Không làm trơn (quyết định H6) |
| Lý trình, trắc dọc | Km / H1…H9 / C… / Đ… / Đầu / Cuối đúng. MSS mét chẵn, thấp hơn ≥ 2 m dưới cả tự nhiên **và thiết kế** (có test). Tỉ lệ 1:1000 / 1:100, số lẻ 3 / 2 / 2 / 2, độ dốc % đúng. **Sửa H6T-5** (tên cọc) |
| Trắc ngang | 15 m mỗi bên khi không nói; ngoài vùng có điểm thì bỏ (không nội suy ra ngoài); trái / phải theo chiều lý trình đúng |
| Chéo với phần cũ | Gói 2D / Full, hoãn nạp, "Chỉ xem" từ chối (lệnh ghi), U hoàn tác cả lượt, không thẻ (không ghi file). **Sửa H6T-4** (`check_drawing` báo nhầm layer `C-…`). Mô tả lệnh, skill và `H6Numbers` khớp; hai chỗ "chưa chắc" có câu "kỹ sư đối chiếu TCVN" (test) |
| Số tiêu chuẩn đã duyệt 26/11 | Bảng H6 của `docs/dot-h-so-tieu-chuan.md` khớp `H6Numbers` (test `The_numbers_approved_26_11_are_in_the_code`) |

## 2. Lỗi đã sửa

| Mã | Mức | Lỗi | Khách thấy gì trước khi sửa | Sửa | Test |
| --- | --- | --- | --- | --- | --- |
| H6T-3 | **Nặng** | Mọi kích thước dùng `UnitsPerMetre = 1000`, tức coi bản vẽ là mm, không đọc `INSUNITS` | Bản vẽ hạ tầng đặt đơn vị **mét** (rất hay gặp): điểm, đồng mức, trắc to gấp 1000 lần, chữ cao 1 250 m | Mỗi lần gọi `terrain`, `H6Numbers.ForDrawing(mm mỗi đơn vị)` đặt `UnitsPerMetre` theo đơn vị bản vẽ (m → 1, cm → 100, inch…), trả lại 1000 sau lần gọi. Mọi cỡ trên giấy (chữ, dòng bảng, cột nhãn, vạch, khoảng cách khung) qua `H6Numbers.Paper(mm, tỉ lệ)` | `A_drawing_in_metres_gets_metres_…` |
| H6T-1 | Vừa | (a) Trên .NET 8 / 10 (AutoCAD 2025 trở lên), mã Windows-1258 chưa được đăng ký nên lời gọi lỗi, rơi về Latin. (b) File "Unicode Text" của Excel (UTF-16) bị đọc như 1258 | File CSV tiếng Việt cũ: chữ có dấu thành rác, tiêu đề "Cao độ" không nhận, cột đọc theo vị trí có thể sai. File .txt Unicode của Excel: không đọc được điểm nào | Đăng ký `CodePagesEncodingProvider` trên .NET Core; nhận BOM UTF-16 LE / BE trước UTF-8 | `A_windows_1258_file_…`, `An_excel_unicode_text_file_utf16_…` |
| H6T-2 | Vừa | Không có dòng tiêu đề, 4 cột "X, Y, Z, mã" bị đọc là "tên, X, Y, Z" | X thành tên điểm, Y thành X, Z thành Y: điểm sai hết, không báo | 4 cột mà cột 1 là số, cột 4 là chữ → X, Y, Z, mã. ≥ 4 cột số: cột 1 là số thứ tự (tăng 1 đơn vị mỗi dòng) → tên; không thì là X | `X_Y_Z_and_a_code_without_a_header_…` |
| H6T-4 | Vừa | `check_drawing` (chuẩn VN) chỉ coi layer `A-…` là chuẩn | Bản vẽ đã "thiết lập style chuẩn" rồi dùng `terrain`: mọi điểm / đồng mức / trắc bị báo "layer ngoài chuẩn" | `IsStandardLayer` nhận thêm `C-…` (layer của skill `ve-ha-tang`) | `The_civil_layers_of_terrain_are_standard_…` |
| H6T-5 | Nhẹ | Lý trình mặt bằng (50 m) đặt tên vạch 50 m là `C1, C2…`, trùng tên với cọc chi tiết 20 m của trắc dọc / trắc ngang | Kỹ sư thấy "C1" trên mặt bằng ở 50 m, "C1" trên trắc ở 20 m | Vạch chi tiết trên mặt bằng chỉ ghi lý trình khi bước khác 20 m. Cọc H / Km / đỉnh / đầu / cuối giữ tên; vạch trùng đỉnh giữ "Đ1" | `A_plan_tick_at_50_m_never_takes_…` |

## 3. Nhận xét, không sửa

- **Đồng mức không làm trơn, không vạch chỉ hướng dốc ở vùng lõm:** đúng quyết định H6. Ghi để chủ dự án xem khi thử.
- **Cao độ tự nhiên ở cọc lấy thẳng giữa hai cọc:** đúng quyết định H6. Địa hình gãy giữa hai cọc 20 m không hiện trên trắc dọc.
- **Chữ của `terrain` theo tỉ lệ mặt bằng (mặc định 1:500):** nếu bản vẽ đã thiết lập chuẩn ở tỉ lệ khác, `check_drawing` có thể báo "chữ lệch cỡ". Đó là khác tỉ lệ thật, không phải lỗi.
- **CSV phân cách bằng dấu phẩy mà số có phẩy thập phân trong ngoặc kép** (`"1234,5"`): không đọc. Excel tiếng Việt lưu CSV bằng dấu chấm phẩy nên ít gặp.
- **.xlsx:** lấy file `sheet1.xml` số nhỏ nhất làm sheet đầu. Thường đúng; sổ có sheet bị sắp lại thì có thể lấy nhầm.
- **Layer MEP của H7** (`M-`, `E-`, `P-`…) cũng sẽ bị `check_drawing` báo như H6T-4. Không thuộc H6. Ghi để thread rà H7 / gộp H8 xem.

## 4. Con số chờ chủ dự án duyệt

Không có số mới khách thấy. Số kỹ thuật: số thứ tự "tăng 1 mỗi dòng" ở ≥ 80 % số dòng (H6T-2).

## 5. Quyết định tự chọn

- Đơn vị bản vẽ đọc từ `INSUNITS`. Bản vẽ không đặt đơn vị coi là mm, như các lệnh khác.
- Vạch 50 m trên mặt bằng chỉ ghi lý trình, không đặt tên `C…`, để một tên chỉ một chỗ.
- `C-…` là layer chuẩn cho `check_drawing`. Mọi layer bắt đầu bằng `C-` đều tính là chuẩn, như `A-…`.

## 6. File

- Sửa:
  - `Core/Terrain.cs`: `ForDrawing`, `Paper`, đọc mã, cột, `LooksLikeRowNumbers`, `PlanLabel`;
  - `Drawing/TerrainTools.cs`: đơn vị theo bản vẽ, nhãn vạch;
  - `Core/DrawingCheck.cs`: `IsStandardLayer`;
  - `Core.Tests/NangCapH6Tests.cs`: cùng nhóm test không chạy song song;
  - `docs/checklist-dot-h6.md`: thêm mục E.
- Mới:
  - `Core.Tests/NangCapH6TestSuaTests.cs` (8 test);
  - bàn giao này.

## 7. Test

- Lõi **1635 / 1635** (thêm 8 test).
- Trợ lý **447 / 447**.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: qua.
- CI: xanh (mục 8).

## 8. CI

Run **45824175827** trên `c9f2c6b`: xanh. Lõi 1635 / 1635, trợ lý `# tests 447`, `# pass 447`, `# fail 0`, UI test WPF 38 / 38. 1 lượt CI.
