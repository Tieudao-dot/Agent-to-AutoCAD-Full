# Chuẩn bị sẵn máy chủ kích hoạt (làm trước, tạo Sheet sau)

*14/11/2026, thread Bảo mật. Bản rút gọn để chủ dự án làm trước phần chuẩn bị trên máy mình; phần Google Sheet làm sau.
Hướng dẫn đầy đủ: [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md).*

## Chia làm 2 buổi

| Buổi | Việc | Cần Google Sheet? | Thời gian |
| --- | --- | --- | --- |
| **Hôm nay** | A. Cài công cụ · B. Tạo thư mục khóa · C. Tạo cặp khóa · D. Tạo `Code.gs` · E. Sao lưu · F. Gửi khóa công khai | Không | ~15 phút |
| **Hôm khác** | G. Tạo Sheet, dán `Code.gs`, chạy `kiemTra` · H. Triển khai web app · I. Gửi link | Có | ~10 phút |

Làm xong buổi 1 là đủ: `Code.gs` nằm chờ trong thư mục khóa, buổi 2 chỉ việc dán. Gửi khóa công khai (bước F) ngay
được, không cần đợi link: luồng chính gắn khóa trước, link gắn sau.

> **Tại sao tớ không tạo khóa giúp cậu?** Khóa riêng tạo trên máy chủ đám mây của tớ thì coi như đã rời tay cậu. Khóa
> riêng phải sinh ra và nằm **chỉ trên máy của cậu**.

## A. Cài công cụ (một lần)

1. **Node.js** bản LTS (20 trở lên): <https://nodejs.org> → tải → cài, bấm Next tới hết.
2. **Git for Windows** (có sẵn Git Bash và OpenSSL): <https://git-scm.com/download/win> → cài mặc định.
3. Kiểm tra: mở **Git Bash** (menu Start, gõ "Git Bash"), gõ lần lượt:
   ```bash
   node --version
   openssl version
   ```
   Thấy số phiên bản (vd `v22.…`, `OpenSSL 3.…`) là được.

## B. Tạo thư mục khóa và tải repo

1. Tạo thư mục `D:/claudecad-full-khoa/`. **Ngoài** mọi thư mục repo, **không** nằm trong Google Drive / OneDrive /
   Dropbox đồng bộ, **không** nằm trong thư mục chia sẻ.
2. Tải repo nhánh mới nhất **`full/nang-cap-tong-the`** (đăng nhập GitHub trước):
   <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/archive/refs/heads/full/nang-cap-tong-the.zip>.
   Mẫu Apps Script ở nhánh này giống hệt nhánh `full/test-tong-the` ghi trong hướng dẫn cũ, dùng nhánh nào cũng được.
3. Giải nén vào `D:/claudecad-full-khoa/`. Được thư mục có `activation` bên trong, ví dụ
   `D:/claudecad-full-khoa/Agent-to-AutoCAD-Full-full-nang-cap-tong-the`.

## C. Tạo cặp khóa

Trong Git Bash:

```bash
cd D:/claudecad-full-khoa
openssl genrsa -out full-private.pem 2048
```

Được file `full-private.pem`: **khóa riêng**. Ai có file này thì tự làm được giấy phép cho bất kỳ máy nào.

## D. Tạo `Code.gs` đã điền

Thay `<gmail>` bằng Gmail sẽ nhận thông báo (cũng là Gmail cậu dùng để tạo Sheet ở buổi 2):

```bash
cd D:/claudecad-full-khoa/Agent-to-AutoCAD-Full-full-nang-cap-tong-the
node activation/make-apps-script.mjs --private-key D:/claudecad-full-khoa/full-private.pem --email <gmail> --out D:/claudecad-full-khoa/Code.gs
node activation/make-apps-script.mjs --private-key D:/claudecad-full-khoa/full-private.pem --print-public > D:/claudecad-full-khoa/khoa-cong-khai.txt
```

- Lệnh 1 tạo `Code.gs` (có SECRET mới + khóa riêng). Tên thư mục giải nén khác thì sửa dòng `cd` cho khớp.
- Lệnh 2 ghi 2 dòng `PublicModulus = "…"` / `PublicExponent = "…"` vào `khoa-cong-khai.txt`. **Hai dòng này công
  khai**, gửi được.
- Muốn gói mặc định cho máy mới khác `2D` (vd `Full`): **chưa sửa** trong `Code.gs`; báo luồng chính để sửa cùng lúc cả
  plugin (`PlanLogic.DefaultPlan`) và script, không thì hai bên lệch nhau.

Xong bước D, thư mục có:

| File | Bí mật? | Dùng để |
| --- | --- | --- |
| `full-private.pem` | **Có** | Tạo lại `Code.gs` về sau |
| `Code.gs` | **Có** (chứa SECRET + khóa riêng) | Dán vào Apps Script ở buổi 2 |
| `khoa-cong-khai.txt` | Không | Gửi luồng chính |
| thư mục repo đã giải nén | Không | Xóa được sau buổi 2 |

## E. Sao lưu (đừng bỏ qua)

- Chép `full-private.pem` và `Code.gs` sang **một USB** (hoặc ổ ngoài) cất riêng. Mất cả hai thì phải đổi khóa, phát
  hành bản mới, **mọi máy khách kích hoạt lại**.
- Không gửi qua Zalo / email / Drive, không chụp màn hình, không dán vào chat với tớ hay ai.
- Sau này sửa script mà muốn key cũ của khách vẫn dùng được thì cần SECRET cũ: nó nằm ở dòng `var SECRET = '…'` trong
  `Code.gs`. Vì vậy giữ `Code.gs` như giữ khóa riêng.

## F. Gửi khóa công khai (làm ngay được)

Mở `khoa-cong-khai.txt` bằng Notepad, chép 2 dòng gửi luồng chính. Chỉ gửi file này, không gửi file nào khác trong
thư mục.

## Buổi 2 (khi cậu tạo Sheet)

Làm đúng bước 3–5 của [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md):

- **G.** Mở <https://sheets.new> **bằng đúng Gmail ở bước D**, đặt tên `ClaudeCAD Full – Thiết bị` → Tiện ích mở rộng →
  Apps Script → xóa nội dung `Code.gs` trong trình soạn, dán toàn bộ file `D:/claudecad-full-khoa/Code.gs` → 💾 Lưu →
  chọn hàm `kiemTra` → ▶ Chạy → cho phép quyền.
  - Phải thấy: trang **Thiết bị** 10 cột (cột cuối **Gói**), dòng máy thử `CC-1234-5678-9ABC`, email
    "[ClaudeCAD] Máy chủ kích hoạt hoạt động".
- **H.** Triển khai → Tùy chọn triển khai mới → Ứng dụng web; Thực thi với tư cách **Tôi**; Ai truy cập **Bất kỳ ai**
  → Triển khai → chép link `https://script.google.com/macros/s/…/exec`. Thử thêm `?action=ping` vào cuối link trên trình
  duyệt, thấy `{"ok":true,…}`.
- **I.** Gửi link đó cho luồng chính. Link không bí mật nhưng cũng đừng đăng công khai (người lạ gọi được, dù không
  lấy được key).
- Sau đó: xóa dòng máy thử `CC-1234-5678-9ABC` trong Sheet; không chia sẻ Sheet cho ai (ai sửa được Sheet thì tự mở máy
  được).

## Giữa hai buổi

- Luồng chính gắn khóa công khai vào plugin được ngay, nhưng **chưa build bộ cài cho khách** tới khi có link (bộ cài
  phải biết gọi máy chủ nào).
- Bộ cài thử nội bộ (bỏ qua kích hoạt, hết hạn 10/12/2026) vẫn dùng bình thường để cậu thử lệnh.
- Lỡ để lộ `full-private.pem` hoặc `Code.gs` trước buổi 2: chưa ai bị ảnh hưởng, chỉ cần làm lại từ bước C rồi gửi lại
  khóa công khai mới.
