# Tổng quan dự án Agent to AutoCAD Full

*Cập nhật: 12/11/2026 (giờ Việt Nam). Luồng chính giữ file này. Bản tổng quan chép từ dự án gốc ClaudeCAD nằm trong
lịch sử git của file này (bản ngay trước commit `ed0753a`) và ở nhánh `luu-tru/2d-base`.*

Bản Full = plugin ClaudeCAD (2D) + **gói dịch vụ theo máy (2D / 3D / Full)** + **vẽ 3D**. Độc lập với dự án gốc;
quy tắc làm việc và danh bạ thread: [`../CLAUDE.md`](../CLAUDE.md).

## 1. Tình hình

- **Nhánh tích hợp mới nhất:** `full/test-tong-the`, commit `9b23471`. Gồm gói dịch vụ, 3D đợt 1, 2, 3 (đã test & sửa
  từng đợt) và lượt test tổng thể.
  - Test lõi 322/322, trợ lý 199/199, build net48 / net8 / net10 sạch, CI Windows run 43236499463 xanh.
  - **Chưa thử trên AutoCAD thật. Chưa phát hành.** `main` vẫn là v0.9.3 của bản gốc.
- **Chưa dùng được bộ cài:** `activation/activation.json` và khóa công khai trong `License.cs` vẫn là của dự án gốc.
  Bộ cài CI build ra chỉ để kiểm build, không cài thử, không phát cho ai.
- **Checklist thử tay:** [`checklist-tong-the.md`](checklist-tong-the.md) (rút gọn ~50 phút, đủ ~90 phút; chi tiết ở
  [`checklist-thu-that.md`](checklist-thu-that.md) phần I, J, K, L, X). Bản dùng thử AutoCAD hết khoảng 10/12/2026.

## 2. Việc chờ chủ dự án

1. **Dựng máy chủ kích hoạt riêng** theo [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md), gửi luồng
   chính URL web app + 2 dòng khóa công khai (không gửi khóa riêng / SECRET).
2. Sau bước 1: luồng chính giao gắn URL / khóa, build **bộ cài thử**; chủ dự án thử theo checklist tổng, gửi lỗi.
3. Trước khi phát hành: số phiên bản (plugin / trợ lý vẫn ghi 0.9.4), secret CI của repo Full, đồng ý merge `main`,
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
| 12/11 | Cách làm việc | Luồng chính điều phối; mỗi đợt một thread, thread đợt tự mở thread con "Test & sửa" |

## 4. Đã làm

| Phần | Nội dung | Nhánh | Bàn giao |
| --- | --- | --- | --- |
| Gói dịch vụ | Cột Gói + dòng ký số `CCF1` ở máy chủ; plugin đọc gói, chặn lệnh ngoài gói (lệnh lẻ / batch / macro); trợ lý chỉ nạp lệnh của gói; giao diện "Gói: …" | `full/goi-dich-vu` | [`ban-giao/goi-dich-vu-plugin.md`](ban-giao/goi-dich-vu-plugin.md) |
| 3D đợt 1 | 14 lệnh: khối cơ bản, extrude / revolve, cộng trừ khối, góc nhìn, kiểu hiển thị, thông tin khối; chụp ảnh 3D; tự kiểm tra | `full/ve-3d` → `full/ve-3d-test-1` (7 lỗi sửa) | [`ve-3d-dot-1.md`](ban-giao/ve-3d-dot-1.md), [`ve-3d-test-1.md`](ban-giao/ve-3d-test-1.md) |
| 3D đợt 2 | 12 lệnh: sweep, loft, cắt / bo / vát / làm rỗng, xoay / đối xứng / căn 3D, UCS, va chạm; danh sách cạnh / mặt | `full/ve-3d-dot-2` → `full/ve-3d-test-2` (6 lỗi) | [`ve-3d-dot-2.md`](ban-giao/ve-3d-dot-2.md), [`ve-3d-test-2.md`](ban-giao/ve-3d-test-2.md) |
| 3D đợt 3 | 3 lệnh: dựng 3D từ mặt bằng, mặt cắt, hình chiếu (API .NET, không gọi FLATSHOT); skill `ve-3d-kien-truc` | `full/ve-3d-dot-3` → `full/ve-3d-test-3` (13 lỗi) | [`ve-3d-dot-3.md`](ban-giao/ve-3d-dot-3.md), [`ve-3d-test-3.md`](ban-giao/ve-3d-test-3.md) |
| Test tổng thể | Rà chéo gói × 3 đợt (1 lỗi), 11 test rà chéo, checklist tổng; lượt sửa 3 điểm đã chốt | `full/test-tong-the` | [`ban-giao/test-tong-the.md`](ban-giao/test-tong-the.md) |

Mỗi nhánh tách từ commit cuối của nhánh trước, nên `full/test-tong-the` chứa toàn bộ.

## 5. Chi phí token mỗi lượt đầu phiên (ước, ~3,5 ký tự / token)

| Gói | Mô tả lệnh + system prompt |
| --- | --- |
| 2D | ~36k |
| 3D | ~37k |
| Full | ~51k |

Riêng phần 3D ~15k (kế hoạch ước 5–6k). Phần này nằm trong prefix được cache: các lượt sau trong 5 phút chỉ tốn ~10%.
Cách giảm thêm còn lại: gộp 6 lệnh khối cơ bản thành 1 lệnh (~−2k, đổi tên lệnh); chưa làm.

## 6. Nhật ký

- **12/11/2026:**
  - tách repo từ dự án gốc;
  - làm gói dịch vụ;
  - chuỗi 6 bước 3D (đợt 1 → test → đợt 2 → test → đợt 3 → test → test tổng thể), mỗi bước 1 lượt CI xanh;
  - sửa 3 điểm chốt sau test tổng thể.
