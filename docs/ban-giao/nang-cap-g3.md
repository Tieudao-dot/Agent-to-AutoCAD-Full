# Bàn giao nâng cấp đợt G3: bảng dự toán

*Thread Nâng cấp đợt G3 (người viết, `session_01D56HmFcYrzsme7GoBm8tNC`), 25/11/2026. Nhánh `full/nang-cap-g3`: tách từ
`full/nang-cap-g1-test` @ `9c7c95f`, gộp `full/nang-cap-g2-test` @ `f49e717` (commit gộp `395c313`, CI 45643804481), rồi
viết G3. Chạy cùng lúc với G4 (render, nhánh `full/nang-cap-g4`). Thử tay: `docs/checklist-dot-g3.md`. CI: cuối file.*

## 1. Khách thấy gì

- "Lập dự toán" / "tính tiền theo khối lượng" → Claude gọi **một** lệnh `cost_estimate` (nhãn "Lập dự toán"):
  - khối lượng lấy từ bảng có sẵn: `quantities` = `volume` (m³ theo layer của khối 3D, đợt D, net mặc định — phần giao tính
    một lần), `blocks` (số block, đợt C), `areas` (diện tích phòng / layer, đợt C); bộ lọc của bảng nguồn trong
    `quantities_params`;
  - **đơn giá chỉ lấy từ khách** (`prices`): Claude không tự đặt, không tra giá tham khảo. Dòng không có giá **để trống,
    không tính vào tổng**, kết quả liệt kê ở `missing_prices` và Claude kể tên, hỏi khách;
  - thành tiền = khối lượng × đơn giá, làm tròn đến đồng; Cộng; dòng phần trăm (thuế VAT, chi phí khác…) **chỉ khi khách
    nói** (`extra`); Tổng cộng.
- `excel`: file `<tên bản vẽ>-du-toan.xlsx` cạnh bản vẽ (hoặc tên khách cho), tab "Dự toán": cột Thành tiền và các dòng tổng
  là **công thức** (`=IF(E2="","",ROUND(D2*E2,0))`, `=SUM(...)`, phần trăm, tổng cộng), số tiền có dấu tách hàng nghìn
  (`#,##0`), Excel tính lại khi mở; khách gõ / sửa đơn giá trong Excel là tiền tự nhảy. File đã có: thẻ ghi đè của F5.
- `table_position`: vẽ bảng "BẢNG DỰ TOÁN" trong bản vẽ (số kiểu Việt Nam 1.234.567; dòng thiếu giá để trống).
- Ghép đơn giá với dòng: theo tên, **không phân biệt dấu, hoa thường, gạch nối / khoảng trắng** ("Tường gạch 220" =
  "tuong-gach 220"); hoặc mẫu có `*` ("A-WALL*", "CUA-DI-*"). Hai giá khác nhau cùng khớp một dòng: dòng đó **không có
  giá** (ghi chú), không chọn hộ. Không ghép "gần giống". Giá khách cho mà không khớp dòng nào: `unused_prices`. Giá ghi
  đơn vị khác đơn vị dòng (vd m² cho dòng m³): vẫn tính nhưng ghi chú "check".

## 2. Cách làm (kỹ thuật)

- Lõi `plugin/ClaudeCAD.Plugin/Core/Estimate.cs`: `G3Numbers` (mọi số / chữ khách gặp), `EstimateLogic` (ghép giá, tính tiền,
  làm tròn, dòng phần trăm, bảng vẽ, các dòng Excel có công thức, đọc dòng của bảng nguồn), `XlsxCell` (ô có công thức /
  kiểu số tiền). Có test, không cần AutoCAD.
- `Core/Xlsx.cs` (bộ ghi .xlsx của F4): thêm ô công thức (`<f>` + giá trị tính sẵn cho trình xem không tự tính), kiểu số
  `#,##0` (kiểu 2, 3 = đậm), `calcPr fullCalcOnLoad` (Excel tính lại khi mở). File của các bảng cũ không có công thức, như
  trước.
- Plugin `Drawing/EstimateTools.cs`: chạy bảng nguồn (`VolumeSchedule` / `BlockSchedule` / `AreaSchedule`) trong cùng giao
  dịch, không vẽ bảng / không ghi file của bảng nguồn; rồi tính, vẽ bảng (`InsertSchedule` của đợt C), ghi Excel.
- **Gói:** lệnh nhóm Common, nhưng bên trong kiểm **gói của bảng nguồn** (`volume_schedule` = 3D, `block_schedule` /
  `area_schedule` = 2D) như dispatcher, chế độ F1 thì bảo đổi chế độ. Bảng vẽ trong bản vẽ là chữ → `table_position` thuộc
  2D (`PlanLogic.TextParams`), máy 3D chỉ có số + Excel (như luật đợt D); trợ lý ẩn `table_position` khỏi schema ở gói 3D.
- **Chỉ xem:** không vẽ bảng thì là lệnh đọc (tính và xuất Excel được); vẽ bảng bị chặn như mọi lệnh ghi.
- **Macro:** chạy lại vẽ lại bảng, không ghi file Excel (như các bảng F4). Tên tham số `quantities` (không phải `source`:
  macro coi `source` là handle và sẽ đổi giá trị khi chạy lại — test `CrossPlanTests` bắt được).
- **Thẻ:** thêm `cost_estimate` vào `F5_CARD_TOOLS` (chỉ mã trên thẻ khách đã bấm Đồng ý mới tới plugin).
- Trợ lý: `agent/src/tools/estimate.ts` (nạp khi cần, ngoài nhóm nạp sẵn), mô tả lệnh và một dòng system prompt nói rõ
  "không tự đặt / tra giá"; nhãn, skill `ve-3d-kien-truc` / `ve-kien-truc`, bước tự kiểm "Nâng cấp đợt G3", `bo-cai/README.md`.
- File dùng chung (để gộp với G4 gọn): `Plans.cs` (2 dòng: nhóm + TextParams), `ToolLabels.cs`, `DrawingService.cs`,
  `tools/index.ts` (import, đăng ký, 1 dòng ẩn tham số), `confirmation.ts`, `systemPrompt.ts`, `Xlsx.cs`, csproj test,
  `SelfTest.cs` (một bước mới ở cuối).

## 3. Con số chờ chủ dự án duyệt

| Số / chữ | Ở đâu (để đổi) | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Đơn vị tiền mặc định **"đồng"** (khách đổi được, vd USD) | `G3Numbers.Currency` | tiêu đề cột "Đơn giá (đồng)", "Thành tiền (đồng)" | khách Việt Nam |
| Làm tròn thành tiền **đến đồng** (0 số lẻ, nửa làm tròn lên) | `G3Numbers.MoneyDecimals` | số tiền | tiền đồng không có số lẻ |
| Định dạng tiền Excel **`#,##0`** | `G3Numbers.ExcelMoneyFormat` | dấu tách hàng nghìn (Excel hiện dấu chấm hay phẩy theo máy) | yêu cầu "có dấu tách hàng nghìn" |
| Bảng trong bản vẽ: **1.234.567**, số lẻ dấu phẩy **12,5** | `G3Numbers.ThousandsSeparator`, `DecimalSeparator` | số trong bảng vẽ | cách viết số Việt Nam, như các bảng đợt C / D |
| Khối lượng giữ **3** số lẻ | `G3Numbers.QuantityDecimals` | cột Khối lượng | như bảng khối lượng đợt D (mặc định 3) |
| Đơn vị dòng: **m³** (khối), **m²** (diện tích), **cái** (block) | `G3Numbers.Unit*` | cột Đơn vị | đơn vị của bảng nguồn |
| Cột: STT, Hạng mục, Đơn vị, Khối lượng, Đơn giá, Thành tiền; dòng "Cộng", "Tổng cộng"; tiêu đề "BẢNG DỰ TOÁN"; tab "Dự toán" | `EstimateLogic.Header`, `G3Numbers` | bảng, file | mẫu bảng dự toán quen dùng |
| Tên file **`<tên bản vẽ>-du-toan.xlsx`** | `G3Numbers.FileSuffix` | tên file | không dấu, như các file Excel F4 |
| Tối đa **5** dòng phần trăm, **500** đơn giá một lần | `G3Numbers.MaxPercentLines`, `MaxPrices` | lệnh từ chối khi quá | chặn trường hợp lỗi |
| Khối lượng thể tích mặc định **net** (phần giao tính một lần) | `quantity` của lệnh | số m³ | đúng khối lượng thi công; `gross` khi khách muốn |

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Lệnh mới hay thêm tham số vào bảng cũ | **Một lệnh mới** `cost_estimate` gọi lại bảng cũ (3 bảng cũ giữ nguyên, không thêm tham số giá vào từng bảng; một chỗ tính tiền) | — |
| Lưu bảng đơn giá để dùng lại | **Không lưu** trong plugin; khách đưa giá mỗi lần (Claude dùng lại trong cùng cuộc chat); file Excel là nơi khách giữ / sửa giá | lưu vào "Ghi chú dự án" hoặc đọc giá từ file Excel: việc thêm |
| Thuế / chi phí khác | **Không tự thêm**; chỉ khi khách nói phần trăm (`extra`) | — |
| Làm tròn | thành tiền từng dòng làm tròn đến đồng rồi mới cộng (Excel cùng công thức); dòng phần trăm làm tròn đến đồng | làm tròn tổng thôi: đổi công thức |
| Ghép tên lệch nhau | bỏ dấu / hoa thường / gạch nối, hoặc mẫu `*`; **không đoán "gần giống"**; nhiều giá cùng khớp → để trống, hỏi | thêm bảng tên tương đương |
| Dòng thiếu giá | để trống, không tính vào tổng, liệt kê; Excel để ô trống để khách gõ | — |
| Giá 0 khách đưa | là một giá (tính 0), không coi là thiếu | — |
| Đơn vị giá khác đơn vị dòng | vẫn tính, ghi chú "check" | từ chối dòng đó |
| Khối lượng nhân giá | khối lượng làm tròn 3 số lẻ như hiện ra (khớp Excel) | — |
| Chỉ xem | tính + xuất Excel được (file ngoài bản vẽ, như các bảng F4); vẽ bảng bị chặn | — |
| Macro | không ghi lại file Excel (như F4) | — |

## 5. Token lúc mở chat (ước tính, cùng cách đo đợt F / G1)

| Gói / chế độ | Trước G3, nạp gọn | Sau G3, nạp gọn | Trước G3, tắt nạp gọn | Sau G3, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 13,6k | 13,6k | 43,4k | 44,3k |
| Gói 3D | 18,9k | 18,9k | 43,7k | 44,5k |
| Full · Cả hai | 20,8k | 20,9k | 64,3k | 65,2k |
| Full · chế độ 2D | 13,6k | 13,7k | 43,5k | 44,4k |
| Full · chế độ 3D | 18,9k | 19,0k | 43,8k | 44,6k |

"Trước G3" = nhánh sau khi gộp G1 + G2 (`395c313`). Nạp gọn (mặc định): lệnh mới chỉ thêm tên + một dòng prompt (~0,06k);
lúc khách nhờ dự toán Claude nạp mô tả lệnh (~0,8k) một lần.

## 6. Test

- Lõi `plugin/ClaudeCAD.Core.Tests/NangCapG3Tests.cs` (7 test): dòng thiếu giá để trống / liệt kê / không vào tổng (giá 0
  khách đưa vẫn là giá); ghép tên (dấu, hoa thường, gạch nối, mẫu `*`, hai mẫu → để trống, không ghép gần giống, giá thừa,
  đơn vị khác); làm tròn + dòng phần trăm + lỗi đầu vào; bảng vẽ số Việt Nam; file Excel (công thức, giá trị tính sẵn,
  kiểu `#,##0`, `fullCalcOnLoad`, XML đúng chuẩn, file bảng cũ không có công thức); dòng từ 3 bảng nguồn + gói của từng
  nguồn + `table_position` là 2D; mã nguồn plugin (kiểm gói nguồn, thẻ, macro, Chỉ xem).
- Trợ lý `agent/test/nangcapg3.test.ts` (3 test): nạp khi cần, mô tả + prompt nói "không tự đặt giá", gói 3D không có
  `table_position`, thẻ chỉ nhận mã đã Đồng ý.
- Sau lần sửa cuối: lõi 1282 / 1282, trợ lý 379 / 379; build 3 bản 0 lỗi 0 cảnh báo.

## 7. Chưa kiểm được (cần AutoCAD / Excel thật)

- Bảng vẽ trong AutoCAD; file mở bằng Excel thật (công thức tính lại, dấu tách hàng nghìn theo máy), LibreOffice / Google
  Sheets; thẻ ghi đè; macro; bước tự kiểm G3 (không chạy trên CI).
- Claude thật có giữ đúng luật "không tự đặt giá" khi khách ép.

## 8. CI

(điền sau khi chạy)
