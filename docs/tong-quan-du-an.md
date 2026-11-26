# Tổng quan dự án Agent to AutoCAD Full

*Cập nhật: 26/11/2026 (giờ Việt Nam). Luồng chính giữ file này. Bản tổng quan chép từ dự án gốc ClaudeCAD nằm trong
lịch sử git của file này (bản ngay trước commit `ed0753a`) và ở nhánh `luu-tru/2d-base`.*

Bản Full = plugin ClaudeCAD (2D) + **gói dịch vụ theo máy (2D / 3D / Full)** + **vẽ 3D**. Độc lập với dự án gốc;
quy tắc làm việc và danh bạ thread: [`../CLAUDE.md`](../CLAUDE.md).

## 1. Tình hình

*Cập nhật 26/11/2026 05h40 (giờ VN).*

- **Đợt F và đợt G xong (25–26/11):** chủ dự án thử thật bộ cài thử bản 3 chạy ổn, chốt nâng cấp tiếp trước khi phát hành.
  - Đợt F (F1 → F5): UCS + nút chế độ; 2D dùng hằng ngày; 3D đẹp và đúng hơn; mở chat nhanh + xuất Excel; ô nạp gọn,
    thẻ xác nhận ghi đè, ghi chú mũi tên.
  - Đợt G (G1 → G5): ra bộ hồ sơ một lệnh; soát bản vẽ; bảng dự toán; render thật; vẽ lại từ ảnh / PDF scan.
  - Mỗi phần có thread viết và thread khác rà; test tổng thể cuối mỗi đợt; kiểm tra độc lập hai lần (đợt F: 9 lỗi, 9
    sửa; F + G: 43 lỗi, 42 sửa, 1 lỗi nhẹ chấp nhận). Tổng 97 lỗi đã ghi, 96 sửa.
  - **Nhánh chốt: `full/nang-cap-g-tong-the` @ `547d6af`** (code `e1455fb`, CI 45710924398 xanh, lõi 1445, trợ lý 407).
  - Báo cáo cho chủ dự án: [`bao-cao-dot-f-g-26-11.md`](bao-cao-dot-f-g-26-11.md). Tiến độ từng bước:
    [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) mục "Đợt F", "Đợt G".
  - **Đính chính 26/11:** log CI của commit chốt (run 45710924398) và bản 5 (45713707097) còn 3 test trợ lý hỏng trên
    Windows (`# fail 3`; Linux qua hết), CI không báo đỏ vì bước test chỉ tính lệnh cuối. Đang chẩn đoán và sửa ở
    `full/nang-cap-h-sua-ci`. Luồng chính từ nay đọc số `# fail` trong log trước khi báo CI xanh. **Kết luận 14h58:** cả
    3 là lỗi của test (đường dẫn, xuống dòng), sản phẩm và bản 5 không bị ảnh hưởng; đã sửa test và CI (`554307b`, CI
    45778275884, `# fail 0`).
  - **Chưa phần nào của đợt F / G chạy trên AutoCAD thật. Con số chờ chủ dự án duyệt:**
    [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md), [`dot-g-cho-duyet.md`](dot-g-cho-duyet.md).

- **Đợt H đang chạy (từ 26/11):** nhóm 1 (việc nhỏ còn sót) và nhóm 3 (8 tính năng mới), chia hai phần. Phần 1: H1 nhóm 1,
  H2 xuất DXF / DWG đời cũ / FBX, H3 so sánh hai bản vẽ, H4 học chuẩn công ty, H5 làm nhiều file một lúc → bộ cài thử bản 6.
  Phần 2: H6 hạ tầng, H7 ống MEP 3D, H8 kết cấu thép, H9 tiếng Anh → bản 7. Kế hoạch, chi phí và tiến độ:
  [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) mục "Đợt H".

- **Nhánh tích hợp trước đợt F:** `full/nang-cap-tong-the` @ `012ed29` (có sửa lỗi bộ cài TT-01 và bản sửa khung chat 15/11; CI 43669231795 xanh trên mã khung chat). Chứa toàn bộ: gói dịch vụ,
  3D đợt 1–3, test tổng thể + 4 lượt kiểm tra độc lập lần 1 (43 lỗi), bộ cài thay bản gốc + cảnh báo, và **chuỗi nâng
  cấp A → E** (13/11) + test tổng thể nâng cấp.
  - Test lõi 910, trợ lý 298, UI 33, build net48 / net8 / net10 sạch.
  - Nhánh tích hợp cũ `full/test-tong-the` @ `19944df` vẫn giữ (nơi sửa lỗi thử thật phần 3D cũ, hiện chưa có lỗi nào).
  - **Chủ dự án đã thử thật bằng bộ cài thử bản 3: chạy ổn (25/11). Chưa phát hành.** `main` vẫn là v0.9.3 của bản gốc.
- **Chuỗi nâng cấp lệnh vẽ (13/11):** [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) (bảng tiến độ). Cả 5 đợt
  xong (~$96, 22 lỗi sửa trong các bước Test & sửa), test tổng thể nâng cấp xong (3 lỗi chéo). **Kiểm tra độc lập
  nâng cấp** (agent mới, 3 lượt): 32 lỗi, 30 sửa đúng, 2 chấp nhận, **0 mở**; báo cáo
  `docs/ban-giao/kiem-tra-doc-lap-nang-cap.md` trên `full/kiem-tra-doc-lap-2`. **Báo cáo cho chủ dự án:**
  [`bao-cao-nang-cap-14-11.md`](bao-cao-nang-cap-14-11.md).
- **Token mỗi lần mở chat** (mô tả lệnh + system prompt): 2D 34,4k → 38,7k, 3D 35,7k → 39,7k, Full 49,7k → 57,5k
  (trước / sau chuỗi nâng cấp; phần này được cache, lượt sau trong 5 phút ~10%). **Sau đợt F + G** (ước tính): bật "Nạp gọn
  lệnh" (mặc định) 2D 13,7k, 3D 19,1k, Full 21,0k; tắt thì 45,5k / 45,2k / 66,8k.
- **Chưa dùng được bộ cài thật:** `activation/activation.json` và khóa công khai trong `License.cs` vẫn là của dự án
  gốc; chờ chủ dự án dựng máy chủ kích hoạt riêng.
- **Bộ cài thử nội bộ** (chỉ chủ dự án dùng, không gửi ai; bỏ qua kích hoạt, gói Full, hết hạn 10/12/2026, nhãn "BẢN THỬ
  NỘI BỘ", không tự cập nhật; nhánh `full/ban-thu-noi-bo`, **không bao giờ gộp**). **Bản mới nhất (bản 5, 26/11):** đủ đợt F + G: `c901952`, CI 45713707097, artifact `AgentToAutoCADFull-ThuNoiBo-nang-cap-5-0.9.4` ([tải](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45713707097/artifacts/15069481207), 135 MB, tới 23/02/2027). Cài đè lên bản 3 phải xóa 4 thư mục skill cũ trước (bộ cài không ghi đè skill đã có). Link và hướng dẫn cài ở thread "Lưu trữ bộ cài" (`session_01D56HmJdn7RASaJtfgMQp5x`). Bản 4 (25/11, chỉ có đợt F) là mốc kiểm nội bộ, không gửi. **Bản 3 (15/11, chủ dự án đã thử):** thêm khung chat mượt khi chat dài, icon nét kiểu AutoCAD, thu gọn cân đối: `d602051`, CI 43670758598, artifact `AgentToAutoCADFull-ThuNoiBo-nang-cap-3-0.9.4` ([tải](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43670758598/artifacts/14143819585), tới 13/02/2027). **Bản 2 (14/11 tối, đã thay):** có đủ
  chuỗi nâng cấp + sửa lỗi bộ cài TT-01 (hỏi đóng AutoCAD bằng tiếng Việt, tự tắt tiến trình phụ của plugin):
  `45b45a6`, CI 43614348802, artifact `AgentToAutoCADFull-ThuNoiBo-nang-cap-2-0.9.4` ([tải](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43614348802/artifacts/14121404198), tới
  12/02/2027). Cài đè lên bản cũ được. Các bản cũ (CI 43373318854, 43481896536) bỏ.
  Bàn giao: `docs/ban-giao/ban-thu-noi-bo.md` trên nhánh đó.
- **Checklist thử tay:** [`checklist-tong-the.md`](checklist-tong-the.md) trên nhánh tích hợp: phần cũ I–M, X (rút gọn
  ~97 phút, đủ ~157 phút) + khối **chuỗi nâng cấp A → E** (rút gọn ~95 phút, đủ ~3 giờ; mục N–R, chéo V1–V8). Chi tiết
  ở [`checklist-thu-that.md`](checklist-thu-that.md). Thêm khối **"Đợt F"** (~2 giờ) và **"Đợt G"** (~3 giờ, mục G0 làm
  trước) ở bản trên nhánh `full/nang-cap-g-tong-the`. Bản dùng thử AutoCAD hết khoảng 10/12/2026.
- **Báo cáo sáng 13/11:** [`bao-cao-sang-13-11.md`](bao-cao-sang-13-11.md). Chủ dự án đã đồng ý cả 9 quyết định
  luồng chính đưa ra trong đêm (13/11).

## 2. Việc chờ chủ dự án

1. **Dựng máy chủ kích hoạt riêng** theo [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md), gửi luồng
   chính URL web app + 2 dòng khóa công khai (không gửi khóa riêng / SECRET).
2. Sau bước 1: luồng chính giao gắn URL / khóa, build **bộ cài thử**; chủ dự án thử theo checklist tổng, gửi lỗi.
3. ~~Thử bằng bộ cài thử nội bộ~~: **đã thử bản 3, chạy ổn (25/11).** Tiếp theo: thử **bộ cài thử bản 5** theo khối
   "Đợt G" (mục G0 trước) và "Đợt F" của checklist tổng.
4. Xem 2 quyết định luồng chính chọn theo đề xuất của kiểm tra độc lập (bản vẽ mét tự quy đổi; mái nhà chữ L / T báo
   trước chỗ chồng) và 3 số đợt E tự chọn (bản thang 150, khe 2 vế chữ U 100, bản mái dốc 150 mm).
5. Trước khi phát hành: số phiên bản (plugin / trợ lý vẫn ghi 0.9.4), secret CI của repo Full, đồng ý merge `main`,
   các chỗ còn trỏ về dự án gốc ([`cho-tro-ve-repo-goc.md`](cho-tro-ve-repo-goc.md)). Cách cập nhật skill cho máy nâng
   cấp đã chốt 26/11 (cập nhật, giữ bản khách đã sửa), làm trong đợt H1.
6. **Duyệt con số và quyết định tự chọn của đợt F + G** (chủ dự án chọn 26/11: thử thật rồi duyệt): [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md),
   [`dot-g-cho-duyet.md`](dot-g-cho-duyet.md); 6 quyết định của luồng chính ở mục 6 của
   [`bao-cao-dot-f-g-26-11.md`](bao-cao-dot-f-g-26-11.md).

## 3. Đã chốt

| Ngày | Điểm | Chốt |
| --- | --- | --- |
| 12/11 | Máy chủ kích hoạt | Riêng cho bản Full; chủ dự án tự dựng (Sheet + Apps Script + cặp khóa mới) |
| 12/11 | Tên gói | 2D / 3D / Full |
| 12/11 | Gói mặc định cho máy mới | 2D (ô Gói trống / gõ sai cũng là 2D) |
| 12/11 | Chia gói | Phương án A: gói 3D có đường cơ bản, không có chữ / kích thước / hatch / layout / in 2D |
| 12/11 | Thẻ xác nhận 3D | Mọi lệnh làm đổi khối có trước lượt (cộng / trừ, cắt, bo, vát, làm rỗng, phá khối 3D), kể cả khối Claude dựng lượt trước |
| 12/11 | Skill 2D | `ve-kien-truc`, `ve-mep`, `ve-ha-tang` gắn `plan: 2d`: gói 3D không nạp |
| 12/11 | Giảm token | Cách an toàn (bỏ thuộc tính lặp ở lệnh dựng khối, rút mô tả), không đổi tên lệnh |
| 12/11 | Máy đã có ClaudeCAD gốc | Bản Full **thay thế** bản gốc như nâng cấp (giữ dữ liệu, kích hoạt lại ở Sheet mới); bộ cài tự nhận ra bản gốc và hỏi "Tiếp tục / Hủy"; tên hiển thị "Agent to AutoCAD Full" |
| 13/11 | 9 quyết định luồng chính đưa ra đêm 12/11 (chủ dự án **đồng ý** sáng 13/11) | Xem [`bao-cao-sang-13-11.md`](bao-cao-sang-13-11.md) mục 3: nút "Tiếp tục / Hủy"; xóa khối 3D cũ luôn hỏi; chỉ nút Đồng ý trên thẻ mở khóa sửa khối 3D cũ; chặn thuộc tính block ở gói 3D; máy offline chỉ nhận dòng gói / trạng thái mới hơn; giữ phương án A; thêm ô dán mã ở thẻ 🔒 và ⚙; chấp nhận chữ mặc định khi chèn block ở gói 3D |
| 13/11 | 2 commit đầu có tên model | Để nguyên trên các nhánh làm việc; khi phát hành gộp vào `main` thành một commit gọn (squash) để lịch sử `main` sạch |
| 13/11 | Kế hoạch nâng cấp lệnh vẽ | Gộp 6 lệnh dựng khối (đợt A); nâng cấp xong mới phát hành; gói lệnh mới theo phương án A (GROUP / MATCHPROP mọi gói); thứ tự A → B → C → D → E, F sau phát hành; sau đó đổi sang chạy liền A → E, bắt đầu ngay, dừng trước phát hành. Xem [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) |
| 13/11 | Đợt E: cầu thang / mái | 3 kiểu cầu thang + 3 kiểu mái; mặc định cao bậc 160, rộng bậc 280, vế 1000, mái bằng dày 150, mái dốc 30°, đua 600 (mm) |
| 13/11 | Cập nhật tình hình | Mọi sub-thread (kể cả thread con) gửi tin ngắn `[Cập nhật từ thread …]` về luồng chính ở các mốc (bắt đầu, push + CI, kết quả CI, mở thread con, xong, bị chặn); luồng chính ghi tình hình trong thread chính, không push noti cho từng tin. Chi tiết trong `CLAUDE.md` |
| 13/11 | Thanh tác vụ bên phải | Mọi việc đang chạy hiện thành task ở thanh bên phải của thread chính (1 task / thread đang chạy, trạng thái = bước hiện tại); sub-thread cũng hiện tác vụ của mình. Chi tiết trong `CLAUDE.md` |
| 14/11 | Chọn vẽ 2D / 3D | Giữ như hiện tại: máy gói Full dùng cả 2D lẫn 3D cùng lúc, Claude tự chọn lệnh. Nút chọn chế độ "Cả hai / 2D / 3D" ghi vào đợt F, quyết sau khi thử thật |
| 15/11 | Dọn thread | Đóng 13 thread đã xong (Gói dịch vụ, Test tổng thể bản đầu, Kiểm tra độc lập nâng cấp, đợt A → E và 5 thread Test & sửa). Lỗi thuộc phần thread đã đóng chuyển cho "Test tổng thể nâng cấp" |
| 25/11 | Đợt F làm ngay | Chủ dự án thử thật bản 3 ổn, chốt nâng cấp tiếp cả 4 nhóm trước khi phát hành: F1 UCS + nút chế độ, F2 2D dùng hằng ngày, F3 3D đẹp và đúng hơn, F4 mở chat nhanh + xuất Excel; ước $125–200 |
| 25/11 | Kiểm tra trước khi báo hoàn thành | Mỗi đợt có Test & sửa + CI; cuối chuỗi test tổng thể + kiểm tra độc lập; luồng chính tự kiểm CI và file bộ cài rồi mới báo "hoàn thành" |
| 25/11 | File quyền cho thread | Luồng chính thêm hộ `.claude/settings.json` trên `full/nang-cap-f-goc` (ngoại lệ một lần) để thread mới tự commit / push nhánh `full/…` và chạy CI; không cho đẩy `main` |
| 25/11 | Con số phải xác nhận trước | Mọi con số khách sẽ gặp hoặc làm đổi kết quả (số mặc định, kích thước, ngưỡng…) phải hỏi chủ dự án xác nhận trước khi làm; thread gom danh sách số gửi luồng chính, luồng chính hỏi. Số kỹ thuật bên trong thì không hỏi, ghi trong bàn giao |
| 25/11 | Số mặc định cầu thang / mái dốc | Đổi: bản thang + chiếu nghỉ dày **120** mm (trước 150), bản mái dốc dày **120** mm (trước 150); giữ khe giữa 2 vế thang chữ U **100** mm. Mái bằng dày 150 giữ như đã chốt 13/11. Sửa trong đợt F3 |
| 25/11 | Số mặc định lan can | Xác nhận giữ: cao 1100; khe giữa các song ≤ 100; trụ cách nhau ≤ 1500; tay vịn và trụ 50 × 50; song 20 × 20; thanh dưới cách sàn 100; tấm kín dày 20 (mm) |
| 25/11 | Cách chạy đợt F | Sau F1: F2 và F3 chạy cùng lúc, mỗi đợt có Test & sửa riêng; thread F4 gộp hai nhánh, CI xanh rồi mới làm F4. Nhanh hơn ~1,5 giờ, thêm ~$5–10 |
| 25/11 | Chạy liền, không hỏi lại | Chủ dự án dặn cứ triển khai đợt F theo kế hoạch, không hỏi lại. Con số: thread dùng số đề xuất, gom bảng "Con số chờ chủ dự án duyệt"; quyết định khác ghi "Quyết định tự chọn"; chủ dự án duyệt một lần ở báo cáo cuối. Vẫn dừng trước phát hành |
| 25/11 | Làm tiếp đợt F5 và đợt G | Chủ dự án chọn làm cả hai nhóm luồng chính đề xuất: F5 (ô nạp gọn trong ⚙, thẻ xác nhận đồng bộ, ghi chú có mũi tên) và đợt G (ra bộ hồ sơ, soát lỗi bản vẽ, bảng dự toán, render thật, vẽ lại từ ảnh / PDF scan; ước $150–200). Chạy liền như đợt F; G1 cùng lúc G2, G3 cùng lúc G4 |
| 25/11 | Giao bộ cài | Chỉ gửi một lần khi xong hết (bản 5, sau đợt G). Trước khi gửi: một agent khác test lại toàn bộ tính năng một lượt. Gửi bằng cách tạo thread "Lưu trữ bộ cài" và đưa link / file bộ cài vào đó. Bản 4 chỉ là mốc kiểm nội bộ, không gửi |
| 26/11 | Duyệt con số đợt F + G | Chủ dự án chọn "thử thật rồi duyệt": bản thử dùng số đề xuất; chủ dự án thử bộ cài thử bản 5, số nào chưa hợp thì báo; thử xong luồng chính mới ghi "đã duyệt". Tới lúc đó các số trong [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md), [`dot-g-cho-duyet.md`](dot-g-cho-duyet.md) vẫn là chờ duyệt |
| 26/11 | Dọn thread | Chủ dự án cho đóng 6 thread không còn việc: F3 lần 1, F3 lần 2, Test & sửa F2 (thread con), Kiểm tra độc lập đợt F, Tối ưu khung chat, Commit & CI. Đã đóng cả 6. Thread con Test & sửa F2 còn một bản vá chưa lưu: luồng chính lấy về [`ban-giao/f2-test-thread-con-ban-va.md`](ban-giao/f2-test-thread-con-ban-va.md) trước khi đóng; đối chiếu thấy 7 điểm sửa đều đã có trên nhánh chốt, còn vài test có thể lấy thêm ở lần sửa lỗi kế tiếp |
| 26/11 | Dọn thread (lần 2) | Chủ dự án bảo dọn các thread không dùng nữa. Đóng thêm 3: Kiểm tra độc lập đợt F + G (xong việc), Test tổng thể nâng cấp (nhánh đã được `full/nang-cap-g-tong-the` thay), QnA (không dùng từ 13/11, đọc code cũ). **Chủ dự án bảo giữ QnA để dùng sau: đã mở lại ngay, dặn thread đọc code mới nhất.** Còn mở: luồng chính, ba thread lưu được code (đợt F1, Test & sửa F1, đợt F2), Bộ cài thử nội bộ, Lưu trữ bộ cài, QnA, Bảo mật. Lỗi thuộc phần thread đã đóng chuyển cho thread đợt F2 |
| 26/11 | Đợt H | Chủ dự án chọn làm nhóm 1 + nhóm 3. Chốt 3 điểm: **chia hai phần** (phần 1 H1 → H5 ra bản 6, phần 2 H6 → H9 ra bản 7, chạy tiếp ngay); **chạy liền, số tiêu chuẩn hỏi trước** (cặp chạy cùng lúc H1 + H2, H3 + H4, H6 + H7; số thép / trắc dọc / ống hỏi chủ dự án trước khi viết); **skill trên máy nâng cấp: cập nhật, giữ bản khách đã sửa** |
| 12/11 | Cách làm việc | Luồng chính điều phối; mỗi đợt một thread, thread đợt tự mở thread con "Test & sửa" |

## 4. Đã làm

| Phần | Nội dung | Nhánh | Bàn giao |
| --- | --- | --- | --- |
| Gói dịch vụ | Cột Gói + dòng ký số `CCF1` ở máy chủ; plugin đọc gói, chặn lệnh ngoài gói (lệnh lẻ / batch / macro); trợ lý chỉ nạp lệnh của gói; giao diện "Gói: …" | `full/goi-dich-vu` | [`ban-giao/goi-dich-vu-plugin.md`](ban-giao/goi-dich-vu-plugin.md) |
| 3D đợt 1 | 14 lệnh: khối cơ bản, extrude / revolve, cộng trừ khối, góc nhìn, kiểu hiển thị, thông tin khối; chụp ảnh 3D; tự kiểm tra | `full/ve-3d` → `full/ve-3d-test-1` (7 lỗi sửa) | [`ve-3d-dot-1.md`](ban-giao/ve-3d-dot-1.md), [`ve-3d-test-1.md`](ban-giao/ve-3d-test-1.md) |
| 3D đợt 2 | 12 lệnh: sweep, loft, cắt / bo / vát / làm rỗng, xoay / đối xứng / căn 3D, UCS, va chạm; danh sách cạnh / mặt | `full/ve-3d-dot-2` → `full/ve-3d-test-2` (6 lỗi) | [`ve-3d-dot-2.md`](ban-giao/ve-3d-dot-2.md), [`ve-3d-test-2.md`](ban-giao/ve-3d-test-2.md) |
| 3D đợt 3 | 3 lệnh: dựng 3D từ mặt bằng, mặt cắt, hình chiếu (API .NET, không gọi FLATSHOT); skill `ve-3d-kien-truc` | `full/ve-3d-dot-3` → `full/ve-3d-test-3` (13 lỗi) | [`ve-3d-dot-3.md`](ban-giao/ve-3d-dot-3.md), [`ve-3d-test-3.md`](ban-giao/ve-3d-test-3.md) |
| Test tổng thể | Rà chéo gói × 3 đợt (1 lỗi), 11 test rà chéo, checklist tổng; lượt sửa 3 điểm đã chốt | `full/test-tong-the` | [`ban-giao/test-tong-the.md`](ban-giao/test-tong-the.md) |
| Nâng cấp A → E (13/11) | A: `draw_solid`, dựng 3D nhận layer tên khác, cột, nhiều tầng, lan can, `array_rect` số tầng; B: sửa hình 2D (kéo giãn, bẻ gãy, kéo dài, chia đều, nhóm, chép thuộc tính, bo / vát polyline + cung, offset hai phía, nối có dung sai); C: tiện ích 2D (hatch bấm điểm, kích thước mặt bằng, thống kê block, bảng diện tích, tìm & thay, in nhiều layout); D: dầm, tường cong, PRESSPULL, STL, bảng khối lượng / va chạm; E: cầu thang, mái | `full/nang-cap-{a..e}` → `-test` → `full/nang-cap-tong-the` | `ban-giao/nang-cap-*.md`, `test-tong-the-nang-cap.md`, `kiem-tra-doc-lap-nang-cap.md` |
| Đợt F (25/11) | F1: lệnh vẽ hiểu UCS, nút chế độ "Cả hai / 2D / 3D"; F2: style chuẩn Việt Nam, trang in có khung tên, block thư viện, trạng thái layer; F3: mái liền khối nhà chữ L / T / U, vật liệu, ảnh PNG, góc nhìn; F4: mở chat nhanh (nạp gọn lệnh), xuất Excel; F5: ô "Nạp gọn lệnh", thẻ xác nhận ghi đè, ghi chú mũi tên | `full/nang-cap-f{1..5}` → `-test` → `full/nang-cap-f-tong-the` @ `a6da118` | `ban-giao/nang-cap-f*.md`, `test-tong-the-dot-f.md` (trên nhánh chốt), [`ban-giao/kiem-tra-doc-lap-dot-f.md`](ban-giao/kiem-tra-doc-lap-dot-f.md) |
| Đợt G (25–26/11) | G1: `create_drawing_set` (ra bộ hồ sơ); G2: `check_drawing` / `fix_drawing` (soát và sửa bản vẽ); G3: `cost_estimate` (bảng dự toán, đơn giá do khách đưa); G4: render thật (`named_view` thêm `render`, có đường lui ảnh chụp); G5: `trace_image` (vẽ lại từ ảnh / PDF scan) | `full/nang-cap-g{1..5}` → `-test` → `full/nang-cap-g-tong-the` @ `547d6af` | `ban-giao/nang-cap-g*.md`, `test-tong-the-dot-g.md` (trên nhánh chốt), [`ban-giao/kiem-tra-doc-lap-dot-fg.md`](ban-giao/kiem-tra-doc-lap-dot-fg.md) |

Mỗi nhánh tách từ commit cuối của nhánh trước, nên `full/nang-cap-g-tong-the` chứa toàn bộ (kể cả `full/nang-cap-tong-the`).

## 5. Chi phí token mỗi lượt đầu phiên (mô tả lệnh + system prompt)

| Gói | Trước chuỗi nâng cấp (`19944df`) | Sau (`f1e7123`) |
| --- | --- | --- |
| 2D | 34,4k | 38,7k |
| 3D | 35,7k | 39,7k |
| Full | 49,7k | 57,5k |

Đo cùng một cách ở thread "Test tổng thể nâng cấp" (bảng cũ ~36k / ~37k / ~51k đếm hơi khác). Gộp 6 lệnh dựng khối ở
đợt A chỉ bớt ~1,1k (ước cũ ~2k), phần lệnh mới dùng lại khoản đó. Phần này nằm trong prefix được cache: các lượt sau
trong 5 phút chỉ tốn ~10%.

Sau đợt F + G (ước tính, đo ở thread test tổng thể đợt G; đợt F4 thêm "nạp gọn lệnh": lệnh ít dùng chỉ nạp mô tả khi cần):

| Gói | Trước đợt F | Cuối đợt G, nạp gọn (mặc định) | Cuối đợt G, tắt nạp gọn |
| --- | --- | --- | --- |
| 2D | 38,8k | 13,7k | 45,5k |
| 3D | 39,8k | 19,1k | 45,2k |
| Full | 57,7k | 21,0k | 66,8k |

## 6. Nhật ký

- **12/11/2026:**
  - tách repo từ dự án gốc;
  - làm gói dịch vụ;
  - chuỗi 6 bước 3D (đợt 1 → test → đợt 2 → test → đợt 3 → test → test tổng thể), mỗi bước 1 lượt CI xanh;
  - sửa 3 điểm chốt sau test tổng thể.
- **Đêm 12/11 → 13/11/2026:**
  - bộ cài thay bản gốc + cảnh báo;
  - kiểm tra độc lập 4 lượt bằng agent mới, lỗi chuyển về thread phụ trách sửa;
  - 3 vòng gộp + CI, kết thúc ở `860101b`.
- **13/11/2026:**
  - chủ dự án đồng ý 9 quyết định đêm qua, chốt squash khi phát hành;
  - bộ cài thử nội bộ (`full/ban-thu-noi-bo`), thread QnA;
  - kế hoạch nâng cấp lệnh vẽ, chốt 5 điểm; duyệt đợt A, chờ thử 3D thật;
  - chủ dự án cho đóng các thread đã xong việc: 3D đợt 1, 2, 3 + 3 thread con Test & sửa, Kiểm tra độc lập. Còn mở:
    Gói dịch vụ, Test tổng thể (nhận lỗi 3D khi thử thật), Bộ cài thử nội bộ, QnA;
  - chủ dự án cho chạy liền chuỗi nâng cấp A → E; mở thread Nâng cấp đợt A.
  - chuỗi nâng cấp A → E chạy liền 14h28 → 23h50 (~$96, 22 lỗi sửa trong Test & sửa), test tổng thể nâng cấp (3 lỗi chéo).
- **14/11/2026 (rạng sáng):**
  - kiểm tra độc lập nâng cấp lượt 1: 31 lỗi (0 nặng, 7 vừa, 24 nhẹ); chuyển về 6 thread sửa; lượt 2: 29 sửa đúng,
    2 chấp nhận, 1 lỗi mới; lượt 3: sửa đúng, 0 lỗi mở. Nhánh tích hợp cuối `full/nang-cap-tong-the` @ `f7f32ad`, CI xanh;
  - bộ cài thử nội bộ build lại có đủ nâng cấp (CI 43481896536); báo cáo [`bao-cao-nang-cap-14-11.md`](bao-cao-nang-cap-14-11.md).
- **14/11/2026 (chiều):** mở thread Bảo mật (`session_01D56HmJNNATNAVHRZejGSMu`) để chủ dự án trao đổi về khóa key, máy chủ
  kích hoạt và quản lý thiết bị.
- **14/11/2026 (tối):** chủ dự án thử bộ cài thử nội bộ, gặp lỗi bộ cài không đóng được ứng dụng đang dùng file (hộp tiếng
  Anh của Inno). Thread Test tổng thể nâng cấp sửa (TT-01), thread Bộ cài thử nội bộ build lại (CI 43614348802).
- **14–15/11/2026:** chủ dự án báo khung chat giật. Thread Tối ưu khung chat sửa (tạm ngừng vẽ tin ngoài màn hình, chỉ
  dựng lại đoạn cuối khi Claude đang viết), thêm bộ icon nét kiểu AutoCAD và thu gọn cân đối theo yêu cầu thêm.
  - Thread bị chế độ Auto chặn commit khoảng 10 giờ; thread con "Commit & CI" lưu hộ: `full/toi-uu-khung-chat` @
    `983d4c2`, CI 43669231795 xanh (lượt đầu đỏ vì 2 UI test cũ chưa theo icon mới).
  - Gộp vào nhánh tích hợp (`012ed29`); bộ cài thử bản 3 (CI 43670758598). Đóng 13 thread đã xong.
- **25/11/2026:** chủ dự án thử thật bản 3: chạy ổn; chốt làm đợt F ngay (4 đợt) và yêu cầu kiểm tra trước khi báo hoàn
  thành. Tạo nhánh gốc `full/nang-cap-f-goc` @ `2dbeee2` có file quyền; mở thread Nâng cấp đợt F1
  (`session_01D56HmFAo4dZ7c8sbeNB9eh`).
  - 15h51: đợt F1 xong (viết + Test & sửa, 4 lỗi sửa, ~$21, `full/nang-cap-f1-test` @ `b2e6431`, CI 45578496260 xanh).
    Chủ dự án chốt thêm: con số phải xác nhận; bản thang và bản mái dốc 120 mm; F2 + F3 chạy cùng lúc; chạy liền không
    hỏi lại. 15h53 mở F2 (`session_01D56HmFcYrzsme7GoBm8tNC`) và F3 (`session_01D56HmFBfQ8mQ37WLtVFweF`).
  - Ba thread mở mới sau 15h53 (F3 lần 1, F3 lần 2, Test & sửa F2) bị chế độ Auto từ chối `git commit`, cho dừng. Việc
    chuyển cho các thread đã lưu được: thread Test & sửa F1 viết F3, thread F1 rà F2, thread F2 rà F3.
  - 17h08: đợt F2 xong (7 lỗi sửa, ~$23, `full/nang-cap-f2-test` @ `c9fdc08`, CI 45590233177 xanh).
  - 17h35: đợt F3 xong (2 lỗi sửa, ~$14, `full/nang-cap-f3-test` @ `53e18b5`, CI 45595009744 xanh).
  - 17h40: giao đợt F4 cho thread F1 (nhánh `full/nang-cap-f4`): gộp F2 + F3 trước, rồi mở chat nhanh + xuất Excel.
    Con số và quyết định chờ chủ dự án duyệt gom ở [`dot-f-cho-duyet.md`](dot-f-cho-duyet.md).
  - 18h20: F4 viết xong (`full/nang-cap-f4` @ `d6c15d6`, CI 45601966332 xanh, $14,6); 18h22 giao thread Test & sửa F1 rà
    trên `full/nang-cap-f4-test`.
  - 18h21–18h50: các thread dừng vì chạm giới hạn sử dụng 5 giờ; chủ dự án cho chạy tiếp sau khi giới hạn đặt lại.
  - 19h17: đợt F4 xong (3 lỗi sửa, ~$25, `full/nang-cap-f4-test` @ `1cdf8d4`, CI 45610810220 xanh). 19h20 giao thread F2
    làm test tổng thể đợt F trên `full/nang-cap-f-tong-the`.
  - 19h44: test tổng thể đợt F xong (3 lỗi nhẹ sửa, ~$10, `full/nang-cap-f-tong-the` @ `afdaa68`, CI 45614706040 xanh).
    19h45 mở thread Kiểm tra độc lập đợt F (`session_01D56HmHz4EXXJceBgkmjcap`).
  - ~20h: chủ dự án chọn làm tiếp hai nhóm. **Đợt F5** (ba việc nhỏ còn sót: ô nạp gọn trong ⚙, thẻ xác nhận đồng bộ,
    ghi chú có mũi tên) giao 20h05 cho thread Test & sửa F1 trên `full/nang-cap-f5`, kịp bộ cài thử bản 4. **Đợt G** (ra
    bộ hồ sơ, soát lỗi bản vẽ, bảng dự toán, render thật, vẽ lại từ ảnh / PDF scan; ước $150–200) chạy liền như đợt F,
    bắt đầu sau khi đợt F xong hẳn. Kế hoạch: [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) mục "Đợt G".
  - 19h57: kiểm tra độc lập ghi 8 lỗi (1 nặng: mái nhà L / T / U vẽ lệch ~1 mm dựng sai); thread F1 và F2 sửa, agent độc
    lập soát lại 3 lượt, thêm 1 lỗi nhẹ. Kết quả: 9 lỗi, 9 đã sửa đúng, 0 lỗi mở
    ([`ban-giao/kiem-tra-doc-lap-dot-f.md`](ban-giao/kiem-tra-doc-lap-dot-f.md)).
  - 21h00: đợt F5 xong (viết + rà, 2 lỗi thẻ sửa, ~$21). 21h15: nhánh tích hợp đợt F chốt ở `full/nang-cap-f-tong-the` @
    `a6da118` (code `7fe215c`, CI 45630097088 xanh, lõi 1217, trợ lý 364); giao build bộ cài thử bản 4.
  - 21h19: bộ cài thử bản 4 (chỉ đợt F, mốc kiểm nội bộ): `full/ban-thu-noi-bo` @ `2a68813`, CI 45632787564 xanh.
  - 21h25 → 26/11 01h19: **đợt G** chạy liền. G1 + G2 cùng lúc, G3 + G4 cùng lúc, rồi G5; mỗi phần một thread viết, một
    thread khác rà (22 lỗi sửa), CI xanh từng bước; test tổng thể đợt G (2 lỗi chéo), `full/nang-cap-g-tong-the` @
    `881b12e`. Dừng 23h00–23h50 vì chạm giới hạn sử dụng 5 giờ.
- **26/11/2026:**
  - 01h20 → 05h06: kiểm tra độc lập F + G bằng agent khác (chủ dự án yêu cầu trước khi gửi bộ cài): lượt đầu 27 lỗi, ba
    lượt sửa + soát lại; tổng 43 lỗi, 42 sửa đúng, 1 lỗi nhẹ chấp nhận (KFG-43). Dừng 02h37–04h50 vì chạm giới hạn sử
    dụng. Báo cáo: [`ban-giao/kiem-tra-doc-lap-dot-fg.md`](ban-giao/kiem-tra-doc-lap-dot-fg.md).
  - 05h24: nhánh chốt `full/nang-cap-g-tong-the` @ `547d6af` (code `e1455fb`, CI 45710924398 xanh, lõi 1445, trợ lý 407).
  - 05h36: bộ cài thử bản 5 (`c901952`, CI 45713707097 xanh). 05h38 mở thread "Lưu trữ bộ cài"
    (`session_01D56HmJdn7RASaJtfgMQp5x`) giữ link và hướng dẫn cài cho chủ dự án.
  - Báo cáo cuối chuỗi: [`bao-cao-dot-f-g-26-11.md`](bao-cao-dot-f-g-26-11.md). Chi phí các thread cho đợt F + G khoảng
    $312 (chưa kể luồng chính). Chờ chủ dự án: thử tay, duyệt con số.
  - Chủ dự án chọn làm tiếp nhóm 1 + nhóm 3 (**đợt H**), chốt 3 điểm. Giao H1 (thread đợt F1) và H2 (thread Test & sửa F1)
    chạy cùng lúc từ `547d6af`.
  - 14h31 H2 viết xong (`export_drawing`), 14h43 H1 viết xong; giao rà chéo (H2: thread đợt F2, H1: thread Test & sửa F1).
    Thread H1 phát hiện CI Windows bỏ qua test trợ lý hỏng (3 test, có từ bản chốt G và bản 5); giao thread đợt F1 sửa.
  - 14h58 sửa CI xong (`554307b`, `# fail 0`). 15h00 gộp H1 + H2 + sửa CI (`98eecf1`); H3 (so hai bản vẽ, thread đợt
    F1) và H4 (chuẩn công ty, thread đợt F2) chạy cùng lúc. 16h04 gộp H3 + H4 rà xong (`5184962`), giao H5 (làm nhiều file
    một lúc, thread đợt F2). 17h21 rà H5 xong. Các lượt rà sửa tổng 27 lỗi (H1 4, H2 6, H3 4, H4 9, H5 4).
  - 16h13 danh sách số tiêu chuẩn phần 2: [`dot-h-so-tieu-chuan.md`](dot-h-so-tieu-chuan.md), chờ chủ dự án duyệt.
  - 17h33 test tổng thể phần 1 xong (thread đợt F1): 2 lỗi chéo + 1 lỗi tài liệu, `full/nang-cap-h-tong-the` @ `aa5753c`
    (code `9612510`, CI 45802392263 xanh, `# fail 0`, lõi 1581, trợ lý 436). Token lúc mở chat tăng ~0,1–0,2k.
  - 17h46 mở thread kiểm tra độc lập đợt H phần 1 (`session_01D56HmH63NM9pSxbHQh6Ckv`, agent mới, chỉ ghi lỗi KTH-xx).
  - 18h05 kiểm tra độc lập báo 27 lỗi (2 nặng ở H5, 13 vừa, 12 nhẹ): [`ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md`](ban-giao/kiem-tra-doc-lap-dot-h-phan-1.md).
    18h10 chia ba thread sửa: đợt F2 phần H5, Test & sửa F1 phần H1 / H2 / H3, đợt F1 phần H4 + prompt, gộp và CI.
  - 18h33 sửa xong 27 lỗi + 5 nghi vấn đúng là lỗi; nhánh tích hợp `b7e408f` (code `0275e38`, CI 45810078065 xanh, `# fail 0`).
    18h35 nhờ thread kiểm tra độc lập soát lại; 18h36 giao thread Bộ cài thử nội bộ chuẩn bị bản 6.
  - 19h01 soát lại lượt 2: 28 / 28 lỗi KTH sửa đúng, 0 lỗi mở. Nhánh chốt phần 1 `full/nang-cap-h-tong-the` @ `76ecfae` (code `93096dc`,
    CI 45813679683 xanh, lõi 1608, trợ lý 443).
  - 19h05 bộ cài thử bản 6 (`5ab2fab`, CI 45815996979 xanh, 141 MB); link gửi thread "Lưu trữ bộ cài". Phần 1 khoảng 5 giờ 10 phút,
    ~$135. Báo cáo: [`bao-cao-dot-h-phan-1-26-11.md`](bao-cao-dot-h-phan-1-26-11.md). Chờ chủ dự án: duyệt số phần 1, thử bản 6.
  - 19h03 phần 2 bắt đầu: H6 hạ tầng (thread đợt F2) và H7 MEP 3D (thread Test & sửa F1) cùng lúc từ `76ecfae`, làm phần không dính
    số tiêu chuẩn; số tiêu chuẩn chờ chủ dự án duyệt.
  - ~19h20 chủ dự án duyệt số tiêu chuẩn phần 2: "Dùng hết số đề xuất" ([`dot-h-so-tieu-chuan.md`](dot-h-so-tieu-chuan.md) mục "Kết quả duyệt"); đã chuyển xuống H6, H7.
  - ~19h50 chủ dự án chốt thêm: ống thoát D60 dốc mặc định 2,5 %, D110+ 2 % (thread rà H7 phát hiện D60 2 % thấp hơn mức tối thiểu).
  - 20h07 rà H6 xong (5 lỗi); 20h35 rà H7 xong (6 lỗi); 20h36 H8 kết cấu bắt đầu (gộp H6-test + H7-test).
  - ~20h45 chủ dự án chốt H9: chữ vẽ vào bản vẽ giữ tiếng Việt khi chọn English (chỉ đổi giao diện và câu trả lời).
  - 21h40 rà H8 xong (3 lỗi; `886e699`, CI 45840644593 xanh); 21h42 H9 tiếng Anh bắt đầu (thread đợt F1), thread Test & sửa F1 đọc trước để rà.
  - 22h40 H9 tiếng Anh viết xong (`60a2047`, CI 45850823367 xanh sau 1 lượt đỏ vì test CRLF); 22h42 thread Test & sửa F1 bắt đầu rà H9.
  - 23h01 rà H9 xong (3 lỗi, 2 nặng ở xác nhận gõ tay; `7834e43`, CI 45854163617 xanh). 23h05 test tổng thể phần 2 bắt đầu (thread đợt F2); 23h10 mở thread kiểm tra độc lập phần 2 (`session_01D56HmJCdGyyQVo7VxMC1g4`) chạy song song.
  - 23h28 test tổng thể phần 2 xong (`6bcd5b5`, CI 45857510577 xanh); kiểm tra độc lập phần 2 xong: 36 lỗi KTP (3 nặng ở H9 / xác nhận gõ tay), [`kiem-tra-doc-lap-dot-h-phan-2.md`](ban-giao/kiem-tra-doc-lap-dot-h-phan-2.md). 23h30 chia ba thread sửa.
