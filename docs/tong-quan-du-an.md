# Tổng quan dự án Agent to AutoCAD Full

*Cập nhật: 13/11/2026 (giờ Việt Nam). Luồng chính giữ file này. Bản tổng quan chép từ dự án gốc ClaudeCAD nằm trong
lịch sử git của file này (bản ngay trước commit `ed0753a`) và ở nhánh `luu-tru/2d-base`.*

Bản Full = plugin ClaudeCAD (2D) + **gói dịch vụ theo máy (2D / 3D / Full)** + **vẽ 3D**. Độc lập với dự án gốc;
quy tắc làm việc và danh bạ thread: [`../CLAUDE.md`](../CLAUDE.md).

## 1. Tình hình

*Cập nhật 14/11/2026 ~0h40 (giờ VN).*

- **Nhánh tích hợp mới nhất:** `full/nang-cap-tong-the` @ `f7f32ad` (CI 43478782014 xanh). Chứa toàn bộ: gói dịch vụ,
  3D đợt 1–3, test tổng thể + 4 lượt kiểm tra độc lập lần 1 (43 lỗi), bộ cài thay bản gốc + cảnh báo, và **chuỗi nâng
  cấp A → E** (13/11) + test tổng thể nâng cấp.
  - Test lõi 909, trợ lý 298, build net48 / net8 / net10 sạch.
  - Nhánh tích hợp cũ `full/test-tong-the` @ `19944df` vẫn giữ (nơi sửa lỗi thử thật phần 3D cũ, hiện chưa có lỗi nào).
  - **Chưa thử trên AutoCAD thật. Chưa phát hành.** `main` vẫn là v0.9.3 của bản gốc.
- **Chuỗi nâng cấp lệnh vẽ (13/11):** [`ke-hoach-nang-cap-lenh.md`](ke-hoach-nang-cap-lenh.md) (bảng tiến độ). Cả 5 đợt
  xong (~$96, 22 lỗi sửa trong các bước Test & sửa), test tổng thể nâng cấp xong (3 lỗi chéo). **Kiểm tra độc lập
  nâng cấp** (agent mới, 3 lượt): 32 lỗi, 30 sửa đúng, 2 chấp nhận, **0 mở**; báo cáo
  `docs/ban-giao/kiem-tra-doc-lap-nang-cap.md` trên `full/kiem-tra-doc-lap-2`. **Báo cáo cho chủ dự án:**
  [`bao-cao-nang-cap-14-11.md`](bao-cao-nang-cap-14-11.md).
- **Token mỗi lần mở chat** (mô tả lệnh + system prompt): 2D 34,4k → 38,7k, 3D 35,7k → 39,7k, Full 49,7k → 57,5k
  (trước / sau chuỗi nâng cấp; phần này được cache, lượt sau trong 5 phút ~10%).
- **Chưa dùng được bộ cài thật:** `activation/activation.json` và khóa công khai trong `License.cs` vẫn là của dự án
  gốc; chờ chủ dự án dựng máy chủ kích hoạt riêng.
- **Bộ cài thử nội bộ** (chỉ chủ dự án dùng, không gửi ai; bỏ qua kích hoạt, gói Full, hết hạn 10/12/2026, nhãn "BẢN THỬ
  NỘI BỘ", không tự cập nhật; nhánh `full/ban-thu-noi-bo`, **không bao giờ gộp**). Bản mới 14/11 có đủ chuỗi nâng cấp:
  `8f84973`, CI 43481896536, artifact `AgentToAutoCADFull-ThuNoiBo-nang-cap-0.9.4`
  ([tải](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43481896536/artifacts/14065019481), tới 11/02/2027). Bản cũ 13/11 (CI 43373318854) chưa có lệnh nâng cấp.
  Bàn giao: `docs/ban-giao/ban-thu-noi-bo.md` trên nhánh đó.
- **Checklist thử tay:** [`checklist-tong-the.md`](checklist-tong-the.md) trên nhánh tích hợp: phần cũ I–M, X (rút gọn
  ~97 phút, đủ ~157 phút) + khối **chuỗi nâng cấp A → E** (rút gọn ~95 phút, đủ ~3 giờ; mục N–R, chéo V1–V8). Chi tiết
  ở [`checklist-thu-that.md`](checklist-thu-that.md). Bản dùng thử AutoCAD hết khoảng 10/12/2026.
- **Báo cáo sáng 13/11:** [`bao-cao-sang-13-11.md`](bao-cao-sang-13-11.md). Chủ dự án đã đồng ý cả 9 quyết định
  luồng chính đưa ra trong đêm (13/11).

## 2. Việc chờ chủ dự án

1. **Dựng máy chủ kích hoạt riêng** theo [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md), gửi luồng
   chính URL web app + 2 dòng khóa công khai (không gửi khóa riêng / SECRET).
2. Sau bước 1: luồng chính giao gắn URL / khóa, build **bộ cài thử**; chủ dự án thử theo checklist tổng, gửi lỗi.
3. **Thử phần 3D** bằng bộ cài thử nội bộ (`checklist-tong-the.md` phần 0, 2, 3, 4, 5, ~54 phút); lỗi gửi luồng chính.
   Sau chuỗi nâng cấp: thử khối "chuỗi nâng cấp" của checklist tổng (cần bộ cài thử build lại).
4. Xem 2 quyết định luồng chính chọn theo đề xuất của kiểm tra độc lập (bản vẽ mét tự quy đổi; mái nhà chữ L / T báo
   trước chỗ chồng) và 3 số đợt E tự chọn (bản thang 150, khe 2 vế chữ U 100, bản mái dốc 150 mm).
5. Trước khi phát hành: số phiên bản (plugin / trợ lý vẫn ghi 0.9.4), secret CI của repo Full, đồng ý merge `main`,
   các chỗ còn trỏ về dự án gốc ([`cho-tro-ve-repo-goc.md`](cho-tro-ve-repo-goc.md)).

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

Mỗi nhánh tách từ commit cuối của nhánh trước, nên `full/nang-cap-tong-the` chứa toàn bộ.

## 5. Chi phí token mỗi lượt đầu phiên (mô tả lệnh + system prompt)

| Gói | Trước chuỗi nâng cấp (`19944df`) | Sau (`f1e7123`) |
| --- | --- | --- |
| 2D | 34,4k | 38,7k |
| 3D | 35,7k | 39,7k |
| Full | 49,7k | 57,5k |

Đo cùng một cách ở thread "Test tổng thể nâng cấp" (bảng cũ ~36k / ~37k / ~51k đếm hơi khác). Gộp 6 lệnh dựng khối ở
đợt A chỉ bớt ~1,1k (ước cũ ~2k), phần lệnh mới dùng lại khoản đó. Phần này nằm trong prefix được cache: các lượt sau
trong 5 phút chỉ tốn ~10%.

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
