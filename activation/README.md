# Kích hoạt ClaudeCAD + quản lý thiết bị (từ v0.9.4)

> **Bản Agent to AutoCAD Full:** dùng máy chủ kích hoạt, Google Sheet và cặp khóa **riêng**, không chung với dự án
> gốc. Các bước dựng: [`../docs/dung-may-chu-kich-hoat-full.md`](../docs/dung-may-chu-kich-hoat-full.md). Mẫu Apps
> Script ở đây đã có cột **Gói** (2D / 3D / 2D + 3D), và plugin trên nhánh `full/goi-dich-vu` đã đọc gói và chặn lệnh
> ngoài gói (chưa phát hành, chưa thử trên AutoCAD thật).

Mỗi máy muốn dùng ClaudeCAD cần được **chủ plugin cho phép**. Chủ plugin quản lý mọi máy trong một **Google Sheet "ClaudeCAD – Thiết bị"**: thấy máy nào đang dùng, duyệt máy mới (`Active`) hoặc ngưng cho dùng (`Disabled`) chỉ bằng cách đổi một ô. Key gửi qua Zalo vẫn dùng được làm đường dự phòng.

## Luồng hoạt động

1. Người dùng chạy `ClaudeCAD-Setup.exe`. Bộ cài dừng ở trang **Kích hoạt ClaudeCAD**, hiện **mã máy** (`CC-XXXX-XXXX-XXXX`) và dòng "Liên hệ Zalo owner: 0344644415".
2. Cùng lúc đó bộ cài gửi **tên thiết bị, user Windows và mã máy** tới máy chủ kích hoạt của chủ plugin (một Google Apps Script gắn với Google Sheet, chạy trên tài khoản Google của chủ plugin). Máy đó hiện thành một dòng **Chờ duyệt** trong trang **Thiết bị** của Sheet, và chủ plugin nhận **email** (kèm KEY dự phòng). Người dùng không nhận được key.
3. Chủ plugin chọn một trong hai cách:
   - **Duyệt qua Sheet (khuyên dùng):** đổi ô **Trạng thái** của dòng đó thành `Active`. Người dùng bấm **Next** cài tiếp không cần key; lần mở AutoCAD kế tiếp plugin tự hỏi máy chủ, nhận giấy phép và dùng được (đang mở AutoCAD thì bấm **Kiểm tra lại** trên thẻ 🔒 để dùng ngay).
   - **Gửi key qua Zalo:** gửi KEY trong email cho người đó; họ dán vào ô **Key kích hoạt** của bộ cài (hoặc khung chat). Key đúng thì máy nhận giấy phép và dòng trong Sheet tự chuyển thành `Active`.
4. Giấy phép đã ký lưu ở `%LocalAppData%\ClaudeCAD\license.txt`; plugin kiểm tra chữ ký mỗi lần mở AutoCAD. Chép thư mục plugin sang máy khác, hoặc cài bằng `.zip` / `install.ps1`, thì khung chat chỉ hiện thẻ **🔒 Kích hoạt** (thẻ này cũng tự gửi yêu cầu cho chủ plugin, có nút Kiểm tra lại và ô dán key).
5. Plugin **hỏi lại trạng thái** với máy chủ mỗi lần mở AutoCAD và ~6 giờ/lần (câu trả lời có chữ ký số, lưu ở `status.txt`). Nhờ đó chủ plugin khóa / mở máy từ xa được, và cột **Lần cuối dùng** + **Phiên bản** trong Sheet luôn mới (ghi tối đa 30 phút/lần).

Cập nhật bản mới, cài đè hay gỡ ra cài lại trên cùng máy đều **không** hỏi lại key. Cài lại Windows hoặc thay mainboard/ổ cứng thì mã máy đổi, máy xin duyệt lại như máy mới.

## Bảng "Thiết bị" — duyệt, khóa, theo dõi (chủ plugin)

Mỗi dòng là một máy: **Mã máy · Thiết bị · User Windows · Ngày xin · Ngày kích hoạt · Trạng thái · Lần cuối dùng · Phiên bản · Ghi chú · Gói** (cột Ghi chú để chủ plugin ghi tùy ý, ví dụ tên người dùng thật).

| Muốn | Làm |
| --- | --- |
| Cho máy mới dùng | Đổi **Trạng thái** thành `Active` (chọn trong danh sách thả xuống). Lần mở AutoCAD kế tiếp máy đó tự dùng được (hoặc bấm **Kiểm tra lại** để dùng ngay) — không cần key. |
| Ngưng cho dùng | Đổi thành `Disabled`. Máy đó bị khóa ở lần kiểm tra kế tiếp: khung chat hiện *"Máy này đã bị chủ plugin tạm khóa. Liên hệ Zalo owner: 0344644415"*; trợ lý dừng; key cũ cũng không kích hoạt lại được. |
| Cho dùng lại | Đổi lại thành `Active`. Máy bấm **Kiểm tra lại** (hoặc đợi lần kiểm tra kế tiếp) là dùng tiếp ngay, không cần key mới. |
| Xem máy nào còn dùng | Cột **Lần cuối dùng** (cập nhật mỗi lần máy kiểm tra, tối đa 30 phút/lần) và **Phiên bản**. |
| Chọn nhóm lệnh máy được dùng | Đổi ô **Gói**: `2D`, `3D` hoặc `2D + 3D`. Máy mới tự nhận `2D`; ô trống hoặc gõ sai cũng tính là `2D`. Có hiệu lực ở lần kiểm tra kế tiếp của máy đó. |
| Bỏ một máy khỏi danh sách | Xóa dòng — máy đó quay về "Chờ duyệt" ở lần kiểm tra kế tiếp (bị khóa cho tới khi duyệt lại). |

Lưu ý:

- **Máy mất mạng** (hoặc cố tình chặn mạng) dùng được tối đa **7 ngày** kể từ lần kiểm tra thành công gần nhất, sau đó plugin tự khóa tới khi máy kiểm tra lại được. Vì vậy lệnh `Disabled` chậm nhất 7 ngày là có hiệu lực với mọi máy.
- Trạng thái chỉ nhận `Active` / `Disabled` (không phân biệt hoa thường); mọi giá trị khác — kể cả gõ nhầm — được coi là "Chờ duyệt" (máy bị khóa), nên lỗi gõ không bao giờ mở nhầm quyền.
- Tên thiết bị và user Windows do máy kia **tự khai** — xác nhận người xin qua Zalo trước khi duyệt.

## Cài máy chủ kích hoạt + bảng thiết bị (chủ plugin làm một lần, khoảng 10 phút)

Cần file **`ClaudeCAD-Code.gs`** do người phát triển gửi riêng. File này đã điền sẵn email nhận thông báo và khóa bí mật. **Không chia sẻ file này, không đưa lên GitHub**: ai có file này thì tự tạo được key.

1. Mở <https://sheets.new> bằng Gmail nhận thông báo → đặt tên bảng tính, ví dụ `ClaudeCAD – Thiết bị`.
2. Trong bảng tính: **Tiện ích mở rộng** (Extensions) → **Apps Script**. Đặt tên dự án, ví dụ `ClaudeCAD kích hoạt`.
3. Xóa hết nội dung trong `Code.gs`, dán **toàn bộ** nội dung file `ClaudeCAD-Code.gs`, rồi bấm 💾 **Lưu**.
4. Trên thanh công cụ, chọn hàm **`kiemTra`** rồi bấm ▶ **Chạy** (Run). Google sẽ hỏi quyền:
   **Xem lại quyền** → chọn tài khoản → màn hình "Google chưa xác minh ứng dụng này" → **Nâng cao** (Advanced) → **Đi tới … (không an toàn)** → **Cho phép** (Allow).
   Màn hình này hiện vì script do chính cậu tạo, chưa qua kiểm duyệt của Google. Script chỉ xin quyền **gửi email** và **sửa bảng tính này** bằng tài khoản của cậu.
   Xong thì: bảng tính có thêm trang **"Thiết bị"** (kẻ sẵn tiêu đề + danh sách thả xuống cho cột Trạng thái, kèm một dòng máy thử `CC-1234-5678-9ABC` có thể xóa), và hộp thư có email **"[ClaudeCAD] Máy chủ kích hoạt hoạt động"**.
5. **Triển khai** (Deploy) → **Tùy chọn triển khai mới** (New deployment) → bấm ⚙ cạnh "Chọn loại" → **Ứng dụng web** (Web app):
   - Thực thi với tư cách (Execute as): **Tôi** (Me)
   - Người có quyền truy cập (Who has access): **Bất kỳ ai** (Anyone)
   → **Triển khai** → sao chép **URL ứng dụng web**, dạng `https://script.google.com/macros/s/AKfy…/exec`.
6. Thử: mở URL đó trên trình duyệt, thêm `?action=ping` vào cuối. Thấy `{"ok":true,"service":"ClaudeCAD activation"}` là xong.
7. Gửi URL này cho người phát triển. URL được ghi vào `activation/activation.json`, và các bản phát hành sau sẽ dùng nó.

Sửa script về sau: dán code mới → 💾 Lưu → chạy **`kiemTra`** một lần (để Google hỏi thêm quyền nếu bản mới cần) → **Triển khai → Quản lý triển khai** (Manage deployments) → ✏️ → Phiên bản: **Phiên bản mới** (New version) → Triển khai. Làm như vậy thì URL giữ nguyên. Nếu bấm "Tùy chọn triển khai mới", URL sẽ đổi và bộ cài đã phát hành sẽ không gọi được máy chủ mới.

## Cấp key bằng tay (máy không có mạng)

Mở dự án Apps Script (từ bảng tính: Tiện ích mở rộng → Apps Script), sửa dòng `var MAY_THU_CONG = 'CC-…';` thành mã máy người đó gửi, chọn hàm **`taoKeyThuCong`**, bấm ▶ **Chạy**. Máy đó được ghi vào Sheet là `Active`, và email gửi về có:

- **KEY**: dùng cho máy có mạng.
- **Dòng gói** `CCF1|…` (thêm từ bản Full): gói của máy lấy từ cột Gói. Máy không có mạng dán dòng này cùng hai dòng
  bên dưới (dán riêng dòng gói cũng được, để đổi gói cho máy đã kích hoạt). Không dán thì máy dùng gói mặc định.
- **Mã cho máy không có mạng**: **hai dòng** — dòng `CCL1|…` (giấy phép) và dòng `CCS1|…|offline|…` (mã miễn kiểm tra trạng thái). Dán **cả hai dòng** vào ô key của khung chat trong AutoCAD. Máy có mã miễn kiểm tra sẽ **không bị khóa từ xa và không bị giới hạn 7 ngày** — chỉ cấp cho máy thật sự không có mạng.

## Cài ngầm cho nhiều máy

```bash
ClaudeCAD-Setup.exe /VERYSILENT /KEY=XXXX-XXXX-XXXX-XXXX
```

Key vẫn riêng cho từng máy. Cài ngầm mà không có `/KEY=` thì plugin vẫn được cài, nhưng khung chat sẽ hiện thẻ 🔒 (duyệt qua Sheet rồi bấm **Kiểm tra lại**, hoặc dán key).

## Kỹ thuật (cho người phát triển)

- **Mã máy:** 48 bit đầu của SHA-256(`"ClaudeCAD|"` + MachineGuid chữ thường), lấy từ `HKLM\SOFTWARE\Microsoft\Cryptography`. Bộ cài (`installer/ClaudeCAD.iss`) và plugin (`LicenseLogic.MachineCode`) tính theo cùng một công thức.
- **Key:** 80 bit đầu của HMAC-SHA256(SECRET, `"CCK1|"` + mã máy), viết bằng base32 Crockford dạng `XXXX-XXXX-XXXX-XXXX`. Chỉ máy chủ biết SECRET, và chỉ máy chủ kiểm tra key.
- **Giấy phép:** `CCL1|<mã máy>|<ngày>|<chữ ký RSA-2048 SHA-256 PKCS#1 v1.5, base64url>`. Khóa riêng nằm trong Apps Script; khóa công khai nằm trong `plugin/ClaudeCAD.Plugin/Core/License.cs`. Lưu ở `%LocalAppData%\ClaudeCAD\license.txt`.
- **Trạng thái thiết bị:** `?action=status` trả `CCS1|<mã máy>|<active|disabled|pending|offline>|<ngày UTC>|<chữ ký>` (cùng khóa và cùng kiểu chữ ký với giấy phép), lưu ở `status.txt`. Plugin (`LicenseLogic.VerifyStatus`) chỉ mở khóa khi: giấy phép hợp lệ **và** trạng thái `active` ký trong vòng **7 ngày** (`StatusMaxAgeDays`) — hoặc `offline` (không bao giờ hết hạn). Trạng thái `pending`/`disabled`, quá hạn, hay ngày nằm ở tương lai (vặn lùi đồng hồ) đều khóa. Máy `Active` gọi `status` được trả kèm giấy phép, nên duyệt qua Sheet không cần key; `activate` bằng key cũng trả kèm trạng thái đã ký.
- **Gói trong plugin:**
  - `Core/Plans.cs` (`PlanLogic`) gắn nhãn Chung / 2D / 3D cho mọi lệnh. Lệnh chưa có nhãn chỉ mở cho gói `2d3d`;
    test lõi `PlanTests` đỏ khi một lệnh đăng ký trong `Drawing/*.cs` chưa có nhãn.
  - `LicenseLogic.VerifyPlan` kiểm chữ ký dòng `CCF1` và đúng máy; `EffectivePlan`: dòng gói cũ hơn dòng trạng thái thì
    về gói mặc định (chặn việc giữ lại dòng `2D + 3D` cũ), máy có mã miễn kiểm tra giữ dòng gói được cấp.
  - Lưu ở `%LocalAppData%\ClaudeCAD\plan.txt`. Bản build không có máy chủ kích hoạt: mọi lệnh, như trước.
  - `ToolDispatcher` từ chối lệnh ngoài gói với mã `NOT_IN_PLAN` (lệnh lẻ, từng lệnh trong `batch_execute`, macro).
  - Plugin gửi `context.service_plan` + `context.plan_tools` kèm mỗi tin nhắn; trợ lý chỉ nạp các lệnh đó, system prompt
    nói rõ gói, và khởi động lại phiên Claude (cùng cuộc chat) khi gói đổi.
  - **Gói mặc định `2D` là tạm**, chờ chủ dự án chốt: đổi cùng lúc `GOI_MAC_DINH` (script) và `PlanLogic.DefaultPlan`.
- **Gói:** `?action=status` và `?action=activate` trả thêm `plan` = `CCF1|<mã máy>|<2d|3d|2d3d>|<ngày UTC>|<chữ ký>`, cùng khóa và cùng kiểu chữ ký với dòng trạng thái. Giá trị lấy từ cột Gói (cột 10); mặc định là hằng `GOI_MAC_DINH` trong script. Dòng `CCS1` không đổi. `taoKeyThuCong` gửi thêm dòng này cho máy không có mạng.
- **Google Sheet:** script phải **gắn với bảng tính** (tạo từ Tiện ích mở rộng → Apps Script) vì dùng `SpreadsheetApp.getActive()`. `request` thêm dòng Chờ duyệt; `activate`/`taoKeyThuCong` đặt Active; `status` cập nhật Lần cuối dùng + Phiên bản (tối đa 30 phút/lần/máy, có LockService); `Disabled` chặn cả `request` (không email) lẫn `activate` (key đúng cũng từ chối). Máy lạ gọi `status` không được thêm dòng.
- **Tạo lại `ClaudeCAD-Code.gs`** (đổi email, hoặc mất file):
  ```bash
  node activation/make-apps-script.mjs --private-key license-private.pem --email <gmail> --secret <secret cũ> --out ClaudeCAD-Code.gs
  ```
  Bỏ `--secret` thì script tạo SECRET mới, và mọi key đã gửi trước đó đều không còn dùng được để kích hoạt máy mới. Giấy phép đã kích hoạt vẫn dùng được.
- **Đổi cặp khóa** (lộ khóa riêng): tạo khóa mới bằng `openssl genrsa -out license-private.pem 2048`, chạy `--print-public` để lấy khóa công khai, cập nhật `LicenseLogic.PublicModulus`, rồi phát hành bản mới. Lưu ý: giấy phép cũ sẽ hết hiệu lực trên bản mới, nên mọi máy phải kích hoạt lại.
- **Giới hạn:** mã nguồn đang công khai. Người biết lập trình có thể sửa mã để bỏ bước kiểm tra rồi tự build lại. Key + trạng thái chặn được người dùng thông thường; muốn chặn cả trường hợp này thì phải để repo ở chế độ riêng tư.
- **Chống spam:** tối đa **30 máy mới mỗi giờ** được thêm vào Sheet (mã máy bịa không làm đầy bảng được) và 10 email xin key mỗi giờ; chừa lại 5 lượt trong hạn mức email ngày (~100 với Gmail thường) cho `taoKeyThuCong` / `kiemTra`. Một máy hỏi lại trong vòng 30 phút không tạo email mới (nhưng vẫn được ghi vào Sheet); máy đã `Active` xin lại (mất giấy phép, cài lại) không tạo email — `status` trả lại giấy phép.
- **Máy chờ duyệt** cũng hỏi `status` khi mở AutoCAD và ~6 giờ/lần, nên đổi `Active` là máy tự mở khóa ở lần mở AutoCAD kế tiếp.
- **Trần email theo ngày:** tối đa **40 email xin key mỗi ngày** (đếm trong PropertiesService, vì bộ nhớ đệm CacheService chỉ giữ tối đa 6 giờ), cộng với giới hạn 10/giờ và phần chừa lại trong hạn mức Gmail. Hết lượt thì máy vẫn được ghi vào Sheet, chỉ không có email. Một máy gửi 2 yêu cầu cùng lúc chỉ tạo 1 email (kiểm tra trùng lần nữa khi đã giữ khóa LockService).
- **Chống công thức độc trong Sheet:** tên máy, user Windows, phiên bản do máy kia gửi lên; giá trị bắt đầu bằng `=` `+` `-` `@` được thêm dấu `'` phía trước để Google Sheets coi là chữ, không chạy như công thức (chặn `=IMAGE(...)` / `=HYPERLINK(...)` lấy dữ liệu bảng gửi ra ngoài).
- **Link máy chủ nằm trong DLL của plugin** (nhúng từ `activation/activation.json` lúc build). Xóa hay sửa file `Contents\activation.json` trên máy người dùng không tắt được việc kiểm tra trạng thái. Biến môi trường `CLAUDECAD_ACTIVATION_URL` chỉ đổi được link, không xóa được (link lạ không có chữ ký của chủ plugin, nên sau 7 ngày máy vẫn khóa).
- **Chống vặn lùi đồng hồ:** plugin nhớ thời điểm muộn nhất từng thấy (`%LocalAppData%\ClaudeCAD\clock.txt` và `HKCU\Software\ClaudeCAD\Activation`). Đồng hồ lùi quá 1 ngày so với mốc đó thì trạng thái đã lưu không còn được tin, máy bị tạm khóa tới khi kiểm tra trực tuyến được. Kiểm tra trực tuyến thành công thì mốc được đặt lại, nên người lỡ chỉnh sai giờ chỉ cần có mạng là hết. Mã miễn kiểm tra (`offline`) không bị ảnh hưởng.
- **Giới hạn của chống vặn đồng hồ:** người rành kỹ thuật vẫn xóa được cả hai mốc. Cách này chặn được người dùng thông thường, giống mọi phần khóa khác khi mã nguồn công khai. Xem các lượt xin / kích hoạt / kiểm tra ở **Thực thi** (Executions) trong trình soạn Apps Script.
- **Tên thiết bị và người dùng trong email / Sheet do máy kia tự khai** — xác nhận người xin qua Zalo trước khi duyệt.
