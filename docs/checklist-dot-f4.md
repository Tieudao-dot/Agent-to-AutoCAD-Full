# Checklist thử tay đợt F4 — mở chat nhanh hơn, xuất bảng ra Excel

> **Đã gộp vào [`docs/checklist-tong-the.md`](checklist-tong-the.md), khối "Đợt F" (25/11/2026).** Thử theo checklist tổng; file này giữ để tra mã mục cũ (F4-…).

*Nhánh `full/nang-cap-f4`. Chi tiết: `docs/ban-giao/nang-cap-f4.md`. Cần bộ cài build từ nhánh này, máy **gói Full** (bộ
cài thử nội bộ là Full) và Excel (hoặc LibreOffice / Google Sheets) để mở file. ~25 phút.*

## 0. Tự kiểm nhanh (~3 phút)

1. Mở một bản vẽ trống, gõ `CLAUDECHATSELFTEST`. Đạt: dòng **"Nâng cấp đợt F4: thống kê block xuất file Excel (.xlsx)
   đọc lại được"** có dấu ✓ (bước này tự tạo block riêng, không cần bước F2).

## A. Mở chat nhanh hơn (~10 phút)

1. Đóng hẳn AutoCAD. Mở lại, mở một bản vẽ, mở khung chat, **đợi khoảng 10 giây** rồi gửi "chào". Đạt: chữ trả lời bắt
   đầu hiện ra **nhanh hơn rõ** so với bản trước (bản trước phải chờ trợ lý khởi động lúc gửi tin đầu). Ghi lại số giây
   nếu tiện (tính từ lúc bấm Gửi đến lúc chữ đầu tiên hiện).
2. AutoCAD mở **không chậm hơn** thấy rõ so với bản trước (đếm từ lúc bấm biểu tượng đến lúc vẽ được).
3. Mở chat xong, **chưa gửi gì**: vào claude.ai → Settings → Usage (hoặc xem dòng hạn mức trong khung chat). Đạt: mức dùng
   **không tăng** chỉ vì mở khung chat (trợ lý chỉ khởi động, chưa gửi gì cho Claude).
4. Nhờ việc dùng lệnh ít gặp, vd "đổi tên trạng thái layer", "dựng cầu thang chữ U cao 3300", "chèn bồn cầu",
   "thiết lập style chuẩn 1:100". Đạt: Claude vẫn tìm ra đúng lệnh và làm được (có thể thấy thêm một bước tìm lệnh trong
   danh sách thao tác). Không có câu kiểu "tớ không có lệnh này".
5. Mở Task Manager, tắt tiến trình `claude.exe` của plugin trong lúc khung chat đang rảnh. Đợi ~10 giây rồi gửi một yêu
   cầu. Đạt: vẫn trả lời bình thường, **không** hiện thẻ lỗi trong khung chat.
6. Đổi nút chế độ sang **2D**, đợi vài giây, gửi "vẽ hình chữ nhật 4000 × 3000". Đạt: làm được; đổi sang **3D** rồi nhờ
   "ghi chữ ABC": Claude nhắc đổi chế độ (như F1), không mời mua gói.
7. Máy **chưa kích hoạt** (hoặc bị khóa): mở khung chat. Đạt: không có tiến trình `claude.exe` nào chạy (chưa kích hoạt thì
   trợ lý không khởi động sẵn).

## B. Xuất bảng ra Excel (~12 phút)

Chuẩn bị: bản vẽ đã lưu (có tên file), có vài phòng (polyline kín + chữ tên phòng tiếng Việt có dấu, vd "PHÒNG NGỦ 1"),
vài block (vd chèn 3 cửa đi thư viện), và vài khối 3D trên 2 layer (máy 3D / Full).

1. "lập bảng diện tích các phòng và xuất ra Excel". Đạt: Claude báo đường dẫn file, file nằm **cạnh bản vẽ**, tên
   `<tên bản vẽ>-dien-tich.xlsx`. Mở bằng Excel: chữ tiếng Việt **đúng dấu**, hàng tiêu đề in đậm, cột diện tích là
   **số** (chọn cả cột thấy Excel tự cộng Sum ở thanh dưới; gõ `=SUM(...)` ra đúng tổng).
2. "thống kê block ra Excel". Đạt: file `<tên bản vẽ>-thong-ke-block.xlsx`, cột số lượng là số.
3. Làm lại bước 1. Đạt: Claude **hỏi** trước khi ghi đè file đã có; đồng ý thì file mới thay file cũ; không đồng ý thì
   file cũ giữ nguyên.
4. "xuất bảng diện tích ra file D:\\Bao cao\\phong.xlsx" (thư mục có thật). Đạt: file đúng chỗ đó. Thư mục không có thật:
   Claude báo thư mục không tồn tại, không tạo gì.
5. Đang **mở** file Excel đó trong Excel, nhờ xuất lại và đồng ý ghi đè. Đạt: Claude báo không ghi được (file đang mở),
   nhờ cậu đóng Excel rồi thử lại; không treo AutoCAD.
6. Máy **gói 3D** (hoặc Full chế độ 3D): "bảng khối lượng các khối theo layer, xuất Excel" và "kiểm tra va chạm giữa dầm
   và ống, xuất Excel". Đạt: có file `-khoi-luong.xlsx` / `-va-cham.xlsx` (không cần bảng trong bản vẽ); số khối lượng m³
   là số. Không có va chạm: Claude nói không có va chạm, không tạo file.
7. Máy **gói 2D**: "xuất bảng khối lượng 3D ra Excel". Đạt: Claude nói máy không có phần 3D (không tạo file).
8. Bản vẽ **chưa lưu** (Drawing1): xuất Excel. Đạt: file vào thư mục Documents, tên `Drawing1-...xlsx`.

## C. Sau Test & sửa đợt F4 (~8 phút)

*Nhánh `full/nang-cap-f4-test`. Chi tiết: `docs/ban-giao/nang-cap-f4-test.md`.*

1. Lưu macro cho lượt "lập bảng thống kê block (có bảng trên bản vẽ) và xuất Excel", rồi chạy macro đó. Đạt: bảng mới
   được vẽ lại trên bản vẽ; **không** có lỗi "file đã có"; file Excel cũ **không** bị ghi đè (giờ sửa đổi cuối của file
   giữ nguyên).
2. Đặt tên một block có ký tự lạ (vd `CỬA & SỔ <1>`), "thống kê block ra Excel". Đạt: Excel mở file không báo hỏng, tên
   hiện đúng.
3. **Công tắc tắt nạp gọn:** đóng AutoCAD, tạo file trống `tool-search-off` trong `%LocalAppData%\ClaudeCAD` (gõ
   `%LocalAppData%\ClaudeCAD` vào thanh địa chỉ của File Explorer, chuột phải → New → Text Document, đổi tên thành
   `tool-search-off`, bỏ đuôi `.txt`). Mở AutoCAD, nhờ một việc dùng lệnh ít gặp (vd "kéo giãn hình chữ nhật"). Đạt:
   Claude làm được như trước F4; trong `%LocalAppData%\ClaudeCAD\logs` (file log của trợ lý) có dòng
   `tools: all in full (deferring switched off)`. Xóa file `tool-search-off`, mở lại AutoCAD: dòng log thành
   `tools: most used in full, the rest behind ToolSearch`.
