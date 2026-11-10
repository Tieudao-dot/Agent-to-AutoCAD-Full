# Kích hoạt ClaudeCAD bằng key (từ v0.9.4)

Mỗi máy muốn cài hoặc dùng ClaudeCAD cần **key do chủ plugin cấp**, riêng cho từng máy và có hiệu lực vĩnh viễn.

## Luồng hoạt động

1. Người dùng chạy `ClaudeCAD-Setup.exe`. Bộ cài dừng ở trang **Kích hoạt ClaudeCAD**, hiện **mã máy** (`CC-XXXX-XXXX-XXXX`) và dòng "Liên hệ Zalo owner: 0344644415".
2. Cùng lúc đó bộ cài gửi **tên thiết bị, user Windows và mã máy** tới máy chủ kích hoạt của chủ plugin (một Google Apps Script chạy trên tài khoản Google của chủ plugin). Máy chủ **tự tạo key** cho mã máy đó và **gửi email cho chủ plugin**. Người dùng không nhận được key.
3. Chủ plugin đọc email. Đồng ý thì gửi key cho người đó qua Zalo; không đồng ý thì bỏ qua email.
4. Người dùng dán key vào bộ cài → bộ cài hỏi máy chủ → key đúng thì nhận **giấy phép đã ký**, lưu ở `%LocalAppData%\ClaudeCAD\license.txt`, rồi cài tiếp.
5. Plugin kiểm tra chữ ký giấy phép mỗi lần mở AutoCAD, không cần mạng. Chép thư mục plugin sang máy khác, hoặc cài bằng `.zip` / `install.ps1`, thì khung chat chỉ hiện thẻ **🔒 Kích hoạt ClaudeCAD**. Thẻ này cũng tự gửi yêu cầu cho chủ plugin và có ô dán key.

Cập nhật bản mới, cài đè hay gỡ ra cài lại trên cùng máy đều **không** hỏi lại key. Cài lại Windows hoặc thay mainboard/ổ cứng thì mã máy đổi, khi đó cần xin key mới.

## Cài máy chủ kích hoạt (chủ plugin làm một lần, khoảng 10 phút)

Cần file **`ClaudeCAD-Code.gs`** do người phát triển gửi riêng. File này đã điền sẵn email nhận key và khóa bí mật. **Không chia sẻ file này, không đưa lên GitHub**: ai có file này thì tự tạo được key.

1. Mở <https://script.google.com> bằng Gmail nhận key → **Dự án mới** (New project). Đặt tên dự án, ví dụ `ClaudeCAD kích hoạt`.
2. Xóa hết nội dung trong `Code.gs`, dán **toàn bộ** nội dung file `ClaudeCAD-Code.gs`, rồi bấm 💾 **Lưu**.
3. Trên thanh công cụ, chọn hàm **`kiemTra`** rồi bấm ▶ **Chạy** (Run). Google sẽ hỏi quyền:
   **Xem lại quyền** → chọn tài khoản → màn hình "Google chưa xác minh ứng dụng này" → **Nâng cao** (Advanced) → **Đi tới ClaudeCAD kích hoạt (không an toàn)** → **Cho phép** (Allow).
   Màn hình này hiện vì script do chính cậu tạo, chưa qua kiểm duyệt của Google. Script chỉ xin quyền **gửi email bằng tài khoản của cậu**.
   Kiểm tra hộp thư: có email **"[ClaudeCAD] Máy chủ kích hoạt hoạt động"** là được.
4. **Triển khai** (Deploy) → **Tùy chọn triển khai mới** (New deployment) → bấm ⚙ cạnh "Chọn loại" → **Ứng dụng web** (Web app):
   - Thực thi với tư cách (Execute as): **Tôi** (Me)
   - Người có quyền truy cập (Who has access): **Bất kỳ ai** (Anyone)
   → **Triển khai** → sao chép **URL ứng dụng web**, dạng `https://script.google.com/macros/s/AKfy…/exec`.
5. Thử: mở URL đó trên trình duyệt, thêm `?action=ping` vào cuối. Thấy `{"ok":true,"service":"ClaudeCAD activation"}` là xong.
6. Gửi URL này cho người phát triển. URL được ghi vào `activation/activation.json`, và các bản phát hành sau sẽ dùng nó.

Sửa script về sau: **Triển khai → Quản lý triển khai** (Manage deployments) → ✏️ → Phiên bản: **Phiên bản mới** (New version) → Triển khai. Làm như vậy thì URL giữ nguyên. Nếu bấm "Tùy chọn triển khai mới", URL sẽ đổi và bộ cài đã phát hành sẽ không gọi được máy chủ mới.

## Cấp key bằng tay (máy không có mạng)

Mở dự án trên script.google.com, sửa dòng `var MAY_THU_CONG = 'CC-…';` thành mã máy người đó gửi, chọn hàm **`taoKeyThuCong`**, bấm ▶ **Chạy**. Email gửi về có hai thứ:

- **KEY**: dùng cho máy có mạng.
- **Mã kích hoạt offline**: một dòng dài bắt đầu bằng `CCL1|`, dán vào ô key của bộ cài hoặc khung chat. Mã này không cần mạng.

## Cài ngầm cho nhiều máy

```bash
ClaudeCAD-Setup.exe /VERYSILENT /KEY=XXXX-XXXX-XXXX-XXXX
```

Key vẫn riêng cho từng máy. Cài ngầm mà không có `/KEY=` thì plugin vẫn được cài, nhưng khung chat sẽ hỏi key.

## Kỹ thuật (cho người phát triển)

- **Mã máy:** 48 bit đầu của SHA-256(`"ClaudeCAD|"` + MachineGuid chữ thường), lấy từ `HKLM\SOFTWARE\Microsoft\Cryptography`. Bộ cài (`installer/ClaudeCAD.iss`) và plugin (`LicenseLogic.MachineCode`) tính theo cùng một công thức.
- **Key:** 80 bit đầu của HMAC-SHA256(SECRET, `"CCK1|"` + mã máy), viết bằng base32 Crockford dạng `XXXX-XXXX-XXXX-XXXX`. Chỉ máy chủ biết SECRET, và chỉ máy chủ kiểm tra key.
- **Giấy phép:** `CCL1|<mã máy>|<ngày>|<chữ ký RSA-2048 SHA-256 PKCS#1 v1.5, base64url>`. Khóa riêng nằm trong Apps Script; khóa công khai nằm trong `plugin/ClaudeCAD.Plugin/Core/License.cs`.
- **Tạo lại `ClaudeCAD-Code.gs`** (đổi email, hoặc mất file):
  ```bash
  node activation/make-apps-script.mjs --private-key license-private.pem --email <gmail> --secret <secret cũ> --out ClaudeCAD-Code.gs
  ```
  Bỏ `--secret` thì script tạo SECRET mới, và mọi key đã gửi trước đó đều không còn dùng được để kích hoạt máy mới. Giấy phép đã kích hoạt vẫn dùng được.
- **Đổi cặp khóa** (lộ khóa riêng): tạo khóa mới bằng `openssl genrsa -out license-private.pem 2048`, chạy `--print-public` để lấy khóa công khai, cập nhật `LicenseLogic.PublicModulus`, rồi phát hành bản mới. Lưu ý: giấy phép cũ sẽ hết hiệu lực trên bản mới, nên mọi máy phải kích hoạt lại.
- **Giới hạn:** mã nguồn đang công khai. Người biết lập trình có thể sửa mã để bỏ bước kiểm tra rồi tự build lại. Key chặn được người dùng thông thường; muốn chặn cả trường hợp này thì phải để repo ở chế độ riêng tư.
- **Chống spam:** tối đa 10 email xin key mỗi giờ; chừa lại 5 lượt trong hạn mức email ngày (~100 với Gmail thường) cho `taoKeyThuCong` / `kiemTra`. Một máy hỏi lại trong vòng 30 phút không tạo email mới. Xem các lượt xin / kích hoạt ở **Thực thi** (Executions) trong script.google.com.
- **Tên thiết bị và người dùng trong email do máy kia tự khai** — xác nhận người xin key qua Zalo trước khi gửi key.
