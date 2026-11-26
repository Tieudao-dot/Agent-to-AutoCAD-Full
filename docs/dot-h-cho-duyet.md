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

## Sau kiểm tra độc lập đợt H phần 1 (sửa KTH, 26/11)

Báo cáo: `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md` (27 lỗi KTH). Bàn giao: `nang-cap-h-sua-kth-a.md` (H4, prompt),
`nang-cap-h-sua-kth-b.md` (H1 / H2 / H3), `nang-cap-h-sua-kth-c.md` (H5).

### Con số mới

| Số | Ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| **5 000** chỗ khác (H3) | `H3Numbers.MaxRowsForClouds` (`Core/DrawingCompare.cs`) | So hai bản khác nhau quá nhiều: Claude nói không vẽ đám mây, đưa danh sách / Excel | Số đã có trong code (`MaxClouds × 10`), nay thành hằng số riêng; 5 000 chỗ khác là hai bản gần như khác hẳn |
| Chờ tối đa **5 phút** cho `export_drawing`, **2 phút** cho `compare_drawings` và `company_standard` học mẫu (H2 / H3 / H4) | `timeoutFor` (`agent/src/ipc/rpc.ts`) | File quá lớn mà quá giờ thì Claude báo hết giờ (trước là 30 giây như lệnh vẽ) | 5 phút = trần chung của lệnh chậm; so / học một bản vẽ lớn thường dưới 1 phút |
| Mỗi file của `batch_files`: tối đa **2 phút** khi soát / đếm block, **5 phút** khi áp chuẩn / in PDF (H5, KTH-02) | `timeoutFor` (`agent/src/ipc/rpc.ts`); xem `nang-cap-h-sua-kth-c.md` | Một file quá hạn thì Claude dừng cả lượt và báo | Soát / đếm chỉ đọc file; áp chuẩn / in PDF ghi file nên cần lâu hơn |

Số bên trong (không phải duyệt):
- giá trị thuộc tính động làm tròn 3 số lẻ;
- góc mẫu hatch làm tròn 0,001°;
- tối đa 5 000 ô chữ đọc mỗi bảng (H3);
- nhận khung nhìn của chính layout mẫu khi to bằng 98 % tờ giấy (H4).

### Quyết định tự chọn mới

- **H3:**
  - polyline kín: tra theo tâm, so theo dung sai từ mọi đỉnh đầu, cả hai chiều (KTH-15, sửa lại ở KTH-28). Hạn chế còn lại: phòng vừa đổi hình vừa có hai đỉnh chênh x dưới dung sai có thể ra "bớt + thêm" thay vì "đổi";
  - độ dài để đo góc là đường chéo khung bao (KTH-17);
  - chỗ cũ và chỗ mới của một đối tượng tính là một chỗ khác (KTH-22);
  - `compare_drawings` không còn khai "chỉ đọc" (có khoanh mây).
- **H1:** skill khách đã xóa quay lại khi bản cài sau có bản mới của nó; muốn bỏ hẳn thì tắt trong ⚙ (KTH-25).
- **H4:**
  - kiểu mặc định của AutoCAD mà mẫu không dùng thì không học (KTH-09);
  - tên bộ chuẩn chỉ khác dấu câu mà chung thư mục thì từ chối (KTH-13);
  - tên có chữ ngoài a–z có mã riêng trong tên thư mục;
  - khung tên là block nhiều ô nhất.
- **H5:**
  - `batch_files` từ chối khi đang phát macro;
  - một file quá hạn thì dừng cả lượt, kể các file đã xong, nhờ khách mở kiểm file quá hạn trước khi chạy lại (KTH-02).
- **Prompt:** mục 7 cho phép các lệnh làm file khi khách yêu cầu (KTH-27).


# Đợt H phần 2 (H6 → H9): con số và quyết định chờ chủ dự án duyệt

*Thread test tổng thể đợt H phần 2 gom 26/11 (nhánh `full/nang-cap-h-tong-the-2`) và soát từng dòng với code: khớp. Các thread viết
và rà đã soát bảng của đợt mình; H7 "5 000 đối tượng" đã sửa thành "5 000 khối 3D" (H7-test).*

**Số theo tiêu chuẩn (H6, H7, H8):** đã duyệt 26/11 ("dùng hết số đề xuất"; D60 dốc 2,5 %, D110 trở lên 2 %; vạch lý trình 50 m trên
mặt bằng, 20 m trên trắc; không cộng hao hụt). Xem `docs/dot-h-so-tieu-chuan.md`, không chép lại ở đây. Sáu chỗ "chưa chắc" ghi
"số mặc định, kỹ sư đối chiếu TCVN hiện hành" trong skill và mô tả lệnh (test tổng thể thêm câu này vào mô tả `draw_mep`, vì máy
gói 3D không có skill `ve-mep`).

**Hai lựa chọn luồng chính tự chọn ở H9:** (1) file Excel giữ tiếng Việt như bảng chèn vào bản vẽ; (2) xuất chat ra Markdown theo
ngôn ngữ giao diện lúc xuất.

## H6: hạ tầng (`terrain`)

### Con số

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Số điểm mỗi lần | 10 000 | nhiều hơn: báo chia file | lưới tam giác và đồng mức vẫn nhanh; khu đo thường vài trăm – vài nghìn điểm |
| Cỡ file điểm | 20 MB | lớn hơn: báo | 10 000 dòng CSV chỉ khoảng 0,5 MB |
| Số cọc mỗi tuyến | 1 000 | nhiều hơn: nhờ tăng khoảng cọc | 20 km ở 20 m |
| Số trắc ngang mỗi lần | 200 | nhiều hơn: nhờ chọn cọc hoặc tăng khoảng | một lượt vẽ không quá nặng |
| Số mức đồng mức | 2 000 | nhiều hơn: nhờ tăng khoảng cao đều | |
| Tỉ lệ in mặt bằng mặc định | 1:500 | cỡ chữ điểm / đồng mức / lý trình | như skill `ve-ha-tang` |
| Bước lấy mẫu trắc ngang | 1 m, bỏ điểm lệch < 0,01 m so với đường thẳng | số cột trong bảng trắc ngang | đủ mịn, bảng gọn |
| Bỏ tam giác cạnh dài | > 4 × cạnh trung vị (`max_edge` 0 thì giữ hết) | không có đồng mức cắt qua vùng trống | |
| Bố trí khung (mm giấy) | dòng bảng trắc dọc 10, cột nhãn 45; dòng trắc ngang 8, cột nhãn 30; vạch lý trình 4; khoảng cách khung 15; 5 trắc ngang mỗi cột | hình khung | |
| Hai điểm trùng | cách nhau < 1 mm (thật) thì bỏ điểm sau | số điểm "trùng bỏ" | |
| Ngưỡng tọa độ thật | > 100 000 m | Claude hỏi gốc tọa độ | VN-2000 ở Việt Nam luôn lớn hơn |
| Tên / màu layer | `C-SURVEY-PT` 3, `C-LEVEL` 2, `C-COORD` 3, `C-TOPO-MINR` 8, `C-TOPO-MAJR` 33 (0,35), `C-STATION` 2, `C-PROF-GRID` 8, `C-PROF-GRND` 3 (0,35), `C-PROF-DSGN` 1 (0,35), `C-PROF-TEXT` 7, `C-TEXT` 7 | | `C-LEVEL`, `C-COORD`, `C-STATION`, `C-TEXT` giữ như skill |

Số theo tiêu chuẩn (mục 3a): **đã duyệt 26/11**; các số trong bảng 3b vẫn chờ duyệt.

Thời gian chờ lệnh (số kỹ thuật, không phải hỏi): `terrain` vào nhóm lệnh chậm, 2 phút.

*Test & sửa H6:* Không có số mới khách thấy. Số kỹ thuật: số thứ tự "tăng 1 mỗi dòng" ở ≥ 80 % số dòng (H6T-2).

### Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Một lệnh hay nhiều | **một lệnh** `terrain` với 5 việc, hoãn nạp | tách lệnh |
| Khối lượng đào đắp | **không làm** (dễ hiểu nhầm là tính toán; cần mặt cắt thiết kế đầy đủ). Trắc dọc chỉ ghi chênh cao TK − TN tại cọc | thêm việc `volumes` theo trắc ngang |
| Điểm khảo sát lưu ở đâu | dấu chấm (POINT) ở đúng cao độ trên layer `C-SURVEY-PT`; đồng mức / trắc đọc lại từ layer đó mỗi lần | block có thuộc tính |
| Tọa độ thật hay gốc cục bộ | **hỏi khách** (như skill); `origin` "auto" = X / Y nhỏ nhất làm tròn xuống 100 m | tự chọn gốc |
| Cột X / Y có vẻ đảo | chỉ cảnh báo, không tự đảo | tự đảo |
| Đồng mức | nối thẳng giữa các cạnh tam giác, **không làm trơn**; mỗi đường ở cao độ của nó | làm trơn (spline) |
| Vẽ lại đồng mức | thêm đường mới, không xóa đường cũ (xóa là việc khách chọn) | tự xóa layer đồng mức cũ (cần thẻ) |
| Cao độ tự nhiên ở cọc | nội suy trên lưới tam giác tại cọc (thẳng giữa hai cọc) | thêm điểm gãy địa hình giữa các cọc |
| Tên cọc | Km0, H1…H9, C1, C2… (cọc chi tiết đánh số liên tục), Đ1… (đỉnh), Đầu / Cuối khi không trùng cọc chẵn | đánh số theo từng 100 m |
| Trái / phải trắc ngang | nhìn theo chiều tăng lý trình, bên trái vẽ bên trái | |
| Đường thiết kế trắc dọc | khách đưa các điểm [lý trình, cao độ]; nối thẳng (không có đường cong đứng) | thêm cong đứng |
| Excel đầu vào | đọc sheet đầu, giá trị đã tính của công thức; .xls cũ: nhờ lưu .xlsx | |
| Mã hóa CSV | UTF-8, rồi Windows-1258, rồi Latin | |
| Hệ tọa độ | giữ WCS (không đổi theo UCS) | |
| Thẻ F5 | không có (không ghi file) | |

*Test & sửa H6:*

- Đơn vị bản vẽ đọc từ `INSUNITS`. Bản vẽ không đặt đơn vị coi là mm, như các lệnh khác.
- Vạch 50 m trên mặt bằng chỉ ghi lý trình, không đặt tên `C…`, để một tên chỉ một chỗ.
- `C-…` là layer chuẩn cho `check_drawing`. Mọi layer bắt đầu bằng `C-` đều tính là chuẩn, như `A-…`.

## H7: ống MEP 3D (`draw_mep`, `mep_report`)

### Con số

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 200 điểm | `H7Numbers.MaxPoints`: số điểm một tuyến | Tuyến dài hơn: chia làm nhiều lần | Một tuyến trong một tầng hiếm khi quá vài chục góc |
| 5 000 khối 3D | `MaxSolids`: số khối 3D đọc khi thống kê / soát / tìm dầm (nét 2D, chữ không tính; *Test & sửa H7 soát lại*) | Bản vẽ nhiều khối hơn: chỉ đọc phần đầu, có ghi chú | Như các lệnh soát khác |
| 1,15 lần | `FittingOversize`: tê to hơn ống chính | Tê thấy rõ trên mô hình | Giống ống nối thật (đầu nối to hơn ống) |
| 2 lần đường kính nhánh | `TeeLengthFactor`: chiều dài thân tê dọc ống chính | Cỡ tê trên mô hình | Vừa ôm chỗ nối |
| 300 dòng | `MaxClashRows`: số chỗ chạm / sát kể trong kết quả | Nhiều hơn: chỉ đếm | Kết quả ngắn cho Claude |

### Quyết định tự chọn

- **Hai lệnh**, không phải một: vẽ (ghi) và báo cáo (đọc). Lý do: lệnh đọc chạy được ở "Chỉ xem"; mô tả lệnh ngắn hơn.
- Ống và co là **khối 3D riêng**, không gộp. Mỗi khối mang dấu riêng để thống kê đúng sau khi khách xóa / chép một đoạn.
  Chiều dài lấy theo dấu lúc vẽ. Khách kéo giãn ống bằng lệnh AutoCAD thì thống kê không đổi; vẽ lại bằng `draw_mep` thì đúng.
- Chiều dài co tính theo đường tim (bán kính × góc), cộng vào chiều dài của cỡ ống đó.
- Tê đếm vào hệ và cỡ của **nhánh**.
- Soát "sát": ống thẳng đo từ trục (đúng cả khi chéo); co / tê đo giữa hai hộp bao. Ống gió lấy nửa đường chéo mặt cắt
  (về phía an toàn).
- Dầm / cột tìm theo tên layer (BEAM / COL / DAM / COT / S-). Khách dùng tên khác thì chỉ khối qua `other_handles`.
- `under_beams`: dầm tìm theo tên layer BEAM / DAM, sàn theo SLAB / SAN; lấy đáy thấp nhất trong vùng tuyến đi qua.
- Không tính độ dốc cho ống cấp nước / PCCC / ống gió, trừ khi khách cho `slope`.

*Test & sửa H7:* không có số mới; nhận xét để lại (tê ghi cỡ ống nhánh trong thống kê; nhận layer dầm / cột theo chữ `BEAM` / `COL` / `DAM` / `COT` / `S-` còn rộng) ở `nang-cap-h7-test.md` mục 5.

## H8: kết cấu (`build_footing`, `draw_rebar`, `rebar_schedule`)

### Con số

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 50 mm | `FirstStirrupMm`: đai đầu tiên cách mép gối | Vị trí đai đầu | Thông lệ |
| 50 – 1000 mm | `MinSpacingMm` / `MaxSpacingMm`: khoảng cách thép nhận được | Ngoài khoảng: báo lỗi | Chặn gõ nhầm (a20, a2000) |
| 40 thanh | `MaxBarsInSection` | Nhiều hơn: báo lỗi | Một mặt cắt hiếm khi quá 20 |
| 500 móng | `MaxFootings`; 400 nét đai vẽ mỗi lần (`MaxStirrupLines`, số đếm vẫn đủ) | Giới hạn | AutoCAD không bị chậm |
| 2,5 mm chữ, bóng số hiệu 8 mm trên giấy | `TextHeightPaper`, `MarkBubblePaper` | Cỡ chữ, vòng số hiệu | Như chữ thường TCVN 7284 |
| màu layer 8 / 9 / 7 / 1 / 7 | `FootingColor`, `LeanColor`, `ConcreteColor`, `RebarColor`, `TextColor` | Màu nét | Thép đỏ cho dễ thấy |
| khoảng cách mặt cắt cạnh triển khai | max(1000, 3 h) mm | Vị trí mặt cắt | Không chồng lên triển khai |

### Quyết định tự chọn

- **Ba lệnh**: móng (3D), vẽ thép (2D), bảng (2D). Theo kế hoạch, móng 3D thuộc gói 3D / Full, thép 2D thuộc gói 2D / Full.
- Dầm vẽ triển khai **và** mặt cắt trong một lần gọi. Dấu thống kê nằm trên triển khai, nên bảng không đếm hai lần.
- Dấu thống kê nằm trên **thanh đầu tiên** của mỗi loại, ghi luôn số thanh. Xóa thanh đó thì loại thanh đó biến khỏi bảng.
  Xóa thanh khác thì bảng vẫn đếm đủ (đếm theo số lúc vẽ).
- Thép gân dầm: neo 30d rồi **bẻ L 10d** ở mỗi đầu (làm theo skill, số đã duyệt; sửa 26/11 sau khi thread F2 đọc trước
  `c01e557` thấy code chưa dùng 10d). Chọn làm theo skill thay vì sửa skill vì 10d là số đã duyệt và cộng thêm thép (thống
  kê không thiếu). Thanh dưới bẻ lên, thanh trên bẻ xuống. Chỉ áp cho dầm: thép cột là thanh nối (không neo), thép sàn mặc
  định Ø8 trơn (móc 180°); thép sàn gân khách nhập vẫn tính thẳng.
- Đoạn nối chồng của thanh dài hơn cây 11,7 m được cộng ở **bảng thép** (`rebar_schedule`), không cộng vào chữ "L=…" trên
  hình (chữ trên hình là chiều dài hình học); `draw_rebar` báo trước số mối nối. Nối làm dài thanh nên số mối nối được đếm
  lại tới khi không đổi.
- Số hiệu trùng: không tự đánh số lại (sẽ lệch chữ trên hình), chỉ tách dòng và báo.
- Móc 180° của thép trơn được tính vào chiều dài, không vẽ hình móc. Đai tính hai móc 135°, đoạn thẳng max(6d, 75).
- Một mặt cắt chỉ một hàng thép dưới / một hàng trên. Không đủ chỗ thì báo, không tự xếp hai hàng.
- Móng băng: lót không kéo dài thêm 100 mm ở hai đầu tuyến (chỉ hai bên).

*Test & sửa H8:* không có số mới (mặt cắt dầm vẽ phóng `tỉ lệ / 20` lần để in cùng khung 1:50 đọc đúng 1:20 — đó là cách vẽ, không phải số mới).

## H9: giao diện tiếng Anh

### Con số

Không có số mới khách thấy (H9 và Test & sửa H9).

### Quyết định tự chọn

| # | Quyết định | Lý do / đổi lại được không |
| --- | --- | --- |
| 1 | **Chữ vẽ vào bản vẽ giữ tiếng Việt khi chọn English** (chủ dự án chốt 26/11/2026 ~20h45). Gồm: tiêu đề bảng, khung tên, tên tờ, tên cọc, mô tả layer. | Hồ sơ nộp ở Việt Nam theo chuẩn Việt Nam. Khách nhờ Claude đổi từng chữ được. Có test kiểm các file vẽ chữ không gọi `Tr` |
| 2 | **File Excel giữ tiếng Việt như bảng trên bản vẽ** (luồng chính chọn 26/11): tiêu đề, "Cộng Ø", "Tổng" | Bảng vừa chèn vào bản vẽ vừa xuất Excel, và `XlsxLogic.DataRows` nhận dòng "Tổng" bằng cách so chữ. Tài liệu khách ghi "(cả bảng xuất Excel)" |
| 3 | Không dịch skill | Claude đọc skill tiếng Việt rồi trả lời tiếng Anh. Rẻ, an toàn, skill khách sửa không phải giữ hai bản |
| 4 | Lời báo plugin gửi cho Claude (lỗi lệnh, NOT_IN_PLAN, "Chỉ xem"…) không dịch | Claude đọc rồi tự nói lại bằng ngôn ngữ khách chọn. Đã dặn trong `ENGLISH_SECTION` |
| 5 | SelfTest: chỉ dịch tên bước và dòng tổng; chi tiết kỹ thuật từng bước giữ tiếng Việt | Phần chi tiết là để người phát triển đọc (khoảng 240 câu kỹ thuật). Dịch thêm thì tốn và dễ sai mà khách không cần |
| 6 | Gói báo cáo lỗi (`DiagnosticsBundle`) giữ tiếng Việt | Gửi cho người hỗ trợ Việt Nam, không phải để khách đọc |
| 7 | Đổi ngôn ngữ: chữ đổi ngay; Claude đổi từ **tin nhắn tiếp theo** | Luồng chính duyệt là "từ chat mới"; làm từ tin nhắn tiếp theo vẫn đúng ý đó và nhanh hơn. Claude được khởi động lại với cùng cuộc chat, không mất ngữ cảnh |
| 8 | Ngôn ngữ chọn ở bộ cài chỉ áp dụng cho máy chưa có cài đặt khung chat | Máy đang dùng không bị đổi ngôn ngữ khi cài đè |
| 9 | Bộ cài dùng `Default.isl` cho cả hai ngôn ngữ | Inno Setup không có sẵn file tiếng Việt chính thức. Nút chuẩn (Next / Back…) vẫn tiếng Anh như bản trước; các câu khách hay vướng thì đã tiếng Việt |
| 10 | Dòng liên hệ mặc định "Liên hệ Zalo owner…" có bản tiếng Anh. Dòng chủ plugin tự đặt (`update-config.json` / `activation.json`) giữ nguyên chữ chủ plugin viết | Không đoán bản dịch chữ của chủ plugin. Trang kích hoạt trong bộ cài English vẫn hiện dòng liên hệ đã cấu hình (mặc định tiếng Việt) |
| 12 | **Xuất chat ra Markdown theo ngôn ngữ giao diện lúc xuất** (luồng chính chọn 26/11) | Đây là chữ giao diện, không phải chữ trên bản vẽ |
| 13 | Câu dặn Claude "tell the user in Vietnamese" đổi thành "in the user's language" / "in the language of the user" | Prompt gốc nói ngôn ngữ của khách là tiếng Việt, trừ khi phần "# Language" ở cuối nói khác. Rẻ, không phải nhánh hai bản prompt |
| 14 | `install.ps1` (cài bằng PowerShell cho người quản trị) giữ tiếng Việt | Dùng cho người cài hộ nhiều máy, không phải khách |
| 11 | Claude nhận thêm "yes, ok, sure, proceed, go ahead, do it…" là đồng ý; "no, not, don't, stop, cancel, wait" là không. **Từ phủ định ở bất kỳ đâu trong câu là không đồng ý** (cả tiếng Việt: "ok, nhưng đừng xóa tường" = không; sửa KTP-47 26/11: "không sao" cũng là không, Claude hỏi lại) | Không chắc thì không đồng ý. Luật khối 3D vẫn chỉ nhận nút trên thẻ |

## Test tổng thể đợt H phần 2

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Skill `ve-mep`, `ve-ket-cau` là `plan: 2d` (máy gói 3D không nạp) | **giữ** `plan: 2d` (quyết định 12/11: skill 2D không nạp trên máy 3D để không tốn token và không gợi lệnh 2D). Mô tả `draw_mep` / `build_footing` đã có số mặc định và câu "kỹ sư đối chiếu TCVN" | đổi sang `plan: all` (máy 3D nạp cả phần 2D của skill) |
| Soát theo chuẩn công ty (H4) với layer của lệnh mới | layer không có trong bộ chuẩn công ty bị báo khác chuẩn (như `A-…`) | miễn layer của lệnh như layer khung tên |
| `mep_report`, `rebar_schedule` dưới UCS | đọc, không đổi tọa độ; `table_position` là điểm UCS (như lệnh đọc khác) | |

## Sau kiểm tra độc lập đợt H phần 2 (sửa KTP, 26/11)

### Quyết định tự chọn mới

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| *(KTP-21, H8)* Móc 180° thép trơn | thép trơn đo tới mép lớp bảo vệ rồi cộng **6,25d** mỗi móc (luồng chính chọn); sàn Ø8a200 ra L = 3060 | `H8Numbers.PlainHookAddFactor` |
| *(H7)* Ống gió rẽ theo cạnh cao (rẽ đứng) | bán kính mặc định chỉ tăng khi cần: co rẽ theo cạnh lớn hơn bề rộng lấy 1 × cạnh đó, và báo khách | `MepLogic.BendRadius` |
| *(H8)* Nhịp dầm nhỏ nhất | 100 mm (nhỏ hơn: báo lỗi, nhờ kiểm đơn vị) | `H8Numbers.MinSpanMm` |
| *(H8)* Bảng thép | thêm dòng "Cộng CB240-T" / "Cộng CB400-V" (tổng theo loại thép) | |
| *(KTP-13, H7)* Khe hở báo ra | đo theo tiết diện về phía vật kia (dưới dầm: nửa chiều cao); bước kiểm chạm thật vẫn dùng nửa đường chéo (không bỏ sót) | |
| *(H7)* Nhận layer dầm / cột khi soát va chạm | `S-` chỉ khi đứng đầu tên layer | `MepLogic.IsStructureLayer` |
| *(H7)* `under_beams` | theo từng đoạn tuyến (dầm trên đoạn nào thì đoạn đó) | |
| *(H6, KTP-08 / 09)* File không tiêu đề, cột 1 là số nguyên nhỏ trước tọa độ thật | cột 1 là tên điểm | cho cột bằng `columns` |
| *(H6, KTP-10)* Số "1.234" (một dấu chấm) | là số thập phân 1,234; chỉ ≥ 2 nhóm hoặc nhóm + phẩy thập phân mới là dấu nghìn | |
| *(KTP-02 → 04, H9)* Xác nhận gõ tay | **danh sách đóng**: chỉ đồng ý khi mọi chữ trong câu là chữ đồng ý, chữ của cụm đồng ý ("xóa đi", "go ahead") hoặc chữ đệm (nhé, ạ, please…). Câu có chữ khác ("ko", "nếu", "if", "thôi", "chờ", "later", "❌"…) là không chắc = không đồng ý, Claude hỏi lại. "thôi được", "được thôi", "ok thôi" là đồng ý; "thôi" đứng riêng và "không sao" là không (sửa KTP-47, 26/11) | thêm chữ đệm vào `FILLERS` (`agent/src/agent/confirmation.ts`) khi thử thật thấy thiếu |
| *(KTP-07, H9)* Câu lỗi từ máy chủ kích hoạt khi English | máy chủ không đổi; plugin và bộ cài dịch 8 câu lỗi cố định, câu lạ hiện nguyên văn | `L.ServerTexts` |
| *(KTP-05, H9)* Ngôn ngữ lúc AutoCAD vừa nạp plugin | đặt theo cài đặt (máy mới: ngôn ngữ chọn ở bộ cài) trước dòng "đã nạp…" | |

### Con số mới chờ duyệt

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 6,25d mỗi móc 180° thép trơn | `H8Numbers.PlainHookAddFactor` (KTP-21; luồng chính chọn) | Chiều dài thanh thép trơn trong bảng thép (sàn Ø8a200: L = 3060) | Cách tính móc 180° thường dùng (đo tới mép lớp bảo vệ rồi cộng 6,25d); kỹ sư đối chiếu TCVN hiện hành |
| 100 mm | `H8Numbers.MinSpanMm`: nhịp dầm nhỏ nhất | Nhịp nhỏ hơn: báo lỗi, nhờ kiểm đơn vị | Chặn bản vẽ sai đơn vị (m vẽ thành mm) mà không chặn dầm thật |
| 1 × cạnh | `MepLogic.BendRadius`: bán kính co ống gió khi rẽ theo cạnh lớn hơn bề rộng | Co rẽ đứng của ống gió dẹt to hơn trước, có câu báo | Co không tự cắt vào mình; như co ống gió thường gặp |
