# Bàn giao: nâng cấp đợt H5 — làm nhiều file một lúc (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người viết, kiêm gộp). Nhánh `full/nang-cap-h5`:

- **gộp** `5184962` = `full/nang-cap-h4-test` @ `94314f6` + `full/nang-cap-h3-test` @ `12e3848`. Chỗ chồng ở SelfTest, `index.ts`,
  README, csproj test, `DrawingService.cs`: giữ cả hai bên. CI #85 (45792384874) xanh, `# pass 429 / # fail 0`;
- **H5** viết tiếp trên đó.

Thử tay: `docs/checklist-dot-h5.md`. Chưa chạy trên AutoCAD thật.

## 1. Khách thấy gì

- "Soát / áp chuẩn / in PDF / đếm block cho cả thư mục này" (có thể gồm thư mục con, hoặc một danh sách file) → Claude kể số file,
  các file sẽ bỏ qua và lý do, rồi **hỏi thẻ** trước khi bắt đầu.
- Đồng ý → khung chat hiện "file i / N"; cuối cùng mỗi file một dòng: **xong / lỗi / bỏ qua** và lý do. Một file lỗi không làm
  dừng cả lượt. Bấm **Dừng**: ngừng sau file đang làm, không để file nào ghi dở.
- **Soát** (`check_drawing`, kể cả theo bộ chuẩn công ty H4) và **đếm block**: đọc file qua phần đọc chung của H3 (không khóa, không
  sửa).
- **Áp chuẩn** (`setup_standards`, kể cả `company`):
  - chép bản gốc vào `<thư mục>\_sao-luu-claudecad\<ngày giờ>\…` (giữ cây thư mục con);
  - đọc file vào bộ nhớ, áp chuẩn, ghi ra file tạm cùng thư mục, rồi thay file gốc (`File.Replace`). Cùng đời DWG như trước.
  - Bỏ qua: file chỉ đọc, file có `.dwl` (đang mở ở máy / AutoCAD khác), file đang mở trong AutoCAD này.
  - Kiểu khác chuẩn trong từng file **giữ nguyên** (không hỏi thẻ cho từng file), ghi trong dòng của file đó.
- **In PDF**: mỗi file mở chỉ đọc trong AutoCAD, in mọi layout vào `<tên file>.pdf` cạnh file, đóng lại, trở về bản vẽ của khách.
  File đang mở ở đây thì in luôn ở tab đó (bỏ qua nếu còn thay đổi chưa lưu). PDF có sẵn: thẻ của lượt nói rõ số file bị thay.
- `excel: true`: `tong-hop-<việc>-<ngày giờ>.xlsx` trong thư mục (đếm block: thêm `tong-hop-block-<ngày giờ>.xlsx`); không bao giờ ghi
  đè file có sẵn (tên có ngày giờ).
- Gói 2D / Full (cả bốn việc là lệnh 2D); từng việc còn được kiểm theo gói / chế độ như lệnh con. "Chỉ xem": soát, đếm, in PDF được,
  áp chuẩn bị từ chối.

## 2. Code

| File | Việc |
| --- | --- |
| `Core/BatchFiles.cs` (mới) | `H5Numbers`; `BatchLogic`: việc → lệnh con, lấy danh sách file (thư mục con, bỏ thư mục sao lưu, giới hạn), lý do bỏ qua, mã thẻ, sao lưu, tên file tổng hợp, bảng tổng hợp |
| `Drawing/BatchFilesTools.cs` (mới) | lệnh `batch_files`: plan / run (một file) / finish; bốn việc; ghi file qua sao lưu + file tạm + thay |
| `Drawing/ToolContext.cs` | thêm hàm dựng cho database khác (file mở bên cạnh) |
| `Drawing/ToolDispatcher.cs` | thêm `ViewOnly`, `RequireToolInPlan` (2 dòng, không đổi luồng cũ) |
| `Core/Plans.cs`, `DrawingService.cs`, `UI/ToolLabels.cs`, `SelfTest.cs` | nhóm 2D, đăng ký, nhãn "Làm nhiều file", bước tự kiểm |
| `agent/src/tools/batchFiles.ts` (mới) | lặp từng file, tiến độ, Dừng, tổng hợp; `index.ts`, `confirmation.ts` (thẻ), `systemPrompt.ts` (1 dòng ở phần 2D) |

## 3. Con số chờ chủ dự án duyệt

Tất cả ở `H5Numbers` (`Core/BatchFiles.cs`).

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Số bản vẽ mỗi lượt | 200 | nhiều hơn: làm 200 file đầu (theo tên), Claude nói số còn lại, nhờ chia thư mục | một dự án lớn thường vài chục – 150 bản vẽ; một lượt 200 file đã mất nhiều phút |
| Độ sâu thư mục con | 5 cấp | sâu hơn không đọc | thư mục dự án thường 2 – 3 cấp |
| Số tên file kể trong thẻ / kết quả | 30 | phần còn lại chỉ đếm | gọn trong khung chat |
| Số nhóm lỗi ghi cho mỗi file khi soát | 3 | | |
| Cỡ file lớn nhất | 200 MB (như H3, `OtherDrawingNumbers.MaxFileMb`) | lớn hơn: bỏ qua | |
| Tên thư mục sao lưu | `_sao-luu-claudecad` | | dấu `_` để nằm đầu danh sách, dễ thấy |
| *(Sửa KTH-02)* Thời gian chờ một file khi soát / đếm block | 2 phút (như các lệnh chậm) | quá hạn: file đó báo lỗi hết giờ, lượt chạy tiếp | `agent/src/ipc/rpc.ts`, `batchTimeout` |
| *(Sửa KTH-02)* Thời gian chờ một file khi áp chuẩn / in PDF | 5 phút (trần chung của mọi lệnh) | như trên | mở + ghi / in một bản vẽ lớn |

Không đặt giới hạn thời gian mỗi file: mỗi file là một lần gọi riêng nên giới hạn thời gian có sẵn của một lần gọi vẫn áp dụng.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Một lệnh hay nhiều | **một lệnh** `batch_files` với 4 việc, hoãn nạp | — |
| Chạy cả lượt trong một lần gọi | **mỗi lần gọi một file**, trợ lý lặp (không giữ nhiều file trong bộ nhớ, AutoCAD rảnh giữa hai file, Dừng được giữa hai file) | một lần gọi cho cả lượt |
| Nơi sao lưu | `<thư mục>\_sao-luu-claudecad\<ngày giờ>\` giữ cây thư mục con; danh sách file rời: cạnh từng file | thư mục sao lưu chung trong `%LOCALAPPDATA%` |
| Cách ghi | file tạm cùng thư mục rồi `File.Replace` (không bao giờ file ghi dở); cùng đời DWG | ghi thẳng |
| Kiểu khác chuẩn trong từng file | giữ nguyên, ghi trong kết quả (không có thẻ cho từng file) | thêm một thẻ chung "ghi đè kiểu khác chuẩn" |
| File đang mở ở đây / có `.dwl` / chỉ đọc | bỏ qua khi ghi; `.dwl` có thể là file khóa cũ còn sót, khách xóa đi thì làm được | ghi đè bất chấp `.dwl` |
| `fix_drawing` hàng loạt | **chưa làm** (mỗi bản vẽ cần khách chọn layer / nhóm sửa) | thêm việc `fix` với các nhóm không cần chọn |
| So sánh H3 hàng loạt | chưa làm (cần cặp file) | thêm việc `compare` theo cặp tên |
| Thống kê / xuất bảng | đếm block theo tên (Excel) | thêm bảng diện tích theo layer |
| In PDF | mở từng file chỉ đọc trong AutoCAD (lệnh in chỉ in bản vẽ đang mở), mọi layout vào một PDF | in từ database bên cạnh (AutoCAD không hỗ trợ) |
| DXF | đọc được khi soát / đếm; không áp chuẩn / in | — |
| Nhóm gói | 2D (cả bốn việc là lệnh 2D) | Common khi có việc 3D |

## 5. Token lúc mở chat

| Gói / chế độ | Nạp gọn trước → sau | Tắt nạp gọn trước → sau |
| --- | --- | --- |
| 2D | 13,9k → 13,9k (+111 ký tự) | 47,3k → 47,9k |
| 3D | 19,1k → 19,1k (không đổi) | 46,4k → 46,4k |
| Full · Cả hai | 21,1k → 21,2k | 68,7k → 69,2k |

"Trước" là commit gộp `5184962`. Đo bằng số ký tự (khoảng 3,5 ký tự / token).

## 6. Test

- Lõi: **1572 / 1572** (thêm 8 ở `NangCapH5Tests.cs`).
- Trợ lý: **434 / 434** (thêm 5 ở `agent/test/nangcaph5.test.ts`: chỉ kế hoạch khi chưa có thẻ, mỗi file một lần gọi, file lỗi
  không dừng lượt, lỗi gói / Chỉ xem / thẻ dừng lượt, Dừng giữa hai file).
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi; 1 cảnh báo CA2022 ở `TraceTools.cs` có sẵn từ trước H4.
- SelfTest: bước "Nâng cấp đợt H5…": 2 bản sao bản vẽ test, soát 2 file, áp chuẩn 2 file (có sao lưu, đọc lại thấy VN-ARIAL), cần
  thẻ, không còn file tạm.
- CI: ghi ở mục 8.

## 7. Chưa kiểm được

- AutoCAD thật:
  - mở / in / đóng từng file từ khung chat (ngữ cảnh ứng dụng), trở lại bản vẽ của khách;
  - `ReadDwgFile` + `SaveAs` cùng đời rồi `File.Replace` trên ổ mạng;
  - `SetupStandards` / `SetupCompanyStandard` trên database không phải bản vẽ đang mở (đổi `WorkingDatabase` tạm).
- Thời gian thật với thư mục 100 – 200 file.

## 8. CI

- Gộp: run 45792384874 (#85) trên `5184962`: xanh, `# pass 429 / # fail 0`, lõi 1564.
- H5: run 45795177430 (#86) trên `a6ac65d` (commit code cuối): **xanh**, 1 / 2 lượt; log Windows `# pass 434 / # fail 0`, lõi 1572 / 1572. Commit sau đó chỉ là tài liệu (mục này).
