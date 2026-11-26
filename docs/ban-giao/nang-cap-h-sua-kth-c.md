# Bàn giao: sửa lỗi kiểm tra độc lập đợt H phần 1 (KTH), phần H5 — nhánh `full/nang-cap-h-sua-kth-c`

Thread `session_01D56HmFcYrzsme7GoBm8tNC`. Tách từ `full/nang-cap-h-tong-the` @ `aa5753c`. Chưa chạy CI (thread giữ nhánh tích hợp gộp
rồi chạy). Chưa chạy trên AutoCAD thật.

| Mã | Mức | Lỗi | Đã sửa | Test |
| --- | --- | --- | --- | --- |
| KTH-01 | Nặng | `batch_files`: khách bấm Đồng ý vẫn không chạy file nào. `runBatch` chỉ chạy khi tham số có cờ đã duyệt, nhưng Claude gửi `confirm_handles`; cờ chỉ được thêm ở cầu nối (`session.paramsToSend`) sau bước kiểm đó, nên mọi lần gọi chỉ trả lại kế hoạch | `cardGiven`: có `confirm_handles` (hoặc cờ đã duyệt) thì chạy. Cầu nối đổi `confirm_handles` theo thẻ của lượt cho từng lần gọi plugin; nếu thẻ chưa được duyệt, plugin từ chối ngay file đầu và lời từ chối đó là câu trả lời (không file nào bị đụng) | `agent/test/nangcaph5.test.ts` "real path": Claude gửi `confirm_handles` → `applyUserConfirmation` với thẻ đã duyệt → plan, run × 2, finish; thẻ chưa duyệt → bị từ chối ở file đầu; không có `confirm_handles` → chỉ kế hoạch |
| KTH-02 | Nặng | Mỗi file chỉ có 30 giây (`batch_files` không trong nhóm lệnh chậm): quá hạn thì file báo lỗi dù có thể đã ghi xong, và các file sau quá hạn dây chuyền | `timeoutFor`: `batch_files` action run = 2 phút (soát / đếm block), 5 phút (áp chuẩn / in PDF, trần chung); plan / finish giữ mặc định | test `timeoutFor` |
| KTH-03 | Vừa | Hai file trùng tên nằm ngoài `folder` (gọi kèm `files`) dùng chung một bản sao lưu: file thứ hai bị ghi mà không được sao lưu | `BackupPath`: file trong `folder` sao lưu vào `<folder>\_sao-luu-claudecad\<giờ>\<đường dẫn dưới folder>`; file ngoài `folder` (hoặc không có `folder`) sao lưu **cạnh chính nó** (`BackupBase`). Thư mục chỉ "giống đầu tên" (`goc-2` với `goc`) không bị coi là bên trong | `SuaKthCTests.KTH03_…` |
| KTH-04 | Vừa | Đếm block dừng ở 20 000 đối tượng mà không báo | Đếm mọi đối tượng của model space, từng cái một (không giữ danh sách, không giới hạn) | `SuaKthCTests.KTH04_05_07_…` |
| KTH-05 | Vừa | `groups` lạ / `scale` sai chỉ lộ ra sau thẻ (mỗi file báo lỗi, áp chuẩn còn để lại bản sao lưu thừa) | Kiểm `groups` (`DrawingCheckLogic.Find`) và `scale` (`ScaleDenominator`) ngay khi lập kế hoạch, trước thẻ | như trên |
| KTH-06 | Nhẹ | `backup_folder` chỉ ghi thư mục của file đầu khi các file ở nhiều thư mục | `BackupFolders`: một thư mục thì ghi nó; nhiều thì ghi "cạnh từng file" và liệt kê (`backup_folders`, tối đa 30). Excel tổng hợp vẫn vào `folder` hoặc thư mục của file đầu, đường dẫn có trong kết quả | `SuaKthCTests.KTH06_…` |
| KTH-07 | Nhẹ | `folder` không có thật vẫn bị tạo (áp chuẩn tạo `_sao-luu-claudecad` ở đó) khi kèm `files` | Đã đưa `folder` thì phải tồn tại, có hay không có `files` | `SuaKthCTests.KTH04_05_07_…` |
| KTH-08 | Nhẹ | Checklist H5 C4 hứa bỏ qua mọi file đang mở có thay đổi chưa lưu, code chỉ biết của tab đang xem | Sửa C4 ở `docs/checklist-dot-h5.md` và khối H5 của `docs/checklist-tong-the.md`: tab khác thì in bản đang hiện, nhờ lưu trước | `SuaKthCTests.KTH08_…` |

## Hai nghi vấn

| Nghi vấn | Kết luận | Đã làm | Test |
| --- | --- | --- | --- |
| Macro sửa tay có `batch_files` `action: "run"` lọt qua thẻ (`RequireCard` cho qua khi phát macro) | **Đúng là lỗi** (lệnh không được ghi vào macro, nhưng file macro sửa tay thì phát được): áp chuẩn / in PDF hàng loạt có thể chạy không có thẻ | `batch_files` từ chối hẳn khi đang phát macro (mọi action), nhờ khách làm trong chat | `SuaKthCTests.KTH04_05_07_and_macros_…` |
| Thời gian chờ 30 s của `export_drawing`, `compare_drawings`, `company_standard learn` | **Đúng** (không nằm trong nhóm lệnh chậm) | `timeoutFor`: `export_drawing` 5 phút (trần chung); `compare_drawings`, `company_standard` action learn 2 phút. Số kỹ thuật bên trong, khách không thấy trực tiếp | `nangcaph5.test.ts` "slow tools too" |

Con số mới (khách thấy gián tiếp: file quá lâu báo lỗi hết giờ): 2 phút / 5 phút, ghi vào bảng "Con số chờ chủ dự án duyệt" của
`docs/ban-giao/nang-cap-h5.md`.

Test sau lần sửa cuối: lõi **1585 / 1585** (thêm 4 ở `SuaKthCTests.cs`), trợ lý **441 / 441** (thêm 5), build `net48`, `net8.0-windows`, `net10.0-windows` 0 lỗi (1
cảnh báo CA2022 có sẵn ở `TraceTools.cs`).

## Soát lại KTH-02 (sau kiểm tra độc lập)

Thread kiểm tra độc lập soát lại: KTH-01, 03 → 08 và hai nghi vấn sửa đúng. KTH-02 mới đúng phần chính: khi một file quá hạn
(`TIMEOUT`), lượt vẫn ghi lỗi cho file đó rồi đi tiếp file sau.

Đã sửa (`agent/src/tools/batchFiles.ts`):

- gặp `TIMEOUT` thì **dừng lượt** (không bắt đầu file kế tiếp, vì AutoCAD có thể vẫn đang làm file đó);
- trả kết quả dở qua `partialResult`: kể các file đã xong;
- thêm `timed_out_file` + `check_first`: file đó có thể đã ghi / in xong (áp chuẩn thì bản sao lưu đã có), nhờ khách mở kiểm
  trước khi chạy lại;
- không ghi file tổng hợp (như các trường hợp dừng giữa lượt khác).

Nhánh đã gộp `full/nang-cap-h-tong-the` @ `b7e408f` trước khi sửa. Không đụng `DrawingCompare.cs`.

Test sau lần sửa này: lõi **1605 / 1605**, trợ lý **443 / 443** (thêm 2 ở `agent/test/nangcaph5.test.ts`: quá hạn giữa lượt,
quá hạn ở file đầu); build 3 target 0 lỗi (1 cảnh báo CA2022 có sẵn). Không chạy CI (thread giữ nhánh tích hợp chạy).
