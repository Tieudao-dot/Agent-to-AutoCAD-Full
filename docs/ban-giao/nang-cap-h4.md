# Bàn giao: nâng cấp đợt H4 — học chuẩn công ty từ bản vẽ mẫu (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người viết). Nhánh `full/nang-cap-h4`, tách từ `full/nang-cap-h3` @ `98eecf1` (gộp H1-test +
H2-test + Sửa CI, phần dùng chung đọc bản vẽ khác của H3). Thử tay: `docs/checklist-dot-h4.md`. Chưa chạy trên AutoCAD thật.

## 1. Khách thấy gì

- "Học chuẩn công ty từ file `D:\Mau\ABC.dwt`, đặt tên ABC" → Claude báo đã học bao nhiêu layer, kiểu chữ / kích thước / ghi chú mũi
  tên, khung tên (tên block + các ô), khổ giấy. File mẫu chỉ được đọc.
- Bộ chuẩn nằm ở `%LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty\<tên không dấu>\` (`standard.json` + `mau.dwg` là bản chép file mẫu):
  theo từng người dùng Windows, ngoài thư mục cài, không mất khi cài đè / nâng cấp. Nhiều bộ được; xem / xóa qua chat.
- "Áp chuẩn ABC" (`setup_standards company`): thêm layer / kiểu còn thiếu từ bản chép file mẫu; layer đã có mà khác chuẩn (màu,
  kiểu nét, độ dày, in / không in) thì hỏi thẻ rồi mới thay; đặt kiểu chữ / kích thước / ghi chú đang dùng như file mẫu.
- "Soát theo chuẩn ABC" (`check_drawing company`): layer lạ, chữ / kích thước / ghi chú mũi tên dùng kiểu ngoài bộ, chữ lệch cỡ so với
  các cỡ chữ của file mẫu.
- "Tạo tờ in theo chuẩn ABC" (`create_sheet company`): khổ giấy như layout của file mẫu, khung tên công ty chép từ file mẫu đặt đúng chỗ,
  khung nhìn đúng chỗ khung nhìn của mẫu; ô khung tên điền theo tên ô (`attributes`).
- File mẫu bị đổi / xóa sau khi học: vẫn dùng được nhờ bản chép. Bản chép mất: layer và kiểu chữ vẫn áp được (từ `standard.json`),
  khung tên / kiểu kích thước không còn, Claude nói rõ và nhờ học lại.

## 2. Code

| File | Việc |
| --- | --- |
| `Core/CompanyStandard.cs` (mới) | `H4Numbers`; mô hình `CompanyStandard`; `CompanyStandardLogic`: tên, thư mục, lưu / đọc / xóa, giới hạn số bộ, JSON, khổ giấy A0 … A4, cỡ chữ hay dùng, kiểm layer / kiểu / cỡ chữ, mã thẻ, khác biệt của layer |
| `Drawing/CompanyStandardTools.cs` (mới) | lệnh `company_standard` (learn / list / show / delete); đọc file mẫu qua `OtherDrawing.Open` của H3; chép file mẫu; `SetupCompanyStandard`; `CompanyTitleBlockDef` (chép block khung tên bằng `WblockCloneObjects`) |
| `Drawing/Standards2DTools.cs` | `setup_standards` có `company` thì gọi `SetupCompanyStandard`; `create_sheet` dùng khổ giấy / khung tên / khung nhìn của bộ chuẩn, `attributes` |
| `Drawing/DrawingCheckTools.cs` | `check_drawing company`: luật layer / kiểu / cỡ chữ theo bộ chuẩn; nhóm cần chuẩn không có `fix` |
| `Core/OtherDrawing.cs` (dùng chung với H3) | **một dòng**: `.dwt` đọc như `.dwg` |
| `Core/Plans.cs`, `DrawingService.cs`, `UI/ToolLabels.cs`, `SelfTest.cs` | nhóm 2D, đăng ký, nhãn "Chuẩn công ty", bước tự kiểm |
| `agent/src/tools/companyStandard.ts` (mới), `standards2d.ts`, `drawingCheck.ts`, `index.ts`, `confirmation.ts`, `systemPrompt.ts` | lệnh mới (hoãn nạp), tham số `company` / `attributes`, thẻ, một dòng prompt (chỉ gói 2D / Full) |

## 3. Con số chờ chủ dự án duyệt

Tất cả ở `H4Numbers` (`Core/CompanyStandard.cs`).

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Số bộ chuẩn công ty trên một máy | 20 | bộ thứ 21: Claude nhờ xóa bớt | đủ cho văn phòng làm với nhiều đối tác; danh sách gọn |
| Số layer học từ một file mẫu | 1 000 | nhiều hơn: học 1 000 layer đầu, báo rõ | file mẫu thật thường 50 – 300 layer |
| Số kiểu mỗi loại (chữ / kích thước / ghi chú) | 100 | như trên | |
| Số khung tên (và khổ giấy) ghi nhớ | 10 | như trên | thường 1 – 3 layout mẫu |
| Số cỡ chữ ghi nhớ | 12 (hay dùng nhất) | chữ lệch mọi cỡ này hơn 10 % (như G2) thì báo | đủ cho tiêu đề / tên phòng / ghi chú / chữ nhỏ ở vài tỉ lệ |
| Cỡ file mẫu lớn nhất được chép giữ lại | 50 MB | lớn hơn: vẫn học layer / kiểu, không giữ khung tên | file mẫu thật thường dưới 5 MB |
| Tên bộ chuẩn dài nhất | 60 ký tự | | |
| Sai số khi nhận khổ giấy A0 … A4 | 2 mm | layout cỡ khác: dùng A3 và nói rõ | khổ giấy của máy in hay lệch vài phần mm |

Số kỹ thuật (khách không thấy): đọc tối đa `G2Numbers.MaxScan` đối tượng model space để đếm cỡ chữ.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Một lệnh hay nhiều | **một lệnh** `company_standard`, 4 thao tác, hoãn nạp; dùng ở 3 lệnh cũ qua `company` | — |
| Gói | 2D / Full (như `setup_standards`) | thêm vào nhóm Common |
| Giữ khung tên khi file mẫu mất | **chép file mẫu** vào thư mục bộ chuẩn (`mau.dwg`; DXF thì lưu thành DWG) | chỉ giữ JSON (mất khung tên khi file mẫu mất) |
| Cách áp layer / kiểu | **chép từ bản chép file mẫu** (`WblockCloneObjects`, giữ đủ mọi thuộc tính); mất bản chép thì dựng layer / kiểu chữ từ JSON | dựng hết từ JSON |
| Layer đã có mà khác chuẩn | thẻ Đồng ý rồi mới thay | — |
| Kiểu chữ / kích thước / ghi chú trùng tên đã có | **giữ nguyên, không so** (rẻ); chỉ thêm kiểu còn thiếu | so từng thuộc tính như bộ chuẩn VN |
| `fix_drawing` với bộ chuẩn công ty | không đề nghị sửa tự động các nhóm theo chuẩn (fix của G2 áp kiểu VN) | thêm `company` cho `fix_drawing` |
| Khung tên công ty | thay cả khung viền + khung tên VN (khung tên công ty thường có khung viền); khung nhìn lấy theo mẫu khi cùng khổ | vẽ thêm khung viền VN |
| Khổ giấy lạ (không phải A0 … A4) | dùng A3, nói rõ | thêm khổ tùy ý vào `create_sheet` |
| Cỡ chữ chuẩn | các cỡ hay dùng trong model space của mẫu + cỡ cố định của kiểu chữ, đổi theo đơn vị bản vẽ | nhập cỡ chữ tay |
| Quan hệ với skill (bảng layer theo ngành) | skill giữ nguyên; khách nói chuẩn công ty thì dùng `company`; prompt thêm 1 dòng | gộp bảng layer skill vào bộ chuẩn |
| Layer của xref, kiểu kích thước con (`ISO-25$0`) | không học | — |
| "Chỉ xem", macro | được chạy khi Chỉ xem (không đổi bản vẽ); không ghi vào macro | — |
| `.dwt` | thêm vào phần đọc chung của H3 (một dòng) | — |

## 5. Token lúc mở chat

| Gói / chế độ | Nạp gọn trước → sau | Tắt nạp gọn trước → sau |
| --- | --- | --- |
| 2D | 13,8k → 13,8k (+192 ký tự) | 46,1k → 46,7k |
| 3D | 19,1k → 19,1k | 45,8k → 45,8k |
| Full · Cả hai | 21,0k → 21,1k | 67,4k → 68,0k |

Đo bằng số ký tự (khoảng 3,5 ký tự / token). Mô tả lệnh mới chỉ nạp khi Claude cần.

## 6. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1530 / 1530** (thêm 10 ở `NangCapH4Tests.cs`).
- Trợ lý `cd agent && npm test`: **422 / 422** (thêm 3 ở `agent/test/nangcaph4.test.ts`).
- Build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa các lần): 0 lỗi; 1 cảnh báo CA2022 ở `TraceTools.cs`
  (có sẵn ở `98eecf1`, không phải H4).
- SelfTest: bước "Nâng cấp đợt H4: học chuẩn công ty…" (học từ bản chép bản vẽ test vào thư mục test, xem, xóa cần thẻ, file mẫu không đổi).
- CI: ghi ở mục 8.

## 7. Chưa kiểm được

- AutoCAD thật: `WblockCloneObjects` từ file mẫu (layer, kiểu, block khung tên có thuộc tính), vị trí khung tên / khung nhìn trên tờ,
  `PlotPaperSize` của layout mẫu, layout mẫu chưa mở bao giờ, file mẫu `.dwt`.
- Bộ chuẩn có hàng trăm layer: thời gian học / áp.

## 8. CI
