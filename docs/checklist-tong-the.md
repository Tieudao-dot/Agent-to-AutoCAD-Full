# Checklist tổng: thử bản Full trên AutoCAD (gói dịch vụ + 3D đợt 1, 2, 3)

*12/11/2026 · thread "Test tổng thể", nhánh `full/test-tong-the`. Một lượt đủ ~100 phút; lượt rút gọn (phần 0–5)
~60 phút. Chi tiết từng mục (bước, kết quả đúng đầy đủ): [`checklist-thu-that.md`](checklist-thu-that.md) phần I, J, K,
L. File này chỉ xếp thứ tự và ghi phần cần nhìn kỹ.*

Bản dùng thử AutoCAD của cậu hết khoảng **10/12/2026**: làm phần 0–5 trước.

## Điều kiện trước (chưa làm thì chưa thử được phần gói)

1. **Dựng máy chủ kích hoạt riêng** theo [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md): Sheet
   "Thiết bị" mới có cột **Gói**, Apps Script từ `activation/apps-script/Code.gs.template`, cặp khóa mới.
2. **Gửi luồng chính** URL web app + 2 dòng khóa công khai (`modulus`, `exponent`). **Không gửi khóa riêng, không gửi
   SECRET.**
3. Luồng chính giao một thread gắn URL + khóa vào repo (`activation/activation.json`, `License.cs`) và build **bộ cài
   thử** từ nhánh này. Bộ cài CI hiện có trỏ máy chủ của bản gốc: chỉ để kiểm build, **đừng cài thử bản đó**.

Cần thêm: máy Windows có AutoCAD bản đầy đủ (không phải LT), tài khoản Claude, chủ plugin mở sẵn Sheet "Thiết bị" mới.
Cho mục T5 (cài đè lên bản gốc): một máy đang dùng ClaudeCAD bản gốc v0.9.3 đã kích hoạt, có vài cuộc chat. Làm T5
**trước** khi cài bản Full lên máy đó bằng cách khác.

## Cách ghi kết quả và báo lỗi

- Đánh dấu Đạt / Lỗi trong [`checklist-thu-that.md`](checklist-thu-that.md) (cột Đạt / Lỗi) hoặc ghi ngay dưới file này.
- Lỗi ở bước nào thì gửi luồng chính:
  1. mã bước (vd. `L9`) + một dòng: đã gõ gì, thấy gì khác với "kết quả đúng";
  2. **file báo cáo của `CLAUDECHATSELFTEST`**: `selftest-<ngày-giờ>.txt` (Notepad tự mở; nằm trong thư mục log mà
     `CLAUDECHATLOG` mở), cả khi không có dòng ✗. Có dòng ✗ thì gửi thêm **thư mục thử** ghi ở cuối báo cáo (có
     `capture-3d.png` và bản vẽ thử);
  3. ⚙ ▸ **Gửi báo cáo lỗi** → file `.zip` trên Desktop (không có nội dung chat, bản vẽ hay key);
  4. ảnh chụp cả cửa sổ AutoCAD (Win+Shift+S);
  5. với phần 3D: câu báo / ghi chú Claude trả về (chép nguyên, kể cả tiếng Anh), và lưu bản vẽ lúc lỗi nếu được.
- **Đừng gửi key, khóa riêng, SECRET** trong ảnh hay tin nhắn; chỉ cần mã máy `CC-…`.

## Thứ tự thử

| Phần | Việc | Thời gian | Vì sao làm lúc này |
| --- | --- | --- | --- |
| 0 | Tự kiểm tra | ~3 phút | Một lệnh chạy hết 7 bước 3D của 3 đợt nối tiếp: hỏng ở đây thì các phần sau khó đọc |
| 1 | Gói dịch vụ (I1–I6 + T1–T5) | ~20 phút | Gói sai thì máy được / bị chặn sai lệnh; khách đang dùng bản gốc chuyển sang bản Full bằng cài đè: quan trọng nhất |
| 2 | Nét phía sau, hình chiếu (L9, L11) | ~6 phút | `GenerateSectionGeometry` chưa kiểm được ngoài AutoCAD |
| 3 | 3D đợt 1 sau sửa (J10–J15) | ~8 phút | Hướng nêm, góc vát, macro, AutoCAD không phải tiếng Anh |
| 4 | 3D đợt 2 sau sửa (K17–K21) | ~8 phút | Làm rỗng, đường tạm, macro theo số cạnh, căn khối |
| 5 | 3D đợt 3 sau sửa (L17–L23) | ~12 phút | Mặt bằng lộn xộn: khe giữa hai tường, nét bề rộng, cửa sổ trên layer tường |
| 6 | Một lượt dùng lệnh của cả 3 đợt (X1–X4) | ~10 phút | Hoàn tác U, thẻ xác nhận (cả khi phá khối 3D), "Chỉ xem" khi trộn đợt |
| 7 | Phần còn lại: J1–J9, K1–K16, L1–L8, L10, L12–L16 | ~35 phút | Đã có từ lúc viết từng đợt |

Phần 0–5 là lượt rút gọn (~60 phút). Mục ○ trong `checklist-thu-that.md` bỏ được khi ít thời gian.

### 0. Tự kiểm tra (~3 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| S1 | Gói `Full` (hoặc `3D`), mở một bản vẽ bất kỳ, gõ `CLAUDECHATSELFTEST` | Mọi dòng ✓, trong đó 7 dòng 3D: "dựng hộp, trừ trụ" (≈ 0,404602 m³), "Góc nhìn 3D (SW iso)", "bo 4 cạnh đứng R100" (≈ 0,474850 m³), "cắt đôi … mỗi nửa ~½", "check_interference thấy va chạm", "dựng 3D phòng 4×3 m có cửa đi" (≈ 8,022 m³), "cắt ngang tường ở cao 1 m". Dòng cuối "Bản vẽ của cậu không bị đụng tới" ✓; bản vẽ tạm tự đóng, không còn tab lạ |
| S2 | Chạy S1 lần thứ hai ngay sau đó | Vẫn mọi dòng ✓ (lần chạy trước không để lại layer / khối ảnh hưởng lần sau) |

Lỗi: gửi file `selftest-….txt` + thư mục thử ghi ở cuối báo cáo (chỉ còn giữ khi có dòng ✗).

### 1. Gói dịch vụ (~10 phút)

Làm **I1–I5** trong `checklist-thu-that.md` phần I (máy mới = Gói 2D; đổi 3D; macro có chữ ở gói 3D; đổi Full giữ
cuộc chat; `plan.txt` chỉ đọc). Thêm 3 mục rà chéo:

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| T1 | Gói `3D`: "vẽ mặt bằng phòng 4x6 m tường 220 có ghi kích thước" | Claude vẽ được tường bằng đường cơ bản hoặc nói dựng 3D được; **không** gọi offset / kích thước / chữ; nói rõ máy dùng gói 3D, chưa có kích thước, liên hệ chủ plugin |
| T2 | Ở gói Full lưu macro lượt J2 (tường + cửa, trừ khối); đổi máy về `2D`, Kiểm tra lại, chạy macro đó | Lệnh 3D trong macro báo lỗi ngoài gói (NOT_IN_PLAN), bản vẽ không có khối mới, AutoCAD không treo |
| T3 | Một đoạn chat dùng cả 4 skill mẫu (bấm **+**). Gói `2D`: gửi một tin, xem log (`CLAUDECHATLOG`) dòng "skills loaded"; đổi sang `3D`, gửi tiếp; đổi sang `Full`, gửi tiếp | 2D: `ve-kien-truc`, `ve-mep`, `ve-ha-tang`, **không** có `ve-3d-kien-truc`. 3D: **chỉ** `ve-3d-kien-truc` (3 skill 2D không nạp; cả 4 vẫn hiện trong thư viện). Full: cả 4. Cuộc chat cũ vẫn còn sau mỗi lần đổi. Cả trên máy nâng cấp từ bản gốc (skill cũ không có dòng `plan: 2d`): gói 3D vẫn không nạp 3 skill 2D; có nạp là lỗi |
| T4 | Gói `Full`: một cuộc chat có dựng 3D (vd. J2); đổi máy về `2D`, Kiểm tra lại, gửi tiếp trong **cùng** cuộc chat "đếm số khối 3D trong bản vẽ" | Claude trả lời bình thường trong cuộc chat cũ (không lỗi kết nối, không mất lịch sử); không dựng / sửa khối |
| T5 | **Cài đè lên máy đang có ClaudeCAD bản gốc** (I6 trong `checklist-thu-that.md`, cần một máy có bản gốc v0.9.3). (a) Chạy bộ cài, đọc hộp thoại, bấm **Cancel**. (b) Chạy lại, bấm **OK**, đi hết trang Kích hoạt. (c) Mở AutoCAD, chủ plugin duyệt máy ở bảng thiết bị mới, chọn gói `3D`, **Kiểm tra lại**; rồi đổi ô Gói sang `Full`, **Kiểm tra lại**. (d) Nếu được: thử lại trên một máy bản gốc khác bằng `ClaudeCAD-Setup.exe /VERYSILENT` | (a) Bản gốc giữ nguyên, AutoCAD vẫn dùng bản gốc. (b) Hộp thoại ghi đúng "bản gốc, chỉ 2D" + phiên bản; trang Kích hoạt hiện lại; cài xong không lỗi. (c) Khung chat tên **Agent to AutoCAD Full**, cuộc chat cũ còn, đăng nhập Claude còn; sau duyệt hiện "Gói: 3D", đổi ô Gói thì thành "Gói: Full" mà không cài lại; máy gói 3D không nạp 3 skill mẫu 2D cũ (log "skills loaded"). (d) Không hỏi gì; log cài đặt (`%TEMP%\Setup Log …`) có dòng "existing install: original …" và "silent install: replacing …". Lỗi: gửi log cài đặt + ảnh hộp thoại |

### 2. Nét phía sau và hình chiếu (L9, L11, ~6 phút)

Làm L2 trước (phòng 4×6 m dựng 3D), rồi **L9** và **L11** đúng như `checklist-thu-that.md`. Nhìn kỹ:

- L9: nét phía sau (`A-SECT-BACK`) là phía Claude nói "nhìn sang", không phải phía ngược lại; có ghi chú "behind lines
  could not be made" thì chép nguyên câu gửi lại.
- L11: kết quả có `hidden_lines_removed: true`. Báo `edges` thì gửi lại câu ghi chú.

Lỗi ở đây chỉ làm mất nét phía sau / nét khuất, vết cắt vẫn có: ghi lại, làm tiếp.

### 3. 3D đợt 1 sau sửa (J10–J15, ~8 phút)

J10 nêm (mặt cao ở x = 0), J11 đùn có vát (nhỏ dần lên), J12 4 LINE chiều kim đồng hồ đùn lên trên, J13 macro tường +
cửa ở bản vẽ mới rồi bản vẽ cũ (tường cũ không bị khoét thêm), J14 kiểu Conceptual trên AutoCAD không phải tiếng Anh, J15
giao hai hộp chạm mặt (báo không giao được, bản vẽ không đổi).

### 4. 3D đợt 2 sau sửa (K17–K21, ~8 phút)

K17 làm rỗng hở miệng + kín (thể tích ≈ 0,1925 và ≈ 0,0404 m³), K18 layer hiện hành khóa vẫn sweep / loft được, K19
macro bo cạnh số 3 giữa hai đời AutoCAD (nếu có), K20 va chạm mỗi cặp một lần, K21 căn hộp bằng 3 cặp điểm.

### 5. 3D đợt 3 sau sửa (L17–L23, ~12 phút)

L17 khe 400 giữa hai tường không bị lấp, L18 tường đổi dày / góc L không lồi, L19 nét in bề rộng 25, L20 cửa sổ vẽ trên
layer tường được báo, L21 sàn trùng chỉ một, L22 chạy thử báo `existing_solids`, L23 cửa sổ ở góc phòng bị hỏi lại.

### 6. Một lượt dùng lệnh của cả 3 đợt (~8 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| X1 | Sau L2 (phòng đã dựng 3D), **một tin**: "đặt UCS mặt trước, dựng thêm hộp 500x500x500 tại gốc, bo 4 cạnh đứng R50, rồi vẽ mặt cắt ngang ở cao 1 m". Sau đó gõ U **một lần** | Một lượt, không thẻ xác nhận (hộp mới dựng trong lượt); U một lần bỏ cả hộp, mặt cắt, và UCS về như trước lượt; khối tường của L2 còn nguyên |
| X2 | Lượt sau: "bo 4 cạnh đứng của khối tường R20" rồi một tin khác "cắt khối tường bằng mặt phẳng x = 2000" | Mỗi lần đều có **thẻ xác nhận** trước khi đổi (khối có từ lượt trước); Hủy → bản vẽ không đổi |
| X3 | Bật 🔒 Chỉ xem: "xem góc SW iso kiểu Conceptual", "kiểm tra va chạm các khối", "liệt kê cạnh khối tường", "dựng 3D lại mặt bằng (chạy thử)", "đặt UCS mặt trên" | Ba lệnh đầu chạy (đổi hiển thị / chỉ đọc); chạy thử dựng 3D và UCS bị từ chối "Chỉ xem"; bản vẽ không đổi |
| X4 | Gói `Full`. (a) Một tin: "dựng hộp 1000x1000x1000 rồi phá khối đó ra". (b) Lượt sau: "dựng hộp 500x500x500" rồi tin khác "phá khối hộp vừa dựng ra" → bấm **Hủy** trên thẻ; gõ lại, lần này bấm **Đồng ý**. (c) "vẽ 3 hình chữ nhật, gộp thành block rồi phá block đó ra" | (a) Không hỏi (khối dựng trong lượt), hộp thành các mặt. (b) Lần đầu: **thẻ xác nhận** trước khi phá (khối từ lượt trước); Hủy → hộp còn nguyên; Đồng ý → hộp thành các mặt. Log không có lỗi lạ. (c) Không hỏi (block là đối tượng 2D, luật cũ: chỉ hỏi khi > 20 đối tượng). Gõ U sau mỗi lượt hoàn tác đúng lượt đó |

### 7. Phần còn lại (~35 phút)

J1–J9, K1–K16, L1–L8, L10, L12–L16 trong `checklist-thu-that.md` (mục ★ trước). J8, K15, L13 (máy gói 2D) đã phủ một
phần ở phần 1: vẫn làm nhanh để thấy câu Claude nói.

## Đã đổi theo chốt 12/11/2026 (cậu thử ở trên)

- **Phá khối 3D** có trước lượt (kể cả khối Claude dựng ở lượt trước) luôn qua thẻ xác nhận; khối dựng trong lượt và
  đối tượng 2D giữ luật cũ: X4.
- **Skill 2D không nạp ở gói 3D**; skill 3D giữ quy ước mặt bằng mà lệnh dựng 3D đọc: T3, và L2 ở gói 3D (Claude vẽ
  mặt bằng bằng đường cơ bản theo mục 0 của skill `ve-3d-kien-truc`, không ghi kích thước).
- **Lệnh dựng khối không còn tham số màu / nét / layout** (đỡ token): khối lấy màu theo layer; "dựng hộp màu đỏ" thì
  Claude dựng rồi đổi màu bằng lệnh đổi thuộc tính. Thử nhanh khi làm J2: tường đúng layer, màu theo layer.
