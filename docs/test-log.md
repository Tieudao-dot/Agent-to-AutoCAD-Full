# Nhật ký smoke test thủ công

Môi trường cần: Windows 10/11 x64, AutoCAD 2022 bản full, bản vẽ trắng (acadiso.dwt) trừ khi ghi khác, Node.js >= 18, đã `claude login` hoặc có `ANTHROPIC_API_KEY`.

Trạng thái: ⬜ chưa chạy · ✅ đạt · ❌ không đạt · ⚠️ đạt một phần. Ghi ngày, phiên bản commit và ghi chú ở cột cuối.

> **Chưa có kịch bản nào được chạy trên AutoCAD thật.** Phiên phát triển chạy trên cloud Linux, không có Windows/AutoCAD. Những gì đã kiểm chứng tự động trên cloud được liệt kê ở cuối file.

## Tiêu chí nghiệm thu (spec §12)

| # | Kịch bản | Kỳ vọng | Trạng thái | Ghi chú |
| --- | --- | --- | --- | --- |
| 1 | "Vẽ lưới trục 5 trục đứng bước 4000, 4 trục ngang bước 3600, kèm ký hiệu trục" | xline/line đúng layer trục, vòng tròn + chữ ký hiệu đầu trục | ⬜ chưa chạy | |
| 2 | "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước" | tường hai nét, cửa có cánh + cung mở, dim đủ bốn phía | ⬜ chưa chạy | |
| 3 | (bản vẽ có sẵn text) "Tìm tất cả text chứa 'WC' và đổi thành 'VỆ SINH'" | đúng và đủ các text | ⬜ chưa chạy | |
| 4 | "Xóa toàn bộ đối tượng trên layer A-TEMP" | Claude báo số lượng, hỏi xác nhận; trả lời "đồng ý" mới xóa | ⬜ chưa chạy | |
| 5 | "Đo diện tích polyline tô màu đỏ lớn nhất" | trả đúng diện tích m² | ⬜ chưa chạy | |
| 6 | "Chèn block GHE vừa định nghĩa thành dãy 3 hàng 8 cột giãn cách 600x900" | array đúng | ⬜ chưa chạy | |
| 7 | Một lượt vẽ 200 đoạn thẳng qua `batch_execute` | phần thực thi < 2 s (xem log `tool batch_execute ok in N ms`) | ⬜ chưa chạy | |
| 8 | Gõ U một lần sau bất kỳ kịch bản nào | hoàn tác toàn bộ lượt đó | ⬜ chưa chạy | |
| 9 | Tắt sidecar (`node.exe`) bằng Task Manager giữa phiên | palette báo mất kết nối, tự khôi phục, chat tiếp được | ⬜ chưa chạy | |
| 10 | Bật skill `ve-kien-truc`, chạy lại kịch bản 2 | tự dùng đúng tên layer và chiều cao chữ trong skill, không cần nhắc | ⬜ chưa chạy | |

## Kiểm tra theo phase

| Phase | Kiểm tra | Trạng thái | Ghi chú |
| --- | --- | --- | --- |
| 1a | NETLOAD dll không lỗi, `CLAUDECHAT` mở palette | ⬜ chưa chạy | |
| 1a | Chat "chào Claude" → text stream dần trong palette | ⬜ chưa chạy | |
| 1a | "kiểm tra kết nối với plugin" → tool `ping` trả phiên bản AutoCAD + tên bản vẽ | ⬜ chưa chạy | |
| 1a | Stop giữa lượt → lượt dừng, nút Gửi bật lại | ⬜ chưa chạy | |
| 1a | New → lịch sử xóa, Claude không nhớ ngữ cảnh cũ | ⬜ chưa chạy | |
| 1a | Đóng AutoCAD → không còn `node.exe` của ClaudeCAD | ⬜ chưa chạy | |

## Đã kiểm chứng tự động trên cloud (Linux)

| Ngày | Hạng mục | Kết quả |
| --- | --- | --- |
| 2026-11-05 | `npm run build` sidecar | ✅ |
| 2026-11-05 | `npm test` — 32 test: protocol (encode/decode, UTF-8 cắt giữa chunk, CRLF, BOM, dòng quá dài), ToolRpc (tương quan id, timeout, response muộn, mất kết nối, failAll), tích hợp qua socket thật (echo tool, lỗi plugin, timeout, đứt pipe, dòng hỏng, tiến trình sidecar thật nối pipe rồi tự thoát), AgentSession với query giả (stream text, nhiều lượt, cancel, new session, process chết, lỗi đăng nhập, skill plugin) | ✅ 32/32 |
| 2026-11-05 | `dotnet build` plugin net48 với reference AutoCAD.NET 24.1.51000 | ✅ 0 warning |
| 2026-11-05 | `dotnet test plugin/ClaudeCAD.Core.Tests` — JsonCodec, Params | ✅ 16/16 |
