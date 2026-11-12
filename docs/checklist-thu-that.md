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
