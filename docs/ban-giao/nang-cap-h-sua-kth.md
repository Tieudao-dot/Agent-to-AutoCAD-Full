# Bàn giao: sửa lỗi kiểm tra độc lập đợt H phần 1 (KTH)

## Phần thread đợt F2 (người viết H4, H5) — nhánh `full/nang-cap-h-sua-kth-c`

Thread `session_01D56HmFcYrzsme7GoBm8tNC`. Tách từ `full/nang-cap-h-tong-the` @ `aa5753c`. Chưa chạy CI (thread giữ nhánh tích hợp gộp
rồi chạy). Chưa chạy trên AutoCAD thật.

| Mã | Mức | Lỗi | Đã sửa | Test |
| --- | --- | --- | --- | --- |
| KTH-01 | Nặng | `batch_files`: khách bấm Đồng ý vẫn không chạy file nào. `runBatch` chỉ chạy khi tham số có cờ đã duyệt, nhưng Claude gửi `confirm_handles`; cờ chỉ được thêm ở cầu nối (`session.paramsToSend`) sau bước kiểm đó, nên mọi lần gọi chỉ trả lại kế hoạch | `cardGiven`: có `confirm_handles` (hoặc cờ đã duyệt) thì chạy. Cầu nối đổi `confirm_handles` theo thẻ của lượt cho từng lần gọi plugin; nếu thẻ chưa được duyệt, plugin từ chối ngay file đầu và lời từ chối đó là câu trả lời (không file nào bị đụng) | `agent/test/nangcaph5.test.ts` "real path": Claude gửi `confirm_handles` → `applyUserConfirmation` với thẻ đã duyệt → plan, run × 2, finish; thẻ chưa duyệt → bị từ chối ở file đầu; không có `confirm_handles` → chỉ kế hoạch |
| KTH-02 | Nặng | Mỗi file chỉ có 30 giây (`batch_files` không trong nhóm lệnh chậm): quá hạn thì file báo lỗi dù có thể đã ghi xong, và các file sau quá hạn dây chuyền | `timeoutFor`: `batch_files` action run = 2 phút (soát / đếm block), 5 phút (áp chuẩn / in PDF, trần chung); plan / finish giữ mặc định | test `timeoutFor` |

Con số mới (khách thấy gián tiếp: file quá lâu báo lỗi hết giờ): 2 phút / 5 phút, ghi vào bảng "Con số chờ chủ dự án duyệt" của
`docs/ban-giao/nang-cap-h5.md`.

Test sau lần sửa cuối: lõi **1581 / 1581**, trợ lý **440 / 440** (thêm 4), build `net48`, `net8.0-windows`, `net10.0-windows` 0 lỗi (1
cảnh báo CA2022 có sẵn ở `TraceTools.cs`).
