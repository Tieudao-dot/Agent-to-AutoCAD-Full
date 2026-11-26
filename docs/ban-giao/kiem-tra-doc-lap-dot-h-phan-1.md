# Kiểm tra độc lập đợt H phần 1 (H1 → H5)

*Luồng chính lưu 26/11/2026 từ toàn văn thread gửi về (thread chỉ ghi lỗi, không commit). Thread
`session_01D56HmH63NM9pSxbHQh6Ckv`. Tóm tắt: 27 lỗi (2 nặng, 13 vừa, 12 nhẹ). Chi phí ~$13, ~55 phút.*

- Thread: Kiểm tra độc lập đợt H phần 1. Agent mới, chưa viết hay test phần nào của đợt H.
- Ngày: 26/11/2026.
- Nhánh rà: `full/nang-cap-h-tong-the` @ `aa5753c` (code `9612510`). Rà các thay đổi từ `547d6af` tới HEAD.
- Thread này chỉ ghi lỗi: không sửa code, không commit, không push, không chạy CI.
- Cách làm:
  - chạy đủ test và build;
  - 4 tác vụ con rà song song (H5, H4, H3, H1 + H2), mỗi tác vụ viết test thử trên một bản sao riêng trong scratchpad (không giữ lại);
  - thread chính rà workflow CI, prompt, README, SelfTest, rồi tự soát lại code của các lỗi nặng và vừa chính.

## Test đã chạy

| Phần | Kết quả |
| --- | --- |
| Lõi `dotnet test plugin/ClaudeCAD.Core.Tests` | 1581 / 1581 qua |
| Trợ lý `cd agent && npm ci && npm test` | 436 / 436 qua, `# fail 0` |
| Build `net48`, `net8.0-windows`, `net10.0-windows` (Linux, `-p:EnableWindowsTargeting=true`, xóa obj / bin giữa các lần) | cả 3 thành công, 0 lỗi |
| Test thử chứng minh lỗi (không giữ lại) | H3: 7 test (4 hỏng đúng các lỗi); H4: 4 C# + 2 TS; H1 / H2: 9 test; H5: 2 file TS + 1 file C# |

## Bảng lỗi

Tổng cộng 27 lỗi: 2 nặng, 13 vừa, 12 nhẹ. Cột "Thread sửa" do luồng chính chia 26/11.

| Mã | Mức | Mảng | Lỗi | Thread sửa |
| --- | --- | --- | --- | --- |
| KTH-01 | **nặng** | H5 | Bấm Đồng ý xong, `batch_files` vẫn không chạy file nào | C |
| KTH-02 | **nặng** | H5 | Mỗi file chỉ có 30 giây; quá hạn thì báo lỗi dù file đã ghi, các file sau quá hạn dây chuyền | C |
| KTH-03 | vừa | H5 | Hai file trùng tên dùng chung một bản sao lưu: file thứ hai bị ghi mà không có sao lưu | C |
| KTH-04 | vừa | H5 | Đếm block dừng ở 20 000 đối tượng mà không báo | C |
| KTH-05 | vừa | H5 | `groups` / `scale` sai chỉ lộ ra sau khi khách duyệt thẻ, có thêm bản sao lưu thừa | C |
| KTH-06 | nhẹ | H5 | `backup_folder` báo sai khi file nằm ở nhiều thư mục | C |
| KTH-07 | nhẹ | H5 | `folder` không có thật vẫn bị tạo ra | C |
| KTH-08 | nhẹ | tài liệu | Checklist H5 C4 hứa bỏ qua file có thay đổi chưa lưu, code chỉ làm được với tab đang xem | C |
| KTH-09 | vừa | H4 | Chuẩn công ty học luôn kiểu "Standard": soát không bao giờ báo chữ / kích thước kiểu Standard | A |
| KTH-10 | vừa | H4 | Áp chuẩn công ty khi hơn 50 layer khác chuẩn: bước hai không gửi được | A |
| KTH-11 | vừa | H4 | `create_sheet company` báo ô khung tên "đã điền" dù không điền gì | A |
| KTH-12 | nhẹ | H4 | Tờ in tạo theo chuẩn công ty bị chính lệnh soát theo chuẩn đó báo layer KHUNG sai chuẩn | A |
| KTH-13 | nhẹ | H4 | Tên công ty khác nhau ra cùng một thư mục, nên dùng nhầm bộ chuẩn mà không báo | A |
| KTH-14 | nhẹ | H4 | Mẫu khác đơn vị: cỡ cố định của kiểu chữ không được đổi theo đơn vị (đường lui) | A |
| KTH-15 | vừa | H3 | Polyline kín vẽ ngược chiều hoặc bắt đầu ở góc khác bị báo "bớt + thêm" | B |
| KTH-16 | vừa | H3 | Điểm neo của polyline lấy nhầm x0, y0, x1: kéo giãn phòng bị báo "bớt + thêm" thay vì "đổi" | B |
| KTH-17 | nhẹ–vừa | H3 | Góc của cung / chữ / block so khít, không theo dung sai | B |
| KTH-18 | vừa | H3 | Bỏ sót thay đổi của block động | B |
| KTH-19 | vừa | H3 | Bỏ sót đổi chữ trong MLeader / Table, đổi điểm điều khiển của Spline | B |
| KTH-20 | nhẹ | H3 | Không so tỉ lệ / góc hatch, kiểu kích thước | B |
| KTH-21 | nhẹ | H3 | Hơn 5 000 dòng khác thì báo sai "hơn 500 đám mây"; số 5 000 không có trong bảng duyệt | B |
| KTH-22 | nhẹ | H3 | Dòng "đổi" chỉ khoanh chỗ mới, chỗ cũ không có dấu | B |
| KTH-23 | vừa | H2 | FBX ép đỉnh về float sau khi cộng lại độ dời: lại hỏng KT2-06 với tọa độ xa gốc | B |
| KTH-24 | vừa | H1 | Cập nhật trong app từ bản trước H1 lên H1 không mang skill mới tới | B |
| KTH-25 | nhẹ | H1 | Khách xóa một skill thì skill đó tự quay lại mỗi lần mở khung chat | B |
| KTH-26 | nhẹ | tài liệu | Checklist H1 ghi sai thư mục cài | B |
| KTH-27 | nhẹ | chéo (prompt) | Prompt mục 7 cấm "đụng file khác bản vẽ đang mở", mâu thuẫn với `batch_files` / `company_standard` | A |

Thread sửa:
- A = thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh`, sửa ngay trên nhánh tích hợp `full/nang-cap-h-tong-the`, rồi gộp hai nhánh kia và chạy CI;
- B = thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5`, nhánh `full/nang-cap-h-sua-kth-b`;
- C = thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC`, nhánh `full/nang-cap-h-sua-kth-c`.

## Chi tiết

### KTH-01 · nặng · H5: bấm Đồng ý xong, `batch_files` vẫn không chạy file nào

- **Chỗ lỗi:** `agent/src/tools/batchFiles.ts:29`, dòng `if (!plan.ok || base[USER_CONFIRMED_PARAM] === undefined) return plan;`.
- **Đường đi:**
  - Claude gọi lại lệnh sau thẻ với `confirm_handles`; schema dòng 109 chỉ có tham số này.
  - Việc đổi `confirm_handles` sang `user_confirmed` chỉ làm trong `bridge.call` → `session.paramsToSend` (`agent/src/index.ts:73`, `session.ts:255`). Bước này chạy khi từng lời gọi được gửi sang plugin, tức là **sau** dòng 29.
  - Trong `runBatch`, `base` không bao giờ có `user_confirmed`, nên lần gọi sau thẻ vẫn chỉ lập kế hoạch rồi trả kế hoạch đó.
  - Kết quả: cả 4 việc (soát, đếm block, áp chuẩn, in PDF) đều không chạy được qua khung chat. Claude dễ hỏi thẻ lại mãi.
- **Vì sao test cũ không bắt được:** `agent/test/nangcaph5.test.ts:52,63,67,77,86` truyền thẳng `user_confirmed` vào `runBatch`; SelfTest gọi thẳng `DrawingService.BatchFiles`.
- **Chứng minh:** test thử gọi hàm xử lý thật của `batchFilesTools(...)` với `confirm_handles: ['ABCDEF0123456789']`. Cầu nối giả làm như `index.ts`, thẻ đã duyệt có đúng mã đó. Lệnh gửi sang plugin chỉ có `["plan"]`, kết quả trả về là kế hoạch.
- **Đúng phải là:** sau Đồng ý thì gửi lần lượt `plan`, `run 0…n-1`, `finish`.
- **Gợi ý sửa:** dòng 29 xét `base.confirm_handles` (hoặc cả hai tham số). Nếu thẻ không khớp, plugin vẫn từ chối ở `run 0` ("Confirmation required"). Thêm test đi qua `handler` với `confirm_handles`.

### KTH-02 · nặng · H5: mỗi file chỉ có 30 giây, quá hạn dây chuyền

- **Chỗ lỗi:** `agent/src/ipc/rpc.ts:87` (`SLOW_TOOLS` không có `batch_files`), `:108`; `batchFiles.ts:22-24` (`stopsRun` không gồm `TIMEOUT`).
- **Đường đi:**
  - Mỗi lần `run` một file chỉ được `DEFAULT_TOOL_TIMEOUT_MS` = 30 s; riêng `plot_pdf` được 120 s.
  - Một lần `run` của việc `pdf` phải mở file rồi in mọi layout. Việc `standards` phải đọc, sao lưu rồi lưu file lớn.
  - Hết 30 s thì `runBatch` ghi dòng `error TIMEOUT` rồi gửi file kế tiếp. Lúc đó plugin vẫn đang làm file trước, nên file sau phải xếp hàng trong khi đồng hồ 30 s của nó đã chạy. Thế là quá hạn dây chuyền.
- **Chứng minh:** test thử cho `timeoutFor('batch_files', {action:'run', task:'pdf'})` = 30000, `plot_pdf` = 120000. Cho `run 0` trả TIMEOUT thì lệnh gửi đi là `plan, run0, run1, run2, finish`, và a.dwg ghi `status: error`.
- **Hậu quả:** báo cáo ghi "lỗi" cho file thật ra đã áp chuẩn hoặc đã in. Khách hoặc Claude dễ chạy lại.
- **Đúng phải là:** đủ thời gian cho một file. Nếu quá hạn thì dừng lượt và nói rõ file đó "có thể đã làm xong".
- **Gợi ý sửa:** thêm `batch_files` vào `SLOW_TOOLS` (hoặc cho riêng `run` 120–300 s). Gặp `TIMEOUT` thì dừng bằng `partialResult`.

### KTH-03 · vừa · H5: hai file trùng tên dùng chung một bản sao lưu

- **Chỗ lỗi:** `plugin/ClaudeCAD.Plugin/Core/BatchFiles.cs:198-208`, `Drawing/BatchFilesTools.cs:215` (`if (!File.Exists(backup))`).
- **Tái hiện:**
  - Gọi kèm cả `folder` và `files`, với các file nằm ngoài `folder`; đường dẫn tương đối khi đó chỉ còn tên file.
  - `X:\A\mat-bang.dwg` và `X:\B\mat-bang.dwg` cùng ra `<folder>\_sao-luu-claudecad\<giờ>\mat-bang.dwg`.
  - File B không được chép vì bản sao lưu "đã có", nhưng B vẫn bị ghi, và `row["backup"]` báo đã sao lưu (thật ra là bản của A).
  - Test thử `Two_listed_files_with_one_name_outside_the_folder_share_one_backup` (`Assert.Equal(ba, bb)`) qua.
- **Đúng phải là:** mỗi file có bản sao lưu riêng, hoặc không ghi khi không sao lưu được.
- **Gợi ý sửa:** file ngoài `folder` thì sao lưu cạnh chính nó, hoặc từ chối file ngoài `folder`. Bản sao lưu đã có mà là của file khác thì báo lỗi, không ghi.

### KTH-04 · vừa · H5: đếm block dừng ở 20 000 đối tượng mà không báo

- **Chỗ lỗi:** `BatchFilesTools.cs:191`, `od.ModelEntities(tr, G2Numbers.MaxScan, out _)` bỏ tổng số đi.
- **Hậu quả:** đối tượng thứ 20 001 trở đi không được đếm, kết quả và Excel không ghi chú gì. `block_schedule` trên một bản vẽ thì không có giới hạn này, việc `check` thì có ghi "(đọc x / y đối tượng)". Mặt bằng tổng thể lớn hay vượt 20 000 đối tượng.
- **Gợi ý sửa:** lấy `total` và ghi chú khi đọc chưa hết, hoặc bỏ giới hạn cho riêng việc đếm.

### KTH-05 · vừa · H5: tham số sai chỉ lộ ra sau khi khách duyệt thẻ

- **`groups` lạ:** `PlanOf` không kiểm. `BatchCheck` (`BatchFilesTools.cs:161`) báo lỗi ở từng file, nên khách duyệt thẻ xong thì cả N file đều "Lỗi".
- **`scale` sai** (`"abc"`, `"2:1"`): chỉ bị bắt trong `SetupStandards` (`Core/Standards2D.cs:111`), sau khi `BatchStandards` đã chép bản sao lưu (dòng 215). Mỗi file có một bản sao lưu thừa rồi mới "Lỗi".
- **Gợi ý sửa:** kiểm `groups` (`DrawingCheckLogic.Find`) và `scale` (`ScaleDenominator`) trong `PlanOf`, trước thẻ.

### KTH-06 · nhẹ · H5: `backup_folder` báo sai

- **Chỗ lỗi:** `BatchFilesTools.cs:112`, `:327`, `:310`.
- **Lỗi:** không có `folder` và các file nằm ở nhiều thư mục thì `backup_folder` (cả trong kế hoạch lẫn kết quả cuối) chỉ ghi thư mục của file đầu tiên, dù sao lưu thật nằm cạnh từng file. Excel tổng hợp cũng vào thư mục của file đầu.
- **Gợi ý sửa:** ghi "cạnh từng file" hoặc liệt kê các thư mục.

### KTH-07 · nhẹ · H5: `folder` không có thật vẫn bị tạo ra

- **Lỗi:** có `files` thì `PlanOf` (dòng 46) không bắt `folder` phải tồn tại. Áp chuẩn sẽ `Directory.CreateDirectory` tạo `<folder>\_sao-luu-claudecad\…` ở chỗ chưa có.
- **Gợi ý sửa:** đã đưa `folder` thì bắt phải tồn tại.

### KTH-08 · nhẹ · tài liệu: checklist H5 C4 lệch code

- **Lệch:** `docs/checklist-dot-h5.md` C4 ghi file đang mở có thay đổi chưa lưu thì "bỏ qua". `BatchFilesTools.cs:77` chỉ phát hiện được khi đó là tab đang xem; tab khác thì in luôn bản trên màn hình (bàn giao H5-test mục 3 đã ghi).
- **Gợi ý sửa:** sửa C4 (và khối tương ứng ở `checklist-tong-the.md`) cho khớp.

### KTH-09 · vừa · H4: chuẩn công ty học luôn kiểu "Standard"

- **Chỗ lỗi:** `Drawing/CompanyStandardTools.cs:180-213` (lúc học), `Core/CompanyStandard.cs:280-282` (`HasTextStyle` / `HasDimStyle` / `HasMLeaderStyle`).
- **Đường đi:** lúc học, mọi kiểu chữ, kiểu kích thước, kiểu ghi chú của file mẫu đều được nhận. File DWG nào cũng có kiểu chữ "Standard" (không xóa được), gần như luôn có kiểu kích thước "Standard" / "ISO-25" và kiểu ghi chú "Standard". Vì thế lúc soát, chữ / kích thước dùng các kiểu này luôn được coi là đúng chuẩn.
- **Hệ quả:** mục C1 của `docs/checklist-dot-h4.md` và `checklist-tong-the.md` (chữ kiểu Standard phải bị báo "kiểu ngoài bộ chuẩn") sẽ không đạt trên AutoCAD thật.
- **Đúng phải là:** kiểu mặc định của AutoCAD không tính là kiểu của công ty, trừ khi mẫu thật sự dùng nó.
- **Gợi ý sửa:** khi học, bỏ Standard / Annotative / ISO-25 nếu mẫu không có đối tượng nào dùng và không đặt làm kiểu hiện hành. Không thì sửa C1.

### KTH-10 · vừa · H4: áp chuẩn công ty khi hơn 50 layer khác chuẩn

- **Chỗ lỗi:**
  - `CompanyStandardTools.cs:408-413`: trả `kept_handles` không giới hạn (một bộ chuẩn có thể có tới 1 000 layer) và bảo gọi lại với `handles = kept_handles`;
  - `agent/src/tools/standards2d.ts`: `handles.max(50)`;
  - plugin: `ApprovedOverwrites` gọi `p.Handles("handles", 50)` → lỗi TooManyEntities.
- **Chứng minh:** `schemaOf('setup_standards').safeParse({company:'ABC', handles:<51 handle>})` cho `success = false`.
- **Đúng phải là:** mỗi lượt tối đa 50 handle và nói còn bao nhiêu, hoặc nâng giới hạn cho đường `company`.
- **Gợi ý sửa:** `kept.Take(50)` kèm `kept_more` và lời dặn gọi tiếp.

### KTH-11 · vừa · H4: `create_sheet company` báo ô khung tên "đã điền" dù không điền gì

- **Chỗ lỗi:** `Drawing/Standards2DTools.cs` (đoạn `objValues` / `AddAttributes` / `result["title_fields"]`), `BlockTools.cs:127-131`.
- **Đường đi:** kết quả vẫn trả `title_fields` gồm NGAY / TI_LE / KHO_GIAY và các ô `fields` theo tên ô của khung VN (vd `project` → CONG_TRINH). Khung tên công ty có tên ô khác, nên các giá trị này không được ghi vào đâu. Ô gõ sai tên trong `attributes` bị bỏ qua mà không báo (`insert_block` thì báo ô `unknown`).
- **Tái hiện (checklist D1):** "tên dự án Nhà phố Q7" → Claude truyền `fields.project` → kết quả ghi `CONG_TRINH: "Nhà phố Q7"` → Claude báo đã điền, nhưng khung tên công ty vẫn trống. Ngày, tỉ lệ, khổ giấy cũng không tự điền.
- **Đúng phải là:** với khung tên công ty, kết quả liệt kê đúng ô nào đã điền (giá trị thật), ô nào trống, tên ô không có.
- **Gợi ý sửa:** khi `cDef != null`, trả `filled` = các tag thật trong `atts`, `unknown_attributes` = khóa không khớp, bỏ `title_fields` kiểu VN.

### KTH-12 · nhẹ · H4: tờ in theo chuẩn công ty bị soát chính chuẩn đó báo sai layer

- **Chỗ lỗi:** `Standards2DTools.cs` (`br.LayerId = frameLayer` đặt khung tên công ty lên layer "KHUNG"), `Core/CompanyStandard.cs:272-278` (`HasLayer` chỉ miễn 0, Defpoints, layer xref, layer ảnh nền G5, layer hình chiếu).
- **Chứng minh:** `HasLayer(company, "KHUNG")` và `HasLayer(company, "KHUNG-VP")` đều `False`.
- **Hệ quả:** `create_sheet company` rồi `check_drawing company` (mặc định có layout) thì mỗi tờ bị báo một lỗi `nonstandard_layers` cho chính khung tên plugin vừa đặt.
- **Gợi ý sửa:** `HasLayer` miễn `F2Numbers.FrameLayer` / `ViewportLayer`, hoặc đặt khung tên lên đúng layer của nó trong file mẫu.

### KTH-13 · nhẹ · H4: tên công ty khác nhau ra cùng một thư mục

- **Chỗ lỗi:** `Core/CompanyStandard.cs:125-134` (`Slug`), `159-169` (`Load`).
- **Chứng minh:**
  - "株式会社" và "東京建築" đều ra thư mục "chuan";
  - "C++" và "C#" đều ra "c";
  - "Đ&Đ" và "D D" đều ra "d-d".
  - Học "株式会社" rồi `Load(root, "東京建築")` thì trả về bộ "株式会社" mà không báo.
- **Hệ quả:** `check_drawing` / `setup_standards` / `create_sheet` với `company: "東京建築"` dùng bộ chuẩn của công ty khác mà không nói. Lúc học thì có thẻ "thay", nên không mất dữ liệu.
- **Gợi ý sửa:** tên lưu khác tên gọi (ngoài hoa / thường / dấu) thì từ chối. Tên không có chữ Latin thì thêm mã băm vào `Slug`.

### KTH-14 · nhẹ · H4: mẫu khác đơn vị, cỡ cố định của kiểu chữ không được đổi

- **Chỗ lỗi:** `CompanyStandardTools.cs:370-373`, `:385`.
- **Lỗi:** khi mất bản chép mẫu (đường lui), kiểu chữ được dựng bằng `CreateTextStyle` với `height = t.Height` theo đơn vị của mẫu, không nhân `s.MmPerUnit / dmm`. Vd mẫu đơn vị mét có kiểu cao 0,0025 thì bản vẽ mm có kiểu cao 0,0025 mm. Lời ghi chú "text heights were converted" dễ làm hiểu nhầm là kiểu chữ cũng đã đổi.
- **Gợi ý sửa:** ở đường lui, đổi `t.Height` theo đơn vị; ghi chú nói rõ.

### KTH-15 · vừa · H3: polyline kín vẽ ngược chiều / bắt đầu ở góc khác bị báo "bớt + thêm"

- **Chỗ lỗi:** `Drawing/CompareTools.cs:180`: `Alt` (đọc ngược) chỉ gán khi `!pl.Closed`, và không có cách đọc theo điểm đầu khác.
- **Lệch tài liệu:** `docs/ban-giao/nang-cap-h3.md:88` và `:167` ghi polyline vẽ ngược chiều thì nhận đúng; checklist A4 cũng yêu cầu vậy.
- **Tái hiện** (khớp theo hình học, dung sai 1):
  - phòng (0,0)(4000,0)(4000,3000)(0,3000) so với cùng phòng vẽ theo chiều kim đồng hồ → `removed pline | added pline`;
  - cùng phòng bắt đầu ở (4000,0) → `removed | added`.
- **Đúng phải là:** không có khác biệt.
- **Gợi ý sửa:** polyline kín thì chuẩn hóa thứ tự đỉnh (đỉnh nhỏ nhất trước, chiều cố định) trước khi so.

### KTH-16 · vừa · H3: điểm neo của polyline lấy nhầm x0, y0, x1

- **Chỗ lỗi:** `Core/DrawingCompare.cs:212`, `:399-400`. `Anchor` lấy 3 số đầu của `Nums`, mà `Nums` của polyline là các cặp x, y, nên số thứ ba là x của đỉnh thứ hai.
- **Tái hiện:** kéo giãn phòng thành (0,0)(5000,0)(5000,3000)(0,3000). Điểm đầu không đổi mà ra `removed pline | added pline`.
- **Đúng phải là:** một dòng "changed" (đổi hình), như `nang-cap-h3.md:89`.
- **Gợi ý sửa:** với `pline`, điểm neo là `[x0, y0, elevation]`.

### KTH-17 · nhẹ–vừa · H3: góc so khít, không theo dung sai

- **Chỗ lỗi:** `CompareTools.cs:166,185,193`, `DrawingCompare.cs:215,235`. Góc đầu / cuối của cung, góc xoay của chữ và block nằm trong `Exact` và được so như chữ (làm tròn 6 số lẻ).
- **Tái hiện:** cung bán kính 5000, góc cuối lệch 0,0001 rad (điểm cuối lệch 0,5 mm, nhỏ hơn dung sai 1 mm) → `changed arc geometry`.
- **Đúng phải là:** không khác.
- **Gợi ý sửa:** so điểm đầu / cuối cung theo dung sai, hoặc so góc với ngưỡng = dung sai / bán kính.

### KTH-18 · vừa · H3: bỏ sót thay đổi của block động

- **Chỗ lỗi:** `CompareTools.cs:190-201`. Chỉ lấy tên block gốc, vị trí, góc xoay, tỉ lệ, thuộc tính; không đọc thuộc tính động, không so khung bao.
- **Hậu quả:** cửa động đổi rộng 800 → 900, đổi visibility hay lật chiều thì không báo, theo cả hai cách khớp.
- **Gợi ý sửa:** thêm `Props["dyn"]` = các cặp tên = giá trị của thuộc tính động.

### KTH-19 · vừa · H3: bỏ sót đổi chữ trong MLeader / Table, đổi Spline

- **Chỗ lỗi:** `CompareTools.cs:223-226`. MLeader (đợt F5 tạo ra), Table, Spline, Ellipse, Leader rơi vào nhánh `default`: chỉ so khung bao, màu, linetype, lineweight.
- **Hậu quả:** đổi chữ "C1" → "C2" trong MLeader hay ô bảng (khung bao giữ nguyên) thì không báo.
- **Gợi ý sửa:** MLeader so `MText.Contents`, Table gom chữ các ô, Spline so điểm điều khiển.

### KTH-20 · nhẹ · H3: không so tỉ lệ / góc hatch, kiểu kích thước

- **Hatch** (`CompareTools.cs:214-218`): chỉ so tên mẫu và diện tích, không so `PatternScale` / `PatternAngle`.
- **Kích thước:** không so `DimensionStyleName`.
- **Gợi ý sửa:** thêm vào `Props`.

### KTH-21 · nhẹ · H3: lời báo sai khi hơn 5 000 dòng khác

- **Chỗ lỗi:** `CompareTools.cs:104-106`. Quá `MaxClouds * 10` = 5 000 dòng thì không tính đám mây, nhưng vẫn báo "more than 500 clouds", dù 6 000 dòng cùng một chỗ chỉ gộp thành 1 đám mây (test thử đạt).
- **Thiếu trong bảng duyệt:** số 5 000 không có trong `dot-h-cho-duyet.md`.
- **Gợi ý sửa:** tách lời báo cho hai trường hợp; đưa 5 000 thành hằng số và ghi vào bảng duyệt.

### KTH-22 · nhẹ · H3: dòng "đổi" chỉ khoanh chỗ mới

- **Chỗ lỗi:** `DrawingCompare.cs:280-281`, lấy khung bao của bản mới.
- **Hậu quả:** đối tượng dời xa thì đám mây vàng chỉ khoanh chỗ mới, chỗ cũ không có dấu.
- **Gợi ý sửa:** dùng khung bao gộp của cả cũ và mới.

### KTH-23 · vừa · H2: FBX lại hỏng KT2-06 (tọa độ xa gốc bị làm tròn)

- **Chỗ lỗi:** `Drawing/ExportTools.cs:223-229` (`Unshift`), gọi ở dòng 190.
- **Đường đi:**
  - `StlShift` dời khối về gần gốc để STL float không làm tròn đỉnh (KT2-06).
  - `Unshift` cộng ngược về tọa độ thật rồi ép `(float)`; `FbxWriter.FromTriangles` nhận `float[]`.
  - FBX lưu số double, nhưng độ chính xác đã mất ở bước này.
- **Chứng minh:** test thử: `500000012.3 → 500000000` (lệch 12,3 mm; bước float ở 5e8 là 32 mm). Bản vẽ dùng tọa độ VN-2000 sẽ có đỉnh rơi vào lưới 32–64 mm.
- **Đúng phải là:** đỉnh giữ đúng tới 0,0001 mm, như bảng số ghi ("gộp đỉnh theo 0,0001 mm").
- **Gợi ý sửa:** `Unshift` / `FromTriangles` dùng `double[]`, cộng `shift` dạng double.

### KTH-24 · vừa · H1: cập nhật trong app lên H1 không mang skill mới tới

- **Chỗ lỗi:** `Core/UpdateLogic.cs:83-100` (`ApplyScript`), `installer/ClaudeCAD.iss:79`.
- **Đường đi:**
  - Script robocopy do plugin **đang chạy**, tức bản cũ trước H1, sinh ra lúc cập nhật. Script cũ không có dòng chép vào `skills-ban-moi`.
  - Bundle cập nhật cũng không có sẵn `Contents\skills-ban-moi`; chỉ bộ cài Inno tạo thư mục này.
  - Vì vậy lần cập nhật trong app đầu tiên lên H1 không có `skills-ban-moi`. `SkillSync.Sync` thoát ngay (`!Directory.Exists(stagedDir)`), skill chưa sửa vẫn là bản cũ, tới lần cập nhật sau mới chạy. Đường cài đè bằng Setup.exe thì đúng.
- **Lệch tài liệu:** `bo-cai/README.md` (dòng H1: "cài đè hoặc cập nhật trong app giờ thay các skill…"), `docs/ban-giao/nang-cap-h1.md` §1. Chỉ checklist A.6 ghi đúng ("lên bản sau H1").
- **Gợi ý sửa:** lúc build, đưa sẵn `Contents\skills-ban-moi` vào bundle (robocopy đầu của script cũ sẽ tự chép), hoặc ghi rõ hạn chế này trong tài liệu.

### KTH-25 · nhẹ · H1: skill khách đã xóa tự quay lại mỗi lần mở khung chat

- **Chỗ lỗi:** `Core/SkillSync.cs`, nhánh `if (!File.Exists(dst))` trong `Sync`.
- **Đường đi:** `skills-ban-moi` nằm lại vĩnh viễn và `Sync` chạy mỗi lần mở khung chat. File thiếu thì luôn được chép lại, kể cả khi `.ban-da-cai.json` đã ghi là từng cài rồi khách xóa. Skill quay lại ở trạng thái bật.
- **Chứng minh:** test thử `KT_deleted_skill_comes_back_at_every_start` → `added after delete: ve-mep/SKILL.md`.
- **Trước H1:** skill đã xóa chỉ quay lại khi cài đè.
- **Gợi ý sửa:** `rel` đã có trong `m.Installed` mà file không còn thì coi là khách xóa, chỉ chép lại khi có bản mới (hash khác). Hoặc ghi vào tài liệu: muốn bỏ skill thì tắt, đừng xóa.

### KTH-26 · nhẹ · tài liệu: checklist H1 ghi sai thư mục cài

- **Lệch:** `docs/checklist-dot-h1.md` dòng 5–6 ghi `C:\Program Files\ClaudeCAD\Contents\skills`. Bộ cài thật cài theo từng người dùng: `{userappdata}\Autodesk\ApplicationPlugins\ClaudeCAD.bundle` (`installer/ClaudeCAD.iss:42`, `PrivilegesRequired=lowest`).
- **Gợi ý sửa:** đổi thành `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills`.

### KTH-27 · nhẹ · chéo (prompt): prompt mục 7 mâu thuẫn với lệnh làm nhiều file

- **Chỗ lỗi:** `agent/src/agent/systemPrompt.ts:45`.
- **Mâu thuẫn:**
  - mục 7 vẫn ghi "Never touch files other than the open drawing.";
  - ngay cạnh đó (dòng 46–47) và trong `TWO_D_HINTS` lại hướng dẫn `export_drawing`, `batch_files` (áp chuẩn ghi vào file khác) và `company_standard` (đọc file mẫu).
- **Hậu quả:** Claude có thể từ chối "áp chuẩn cả thư mục", hoặc lưỡng lự khi khách yêu cầu.
- **Gợi ý sửa:** sửa thành "Never touch other files except through export_drawing / compare_drawings / company_standard / batch_files after the card".

## Đã kiểm, không thấy lỗi

### Sửa CI

- `.github/workflows/build.yml`: mọi bước nhiều lệnh (`npm ci` / `npm test` / `npm run build`; `choco` / `ISCC`; vòng build 3 target) đều kiểm `$LASTEXITCODE` sau từng lệnh.
- Bước test lõi và test UI là một lệnh, nên lỗi làm đỏ job.
- `npm test` = `build:test && node --test` trả mã lỗi khi có test hỏng.
- `agent/test/ci-workflow.test.ts` khóa đúng các dòng này.

### H1

- Mã băm skill khớp giữa `SkillSync.Hash` và `installer/skill-hashes.sh` với file có BOM + CRLF.
- Cài đè nhiều lần không sinh `-2`, `-3` mãi: chỉ sang tên kế tiếp khi khách đã sửa `.ban-moi`; giới hạn 99 tên.
- Bộ cài / updater không xóa skill khách tự thêm (`/PURGE` bỏ qua `skills`; `[InstallDelete]` chỉ đụng `skills-ban-moi`).
- `SKILL.ban-moi.md` không bị nạp làm skill. Dòng báo trong chat hiện một lần cho mỗi bản mới.
- KFG-43: băm thêm z, so từng số theo dung sai; kết quả trùng không đổi.
- PDF:
  - số trang 0 / âm / quá số trang bị chặn;
  - file đổi đuôi bị `CheckContent` chặn;
  - PDF có mật khẩu hoặc hỏng thì lui về ảnh chụp AutoCAD, kèm lý do;
  - file tạm được dọn; có `DecodePixelWidth`.

### H2

- Đọc lại FBX nhị phân theo spec: đầu file, `EndOffset`, mảng, tên có dấu UTF-8, `PolygonVertexIndex`, pháp tuyến, kết nối, `UnitScaleFactor` đều khớp.
- `VersionOf` và 6 đời `DwgVersion` hợp lệ.
- `OutputPath` từ chối sai đuôi, tên rỗng, trùng bản vẽ đang mở.
- Bản vẽ đang mở không bị `SaveAs` (xuất từ bản sao `Wblock`); file tạm được xóa.
- FBX bị chặn ở gói 2D và ở Full chế độ 2D (cả `FBX`, `.fbx`); trong batch / macro cũng bị chặn; schema gói 2D không có `fbx`.
- Thẻ ghi đè F5 chạy trước khi ghi.

### H3

- Đọc file kia bằng `OpenForReadAndAllShare` + `CloseInput(true)`, DXF qua `DxfIn`; không có `ForWrite` hay `Save`.
- Lời báo đúng cho file đời mới hơn, file khóa, `.bak` / `.dwl`, file rỗng, file quá 200 MB, so với chính bản đang mở.
- Đám mây vẽ trong WCS nên đúng chỗ khi UCS xoay.
- Layer khóa / đóng băng bị từ chối trước thẻ; quá 50 đám mây thì hỏi thẻ.
- Không có `mark` thì chạy được ở "Chỉ xem", có `mark` thì là lệnh ghi.
- Đổi đơn vị mm ↔ m. Nhiều đối tượng giống nhau ghép một đối một. Polyline hở ngược chiều nhận đúng.
- Lọc theo gói / chế độ. Excel ghi chữ dạng inline, không thành công thức.

### H4

- Tên thư mục: ký tự cấm, `..`, tên thiết bị Windows, dấu tiếng Việt, hoa / thường đều an toàn.
- `standard.json` hỏng thì báo rõ, `list` ghi `damaged`, vẫn xóa được.
- Ghi đè và xóa đều qua thẻ; ghi qua `.tmp`.
- `check_drawing company` không lẫn chuẩn VN. `fix_drawing company` từ chối nhóm `nonstandard_styles`.
- Tham số `company` có trong schema TS của `setup_standards`, `create_sheet`, `check_drawing`, `fix_drawing`, `batch_files`.
- Gói / chế độ / nạp gọn / "Chỉ xem" theo đúng quyết định đã ghi.
- Chỉ ghi vào `%LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty\<thư mục>`.

### H5

- Thứ tự ghi: kiểm đầu file (TTH-1), sao lưu, đọc, ghi file tạm cùng thư mục, `File.Replace`, `finally` xóa file tạm. Ghi hỏng giữa chừng thì file gốc còn nguyên, đời DWG giữ nguyên.
- Hai lượt khác giờ có hai mốc sao lưu khác nhau; thư mục sao lưu và file tạm không bị quét lại.
- File bị bỏ qua: chỉ đọc, có `.dwl` / `.dwl2`, đang mở, rỗng, quá 200 MB; `.dwt` không được nhận; DXF chỉ dùng khi soát / đếm.
- Mã thẻ gồm việc, thông số và danh sách file; thêm / bớt file làm mã đổi, bị chặn ở `run 0` hoặc giữa lượt (vẫn kể các file đã làm).
- Gói / chế độ kiểm cả `batch_files` lẫn lệnh con. "Chỉ xem" chặn áp chuẩn. Không vào được `batch_execute`, không ghi vào macro.
- Luật đường dẫn kiểm cả `folder` lẫn từng file, và kiểm lại khi mở. `WorkingDatabase` được trả lại trong `finally`.
- Excel không có công thức từ chữ khách.

### Chéo

- Nhãn lệnh tiếng Việt có đủ 5 lệnh mới (`ToolLabels.cs`).
- `F5_CARD_TOOLS` có `export_drawing`, `company_standard`, `batch_files`, `compare_drawings`.
- Nạp gọn: các lệnh mới không nạp sẵn, nạp qua ToolSearch.
- Prompt mục 1 coi tên / chữ / thuộc tính đọc từ bản vẽ là dữ liệu.
- SelfTest H2 / H3 / H4 có đủ, gọi đúng hàm.
- `bo-cai/README.md` khớp code, trừ KTH-24.

### Bảng duyệt `docs/dot-h-cho-duyet.md`

- Các số H1–H5 khớp code (`H3Numbers`, `H4Numbers`, `H5Numbers`, các số H1 / H2).
- Thiếu một số khách gặp: ngưỡng 5 000 dòng của H3 (KTH-21).

## Nghi vấn chưa chứng minh

### H5

- **Macro sửa tay:** file macro có `batch_files` `action:"run"` thì `RequireCard` cho qua khi đang phát macro (`ConfirmF5.cs:17`), nên áp chuẩn / in PDF hàng loạt có thể chạy không có thẻ. Lúc phát lại macro không lọc tên lệnh (`ChatController.cs` khoảng dòng 1523).
- **Thư mục quá rộng:** `folder` = gốc ổ cùng `recursive` có thể duyệt rất lâu ngay ở bước kế hoạch, dính giới hạn 30 s.
- **Thư mục con dạng junction trỏ ra ổ mạng:** lúc duyệt không kiểm luật đường dẫn. Áp chuẩn và in PDF không qua `OtherDrawing.Open`.
- **Cùng một file viết hai kiểu đường dẫn** (ổ ánh xạ và UNC): `SameFile` so chuỗi nên không nhận ra.
- **Tên block** đi vào `note` / `counts` gửi cho Claude và có thể chứa câu lệnh giả. Rủi ro thấp, prompt mục 1 đã che.

### Chéo

- **Thời gian chờ 30 s** cũng áp cho `export_drawing` (`Wblock` + `SaveAs` bản vẽ lớn, FBX 2 000 khối), `compare_drawings` và `company_standard learn` (mở file tới 200 MB). Các lệnh này không nằm trong `SLOW_TOOLS` / `BUILD_TOOLS`, chưa đo trên AutoCAD thật.
- **`compare.ts`** khai `readOnlyHint: true` dù có `mark` (vẽ đám mây). Nếu SDK cho lệnh chỉ đọc chạy song song thì lệnh này có thể chạy cùng lúc với lệnh ghi khác.

### H3

- Tên linetype / kiểu chữ / block so có phân biệt hoa thường, layer thì không.
- Gói 3D chỉ coi `Solid3d` là 3D: Surface / Region / Mesh bị bỏ ở gói 3D nhưng được so ở gói 2D.
- Số trong "what" theo đơn vị bản vẽ, trong khi `tolerance_mm` theo mm.
- Ghi chú "unsaved changes" của `OtherDrawing.Info()` không bao giờ hiện (đã ghi ở `nang-cap-h3-test.md`).

### H4

- **Khung nhìn của layout mẫu:** `ReadTemplate` coi khung nhìn đầu tiên là khung nhìn của layout và bỏ qua (G2 đã sửa chỗ tương tự bằng `GetViewports()`).
- **Chọn khung tên:** lấy block có thuộc tính đầu tiên (có thể nhận nhầm dấu sửa đổi); khung tên là xref hay nằm lồng thì không nhận ra; giá trị đã điền sẵn trong khung tên mẫu không mang sang tờ mới.
- **File mẫu DXF:** lưu bản chép bằng `SaveAs("mau.dwg.tmp")`, cần thử đuôi `.tmp` trên AutoCAD thật.
- **Checklist F1 "học từ thư mục mạng":** gõ đường dẫn `\\server\…` sẽ bị luật đường dẫn từ chối, trừ khi bản vẽ đang mở cùng chỗ.
- **`create_drawing_set`** chưa có `company`. "Ra bộ hồ sơ theo chuẩn ABC" vẫn ra khung tên VN. Tài liệu không hứa có, nhưng chủ dự án nên xem.

### H1 / H2

- **Trang PDF rất cao:** chỉ giới hạn bề rộng 6 000 px, không giới hạn bề cao.
- **Máy chuyển từ ClaudeCAD gốc:** mã băm của skill phát hành sau ngày tách không có trong `KnownShipped`, nên chat báo nhầm "khách đã sửa".
- **`Database.Wblock()` cả bản vẽ** có thể bỏ layer / block / kiểu không dùng.
- **Đời "2016":** plugin hiểu là 2013, nhưng schema zod chỉ nhận đúng 6 năm.
- **Khối 3D trên layer khóa** khi xuất FBX (giống `export_stl`).

## Chưa kiểm được (cần AutoCAD / Windows / Claude / Blender / 3ds Max thật)

- **H5:**
  - `DocumentCollectionExtension.Open` / `CloseAndDiscard`, đổi bản vẽ đang xem khi in PDF;
  - `File.Replace` trên ổ mạng;
  - `SaveAs` bản vẽ phụ (ảnh xem trước, xref tương đối);
  - bấm Dừng thì kết quả có tới được Claude không.
- **H4:**
  - `WblockCloneObjects` khung tên / layer / kiểu (cả từ điển kiểu ghi chú);
  - vị trí khung tên và khung nhìn trên tờ;
  - `.dwt`;
  - thời gian với bộ chuẩn hàng trăm layer.
- **H3:**
  - bấm U có xóa hết đám mây không;
  - `GeometricExtents` của chữ trong bản vẽ đọc kèm;
  - file kia đang mở ở AutoCAD khác hoặc trên thư mục mạng;
  - đường dẫn có dấu tiếng Việt;
  - DXF hỏng hoặc đời mới;
  - SelfTest H3.
- **H2:**
  - `Wblock` có giữ đủ layout không;
  - `SaveAs` / `DxfOut` đời 2000–2018, mở bằng AutoCAD đời cũ;
  - FBX mở trong Blender / 3ds Max.
- **H1:**
  - Windows.Data.Pdf qua PowerShell; trang xoay hoặc CropBox khác MediaBox;
  - robocopy `/XD` / `/MIR`, bộ cài Inno thật.
- **Sửa CI:** không chạy CI theo yêu cầu; chỉ đọc workflow.
- **Claude thật:** cách Claude dùng thẻ / gọi lại lệnh (KTH-01 đã chứng minh bằng hàm xử lý thật và cầu nối giả).

## Thời gian, chi phí

- Thời gian: khoảng 55 phút. Phần tốn nhất là 4 tác vụ con rà song song (mỗi tác vụ khoảng 35 phút, khoảng 180–220 nghìn token mỗi tác vụ) và cài .NET SDK.
- Chi phí: khoảng $13 lúc viết báo cáo (`usage.cost_usd` của `get_session`).

## Kết quả sửa và soát lại

### Lượt sửa 1 (26/11 18h10 → 18h33)

- Phần H5 (thread đợt F2): `full/nang-cap-h-sua-kth-c` @ `5ce5296`, bàn giao `nang-cap-h-sua-kth-c.md`.
- Phần H1 / H2 / H3 (thread Test & sửa F1): `full/nang-cap-h-sua-kth-b` @ `bf21eee`, bàn giao `nang-cap-h-sua-kth-b.md`.
- Phần H4 + prompt (thread đợt F1): `b2e7549`, bàn giao `nang-cap-h-sua-kth-a.md`.
- Gộp: `full/nang-cap-h-tong-the` @ `b7e408f` (code `0275e38`), CI 45810078065 xanh, `# pass 441 / # fail 0`, lõi 1605.
- Sửa thêm 5 mục nghi vấn, đều là lỗi thật:
  - macro chạy `batch_files` không thẻ;
  - thời gian chờ 30 s của `export_drawing` / `compare_drawings` / `company_standard learn`;
  - `compare.ts` khai chỉ đọc;
  - khung nhìn layout mẫu;
  - chọn khung tên.

### Soát lại lượt 1 (thread kiểm tra độc lập, 18h55, ~$3)

Chạy lại: lõi 1605 / 1605, trợ lý 441 / 441.

| Mã | Kết quả |
| --- | --- |
| KTH-01, 03 → 14, 16 → 27 | Sửa đúng |
| KTH-02 | Đúng phần chính (chờ 2 phút khi soát / đếm, 5 phút khi áp chuẩn / in PDF). Còn lại (nhẹ): quá hạn vẫn đi tiếp, chưa nói "có thể đã làm xong" → giao thread đợt F2 sửa nốt |
| KTH-15 | Sửa đúng ca đã báo, nhưng sinh KTH-28 |
| 5 nghi vấn | Sửa đúng |
| Lưu ý nhỏ KTH-11 | Ô khung tên còn giá trị mặc định của mẫu vẫn tính là "đã điền" (không sửa, ghi để chủ dự án biết) |

**KTH-28 · vừa · H3 (mới, sinh từ bản sửa KTH-15):**
- **Lỗi:** polyline kín có hai đỉnh gần bằng nhau về x vẫn bị báo "bớt + thêm".
- **Chỗ lỗi:** `Core/DrawingCompare.cs`, `CanonicalLoop` chọn đỉnh đầu bằng phép so khít, không theo dung sai.
- **Chứng minh:** `(0,0)(4000,0)(4000,3000)(0.3,3000)` so với `(0.3,0)(4000,0)(4000,3000)(0,3000)` cho kết quả `removed | added`.
- **Thread sửa:** Test & sửa F1, nhánh `kth-b`.

### Lượt sửa 2

*(Luồng chính ghi khi KTH-28 và phần còn lại của KTH-02 sửa xong, gộp, CI và soát lại.)*
