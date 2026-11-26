# Bàn giao: Test tổng thể đợt H phần 2 (H6 → H9) (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC`. Nhánh `full/nang-cap-h-tong-the-2`, tách từ `full/nang-cap-h9-test` @ `adf0093` (sau rà
H9, code `7834e43`), gộp tài liệu luồng chính (`docs/`, `CLAUDE.md` từ `claude/inspiring-cray-58xsmt`; 5 file chung chồng nhau là
tài liệu luồng chính, lấy bản luồng chính). Giữ nhánh tích hợp phần 2 cho bản sửa sau kiểm tra độc lập. Chưa chạy trên AutoCAD thật.

## 1. Rà chiều ngang

| Điểm | Kết quả |
| --- | --- |
| Layer mới `C-` / `P-` / `M-` / `F-` / `S-` với `check_drawing` / `fix_drawing` (G2), `batch_files` (H5) | Là layer chuẩn (`IsStandardLayer`, sửa ở H6T-4, H7T-06, H8); `fix_drawing` và `batch_files` check dùng cùng hàm: không báo, không sửa |
| Cùng layer với chuẩn công ty (H4) | Không có trong bộ chuẩn công ty thì báo khác chuẩn, như layer `A-…` (chuẩn công ty là danh sách đóng). Giữ, ghi ở "Quyết định tự chọn" |
| Excel (F4) | `mep_report`, `rebar_schedule`: thẻ F5 khi ghi đè; bảng chèn vào bản vẽ và Excel cùng một bảng (`ScheduleTable`); giữ tiếng Việt khi English (H9 quyết định 2; không gọi `Tr`, có test). `terrain` không xuất Excel (bảng trắc chỉ vẽ) |
| Gói 2D / 3D / Full | `terrain`, `draw_rebar`, `rebar_schedule` 2D / Full; `draw_mep`, `mep_report`, `build_footing` 3D / Full (test chéo `TongTheDotHPhan2Tests`) |
| "Chỉ xem" | 4 lệnh vẽ là lệnh ghi (dispatcher từ chối); `mep_report` đọc; `rebar_schedule` đọc trừ khi chèn bảng (`table_position`) |
| Nạp gọn (F4) | 6 lệnh đều hoãn nạp; nhãn tiếng Việt + tiếng Anh |
| UCS (F1) | `terrain` giữ WCS (tọa độ thật); `build_footing`, `draw_rebar` vẽ như World rồi dời theo UCS; `draw_mep` đổi điểm + cao độ theo UCS, từ chối điểm mặt bằng dưới UCS nghiêng (H7T-05); hai lệnh báo cáo đọc |
| H9 | Nhãn 6 lệnh có bản tiếng Anh; lời báo plugin → Claude không dịch (H9 quyết định 4), không còn "in Vietnamese" ở H6 → H8; chữ vẽ (tên cọc, nhãn ống, bảng thép, trắc) giữ tiếng Việt (test). Rà H9: xác nhận gõ tay có điều kiện / nháy cong = không đồng ý, máy nâng cấp lấy ngôn ngữ bộ cài, chỉ file khung chat gọi `Tr`: test của thread rà có, lệnh mới H6 → H8 theo đúng |
| Skill `ve-ket-cau` / `ve-mep` `plan: 2d` | Máy gói 3D không nạp hai skill này (cả phần móng / ống 3D). **Giữ** `plan: 2d` (quyết định 12/11/2026: skill 2D không nạp trên máy 3D). Mô tả `build_footing` / `draw_mep` đã có số mặc định; **thêm** câu "mức dốc tối thiểu là số mặc định, kỹ sư đối chiếu TCVN" vào mô tả `draw_mep` (trước chỉ có trong skill 2D) |
| `H6Numbers.UnitsPerMetre` (H6T-3) | Đổi theo bản vẽ trong `using (ForDrawing(...))`: trả lại 1000 cả khi lệnh lỗi giữa chừng. Đúng |

## 2. Số tiêu chuẩn trong code so bản duyệt (`docs/dot-h-so-tieu-chuan.md`)

Khớp (test của từng đợt + `TongTheDotHPhan2Tests.The_approved_defaults_of_part_2`): D60 dốc 2,5 %, D110 trở lên 2 %; vạch lý trình
50 m mặt bằng, cọc 20 m trên trắc; không cộng hao hụt ống / thép (cột hao hụt tắt). Sáu chỗ "kỹ sư đối chiếu TCVN hiện hành": lớp
bảo vệ móng 70, neo / nối tối thiểu, móc đai 75 (skill `ve-ket-cau` + mô tả `draw_rebar`), dốc tối thiểu ống thoát (skill `ve-mep`
+ mô tả `draw_mep`, thêm ở lượt này), khoảng cao đều đồng mức và cọc 20 m (skill `ve-ha-tang` + mô tả `terrain`).

## 3. Lỗi đã sửa

| Mã | Mức | Lỗi | Đã sửa |
| --- | --- | --- | --- |
| TTH2-1 | Nhẹ | Máy gói 3D (không nạp skill `ve-mep`) không được dặn mức dốc tối thiểu ống thoát là số mặc định cần kỹ sư đối chiếu TCVN | Thêm vào mô tả `draw_mep`; test |

Không có lỗi chéo nặng. Các lỗi của từng đợt đã sửa ở lượt rà (H6T-1 … 5, H7T-01 … 06, H8T-01 … 03, rà H9 3 lỗi).

## 4. Token lúc mở chat (cuối phần 1 `76ecfae` → cuối phần 2)

| Gói / chế độ | Nạp gọn | Tắt nạp gọn | English (nạp gọn / tắt) |
| --- | --- | --- | --- |
| 2D | 13,9k → 14,1k | 48,0k → 51,0k | 14,5k / 51,4k |
| 3D | 19,2k → 19,3k | 46,4k → 48,5k | 19,7k / 48,9k |
| Full · Cả hai | 21,2k → 21,5k | 69,3k → 74,4k | 21,9k / 74,7k |
| Full · chế độ 2D | 14,0k → 14,2k | 48,0k → 51,1k | 14,6k / 51,4k |
| Full · chế độ 3D | 19,2k → 19,4k | 46,5k → 48,6k | 19,8k / 48,9k |

Đo bằng số ký tự (khoảng 3,5 ký tự / token). English thêm khoảng 0,4k (phần `# Language`). Nạp gọn giữ mức tăng của 6 lệnh mới
ở 0,1 – 0,3k.

## 5. Tài liệu

- `docs/checklist-tong-the.md`: khối "Đợt H phần 2" (mục làm trước P2-0: điểm khảo sát thật + VN-2000, soát va chạm ống, bảng thép
  so kỹ sư tính tay, bộ cài English + cài lại, gõ tay tiếng Anh, chữ bản vẽ / Excel giữ tiếng Việt; rồi H6 → H9; mục chéo P2-X).
  `checklist-dot-h6.md` … `h9.md` ghi đã gộp.
- `docs/dot-h-cho-duyet.md`: phần 2 (số thường + quyết định tự chọn H6 → H9 và các lượt rà, đã soát với code; số tiêu chuẩn trỏ
  `dot-h-so-tieu-chuan.md`; hai lựa chọn H9 của luồng chính).

## 6. Test

- Lõi: **1687 / 1687** (thêm 5 ở `TongTheDotHPhan2Tests.cs`).
- Trợ lý: **466 / 466**.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi.

## 7. CI

Run 45857510577 trên `8c46a8e` (commit code cuối): **xanh**, log Windows trợ lý `# pass 466 / # fail 0`, lõi `Failed: 0, Passed:
1687`. Commit sau đó chỉ là tài liệu (mục này). Một lượt CI (giới hạn 2).

## 8. Chưa kiểm được

- AutoCAD thật cho cả phần 2 (điểm khảo sát thật, ống trong mô hình thật, bảng thép so tính tay, bộ cài English): checklist P2-0.
