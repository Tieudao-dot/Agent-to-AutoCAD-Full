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

*(điền sau khi chạy)*
