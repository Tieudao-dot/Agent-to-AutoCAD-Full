# Dựng máy chủ kích hoạt riêng cho bản Full

*12/11/2026, sửa 13/11/2026 (tải nhánh `full/test-tong-the`). Chủ dự án tự dựng; luồng chính chuẩn bị mẫu và gắn kết
quả vào repo.*

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

## Chuẩn bị (cho bước 1–2)

- **Node.js 20 trở lên**: tải bản LTS ở <https://nodejs.org>, cài như bình thường.
- **OpenSSL**: có sẵn trong **Git Bash** của Git for Windows (<https://git-scm.com/download/win>).
- Chạy **mọi lệnh ở bước 1–2 trong Git Bash** (chuột phải trong thư mục → *Open Git Bash here*, hoặc mở Git Bash rồi
  `cd`). Đường dẫn viết kiểu `D:/…` (dấu `/`) như các ví dụ dưới.
- Làm trong một thư mục **ngoài** mọi repo, ví dụ `D:/claudecad-full-khoa/`. Khóa riêng và `Code.gs` đã điền nằm ở đây,
  không bao giờ đưa lên GitHub.

## Bước 1 — Tạo cặp khóa mới

```bash
mkdir -p D:/claudecad-full-khoa
cd D:/claudecad-full-khoa
openssl genrsa -out full-private.pem 2048
```

Giữ file `full-private.pem` ở nơi an toàn (và một bản sao lưu). Mất file này thì phải đổi khóa và phát hành bản mới.

## Bước 2 — Tạo `Code.gs` đã điền

Tải repo về ở nhánh **`full/test-tong-the`** (mẫu Apps Script mới nhất: cột Gói 2D / 3D / Full, các bản sửa sau
kiểm tra độc lập): <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/archive/refs/heads/full/test-tong-the.zip>
(repo riêng tư thì đăng nhập GitHub trước). **Đừng** dùng nhánh `claude/inspiring-cray-58xsmt`: nhánh đó còn mẫu cũ,
chưa có gói Full. Giải nén vào `D:/claudecad-full-khoa/`, được thư mục `Agent-to-AutoCAD-Full-full-test-tong-the` (tên có thể
hơi khác: là thư mục có `activation` bên trong), rồi trong Git Bash:

```bash
cd D:/claudecad-full-khoa/Agent-to-AutoCAD-Full-full-test-tong-the
node activation/make-apps-script.mjs --private-key D:/claudecad-full-khoa/full-private.pem --email <gmail nhận thông báo> --out D:/claudecad-full-khoa/Code.gs
```

`--out` phải nằm **ngoài** thư mục repo (lệnh từ chối ghi vào trong repo, vì file có SECRET và khóa riêng).

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
- Máy không có mạng: chọn gói trong Sheet trước (máy chưa có dòng thì thêm dòng với mã máy, chọn Gói; không thì gói là
  `GOI_MAC_DINH`). Trong Apps Script sửa dòng `var MAY_THU_CONG = 'CC-…';` thành mã máy, chọn hàm **`taoKeyThuCong`**,
  bấm ▶ **Chạy**. Email gửi về có **KEY** (máy có mạng) và **ba dòng** cho máy không có mạng: `CCL1|…` (giấy phép),
  `CCS1|…|offline|…` (mã miễn kiểm tra), `CCF1|…` (gói). Khách dán **đủ ba dòng** vào một ô. Đổi gói cho máy đó thì
  chạy lại hàm và gửi dòng `CCF1` mới (plugin chỉ nhận dòng gói mới hơn hoặc cùng ngày).
- Đã khóa máy (`Disabled`) rồi cấp lại mã không cần mạng **trong cùng ngày** (giờ UTC: ngày mới bắt đầu lúc 7h sáng giờ
  Việt Nam) thì máy từ chối mã mới (KT-43): đợi sang ngày rồi chạy lại `taoKeyThuCong`, hoặc máy có mạng thì đổi về
  `Active`. Chi tiết: [`activation/README.md`](../activation/README.md).

**Lưu ý (sửa 12/11/2026, KT-31):** plugin bản Full **đã đọc** dòng `CCF1` và chặn lệnh theo gói: máy chỉ dùng được
lệnh của gói ghi trong Sheet. **Chọn ô Gói cho từng máy** (2D / 3D / Full). Ô trống thì máy dùng gói mặc định 2D, tức
không có lệnh 3D. Đổi ô Gói là đổi gói, không cần cài lại. Máy dùng mã không cần mạng thì gói nằm trong dòng `CCF1` của
email: dán đủ các dòng. Ô nào cũng nhận nhiều dòng (sửa 12/11/2026, KT-41): ô key của bộ cài và ô key trên thẻ
**Kích hoạt** trong khung chat (máy chưa kích hoạt), ô **Dùng mã** trên thẻ 🔒 (máy đã kích hoạt nhưng bị khóa, chờ duyệt
hoặc quá 7 ngày không kiểm tra), và ⚙ ▸ **Tài khoản AI** ▸ **Dán mã của chủ plugin** (máy đang dùng, ví dụ đổi gói bằng
dòng `CCF1` mới). Máy có mạng thì bấm **Kiểm tra lại** (cùng chỗ trong ⚙, hoặc trên thẻ 🔒) thay cho chờ ~6 giờ.

## Sửa script về sau

Tạo lại `Code.gs` bằng lệnh ở bước 2, **thêm `--secret <SECRET cũ>`** (lấy từ dòng `var SECRET` trong `Code.gs` đang
chạy) để key đã gửi vẫn dùng được. Dán đè → 💾 Lưu → chạy `kiemTra` → **Triển khai → Quản lý triển khai** → ✏️ →
Phiên bản mới → **Triển khai**. URL giữ nguyên.
