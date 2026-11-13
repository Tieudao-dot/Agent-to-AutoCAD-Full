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
| F (sau phát hành) | Mọi lệnh hiểu UCS (hiện theo WCS); vật liệu / render; block từ thư viện; bộ layout khung tên; layer state; lưu góc nhìn; bộ style chuẩn Việt Nam | — |

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
| Test tổng thể nâng cấp | `session_01D56HmGm85zfmxQqdNwrF2N` (mở 23h51) | — | `full/nang-cap-tong-the` (từ `cdaa29b`) | Đang chạy; giữ nhánh tích hợp |
| Kiểm tra độc lập nâng cấp | (mở sau test tổng thể) | — | | Chờ |

Lịch sử duyệt: 13/11 chủ dự án duyệt đợt A, ban đầu chờ thử 3D thật; cùng ngày đổi sang chạy liền A → E, bắt đầu ngay.
