# Checklist tổng: thử bản Full trên AutoCAD (gói dịch vụ + 3D đợt 1, 2, 3 + chuỗi nâng cấp A → E)

*12/11/2026 · thread "Test tổng thể", nhánh `full/test-tong-the`; 13/11/2026 thêm khối **chuỗi nâng cấp A → E** (thread
"Test tổng thể nâng cấp", nhánh `full/nang-cap-tong-the`, cuối file). Hai khối:*

| Khối | Phần | Lượt rút gọn | Lượt đủ |
| --- | --- | --- | --- |
| Gói + 3D đợt 1–3 | 0–8 (mục I–M, X của `checklist-thu-that.md`) | ~97 phút (phần 0–5) | ~157 phút |
| Chuỗi nâng cấp A → E | 9–16 (mục N–R của `checklist-thu-that.md`, V1–V14) | ~110 phút (phần 9–15) | ~3 giờ |

*Cả hai khối, lượt rút gọn: ~3 giờ 25 phút; lượt đủ: ~5 giờ 50 phút. Chi tiết từng mục (bước, kết quả đúng đầy đủ):
[`checklist-thu-that.md`](checklist-thu-that.md). File này chỉ xếp thứ tự và ghi phần cần nhìn kỹ.*

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
| 1 | Gói dịch vụ (I1–I6, I5b–I5d + T1–T5 + M1–M2, M10–M12) | ~42 phút | Gói sai thì máy được / bị chặn sai lệnh; khách đang dùng bản gốc chuyển sang bản Full bằng cài đè: quan trọng nhất |
| 2 | Nét phía sau, hình chiếu (L9, L11) | ~6 phút | `GenerateSectionGeometry` chưa kiểm được ngoài AutoCAD |
| 3 | 3D đợt 1 sau sửa (J10–J16) | ~10 phút | Hướng nêm, góc vát, macro, AutoCAD không phải tiếng Anh, báo lỗi rõ khi xoay / giao sai |
| 4 | 3D đợt 2 sau sửa (K17–K25) | ~13 phút | Làm rỗng, đường tạm, macro theo số cạnh, căn khối, va chạm chung mặt, UCS nghiêng, bo cạnh sau khi dời khối |
| 5 | 3D đợt 3 sau sửa (L17–L28) | ~22 phút | Mặt bằng lộn xộn: khe giữa hai tường, nét bề rộng, cửa sổ trên layer tường; nhà nhiều tầng, cửa sát góc phòng |
| 6 | Một lượt dùng lệnh của cả 3 đợt (X1–X5) | ~12 phút | Hoàn tác U, thẻ xác nhận (cả khi phá khối 3D), "Chỉ xem" khi trộn đợt |
| 7 | Phần còn lại: J1–J9, K1–K16, L1–L8, L10, L12–L16 | ~35 phút | Đã có từ lúc viết từng đợt |
| 8 | Sau kiểm tra độc lập: M3–M9 trong `checklist-thu-that.md` | ~15 phút | Lỗi đã sửa cần thử thật (thẻ xác nhận, macro) và điểm chỉ AutoCAD cho thấy |

Phần 0–5 là lượt rút gọn (~97 phút). Mục ○ trong `checklist-thu-that.md` bỏ được khi ít thời gian.

### 0. Tự kiểm tra (~3 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| S1 | Gói `Full` (hoặc `3D`), mở một bản vẽ bất kỳ, gõ `CLAUDECHATSELFTEST` | Mọi dòng ✓, trong đó 7 dòng 3D: "dựng hộp, trừ trụ" (≈ 0,404602 m³), "Góc nhìn 3D (SW iso)", "bo 4 cạnh đứng R100" (≈ 0,474850 m³), "cắt đôi … mỗi nửa ~½", "check_interference thấy va chạm", "dựng 3D phòng 4×3 m có cửa đi" (≈ 8,022 m³), "cắt ngang tường ở cao 1 m". Dòng cuối "Bản vẽ của cậu không bị đụng tới" ✓; bản vẽ tạm tự đóng, không còn tab lạ |
| S2 | Chạy S1 lần thứ hai ngay sau đó | Vẫn mọi dòng ✓ (lần chạy trước không để lại layer / khối ảnh hưởng lần sau) |

Lỗi: gửi file `selftest-….txt` + thư mục thử ghi ở cuối báo cáo (chỉ còn giữ khi có dòng ✗).

### 1. Gói dịch vụ (~42 phút)

Làm **I1–I5** trong `checklist-thu-that.md` phần I (máy mới = Gói 2D; đổi 3D; macro có chữ ở gói 3D không chạy lệnh
nào; đổi Full giữ cuộc chat; I5b gói 3D không có thuộc tính block; I5c máy offline dán lại dòng gói cũ hơn bị từ chối; I5d mã miễn kiểm tra cũ không mở lại máy đã bị khóa;
I5 `plan.txt` chỉ đọc không giữ được Full, cần dòng Full từ **hôm trước** theo giờ UTC), I6 (cài đè lên bản gốc) và
M1–M2, M10–M12 (mã không cần mạng 3 dòng dán ở bộ cài, thẻ Kích hoạt, thẻ 🔒, ⚙; bản cập nhật bản gốc đang chờ).
Thêm 5 mục rà chéo:

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| T1 | Gói `3D`: "vẽ mặt bằng phòng 4x6 m tường 220 có ghi kích thước" | Claude vẽ được tường bằng đường cơ bản hoặc nói dựng 3D được; **không** gọi offset / kích thước / chữ; nói rõ máy dùng gói 3D, chưa có kích thước, liên hệ chủ plugin |
| T2 | Ở gói Full lưu macro lượt J2 (tường + cửa, trừ khối); đổi máy về `2D`, Kiểm tra lại, chạy macro đó | Macro **không chạy lệnh nào**: thẻ lỗi tiếng Việt "Không chạy macro …: máy này dùng gói 2D, mà macro có lệnh ngoài gói (…). Bản vẽ không bị thay đổi." (KT-04); AutoCAD không treo |
| T3 | Một đoạn chat dùng cả 4 skill mẫu (bấm **+**). Gói `2D`: gửi một tin, xem log (`CLAUDECHATLOG`) dòng "skills loaded"; đổi sang `3D`, gửi tiếp; đổi sang `Full`, gửi tiếp | 2D: `ve-kien-truc`, `ve-mep`, `ve-ha-tang`, **không** có `ve-3d-kien-truc`. 3D: **chỉ** `ve-3d-kien-truc` (3 skill 2D không nạp; cả 4 vẫn hiện trong thư viện). Full: cả 4. Cuộc chat cũ vẫn còn sau mỗi lần đổi. Cả trên máy nâng cấp từ bản gốc (skill cũ không có dòng `plan: 2d`): gói 3D vẫn không nạp 3 skill 2D; có nạp là lỗi |
| T4 | Gói `Full`: một cuộc chat có dựng 3D (vd. J2); đổi máy về `2D`, Kiểm tra lại, gửi tiếp trong **cùng** cuộc chat "đếm số khối 3D trong bản vẽ" | Claude trả lời bình thường trong cuộc chat cũ (không lỗi kết nối, không mất lịch sử); không dựng / sửa khối |
| T5 | **Cài đè lên máy đang có ClaudeCAD bản gốc** (I6 trong `checklist-thu-that.md`, cần một máy có bản gốc v0.9.3). (a) Chạy bộ cài, đọc hộp thoại (hai nút **Tiếp tục** / **Hủy**), bấm **Hủy**. (b) Chạy lại, bấm **Tiếp tục**, đi hết trang Kích hoạt. (c) Mở AutoCAD, chủ plugin duyệt máy ở bảng thiết bị mới, chọn gói `3D`, **Kiểm tra lại**; rồi đổi ô Gói sang `Full`, **Kiểm tra lại**. (d) Nếu được: thử lại trên một máy bản gốc khác bằng `ClaudeCAD-Setup.exe /VERYSILENT` | (a) Bộ cài thoát ngay, bản gốc giữ nguyên, AutoCAD vẫn dùng bản gốc. (b) Hộp thoại ghi đúng "bản gốc, chỉ 2D" + phiên bản; trang Kích hoạt hiện lại; cài xong không lỗi. (c) Khung chat tên **Agent to AutoCAD Full**, cuộc chat cũ còn, đăng nhập Claude còn; sau duyệt hiện "Gói: 3D", đổi ô Gói thì thành "Gói: Full" mà không cài lại; máy gói 3D không nạp 3 skill mẫu 2D cũ (log "skills loaded"). (d) Không hỏi gì; log cài đặt (`%TEMP%\Setup Log …`) có dòng "existing install: original …" và "silent install: replacing …". Lỗi: gửi log cài đặt + ảnh hộp thoại |

### 2. Nét phía sau và hình chiếu (L9, L11, ~6 phút)

Làm L2 trước (phòng 4×6 m dựng 3D), rồi **L9** và **L11** đúng như `checklist-thu-that.md`. Nhìn kỹ:

- L9: nét phía sau (`A-SECT-BACK`) là phía Claude nói "nhìn sang", không phải phía ngược lại; có ghi chú "behind lines
  could not be made" thì chép nguyên câu gửi lại.
- L11: kết quả có `hidden_lines_removed: true`. Báo `edges` thì gửi lại câu ghi chú.

Lỗi ở đây chỉ làm mất nét phía sau / nét khuất, vết cắt vẫn có: ghi lại, làm tiếp.

### 3. 3D đợt 1 sau sửa (J10–J16, ~10 phút)

J10 nêm (mặt cao ở x = 0), J11 đùn có vát (nhỏ dần lên), J12 4 LINE chiều kim đồng hồ đùn lên trên, J13 macro tường +
cửa ở bản vẽ mới rồi bản vẽ cũ (tường cũ không bị khoét thêm), J14 kiểu Conceptual trên AutoCAD không phải tiếng Anh, J15
giao hai hộp chạm mặt (báo không giao được, bản vẽ không đổi), J16 xoay biên dạng cắt qua trục / giao khối tách rời /
góc nhìn với handle đã xóa: mỗi lệnh báo lỗi rõ, bản vẽ không đổi.

### 4. 3D đợt 2 sau sửa (K17–K25, ~13 phút)

K17 làm rỗng hở miệng + kín (thể tích ≈ 0,1925 và ≈ 0,0404 m³), K18 layer hiện hành khóa vẫn sweep / loft được, K19
macro bo cạnh số 3 giữa hai đời AutoCAD (nếu có), K20 va chạm mỗi cặp một lần, K21 căn hộp bằng 3 cặp điểm, K22 hai
hộp chồng nhau chung một mặt phẳng vẫn báo va chạm, K23 UCS nghiêng nhìn thẳng có trục X sang phải, K24 dời / xoay hộp
bằng tay rồi bo cạnh số 3 theo `topology_id` cũ vẫn đúng cạnh, K25 nhiều tường chỉ chạm nhau không bị báo va chạm (cặp
chưa đo là "chưa xác nhận").

### 5. 3D đợt 3 sau sửa (L17–L28, ~22 phút)

L17 khe 400 giữa hai tường không bị lấp, L18 tường đổi dày / góc L không lồi, L19 nét in bề rộng 25, L20 cửa sổ vẽ trên
layer tường được báo, L21 sàn trùng chỉ một, L22 chạy thử báo `existing_solids`, L23 cửa sổ ở góc phòng bị hỏi lại,
L24 dựng tầng 2 ngay sau tầng 1 (không hỏi xóa tầng 1, sàn tầng 2 nằm trên tường tầng 1), L25 cửa đi sát góc khoét đúng
tường có khe, L26 chạy thử không lưu phiên bản và chạy được khi "Chỉ xem", L27 bản vẽ đơn vị mét dựng tầng 2 ở cao 3,3,
L28 đảo trong lỗ sàn là sàn riêng và block cửa trên layer tường được nhận.

### 6. Một lượt dùng lệnh của cả 3 đợt (~12 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| X1 | Sau L2 (phòng đã dựng 3D), **một tin**: "đặt UCS mặt trước, dựng thêm hộp 500x500x500 tại gốc, bo 4 cạnh đứng R50, rồi vẽ mặt cắt ngang ở cao 1 m". Sau đó gõ U **một lần** | Một lượt, không thẻ xác nhận (hộp mới dựng trong lượt); U một lần bỏ cả hộp, mặt cắt, và UCS về như trước lượt; khối tường của L2 còn nguyên |
| X2 | Lượt sau: "bo 4 cạnh đứng của khối tường R20" rồi một tin khác "cắt khối tường bằng mặt phẳng x = 2000" | Mỗi lần đều có **thẻ xác nhận** trước khi đổi (khối có từ lượt trước); Hủy → bản vẽ không đổi |
| X3 | Bật 🔒 Chỉ xem: "xem góc SW iso kiểu Conceptual", "kiểm tra va chạm các khối", "liệt kê cạnh khối tường", "dựng 3D lại mặt bằng (chạy thử)", "đặt UCS mặt trên" | Bốn lệnh đầu chạy (đổi hiển thị / chỉ đọc; chạy thử dựng 3D chỉ đọc từ khi sửa KT-15); UCS bị từ chối "Chỉ xem"; bản vẽ không đổi |
| X4 | Gói `Full`. (a) Một tin: "dựng hộp 1000x1000x1000 rồi phá khối đó ra". (b) Lượt sau: "dựng hộp 500x500x500" rồi tin khác "phá khối hộp vừa dựng ra" → bấm **Hủy** trên thẻ; gõ lại, lần này bấm **Đồng ý**. (c) "vẽ 3 hình chữ nhật, gộp thành block rồi phá block đó ra" | (a) Không hỏi (khối dựng trong lượt), hộp thành các mặt. (b) Lần đầu: **thẻ xác nhận** trước khi phá (khối từ lượt trước); Hủy → hộp còn nguyên; Đồng ý → hộp thành các mặt. Log không có lỗi lạ. (c) Không hỏi (block là đối tượng 2D, luật cũ: chỉ hỏi khi > 20 đối tượng). Gõ U sau mỗi lượt hoàn tác đúng lượt đó |
| X5 | Gói `Full`, một hộp dựng ở lượt trước: "xóa hộp đó". Khi thẻ hiện, **gõ** "ok"; lượt sau bấm **Đồng ý** trên thẻ | Gõ "ok": hộp còn nguyên, Claude hiện lại thẻ (chỉ nút Đồng ý mở khóa, chốt 12/11/2026). Bấm Đồng ý: hộp bị xóa. Xóa đường 2D (dưới 20 đối tượng) thì không hỏi như cũ |

### 7. Phần còn lại (~35 phút)

J1–J9, K1–K16, L1–L8, L10, L12–L16 trong `checklist-thu-that.md` (mục ★ trước). J8, K15, L13 (máy gói 2D) đã phủ một
phần ở phần 1: vẫn làm nhanh để thấy câu Claude nói.

### 8. Sau kiểm tra độc lập (~15 phút)

M3–M9 trong `checklist-thu-that.md` phần M: thẻ xác nhận khi xóa / sửa khối có trước lượt (gõ "ok" không thay được nút
Đồng ý), macro sửa khối có sẵn không chạy, chóp đáy vuông, thứ tự cạnh / mặt sau khi mở lại bản vẽ, "Chỉ xem" + đổi góc
nhìn, khối trên layer khóa, `PURGE` sau mặt cắt.

## Đã đổi theo chốt 12/11/2026 (cậu thử ở trên)

- **Phá và xóa khối 3D** có trước lượt (kể cả khối Claude dựng ở lượt trước) luôn qua thẻ xác nhận, và **chỉ nút Đồng
  ý trên thẻ** mở khóa, đúng các khối thẻ đã liệt kê (gõ "ok" hay nút "Làm" của thẻ kế hoạch không tính); khối dựng
  trong lượt và đối tượng 2D giữ luật cũ: X4, X5, M3. Macro không có thẻ nên không sửa khối có sẵn: M4.
- **Skill 2D không nạp ở gói 3D**; skill 3D giữ quy ước mặt bằng mà lệnh dựng 3D đọc: T3, và L2 ở gói 3D (Claude vẽ
  mặt bằng bằng đường cơ bản theo mục 0 của skill `ve-3d-kien-truc`, không ghi kích thước).
- **Lệnh dựng khối không còn tham số màu / nét / layout** (đỡ token): khối lấy màu theo layer; "dựng hộp màu đỏ" thì
  Claude dựng rồi đổi màu bằng lệnh đổi thuộc tính. Thử nhanh khi làm J2: tường đúng layer, màu theo layer.

---

# Phần chuỗi nâng cấp A → E (thêm 13/11/2026)

*Thread "Test tổng thể nâng cấp", nhánh `full/nang-cap-tong-the`. Chi tiết từng mục: `checklist-thu-that.md` phần
N (đợt A), O (B), P (C), Q (D), R (E). Phần dưới chỉ xếp thứ tự, thêm các mục chéo giữa các đợt (V1–V8) và ghi chỗ cần
nhìn kỹ. Cách ghi kết quả / báo lỗi như phần đầu file.*

## Thời gian

| Lượt | Gồm | Thời gian ước |
| --- | --- | --- |
| **Rút gọn** (làm trước) | S3 + V1–V14 + **một số** mục ★ chọn lọc của N–R ghi ở phần 11–15 (không phải mọi mục ★) | **~110 phút** |
| Đủ | Rút gọn + mọi mục ○ của N–R | ~3 giờ (N 34, O 30, P 30, Q 35, R 30 phút + V 25 phút) |

## Ưu tiên cao nhất (hỏng thì báo ngay, chưa cần làm tiếp)

1. **S3** (tự kiểm tra): 6 dòng nâng cấp ✓. Đây là cách nhanh nhất biết phần chỉ AutoCAD làm được (chép tầng, cộng mảnh
   lan can, kéo mặt khối, vế thang nghiêng đúng chiều, cắt mái) có chạy thật không.
2. **V1–V3** (nhà nhiều tầng có cột, cầu thang, mái, rồi bảng khối lượng): kịch bản khách hay dùng nhất của bản Full.
3. **R1, R7** (cầu thang, mái): phần mới nhất, nhiều phép cộng / trừ khối nhất.
4. **O1, P9 + P10** (kéo giãn; tìm & thay qua thẻ): sửa đối tượng có sẵn, phải đúng luật thẻ xác nhận.

## Thứ tự thử

| Phần | Việc | Thời gian |
| --- | --- | --- |
| 9 | Tự kiểm tra S3 | ~4 phút |
| 10 | Mục chéo V1–V8 | ~25 phút |
| 11 | ★ của đợt A: N4, N5, N8, N10, N13, N15 | ~15 phút |
| 12 | ★ của đợt B: O1, O3, O7, O8, O10, O14, O17 | ~15 phút |
| 13 | ★ của đợt C: P1, P3, P7, P9, P10, P11 | ~13 phút |
| 14 | ★ của đợt D: Q1, Q3, Q5, Q8, Q11 | ~12 phút |
| 15 | ★ của đợt E: R1, R3, R5, R7, R14 | ~11 phút |
| 16 | Phần còn lại của N–R: các mục ★ chưa chọn ở phần 11–15 (N0–N2, N17, O0, O4, O6, O11–O13, P5, P6, P14, P17, Q4, Q6, Q9, Q10, Q13, R2, R6, R8, R10, R13, R15; N0, O0, Q13, R13 đã phủ bởi S3) rồi các mục ○ | ~85 phút |

### 8b. Khung chat mượt khi đoạn chat dài (~5 phút, thread "Tối ưu khung chat", 14/11/2026)

- Mở một đoạn chat dài (vài chục lượt trở lên, có bảng, khối code). Cuộn lên xuống bằng con lăn chuột, rồi kéo thanh
  cuộn thật nhanh từ dưới lên trên: **không giật, không thấy khoảng trống** ở chỗ tin cũ (tin hiện ra ngay khi cuộn tới).
- Ở cuối đoạn chat dài, gõ nhanh một câu dài vài dòng vào ô nhập: **chữ hiện kịp tay**, không khựng. Lúc Claude đang
  viết một câu trả lời dài có bảng: cuộn lên đọc tin cũ không bị kéo xuống, cuộn về cuối thì lại tự theo.
- Bấm **A+ / A−** (cỡ chữ) và đổi giao diện sáng / tối: mọi tin, kể cả tin ở xa phía trên, đổi đúng. Bôi đen chữ
  trong một câu trả lời cũ rồi **Ctrl+C**, dán ra Notepad: chép đủ. "Xem thêm tin cũ", "Hoàn tác lượt này",
  "Xem kết quả" vẫn bấm được như cũ.

### 8c. Icon và thu gọn khung chat (~3 phút, 14/11/2026)

- Nhìn thanh công cụ và hàng nút dưới ô nhập: **icon nét mảnh một màu** giống icon palette của AutoCAD, không còn hình
  emoji (ổ khóa, đĩa mềm, ghim màu…). Đổi giao diện AutoCAD sáng / tối: icon đổi màu theo. Rê chuột lên từng icon: có
  chú thích.
- Bấm nút mũi tên (Thu gọn) khi palette đang **nổi**: palette co lại vừa khít thanh công cụ + ô nhập, **nút Gửi cùng hàng
  ô nhập**, không có khoảng trống dưới ô nhập. Gửi một yêu cầu: dòng "Đã xong…" hiện ra, palette cao thêm vừa đủ, không
  che ô nhập. Bấm mũi tên lần nữa: mở lại đủ đoạn chat. Thử thêm khi palette **neo cạnh màn hình**: chỉ nội dung co lại.

### 9. Tự kiểm tra (~4 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| S3 | Gói `Full`, gõ `CLAUDECHATSELFTEST` (chạy hai lần liền) | Mọi dòng ✓ cả hai lần, trong đó 6 dòng "Nâng cấp đợt …": A (2 tầng + sàn mái + 2 cột), A (lan can 3 m), B (bo góc, kéo dài, bẻ gãy, chia, nối, kéo giãn), **C (tìm & thay chữ, bảng diện tích phòng: dòng mới của bước này)**, D (dầm, tường cong, PRESSPULL, STL), E (cầu thang, mái 4 dốc). Dòng cuối "Bản vẽ của cậu không bị đụng tới" ✓ |

### 10. Mục chéo giữa các đợt (~25 phút)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| V1 | Mặt bằng phòng L2 + 4 cột vuông 220 trên `A-COL` ở góc + đường bao sàn trên `A-SLAB`. Một tin: "dựng 3D 2 tầng, mỗi tầng 3300, sàn 150, có sàn mái, rồi lợp mái 4 dốc" | Tường / cột / sàn 2 tầng + sàn mái (z 6450 → 6600); **mái đặt trên mặt sàn mái** (mép dưới mái ở 6600, không cắt vào sàn mái; báo `wall_top_from` "top of the roof slab…"). U một lần bỏ cả lượt |
| V2 | Tiếp V1 (lượt mới): "dựng cầu thang chữ U trong phòng từ sàn tầng 1 lên tầng 2" rồi "dựng dầm 220 × 400 dọc tường dưới sàn tầng 2" | Cầu thang 21 bậc, đỉnh ở 3300 (mặt sàn tầng 2), bậc trên cùng gác lên sàn; dầm đỉnh 3150, đáy 2750, chạm đáy sàn (sàn 150). Không hỏi thẻ (chỉ dựng mới) |
| V3 | Tiếp V2: "lập bảng khối lượng theo layer, trừ phần chồng nhau" | Có dòng cột, dầm, tường, sàn, cầu thang, mái; phần chồng (cột trong tường, dầm trong tường, mép cầu thang gác lên sàn; dầm chỉ chạm đáy sàn, không chồng) **chỉ tính một lần**: cột giữ đủ, tường / sàn bị trừ. Gói `Full` chèn được bảng; gói `3D` chỉ báo số trong chat |
| V4 | Tiếp V3: "vẽ mặt cắt đứng qua cầu thang và mái" rồi "vẽ hình chiếu mặt đứng phía trước" | Mặt cắt có đường bao + hatch của tường, sàn, vế thang (răng cưa bậc), mái dốc; hình chiếu thấy mái và lan can. Khối 3D không đổi |
| V5 | Mặt bằng có một tường cong (hai cung, Q3) có khe cửa đi + ký hiệu cửa. "dựng 3D" rồi "vẽ mặt cắt ngang ở cao 1 m" | Tường cong có lỗ cửa đúng chỗ khe; mặt cắt ngang thấy đường cong và chỗ hở của cửa |
| V6 | 2D (gói `2D` hoặc `Full`): mặt bằng một phòng 6000 × 4000 như P3 (tường trên `A-WALL`), trong phòng một polyline kín (layer phòng) + chữ "P.NGU", một block cửa có thuộc tính MA = "P.NGU-1". "kéo giãn tường phải và cạnh phải của phòng sang phải 1000", "bẻ gãy tường dưới ở giữa", rồi "ghi kích thước mặt bằng" và "lập bảng diện tích các phòng" | Kích thước ghi theo hình **sau** kéo giãn (7000); bảng diện tích 28 m² tên P.NGU. Cạnh bị bẻ gãy vẫn được đo kích thước |
| V7 | Tiếp V6: "thay P.NGU bằng PHÒNG NGỦ ở mọi chỗ (cả trong block)" → bấm **Đồng ý**; rồi gõ U một lần | Thẻ liệt kê chữ + thuộc tính; Đồng ý → đổi cả TEXT / MTEXT / thuộc tính MA thành "PHÒNG NGỦ-1"; U một lần trả lại hết |
| V8 | Có 3 layout giấy: "in tất cả layout ra một PDF" sau V6 / V7 | Một PDF nhiều trang theo thứ tự tab, chữ đã thay xuất hiện đúng |

### 10b. Sau kiểm tra độc lập nâng cấp (KT2, ~10 phút, làm cùng lượt rút gọn)

| # | Làm gì | Kết quả đúng |
| --- | --- | --- |
| V9 | *(KT2-10)* Bản vẽ đơn vị **mét**: dựng một cột 0,4 × 0,4 cao 3,3, "lặp cột này 2 hàng x 2 cột cách 6 m x 4 m, 3 tầng cách 3,3 m" | Các tầng cách nhau **3,3** đơn vị bản vẽ (không phải 3300) |
| V10 | *(KT2-11)* Vẽ một hình chữ nhật cột trên `A-COL` **hai lần chồng khít** rồi "dựng 3D mặt bằng" | Một khối cột; báo cáo có dòng "the same outline as …: one column built" |
| V11 | *(KT2-21)* Mặt bằng **không có đường bao sàn**: "dựng tầng 1, tầng cao 3,3 m" | Claude **hỏi chọn sàn** (theo khung bao tường / không sàn) trước khi dựng, chưa dựng gì |
| V12 | *(KT2-22)* Polyline trên layer `LAN CAN`: "dựng 3D mặt bằng" | Claude hỏi bằng thẻ có ghi tên layer `LAN CAN` trước khi dựng lan can; Đồng ý rồi mới dựng |
| V13 | *(KT2-17, lượt 2)* Đang ở Model, đã đặt khổ giấy cho Model là A4 (Page Setup). "in bản vẽ ra PDF" (không nói khổ giấy) | PDF ra **khổ A4** như đã đặt cho Model, không còn mặc định A3 |
| V14 | *(TT-01, bộ cài)* (a) Mở AutoCAD (có bản vẽ chưa lưu) rồi chạy bộ cài. (b) Bấm **Thử lại** khi AutoCAD vẫn mở; rồi lưu, đóng AutoCAD, bấm **Thử lại**. (c) Cài lại lần nữa ngay sau khi đóng AutoCAD (tiến trình phụ của plugin có thể còn chạy) | (a) Hộp tiếng Việt "AutoCAD đang mở…" nút **Thử lại / Hủy**; AutoCAD **không bị tắt**, bản vẽ còn nguyên. (b) Lần đầu hộp hiện lại; sau khi đóng AutoCAD thì cài tiếp. (c) Cài xong không hỏi gì (bộ cài tự tắt `node.exe` / `claude.exe` của plugin). Không lần nào hiện hộp tiếng Anh "Setup was unable to automatically close all applications". Hủy ở (a) → máy giữ bản cũ |

Đợt B thêm O21 (kéo giãn không đụng layer đang tắt), O22 (25 đoạn kéo giãn theo khung qua thẻ) trong
`checklist-thu-that.md`: làm ở phần 16. Các sửa khác của KT2 (đơn vị mét ở tiện ích 2D, mái chữ L báo trước chỗ chồng,
ô cầu thang có chỗ lõm…): xem bàn giao từng đợt, thử khi làm mục cùng chủ đề.

## 3 số đợt E tự chọn — cậu xem có đồng ý không

Đợt E dùng đúng số cậu đã chốt (bậc 160 × 280, vế 1000, chiếu nghỉ bằng vế; mái bằng 150; mái dốc 30°, đua 600). Ba số
dưới đây cậu **chưa chốt**, thread đợt E tự chọn theo cách làm thường gặp. Khách nói số khác thì theo số của khách; nếu
cậu muốn đổi số mặc định thì báo luồng chính:

| Số | Giá trị đang dùng | Cậu thấy ở đâu |
| --- | --- | --- |
| Bản thang (phần bê tông dưới bậc) và chiếu nghỉ dày | **150 mm** | R1–R3: mặt cắt V4 thấy bản nghiêng dưới các bậc dày 150 |
| Khe giữa 2 vế cầu thang chữ U | **100 mm** | R3, V2: nhìn từ trên thấy khe hở giữa hai vế |
| Bản mái dốc dày (đo vuông góc mặt mái) | **150 mm** | R7, R8, V1: mép mái đua |

# Đợt F1: lệnh hiểu UCS + nút chế độ "Cả hai / 2D / 3D" (thêm 25/11/2026)

*Thread "Nâng cấp đợt F1", nhánh `full/nang-cap-f1`. Chi tiết: `docs/ban-giao/nang-cap-f1.md`. Cần máy **gói Full** (bộ
cài thử nội bộ là Full) và một bản vẽ trống đơn vị mm. ~30 phút.*

**UCS là gì (một câu):** UCS là hệ trục khách tự đặt (gõ lệnh `UCS` trong AutoCAD, hoặc nhờ Claude), ví dụ dời gốc tọa
độ tới góc nhà hoặc xoay trục theo hướng nhà. Biểu tượng trục X / Y ở góc màn hình đổi theo.

### F1-a. World vẫn như cũ (~5 phút, làm trước)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| U0 | Gõ `UCS` → `W` (World). Chạy một macro đã lưu từ trước đợt F1 (nếu có), rồi nhờ "vẽ hình chữ nhật 4000 × 3000 tại 0,0" | Macro vẽ ra **đúng chỗ như trước**. Hình chữ nhật có góc ở 0,0, cạnh ngang / dọc như cũ. Claude không nhắc gì tới UCS |

### F1-b. Vẽ trong UCS đã dời / xoay (~12 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| U1 | Gõ `UCS` → chọn gốc mới tại điểm 10000,5000 → xoay trục Z 30° (`UCS` → `Z` → 30). Nhờ "vẽ hình chữ nhật 4000 × 3000 tại gốc" | Góc hình chữ nhật nằm **đúng gốc UCS mới** (chỗ biểu tượng trục), cạnh dài song song trục X của UCS (nghiêng 30° so với màn hình nếu đang nhìn World) |
| U2 | Tiếp U1: "ghi chữ PHÒNG KHÁCH cao 250 giữa hình", "ghi kích thước cạnh dưới" | Chữ nằm giữa hình, **xoay theo UCS** (dọc theo cạnh dài). Kích thước nằm dọc cạnh dưới, số đo 4000 |
| U3 | Tiếp: "dời hình chữ nhật theo trục X 1000", rồi "hình chữ nhật đang ở tọa độ nào?" | Hình dời **dọc theo trục X của UCS** (không phải ngang màn hình World). Claude trả lời góc ở khoảng **1000, 0** và nói là theo UCS |
| U4 | Tiếp: "dựng khối hộp 1000 × 500 cao 800 tại gốc", rồi xoay góc nhìn 3D | Khối hộp đứng ở gốc UCS, cạnh song song cạnh hình chữ nhật |
| U5 | Bấm nút **Chọn điểm** trên khung chat, bấm đúng vào gốc UCS, gửi "vẽ đường tròn bán kính 500 tại điểm này" | Ô nhập có số gần **[0, 0]**; đường tròn tâm đúng chỗ đã bấm |
| U6 | Bấm **Lưu macro** cho lượt U1. Gõ `UCS` → `W`. Chạy macro vừa lưu | Hình chữ nhật mới vẽ **chồng đúng lên hình của U1** (macro nhớ UCS lúc ghi), không nhảy về 0,0 của World |
| U7 | Gõ `UCS` → `X` → 90 (UCS mặt đứng). Nhờ "vẽ hình chữ nhật 2000 × 1000 tại gốc", rồi xoay góc nhìn 3D | Hình chữ nhật **đứng thẳng** (nằm trên mặt đứng), không nằm sàn |
| U8 | Gõ `UCS` → `W`. Hỏi "bản vẽ đang dùng hệ tọa độ nào?" | Claude nói đang là World (WCS); số lại như cũ |

### F1-c. Nút chế độ (~10 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| M1 | Mở khung chat trên máy gói **Full** | Hàng trên cùng, cạnh "Gói: Full", có nút hình khối ghi **Cả hai**. (Máy gói 2D hoặc 3D: **không có** nút này) |
| M2 | Bấm nút → chọn **2D — chỉ lệnh 2D** | Nút đổi thành **2D**; khung chat có dòng "Đã chuyển sang chế độ 2D … áp dụng từ tin nhắn tới" |
| M3 | Ở chế độ 2D, nhờ "dựng khối hộp 1000 × 1000 × 1000" | Claude nói kiểu **"Đang ở chế độ 2D, đổi sang 3D hoặc Cả hai để vẽ"**. **Không** có câu mua gói, nâng cấp, liên hệ chủ plugin. Bản vẽ không có gì mới |
| M4 | Chọn **3D**, nhờ "ghi chữ ABC" | Claude nói đang ở chế độ 3D, đổi sang 2D hoặc Cả hai. Rồi chọn **Cả hai**, nhờ lại: chữ được ghi |
| M5 | Chọn **2D**, đóng hẳn AutoCAD, mở lại, mở khung chat | Nút vẫn ghi **2D** (nhớ lựa chọn) |
| M6 | Đang ở **Cả hai**, gửi một yêu cầu dài (vd "vẽ mặt bằng 3 phòng có cửa"); trong lúc Claude đang làm, chọn **3D** | Lượt đang chạy **làm tiếp bình thường** tới hết (không bị lỗi nửa chừng); dòng báo ghi "áp dụng từ tin nhắn sau, lượt đang chạy giữ chế độ cũ". Tin sau mới theo chế độ 3D; Claude vẫn nhớ đoạn chat trước |
| M7 | Bấm **Thu gọn** khung chat | Nút chế độ **vẫn thấy** ở hàng trên cùng, bấm được |
| M8 | Ở chế độ 2D, chạy một macro có cả lệnh 3D (nếu có) | Macro chạy đủ (macro không dùng Claude nên không bị chế độ giới hạn) |

### F1-d. Sau Test & sửa đợt F1 (~6 phút)

*Nhánh `full/nang-cap-f1-test`. Chi tiết: `docs/ban-giao/nang-cap-f1-test.md`.*

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| T1 | Vẽ (ở World) mặt bằng 2 phòng cạnh nhau trên layer A-WALL, cách nhau xa. Gõ `UCS` → `Z` → 30. Nhờ "dựng 3D phần mặt bằng nằm trong khung từ 0,0 tới …" (khung theo UCS chỉ ôm **một** phòng) | Chỉ phòng nằm trong khung (theo trục UCS) được dựng; phòng ngoài khung không bị dựng, không bị sót tường của phòng trong khung |
| T2 | Gõ `UCS` → `X` → 90 (UCS mặt đứng). Nhờ "đùn hình chữ nhật 1000 × 500 tại gốc thành khối dày 200" (Claude dùng `extrude` với các điểm) | Khối **đứng trên mặt đứng** (dày 200 theo trục Z của UCS), không báo lỗi, không ra khối dẹt / mỏng như tờ giấy |
| T3 | Máy Full, chọn chế độ **3D**. Nhờ "chép thuộc tính của khối A sang khối B", rồi "lập bảng khối lượng các khối" | Claude làm được phần 3D; nếu nhắc tới phần chữ / bảng thì nói **đổi sang 2D hoặc Cả hai**, không nói "gói của máy không có 2D", không mời mua gói |
