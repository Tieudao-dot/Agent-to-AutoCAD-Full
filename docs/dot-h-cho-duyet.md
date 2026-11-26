# Đợt H phần 1: con số và quyết định chờ chủ dự án duyệt

Chủ dự án chọn "chạy liền, số tiêu chuẩn hỏi trước" cho đợt H (26/11/2026): phần 1 không có số tiêu chuẩn, nên các thread dùng
số đề xuất, không dừng để hỏi. File này gom các số và các điểm tự chọn của H1 → H5 và các lượt rà để cậu duyệt **một lần**. Số
nào cậu muốn đổi thì chỉ sửa một chỗ (cột "Ở đâu").

Như đợt F / G (chủ dự án chọn 26/11 "thử thật rồi duyệt"): bản thử dùng các số đề xuất dưới đây. Thử xong luồng chính mới ghi "đã
duyệt" kèm ngày; tới lúc đó các số vẫn là **chờ duyệt**.

**Thread test tổng thể đợt H phần 1 đã soát từng dòng so với code (26/11, nhánh `full/nang-cap-h-tong-the`)**: các số khớp code.

**Số tiêu chuẩn phần 2:** xem `docs/dot-h-so-tieu-chuan.md` (luồng chính giữ), không chép lại ở đây.

## H1: skill khi nâng cấp, trace_image đọc thẳng PDF, soát nhiều tầng

Cài đè hoặc cập nhật trong app: skill khách chưa sửa được thay bản mới; skill khách đã sửa được giữ, bản mới nằm cạnh, khung chat
báo một lần. Ảnh Claude xem của PDF scan dựng thẳng bằng bộ đọc PDF của Windows 10 / 11. Bàn giao: `docs/ban-giao/nang-cap-h1.md`,
`nang-cap-h1-test.md`.

### Con số

| Số | Ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| **30 giây** chờ Windows dựng một trang PDF | `PdfRenderLogic.TimeoutSeconds` (`Core/PdfRender.cs`) | Trang quá nặng thì sau tối đa 30 giây ảnh xem lui về ảnh chụp AutoCAD; trong lúc chờ AutoCAD đứng | Trang scan A1 thường 1 – 5 giây |
| Độ rộng trang dựng **1024 – 6000 px**, bậc 256 | `PdfRenderLogic.MinWidthPx / MaxWidthPx`, `RenderWidth` | Độ nét khi Claude phóng to một phần bản scan; 6000 px giữ bộ nhớ ~100 MB một trang | 1024 = cỡ ảnh xem; bậc 256 để dùng lại ảnh đã dựng |
| Tên **`.ban-moi`** cho bản mới cạnh skill đã sửa | `SkillSync.SideSuffix` (`Core/SkillSync.cs`) | Thấy `SKILL.ban-moi.md` trong thư mục skill | Tiếng Việt không dấu, nằm cạnh file gốc |
| **`-2`, `-3`…** khi khách đã sửa cả `SKILL.ban-moi.md` (rà H1) | `SkillSync.SidePath(path, n)` | Thấy `SKILL.ban-moi-2.md` | Không ghi đè phần khách đã gộp |

Số bên trong (không phải duyệt): ảnh PDF lưu tạm giữ 7 ngày, file dở xóa sau 1 giờ (`CacheKeepDays`, `PartialKeepHours`); tối đa
99 tên `.ban-moi-n`; KFG-43 test 20 000 điểm < 1 giây.

### Quyết định tự chọn

- So "khách đã sửa chưa" ở plugin (C#, có test) lúc mở khung chat; một cách cho cả bộ cài và bản cập nhật trong app.
- Báo một lần mỗi bản mới; skill bị bỏ khỏi bộ cài thì không xóa ở máy khách.
- PDF đọc bằng Windows.Data.Pdf qua PowerShell có sẵn: **không thêm thư viện, bộ cài +0 MB**, không đổi target; đổi lại không chạy
  trên Windows 7 / 8 và chính sách nhóm chặn PowerShell thì lui về ảnh chụp như cũ.
- Script chạy trên luồng của AutoCAD (tối đa 30 giây), như ảnh chụp cũ.
- Rà H1: dòng báo skill thêm sau khi nạp lịch sử chat; ảnh dựng dở không bao giờ mang tên ảnh lưu; dọn thư mục ảnh tạm.

## H2: xuất DWG đời cũ / DXF / FBX

"Lưu ra DWG 2010 để gửi đối tác", "xuất DXF", "xuất FBX cho 3ds Max / Blender": file mới, bản vẽ đang mở giữ nguyên tên, file và đời.
Bàn giao: `docs/ban-giao/nang-cap-h2.md`, `nang-cap-h2-test.md`.

### Con số

| Số | Ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Đời DXF / DWG mặc định **2013** (AC1027) | `H2Numbers.DefaultVersion` (`Core/Export.cs`) | File `<bản vẽ>-acad2013.dwg`, mở bằng AutoCAD 2013 trở lên | Đời phổ biến nhất mà đa số phần mềm CAD và SketchUp đọc |
| Các đời cho chọn: **2018, 2013, 2010, 2007, 2004, 2000** | `ExportLogic.Versions` | Cũ hơn 2000 bị từ chối | Các đời AutoCAD .NET lưu được |
| Tối đa **2 000** khối trong một FBX | `H2Numbers.MaxFbxSolids` | Nhiều hơn thì nhờ chọn layer / khối | Như `export_stl` |

Số bên trong: DXF 16 chữ số thập phân; gộp đỉnh trùng của lưới theo 0,0001 mm.

### Quyết định tự chọn

Xem `docs/ban-giao/nang-cap-h2.md` mục 7 và `nang-cap-h2-test.md` mục 4. Chính:
- FBX do plugin tự ghi (không gọi `FBXEXPORT`): một lệnh không hỏi thêm, test được;
- bản vẽ đang mở không bao giờ đổi tên: xuất từ bản chép trong bộ nhớ;
- rà H2: FBX có pháp tuyến phẳng từng tam giác; gói 2D không thấy `fbx`; có cả `handles` và `layers` thì từ chối.

## H3: so sánh hai bản vẽ

"So bản vẽ này với file bản cũ": thêm / bớt / đổi, khoanh đám mây trên layer `X-SO-SANH`, xuất Excel. File kia chỉ được đọc.
Bàn giao: `docs/ban-giao/nang-cap-h3.md`, `nang-cap-h3-test.md`.

### Con số

Mọi số ở `H3Numbers` (`Core/DrawingCompare.cs`) và `OtherDrawingNumbers` (`Core/OtherDrawing.cs`).

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Dung sai **1 mm** (`ToleranceMm`) | Lệch ≤ 1 mm coi như không đổi; rà H3 dùng cho cả giá trị kích thước và độ dày polyline | Bản vẽ ghi số tới mm |
| Thể tích **0,1 %** (`VolumeShare`) | Thể tích khối 3D (rà H3: cả diện tích hatch) đổi ≤ 0,1 % coi như không đổi | Bỏ qua sai số tính của AutoCAD |
| **20 000** đối tượng mỗi bên (`MaxObjects`) | Nhiều hơn thì so phần đầu, có ghi chú | Như soát bản vẽ G2 |
| **50 %** handle trùng (`SameOriginShare`) | Từ đó trở lên mới xét khớp theo handle (rà H3: so cả hai cách, giữ cách nhiều đối tượng không đổi hơn) | Bản sửa của cùng một file giữ phần lớn handle |
| **100** dòng (`MaxRows`) | Hơn thì xem Excel | Kết quả ngắn cho Claude |
| **50 / 500** đám mây (`CardAboveClouds`, `MaxClouds`) | Quá 50 hỏi thẻ; quá 500 không vẽ | 50 đã kín một mặt bằng |
| Lề mây **5 %**, tối thiểu **100 mm** (`CloudMarginShare`, `CloudMinMarginMm`) | Khoảng hở giữa mây và chỗ khác | Đủ thấy ở tỉ lệ 1:100 |
| Màu **3 / 1 / 2** (xanh lá thêm, đỏ bớt, vàng đổi) | Màu đám mây | Như đèn giao thông |
| Layer **`X-SO-SANH`** (`Layer`) | Tên layer đám mây | Tiền tố `X-` như layer ảnh nền G5 |
| File đọc tối đa **200 MB** (`OtherDrawingNumbers.MaxFileMb`, dùng chung H3 / H4 / H5) | File lớn hơn bị từ chối / bỏ qua | Bản vẽ thường < 50 MB |

### Quyết định tự chọn

Xem `docs/ban-giao/nang-cap-h3.md` mục 7 và `nang-cap-h3-test.md` mục 5. Chính:
- không dùng lệnh DWG Compare dựng sẵn (không có API .NET, kết quả không đọc được); tự so bằng database đọc bên cạnh;
- bảng: chỉ Excel và danh sách trong kết quả, không vẽ bảng lên bản vẽ;
- so model space; block so ở mức block (tên, vị trí, xoay, tỉ lệ, thuộc tính);
- lần so sau không xóa đám mây cũ; layer `X-SO-SANH` in ra được;
- polyline có cung vẽ ngược chiều không nhận là một khi khớp theo hình học;
- rà H3: một bên không đặt đơn vị thì không đổi đơn vị, ghi chú để Claude hỏi.

## H4: học chuẩn công ty từ bản vẽ mẫu

"Học chuẩn công ty từ file mẫu", rồi "áp / soát / tạo tờ in theo chuẩn ABC". Bộ chuẩn ở `%LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty\`.
Bàn giao: `docs/ban-giao/nang-cap-h4.md`, `nang-cap-h4-test.md`.

### Con số

Mọi số ở `H4Numbers` (`Core/CompanyStandard.cs`).

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| **20** bộ chuẩn mỗi máy (`MaxStandards`; rà H4: bộ hỏng cũng tính) | Bộ thứ 21: Claude nhờ xóa bớt | Đủ cho văn phòng làm với nhiều đối tác |
| **1 000** layer học từ một file mẫu (`MaxLayers`) | Nhiều hơn: học 1 000 layer đầu, báo rõ | File mẫu thật thường 50 – 300 layer |
| **100** kiểu mỗi loại (`MaxStyles`) | Như trên | |
| **10** khung tên / khổ giấy (`MaxTitleBlocks`) | Như trên | Thường 1 – 3 layout mẫu |
| **12** cỡ chữ hay dùng nhất (`MaxHeights`) | Chữ lệch mọi cỡ này > 10 % (như G2) thì báo | Đủ cho tiêu đề / tên phòng / ghi chú |
| File mẫu chép giữ tối đa **50 MB** (`MaxTemplateMb`) | Lớn hơn: vẫn học layer / kiểu, không giữ khung tên | File mẫu thật thường < 5 MB |
| Tên bộ chuẩn tối đa **60** ký tự (`MaxNameLength`) | | |
| Sai số khổ giấy **2 mm** (`PaperTolMm`) | Layout cỡ khác A0 … A4: dùng A3 và nói rõ | Khổ của máy in hay lệch vài phần mm |

### Quyết định tự chọn

Xem `docs/ban-giao/nang-cap-h4.md` mục 4 (14 dòng) và `nang-cap-h4-test.md` mục 5. Chính:
- một lệnh `company_standard` (học / xem / xóa), dùng ở 3 lệnh cũ qua `company`; gói 2D / Full;
- chép file mẫu vào thư mục bộ chuẩn (`mau.dwg`) để giữ khung tên khi file gốc mất;
- kiểu trùng tên đã có giữ nguyên; layer khác chuẩn qua thẻ (rà H4: đặt lại thuộc tính, không thay bằng bản chép);
- `fix_drawing` không áp chuẩn công ty (rà H4: có `company` thì từ chối nhóm theo chuẩn);
- khung tên công ty thay cả khung viền VN; khổ giấy lạ dùng A3; rà H4: khổ khác khổ mẫu thì báo khung tên có thể lệch;
- rà H4: tên bộ chuẩn trùng tên thiết bị Windows (CON, NUL…) thêm hậu tố `-chuan`.

## H5: làm nhiều file một lúc

"Soát / áp chuẩn / in PDF / đếm block cho cả thư mục": thẻ trước khi chạy, mỗi file một dòng xong / lỗi / bỏ qua, Dừng được giữa
hai file; áp chuẩn sao lưu trước. Bàn giao: `docs/ban-giao/nang-cap-h5.md`, `nang-cap-h5-test.md`.

### Con số

Mọi số ở `H5Numbers` (`Core/BatchFiles.cs`).

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| **200** bản vẽ mỗi lượt (`MaxFiles`) | Nhiều hơn: làm 200 file đầu (theo tên), Claude nói số còn lại | Dự án lớn thường vài chục – 150 bản vẽ |
| Thư mục con sâu **5** cấp (`MaxDepth`) | Sâu hơn không đọc | Thư mục dự án thường 2 – 3 cấp |
| **30** tên file kể trong thẻ / kết quả (`MaxListed`) | Phần còn lại chỉ đếm | Gọn trong khung chat |
| **3** nhóm lỗi ghi cho mỗi file khi soát (`TopGroups`) | | |
| Cỡ file tối đa **200 MB** (dùng `OtherDrawingNumbers.MaxFileMb`) | Lớn hơn: bỏ qua | Như H3 |
| Tên thư mục sao lưu **`_sao-luu-claudecad`** (`BackupFolder`) | | Dấu `_` để nằm đầu danh sách |

Số bên trong: `BatchDocSwitches.KeepSeconds` = 30 giây (rà H5).

### Quyết định tự chọn

Xem `docs/ban-giao/nang-cap-h5.md` mục 4 (11 dòng) và `nang-cap-h5-test.md` mục 5. Chính:
- một lệnh `batch_files` 4 việc; mỗi lần gọi một file, trợ lý lặp;
- sao lưu cạnh thư mục (giữ cây con); ghi qua file tạm rồi thay (`File.Replace`), cùng đời DWG;
- kiểu khác chuẩn trong từng file giữ nguyên (không thẻ cho từng file);
- bỏ qua file đang mở ở đây / có `.dwl` / chỉ đọc khi ghi;
- chưa làm `fix_drawing` và so sánh hàng loạt.

## Sửa CI đợt H

`docs/ban-giao/nang-cap-h-sua-ci.md`: không có số. Quyết định: kiểm mã lỗi sau từng lệnh thay vì bật cho cả job; `build.yml` của
`full/ban-thu-noi-bo` chưa sửa (ghi để chép sang khi build bộ cài thử sau).

## Test tổng thể đợt H phần 1

Không thêm số. Hai lỗi chéo đã sửa (`docs/ban-giao/test-tong-the-dot-h-phan-1.md`):
- **TTH-1:** áp chuẩn hàng loạt kiểm đầu file như phần đọc chung H3 trước khi sao lưu;
- **TTH-2:** ba lệnh file của đợt H giữ hệ WCS.

Quyết định: không có điểm mới.
