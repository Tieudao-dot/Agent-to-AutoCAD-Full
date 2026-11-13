# Bàn giao: Test & sửa Nâng cấp đợt C (tiện ích 2D)

*13/11/2026 · thread Test & sửa đợt C (con của Nâng cấp đợt C) · nhánh `full/nang-cap-c-test` (xuất phát `full/nang-cap-c`
@ `1305053`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **P** của [`checklist-thu-that.md`](../checklist-thu-that.md)
(P14–P20 là các lỗi dưới đây).*

## Lỗi tìm thấy → sửa

| # | Lỗi (người dùng sẽ thấy gì) | Sửa | Ở đâu |
| --- | --- | --- | --- |
| NC-1 | **Tìm & thay làm hỏng định dạng chữ.** Thay "P" trong MTEXT 2 dòng thì mã xuống dòng `\P` thành `\X` (2 dòng dính thành một, hiện `\X`); thay "c" trong TEXT có `%%c100` thì ký hiệu Ø thành `%%X100`; tương tự với mã font / màu / cỡ chữ (`\fArial;`, `\C1;`…). Chữ thay có `\ { }` (vd đường dẫn, `{B}`) bị MTEXT hiểu thành mã định dạng | Chỉ thay trong phần chữ thường, không bao giờ chạm mã định dạng (`FormatSegments`, `ReplaceFormatted`): TEXT giữ `%%c %%d %%p %%nnn…`; MTEXT, thuộc tính nhiều dòng, multileader, chữ kích thước, ô bảng giữ thêm `\P`, `\~`, `\U+xxxx`, mã có tham số tới `;`, ngoặc `{ }`. Chữ thay ghi vào MTEXT được thoát (`\` `{` `}` hiện đúng như gõ, xuống dòng thành `\P`). Tìm chính mã `%%c` vẫn được (tìm có `%%` thì `%%` là chữ thường) | `Core/Util2D.cs`, `Drawing/Util2DTools.cs` (`TextHits`, `AttributeHits`) |
| NC-2 | **Đếm block theo thuộc tính hằng ra "(trống)".** Thuộc tính Constant nằm trong định nghĩa block, không có trong từng block chèn | Đọc thêm thuộc tính hằng của định nghĩa (lưu tạm theo định nghĩa), thuộc tính thường vẫn ưu tiên | `Util2DTools.cs` (`ConstantAttributes`) |
| NC-3 | **In nhiều layout vào một PDF có thể ra trang sai / trắng.** Code mở từng layout rồi quay về layout cũ *trước khi* in, nên lúc in các trang không phải layout hiện hành (AutoCAD in khung nhìn của layout hiện hành; mẫu in nhiều trang của Autodesk luôn chuyển layout trước mỗi trang) | Chuyển sang layout của trang ngay trước `BeginPage`; in xong vẫn trả về layout đang mở (trong `finally`) | `LayoutTools.cs` (`PlotPdf`) |
| NC-4 | **`output` tương đối ("ketqua.pdf") rơi vào thư mục của AutoCAD** (vd `C:\Program Files\Autodesk\…`, thường không ghi được), vì `GetFullPath` tính theo thư mục làm việc của AutoCAD. Tên file có `? * \|` hoặc tên dành riêng (CON, NUL) không báo rõ | Đường dẫn tương đối tính từ **thư mục bản vẽ** (bản vẽ chưa lưu: Documents). Tên file không hợp lệ trên Windows: báo lỗi rõ (`BadFileName`), không in | `LayoutTools.cs`, `Util2D.cs` |
| NC-5 | **Kích thước mặt bằng sai ở bản vẽ đơn vị mét.** Các điểm cách nhau dưới 1 đơn vị bị gộp: bản vẽ mét mất cửa 0,9 m, ô tường 0,2 m | Dung sai theo cỡ nhà: 1 đơn vị, tối đa 1/10 000 kích thước nhà (`PieceTolerance`); bản vẽ mm giữ đúng như cũ | `Util2D.cs`, `Util2DTools.cs` |
| NC-6 | **Bảng diện tích bỏ sót phòng vẽ bằng polyline không bấm Close** (điểm cuối bắt vào điểm đầu, rất hay gặp) | Polyline có điểm cuối trùng điểm đầu (≥ 4 đỉnh) tính là vùng kín; bỏ đỉnh lặp khi lấy đường bao để hatch trùng vẫn được tính một lần | `Util2DTools.cs` (`ClosedByEnds`, `Outline`) |

Đã rà, không phải lỗi: hatch bấm điểm (lỗi giữa chừng thì cả lượt hủy — dispatcher `Abort`, đối tượng tạm không còn; đường bao
không phải Curve được `Dispose`); thẻ xác nhận tìm & thay (trợ lý chặn trước, plugin kiểm `user_confirmed` **trước** mọi lệnh
ghi; `batch_execute`, thẻ thiếu handle, handle thuộc tính của block đều đã có test); `match_handles` tối đa 5000 = giới hạn
`handles` của `request_confirmation`; nhãn gói 2D / Full của 6 lệnh; gọi cũ `draw_hatch` / `plot_pdf`; `layouts: ["all"]` khi chỉ
có Model báo lỗi; `"all"` lẫn với tên khác là tên layout.

Trợ lý: chỉ đổi mô tả `output` của `plot_pdf` (đường dẫn tương đối nằm cạnh bản vẽ). System prompt không đổi, token gần như
không đổi (+~50 ký tự ở gói 2D / Full).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **613** test (584 trước). `NangCapCTestSuaTests.cs` (29): mã `\P`, mã có
  tham số, `\U+`, nhóm `{ }`, `%%c` / `%%nnn` không bị chạm; tìm `%%c` vẫn thay; chữ thay được thoát trong MTEXT, không thoát
  trong TEXT; ghép lại các đoạn ra đúng chữ gốc; nguyên từ / hoa thường trong từng đoạn; plugin không còn gọi `Replace` trơn;
  thuộc tính hằng (kiểm mã nguồn + đếm không phân biệt hoa thường); layout hiện hành trước `BeginPage`, trả lại layout cũ;
  `output` tương đối, 9 tên file đúng / sai, tên an toàn từ `SafeFileName` luôn hợp lệ; `all` / `*` / thứ tự tab; nhà đơn vị mét
  giữ cửa 0,9; dung sai mm = 1; cửa sát góc + trục xiên; polyline khép bằng điểm cuối; hatch trùng phòng chữ L, chữ ở góc lõm
  không thuộc phòng; bảng diện tích dấu phẩy + tổng; kiểm thẻ trước khi ghi.
- Trợ lý: `cd agent && npm ci && npm test` — **270** test, xanh (không thêm: chỉ đổi mô tả).
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, .NET SDK 10 từ kho Ubuntu,
  `DOTNET_ROLL_FORWARD=Major` để chạy test net8 trên runtime 10): xanh, 0 cảnh báo.
- CI: xem mục cuối.

## Còn phải thử tay (chỉ thấy trên AutoCAD thật)

P14–P20 trong checklist, cùng các mục cũ của đợt C: dò đường bao (`TraceBoundary`) khi điểm ngoài màn hình / ở layout giấy;
in nhiều trang (khổ giấy chung cho mọi trang, chưa theo khổ từng layout); ghi MTEXT / thuộc tính nhiều dòng / multileader / ô
bảng sau khi thay.

## Giới hạn còn lại (không sửa ở bước này)

- Tìm & thay: chữ cần tìm bị mã định dạng chen giữa (vd đổi màu một nửa chữ) không khớp; chữ trong định nghĩa block và xref
  không thay. Sau khi bấm Đồng ý, plugin kiểm **đối tượng** đúng thẻ, không kiểm lại chữ tìm / thay (Claude phải gọi đúng như
  câu trên thẻ).
- Thống kê block: block lồng trong block khác không đếm.
- Bảng diện tích: số m² giả định bản vẽ mm (chưa đọc INSUNITS); tổng lấy từ diện tích chưa làm tròn nên có thể lệch 0,01 so
  với cộng tay các dòng đã làm tròn.
- `CLAUDECHATSELFTEST` vẫn chưa có bước đợt C (cần bản vẽ mẫu có tường / block / chữ).

## Commit

Xem `git log full/nang-cap-c-test`: commit sửa NC-1…NC-6 + test + tài liệu, sau đó commit ghi kết quả CI vào file này.
