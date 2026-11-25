# Kế hoạch nâng cấp lệnh vẽ (2D + 3D)

*13/11/2026 · thread QnA soạn theo yêu cầu của chủ dự án, luồng chính lưu lại. **Chủ dự án đã chốt 5 điểm (13/11, cuối
file).** Sau đó (13/11, qua QnA, luồng chính xác nhận lại) chủ dự án cho **chạy liền A → E, bắt đầu ngay**, không duyệt
từng đợt; kiểu cầu thang / mái đợt E đã chốt; chỉ dừng hỏi trước phát hành.*

## Hiện trạng

- 89 lệnh 2D và lệnh chung, có từ bản gốc v0.9.3, đã dùng thật.
- 29 lệnh 3D (3 đợt), qua kiểm tra tự động và kiểm tra độc lập, **chưa thử trên AutoCAD thật**.
- Code đọc ở `full/test-tong-the` @ `19944df` (`agent/src/tools/*.ts`, `plugin/ClaudeCAD.Plugin/Core/Plans.cs`).

## Nguyên tắc

- Thử thật trước: bộ cài thử nội bộ đã có.
- Mỗi đợt một thread viết, kèm thread con "Test & sửa", mỗi thread 1 lượt CI.
- Cuối chuỗi: kiểm tra độc lập và test tổng thể.
- Giữ tên lệnh cũ, để macro khách đã ghi không hỏng.
- Đo lại token theo gói sau mỗi đợt.

## Các đợt (đề xuất)

| Đợt | Nội dung | Gói |
| --- | --- | --- |
| 0 | Chủ dự án thử theo `docs/checklist-tong-the.md` (bộ cài thử nội bộ); lỗi chuyển về thread phụ trách | — |
| A (3D, rẻ) | `build_3d_from_plan` tự nhận layer tên khác (hỏi lại người dùng); cột; nhiều tầng một lần; lan can; `array_rect` thêm số tầng; gộp 6 lệnh dựng khối (`draw_box`, `draw_cylinder`, `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid`) thành 1 | 3D / Full |
| B (sửa hình 2D) | Lệnh mới: STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE, GROUP. Nâng cấp: bo / vát góc cho polyline và cung (hiện chỉ cho 2 đoạn thẳng), offset hai phía, MATCHPROP, nối đường có dung sai | GROUP, MATCHPROP: mọi gói. Còn lại: 2D / Full |
| C (2D) | Hatch bấm một điểm; tự ghi kích thước cả mặt bằng; thống kê block ra bảng, đếm theo thuộc tính; tổng diện tích phòng / layer ra bảng; tìm & thay chữ (có thẻ xác nhận); in nhiều layout vào một PDF | 2D / Full |
| D (3D) | Dầm; tường cong; PRESSPULL; xuất STL; bảng khối lượng theo layer; va chạm ra bảng | 3D / Full |
| E (lớn) | Cầu thang (1 vế / chữ L / chữ U); mái bằng / 2 mái / 4 mái. Số mặc định: điểm 6 | 3D / Full |
| F (ban đầu để sau phát hành; 25/11 chốt làm ngay, xem mục "Đợt F") | Mọi lệnh hiểu UCS (hiện theo WCS); vật liệu / render; block từ thư viện; bộ layout khung tên; layer state; lưu góc nhìn; bộ style chuẩn Việt Nam; nút chọn chế độ "Cả hai / 2D / 3D" cho máy gói Full (mặc định Cả hai; chọn riêng thì bớt ~1/3 token mỗi lần mở chat), quyết sau khi thử thật (chốt 14/11) | — |

## Chi phí ước

**Số thật của chuỗi 3D** (hệ thống ghi cho từng thread, quy ra giá API, xem lúc 13/11 14h; có thể chưa gồm hết tác vụ con):

| Đợt | Thread viết (gồm sửa sau kiểm tra độc lập) | Test & sửa | Cộng |
| --- | --- | --- | --- |
| 3D đợt 1 | $12,3 | $4,7 | ~$17 |
| 3D đợt 2 | $13,7 | $5,0 | ~$19 |
| 3D đợt 3 | $20,8 | $6,2 | ~$27 |

Kiểm tra độc lập $22; test tổng thể $65 (gồm bộ cài, 3 vòng gộp, nhiều lượt CI); luồng chính $66 tới 13/11.

Ước lại cho nâng cấp:
- mỗi đợt (viết + test & sửa): khoảng $15–30;
- cả chuỗi A–E, kèm kiểm tra độc lập và test tổng thể cuối chuỗi: khoảng $150–230 (QnA ước cũ $50–120 là thấp);
- thử tay trên AutoCAD: thêm 20–40 phút mỗi đợt.
- Số thật đợt A: $35,7 (cao hơn ước vì lượt tự rà chạy ở mức suy nghĩ cao nhất). Nếu các đợt sau cũng vậy thì ~$35–45
  mỗi đợt; ở mức thường thì giữ được $15–30.

## Chủ dự án đã chốt (13/11/2026)

1. **Gộp 6 lệnh dựng khối thành 1** (bớt khoảng 2k token mỗi phiên), làm trong đợt A, trước khi phát hành: sau khi
   phát hành, đổi tên lệnh sẽ làm hỏng macro khách đã ghi.
2. **Nâng cấp xong mới phát hành:** không phát hành bản hiện tại trước. Phát hành sau đợt E (và kiểm tra độc lập, test
   tổng thể cuối chuỗi).
3. **Gói của lệnh mới theo đúng phương án A:**
   - sửa hình 2D mới (STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE) và các nâng cấp sửa hình 2D: 2D / Full;
   - GROUP, MATCHPROP: mọi gói;
   - chữ / kích thước / bảng / hatch / in: 2D / Full;
   - cột, dầm, cầu thang, mái, STL: 3D / Full.
4. **Thứ tự** A → B → C → D → E. F để sau phát hành.
5. ~~Duyệt chi theo từng đợt~~ → **thay bằng (13/11): chạy liền A → B → C → D → E, bắt đầu ngay**, không chờ chủ dự
   án thử 3D (thử song song bằng bộ cài thử nội bộ; lỗi thử thật do thread "Test tổng thể" sửa trên `full/test-tong-the`,
   các đợt gộp bản sửa vào). Xong mỗi đợt luồng chính báo thời gian + chi phí, không chờ duyệt. Cả chuỗi ước $150–230.
   Cuối chuỗi: kiểm tra độc lập, test tổng thể. **Dừng trước phát hành** (merge `main`, CI `release: true`, nâng
   phiên bản hỏi riêng).
6. **Đợt E (chốt 13/11):** đủ 3 kiểu cầu thang (1 vế thẳng, chữ L, chữ U) và 3 kiểu mái (bằng, 2 mái, 4 mái). Số mặc
   định khi khách không nói số: cao bậc 160 mm, rộng bậc 280 mm, vế rộng 1000 mm, chiếu nghỉ rộng bằng vế; mái bằng dày
   150 mm; mái dốc nghiêng 30°, đua ra khỏi tường 600 mm. Khách nói số thì theo số của khách.

## Tiến độ chuỗi nâng cấp

| Đợt | Thread viết | Thread con Test & sửa | Nhánh | Trạng thái |
| --- | --- | --- | --- | --- |
| A | `session_01D56HmEZnPtpeS5RWXPVPYD` (14h28 → 16h17) | `session_01D56HmExJjYYvbztLiiRbdx` (4 lỗi sửa) | `full/nang-cap-a` @ `fc68469` → `full/nang-cap-a-test` @ `33acb5f`, CI 43390204331 + 43398072331 xanh | **Xong** ~1 giờ 50 phút, $35,7 (viết $27,6, vượt mềm $2,6 do lượt tự rà chạy ở mức suy nghĩ cao nhất ~$19; test $8,1) |
| B | `session_01D56HmERJAiGtKizUeoMK7s` (16h18 → 19h31; nghỉ ~1 giờ 40 phút vì giới hạn dùng 5 giờ + đứng chờ CI) | `session_01D56HmJLL8fUhKMHs8VaPJm` (2 lỗi sửa) | `full/nang-cap-b` @ `81eaf7c` → `full/nang-cap-b-test` @ `265b66e`, CI 43408010489 + 43423428390 xanh | **Xong**, ~$21 (đợt $15,8 + test $5,2); token 2D / Full +2,2k, 3D +0,5k |
| C | `session_01D56HmJra3trN7XwA1QApZ6` (19h34 → 21h40; đứng chờ CI 19h58–20h24) | `session_01D56HmJVjAwoMYD2HCjBwLe` (6 lỗi sửa; đứng chờ CI 20h38–21h39) | `full/nang-cap-c` @ `1305053` → `full/nang-cap-c-test` @ `f4f27fe`, CI 43429577551 + 43435421710 xanh | **Xong**, ~$12 (đợt $8,6 + test ~$3,3, số hệ thống ghi lúc 21h38); token 2D / Full +2,1k, 3D không đổi |
| D | `session_01D56HmEVecDKB1j1iGf8vNc` (21h41 → 22h50, không bị đứng) | `session_01D56HmFm4aVWGEgb2v8hWTB` (6 lỗi sửa) | `full/nang-cap-d` @ `809ad78` → `full/nang-cap-d-test` @ `db34801`, CI 43451219212 + 43454839792 xanh | **Xong** ~1 giờ 10 phút, ~$15 (đợt $10,8 + test $4,5); token 3D +1,9k, Full +2,1k |
| E | `session_01D56HmJ3WbDQRkUMc3aJjvF` (22h51 → 23h50, không bị đứng) | `session_01D56HmEVcsHK1xiof9qua9w` (3 lỗi sửa) | `full/nang-cap-e` @ `3e8a67c` → `full/nang-cap-e-test` @ `cdaa29b`, CI 43461186495 + 43465289107 xanh | **Xong** ~1 giờ, ~$12,1 (đợt $7,3 + test $4,8); token 3D / Full +1,4k. Tự chọn 3 số chờ chủ dự án xem: bản thang 150, khe 2 vế chữ U 100, bản mái dốc 150 (mm) |
| Test tổng thể nâng cấp | `session_01D56HmGm85zfmxQqdNwrF2N` (23h51 → 0h13) | — | `full/nang-cap-tong-the` @ `f1e7123`, CI 43468951145 xanh | **Xong**, 3 lỗi chéo sửa; token 2D 34,4k → 38,7k, 3D 35,7k → 39,7k, Full 49,7k → 57,5k; giữ nhánh tích hợp |
| Kiểm tra độc lập nâng cấp | `session_01D56HmHmd7iAXreWAFzYLX4` (0h14 → 1h17, ~$16, 3 lượt) | — | `full/kiem-tra-doc-lap-2` @ `e21e61a` | **Xong**: 32 lỗi (0 nặng), 30 sửa đúng, 2 chấp nhận (KT2-11 phần cân nhắc, KT2-22), 0 mở. Nhánh tích hợp cuối `full/nang-cap-tong-the` @ `f7f32ad`, CI 43478782014 xanh |

Lịch sử duyệt: 13/11 chủ dự án duyệt đợt A, ban đầu chờ thử 3D thật; cùng ngày đổi sang chạy liền A → E, bắt đầu ngay.

Quyết định luồng chính chọn theo đề xuất của kiểm tra độc lập (14/11 0h35, chủ dự án đã cho chạy liền không dừng hỏi;
cách rẻ, đổi lại được, chủ dự án xem trong báo cáo cuối):
- bản vẽ đơn vị mét: các lệnh 2D mới + `array_rect` số tầng + PRESSPULL tự quy đổi đơn vị như lệnh 3D (KT2-04, 10, 13);
- mái nhà chữ L / T: Claude báo trước chỗ nối hai mái sẽ chồng, cần sửa tay (KT2-08); mái liền để đợt F.

**Chuỗi xong 14/11 ~1h40.** Bộ cài thử nội bộ build lại có đủ nâng cấp (CI 43481896536). Báo cáo:
[`bao-cao-nang-cap-14-11.md`](bao-cao-nang-cap-14-11.md). Dừng trước phát hành.

## Đợt F (chốt 25/11/2026)

Chủ dự án đã thử thật bộ cài thử bản 3 (chạy ổn) và chốt **làm đợt F ngay, trước khi phát hành**, cả 4 nhóm. Chạy nối
tiếp; đợt sau xuất phát từ commit cuối của bước Test & sửa đợt trước.

| Đợt | Nội dung | Gói | Ước |
| --- | --- | --- | --- |
| F1 | Mọi lệnh vẽ hiểu UCS (UCS World thì y như cũ); nút chế độ "Cả hai / 2D / 3D" cho máy gói Full | UCS: mọi gói. Nút: chỉ gói Full | $25–45 |
| F2 | Bộ style chuẩn Việt Nam; layout có khung tên; block từ thư viện; lưu / gọi layer state | 2D / Full | $25–40 |
| F3 | Mái liền khối nhà chữ L / T; vật liệu + render; lưu góc nhìn; đổi số mặc định bản thang + chiếu nghỉ và bản mái dốc từ 150 xuống 120 mm (chốt 25/11) | 3D / Full | $25–40 |
| F4 | Mở chat nhanh hơn (trợ lý khởi động sẵn, chỉ nạp mô tả lệnh cần dùng); xuất bảng khối lượng / thống kê ra Excel | Theo gói của bảng nguồn | $20–35 |
| Cuối | Test tổng thể đợt F; kiểm tra độc lập (agent mới); bộ cài thử bản 4 | — | $30–40 |

- Tổng ước $125–200, khoảng 12–14 giờ. Ước theo số thật của chuỗi A → E; các thread đợt F chạy cùng cấu hình với chuỗi đó.
- F1 trước vì đụng nhiều lệnh nhất: lệnh mới ở các đợt sau theo UCS ngay từ đầu. F4 sau cùng để đo token trên đủ bộ lệnh.
- **Cách chạy (chốt 25/11 15h50):** sau F1, **F2 và F3 chạy cùng lúc** (2D và 3D là hai mảng khác nhau), mỗi đợt có
  thread con Test & sửa riêng. Xong cả hai, thread F4 gộp hai nhánh, gỡ chỗ chồng nhau, CI xanh rồi mới làm F4. Nhanh hơn
  khoảng 1,5 giờ, thêm khoảng $5–10 cho bước gộp. Phần thử tay của F2 / F3 viết vào `docs/checklist-dot-f2.md` /
  `docs/checklist-dot-f3.md`; test tổng thể cuối chuỗi gộp vào checklist tổng.
- Nhánh gốc: `full/nang-cap-f-goc` @ `2dbeee2` (nhánh tích hợp `012ed29` + file quyền `.claude/settings.json`).
- **Kiểm tra trước khi báo hoàn thành** (chủ dự án yêu cầu 25/11):
  - mỗi đợt có thread viết + thread con Test & sửa, mỗi thread CI xanh;
  - cuối chuỗi: test tổng thể + kiểm tra độc lập, lỗi sửa xong và được soát lại;
  - luồng chính tự kiểm CI và file bộ cài rồi mới báo chủ dự án.
- F4 (mở chat nhanh, xuất Excel) là ý của luồng chính, chủ dự án chọn 25/11. Còn để sau: vẽ lại từ ảnh / PDF scan.
- **Con số phải được chủ dự án xác nhận** (yêu cầu 25/11): thread đợt F gom các số khách sẽ gặp (cỡ chữ, kích thước
  khung tên, độ dày, góc…) thành bảng "Con số chờ chủ dự án duyệt".
- **Chạy liền, không hỏi lại (chủ dự án dặn 25/11 ~15h52):** chuỗi không dừng để hỏi. Thread dùng số đề xuất, để ở một
  chỗ dễ đổi; điểm cần chọn thì chọn cách rẻ, an toàn, đổi lại được và ghi "Quyết định tự chọn". Luồng chính gom bảng
  con số và các quyết định, đưa chủ dự án duyệt một lần ở báo cáo cuối chuỗi. Vẫn dừng trước phát hành.

### Tiến độ đợt F

| Đợt | Thread viết | Thread con Test & sửa | Nhánh | Trạng thái |
| --- | --- | --- | --- | --- |
| F1 | `session_01D56HmFAo4dZ7c8sbeNB9eh` (25/11 14h27 → 15h19, $14,8) | `session_01D56HmFrL65iF9oJokvqXw5` (15h19 → 15h51, $6,3; 4 lỗi sửa) | `full/nang-cap-f1` @ `8110e45`, CI 45573567269 xanh → `full/nang-cap-f1-test` @ `b2e6431`, CI 45578496260 xanh (lõi 973, trợ lý 311) | **Xong**, ~$21, 1 giờ 24 phút. Token mở chat Full 58k → 39k (2D) / 40k (3D) khi chọn chế độ riêng. Không có con số nào cần duyệt |
| F2 | `session_01D56HmFcYrzsme7GoBm8tNC` (25/11 15h53 → 16h36, $11,3) | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (16h39 → 17h08, ~$7,3; 7 lỗi sửa). Thread con `session_01D56HmJDwuCnKRDvTAFHFXr` bị chặn lưu, đã dừng (~$4), 7 phát hiện của nó đã được đối chiếu | `full/nang-cap-f2` @ `bae8262`, CI 45584939266 xanh → `full/nang-cap-f2-test` @ `c9fdc08` (code `6e9f622`), CI 45590233177 xanh (lõi 1048, trợ lý 322) | **Xong**, ~$23, 1 giờ 15 phút. 4 lệnh mới (`setup_standards`, `create_sheet`, `insert_library_block`, `layer_state`); token mở chat 2D +1,5k, 3D +0,7k, Full +1,5k. Con số chờ duyệt: `docs/dot-f-cho-duyet.md` |
| F3 | `session_01D56HmFrL65iF9oJokvqXw5` (thread Test & sửa F1 nhận viết, 25/11 16h26 → 17h12, ~$10; hai thread mở riêng cho F3 bị chặn lưu, đã dừng) | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` rà từ 17h14 | `full/nang-cap-f3` @ `09ae953` (code `4a26989`, lõi 1001, trợ lý 317), CI 45590903520 xanh → `full/nang-cap-f3-test` | Viết xong: mái liền khối chữ L / T / U, `set_material`, `named_view` (kèm xuất ảnh PNG), bản thang và bản mái dốc 120 mm; token mở chat 3D và Full +1,0k. Đang Test & sửa |
| F4 | — | — | — | Chờ F2 và F3; thread F4 gộp hai nhánh trước |
| Cuối | — | — | — | Chờ F4 |

### Số mặc định chủ dự án xác nhận 25/11/2026

Hai nhóm số thread tự chọn hôm 13/11, nay hỏi lại theo quy tắc "con số phải xác nhận trước":

| Nhóm | Số (mm) | Kết quả |
| --- | --- | --- |
| Cầu thang, mái dốc | Bản thang + chiếu nghỉ 150 → **120**; bản mái dốc 150 → **120**; khe giữa 2 vế thang chữ U **100** | Đổi 2 số, giữ 1 số; sửa trong đợt F3 |
| Lan can | Cao 1100; khe song ≤ 100; trụ cách nhau ≤ 1500; tay vịn và trụ 50 × 50; song 20 × 20; thanh dưới cách sàn 100; tấm kín dày 20 | Giữ nguyên |

Các số đợt E chốt 13/11 giữ nguyên: cao bậc 160, rộng bậc 280, vế rộng 1000, mái bằng dày 150, mái dốc 30°, đua 600.
