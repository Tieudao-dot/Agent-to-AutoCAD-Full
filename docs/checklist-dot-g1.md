# Thử tay đợt G1: ra bộ hồ sơ một lệnh

*Đã gộp vào khối "Đợt G" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt G), thử tay theo file đó.*

*Nhánh `full/nang-cap-g1`. Chi tiết: `docs/ban-giao/nang-cap-g1.md`. Máy gói Full (bộ cài thử nội bộ), bản vẽ đã lưu (có
tên file), đơn vị mm. Khoảng 35 phút. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

Chuẩn bị: một mặt bằng nhà 2 tầng đã dựng 3D (`build_3d_from_plan` với `storeys 2`, có cửa đi / cửa sổ), hoặc bản vẽ mẫu
nhà đã dựng ở các đợt trước.

## A. Tự kiểm (~3 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt G1: ra bộ hồ sơ (mặt đứng + phối cảnh), chạy lại cần thẻ" **đạt** |

## B. Bộ hồ sơ đủ (~15 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Ra bộ hồ sơ cho nhà này, công trình Nhà phố anh Nam, người vẽ Hùng" | Một thẻ lệnh "Ra bộ hồ sơ"; Claude báo 9 tờ KT-01 → KT-09: mặt bằng tầng 1, tầng 2, 4 mặt đứng, mặt cắt A-A, B-B, phối cảnh |
| B2 | Mở từng tab KT-01 … KT-09 | Mỗi tờ có khung, khung tên điền đúng tên bản vẽ, số bản vẽ (KT-01…), tỉ lệ 1:100 (phối cảnh "—"), khổ A3, ngày hôm nay, công trình và người vẽ như đã nói |
| B3 | Xem mặt bằng tầng 1 và tầng 2 | Hình cắt ngang tường (có hatch) ở đúng tầng, cửa thành khoảng hở; hình nằm giữa khung nhìn, không lẫn hình khác |
| B4 | Xem 4 mặt đứng và 2 mặt cắt | Nét đúng hướng (mặt đứng trước nhìn từ phía dưới mặt bằng lên); mặt cắt có hatch phần bị cắt và nét nhìn thấy phía sau |
| B5 | Xem phối cảnh | Nhà nhìn chéo từ góc tây nam, tô bóng (Realistic khi in), không thấy nét 2D hay hình mặt cắt nào trong khung |
| B6 | Về Model | Các hình mặt bằng / mặt đứng / mặt cắt nằm thành một hàng bên phải mô hình, không đè lên gì |
| B7 | Ctrl+Z một lần (hoặc nút hoàn tác lượt) | Mất hết các tờ và các hình vừa tạo |

## C. Chạy lại, PDF, lựa chọn (~12 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | Sau B1, nói lại "ra bộ hồ sơ" | Hiện **thẻ Đồng ý / Hủy** nói sẽ thay bộ hồ sơ cũ. Hủy: không đổi gì. Đồng ý: tờ và hình cũ được thay, không nhân đôi (vẫn KT-01 → KT-09, chỉ một hàng hình trong Model) |
| C2 | Tạo tay một layout tên `KT-10`, rồi "ra bộ hồ sơ thêm 2 mặt cắt nữa" (11 tờ) | Claude báo layout KT-10 là của cậu, không đụng tới, đề nghị tiền tố khác (vd KT2) |
| C3 | "Ra bộ hồ sơ và in ra PDF" | File `<tên bản vẽ>-bo-ho-so.pdf` cạnh bản vẽ, đủ các trang theo thứ tự KT-01… |
| C4 | Làm lại C3 | Thẻ hỏi thay bộ cũ **và** ghi đè file PDF (một thẻ). Đồng ý: file mới thay file cũ |
| C5 | "Ra bộ hồ sơ chỉ mặt đứng trước và sau, khổ A1, tỉ lệ 1:50" | 2 tờ A1, tỉ lệ 1:50 |
| C6 | "Ra bộ hồ sơ, cắt dọc qua cầu thang" (chỉ vị trí cắt) | Mặt cắt theo đường Claude đặt qua cầu thang |
| C7 | Xoay UCS 30° (`UCS` → `Z` → 30), ra bộ hồ sơ | Mặt bằng xoay theo UCS (trục x của UCS nằm ngang trên tờ); mặt đứng trước nhìn theo trục y của UCS |
| C8 | Bản vẽ đơn vị m (INSUNITS = 6) có nhà dựng 3D, ra bộ hồ sơ | Tỉ lệ vẫn 1:100, hình vừa khung |
| C9 | *(Test & sửa G1)* "Ra bộ hồ sơ khổ A4, tỉ lệ 1:20" (nhà ~10 m) | Bộ vẫn ra, Claude nói các hình lớn hơn tờ ở tỉ lệ đó nên tờ chỉ thấy phần giữa, gợi ý tỉ lệ / khổ lớn hơn |

## D. Gói, chế độ, trường hợp thiếu (~8 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | Bật **Chỉ xem**, "ra bộ hồ sơ" | Claude nói Chỉ xem đang bật, không tạo gì |
| D2 | Chế độ **2D** (nút Cả hai / 2D / 3D), "ra bộ hồ sơ" | Chỉ tờ mặt bằng (hình 2D có sẵn); Claude nói mặt đứng / mặt cắt / phối cảnh cần chế độ Cả hai hoặc 3D |
| D3 | Chế độ **3D**, "ra bộ hồ sơ" | Các hình mặt bằng / mặt đứng / mặt cắt vẽ trong Model, không có tờ in; Claude nói xếp lên tờ có khung tên và in PDF cần chế độ Cả hai hoặc 2D |
| D4 | Bản vẽ chỉ có mặt bằng 2D (chưa dựng 3D), "ra bộ hồ sơ" | Một tờ mặt bằng; Claude nói chưa có khối 3D nên không có mặt đứng / mặt cắt / phối cảnh, gợi ý dựng 3D |
| D5 | Ghi macro một lượt "ra bộ hồ sơ" trên bản vẽ A, mở bản vẽ B (nhà khác, chưa có bộ hồ sơ), chạy macro | Bộ hồ sơ ra trên B. Chạy macro lần nữa trên B: báo đã có bộ hồ sơ, macro không thay (chạy trong chat để có thẻ) |
| D6 | *(máy khác)* Máy gói **2D** / **3D**: như D2 / D3 | Giống D2 / D3 nhưng câu báo nói về gói (không nói đổi chế độ) |
