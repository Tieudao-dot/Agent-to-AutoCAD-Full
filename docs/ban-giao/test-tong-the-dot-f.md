# Bàn giao: Test tổng thể đợt F (F1 → F4)

Nhánh `full/nang-cap-f-tong-the`, tách từ `full/nang-cap-f4-test` @ `1cdf8d4` (code `9278808`, đủ F1 → F4), lấy 4 file
tài liệu của luồng chính (`claude/inspiring-cray-58xsmt` @ `f5a0430`: `CLAUDE.md`, `docs/ke-hoach-nang-cap-lenh.md`,
`docs/tong-quan-du-an.md`, `docs/dot-f-cho-duyet.md`). Người làm: thread đã viết F2 và rà F3. Thread này giữ nhánh tích hợp
đợt F; lỗi của kiểm tra độc lập chuyển về đây.

Chưa thử trên AutoCAD / Claude / Excel thật: kiểm bằng đọc code chéo, test lõi / trợ lý, build 3 target, CI (cuối file).

## Đã rà (chiều ngang)

| Chỗ hay lệch | Kết quả |
| --- | --- |
| UCS (F1) × lệnh mới F2 / F3 / F4, bản vẽ mét | Lớp UCS đúng ý từng lệnh: `insert_library_block` Create, `create_sheet` Ref (tâm khung nhìn theo UCS, khung nhìn xoay theo UCS), `setup_standards` / `layer_state` / `set_material` / `named_view` giữ WCS; Excel là tham số của 4 bảng (đi theo lớp UCS của bảng). Bản vẽ mét: F2 chia theo INSUNITS (style `-M`, block, khung nhìn), mái F3 như đợt E. Có test |
| Gói × chế độ × nạp gọn (F4) | 6 lệnh mới đúng nhóm gói (F2: 2 lệnh 2D + 2 lệnh chung; F3: 2 lệnh 3D); mỗi chế độ máy Full nhận đúng lệnh của gói đó (test); 25 lệnh nạp sẵn ⊂ gói, lệnh mới F2 / F3 không rơi khỏi gói (test có sẵn của F4 + test mới); mọi lệnh có nhãn tiếng Việt (test mới) |
| "Chỉ xem", thẻ xác nhận, luật ghi file | Chỉ xem: lệnh đọc (danh sách thư viện, danh sách trạng thái layer, danh sách vật liệu, ảnh PNG, Excel, STL, PDF) chạy; lệnh ghi bị chặn. Thẻ: F2 ghi đè style / trạng thái layer bắt buộc thẻ; F3 gán vật liệu theo luật > 20; góc nhìn và file trùng tên: Claude hỏi trong chat. File: STL / Excel / PNG không ghi đè file có sẵn khi chưa được đồng ý; **PDF ghi đè không hỏi** (có từ trước, ghi để chủ dự án xem). Sửa: PNG nhận `overwrite` như STL / Excel |
| Macro × lệnh mới | Không lệnh nào tạo file khi chạy lại macro (PDF, STL, PNG, Excel đều là lệnh đọc hoặc bị bỏ khi ghi); macro ghi đè style / trạng thái layer chạy lại thì hỏi thẻ lại (`user_confirmed` không được ghi); `set_material` ghi lại được; `create_sheet` chạy lại trên cùng bản vẽ báo tên layout đã có (không sửa layout cũ); F1 nhớ UCS lúc ghi áp dụng cho mọi lệnh mới |
| System prompt, skill | Không nhắc lệnh ngoài gói (test `crossplan` có sẵn); dòng gợi ý F2 ở phần 2D, F3 ở phần 3D, F4 hai nơi; số 120 khớp code, mô tả lệnh, skill; **checklist tổng phần đợt E còn ghi 150 → sửa** |
| `SelfTest` | Thứ tự F3 → F2 → F4; **bước F4 cần block cửa do bước F2 chèn → sửa**: bước F4 tự tạo block `SELFTEST-F4` |
| `bo-cai/README.md` | Đủ dòng F1, F2, F3, F4 (thêm 1 câu Test tổng thể vào dòng F4) |

## Lỗi tìm được và đã sửa

| # | Mô tả | Mức | Sửa |
| --- | --- | --- | --- |
| TF-1 | Bước tự kiểm F4 (xuất Excel) kiểm block `CUA-DI-900` do bước F2 chèn: bước F2 hỏng (hoặc đổi) thì bước F4 báo sai | nhẹ | Bước F4 tự tạo block `SELFTEST-F4` rồi kiểm; test `TongTheDotFTests` |
| TF-2 | `docs/checklist-tong-the.md` (bảng số đợt E) còn ghi bản thang và bản mái dốc **150 mm**, trái với code / skill (120 từ F3) | nhẹ (tài liệu) | Sửa thành 120, ghi rõ F3 đổi; test |
| TF-3 | Xuất ảnh PNG chỉ nhận `replace` để ghi đè, các lệnh xuất khác dùng `overwrite` (trong `batch_execute` Claude hay dùng `overwrite` → bị từ chối "file đã có") | nhẹ | Nhận cả hai; test |

## Token mở chat (ước tính)

Cùng cách đo các đợt trước: lệnh của gói (`plan_tools` theo `Core/Plans.cs`) + 5 lệnh thẻ, tên + mô tả + schema JSON
(`z.toJSONSchema`) + system prompt theo gói / chế độ, ~3,5 ký tự / token. Nạp gọn: lệnh nạp sẵn tính đủ, lệnh hoãn tính
bằng tên `mcp__claudecad__<tên>` + 2 ký tự, định nghĩa ToolSearch ước 2 000 ký tự (như `nang-cap-f4.md`). **Số thật do
Claude Code quyết, cần đo trên máy thật** (mức dùng của lượt đầu). Trước đợt F = `full/nang-cap-tong-the` @ `012ed29`.

| Gói / chế độ | Số lệnh (nạp sẵn) | Trước đợt F | Sau, nạp gọn (mặc định) | Sau, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 104 (19) | 135 669 ký tự ≈ 38,8k | 46 944 ≈ 13,4k (**−65 %**) | 142 652 ≈ 40,8k (+5 %) |
| Gói 3D | 92 (22) | 139 339 ≈ 39,8k | 65 664 ≈ 18,8k (**−53 %**) | 147 126 ≈ 42,0k (+6 %) |
| Full · Cả hai | 137 (25) | 202 011 ≈ 57,7k | 72 269 ≈ 20,6k (**−64 %**) | 213 642 ≈ 61,0k (+6 %) |
| Full · chế độ 2D | 104 (19) | (chưa có chế độ) | 47 167 ≈ 13,5k | 142 875 ≈ 40,8k |
| Full · chế độ 3D | 92 (22) | (chưa có chế độ) | 65 887 ≈ 18,8k | 147 349 ≈ 42,1k |

Số lệnh không kể 5 lệnh thẻ (luôn nạp đủ). Cột "nạp gọn" khớp từng ký tự bảng của `nang-cap-f4.md`.

## Checklist thử tay

Khối F1 cũ và `checklist-dot-f2.md`, `-f3.md`, `-f4.md` gộp thành khối **"Đợt F"** cuối `docs/checklist-tong-the.md`:
F0 tự kiểm → F1 mở chat nhanh → F2 nút chế độ → F3 style / trang in / PDF → F4 block thư viện / trạng thái layer → F5 Excel
→ F6 3D (mái, số mặc định, vật liệu, ảnh, góc nhìn) → F7 UCS → F8 macro → F9 công tắc tắt nạp gọn → F10 cần máy khác.
Mỗi mục có cột "Cần" (Claude / Excel / Công tắc / Máy khác), làm gì, thấy gì là đạt, thời gian từng phần (~2 giờ cả khối),
mã mục cũ để tra. Ba file riêng có dòng trỏ sang checklist tổng.

## Soát `docs/dot-f-cho-duyet.md`

Số và quyết định đã ghi khớp code. Đã sửa / thêm:
- F2: thêm dòng **màu layer mới** (KHUNG 7, KHUNG-VP 8, A-DOOR 4, A-WIND 5, A-FIXT 6, A-FURN 8, L-PLNT 3) — có trong code
  (`F2Numbers`), chưa có trong bảng.
- F3: ghi **mã màu RGB** 8 vật liệu (bảng chỉ ghi "mỗi loại một màu"); ảnh: thêm cỡ chọn được **384–1568 px** và luật ghi
  đè file.
- Thêm mục "Test tổng thể đợt F": 3 lỗi đã sửa, 2 điểm để chủ dự án xem (ghi đè có hai kiểu: thẻ cho F2, hỏi trong chat
  cho góc nhìn / file; PDF ghi đè không hỏi), bảng token, chỗ checklist tổng.

Không đổi số nào trong code.

## Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Hai kiểu ghi đè (thẻ cho style / trạng thái layer; hỏi trong chat cho góc nhìn, file STL / Excel / PNG) | Giữ như các đợt đã chọn, ghi vào `dot-f-cho-duyet.md` để chủ dự án quyết một lần | Đưa `named_view` (replace / delete) vào nhóm thẻ như F2 |
| PDF trùng tên | Giữ cách cũ (ghi đè không hỏi) | Thêm `overwrite` cho `plot_pdf` như STL |
| `replace` / `overwrite` của ảnh PNG | Plugin nhận cả hai; mô tả lệnh giữ `replace` (không tăng token) | — |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1132** (1126 + 6 `TongTheDotFTests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **355** (không đổi; nhớ `rm -rf dist-test` nếu thư mục build còn file test của
  nhánh khác).
- Build 3 target (Linux, .NET SDK 10, xóa obj / bin giữa các lần): 0 lỗi, 0 cảnh báo.

## Chưa kiểm được

- Mọi thứ cần AutoCAD thật (bước tự kiểm, hình vẽ, PDF / PNG / Excel thật), Claude thật (tìm lệnh đã hoãn, token thật,
  thời gian câu trả lời đầu), Excel thật, máy gói 2D / 3D thật. Thử tay: khối "Đợt F" của checklist tổng.

## CI

- Run **#49** (`45614706040`, `release=false`) trên `7aa74c0` (code cuối): **xanh** (build 3 target, test lõi, trợ lý, UI test WPF). 1 lượt CI.
- Commit sau CI chỉ thêm mục này (tài liệu).

## Sau kiểm tra độc lập (KTF, 25/11/2026)

Kiểm tra độc lập (`session_01D56HmHz4EXXJceBgkmjcap`) soát `afdaa68`, ghi 8 lỗi. Phần F1 (UCS) và F4 (Excel, khởi động sẵn)
— KTF-02 → 06 — do thread F1 sửa trên `full/nang-cap-f-sua-ktf`, gộp vào đây khi có commit. Phần F3 / F2 / tài liệu sửa
trực tiếp trên nhánh này:

| Mã | Mô tả | Sửa | Test |
| --- | --- | --- | --- |
| KTF-01 (nặng, F3) | Mái 2 dốc / 4 dốc nhà L / T / U có đường bao lệch nhỏ (vẽ tay, xoay rồi làm tròn ~1 mm): phép thử góc vuông nhận lệch tới ~20 mm / 10 m nhưng lưới hình chữ nhật chỉ gộp tọa độ trong 0,5 mm → mảnh gần trùng, cánh "rộng hơn chút" có nóc chạy ngang mái chính (T lệch 1 mm: 5 mảnh, đỉnh 6603 thay 4293), mảnh mỏng < 1 mm ở mái 4 dốc | Đường bao được **nắn về góc vuông** trước khi chia (`RoofLogic.SquareUp`): khung = hướng trung bình theo chiều dài của mọi cạnh (gấp về 90°), mỗi cạnh nằm đúng ngang / dọc ở trung bình hai đầu, mỗi góc lấy x của cạnh dọc và y của cạnh ngang. Góc chỉ dời cỡ sai số vẽ; đường bao đã vuông giữ nguyên | `KiemTraDocLapDotFTests`: T lệch 1 mm = 2 mảnh, đỉnh 4293; T 37° / 133°, U 13° / 133° / 270°, L cánh hẹp 37°, T 90° xoay + làm tròn: cùng số mảnh và đỉnh như bản vuông, không mảnh nào hẹp < 500 mm, cả 2 dốc và 4 dốc. Bỏ dòng nắn thì 4 test đỏ (đã thử) |
| KTF-07 (nhẹ, tài liệu F2) | `dot-f-cho-duyet.md`, checklist tổng F3-5, `checklist-dot-f2.md` ghi bản vẽ mét hiện "4.00"; code bỏ số 0 thừa (DIMZIN 8) nên khách thấy "4" | Sửa tài liệu ("4", "4.5"); thêm điểm để chủ dự án xem: muốn "4.00" thì đổi cài đặt này. Không đổi code | test tài liệu |
| KTF-08 (nhẹ, skill F2) | `skills/ve-mep/SKILL.md` có hai bước "4." | Đánh số lại 1 → 5 | test số bước |

Nghi vấn đã kiểm:

| Nghi vấn | Kết quả |
| --- | --- |
| F3: tên vật liệu mẫu đè vật liệu của khách ("Glass", "Metal", "tile", "granite" là tên phụ của vật liệu mẫu) | **Đúng** (khách có "Glass" mà nhờ gán "Glass" thì nhận vật liệu mẫu "Kính"). Sửa: vật liệu đúng tên đã có trong bản vẽ được dùng nguyên. Đổi màu vật liệu khách đã có không qua thẻ: thread F5 làm |
| F3: ảnh PNG từ góc nhìn đã lưu bỏ zoom / độ xoay / phối cảnh | **Đúng**, theo thiết kế (bộ chụp off-screen chỉ nhận hướng nhìn và khung). Không sửa (cần viết lại bộ chụp): mô tả lệnh nói rõ "giữ hướng, không giữ độ phóng to; khung cả công trình hoặc handles"; checklist F6-11 ghi rõ |
| F2: `insert_library_block` không đặt ByLayer cho block tham chiếu | **Đúng** (Properties hiện màu hiện hành). Sửa: block tham chiếu ByLayer như khung / khung tên (test F2-test cập nhật 5 → 6 chỗ, mỗi chỗ vẫn ByLayer) |
| F2: tên block không ghi đơn vị, block cùng tên khác cỡ bị dùng lại sai cỡ | **Đúng.** Sửa: block cùng tên mà cỡ lệch > 2 % thì không dùng lại, tạo block thư viện tên `<tên>-TV` (vẫn `CUA-DI-…` / `CUA-SO-…` cho dựng 3D) |
| F2: hoàn tác sau khi xóa / ghi đè trạng thái layer | Chưa kiểm được ngoài AutoCAD: thêm mục thử tay F4-8 |

Số kỹ thuật bên trong (không cần duyệt): nắn góc không có ngưỡng riêng (dùng ngưỡng góc vuông `StairLogic.SquareTol` = 2e-3
sẵn có); lệch cỡ block cho dùng lại `LibraryBlocks.SizeTol` = 2 %.

Quyết định tự chọn: tên block thư viện khi trùng tên khác cỡ là `<tên>-TV` (khách thấy tên này trong danh sách block; ghi
vào `dot-f-cho-duyet.md`). Thử tay thêm: F4-8, F4-9, F6-12 (checklist tổng, khối "Đợt F").

### KTF-01 lần 2 (sau soát lại của kiểm tra độc lập trên `f037b79`)

Soát lại: 7/8 mã đúng; KTF-01 còn sai — `SquareUp` chỉ nắn góc, không gộp hai cạnh song song gần trùng ở hai bên chỗ lõm:
T 2 dốc nhiễu ±2 mm 13/30 lần ra nóc 6603 (đúng 4293); L 12 × 12 m có bậc 2 mm ở cạnh trong ra 5 mảnh, nóc 6603 (đúng 4871);
T 2 dốc ở 30° ra 3 mảnh. Gốc lỗi thứ hai: phần cánh bị cắt ở nóc mái chính luôn nhận nóc **ngang** mái chính, nên khi chân T
nhỉnh hơn thanh ngang vài mm (thành mái chính), hai nửa thanh ngang có nóc chạy ngang chiều dài 6 m của chúng → nóc 6603.

Sửa (`Core/RoofRightAngled.cs`):
- `SquareUp` gộp các mức x (cạnh đứng) / y (cạnh ngang) cách nhau ≤ max(`SnapMinMm` = 5 mm, `SquareTol` × cỡ nhà) thành một
  (trung bình), rồi bỏ cạnh dài 0 / đỉnh thẳng hàng (`Clean`); kết quả phải còn là đường bao góc vuông.
- Phần cánh bị cắt lấy nóc theo **cạnh dài của chính nó** (như cánh không bị cắt), không còn bị ép nóc ngang mái chính.
- Bề rộng chênh nhau < `WidthTieMm` (50 mm) coi là bằng nhau khi chọn mái chính (rồi chọn hình lớn hơn) và khi xét mái nào
  cao hơn (`EndInHigherRidges`), nên nhiễu vài mm không đổi mái chính.

Bậc lõm / lồi nhỏ có thật (50, 100, 220, 500 mm; cạnh trong, cạnh ngoài, mặt sau nhà T, sân trong nhà U): đã thử, sau sửa
nóc 2 dốc **không bao giờ cao hơn** mái 4 dốc cùng nhà, bản mái dày đều, tối đa 7 mảnh (≤ 12). Nên **không** thêm ngưỡng bỏ
bậc (không có số mới khách gặp); bậc được dựng đúng như vẽ.

Test (`KiemTraDocLapDotFTests`, 1189 test lõi): T nhiễu ±2 mm 30 hạt cố định (2 dốc và 4 dốc đều 2 mảnh, nóc 4293); L bậc 2 / 3 /
5 / 10 / 20 mm ở cạnh trong (2 mảnh, nóc 4871); 20 ca bậc thật (nóc 2 dốc ≤ nóc 4 dốc, ≤ 12 mảnh, bản mái dày đúng); T 30° xoay
+ làm tròn ra 2 mảnh. Test mái F3 cũ giữ nguyên, vẫn xanh.

Số kỹ thuật bên trong (không cần duyệt): `SnapMinMm` 5 mm, `WidthTieMm` 50 mm. Nhiễu ±4 mm ở cánh hẹp 1,5 m vẫn có thể bị từ
chối "không phải góc vuông" (ngưỡng `StairLogic.SquareTol` 2e-3 có từ đợt E): giữ nguyên, ghi để biết.

Ghi chú nhẹ của người soát:
- (c) checklist tổng: F6-12 xếp lại sau F6-11.
- (b) `list_entities` theo khung trong UCS xoay đo từng hình theo trục UCS (tạo bản sao để đo), bản vẽ rất nhiều khối 3D có thể
  chậm: thêm mục thử tay F7-10.
- (a) block thư viện đuôi `-TV` đã có sẵn thì dùng lại mà không kiểm cỡ lần nữa (hiếm: cần cả block gốc lẫn block `-TV` khác cỡ):
  ghi ở đây, không sửa.

Thử tay thêm: F6-4b (nhà T vẽ tay lệch + bậc 220 mm).


## Chốt nhánh trước bộ cài thử bản 4 (25/11/2026)

Soát cuối của kiểm tra độc lập trên `fc92cd6`: KTF-01 lần 2 đúng (0/1680 ca sai), đợt F5 không có lỗi chứng minh được; còn
KTF-09 và một nghi vấn, sửa ở đây.

- **Nhánh:** `full/nang-cap-f-tong-the` tiến thẳng (fast-forward) tới `origin/full/nang-cap-f5-test` @ `fc92cd6`
  (= `03bf163` + đợt F5 `1dc03b9` + bản sửa của người rà F5).
- **KTF-09 (nhẹ, F3, có từ F3):** nhà vẽ tay có cạnh ngắn lệch vài mm bị từ chối "không phải góc vuông" (L cánh hẹp 1,5 m
  nhiễu ±2–3 mm: 22/120 lần 2 dốc, 14/120 lần 4 dốc), vì lệch 4 mm trên cạnh 1,5 m = 2,7e-3 > ngưỡng góc 2e-3. Sửa
  (`RoofLogic.IsRightAngled`): một cạnh còn được nhận khi hai đầu của nó lệch ngang (theo hệ trục trung bình theo độ dài
  của mọi cạnh, như `SquareUp`) không quá khoảng cách gộp hai đường tường của `SquareUp` (max(`SnapMinMm` 5 mm, 2e-3 × cỡ
  nhà); nhà 10 m là 20 mm). Chỉ nới thêm, cái gì nhận trước vẫn nhận; tường xiên thật vẫn bị từ chối. Test: L cánh hẹp,
  T, chữ thập nhiễu ±3 mm, 120 hạt cố định × 2 kiểu mái: lần nào cũng dựng được, số mảnh và nóc như nhà vuông; tường xiên
  60 mm / 7,5 m, tường 5°, góc 45° vẫn bị từ chối. Bỏ phần sửa thì 3 ca nhiễu đỏ.
- **Ô "Nạp gọn lệnh" đổi giữa cuộc chat (nghi vấn, chưa thử được với Claude thật):** chọn cách an toàn. Cuộc chat đã có
  tin giữ cách nạp lúc bắt đầu tới khi bấm "Chat mới"; cuộc chat chưa có tin thì áp dụng ngay (khởi động sẵn lại như F5).
  Cách nạp lưu trong file chat của bản vẽ (`ChatTranscript.DeferTools`, ghi ở tin đầu, xóa khi Chat mới); file chat cũ đã
  có cuộc chat coi là bật (trước F5 mọi cuộc chat đều nạp gọn); `ChatDeferLogic.Effective`; tin và khởi động sẵn cùng dùng
  `ChatController.ChatDefer()`. Trợ lý không đổi (vẫn khởi động lại nối tiếp khi cách nạp khác, nay chỉ xảy ra khi chuyển
  sang cuộc chat của bản vẽ khác có cách nạp khác — đúng cách nạp của cuộc chat đó). Dòng giải thích dưới ô: "Áp dụng từ
  cuộc chat mới (bấm Chat mới); cuộc chat đã có tin nhắn giữ cách nạp lúc bắt đầu."
- **Quyết định tự chọn:** hai điểm trên (ghi cả trong `docs/dot-f-cho-duyet.md` và bàn giao F5).
- **Tài liệu:** `docs/checklist-dot-f5.md` gộp vào khối "Đợt F" của checklist tổng (mục F11, có mục "đổi ô ⚙ rồi Chat mới"
  F11-3 và mái cánh hẹp F11-13), file riêng để dòng trỏ; `docs/dot-f-cho-duyet.md` thêm mục "Đợt F5" và "Sau kiểm tra độc
  lập", soát lại cả file với code (hai "điểm để cậu xem lại" về ghi đè đã được F5 làm: gạch đi và ghi rõ); bàn giao F5,
  `bo-cai/README.md` cập nhật.
- **Số kỹ thuật bên trong:** không thêm số mới (dùng lại `SnapMinMm` 5 mm và `StairLogic.SquareTol` 2e-3).
- **Test:** thêm 5 test lõi (`KiemTraDocLapDotFTests`: KTF-09 ×3 nhà, tường xiên, cách nạp theo cuộc chat); sửa 2 dòng trong
  `NangCapF5Tests` (chữ dòng giải thích, dòng code gửi `defer_tools`) và 2 dòng trong `TongTheDotFTests` (tiêu đề khối,
  file F5 trỏ về checklist tổng).
