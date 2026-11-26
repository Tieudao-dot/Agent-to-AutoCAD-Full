# Thử tay đợt H9 — giao diện tiếng Anh

*Đã gộp vào khối "Đợt H phần 2" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt H phần 2), thử tay theo file đó.*

Nhánh `full/nang-cap-h9`. Chưa thử trên AutoCAD thật. Bàn giao: [`docs/ban-giao/nang-cap-h9.md`](ban-giao/nang-cap-h9.md).

## A. Bộ cài

- [ ] A1. Chạy bộ cài: hộp đầu tiên hỏi **Tiếng Việt / English**, Tiếng Việt chọn sẵn.
- [ ] A2. Chọn English: trang kích hoạt, các hộp "AutoCAD đang mở", "file đang được dùng", "máy đang có ClaudeCAD bản gốc" và
  trang cuối ("Sign in to Claude now") là tiếng Anh.
- [ ] A3. Chọn Tiếng Việt: các trang trên là tiếng Việt như bản trước (các chữ chuẩn của Inno như nút Next / Back vẫn tiếng
  Anh như trước đây).
- [ ] A4. Máy chưa từng dùng plugin, cài bằng English: mở AutoCAD, khung chat là tiếng Anh ngay.
- [ ] A5. Máy đã dùng plugin (có cài đặt cũ), cài đè bằng English: khung chat giữ ngôn ngữ đang đặt (tiếng Việt), đổi trong ⚙.
- [ ] A6. Cài ngầm `/VERYSILENT /LANG=english`: không hỏi gì, lần đầu mở khung chat là tiếng Anh.

## B. Đổi ngôn ngữ trong ⚙

- [ ] B1. ⚙ ▸ **Ngôn ngữ / Language** ▸ English: nút trên cùng (gợi ý khi rê chuột), hàng Tỷ lệ / Skill / Layer…, nút Gửi,
  ⚙ (tên các nhóm, các ô), nút chế độ Cả hai / 2D / 3D (máy Full) đổi sang tiếng Anh **ngay**, không cần đóng mở khung chat.
- [ ] B2. Mở các bảng Skill, Layer, Macro, Phiên bản, Ghi chú dự án: chữ tiếng Anh.
- [ ] B3. Chữ đang gõ dở trong ô nhập và chữ trong ghi chú dự án không bị đổi.
- [ ] B4. Đổi về Tiếng Việt: mọi chữ trên về lại tiếng Việt.
- [ ] B5. Đóng mở AutoCAD: ngôn ngữ đã chọn được giữ.
- [ ] B6. (Test & sửa H9) Máy đang dùng bản trước H9 (chưa từng có ô Ngôn ngữ), cài bản H9 và chọn English ở bộ cài:
  khung chat mở bằng English. Sau đó chọn Tiếng Việt trong ⚙, cài lại và chọn English: khung chat **vẫn** Tiếng Việt.

## C. Claude trả lời

- [ ] C1. Chọn English rồi gõ "draw a 5x4 axis grid with 4000 spacing": Claude trả lời tiếng Anh, không xưng "tớ / cậu";
  bước tiến độ, nút gợi ý, thẻ kế hoạch tiếng Anh.
- [ ] C2. Đang chat dở bằng tiếng Việt, đổi sang English, gửi tin tiếp: Claude trả lời tiếng Anh và vẫn nhớ cuộc chat.
- [ ] C3. Xóa > 20 đối tượng: thẻ xác nhận tiếng Anh (Yes / No); gõ "yes" là làm, gõ "no" là không làm.
- [ ] C3b. (Test & sửa H9) Cũng việc xóa đó, gõ câu có điều kiện: "yes but don’t delete the walls" (dấu nháy cong của
  điện thoại / Mac), "đồng ý nhưng giữ tường", "ok xóa hết trừ cửa": Claude **không** xóa, hỏi lại.
- [ ] C4. "Vẽ bảng thống kê block", "tạo tờ A3 có khung tên", "ra bộ hồ sơ" khi đang English: **chữ trên bản vẽ vẫn tiếng
  Việt** (tiêu đề bảng, khung tên, tên tờ); câu trả lời tiếng Anh.
- [ ] C5. Xuất Excel khi đang English: tiêu đề cột trong file vẫn tiếng Việt.
- [ ] C6. Máy gói 2D, English, hỏi việc 3D: Claude báo bằng tiếng Anh máy dùng gói 2D, liên hệ chủ plugin.

## D. Thông báo và lỗi

- [ ] D1. English, rút mạng / dừng trợ lý (CLAUDECHATRESTART): dòng trạng thái "Starting the assistant...", "Connected".
- [ ] D2. English, chưa đăng nhập: thẻ đăng nhập và lời báo tiếng Anh, nhóm Tài khoản AI trong ⚙ tự mở.
- [ ] D3. English, đóng khung chat rồi để Claude làm xong: bong bóng ở thanh trạng thái tiếng Anh.
- [ ] D4. English, `CLAUDECHATSELFTEST`: tên bước và dòng tổng tiếng Anh (chi tiết kỹ thuật từng bước có thể vẫn tiếng Việt).
- [ ] D5. English, ⚙ ▸ Xuất cuộc chat: file Markdown có "User", "Error"…
- [ ] D6. Máy chưa kích hoạt, English: thẻ kích hoạt, mã máy, nút "Send activation request" tiếng Anh.

## E. Không đổi gì khi không chọn English

- [ ] E1. Máy đang dùng (không đụng ⚙): mọi chữ, câu trả lời, thẻ như bản trước.
