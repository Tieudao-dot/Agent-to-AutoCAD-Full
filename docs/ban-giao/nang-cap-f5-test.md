# Bàn giao: Test & sửa đợt F5 (25/11/2026)

Thread: `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà; người viết F5 là thread khác). Nhánh `full/nang-cap-f5-test`, tách từ
`full/nang-cap-f5` @ `1dc03b9`, gộp `full/nang-cap-f-tong-the` @ `03bf163` (bản sửa sau kiểm tra độc lập đợt F, KTF).

## 1. Gộp nhánh

- Commit gộp `33e67bb`. Giữ cả hai phía. Hai chỗ chồng nhau, ghép tay:
  - `agent/src/tools/render.ts` (mô tả `named_view`): giữ câu "ghi đè / xóa góc nhìn hoặc PNG đã có cần thẻ" của F5 và câu
    "nhìn theo góc nhìn đã lưu (không giữ zoom)" của KTF.
  - `bo-cai/README.md`: giữ dòng F4 có phần "Sau kiểm tra độc lập" và dòng F5.
- Sau gộp, trước khi sửa: lõi 1205 = 1189 + 16, trợ lý 364 = 355 + 9, đúng số dự kiến. Đã soát bằng test mã nguồn: các bản
  sửa KTF (số m² / m³ trong Excel, bỏ dòng "Tổng" khỏi số dòng, ghi file qua `File.Replace`, khung UCS cho
  `build_3d_from_plan`, `get_extents` trong UCS, macro mang `mmPerUnit`, khởi động sẵn cho bản vẽ thứ hai) vẫn còn; tên kiểu
  ghi chú mũi tên F5 dùng hậu tố đơn vị của KTF (vd `VN-ML-100-DM` cho bản vẽ đơn vị dm).

## 2. Lỗi tìm thấy và đã sửa

| Mã | Lỗi | Sửa |
| --- | --- | --- |
| TF5-1 | Thẻ "ghi đè góc nhìn X" và thẻ "xóa góc nhìn X" dùng cùng mã (handle của góc nhìn). Khách đồng ý ghi đè thì Claude có thể dùng lại lần đồng ý đó để **xóa** góc nhìn mà không hỏi lại. | Xóa góc nhìn có mã riêng `F5Confirm.DeleteViewToken(handle)` (16 hex). Ghi đè vẫn dùng handle. |
| TF5-2 | Mã file lấy từ chuỗi đường dẫn như đã ghép, nên cùng một file viết hai kiểu (`D:\a\.\b.pdf` và `D:\a\b.pdf`) ra hai mã: khách bị hỏi lại lần nữa. | `RequireOverwriteCard` chuẩn hóa đường dẫn (`Path.GetFullPath`) trước khi lấy mã. Hoa / thường và `/` / `\` vốn đã cùng mã. |

Không có lỗi nào cho phép vượt thẻ.

## 3. Đã rà, không phải sửa

- **Thẻ:**
  - `overwrite` / `replace` cũ không thay được thẻ ngoài macro.
  - Mã tự đặt trong `confirm_handles` không qua được: trợ lý chỉ đổi thành `user_confirmed` những mã có trên thẻ khách đã
    bấm Đồng ý, kể cả khi gọi lô (batch).
  - "Chỉ xem", gói và chế độ vẫn chặn trước khi hỏi thẻ.
  - Chạy lại macro: không hiện thẻ, không treo. Thiếu `overwrite` / `replace` thì từ chối như cũ, không ghi file.
  - Bấm Hủy khi xuất Excel thì không đổi gì, kể cả bảng vẽ trong bản vẽ: lời từ chối hủy cả giao dịch.
  - Bảng không có dòng nào thì từ chối trước khi hỏi thẻ.
  - Tên ngắn kiểu 8.3 (`BAOCAO~1.XLS`) chỉ làm hỏi thêm một thẻ, không vượt được thẻ (ghi chú, không sửa).
- **Ô ⚙ "Nạp gọn lệnh":**
  - Thứ tự ưu tiên: biến môi trường > file `tool-search-off` > ô ⚙.
  - Bật / tắt giữa chat: trợ lý đóng tiến trình Claude cũ rồi mới mở lại (không chạy hai tiến trình), nối tiếp cuộc chat.
    *(Bản chốt đợt F đổi lại: cuộc chat đã có tin giữ cách nạp tới "Chat mới"; xem `docs/ban-giao/nang-cap-f5.md` mục 1.)*
  - Khởi động sẵn nhận cài đặt mới. Máy gói 2D / 3D cũng có ô này.
- **Ghi chú mũi tên:**
  - Kiểu theo đơn vị bản vẽ và tỷ lệ. Kiểu đã có mà khác thì cần thẻ.
  - `add_mleader` trong UCS xoay đi qua đường đổi điểm của F1.
  - Bản vẽ không có bộ chuẩn giữ như trước F5.
- **Vật liệu:** gán cho cả layer và đổi màu vật liệu đã có đều hỏi thẻ. Vật liệu của khách trùng tên vật liệu mẫu được giữ.

## 4. Con số chờ chủ dự án duyệt

Không đổi, không thêm số nào. Đã soát bảng của F5 (`docs/ban-giao/nang-cap-f5.md`): các số khớp code. Chữ cao 2,5 mm, mũi
tên 2,5 mm, khoảng hở 1 mm, đoạn gãy 2,5 mm đều ở `F5Numbers`.

## 5. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Mã thẻ xóa góc nhìn | mã riêng (băm "delete-view:" + handle), khác mã ghi đè | dùng lại handle: bỏ `DeleteViewToken` trong `RenderTools.cs` |
| Tên ngắn 8.3 | không đổi về tên dài (chỉ hỏi thêm một thẻ) | đổi bằng API Windows nếu khách hay gặp |

Bảng "Quyết định tự chọn" của F5: đã soát, giữ nguyên. Riêng dòng "mã thẻ" ở bảng mục 2 của F5 nay là: ghi đè góc nhìn =
handle, xóa góc nhìn = mã riêng.

## 6. Test

- Thêm `plugin/ClaudeCAD.Core.Tests/NangCapF5TestSuaTests.cs` (7 test): mã xóa góc nhìn, một file một mã, mã tự đặt không
  qua, cờ cũ chỉ tính trong macro, thứ tự Excel (số → bảng rỗng → thẻ → ghi), các bản sửa KTF còn sau gộp, vật liệu.
- Sửa 1 dòng test mã nguồn trong `NangCapF5Tests.cs` theo mã xóa góc nhìn mới (ý của test giữ nguyên).
- Sau lần sửa cuối: lõi 1212 / 1212 xanh, trợ lý 364 / 364 xanh, build `net48`, `net8.0-windows`, `net10.0-windows` (xóa
  `obj` / `bin` giữa các lần) không lỗi, không cảnh báo.

## 7. CI

Run 45627464838 (#53) trên `fc92cd6` (code cuối): **xanh** (job windows: build 3 target, test lõi, trợ lý, test giao diện).
Lượt này cũng kiểm KTF-01 lần 2. Dùng 1 / 2 lượt CI. Commit sau đó chỉ sửa tài liệu này.

## 8. Chưa kiểm được

- Chưa chạy trên AutoCAD thật: thẻ hiện đúng câu, multileader hiển thị đúng cỡ, ghi đè file đang mở trong Excel.
- Thử tay theo `docs/checklist-dot-f5.md`. Thêm một mục: lưu góc nhìn trùng tên rồi đồng ý ghi đè, sau đó bảo Claude xóa
  góc nhìn đó; phải hiện thẻ mới.
