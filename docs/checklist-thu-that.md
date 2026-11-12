# Checklist thử thật trên AutoCAD (một lượt ~20 phút)

*Soạn 11/11/2026 cho bộ cài thử 0.9.5 (đã gộp khóa key, giao diện mới, AutoCAD 2026 / 2027). Chi tiết từng mục: `docs/test-log.md` (cột "Mã gốc").*

Mục đích: một lượt thử trên máy có AutoCAD thật, xếp theo rủi ro. Phần **A–E hỏng thì không phát hành**; phần F–H
hỏng thì ghi lại, sửa sau vẫn được. Mỗi bước đánh dấu **Đạt** hoặc **Lỗi** (lỗi thì ghi 1 dòng ở cuối file và làm
tiếp bước sau, trừ khi bước ghi "dừng").

Ký hiệu: ★ bắt buộc · ○ bỏ qua được khi ít thời gian (lượt rút gọn chỉ làm ★, khoảng 12 phút).

## Trước khi bắt đầu

| Cần có | Ghi chú |
| --- | --- |
| Máy Windows có AutoCAD bản đầy đủ (không phải LT), đã đóng AutoCAD | Tốt nhất là máy **chưa từng cài** ClaudeCAD; nếu đã cài thì bước A1–A3 ghi "đã kích hoạt từ trước" |
| `ClaudeCAD-Setup.exe` của bộ cài thử | Lấy từ lượt CI của nhánh gộp (mục Artifacts) |
| Tài khoản Claude Pro / Max (hoặc API key) | Để đăng nhập ở bước B2 |
| Chủ plugin mở sẵn Gmail + Google Sheet "Thiết bị" | Chỉ cần cho phần A |

**Bản cài:** một bộ cài thử duy nhất build từ nhánh gộp `claude/release-0.9.5` (khóa key + giao diện mới +
AutoCAD 2026 / 2027 + tự kiểm tra mở rộng). Làm cả checklist **một lần** trên bản đó; không cần cài riêng từng nhánh.

Nếu vì lý do nào đó phải thử bản cài của một nhánh riêng:

| Phần | Cần nhánh | Thử trên bản khác thì |
| --- | --- | --- |
| A — kích hoạt + quản lý thiết bị | `claude/device-management` | A1 (nhập key tay), A6 chạy được; A2–A5 bỏ |
| F — giao diện mới | `claude/ux-autocad` | bỏ cả phần F |
| H — AutoCAD 2026 / 2027 | `claude/acad-2026-2027` | bỏ cả phần H |
| B4 — các mục tự kiểm tra mới | `claude/test-checklist` | báo cáo ngắn hơn, vẫn đọc ✓ / ✗ như thường |

## A. Cài đặt + kích hoạt key — bài test 6 bước về key (~4 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| A1 | ★ | Chạy `ClaudeCAD-Setup.exe`, tới trang **Kích hoạt**, để trống ô key, bấm Next → Yes | Trang hiện mã máy `CC-…`; cài xong không lỗi; chủ plugin nhận email và thấy dòng **Chờ duyệt** trong Sheet | ☐ | ☐ | K-1, K-8, I-1 |
| A2 | ★ | Mở AutoCAD | Không có cảnh báo "load this plugin?"; khung chat tự mở với thẻ 🔒 có nút **Kiểm tra lại**; gõ tin nhắn thử → bị chặn | ☐ | ☐ | I-2, K-4 |
| A3 | ★ | Chủ plugin đổi ô Trạng thái thành `Active` → bấm **Kiểm tra lại** | "Chủ plugin đã duyệt máy này", thẻ **Bắt đầu** hiện ra, không phải nhập key | ☐ | ☐ | K-8 |
| A4 | ★ | Chủ plugin đổi thành `Disabled` → đóng, mở lại AutoCAD. Sau đó đổi lại `Active` + **Kiểm tra lại** | Khi `Disabled`: thẻ 🔒 "tạm khóa", không gửi được tin. Khi `Active` lại: dùng tiếp ngay | ☐ | ☐ | K-9 |
| A5 | ○ | Dán một key sai vào ô key của thẻ 🔒 (làm lúc đang `Disabled` ở A4) | Báo lỗi, vẫn khóa | ☐ | ☐ | K-2, K-10 |
| A6 | ○ | Chạy lại `ClaudeCAD-Setup.exe` (cài đè) khi máy đã kích hoạt | Không hỏi key; skill / lịch sử chat còn nguyên | ☐ | ☐ | K-3 |

Không nằm trong lượt 20 phút (thử riêng khi rảnh, cần ngắt mạng / chỉnh ngày giờ): K-5, K-6, K-7, K-11 … K-16.

## B. Mở khung chat + đăng nhập (~3 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | ★ | Đóng khung chat rồi mở lại bằng `CC`, sau đó bằng **Ctrl+Shift+Space** khi đang ở bản vẽ | Cả hai cách đều mở khung chat, con trỏ nằm ở ô nhập | ☐ | ☐ | 1a, V6-8 |
| B2 | ★ | Thẻ **Bắt đầu** → **Đăng nhập Claude** → đồng ý trên trình duyệt | "✓ Đã đăng nhập Claude"; thẻ Bắt đầu hiện ✓ bước 1–2 | ☐ | ☐ | I-3, UX-19 |
| B3 | ★ | Gõ "chào Claude, kiểm tra kết nối với plugin" | Chữ hiện dần; Claude trả phiên bản AutoCAD + tên bản vẽ. **Lỗi ở đây thì dừng**, làm mục "Gửi lại gì khi lỗi" | ☐ | ☐ | 1a |
| B4 | ★ | Gõ lệnh `CLAUDECHATSELFTEST`, chờ Notepad mở báo cáo | Dòng cuối "Tất cả đều đạt."; có dòng ✓ "Bản vẽ của cậu không bị đụng tới"; AutoCAD quay về đúng bản vẽ đang mở. Có dòng ✗ → chép nguyên dòng đó | ☐ | ☐ | ST-1 … ST-5, D-2 |

B4 thay cho việc thử tay: nhóm hoàn tác, chụp ảnh bản vẽ, ghi nhận sửa tay, bản sao phiên bản, đọc theme AutoCAD,
icon thanh trạng thái đã gắn, thu gọn không mất tin nhắn, DLL đúng với đời AutoCAD.

## C. Vẽ thử (~3 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| C1 | ★ | Bản vẽ mới. Gõ "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước" | Tường hai nét, cửa có cánh + cung mở, kích thước đủ bốn phía, chữ tiếng Việt có dấu đọc được; layer `A-WALL`, `A-DOOR`, `A-DIM` | ☐ | ☐ | TC-2, TC-10, 1d |
| C2 | ★ | Trong lúc Claude đang vẽ: nhìn khung chat và icon **C** ở thanh trạng thái | Nhóm "Đang làm · bước N…" cập nhật; icon có **chấm xanh**, xong thì mất chấm | ☐ | ☐ | UX-20, UX-15 |
| C3 | ★ | Bấm **Xem kết quả (N)**, rồi bấm một link handle trong câu trả lời | Zoom tới và tô sáng đúng đối tượng vừa vẽ | ☐ | ☐ | UI-3, UI-4 |
| C4 | ○ | Bấm icon **Chọn đối tượng**, quét chọn 2 tường → gõ "đổi mấy cái này sang màu đỏ" | Chip "Kèm 2…"; chỉ 2 tường đó đổi màu | ☐ | ☐ | UI-1, L-1 |
| C5 | ○ | Tự tay dời 1 tường bằng lệnh MOVE, rồi gõ "tớ vừa sửa gì?" | Có dòng ✎ ghi nhận sửa tay; Claude nói đúng tường đã dời | ☐ | ☐ | V7-1 |

(TC-n = kịch bản n trong bảng "Tiêu chí nghiệm thu" của test-log.)

## D. Xác nhận trước khi xóa nhiều (~2 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| D1 | ★ | Gõ "Vẽ 30 đường thẳng ngang dài 1000, cách nhau 100, trên layer A-TEMP" | 30 đường trên layer `A-TEMP` | ☐ | ☐ | TC-7 |
| D2 | ★ | Gõ "Xóa toàn bộ đối tượng trên layer A-TEMP" | **Chưa xóa gì**; thẻ xác nhận có số 30; icon thanh trạng thái có **chấm vàng** | ☐ | ☐ | TC-4, 1c, UX-9, UX-15 |
| D3 | ★ | Bấm **⌖ Xem trên bản vẽ**, rồi **Hủy** | Đúng 30 đường được chọn / tô sáng; sau Hủy vẫn còn đủ 30 đường | ☐ | ☐ | UI-11, UX-9 |
| D4 | ★ | Gõ lại câu ở D2 → **Đồng ý** | 30 đường bị xóa, phần phòng ở C1 không mất gì | ☐ | ☐ | TC-4, R-3 |

## E. Hoàn tác một lần U (~1 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| E1 | ★ | Ngay sau D4: bấm vào bản vẽ, gõ `U` **một lần** | Cả 30 đường quay lại (một U = cả lượt), phòng ở C1 giữ nguyên | ☐ | ☐ | TC-8, 1b |
| E2 | ★ | Gõ "bản vẽ có bao nhiêu đối tượng?" rồi gõ `U` một lần | U hoàn tác lượt *vẽ / xóa* gần nhất, không bị "hụt" vì lượt chỉ đọc | ☐ | ☐ | 1b |
| E3 | ○ | Gõ "vẽ thêm 1 hình tròn bán kính 500" → bấm **Hoàn tác lượt này** trong khung chat | Hình tròn mất; nút ẩn đi sau khi hoàn tác | ☐ | ☐ | UI-5 |

## F. Giao diện mới (~4 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| F1 | ★ | Mở palette Properties (Ctrl+1) đặt cạnh khung chat, theme **tối** | Nền, chữ, ô nhập, viền, font và cỡ chữ của khung chat khớp Properties (chụp 1 ảnh hai palette cạnh nhau) | ☐ | ☐ | UX-1, UX-3 |
| F2 | ★ | Gõ `COLORTHEME` → `1` (sáng), nhìn khung chat và icon thanh trạng thái; rồi `COLORTHEME` → `0` | Khung chat đổi sáng / tối **ngay**, không cần đóng mở; icon **C** vẫn rõ ở cả hai theme (chụp 1 ảnh theme sáng) | ☐ | ☐ | UX-2, UX-17, UI-19 |
| F3 | ★ | Rê chuột qua các icon trên thanh công cụ và 4 icon dưới ô nhập | Icon hiện rõ (không ra ô vuông trống); mỗi icon có tooltip "Tên — giải thích ngắn" | ☐ | ☐ | UX-6, UX-18 |
| F4 | ★ | Bấm **⌄** thu gọn → gõ "vẽ 1 đường thẳng dài 2000" + Enter → bấm dòng "✓ Đã xong…" | Palette co còn hàng công cụ + ô nhập; Claude vẫn vẽ; mở lại thì đoạn chat còn nguyên | ☐ | ☐ | UX-11, UX-12 |
| F5 | ★ | Đóng khung chat (✕). Nhìn icon **C** ở thanh trạng thái, rê chuột, rồi bấm vào icon | Tooltip "ClaudeCAD — … Bấm để mở khung chat"; bấm → khung chat mở lại, đoạn chat còn | ☐ | ☐ | UX-14 |
| F6 | ○ | Bật tự ẩn (auto-hide) palette, gõ lệnh `CC`, gửi "vẽ 1 hình chữ nhật 1000x500", để palette cuộn lại | Lượt xong có **bong bóng** "Claude đã xong: …" ở icon; bấm *Mở khung chat* → mở | ☐ | ☐ | UX-16 |
| F7 | ○ | Đang chạy một lượt dài (lặp lại C1): gõ thêm "thêm cửa sổ 1200" + Enter; lượt sau đó nhấn **Esc** khi con trỏ ở ô nhập | Hàng "⏳ Chờ gửi…" hiện, lượt xong thì tin tự gửi; Esc dừng lượt đang chạy | ☐ | ☐ | UX-7, UX-8 |
| F8 | ○ | Đang thu gọn (⌄), gõ lại câu xóa ở D2 | Palette **tự mở** và hiện thẻ xác nhận (bấm Hủy) | ☐ | ☐ | UX-13 |
| F9 | ○ | ⚙ → mở / đóng các nhóm; đổi Giao diện sang **Sáng** khi AutoCAD đang tối rồi trả về *Theo AutoCAD* | 5 nhóm mở / đóng được; ép Sáng thì chữ vẫn đọc rõ | ☐ | ☐ | UX-5, UX-10 |

Đã có test tự động che (không cần thử tay): nội dung tooltip, bố cục ⚙, hàng chờ gửi, danh sách bước, Markdown,
↑↓ gọi lại tin, popup `/` và `@` — UX-4, UI-8, UI-10, UI-16, V6-7.

## G. Skill, macro, phiên bản (~3 phút)

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| G1 | ★ | Bấm icon skill ✦ → bỏ (−) skill `ve-kien-truc`, bật `ve-mep` → gõ "vẽ 1 đoạn ống nước lạnh dài 3000" | Số skill trên nút đổi theo; Claude dùng layer / chuẩn của skill MEP, không dùng layer kiến trúc | ☐ | ☐ | S-2, 1e, UI-9 |
| G2 | ○ | **+ Add skill** → chọn 1 file `.md` skill của cậu (nếu có) | "✓ Đã thêm skill …", có dấu ✓ trong đoạn chat hiện tại | ☐ | ☐ | SK-1, S-1 |
| G3 | ★ | Sau lượt G1: **💾 Lưu macro** → icon ▷ → **📍 Tại điểm** → bấm 1 điểm trên bản vẽ → gõ `U` một lần | Vẽ lại đúng hình tại điểm chọn, không tốn lượt Claude; một U xóa cả macro | ☐ | ☐ | V6-3, R-1 |
| G4 | ★ | Lưu bản vẽ thành file `.dwg`, gõ "vẽ thêm 1 hình tròn bán kính 300" → mở **Phiên bản** ⟲ → **Xem** → **Đóng — về bản hiện tại** | Có ít nhất 1 phiên bản; tab chỉ xem hiện bản *trước* lượt vừa rồi, ô chat khóa; đóng thì về đúng bản vẽ, đoạn chat không đổi | ☐ | ☐ | V7-5, V7-6, R-2 |
| G5 | ○ | Mở bản vẽ thứ hai (đã lưu), chat 1 câu, chuyển qua lại hai tab bản vẽ | Mỗi bản vẽ có lịch sử chat và bộ skill riêng | ☐ | ☐ | UI-17, S-4 |
| G6 | ○ | ⚙ ▸ **Gửi báo cáo lỗi** | Có file .zip trên Desktop, Explorer mở; trong zip không có nội dung chat / API key | ☐ | ☐ | R-1 (báo cáo), R-6 |

## H. Riêng cho AutoCAD 2026 / 2027 (~3 phút mỗi bản)

Làm trên máy có AutoCAD 2026 và / hoặc 2027. Phần A–G chỉ cần làm đủ trên **một** đời AutoCAD; ở đời còn lại làm H.

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| H1 | ★ | Cài bằng `ClaudeCAD-Setup.exe`, mở AutoCAD 2026 / 2027 | Không hỏi "load this plugin?"; khung chat tự mở | ☐ | ☐ | 2026, 2027 (dòng bộ cài) |
| H2 | ★ | `CLAUDECHATSELFTEST` → đọc dòng "Bản plugin đúng với đời AutoCAD" | ✓; 2026 ghi bản net8 (DLL trong thư mục `2025`), 2027 ghi bản net10 (thư mục `2027`) | ☐ | ☐ | 2026, 2027 (dòng nạp DLL) |
| H3 | ★ | Gõ "vẽ 1 hình chữ nhật 2000x1000 và ghi chữ THỬ ở giữa" → **Hoàn tác lượt này** | Vẽ đúng, chữ có dấu; hoàn tác sạch | ☐ | ☐ | 2026, 2027 (dòng vẽ thử) |
| H4 | ★ | Lặp lại F2 (đổi `COLORTHEME`) và nhìn icon thanh trạng thái | Khung chat đổi theo; icon **C** hiện, có chấm xanh khi Claude làm | ☐ | ☐ | 2026, 2027 (dòng theme) |
| H5 | ○ | (2027) Kéo một file ảnh vào ô chat; bấm icon **Chụp** | Ảnh đính kèm được, không báo lỗi; ảnh chụp bản vẽ rõ nét | ☐ | ☐ | 2027 (dòng vẽ thử) |

## I. Gói dịch vụ 2D / 3D / Full (bản Full, ~5 phút)

Cần máy chủ kích hoạt riêng của bản Full đã dựng (cột **Gói** trong trang "Thiết bị"). Sau mỗi lần đổi ô Gói, bấm
**Kiểm tra lại** (⚙ hoặc thẻ 🔒) thay cho chờ ~6 giờ.

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| I1 | ★ | Máy mới kích hoạt, mở khung chat | Thanh trên cùng và ⚙ hiện **Gói: 2D** (gói mặc định) | ☐ | ☐ | GOI-1 |
| I2 | ★ | Đổi ô Gói thành `3D`, Kiểm tra lại, gõ "vẽ hình chữ nhật 2000x1000 và ghi kích thước" | Toast "Gói đã đổi sang 3D"; vẽ được hình chữ nhật; Claude nói máy dùng gói 3D, chưa có kích thước, liên hệ chủ plugin; không cố vẽ kích thước bằng đường | ☐ | ☐ | GOI-2 |
| I3 | ★ | (gói 3D) Chạy một macro cũ có ghi chữ hoặc kích thước | Lệnh chữ / kích thước trong macro báo lỗi ngoài gói, bản vẽ không có chữ / kích thước mới | ☐ | ☐ | GOI-3 |
| I4 | ★ | Đổi ô Gói thành `Full`, Kiểm tra lại, gõ lại câu ở I2 | Toast "Gói đã đổi sang Full"; có kích thước; cuộc chat cũ vẫn còn | ☐ | ☐ | GOI-4 |
| I5 | ○ | Đặt `%LocalAppData%\ClaudeCAD\plan.txt` chỉ đọc, đổi ô Gói về `2D`, Kiểm tra lại | Máy không giữ được gói cũ: về gói mặc định | ☐ | ☐ | GOI-5 |

## J. 3D đợt 1 (bản Full, gói 3D hoặc Full, ~8 phút)

Máy phải ở gói `3D` hoặc `Full` (phần I). Mở một bản vẽ mới (acadiso.dwt), tab Model.

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| J1 | ★ | Gõ `CLAUDECHATSELFTEST` | Hai dòng mới ✓: "Khối 3D: dựng hộp, trừ trụ, kiểm thể tích" (≈ 0,404602 m³) và "Góc nhìn 3D (SW iso) + kiểu Conceptual + ảnh 3D"; thư mục thử có `capture-3d.png` thấy hộp có lỗ tròn | ☐ | ☐ | 3D-1 |
| J2 | ★ | Gõ "dựng tường dài 4 m, dày 220, cao 3,3 m, khoét 1 cửa đi 900x2100 cách đầu tường 500" | Claude tạo layer riêng (vd. A-WALL-3D), dựng tường + khối cửa, trừ khối cửa **không hỏi xác nhận** (cùng một lượt); đặt góc SW iso, chụp ảnh Conceptual; báo thể tích ≈ 2,904 − 0,416 = 2,488 m³ | ☐ | ☐ | 3D-2 |
| J3 | ★ | Gõ U một lần | Cả tường và lỗ cửa biến mất cùng lúc (một lượt = một U) | ☐ | ☐ | 3D-3 |
| J4 | ★ | Dựng lại J2, lượt sau gõ "khoét thêm cửa sổ 1200x1200 cao 900" | Claude hiện **thẻ xác nhận** trước khi trừ vào tường đã có; Đồng ý → có lỗ cửa sổ; Hủy → tường giữ nguyên | ☐ | ☐ | 3D-4 |
| J5 | ★ | "vẽ biên dạng chữ L rồi đùn cao 200", "xoay biên dạng hình chữ nhật 300x1000 quanh trục Y thành trụ", "dựng hình nón, cầu, nêm, xuyến, chóp 4 cạnh" | Mỗi khối đúng hình (xem ở góc iso); `get_solid_info` trả thể tích, hộp bao, số mặt / cạnh | ☐ | ☐ | 3D-5 |
| J6 | ★ | "xem từ phía trước", "xem góc đông bắc", "chuyển kiểu Realistic", "về 2D Wireframe" | Góc nhìn và kiểu hiển thị đổi đúng; **không** thêm bước U (gõ U không hoàn tác việc đổi góc nhìn) | ☐ | ☐ | 3D-6 |
| J7 | ○ | Bật 🔒 Chỉ xem, gõ "xem góc iso kiểu Conceptual" rồi "dựng hộp 1x1x1 m" | Góc nhìn / kiểu đổi được; dựng hộp bị từ chối (chỉ xem) | ☐ | ☐ | 3D-7 |
| J8 | ★ | Đổi máy về gói `2D`, Kiểm tra lại, gõ "dựng hộp 1000x1000x1000" | Claude nói máy dùng gói 2D, chưa có 3D, liên hệ chủ plugin; không cố vẽ hộp bằng đường | ☐ | ☐ | 3D-8 |
| J9 | ○ | AutoCAD 2022–2024 và 2025–2027 (nếu có): chạy J1 | J1 ✓ trên từng đời | ☐ | ☐ | 3D-9 |
| J10 | ★ | "dựng nêm dài 3000, rộng 1200, cao 600 tại gốc", xem góc SW iso | Mặt đứng cao 600 ở phía x = 0, mặt dốc hạ xuống 0 tại x = 3000; `get_solid_info`: thể tích 1,08 m³, hộp bao [0,0,0]–[3000,1200,600]. Nếu Claude báo "unexpected orientation" thì ghi lại cả câu lỗi | ☐ | ☐ | 3D-10 |
| J11 | ★ | "vẽ hình chữ nhật 1000x500 rồi đùn cao 300 có góc vát 10 độ" | Khối nhỏ dần lên trên (mặt trên nhỏ hơn mặt đáy); hộp bao z từ 0 tới 300 | ☐ | ☐ | 3D-11 |
| J12 | ★ | Vẽ tay 4 LINE nối kín một hình chữ nhật **theo chiều kim đồng hồ**, rồi gõ "đùn 4 đường vừa vẽ cao 500" | Khối đi **lên** (z 0 → 500), không đi xuống | ☐ | ☐ | 3D-12 |
| J13 | ★ | Lượt J2 (tường + cửa, trừ trong cùng lượt) → ⋯ ▸ Lưu thành macro → mở bản vẽ mới, chạy macro; rồi chạy macro lần nữa trong **chính bản vẽ cũ** | Mỗi lần chạy có một tường mới có lỗ cửa; tường cũ trong bản vẽ cũ **không** bị khoét thêm | ☐ | ☐ | 3D-13 |
| J14 | ○ | AutoCAD bản ngôn ngữ khác tiếng Anh (nếu có), hoặc bản vẽ từ mẫu tiếng nước ngoài: "chuyển kiểu Conceptual", "chụp ảnh góc iso kiểu Conceptual" | Kiểu đổi được, ảnh có màu; nếu lỗi thì Claude nêu tên các kiểu có trong bản vẽ — ghi lại danh sách đó | ☐ | ☐ | 3D-14 |
| J15 | ○ | Dựng hộp 1000×1000×1000 và hộp thứ hai chạm đúng mặt (cạnh nhau, không chồng), gõ "giao hai hộp" | Claude báo hai khối không giao nhau / AutoCAD không ghép được, **bản vẽ không đổi**, AutoCAD không treo | ☐ | ☐ | 3D-15 |

## K. 3D đợt 2 (bản Full, gói 3D hoặc Full, ~10 phút)

Máy ở gói `3D` hoặc `Full`. Bản vẽ mới (acadiso.dwt), tab Model. Mỗi bước xong thì nhìn góc SW iso kiểu Conceptual.

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| K1 | ★ | Gõ `CLAUDECHATSELFTEST` | Ba dòng mới ✓: "bo 4 cạnh đứng R100" (≈ 0,474850 m³), "cắt đôi … mỗi nửa ~½" (0,24 m³ mỗi nửa), "hai hộp chồng nhau … va chạm" (0,5 m³) | ☐ | ☐ | 3D2-1 |
| K2 | ★ | "dựng hộp 1000x800x600 rồi bo tròn 4 cạnh đứng bán kính 100" | Một lượt, không hỏi xác nhận; 4 cạnh đứng tròn, cạnh trên / dưới vẫn vuông; thể tích ≈ 0,4749 m³ | ☐ | ☐ | 3D2-2 |
| K3 | ★ | Lượt sau: "vát 4 cạnh trên của hộp đó 30 mm" | **Thẻ xác nhận** trước khi vát (khối có từ lượt trước); Đồng ý → 4 cạnh trên vát, thể tích giảm | ☐ | ☐ | 3D2-3 |
| K4 | ★ | "bo cạnh hộp bán kính 2000" | Claude báo bán kính quá lớn, bản vẽ không đổi, AutoCAD không treo | ☐ | ☐ | 3D2-4 |
| K5 | ★ | "dựng hộp 2000x1000x1000, cắt đôi bằng mặt phẳng đứng ở giữa theo chiều dài, giữ cả hai nửa" | Hai khối, mỗi khối 1 m³; tổng không đổi | ☐ | ☐ | 3D2-5 |
| K6 | ★ | "dựng hộp 1000x1000x800, làm rỗng thành bể hở miệng, thành dày 50" | Bể mở mặt trên, kích thước ngoài giữ nguyên 1000×1000×800, thành 50 (đáy dày 50); thể tích ≈ 0,8 − 0,9×0,9×0,75 = 0,1925 m³ | ☐ | ☐ | 3D2-6 |
| K7 | ★ | "vẽ đường dẫn chữ L từ (0,0,0) lên (0,0,1000) rồi sang (1000,0,1000), quét ống tròn D50 theo đường đó" | Ống tròn D50 bám đường dẫn, tiết diện vuông góc đường dẫn ở đầu ống; thể tích ≈ π·25²·2000 ≈ 0,0039 m³ | ☐ | ☐ | 3D2-7 |
| K8 | ★ | "loft từ hình vuông 1000x1000 ở z=0 lên hình tròn D400 ở z=2000" | Khối chuyển dần từ vuông sang tròn, không xoắn | ☐ | ☐ | 3D2-8 |
| K9 | ★ | "xoay hộp 90 độ quanh trục X qua gốc", "đối xứng hộp qua mặt phẳng YZ, giữ bản gốc", "căn hộp bằng 2 cặp điểm…" | Xoay / đối xứng / căn đúng; một U hoàn tác cả lượt | ☐ | ☐ | 3D2-9 |
| K10 | ★ | "đặt UCS mặt trước", rồi "vẽ hộp 500x500x500 tại gốc"; sau đó "trả UCS về World"; gõ U | Biểu tượng UCS đổi sang mặt trước; hộp vẫn nằm ở gốc WCS, đứng thẳng theo Z (không theo UCS); về World được; **U** hoàn tác cả hộp và UCS của lượt | ☐ | ☐ | 3D2-10 |
| K11 | ★ | Dựng 2 dầm chồng vào 1 ống, "kiểm tra va chạm" | Claude liệt kê cặp va chạm và thể tích phần chồng, bản vẽ không đổi | ☐ | ☐ | 3D2-11 |
| K12 | ★ | Dựng hộp, "liệt kê cạnh và mặt của hộp" rồi "bo cạnh số 3" | `get_solid_info` có danh sách cạnh / mặt và `topology_id`; bo đúng cạnh số 3. Sửa khối bằng tay (vd. tay kéo hộp) rồi "bo cạnh số 5" → Claude đọc lại danh sách cạnh trước khi bo, không bo nhầm | ☐ | ☐ | 3D2-12 |
| K13 | ★ | Lượt K2 → Lưu thành macro → chạy ở bản vẽ mới, rồi 📍 Tại điểm | Mỗi lần có hộp mới bo 4 cạnh đứng; hộp cũ không bị bo thêm | ☐ | ☐ | 3D2-13 |
| K14 | ○ | Bật 🔒 Chỉ xem: "bo cạnh hộp", "đặt UCS mặt trước", "kiểm tra va chạm" | Bo cạnh và UCS bị từ chối (chỉ xem); kiểm tra va chạm vẫn chạy (chỉ đọc) | ☐ | ☐ | 3D2-14 |
| K15 | ★ | Máy gói `2D`: "bo cạnh hộp", "quét ống theo đường" | Claude nói máy dùng gói 2D, chưa có 3D | ☐ | ☐ | 3D2-15 |
| K16 | ○ | AutoCAD 2022–2024 và 2025–2027 (nếu có), và AutoCAD không phải tiếng Anh: chạy K1, K6, K7 | Như trên ở từng đời | ☐ | ☐ | 3D2-16 |

Bổ sung sau Test & sửa đợt 2 ([`ban-giao/ve-3d-test-2.md`](ban-giao/ve-3d-test-2.md)):

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| K17 | ★ | Như K6 (bể hở miệng), rồi "dựng hộp 600x600x600, làm rỗng kín, thành 20" | K6 đúng như trên; hộp kín rỗng ruột, ngoài vẫn 600×600×600, thể tích ≈ 0,216 − 0,56³ ≈ 0,0404 m³. Nếu Claude báo "did not hollow … inward": gửi câu lỗi (dấu `ShellBody` khác tài liệu) | ☐ | ☐ | 3D2-17 |
| K18 | ★ | Khóa layer hiện hành (layer 0 khóa), rồi "quét ống D50 theo đường chữ L (0,0,0)→(0,0,1000)→(1000,0,1000) trên layer ONG" và "loft hình vuông 1000 ở z=0 lên hình vuông 400 ở z=2000 trên layer ONG" | Hai khối dựng trên layer ONG; không có đường dẫn / tiết diện thừa nào còn lại trong bản vẽ; U hoàn tác cả lượt | ☐ | ☐ | 3D2-18 |
| K19 | ○ | Có cả AutoCAD 2022–2024 và 2025–2027 trên một máy: ghi macro "dựng hộp, liệt kê cạnh, bo cạnh số 3 R50" ở đời này, chạy ở đời kia | Macro chạy được, hộp mới bo đúng cạnh số 3 (không báo "solid has changed") | ☐ | ☐ | 3D2-19 |
| K20 | ★ | 3 hộp A, B, C chồng nhau; "kiểm tra va chạm A, B với A, B, C" | Mỗi cặp nêu một lần (A–B, A–C, B–C), không có A–A, không lặp B–A | ☐ | ☐ | 3D2-20 |
| K21 | ★ | "căn hộp bằng 3 cặp điểm: góc (0,0,0)→(5000,0,0), (1000,0,0)→(5000,1000,0), (0,800,0)→(4200,0,0)" | Hộp dời và xoay 90° quanh Z về quanh (5000,0,0); không có lỗi "internal check of the transform" | ☐ | ☐ | 3D2-21 |

Còn chỉ thấy trên AutoCAD thật: `FilletEdges` với setback 0, căn tiết diện của `sweep` (tâm tiết diện đặt vào đầu
đường dẫn), U hoàn tác `set_ucs` (K10), quy ước mảng của `Matrix3d(double[])` (plugin tự kiểm, K21).

## Gửi lại gì khi lỗi

1. **Mã bước** bị lỗi (ví dụ `D2`) + một dòng: đã làm gì, thấy gì khác với "kết quả mong đợi".
2. **Ảnh chụp màn hình** cả cửa sổ AutoCAD lúc lỗi (Win+Shift+S), thấy được khung chat và thanh trạng thái.
3. **⚙ ▸ Gửi báo cáo lỗi** → gửi file `.zip` vừa hiện trên Desktop (thông tin máy, log, kết quả tự kiểm tra; không có
   nội dung chat, bản vẽ hay API key).
4. Nếu khung chat không mở được: gõ `CLAUDECHATLOG`, nén cả thư mục vừa mở rồi gửi.
5. Lỗi lúc **cài đặt / kích hoạt** (phần A): ảnh chụp trang bộ cài đang đứng + chữ báo lỗi. **Đừng gửi key** trong
   ảnh hay tin nhắn; chỉ cần mã máy `CC-…`.
6. File báo cáo của `CLAUDECHATSELFTEST` (Notepad tự mở) nếu có dòng ✗.

## Ghi kết quả

| Ngày | Máy / đời AutoCAD | Bản cài (số phiên bản hoặc commit) | Số bước Đạt / tổng | Bước lỗi |
| --- | --- | --- | --- | --- |
| | | | | |

Ghi chú lỗi (mỗi lỗi một dòng: mã bước — thấy gì):

-

Sau khi thử xong, các mục tương ứng trong `docs/test-log.md` được đổi từ "⬜ chưa chạy" sang ✅ / ❌ theo cột "Mã gốc".
