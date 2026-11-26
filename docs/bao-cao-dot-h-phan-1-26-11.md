# Báo cáo: đợt H phần 1 (26/11/2026)

*Luồng chính. Chạy 14h05 → 19h15 ngày 26/11 (giờ Việt Nam), theo ba điểm cậu chốt 26/11: chia hai phần; chạy liền, số tiêu
chuẩn hỏi trước; skill trên máy nâng cấp cập nhật nhưng giữ bản khách đã sửa.*

## 1. Tóm tắt

- **Xong 5 phần việc (H1 → H5).** Mỗi phần có một thread viết và một thread khác rà + sửa. Sau đó có test tổng thể và kiểm tra
  độc lập bằng agent mới: 28 lỗi, cả 28 sửa đúng và đã soát lại, không còn lỗi mở.
- **Nhánh chốt:** `full/nang-cap-h-tong-the` @ `76ecfae` (code `93096dc`).
  - Test lõi 1608, trợ lý 443 (cuối đợt G: 1445 và 407).
  - Build đủ 3 bản cho AutoCAD 2022–2027.
  - CI Windows xanh (run 45813679683, log `# fail 0`).
- **Sửa thêm một lỗi của CI:** trước đây CI Windows bỏ qua test trợ lý hỏng. Cả 3 test hỏng từ bản chốt G đều là lỗi của chính test, bản 5 không bị ảnh hưởng. Từ nay mỗi lần báo "CI xanh", tớ tự xem số `# fail` trong log.
- **Chưa phần nào chạy trên AutoCAD thật, Claude thật, Blender / 3ds Max thật.** Chưa phát hành: `main` vẫn là v0.9.3 của bản gốc.
- **Bộ cài thử bản 6** (đủ đợt F + G + H phần 1): xem mục 3.
- **Phần 2 đã bắt đầu** (H6 hạ tầng, H7 ống MEP 3D chạy cùng lúc): mới làm phần không dính số tiêu chuẩn, **chờ cậu duyệt số
  tiêu chuẩn** ([`dot-h-so-tieu-chuan.md`](dot-h-so-tieu-chuan.md)).

## 2. Plugin có thêm gì

| Đợt | Khách được gì | Gói |
| --- | --- | --- |
| H1 | Cài đè / cập nhật: skill khách chưa sửa được thay bản mới; skill khách đã sửa được giữ, bản mới đặt cạnh (`SKILL.ban-moi.md`), khung chat báo một dòng. "Vẽ lại từ PDF scan" đọc thẳng trang PDF, không còn ảnh trắng. Soát lỗi nhanh hơn khi nhiều điểm / block trùng | Mọi gói |
| H2 | "Lưu ra DWG 2010 để gửi đối tác", "xuất DXF", "xuất FBX cho 3ds Max / Blender". Bản vẽ đang mở giữ nguyên tên, file, đời | DXF / DWG: mọi gói. FBX: 3D / Full |
| H3 | "So bản vẽ này với bản cũ": thêm / bớt / đổi, khoanh đám mây xanh / đỏ / vàng, xuất Excel. File kia chỉ được đọc | 2D / Full (gói 3D so khối 3D) |
| H4 | "Học chuẩn công ty từ file mẫu" (layer, kiểu chữ, kiểu kích thước, ghi chú, khung tên), rồi "áp / soát / tạo tờ in theo chuẩn ABC" | 2D / Full |
| H5 | "Soát / áp chuẩn / in PDF / đếm block cho cả thư mục": một thẻ trước khi chạy; mỗi file một dòng xong / lỗi / bỏ qua; dừng được giữa hai file; áp chuẩn thì sao lưu từng file trước khi ghi | Theo lệnh dùng |

## 3. Việc cần cậu làm

1. **Duyệt số tiêu chuẩn phần 2 (gấp nhất, đang chặn H6 / H7 / H8).** Danh sách:
   [`dot-h-so-tieu-chuan.md`](dot-h-so-tieu-chuan.md) (trắc dọc, đồng mức, cỡ ống, độ dốc, lớp bê tông bảo vệ, neo / nối thép…).
   Cậu nhắn "duyệt số", tớ hỏi lần lượt từng nhóm, có số đề xuất.
2. **Cài bộ cài thử bản 6.** __LINK_BAN_6__
   - Cài đè lên bản 5 được, **không phải xóa skill cũ**:
     - skill cậu chưa sửa tự được thay;
     - skill cậu đã sửa được giữ, bản mới nằm cạnh (`SKILL.ban-moi.md`).
   - Hướng dẫn từng bước: thread ["Lưu trữ bộ cài"](https://claude.ai/code/session_01D56HmJdn7RASaJtfgMQp5x).
   - Cài xong: thanh trên cùng khung chat ghi "nâng cấp 6". Bấm **Chat mới**, rồi gõ `CLAUDECHATSELFTEST` để plugin tự kiểm.
3. **Thử tay** theo
   [`checklist-tong-the.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/nang-cap-h-tong-the/docs/checklist-tong-the.md),
   khối **"Đợt H phần 1"**:
   - làm mục **H0** trước (~40 phút): 6 việc rủi ro nhất, xem mục 4;
   - rồi H1 → H6 (cả khối khoảng 3 giờ).

   Các khối "Đợt F", "Đợt G" vẫn còn nguyên trong file nếu cậu chưa thử bản 5. Mục nào hỏng thì gửi tớ mã mục (vd H0-3) kèm ảnh
   hoặc chữ ở dòng lệnh.
4. **Duyệt con số phần 1** (mục 5) và **xem các quyết định tự chọn** (mục 6), điểm đã biết (mục 7).
5. Việc cũ vẫn chờ cậu:
   - dựng máy chủ kích hoạt riêng;
   - quyết định phát hành.

   Bộ cài thử và bản dùng thử AutoCAD hết hạn khoảng 10/12/2026.

## 4. Thử trước: các điểm rủi ro nhất (mục H0 của checklist)

| Mục | Vì sao rủi ro | Nếu hỏng thì |
| --- | --- | --- |
| **H0-1** tự kiểm | Đọc PDF dùng bộ đọc có sẵn của Windows qua PowerShell; máy bị chặn PowerShell sẽ không đọc được | Plugin lui về ảnh chụp AutoCAD như bản 5 và nói lý do. Cậu chép dòng báo gửi tớ |
| **H0-2** FBX mở bằng Blender / 3ds Max | Plugin tự ghi file FBX theo đặc tả, mới đọc lại bằng test, chưa mở bằng phần mềm thật | Vẫn còn đường xuất DWG / DXF / STL; cậu gửi tớ cảnh báo của hộp thoại nhập |
| **H0-3** áp chuẩn cả thư mục | Ghi đè file của khách hàng loạt. Đã có sao lưu từng file, ghi qua file tạm rồi mới thay, nhưng chưa chạy trên AutoCAD thật | Bản gốc nằm trong `_sao-luu-claudecad\<ngày giờ>\` cạnh thư mục. **Thử trên thư mục bản sao** |
| **H0-4** PDF scan thật | Chưa biết lề trang PDF có làm lệch tỉ lệ không | Đo `DIST` một kích thước, gửi tớ số lệch |
| **H0-5** skill khi cài đè | Chạy được hay không phụ thuộc bộ cài thật trên máy thật | Skill cậu sửa luôn được giữ, chỉ có thể thiếu bản mới |
| **H0-6** khung tên công ty | Chép khung tên từ file mẫu sang tờ mới chỉ mới qua test tự động | Tờ in vẫn tạo được bằng khung tên VN như bản 5 |

## 5. Con số chờ cậu duyệt

Chi tiết từng số (ở đâu, khách thấy gì, vì sao): [`dot-h-cho-duyet.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/nang-cap-h-tong-the/docs/dot-h-cho-duyet.md)
trên nhánh chốt. Bản thử dùng các số này; số nào cậu muốn đổi chỉ cần sửa một chỗ. Các số chính:

| Phần | Số | Khách thấy gì |
| --- | --- | --- |
| H1 | Chờ Windows dựng một trang PDF tối đa **30 giây**; ảnh trang rộng **1024 – 6000 px** | Trang quá nặng thì lui về ảnh chụp AutoCAD |
| H1 | Tên bản mới cạnh skill đã sửa: **`.ban-moi`**, rồi **`-2`, `-3`…** | Tên file trong thư mục skill |
| H2 | Đời DWG / DXF mặc định **2013**; cho chọn 2018, 2013, 2010, 2007, 2004, 2000; FBX tối đa **2 000** khối | Tên file `…-acad2013.dwg`; nhiều khối hơn thì chọn bớt |
| H3 | Dung sai **1 mm**; thể tích / diện tích lệch ≤ **0,1 %** coi như không đổi | Chỗ lệch nhỏ không bị báo |
| H3 | Tối đa **20 000** đối tượng mỗi bên; **100** dòng trong khung chat (còn lại xem Excel); quá **50** đám mây thì hỏi thẻ, quá **500** không vẽ; quá **5 000** chỗ khác thì không vẽ mây | Giới hạn của một lần so |
| H3 | Đám mây trên layer **`X-SO-SANH`**, màu xanh lá thêm / đỏ bớt / vàng đổi, lề **5 %** (tối thiểu **100 mm**) | Màu và chỗ đám mây |
| H3 – H5 | File đọc tối đa **200 MB** | File lớn hơn bị từ chối / bỏ qua |
| H4 | Tối đa **20** bộ chuẩn mỗi máy; **1 000** layer, **100** kiểu mỗi loại, **10** khung tên, **12** cỡ chữ mỗi bộ; file mẫu chép giữ tối đa **50 MB**; tên tối đa **60** ký tự; sai số khổ giấy **2 mm** (khổ lạ thì dùng A3) | Giới hạn của một bộ chuẩn |
| H5 | Tối đa **200** bản vẽ mỗi lượt, thư mục con sâu **5** cấp; kể **30** tên file trong thẻ; **3** nhóm lỗi mỗi file; thư mục sao lưu **`_sao-luu-claudecad`** | Giới hạn của một lượt làm nhiều file |
| Chờ | Xuất file tối đa **5 phút**; so bản vẽ, học chuẩn **2 phút**; làm nhiều file: **2 phút** mỗi file khi soát / đếm, **5 phút** khi áp chuẩn / in PDF | Quá giờ thì Claude báo hết giờ; làm nhiều file thì dừng lượt và nhờ khách mở kiểm file đang làm |

Như đợt F / G, các số này vẫn là **chờ duyệt** tới khi cậu duyệt. Cậu chọn duyệt ngay theo bảng hay thử bản 6 rồi mới duyệt; khi
cậu quay lại, tớ hỏi một câu.

## 6. Quyết định tớ tự chọn (chính)

Đầy đủ: mục "Quyết định tự chọn" từng phần trong [`dot-h-cho-duyet.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/nang-cap-h-tong-the/docs/dot-h-cho-duyet.md).

- **H1:**
  - đọc PDF bằng bộ đọc có sẵn của Windows 10 / 11: bộ cài không nặng thêm. Đổi lại Windows 7 / 8 hoặc máy chặn PowerShell sẽ lui về ảnh chụp như cũ;
  - skill khách đã xóa chỉ quay lại khi bản cài sau có bản mới của skill đó. Muốn bỏ hẳn thì tắt trong ⚙.
- **H2:**
  - FBX do plugin tự ghi, không gọi lệnh `FBXEXPORT` của AutoCAD: một lệnh, không hỏi thêm, test được;
  - không hứa mở được bằng SketchUp, vì SketchUp đọc DWG / DXF 2013 tốt hơn.
- **H3:**
  - không dùng lệnh so sánh dựng sẵn của AutoCAD (không có giao diện lập trình cho lệnh đó); tự so trên bản đọc kèm;
  - bảng chỉ ra Excel, không vẽ lên bản vẽ;
  - lần so sau không xóa đám mây cũ.
- **H4:**
  - khung tên công ty thay cả khung viền VN;
  - khổ giấy lạ dùng A3 và nói rõ;
  - "sửa lỗi tự động" không áp chuẩn công ty;
  - kiểu mặc định của AutoCAD mà mẫu không dùng thì không học.
- **H5:**
  - chưa làm "sửa lỗi" và "so sánh" hàng loạt;
  - in PDF mở từng file chỉ để đọc;
  - lệnh làm nhiều file từ chối chạy trong macro, vì macro có thể lách thẻ xác nhận;
  - một file quá giờ thì dừng cả lượt.
- **Prompt:** mục 7 ("không đụng file khác") nay cho phép rõ các lệnh làm file khi khách yêu cầu và đã bấm thẻ.

## 7. Điểm đã biết, giữ nguyên

- **H3:** phòng vừa đổi hình vừa có hai góc lệch nhau dưới 1 mm theo chiều ngang có thể bị báo "bớt + thêm" thay vì "đổi". Hiếm gặp. Phòng giống hệt luôn nhận đúng.
- **H4:**
  - ô khung tên còn giá trị mặc định của file mẫu vẫn được tính là "đã điền";
  - lệnh "ra bộ hồ sơ" (G1) chưa nhận chuẩn công ty: bộ hồ sơ vẫn dùng khung tên VN. Cần thì tớ đưa vào phần 2.
- **Nghi vấn chưa chứng minh, chưa sửa:**
  - thư mục con kiểu "lối tắt" trỏ ra ổ mạng;
  - cùng một file gọi bằng hai kiểu đường dẫn (ổ ánh xạ và đường mạng);
  - trang PDF rất cao (chỉ giới hạn bề rộng);
  - máy chuyển từ ClaudeCAD gốc có thể bị báo nhầm "skill đã sửa".
- **Cần AutoCAD thật mới biết:**
  - mở / đóng nhiều file khi in PDF hàng loạt;
  - ghi file trên ổ mạng;
  - chép khung tên;
  - xóa đám mây bằng U;
  - đời DWG cũ mở bằng AutoCAD đời cũ.

## 8. Kiểm tra đã làm

| Bước | Lỗi tìm ra | Kết quả |
| --- | --- | --- |
| Rà từng phần (H1 4, H2 6, H3 4, H4 9, H5 4) | 27 | Sửa hết, CI xanh từng bước (log `# fail 0`) |
| Sửa CI Windows | 3 test hỏng bị giấu | Là lỗi của test, sửa test và cách CI đếm lỗi |
| Test tổng thể phần 1 | 2 lỗi chéo + 1 lỗi tài liệu | Sửa, CI 45802392263 |
| Kiểm tra độc lập (agent mới) | 27 lỗi (2 nặng, 13 vừa, 12 nhẹ) + 5 nghi vấn đúng là lỗi + 1 lỗi sinh ra khi sửa | Cả 28 sửa đúng, soát lại hai lượt, 0 lỗi mở. Báo cáo: [`ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md`](ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md) |

- **Hai lỗi nặng nhất của kiểm tra độc lập** (đều ở H5, đều đã sửa):
  - bấm Đồng ý xong không file nào chạy;
  - mỗi file chỉ có 30 giây.

  Lượt rà H5 không bắt được vì test đi tắt qua bước thẻ. Test mới đi đúng đường bấm thẻ thật.
- **Token lúc mở chat:** tăng khoảng 0,1 – 0,2k so với cuối đợt G (máy Full, nạp gọn bật: 19,2k → 19,3k), vì 5 lệnh mới đều hoãn nạp.

## 9. Thời gian và chi phí

- **Thời gian:** khoảng 5 giờ 10 phút (14h05 → 19h15), ước lúc lập kế hoạch 7–10 giờ.
- **Chi phí các thread cho phần 1** (số tích lũy mỗi thread trừ số lúc cuối đợt G):

  | Thread | USD |
  | --- | --- |
  | Thread đợt F1 (viết H1, H3; rà H4; sửa CI; test tổng thể; sửa H4; giữ nhánh chốt) | ≈ 47,5 |
  | Thread đợt F2 (viết H4, H5; rà H2; sửa H5) | ≈ 37,5 |
  | Thread Test & sửa F1 (viết H2; rà H1, H3, H5; gom số tiêu chuẩn phần 2; sửa H1 / H2 / H3) | ≈ 30,1 |
  | Kiểm tra độc lập đợt H phần 1 | ≈ 18,0 |
  | Bộ cài thử bản 6 | __CHI_PHI_BO_CAI__ |
  | **Cộng** | **≈ __TONG__** |

  Ước lúc lập kế hoạch: $135–205. Chưa kể luồng chính.
- **Phần tốn nhất:** các lượt rà từng phần (27 lỗi) và lượt sửa sau kiểm tra độc lập.
- Tớ không thấy số token chính xác theo từng phần; các số trên là tiền mỗi thread tự báo.

## 10. Nhánh và tài liệu

| Thứ | Ở đâu |
| --- | --- |
| Mã chốt phần 1 | `full/nang-cap-h-tong-the` @ `76ecfae` (code `93096dc`, CI 45813679683) |
| Bộ cài thử bản 6 | __NHANH_BO_CAI__ |
| Checklist thử tay | `docs/checklist-tong-the.md` trên nhánh chốt (khối "Đợt H phần 1") |
| Con số và quyết định chờ duyệt | `docs/dot-h-cho-duyet.md` trên nhánh chốt |
| Số tiêu chuẩn phần 2 | [`dot-h-so-tieu-chuan.md`](dot-h-so-tieu-chuan.md) |
| Kiểm tra độc lập | [`ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md`](ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md) |
| Kế hoạch và tiến độ | [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) mục "Đợt H" |
| Tổng quan dự án | [`tong-quan-du-an.md`](tong-quan-du-an.md) |
