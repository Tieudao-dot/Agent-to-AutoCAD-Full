# Dựng máy chủ kích hoạt riêng cho bản Full

*12/11/2026. Chủ dự án tự dựng; luồng chính chuẩn bị mẫu và gắn kết quả vào repo.*

Bản Full dùng **Google Sheet mới, Apps Script mới và cặp khóa mới**, tách hẳn khỏi dự án gốc. Hệ quả: key và giấy
phép của dự án gốc không dùng được trên bản Full và ngược lại; máy đang dùng bản gốc muốn chuyển sang bản Full phải
xin kích hoạt lại ở Sheet mới.

Cho tới khi xong bước 6, repo vẫn chứa URL và khóa công khai của dự án gốc (xem
[`cho-tro-ve-repo-goc.md`](cho-tro-ve-repo-goc.md)), nên **chưa build bộ cài bản Full để dùng thật**.

## Ai làm gì

| Bước | Việc | Ai |
| --- | --- | --- |
| 1 | Tạo cặp khóa mới trên máy của cậu | Cậu |
| 2 | Tạo file `Code.gs` đã điền từ mẫu | Cậu |
| 3 | Tạo Google Sheet mới, dán `Code.gs`, chạy `kiemTra` | Cậu |
| 4 | Triển khai web app, thử `?action=ping` | Cậu |
| 5 | Gửi tớ 2 thứ **không bí mật**: URL web app và 2 dòng khóa công khai | Cậu |
| 6 | Ghi URL vào `activation/activation.json`, khóa công khai vào `License.cs`, chạy test | Tớ |

Khóa riêng (`*.pem`), SECRET và file `Code.gs` đã điền **không gửi cho tớ, không dán vào chat, không đưa lên GitHub**.

## Bước 1 — Tạo cặp khóa mới

Cần máy có Node.js 20 trở lên và OpenSSL (Git for Windows có sẵn trong Git Bash). Làm trong một thư mục **ngoài**
repo, ví dụ `D:\claudecad-full-khoa`.

```bash
openssl genrsa -out full-private.pem 2048
```

Giữ file `full-private.pem` ở nơi an toàn (và một bản sao lưu). Mất file này thì phải đổi khóa và phát hành bản mới.

## Bước 2 — Tạo `Code.gs` đã điền

Tải repo về (nhánh `claude/inspiring-cray-58xsmt`), rồi từ thư mục repo chạy:

```bash
node activation/make-apps-script.mjs --private-key D:/claudecad-full-khoa/full-private.pem --email <gmail nhận thông báo> --out D:/claudecad-full-khoa/Code.gs
```

Lệnh tự sinh SECRET mới và ghi vào `Code.gs`. Lấy khóa công khai để gửi tớ ở bước 5:

```bash
node activation/make-apps-script.mjs --private-key D:/claudecad-full-khoa/full-private.pem --print-public
```

Lệnh in ra 2 dòng `PublicModulus = "…"` và `PublicExponent = "…"`. Hai dòng này công khai, gửi được.

## Bước 3 — Google Sheet mới + Apps Script

1. Mở <https://sheets.new> bằng Gmail nhận thông báo. Đặt tên khác hẳn bảng cũ, ví dụ `ClaudeCAD Full – Thiết bị`.
2. **Tiện ích mở rộng → Apps Script**. Đặt tên dự án, ví dụ `ClaudeCAD Full kích hoạt`.
3. Xóa hết nội dung `Code.gs` trong trình soạn, dán toàn bộ file `Code.gs` vừa tạo, bấm 💾 **Lưu**.
4. Chọn hàm **`kiemTra`**, bấm ▶ **Chạy**. Google hỏi quyền: **Xem lại quyền** → chọn tài khoản → **Nâng cao** →
   **Đi tới … (không an toàn)** → **Cho phép**.
5. Kết quả phải thấy:
   - trang **"Thiết bị"** có 10 cột, cột cuối là **Gói** với danh sách chọn `2D` / `3D` / `Full`;
   - một dòng máy thử `CC-1234-5678-9ABC`, Trạng thái `Active`, Gói `2D` (xóa được);
   - email "[ClaudeCAD] Máy chủ kích hoạt hoạt động".

## Bước 4 — Triển khai web app

1. **Triển khai → Tùy chọn triển khai mới** → ⚙ → **Ứng dụng web**.
2. Thực thi với tư cách: **Tôi**. Người có quyền truy cập: **Bất kỳ ai**. Bấm **Triển khai**.
3. Sao chép **URL ứng dụng web**, dạng `https://script.google.com/macros/s/…/exec`.
4. Thử trên trình duyệt: thêm `?action=ping` vào cuối URL. Thấy `{"ok":true,"service":"ClaudeCAD activation"}` là được.

## Bước 5 — Gửi tớ

- URL web app ở bước 4.
- 2 dòng `PublicModulus` / `PublicExponent` ở bước 2.

## Bước 6 — Tớ gắn vào repo

- `activation/activation.json`: thay `url`.
- `plugin/ClaudeCAD.Plugin/Core/License.cs`: thay `PublicModulus` (và `PublicExponent` nếu khác).
- Chạy test lõi và test trợ lý; cập nhật `cho-tro-ve-repo-goc.md`.

## Dùng cột Gói

- Máy mới xin kích hoạt tự nhận gói **2D** (hằng `GOI_MAC_DINH` ở đầu `Code.gs`; đổi được, ví dụ `'Full'`).
- Đổi ô **Gói** của một máy: có hiệu lực ở lần máy đó kiểm tra kế tiếp (mở AutoCAD, ~6 giờ một lần, hoặc bấm
  "Kiểm tra lại").
- Ô trống hoặc gõ sai tính là gói mặc định, nên lỗi gõ không mở nhầm 3D.
- Máy không có mạng: chọn gói trong Sheet trước, rồi chạy `taoKeyThuCong`; email có thêm dòng `CCF1|…`. Đổi gói cho
  máy đó thì chạy lại hàm và gửi dòng mới.

**Lưu ý:** hiện mới có phía máy chủ. Plugin **chưa đọc** dòng `CCF1`, nên trước khi làm xong phần gói trong plugin
thì mọi máy vẫn dùng được toàn bộ lệnh như bản 2D.

## Sửa script về sau

Tạo lại `Code.gs` bằng lệnh ở bước 2, **thêm `--secret <SECRET cũ>`** (lấy từ dòng `var SECRET` trong `Code.gs` đang
chạy) để key đã gửi vẫn dùng được. Dán đè → 💾 Lưu → chạy `kiemTra` → **Triển khai → Quản lý triển khai** → ✏️ →
Phiên bản mới → **Triển khai**. URL giữ nguyên.
