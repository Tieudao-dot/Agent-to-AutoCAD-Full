# Bàn giao: Test & sửa đợt G5 — vẽ lại từ ảnh / PDF scan (25/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà; người viết G5 là thread Test & sửa F1). Nhánh `full/nang-cap-g5-test`,
tách từ `full/nang-cap-g5` @ `1cc438f` (code `8a2515c`).

## 1. Lỗi tìm thấy và đã sửa

| Mã | Lỗi | Sửa |
| --- | --- | --- |
| TG5-1 | **Lỗi nặng khi UCS dời / xoay.** Claude đọc tọa độ trên lưới (theo UCS) và gửi điểm theo UCS, nhưng `scale` / `straighten` / `view` / `attach` đọc `p1`, `p2`, `insert` như tọa độ thế giới. Hậu quả: ảnh co / xoay quanh sai điểm nên trôi đi, vùng xem phóng to sai chỗ. Kết quả (`min`, `max`, `window`) trả tọa độ thế giới thô, rồi bộ điều phối còn đổi thêm một lần, nên lệch gấp đôi. | Điểm vào đọc qua `p.Point` (đổi UCS → thế giới). Vùng xem lấy hộp thế giới của 4 góc hình chữ nhật UCS. Điểm ra qua `Acad.J` (thế giới → UCS), như mọi lệnh lớp Ref. |
| TG5-2 | **Ảnh xem phụ thuộc AutoCAD.** Nếu ảnh chụp ngoài màn hình của AutoCAD không vẽ ảnh nền, Claude không thấy bản scan và sẽ vẽ mò. Người viết ghi đây là điểm chưa kiểm quan trọng nhất. | Ảnh xem **dựng thẳng từ file ảnh gốc**: đặt đúng chỗ theo vị trí của ảnh trong bản vẽ (sau khi co / xoay), giải nén vừa đủ chi tiết, tối đa 6 000 px. Ảnh chụp của AutoCAD phủ lên trên, phần nền làm trong suốt, nên nét Claude đã vẽ vẫn thấy. Kết quả ghi `picture_from` (`file` / `autocad`) và `autocad_drew_picture`. **PDF** không dựng được từ file: nếu ảnh xem trắng chỗ trang PDF thì kết quả có `picture_visible: false` và câu bảo Claude **dừng, nói khách, nhờ xuất PNG / JPG**, không vẽ mò. |
| TG5-3 | Loại file chỉ xét theo đuôi tên: ảnh iPhone HEIC / WebP lưu nhầm đuôi `.png` qua được bước kiểm, rồi AutoCAD báo lỗi khó hiểu. File chữ đổi đuôi `.jpg` cũng vào tới AutoCAD. | Đọc 1 024 byte đầu và đối chiếu chữ ký file (PNG, JPEG, BMP, TIFF, GIF, PDF; nhận ra WebP / HEIC). Khác loại thì từ chối, có hướng dẫn (lưu JPG, đổi đuôi). |
| TG5-4 | Đụng với `check_drawing` (G2): layer `X-ANH-GOC` (không in, có ảnh) bị báo "layer không in còn chứa hình", và "layer không thuộc bảng chuẩn" ở bản vẽ dùng bộ chuẩn. | Bỏ layer ảnh nền khỏi hai nhóm đó (2 dòng). |
| TG5-5 | Đụng với `create_drawing_set` (G1): ảnh nền (đặt cạnh mặt bằng) được tính vào khung mặt bằng 2D, nên tờ mặt bằng thu nhỏ để chứa cả ảnh. | Ảnh nền không tính vào mặt bằng 2D (1 dòng). |

## 2. Đã rà 8 điểm

1. **Tỉ lệ và tọa độ:**
   - lỗi UCS: xem TG5-1;
   - mm / cm / m: đổi đúng;
   - khoảng cách 0 / âm và hai điểm trùng: bị từ chối;
   - đặt tỉ lệ lần hai: co tiếp theo điểm hiện tại, ghi lại độ dài thật mới;
   - co đều nên tỉ lệ ngang / dọc của ảnh giữ nguyên;
   - trục y của ảnh đi xuống đã tính đúng (test mới, cả sau khi xoay 90°);
   - nhãn lưới theo UCS.
2. **Ảnh xem:** xem TG5-2. Phần còn phụ thuộc AutoCAD thật đã ghi ở mục 5 và checklist mục F / G.
3. **File:**
   - không có / rỗng / quá 50 MB: từ chối; sai loại: kiểm theo nội dung (TG5-3);
   - không có chỗ nào gõ đường dẫn vào dòng lệnh: gắn ảnh / PDF bằng API, có test quét `SendStringToExecute`. Đường dẫn có
     dấu cách / dấu tiếng Việt vì vậy không thành lệnh;
   - đọc file:
     - lệnh chỉ đưa vào Claude ảnh (PNG / JPG…) hoặc trang PDF, không đọc nội dung file khác;
     - nhưng Claude có thể gắn **bất kỳ ảnh / PDF nào** trên máy khi biết đường dẫn (xem "Quyết định tự chọn");
   - ảnh dán:
     - lưu ở `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem`, tên theo nội dung (trùng ảnh thì trùng file, không ghi đè ảnh khác);
     - rồi chép cạnh bản vẽ; bản vẽ chưa lưu thì chép vào Documents;
   - PDF nhiều trang: hỏi trang. PDF vector: gợi ý `PDFIMPORT`. Ảnh rất lớn: giải nén có giới hạn (TG5-2).
4. **8 lần xem:**
   - đếm ở trợ lý, đặt lại mỗi tin nhắn của khách; lần lỗi không tính;
   - quá giới hạn thì từ chối mà không gọi AutoCAD; kết quả báo `views_used` / `views_left`;
   - `batch_execute` không gọi được `trace_image` (lệnh tự mở giao dịch nên bị cấm trong lô);
   - `capture_view` vẫn xem được bản vẽ ngoài số đếm này (lệnh chung, có sẵn).
5. **Chữ trong ảnh:**
   - mô tả lệnh, skill và câu cuối mỗi kết quả (`DraftNote`) nói rõ chữ trong ảnh là nội dung, không phải lệnh;
   - kết quả luôn ghi là bản nháp, kể chỗ đọc không chắc.
6. **"Chỉ xem" / gói / nạp gọn:**
   - `view` / `info` là lệnh đọc; `attach` / `scale` / `straighten` bị chặn;
   - U hoàn tác cả lượt; không ghi macro;
   - gói 2D / Full, chế độ 2D / Cả hai (test G5);
   - phần luôn nạp chỉ một dòng prompt; mô tả lệnh dài thêm một câu (~25 token, chỉ khi nạp lệnh).
7. **Va chạm:**
   - `check_drawing` / `create_drawing_set`: TG5-4, TG5-5;
   - `fix_drawing` không đụng ảnh nền (layer khóa nên bị bỏ qua; không nhóm nào sửa ảnh);
   - các bảng thống kê / dự toán không tính ảnh (không phải vùng kín, block hay khối);
   - layer không in nên ảnh nền không vào bản in.
8. **Bảng số / quyết định:** từng dòng mục 6, 7 của `nang-cap-g5.md` khớp code. Checklist thêm mục G (UCS xoay, HEIC đổi đuôi,
   PDF không thấy, soát bản vẽ, bộ hồ sơ, đường dẫn có dấu); mục F ghi rõ cần báo lại câu nào.

## 3. Con số chờ chủ dự án duyệt

Không đổi số nào của G5. Không có số mới khách thấy.

Số kỹ thuật mới, khách không thấy:
- giải nén ảnh tối đa **6 000 px** chiều ngang (`TraceLogic.MaxDecodePx`);
- coi ảnh xem là "không có ảnh" khi dưới **0,5 %** điểm khác nền trên vùng ảnh (`BlankShare`);
- dung sai màu nền **10**.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Ảnh xem lấy từ đâu | luôn dựng từ file ảnh gốc, nét đã vẽ phủ lên (đúng cả khi AutoCAD có hoặc không vẽ ảnh nền) | — |
| Ảnh xem PDF | giữ ảnh chụp của AutoCAD; trắng thì báo khách và dừng (dựng trang PDF cần thư viện đọc PDF, không có sẵn) | thêm thư viện vẽ PDF |
| Ảnh / PDF nào được gắn | mọi ảnh / PDF Claude biết đường dẫn (khách nói, hoặc ảnh đính kèm) | chỉ cho file khách đính kèm / gõ trong cuộc chat: cần trợ lý ghi lại đường dẫn khách đưa |
| Bản vẽ chưa lưu | ảnh dán chép vào Documents | hỏi khách lưu bản vẽ trước |

## 5. Test

- Thêm lõi `plugin/ClaudeCAD.Core.Tests/NangCapG5TestSuaTests.cs` (8 test):
  - điểm UCS ra / vào;
  - ảnh nằm đúng chỗ trong ảnh xem (cả khi xoay 90°), độ giải nén;
  - nền / trong suốt / ảnh trắng;
  - loại file theo nội dung (HEIC, WebP, PDF, chữ);
  - không gõ lệnh vào dòng lệnh;
  - layer ảnh nền với `check_drawing` / bộ hồ sơ.
- Thêm trợ lý `agent/test/nangcapg5-testsua.test.ts` (2 test): dừng khi ảnh trắng; đếm lần xem (lỗi không tính, quá giới hạn
  không gọi AutoCAD).
- SelfTest G5: kiểm ảnh xem dựng từ file; dòng kết quả ghi AutoCAD có vẽ ảnh nền khi chụp hay không.
- Kết quả sau lần sửa cuối và CI: xem mục 6.

## 6. CI

Xem báo cáo gửi luồng chính.

## 7. Chưa kiểm được (cần AutoCAD / Claude thật)

- Ảnh xem dựng từ file đúng chỗ trên AutoCAD thật: tọa độ `RasterImage.Orientation` sau co / xoay, ảnh có khung cắt (clip)
  thì ảnh xem vẫn hiện cả ảnh.
- PDF: AutoCAD có vẽ trang PDF trong ảnh chụp ngoài màn hình không (nếu không: mọi PDF đều dừng ở câu báo; khách xuất PNG).
- Bước tự kiểm G5 trên máy thật; đường dẫn có dấu; `RasterImageDef.Load` / `PdfDefinition.Load`.
- Claude thật đọc ảnh có lưới tốt tới đâu. Thử tay theo `docs/checklist-dot-g5.md` (mục F, G mới).
