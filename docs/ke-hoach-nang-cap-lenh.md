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
| F3 | `session_01D56HmFrL65iF9oJokvqXw5` (thread Test & sửa F1 nhận viết, 25/11 16h26 → 17h12, ~$10; hai thread mở riêng cho F3 bị chặn lưu, đã dừng) | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (17h14 → 17h35, ~$4; 2 lỗi sửa) | `full/nang-cap-f3` @ `09ae953` (code `4a26989`, lõi 1001, trợ lý 317), CI 45590903520 xanh → `full/nang-cap-f3-test` @ `53e18b5` (code `d3484f6`), CI 45595009744 xanh (lõi 1012, trợ lý 317) | **Xong**, ~$14, 1 giờ 9 phút. Mái liền khối chữ L / T / U, `set_material`, `named_view` (kèm xuất ảnh PNG), bản thang và bản mái dốc 120 mm; token mở chat 3D và Full +1,0k. Con số chờ duyệt: `docs/dot-f-cho-duyet.md` |
| F4 | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (25/11 17h40 → 18h20, $14,6) | Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5` (18h22 → 19h17, có 30 phút dừng vì chạm giới hạn sử dụng; ~$10; 3 lỗi sửa) | `full/nang-cap-f4` @ `d6c15d6`: gộp F2 + F3 ở `c67bc79` (CI 45597564054 xanh, lõi 1087, trợ lý 328), code F4 `587f7ce` (CI 45601966332 xanh, lõi 1113, trợ lý 347) → `full/nang-cap-f4-test` @ `1cdf8d4` (code `9278808`), CI 45610810220 xanh (lõi 1126, trợ lý 355) | **Xong**, ~$25, 1 giờ 37 phút. Trợ lý khởi động sẵn, 25 lệnh nạp sẵn + lệnh khác tìm khi cần (token mở chat ước tính Full 60,5k → 20,6k, 2D 40,5k → 13,4k, 3D 41,8k → 18,8k), có công tắc tắt nạp gọn, 4 bảng xuất `.xlsx`. Con số chờ duyệt: `docs/dot-f-cho-duyet.md` |
| Cuối | Test tổng thể đợt F: thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (25/11 19h20 → 19h44, ~$10; 3 lỗi nhẹ sửa) | Kiểm tra độc lập: thread mới `session_01D56HmHz4EXXJceBgkmjcap` (mở 19h45; chỉ ghi lỗi, gửi báo cáo về luồng chính) | `full/nang-cap-f-tong-the` @ `afdaa68` (code `7aa74c0`), CI 45614706040 xanh (lõi 1132, trợ lý 355) | Test tổng thể **xong**: checklist khối "Đợt F" (~2 giờ thử tay) ở cuối `docs/checklist-tong-the.md`; token mở chat ước tính trước → sau: 2D 38,8k → 13,4k, 3D 39,8k → 18,8k, Full 57,7k → 20,6k (tắt nạp gọn: 40,8k / 42,0k / 61,0k). Kiểm tra độc lập 19h45 → 19h57 (~$10): 8 lỗi (1 nặng, 3 vừa, 4 nhẹ), báo cáo `docs/ban-giao/kiem-tra-doc-lap-dot-f.md`; sửa ở `f037b79` (CI 45620825504 xanh), soát lại 7/8 đúng; lỗi mái KTF-01 sửa lần 2 ở `03bf163`, soát lượt cuối đúng (0 / 1680 ca sai); thêm KTF-09 (nhẹ) sửa ở bản chốt. **Nhánh tích hợp đợt F chốt 21h15: `full/nang-cap-f-tong-the` @ `a6da118` (code `7fe215c`, CI 45630097088 xanh, lõi 1217, trợ lý 364), có đủ F1 → F5; kiểm tra độc lập: 9 lỗi, 9 đã sửa và soát lại đúng, 0 lỗi mở.** Bộ cài thử bản 4 build xong 21h28: `full/ban-thu-noi-bo` @ `2a68813` (code `5c75b77`), CI 45632787564 xanh, artifact `AgentToAutoCADFull-ThuNoiBo-nang-cap-4-0.9.4` (id 15031114800, 134 MB, tải được tới 23/02/2027); là mốc kiểm nội bộ, không gửi chủ dự án (cậu ấy chọn nhận một lần ở bản 5) |
| F5 | Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5` (25/11 20h05 → 20h32, $11,6) | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (20h35 → 21h00, ~$9; 2 lỗi thẻ sửa; gộp luôn nhánh tích hợp @ `03bf163`) | `full/nang-cap-f5` @ `1dc03b9` (code `99f1bf6`), CI 45622402883 xanh (lõi 1148, trợ lý 364) → `full/nang-cap-f5-test` @ `c14c8d7` (code `fc92cd6`), CI 45627464838 xanh (lõi 1212, trợ lý 364) | **Xong**, ~$21, 55 phút; đã gộp vào nhánh tích hợp đợt F. Ba việc nhỏ còn sót của đợt F, chủ dự án chọn 25/11 ~20h: ô bật / tắt nạp gọn lệnh trong ⚙; thẻ xác nhận đồng bộ (ghi đè file PDF / STL / PNG / Excel, ghi đè / xóa góc nhìn, gán vật liệu cả layer); ghi chú có mũi tên (multileader). Rà xong thì gộp vào nhánh tích hợp đợt F, kịp bộ cài thử bản 4 |

## Đợt G (chốt 25/11/2026 ~20h)

Chủ dự án chọn làm tiếp cả 5 tính năng mới luồng chính đề xuất, **chạy liền như đợt F**: không dừng để hỏi, thread dùng số
đề xuất, luồng chính gom con số và quyết định tự chọn cho chủ dự án duyệt một lần ở cuối; dừng trước phát hành.

| Đợt | Nội dung | Gói | Ước |
| --- | --- | --- | --- |
| G1 | Ra bộ hồ sơ một lệnh: từ mô hình 3D tự xếp mặt bằng, mặt đứng, mặt cắt, phối cảnh lên các tờ có khung tên, in một PDF | Theo gói của từng phần (đủ bộ: Full) | $25–40 |
| G2 | Soát lỗi bản vẽ trước khi xuất: layer sai tên, chữ sai cỡ, thiếu kích thước, đối tượng lệch layer; báo cáo kèm đề xuất sửa | 2D / Full | $20–30 |
| G3 | Bảng dự toán: từ bảng khối lượng, thêm đơn giá do khách nhập, tính thành tiền, xuất Excel | Theo gói của bảng nguồn | $20–30 |
| G4 | Render thật: lệnh RENDER và thư viện vật liệu của Autodesk, có đường lui về ảnh chụp hiện tại | 3D / Full | $20–30 |
| G5 | Vẽ lại từ ảnh / PDF scan (phần lớn và khó chính xác nhất; tốn token của khách mỗi lần dùng) | 2D / Full | $35–45 |
| Cuối | Test tổng thể đợt G; kiểm tra độc lập (agent mới); bộ cài thử bản 5 | — | $30–40 |

- Tổng ước $150–200, khoảng 8–12 giờ chạy; có thể ngắt quãng khi chạm giới hạn sử dụng 5 giờ (luồng chính đặt lịch tự
  cho chạy lại sau khi giới hạn đặt lại).
- **Chạy cùng lúc (chủ dự án đồng ý 25/11 ~20h):** G1 với G2; G3 với G4. Mỗi đợt vẫn có thread viết và thread khác rà.
  G5 chạy riêng sau cùng. Sau mỗi cặp, thread kế tiếp gộp hai nhánh, CI xanh rồi mới làm tiếp.
- **Bắt đầu sau khi đợt F xong hẳn** (kiểm tra độc lập + sửa + F5 + bộ cài thử bản 4), xuất phát từ commit cuối của nhánh
  tích hợp đợt F. Lý do: ưu tiên bộ cài thử bản 4 trước, và giới hạn sử dụng dùng chung cho mọi thread.
- Thread: dùng lại ba thread đã lưu được lên GitHub (thread F1, Test & sửa F1, F2); thread mới chỉ dùng cho kiểm tra độc lập
  (không cần lưu).
- Con số và quyết định tự chọn của đợt G gom vào một file riêng `docs/dot-g-cho-duyet.md`.
- **Giao bộ cài (chủ dự án chốt 25/11 ~21h35):** chỉ gửi **một lần khi xong hết**. Bộ cài thử bản 4 (đợt F + F5) vẫn build
  làm mốc kiểm nội bộ nhưng không gửi. Cuối đợt G: **một agent khác test lại toàn bộ tính năng đợt F và đợt G một lượt**
  (chủ dự án yêu cầu), lỗi sửa xong và được soát lại, rồi build bộ cài thử bản 5; sau đó mới tạo thread **"Lưu trữ bộ
  cài"** và gửi link / file bộ cài vào đó, rồi báo chủ dự án. Con số đợt F (`docs/dot-f-cho-duyet.md`) và đợt G đưa duyệt
  chung một lần ở báo cáo cuối.
- **Đã bắt đầu 25/11 21h25** (chủ dự án dặn "triển khai cho hoàn thành"): G1 và G2 xuất phát từ nhánh tích hợp đợt F đã
  chốt `full/nang-cap-f-tong-the` @ `a6da118`, chạy cùng lúc với bước build bộ cài thử bản 4.

### Tiến độ đợt G

| Đợt | Thread viết | Thread rà | Nhánh | Trạng thái |
| --- | --- | --- | --- | --- |
| G1 | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (giao 25/11 21h25) | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (giao 25/11 22h02), nhánh `full/nang-cap-g1-test` | `full/nang-cap-g1` (từ `a6da118`) | Viết xong 22h00: `dd29c9b` (code `28ca6c2`), CI 45638049687 xanh, lõi 1228, trợ lý 367, ≈ $10–15. **Rà xong 22h23:** sửa 4 lỗi, `full/nang-cap-g1-test` @ `99aaab7` (code `9c7c95f`), CI 45642708237 xanh, lõi 1237, trợ lý 367, ≈ $6 |
| G2 | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (giao 25/11 21h27) | Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5` (giao 25/11 21h50), nhánh `full/nang-cap-g2-test` | `full/nang-cap-g2` (từ `a6da118`) | Viết xong 21h48: `8bbc27c` (code `aa91157`), CI 45636296067 xanh, lõi 1240, trợ lý 369, ≈ $7. **Rà xong 22h13:** sửa 4 lỗi, `full/nang-cap-g2-test` @ `f49e717` (code `7b9be8f`), CI 45640452138 xanh, lõi 1255, trợ lý 373, ≈ $6–9 |
| G3 | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (giao 25/11 22h17): gộp G1 + G2 trước, CI xanh rồi viết | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (giao 25/11 22h48), nhánh `full/nang-cap-g3-test` | `full/nang-cap-g3` (từ `full/nang-cap-g1-test`, gộp `full/nang-cap-g2-test` @ `f49e717`) | Gộp xong 22h31 ở `395c313`, CI 45643804481 xanh, lõi 1275, trợ lý 376. Viết xong 22h46: `6c7c650` (code `ec03a77`), CI 45646666421 xanh, lõi 1282, trợ lý 379, ≈ $12–18 (cả gộp). **Rà xong 23h00:** sửa 3 lỗi, `full/nang-cap-g3-test` @ `4061a6c` (code `d6b4a60`), CI 45649649749 xanh, lõi 1292, trợ lý 379, ≈ $5 |
| G4 | Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5` (giao 25/11 22h33) | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (giao 25/11 23h59), nhánh `full/nang-cap-g4-test` | `full/nang-cap-g4` (từ `full/nang-cap-g3` @ `395c313`) | Viết xong 23h57 (dừng 23h00–23h50 vì chạm giới hạn sử dụng): `7641203` (code `7ddbf12`), CI 45649202227 xanh, lõi 1294, trợ lý 385. **Rà xong 00h21 (26/11):** sửa 6 lỗi (1 nặng), `full/nang-cap-g4-test` @ `29a2e6c` (code `fdeb306`), CI 45661671404 xanh, lõi 1299, trợ lý 387, ≈ $8–12 |
| G5 | Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5` (giao 26/11 00h11): gộp G3 + G4 trước, CI xanh rồi viết | Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh` (giao 26/11 00h42), nhánh `full/nang-cap-g5-test` | `full/nang-cap-g5` (từ `full/nang-cap-g3-test` @ `4061a6c`, gộp `full/nang-cap-g4-test`) | Gộp xong 00h25 (26/11) ở `1c2a1c1`, CI 45662682522 xanh, lõi 1316, trợ lý 390. Viết xong 00h40: `1cc438f` (code `8a2515c`), CI 45665170098 xanh, lõi 1356, trợ lý 398. **Rà xong 01h11 (26/11):** sửa 5 lỗi (1 nặng), `full/nang-cap-g5-test` @ `e3eda7b` (code `464b6e5`), CI 45670027419 xanh, lõi 1364, trợ lý 400, ≈ $9 |
| Test tổng thể G | Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC` (giao 26/11 00h58) | — | `full/nang-cap-g-tong-the` (từ `full/nang-cap-g5-test`, gộp tài liệu luồng chính) | Xong 01h19 (26/11): sửa 2 lỗi chéo, `881b12e` (code `4fbb2a2`), CI 45671817871 xanh, lõi 1368, trợ lý 400, ≈ $12–15. Token mở chat (nạp gọn) cuối F → cuối G: 2D 13,4k → 13,7k, 3D 18,8k → 19,0k, Full 20,7k → 21,0k |
| Test lại F + G bằng agent khác | Thread mới `session_01D56HmFjDhopvJD3nN6ZMUa` (mở 26/11 01h20), chỉ ghi lỗi, không sửa, không lưu lên GitHub | — | đọc `full/nang-cap-g-tong-the` @ `881b12e` | Xong lượt đầu 01h40 (26/11): 27 lỗi (1 nặng khả năng cao, 7 vừa, 19 nhẹ), ≈ $11,4. Báo cáo: `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md`. Chờ soát lại sau khi sửa |
| Sửa lỗi KFG | Ba thread song song (giao 26/11 01h47): thread đợt F1 (G2) `full/nang-cap-g-sua-kfg-a`; thread Test & sửa F1 (G4, G5, F5) `full/nang-cap-g-sua-kfg-b`; thread đợt F2 (G1, G3, F2, prompt) sửa trên nhánh tích hợp rồi gộp hai nhánh kia + CI | Thread kiểm tra độc lập soát lại | `full/nang-cap-g-tong-the` | Đang sửa |
| Cuối | — | — | — | Bộ cài thử bản 5, thread "Lưu trữ bộ cài", báo cáo cuối |

### Số mặc định chủ dự án xác nhận 25/11/2026

Hai nhóm số thread tự chọn hôm 13/11, nay hỏi lại theo quy tắc "con số phải xác nhận trước":

| Nhóm | Số (mm) | Kết quả |
| --- | --- | --- |
| Cầu thang, mái dốc | Bản thang + chiếu nghỉ 150 → **120**; bản mái dốc 150 → **120**; khe giữa 2 vế thang chữ U **100** | Đổi 2 số, giữ 1 số; sửa trong đợt F3 |
| Lan can | Cao 1100; khe song ≤ 100; trụ cách nhau ≤ 1500; tay vịn và trụ 50 × 50; song 20 × 20; thanh dưới cách sàn 100; tấm kín dày 20 | Giữ nguyên |

Các số đợt E chốt 13/11 giữ nguyên: cao bậc 160, rộng bậc 280, vế rộng 1000, mái bằng dày 150, mái dốc 30°, đua 600.
