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

## I. Gói dịch vụ 2D / 3D / Full (bản Full, ~15 phút)

> Bản Full: thử theo thứ tự trong [`checklist-tong-the.md`](checklist-tong-the.md) (điều kiện trước, phần I, L9 / L11,
> J10–J16, K17–K25, L17–L28, rồi mục rà chéo T1–T5, X1–X5 và phần M).

Cần máy chủ kích hoạt riêng của bản Full đã dựng (cột **Gói** trong trang "Thiết bị"). Sau mỗi lần đổi ô Gói, bấm
**Kiểm tra lại** (⚙ ▸ Tài khoản AI, hoặc thẻ 🔒) thay cho chờ ~6 giờ.

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| I1 | ★ | Máy mới kích hoạt, mở khung chat | Thanh trên cùng và ⚙ hiện **Gói: 2D** (gói mặc định) | ☐ | ☐ | GOI-1 |
| I2 | ★ | Đổi ô Gói thành `3D`, Kiểm tra lại, gõ "vẽ hình chữ nhật 2000x1000 và ghi kích thước" | Toast "Gói đã đổi sang 3D"; vẽ được hình chữ nhật; Claude nói máy dùng gói 3D, chưa có kích thước, liên hệ chủ plugin; không cố vẽ kích thước bằng đường | ☐ | ☐ | GOI-2 |
| I3 | ★ | (gói 3D) Chạy một macro cũ có ghi chữ hoặc kích thước | Macro không chạy lệnh nào; thẻ lỗi tiếng Việt "Không chạy macro …: máy này dùng gói 3D, mà macro có lệnh ngoài gói (…). Bản vẽ không bị thay đổi." | ☐ | ☐ | GOI-3 |
| I4 | ★ | Đổi ô Gói thành `Full`, Kiểm tra lại, gõ lại câu ở I2 | Toast "Gói đã đổi sang Full"; có kích thước; cuộc chat cũ vẫn còn | ☐ | ☐ | GOI-4 |
| I5b | ★ | (gói 3D) Gõ "tạo block nhãn phòng có thuộc tính TEN = Phòng khách cao 300 rồi chèn vào" | Không có thuộc tính / chữ: Claude nói gói 3D không có thuộc tính block (chữ), đề nghị block không thuộc tính hoặc liên hệ chủ plugin | ☐ | ☐ | GOI-7 |
| I5c | ○ | (máy offline, gói đang 2D dán ngày mới) Dán lại dòng `CCF1` Full cũ hơn | Báo "Dòng gói này ngày …, cũ hơn dòng gói máy đang có", gói vẫn 2D | ☐ | ☐ | GOI-8 |
| I5d | ★ | Máy từng được cấp mã miễn kiểm tra, rồi cậu đổi ô Trạng thái thành `Disabled`, máy (có mạng) Kiểm tra lại → thẻ 🔒. Dán lại mã miễn kiểm tra cũ vào ô **Dùng mã** (và thử ⚙ ▸ Dán mã của chủ plugin, chạy lại bộ cài với mã đó) | Máy vẫn khóa; báo "Mã này … cũ hơn trạng thái máy đang có; liên hệ chủ plugin để nhận mã mới" (hoặc "đã bị chủ plugin tạm khóa …" nếu cùng ngày). Đổi lại `Active` → Kiểm tra lại → dùng được | ☐ | ☐ | GOI-9 |
| I5 | ○ | Máy đang ở gói **Full** với dòng Full ghi từ **hôm trước** (ngày tính theo UTC: làm I4 rồi I5 sau 7h sáng hôm sau, giờ Việt Nam): đặt `%LocalAppData%\ClaudeCAD\plan.txt` chỉ đọc, đổi ô Gói sang `3D`, Kiểm tra lại. Rồi bỏ chỉ đọc, Kiểm tra lại | Lúc `plan.txt` chỉ đọc: **Gói: 2D** (dòng gói mới không lưu được, dòng Full cũ không còn được dùng), **không bao giờ còn Full**. Bỏ chỉ đọc: **Gói: 3D**. (Dòng Full ghi **cùng ngày** UTC thì máy giữ Full tới hết ngày đó: sót nhỏ đã chấp nhận, xem bàn giao gói dịch vụ. Đổi ô sang `2D` như trước không phân biệt được đúng / sai, vì 2D cũng là mặc định) | ☐ | ☐ | GOI-5 |
| I6 | ★ | Máy đang có **ClaudeCAD bản gốc** (v0.9.3, đã kích hoạt, có vài cuộc chat): đóng AutoCAD, chạy bộ cài bản Full | Hộp thoại "Máy này đang có ClaudeCAD (bản gốc, chỉ 2D), phiên bản 0.9.3 … Lịch sử chat và cài đặt được giữ; key cũ không dùng được…", hai nút **Tiếp tục** / **Hủy** → **Tiếp tục** → trang **Kích hoạt** hiện lại, mã máy như cũ → cài xong. Mở AutoCAD: khung chat tên **Agent to AutoCAD Full**, ⚙ ghi "Agent to AutoCAD Full 0.9.x", cuộc chat cũ còn, không phải đăng nhập Claude lại; thẻ 🔒 Kích hoạt tới khi chủ plugin duyệt ở bảng thiết bị mới và chọn gói → **Kiểm tra lại** → "Gói: …". Windows ▸ Apps chỉ có một mục "Agent to AutoCAD Full". (Bấm **Hủy** ở hộp thoại thì bộ cài thoát, bản gốc giữ nguyên) | ☐ | ☐ | GOI-6 |

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
| J16 | ○ | "xoay biên dạng nửa hình tròn quanh trục cắt qua nó"; rồi "giao 3 khối" với 2 khối đầu tách rời; rồi `set_view_3d` với handle đã xóa | Revolve báo biên dạng cắt trục; giao báo các khối không giao nhau, bản vẽ không đổi; góc nhìn báo không tìm thấy handle | ☐ | ☐ | KT-28/29/30 |

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
| K22 | ★ | Hai hộp 1000×1000×1000 chồng một phần và **chung một mặt phẳng**: hộp A tại (0,0,0), hộp B tại (500,0,0) (cùng đáy z = 0, cùng mặt y = 0); "kiểm tra va chạm A và B" | `interfere: true`, cặp A–B có trong danh sách (thể tích ≈ 0,5 m³, hoặc `volume: null` kèm ghi chú nếu AutoCAD không đo được); **không** báo "chỉ chạm" / không va chạm | ☐ | ☐ | 3D2-22 |
| K23 | ○ | "đặt UCS có trục X theo hướng (1,1,0), trục Y theo (0,0,1), xem thẳng vào mặt phẳng đó (plan_view)" | Biểu tượng UCS: trục X nằm ngang chỉ sang phải màn hình, Y chỉ lên | ☐ | ☐ | 3D2-23 |
| K24 | ○ | Dựng hộp, "liệt kê cạnh của hộp"; tay dời hộp một đoạn lẻ (vd. 12,345 mm) hoặc xoay 37° quanh Z; rồi "bo cạnh số 3 R50" (Claude dùng `topology_id` cũ) | Bo được ngay, đúng cạnh số 3, không báo "solid has changed" | ☐ | ☐ | 3D2-24 |
| K25 | ○ | Mô hình nhiều tường chạm nhau ở góc (vd. 30 tường quanh nhiều phòng, chỉ chạm, không chồng), cộng 1 dầm xuyên qua một tường; "kiểm tra va chạm tất cả" | Chỉ cặp dầm–tường là va chạm; các cặp tường chạm nhau là "chạm" hoặc "chưa xác nhận" (Claude nói rõ, gợi ý kiểm nhóm nhỏ hơn), **không** bị báo là va chạm | ☐ | ☐ | 3D2-25 |

Còn chỉ thấy trên AutoCAD thật: `FilletEdges` với setback 0, căn tiết diện của `sweep` (tâm tiết diện đặt vào đầu
đường dẫn), U hoàn tác `set_ucs` (K10), quy ước mảng của `Matrix3d(double[])` (plugin tự kiểm, K21).

## L. 3D đợt 3 (bản Full, gói Full, ~18 phút)

Máy ở gói `Full` (L2 vẽ mặt bằng 2D bằng skill `ve-kien-truc`; ở gói 3D skill này không nạp, xem T1 trong `checklist-tong-the.md`). Bản vẽ mới (acadiso.dwt, đơn vị mm), tab Model. Chi tiết:
[`ban-giao/ve-3d-dot-3.md`](ban-giao/ve-3d-dot-3.md); L17–L23 từ test & sửa: [`ban-giao/ve-3d-test-3.md`](ban-giao/ve-3d-test-3.md).

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| L1 | ★ | Gõ `CLAUDECHATSELFTEST` | Hai dòng mới ✓: "dựng 3D phòng 4×3 m có cửa đi" (≈ 8,022 m³), "cắt ngang tường ở cao 1 m" (có đường bao, 1 hatch, ≈ 2,62 m²) | ☐ | ☐ | 3D3-1 |
| L2 | ★ | Dùng skill `ve-kien-truc` (gói Full; ở gói 3D skill này không nạp, Claude vẽ theo mục 0 của `ve-3d-kien-truc`, không có kích thước): "vẽ phòng 4x6m tường 220 cửa đi 900 ở cạnh dài" (ví dụ mục 7 của skill). Lượt sau: "dựng 3D mặt bằng này, tường cao 3,3 m" | Claude chạy thử (`dry_run`) rồi báo 4 tường 220, 1 cửa đi, không có gì bị bỏ qua; dựng khối trên `A-WALL-3D`, thể tích ≈ **14,08 m³**, lỗ cửa cao 2200 có lanh tô phía trên; Claude tự xoay iso Conceptual và chụp. Các nét 2D của mặt bằng không đổi (handle, layer như cũ) | ☐ | ☐ | 3D3-2 |
| L3 | ★ | Ngay sau L2: "dựng 3D lại mặt bằng này" | Claude báo mặt bằng đã dựng (liệt kê khối cũ) và hỏi xóa khối cũ / dựng thêm; không tự tạo khối trùng | ☐ | ☐ | 3D3-3 |
| L4 | ★ | Thêm vào mặt bằng một cung tròn và một đường đơn trên `A-WALL`, rồi "dựng 3D lại, xóa khối cũ trước" | **Thẻ xác nhận** liệt kê các khối cũ (handle) **trước** khi xóa (khối 3D có trước lượt luôn qua thẻ, chốt 12/11/2026); bấm **Đồng ý** mới xóa, gõ "ok" thay vì bấm thì Claude hiện lại thẻ. Sau đó Claude báo 2 đối tượng bị bỏ qua kèm lý do (tường cong; không có nét song song) và link handle; phần còn lại dựng như L2 | ☐ | ☐ | 3D3-4 |
| L5 | ★ | Mặt bằng có một khe hở 1200 trong tường không có ký hiệu cửa: "dựng 3D" | Claude báo khe hở (vị trí, rộng 1200) và hỏi; trả lời "đó là cửa sổ" → cửa sổ bậu 900, cao 1500 (tường dưới bậu và trên lanh tô) | ☐ | ☐ | 3D3-5 |
| L6 | ○ | Cửa là block `CUA-DI-900` chèn đè lên tường liền (không có khe) | Lỗ cửa rộng ≈ 900 được khoét đúng chỗ block (báo `from: symbol`) | ☐ | ☐ | 3D3-6 |
| L7 | ★ | Vẽ polyline kín trên layer `A-SLAB` bao ngoài phòng L2 và một polyline kín nhỏ bên trong (lỗ cầu thang), "dựng 3D có sàn dày 120" | Sàn trên `A-SLAB-3D`, mặt trên ở z = 0, dày 120, có lỗ; `area_m2` = diện tích ngoài − lỗ | ☐ | ☐ | 3D3-7 |
| L8 | ○ | Bản vẽ đơn vị mét (`INSUNITS` = 6), mặt bằng vẽ bằng mét (tường 0.22): "dựng 3D, tường cao 3,3 m" | Nhận được tường 220 mm; khối cao 3,3 (đơn vị bản vẽ), báo `units: meters` | ☐ | ☐ | 3D3-8 |
| L9 | ★ | Sau L2: "vẽ mặt cắt đứng qua cửa đi, có nét nhìn thấy phía sau" | Đường bao vết cắt (`A-SECT`) + hatch (`A-SECT-PAT`) nằm **bên phải** mô hình, không đè lên; nét phía sau (`A-SECT-BACK`) là phần tường **phía Claude nói là "nhìn sang"**, không phải phía ngược lại; tường cắt ngang dày 220 cao 3300. Ghi lại nếu kết quả có ghi chú "behind lines could not be made" | ☐ | ☐ | 3D3-9 |
| L10 | ★ | "vẽ mặt bằng cắt ở cao 1 m của khối tường" | Một vòng tường kín có khe cửa, hatch không tô vào trong phòng; `cut_area_m2` ≈ 4,20 | ☐ | ☐ | 3D3-10 |
| L11 | ★ | "vẽ mặt đứng phía trước của mô hình" | Hình chiếu 2D trên `A-ELEV` cạnh mô hình; kết quả báo `hidden_lines_removed: true` (nét khuất đã bỏ: cạnh của tường phía sau không vẽ chồng lên mặt tường trước, chỉ thấy qua lỗ cửa). Nếu báo `edges` thì gửi lại câu ghi chú | ☐ | ☐ | 3D3-11 |
| L12 | ★ | Sau L9–L11: gõ U một lần; rồi `list_entities` (hỏi Claude "trong bản vẽ có đối tượng SECTIONOBJECT nào không?") | U hoàn tác cả lượt cuối; không còn đối tượng Section tạm nào; không có hộp thoại nào hiện ra trong lúc cắt | ☐ | ☐ | 3D3-12 |
| L13 | ★ | Máy gói `2D`: "dựng 3D mặt bằng này" và xem log (`CLAUDECHATLOG`) | Claude nói máy dùng gói 2D, không thử lệnh khác; dòng "skills loaded:" **không** có `ve-3d-kien-truc` (kể cả khi đã bấm + cho đoạn chat) | ☐ | ☐ | 3D3-13 |
| L14 | ★ | Bật "Chỉ xem", "dựng 3D mặt bằng này" | Claude báo đang "Chỉ xem", bản vẽ không đổi | ☐ | ☐ | 3D3-14 |
| L15 | ○ | AutoCAD không phải tiếng Anh (Đức / Pháp…): L2, L9 | Như L2, L9 (tên mẫu hatch ANSI31 giống nhau ở mọi ngôn ngữ) | ☐ | ☐ | 3D3-15 |
| L16 | ○ | Lưu macro lượt L2 + L9 ("Lưu thành macro"), mở bản vẽ khác có cùng mặt bằng, chạy macro | Dựng + cắt trên mặt bằng mới, không đụng bản vẽ cũ | ☐ | ☐ | 3D3-16 |
| L17 | ★ | Vẽ 2 tường song song dày 200 (2 nét mỗi tường, đóng đầu) cách nhau 400 trên `A-WALL`, "dựng 3D mặt bằng này" | Claude chạy thử, báo 2 tường 200 và 1 mục bỏ qua "space between two walls"; khối **không** lấp khe 400 | ☐ | ☐ | 3D3T-1 |
| L18 | ○ | Tường chung mặt ngoài đổi dày 200 → 100 giữa chừng (có nét bậc); và 2 polyline bề rộng 100 / 300 gặp nhau thành góc L; "dựng 3D" | Nhìn Top: khối đúng bằng nét vẽ, không lồi ra ở chỗ đổi dày hay ở góc | ☐ | ☐ | 3D3T-2 |
| L19 | ★ | Vẽ phòng L2 bằng polyline có bề rộng 25 (nét in) thay cho LINE, "dựng 3D" | Vẫn 4 tường 220, 1 cửa; ghi chú "read as plain wall face lines" | ☐ | ☐ | 3D3T-3 |
| L20 | ★ | Trong một tường liền, vẽ cửa sổ bằng 2 nét song song giữa hai mặt tường, trên `A-WALL`; "dựng 3D" | Claude báo 2 nét đó bị bỏ qua ("inside wall… window drawn on the wall layer?") và hỏi; không âm thầm mất cửa sổ | ☐ | ☐ | 3D3T-4 |
| L21 | ○ | Đường bao sàn L7 copy tại chỗ sang layer `A-FLOR`, "dựng 3D có sàn" | Một sàn; báo 1 mục "the same outline" | ☐ | ☐ | 3D3T-5 |
| L22 | ★ | Sau L2: "chạy thử dựng 3D lại mặt bằng này" | Báo cáo chạy thử có `existing_solids`; Claude hỏi trước khi dựng | ☐ | ☐ | 3D3T-6 |
| L23 | ○ | "thêm cửa sổ rộng 1200 tại góc phòng" (điểm đúng góc trong của 2 tường) | Claude báo điểm nằm ở chỗ hai tường gặp nhau và hỏi lại; không dựng gì | ☐ | ☐ | 3D3T-7 |
| L24 | ★ | **(KT-14)** Phòng L2, chưa dựng: "dựng 3D tầng 1, tầng cao 3,3 m, sàn 120, có sàn" rồi lượt sau "dựng tầng 2 cùng mặt bằng này" | Tầng 2 dựng được ngay (không báo "already built", **không hỏi xóa tầng 1**); tường tầng 1 cao 3180 (3300 − 120), sàn tầng 2 từ z 3180 tới 3300 nằm trên đỉnh tường tầng 1, không chồng vào tường; tường tầng 2 từ 3300 tới 6480. Chạy lại đúng lệnh tầng 2 lần nữa → Claude báo tầng 2 đã dựng và hỏi, vẫn không gợi ý xóa tầng 1 | ☐ | ☐ | KT-14 |
| L25 | ★ | **(KT-11)** Phòng có cửa đi đặt sát góc (khe từ mặt trong tường bên cạnh, bản lề ở góc), "dựng 3D" | Lỗ cửa và lanh tô nằm ở **tường có khe**; tường bên cạnh liền, không bị khoét | ☐ | ☐ | KT-11 |
| L26 | ○ | **(KT-15)** "chạy thử dựng 3D mặt bằng này" (chỉ chạy thử) | Không có thông báo "Đã lưu phiên bản", không có nút "Hoàn tác lượt này"; chạy được cả khi bật "Chỉ xem" | ☐ | ☐ | KT-15 |
| L27 | ○ | **(KT-16)** Bản vẽ đơn vị mét (L8): "dựng tầng 2" | Tầng 2 đặt ở cao 3,3 (đơn vị bản vẽ), không phải 3300 | ☐ | ☐ | KT-16 |
| L28 | ○ | **(KT-22, KT-20)** Đường bao sàn có lỗ cầu thang và một đảo bên trong lỗ; block `CUA-DI-900` chèn trên layer `A-WALL` | Đảo là sàn riêng (không bị trừ); block được nhận là cửa đi | ☐ | ☐ | KT-22 |

Còn chỉ thấy trên AutoCAD thật (đã viết an toàn nhất có thể, chưa kiểm được trên máy cloud):

- `Section.GenerateSectionGeometry` (L9, L11): thứ tự 5 mảng kết quả (tên tham số trong API .NET lệch so với
  ObjectARX nên plugin chỉ bật phần "background" và lấy mọi mảng như nhau), phía "phía sau" theo `ViewingDirection`,
  kết quả 2D nằm trên mặt phẳng cắt hay đã trải phẳng (plugin tự chiếu lên mặt phẳng nếu cần), có cho hình chiếu khi mặt
  phẳng nằm ngoài mô hình không (không thì tự lùi về chiếu mọi cạnh, báo `edges`). Lỗi ở phần này chỉ làm mất nét phía
  sau, vết cắt vẫn có.
- `Solid3d.GetSection` + `Region.Explode` (L1, L9, L10): đường bao cung tròn / spline; hatch kiểu Normal để trống phần
  trong vòng.
- Cộng hàng trăm hộp tường (L2 với mặt bằng lớn): thời gian; khối nào AutoCAD không cộng được thì để riêng và báo.

## M. Sau kiểm tra độc lập (bản Full, ~33 phút)

*12/11/2026 · từ báo cáo [`ban-giao/kiem-tra-doc-lap.md`](ban-giao/kiem-tra-doc-lap.md), mục "Điểm chỉ kiểm được trên
AutoCAD thật", và các lỗi đã sửa cần thử thật. M1–M2 và M10–M12 làm cùng phần I.*

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| M1 | ★ | Máy **rút mạng**, chưa kích hoạt. Chủ plugin chạy `taoKeyThuCong` (email có 3 dòng `CCL1…`, `CCS1…`, `CCF1…`). Chạy bộ cài, dán **cả 3 dòng** vào ô key (ô nhiều dòng), Next | Ô giữ đủ 3 dòng; cài xong mở AutoCAD dùng được ngay, hiện đúng gói ghi trong email, **không** thẻ 🔒. Lỗi: ghi lại ô giữ gì sau khi dán, gửi `%LocalAppData%\ClaudeCAD\status.txt` và `plan.txt` (không gửi `license.txt`) | ☐ | ☐ | KT-01 |
| M2 | ○ | Máy bản gốc 0.9.x: gõ `CLAUDECHATUPDATE` (tải xong bản cập nhật), **để AutoCAD mở**, chạy bộ cài Full, bấm **Tiếp tục** (bộ cài tự đóng AutoCAD) | Sau cài: khung chat tên "Agent to AutoCAD Full", `Contents\edition.json` còn; mở lại AutoCAD vẫn là bản Full (không quay về bản gốc). Log cài đặt có dòng "removed the updater script…" / "removed the update staged…" | ☐ | ☐ | KT-07 |
| M10 | ★ | Máy **rút mạng**, cài bằng bộ cài **không** nhập key (Next → Yes). Mở AutoCAD → thẻ **Kích hoạt**; dán **cả 3 dòng** email `taoKeyThuCong` vào ô key, bấm **Kích hoạt** | Ô giữ đủ 3 dòng (ô cao lên, có thanh cuộn); "Đã kích hoạt …"; dùng được ngay, **không** thẻ 🔒, thanh trên hiện đúng gói trong email. Enter = Kích hoạt, Shift+Enter xuống dòng | ☐ | ☐ | KT-41 |
| M11 | ★ | Máy đã kích hoạt, rút mạng. Đóng AutoCAD, xóa `%LocalAppData%\ClaudeCAD\status.txt`, mở AutoCAD → thẻ 🔒 "Chưa xác nhận được máy này…". Dán 2 dòng `CCS1…` + `CCF1…` (hoặc cả 3 dòng) của email vào ô dưới chữ "Máy không có mạng…", bấm **Dùng mã** | Ô giữ đủ các dòng; toast "ClaudeCAD đã được mở khóa", thẻ 🔒 biến mất, chat dùng được, đúng gói. Dán mã của máy khác: báo lỗi trên thẻ, vẫn khóa | ☐ | ☐ | KT-41 |
| M12 | ○ | Máy đang dùng, không mạng (gói 3D). Chủ plugin đổi ô Gói sang `Full`, chạy lại `taoKeyThuCong`. ⚙ ▸ **Tài khoản AI** ▸ **Dán mã của chủ plugin**, dán dòng `CCF1` mới, **Dùng mã**. (Có mạng: ⚙ ▸ Tài khoản AI ▸ **Kiểm tra lại**) | Thẻ "Dán mã của chủ plugin" hiện; "Đã lưu gói Full cho máy này.", toast "Gói đã đổi sang Full", thanh trên "Gói: Full". Đây cũng là chỗ dán cho I5c. Kiểm tra lại: toast trạng thái máy | ☐ | ☐ | KT-41 |
| M3 | ★ | Gói Full, một hộp dựng ở lượt trước. (a) "xóa hộp đó" → khi thẻ hiện, **gõ** "ok" thay vì bấm. (b) Bấm **Đồng ý** trên thẻ. (c) Hộp khác ở lượt trước: "sao chép hộp, khoét một lỗ trên bản sao rồi xóa hộp gốc" | (a) Hộp còn nguyên; Claude hiện lại thẻ, nhắc bấm Đồng ý. (b) Hộp bị xóa. (c) Bản sao khoét được không hỏi (khối mới); xóa hộp gốc thì có thẻ | ☐ | ☐ | KT-09, KT-12 |
| M4 | ○ | Gói Full: lượt J2 (tường + cửa) → "Lưu thành macro"; lượt sau "khoét thêm cửa sổ vào tường" (đồng ý trên thẻ) → lưu thành macro thứ hai; chạy macro thứ hai | Macro thứ hai **không chạy** (không hỏi chọn điểm): thẻ lỗi "Macro này sửa khối 3D có sẵn trong bản vẽ… không chạy", bản vẽ không đổi. Macro thứ nhất vẫn chạy bình thường | ☐ | ☐ | KT-10 |
| M5 | ○ | `draw_solid` `pyramid` (trước đây `draw_pyramid`) 4 cạnh, bán kính 500, rotation 0 ("dựng chóp đáy vuông cạnh 1000") | Hộp bao ±500 theo X / Y (đáy vuông theo trục, không xoay 45°), đáy ở z của tâm | ☐ | ☐ | KT (Pyramid) |
| M6 | ○ | Dựng hộp, "liệt kê cạnh và mặt"; lưu bản vẽ, mở lại (hoặc mở bằng đời AutoCAD khác), "liệt kê cạnh và mặt" của cùng khối | Thứ tự và số cạnh / mặt giữ nguyên (macro bo cạnh theo số dựa vào đây) | ☐ | ☐ | KT (Brep) |
| M7 | ○ | Bật 🔒 Chỉ xem, "xem góc SW iso", rồi đóng bản vẽ | Ghi lại: AutoCAD có hỏi lưu không (đổi góc nhìn có thể làm bản vẽ "đã sửa") | ☐ | ☐ | KT (Chỉ xem) |
| M8 | ○ | Khóa layer của một khối, "xoay khối 90 độ quanh trục X" và "đối xứng khối qua mặt YZ" | Câu báo dễ hiểu (layer bị khóa, hỏi mở khóa); ghi lại câu Claude nói | ☐ | ☐ | KT (khóa layer) |
| M9 | ○ | Sau L9 / L11: gõ `PURGE` | Không có đối tượng lạ (không còn `SectionSettings` / đối tượng tạm của mặt cắt) | ☐ | ☐ | KT (Section) |

## N. Nâng cấp đợt A (bản Full, gói 3D hoặc Full, ~34 phút)

*13/11/2026 · từ [`ban-giao/nang-cap-a.md`](ban-giao/nang-cap-a.md). Máy ở gói `3D` hoặc `Full`, bản vẽ mới
(acadiso.dwt), tab Model. N0: tự kiểm tra; N1–N3: 6 lệnh dựng khối gộp thành một; N4–N7: layer tên khác; N8–N9: cột;
N10–N12: nhiều tầng; N13–N14: lan can; N15–N16: mảng nhiều tầng; N17–N19: thêm sau bước Test & sửa đợt A. **Quan trọng nhất: N0, N4, N5, N10.***

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| N0 | ★ | Gõ `CLAUDECHATSELFTEST` | Hai dòng mới ✓: "Nâng cấp đợt A: dựng 2 tầng + sàn mái + 2 cột từ mặt bằng…" (tường 2 tầng ≈ 15,204 m³, 2 cột × 2 tầng ≈ 0,9159 m³, 2 sàn + sàn mái = 6,048 m³) và "Nâng cấp đợt A: lan can thẳng 3 m…" (≈ 0,03139 m³). Dòng ✗ thì gửi file báo cáo | ☐ | ☐ | A3–A5 |
| N1 | ★ | "dựng hộp 1000x500x300, trụ D400 cao 3,3 m, cầu D600, nêm 3000x1200x450, xuyến R500 ống 50, chóp 4 cạnh đáy 1000" | Dòng trạng thái ghi "Dựng khối 3D" (không còn "Dựng khối hộp"…); đủ 6 khối đúng hình ở góc iso; thể tích từng khối đúng (hộp 0,15 m³, trụ ≈ 0,415 m³) | ☐ | ☐ | A1 |
| N2 | ★ | Macro ghi **trước** đợt A có `draw_box` (ghi trên bản thử cũ, hoặc sửa tay một file macro đổi `draw_solid` thành `draw_box` và bỏ `shape`): chạy trên bản mới | Macro chạy, hộp dựng như cũ (tên cũ vẫn chạy trong plugin) | ☐ | ☐ | A1 |
| N3 | ○ | Gói `2D`: chạy macro N2 | Thẻ "Không chạy macro… gói 2D… (draw_box)"; bản vẽ không đổi | ☐ | ☐ | A1 |
| N4 | ★ | Vẽ phòng L2 nhưng tường trên layer `TUONG`, cửa đi trên `CUA`, cửa sổ trên `CUA SO`. Gõ "dựng 3D mặt bằng này" (không nhắc tên layer) | Claude chạy thử, nói ánh xạ đoán được và hiện **thẻ xác nhận có ghi đủ tên layer** (tường = TUONG, cửa đi = CUA, cửa sổ = CUA SO); **chưa dựng gì** | ☐ | ☐ | A2 |
| N5 | ★ | Sau N4 bấm **Đồng ý** | Dựng đúng như L2 (tường có lỗ cửa); khối trên `A-WALL-3D`. Lượt sau "dựng tầng 2 cùng mặt bằng": dựng ngay, **không hỏi lại layer** | ☐ | ☐ | A2 |
| N6 | ○ | Làm lại N4 (bản vẽ khác hoặc chat mới) rồi bấm **Hủy** | Không dựng gì. Gõ tiếp "tường ở layer TUONG, cửa ở layer CUA, dựng đi": Claude dựng theo đúng layer cậu nêu, không cần thẻ | ☐ | ☐ | A2 |
| N7 | ○ | Chat mới, cùng bản vẽ N4, gõ không dấu "dung 3d mat bang nay, tuong cao 3m" | Vẫn hiện thẻ hỏi layer (chữ "tuong" trong câu không được coi là tên layer `TUONG`) | ☐ | ☐ | A2 |
| N8 | ★ | Thêm 4 cột 220x220 (`draw_rectangle`) và 1 cột tròn D300 trên layer `A-COL` ở góc phòng L2 (chưa dựng). "dựng 3D, tầng cao 3,3 m, sàn 120" | Có 5 khối cột trên `A-COLS-3D` cao 3180 (bằng tường), đáy ở z 0; hatch trên `A-COL` không gây lỗi; Claude báo số cột và kích thước | ☐ | ☐ | A3 |
| N9 | ○ | Vẽ một hình chữ nhật 5000x4000 trên `A-COL`, "chạy thử dựng 3D" | Hình đó bị bỏ qua với lý do "larger than a column"; không thành khối cột khổng lồ | ☐ | ☐ | A3 |
| N10 | ★ | Phòng L2 chưa dựng, **có đường bao sàn** (hình kín trên `A-SLAB`; chưa có thì vẽ thêm một hình chữ nhật theo mép ngoài tường): "dựng 3 tầng, mỗi tầng cao 3,3 m, sàn 120" | Một lượt dựng 3 tầng: tường mỗi tầng cao 3180 ở z 0 / 3300 / 6600; sàn tầng 2, 3 nằm trên đỉnh tường tầng dưới (z 3180–3300, 6480–6600), không chồng vào tường; **không hỏi xóa gì**; không chờ lâu hơn hẳn dựng 1 tầng; gõ U một lần mất cả 3 tầng | ☐ | ☐ | A4, L24 |
| N11 | ○ | Sau N10: "dựng thêm tầng 4" | Dựng ngay ở z 9900, không báo "already built", không hỏi xóa tầng dưới. Gõ lại câu N10 → Claude báo các tầng đã dựng và hỏi, không gợi ý xóa tầng khác | ☐ | ☐ | A4, L24 |
| N12 | ○ | Bản vẽ đơn vị mét (L8): "dựng 2 tầng, mỗi tầng 3,3 m, có sàn mái" | Tầng 2 ở cao 3,3 (đơn vị bản vẽ), sàn mái trên cùng ở 6,6; không có số 3300 | ☐ | ☐ | A4, L27 |
| N13 | ★ | Vẽ polyline ban công 3 cạnh (3000 + 1200 + 3000). "dựng lan can theo đường này" | Một khối trên `A-RAIL-3D` cao **1100**: tay vịn trên cùng, trụ ở mỗi góc và cách ≤ 1500, song đứng khe ≤ 100; polyline vẫn nguyên | ☐ | ☐ | A5 |
| N14 | ○ | "lan can kính cao 1200 trên sàn tầng 2" (cùng polyline) | Tấm kính (không song), cao 1200, đáy ở z 3300 | ☐ | ☐ | A5 |
| N15 | ★ | Dựng một cột 400x400 cao 3300, "lặp cột này 3 hàng x 4 cột cách 6 m x 4 m, 3 tầng cách 3,3 m" | 36 cột (35 bản sao mới), tầng trên đặt đúng cao 3300 / 6600 | ☐ | ☐ | A6 |
| N16 | ○ | Gói `2D`: "lặp hình chữ nhật này 2 hàng x 2 cột, 3 tầng cách 3000" | Claude nói số tầng thuộc gói 3D (mảng 2D thường vẫn làm được nếu cậu bỏ số tầng) | ☐ | ☐ | A6 |
| N17 | ★ | Phòng **không có đường bao sàn** (không có hình kín trên `A-SLAB` / `A-FLOR`), chưa dựng: "dựng 3 tầng, mỗi tầng cao 3,3 m" | **Chưa dựng gì**; Claude hỏi cậu chọn: sàn theo khung bao tường, chỉ đường bao sàn, hay không làm sàn (tường cao hết tầng). Chọn xong mới dựng, các tầng **không hở nhau** 150 mm | ☐ | ☐ | NA-3 |
| N18 | ○ | "dựng lan can từ điểm 0,0 tới điểm 5000000,0" (5 km, gõ nhầm số) | Claude báo đường quá dài (quá 2000 mảnh), không dựng gì; AutoCAD **không đứng máy** | ☐ | ☐ | NA-2 |
| N19 | ○ | Lan can dài: polyline thẳng 60 m, "dựng lan can theo đường này" | Dựng xong trong dưới 2 phút, không báo "hết giờ", không dựng 2 lần. Lâu hơn hoặc lỗi thì ghi lại thời gian (để hạ giới hạn 2000 mảnh) | ☐ | ☐ | A5 |

Còn chỉ thấy trên AutoCAD thật: sao chép khối tầng 1 lên các tầng trên (N0, N10); cộng hàng trăm hộp của lan can dài
(N13 với đường rất dài: thời gian; mảnh không cộng được để riêng, báo `separate_pieces`); cột hình tròn / polyline có
cung (đùn từ region).

## O. Nâng cấp đợt B — sửa hình 2D (bản Full, gói 2D hoặc Full, ~30 phút)

*13/11/2026 · từ [`ban-giao/nang-cap-b.md`](ban-giao/nang-cap-b.md). Máy ở gói `2D` hoặc `Full` (O14, O15: gói `3D`),
bản vẽ mới (acadiso.dwt), tab Model. Sau mỗi dòng gõ U một lần: mọi thay đổi của lượt đó mất hết. **Quan trọng nhất: O0,
O1, O4, O6, O14.***

| # | | Làm gì | Kết quả mong đợi | Đạt | Lỗi | Mã gốc |
| --- | --- | --- | --- | --- | --- | --- |
| O0 | ★ | Gõ `CLAUDECHATSELFTEST` | Dòng mới ✓ "Nâng cấp đợt B: bo 4 góc polyline, kéo dài, bẻ gãy, chia đều, nối có dung sai, kéo giãn" (chu vi bo góc ≈ 282,832). Dòng ✗ thì gửi file báo cáo | ☐ | ☐ | B1–B9 |
| O1 | ★ | Vẽ hình chữ nhật 6000x3000. "kéo giãn cạnh phải hình này sang phải 1000" | Hình thành 7000x3000, cạnh trái giữ nguyên, vẫn cùng handle; chữ / đối tượng nằm hẳn trong khung thì được dời theo | ☐ | ☐ | B1 |
| O2 | ○ | Vẽ 30 đoạn thẳng ngắn cạnh nhau rồi "kéo giãn tất cả đầu bên phải sang phải 500" | Claude **hỏi trước** (nói số đối tượng), chỉ làm sau khi cậu đồng ý | ☐ | ☐ | B1 |
| O3 | ★ | Vẽ đường thẳng 5000. "bẻ gãy đường này ở giữa" rồi "cắt bỏ đoạn từ 1000 tới 2000 của đoạn đầu" | Lần 1: 2 đoạn 2500; lần 2: còn 1000 + 500, khoảng hở 1000. Vẽ đường tròn rồi "cắt bỏ một phần tư đường tròn" → còn cung 3/4 | ☐ | ☐ | B2 |
| O4 | ★ | Vẽ đường thẳng 3000 và cung bán kính 1000. "kéo dài đường thẳng thêm 500", "rút ngắn cung còn 80%", "cho đường thẳng dài đúng 4200" | Đường 3500 rồi 4200 (chỉ một đầu đổi); cung ngắn lại, tâm và bán kính giữ nguyên | ☐ | ☐ | B3 |
| O5 | ○ | Vẽ polyline 2 đoạn (đoạn cuối là cung). "kéo dài đầu cuối thêm 300" | Cung cuối dài thêm theo đúng đường tròn của nó, polyline vẫn một đối tượng | ☐ | ☐ | B3 |
| O6 | ★ | Vẽ đường thẳng 6000. "chia đường này làm 6 phần" rồi "cứ 1500 đặt một block cọc (block có sẵn) dọc đường" | 5 điểm chia (thấy rõ nếu kiểu điểm PDMODE đã đổi; nếu không Claude nhắc đổi kiểu điểm); 3 block cọc xoay theo đường. Đường không bị đổi | ☐ | ☐ | B4 |
| O7 | ★ | Chọn một bộ bàn ghế (vài đối tượng). "nhóm các đối tượng này thành nhóm BAN AN" rồi kéo thử một đối tượng | Cả nhóm chọn cùng nhau (lệnh GROUP của AutoCAD thấy nhóm BAN AN). "bỏ nhóm BAN AN" → chọn riêng từng cái được, đối tượng không đổi | ☐ | ☐ | B5 |
| O8 | ★ | Một đường layer `A-WALL` màu đỏ nét đứt; 5 đường khác layer `0`. "chép thuộc tính đường đỏ sang 5 đường kia" | 5 đường thành layer `A-WALL`, màu đỏ, nét đứt, cùng độ dày nét / tỉ lệ nét | ☐ | ☐ | B6 |
| O9 | ○ | Một chữ kiểu `Standard` cao 250 và vài chữ khác cao 100. "chép thuộc tính chữ cao 250 sang các chữ kia" | Các chữ đổi kiểu chữ và chiều cao, nội dung giữ nguyên. Kích thước: chép kiểu kích thước sang kích thước khác được | ☐ | ☐ | B6 |
| O10 | ★ | Vẽ polyline kín 6000x3000 (một đối tượng). "bo tròn tất cả các góc bán kính 300", rồi trên một polyline khác "vát góc thứ 2 cạnh 200" | Bốn góc bo R300, polyline vẫn một đối tượng cùng handle; lần 2 chỉ đúng một góc bị vát. Góc nào cạnh quá ngắn thì Claude nói rõ góc đó bỏ qua | ☐ | ☐ | B7 |
| O11 | ★ | Vẽ một đường thẳng và một cung chạm nhau thành góc. "bo góc giữa đường và cung bán kính 200" | Có cung bo R200 tiếp xúc cả hai; đường và cung được cắt bớt đúng đến chỗ tiếp xúc. "vát góc 150" trên cặp khác cũng làm được | ☐ | ☐ | B7 |
| O12 | ★ | Vẽ trục tường (đường thẳng). "vẽ tường 220 tim ở trục này" | Hai đường song song cách trục 110 mỗi bên (một lệnh, không phải hỏi phía) | ☐ | ☐ | B8 |
| O13 | ★ | Vẽ 3 đoạn thẳng nối nhau nhưng hở 0,3 mm ở một chỗ. "nối các đường này thành polyline" | Claude nối được (dung sai, khe được nối bằng một đoạn thẳng ngắn), báo chỗ đã nối khe; không có dung sai thì báo đoạn không nối | ☐ | ☐ | B9 |
| O14 | ★ | Gói `3D`: một khối 3D trên layer `0` và một khối trên `A-WALL-3D` màu xanh. "chép thuộc tính khối xanh sang khối kia" | Làm được (layer, màu…). "chép kiểu chữ của chữ này sang chữ kia" → Claude nói phần chữ / kích thước thuộc gói 2D | ☐ | ☐ | B6 |
| O15 | ○ | Gói `3D`: "nhóm các khối này" / "kéo giãn hình này" | Nhóm làm được; kéo giãn Claude nói thuộc gói 2D | ☐ | ☐ | B5, B1 |
| O16 | ○ | Macro ghi **trước** đợt B có `fillet_entities` / `offset_entity` / `join_entities`: chạy trên bản mới | Macro chạy như cũ (tên lệnh, tham số cũ giữ nguyên) | ☐ | ☐ | B7–B9 |

Còn chỉ thấy trên AutoCAD thật: kéo giãn kích thước / block / chữ (điểm kéo giãn của AutoCAD), cắt đường tròn / elip /
spline kín (thứ tự các mảnh khi tách), chia đường cong spline, nhóm trên layer khóa.

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
