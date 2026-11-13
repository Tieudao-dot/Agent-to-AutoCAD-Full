# Bàn giao: Nâng cấp đợt C (tiện ích 2D)

*13/11/2026 · thread Nâng cấp đợt C · nhánh `full/nang-cap-c` (xuất phát `full/nang-cap-b-test` @ `265b66e` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **P** của
[`checklist-thu-that.md`](../checklist-thu-that.md).*

## Đã làm

Mọi việc ở gói **2D / Full** (phương án A, chủ dự án chốt 12/11 + 13/11: chữ / kích thước / bảng / hatch / in chỉ ở 2D /
Full). Gói 3D không thấy và không gọi được (plugin chặn `NOT_IN_PLAN`, trợ lý lọc theo `plan_tools`).

| # | Việc | Lệnh (tên cho Claude) | Ở đâu | Ghi chú |
| --- | --- | --- | --- | --- |
| C1 | Hatch bấm một điểm | `draw_hatch` (nâng cấp: `pick_point`, `islands`, `keep_boundary`) | `DrawTools.cs` (`DrawHatch`); `Util2DLogic.OuterLoopIndex`, `NoBoundaryMessage` | `Editor.TraceBoundary` dò đường bao kín quanh điểm (như HATCH "Pick points"), vùng lớn nhất là bao ngoài, các vùng trong là đảo (`islands: false` = bỏ đảo). Đường bao dò được thêm vào bản vẽ để làm biên, xong thì xóa (hatch không liên kết); `keep_boundary: true` giữ lại polyline biên (hatch liên kết). Không có vùng kín: báo rõ (vùng hở hoặc điểm ở ngoài / ngoài màn hình), không vẽ gì. Gọi cũ (`boundary_handles`, `boundary_points`) đi đúng đường cũ |
| C2 | Tự ghi kích thước cả mặt bằng | `dim_floor_plan` (mới) | `Util2DTools.cs`; `Util2DLogic.FloorPlanDims`, `Visible`, `DefaultDimOffset` | Tường: `wall_layers` (mặc định `A-WALL`) hoặc `wall_handles` (line / polyline / cung lấy dây cung). Mỗi phía (`sides`, mặc định 4 phía) tối đa 3 hàng ngoài tường: **chi tiết** (đỉnh tường nhìn thấy từ phía đó: góc, mép lỗ cửa; đỉnh bị tường ngoài che như mặt trong, tường trong thì không ghi), **trục** (khi có `axis_layers` / `axis_handles`: trục dọc cho phía trên / dưới, trục ngang cho trái / phải), **tổng**. Hàng giống hàng sau thì bỏ (phía không có lỗ chỉ còn hàng tổng). Khoảng cách: `offset` (hàng 1 cách tường) và `spacing` (giữa các hàng), mặc định 8 mm trên giấy × DIMSCALE, ít nhất 5 % kích thước nhà. Kiểu kích thước hiện hành (hoặc `dim_style`). Tối đa 600 kích thước |
| C3 | Thống kê block ra bảng | `block_schedule` (mới) | `Util2DTools.cs`; `Util2DLogic.CountBlocks`, `BlockTable`, `NaturalComparer` | Đếm theo tên block (block động theo tên gốc; bỏ xref, block ẩn danh, layout), lọc `names` / `layers` (ký tự đại diện), theo giá trị một thuộc tính (`group_by_attribute`, vd `MA`; tag không phân biệt hoa thường; không có / trống = "(trống)"). Mã xếp theo số (D2 trước D10). Trả số cho Claude; có `table_position` thì chèn bảng (STT, tên, giá trị, số lượng, dòng Tổng) qua đúng code của `add_table`. Không `table_position` = chỉ đọc (không thêm bước hoàn tác) |
| C4 | Tổng diện tích phòng / theo layer ra bảng | `area_schedule` (mới) | `Util2DTools.cs`; `Util2DLogic.AreaRows`, `RoomNames`, `Dedupe`, `Containers`, `AreaTable` | Vùng kín: polyline kín, đường tròn, elip, spline kín, hatch, region. `group_by`: `layer` (mặc định: số vùng + tổng theo layer) hoặc `room` (tên phòng = chữ TEXT / MTEXT nằm trong vùng, vùng trong cùng nhận chữ; nhiều chữ nối theo thứ tự đọc; không có chữ = "(chưa có tên)"). Hatch vẽ trên polyline phòng (cùng diện tích, cùng tâm) tính một lần. **Vùng bao vùng khác** (vd polyline bao cả nhà trên layer phòng): báo `contains_others`; có `table_position` thì **không chèn bảng** cho tới khi Claude hỏi người dùng (bỏ vùng đó, hoặc `allow_nested: true`). Đơn vị m² (bản vẽ mm), `decimals` mặc định 2, bảng dùng dấu phẩy thập phân |
| C5 | Tìm & thay chữ | `find_replace_text` (mới) | `Util2DTools.cs`; `Util2DLogic.Replace`, `NotApproved`, `ReplaceCardMessage`; trợ lý `confirmation.ts` (`replaceNotApproved`, `REPLACE_TOOLS`) | TEXT, MTEXT, thuộc tính block, chữ ghi đè của kích thước, multileader, ô bảng (`types` để lọc), `match_case`, `whole_word`, `layers`, `layout`. **Luôn hai bước:** (1) không `handles` = xem trước, không đổi gì: số đối tượng, số chỗ thay, ví dụ trước / sau, `match_handles`; (2) Claude gọi `request_confirmation` với `match_handles`, người dùng **bấm Đồng ý trên thẻ**, rồi gọi lại với `handles`. Thay một chữ cũng cần thẻ, gõ "ok" hay nút "Làm" của thẻ kế hoạch không tính. Chặn hai lớp: trợ lý từ chối trước khi gửi, plugin chỉ thay đối tượng có trong `user_confirmed` (trợ lý thêm = handle trên thẻ; Claude không đặt được; macro không ghi nên macro chạy lại bị từ chối). MTEXT: thay trong nội dung gốc; chuỗi tìm có `\`, `{`, `}` thì bỏ qua MTEXT (không cắt mã định dạng). Layer khóa: báo lỗi, cả lượt quay lại |
| C6 | In nhiều layout vào một PDF | `plot_pdf` (nâng cấp: `layouts`) | `LayoutTools.cs` (`PlotPdf`, `ConfigurePlot`, `LayoutTabOrder`); `Util2DLogic.ResolveLayouts`, `SafeFileName`, `DefaultPdfName` | `layouts: ["A1", "A2"]` (theo thứ tự nêu) hoặc `["all"]` (mọi layout giấy theo thứ tự tab) → **một** file PDF nhiều trang (một `BeginDocument`, mỗi layout một trang). Mặc định `<bản vẽ>-<n>-layouts.pdf` cạnh bản vẽ. Mỗi trang lấy thiết lập in của layout đó rồi đè máy in / khổ / tỉ lệ / bảng nét như lệnh cũ. Không cho `layout` cùng `layouts`, không cho `area: "window"` với nhiều layout. **Tên file an toàn** (cả khi in một layout): ký tự `\ / : * ? " < > |` trong tên layout đổi thành `_`, bỏ dấu chấm / cách cuối, tên dành riêng của Windows (CON, NUL…) thêm `_`; `output` là thư mục hoặc đường dẫn sai thì báo lỗi. Layout đang mở giữ nguyên sau khi in |

Kèm theo:

- `Core/Plans.cs`: 4 lệnh mới nhãn **TwoD**; `draw_hatch`, `plot_pdf` giữ nhãn TwoD.
- `ToolDispatcher`: `block_schedule` / `area_schedule` không có `table_position`, `find_replace_text` không có `handles` là
  lệnh **chỉ đọc** (`readsWhen`): không chép bản vẽ, không thêm bước hoàn tác, không ghi macro.
- Trợ lý: `agent/src/tools/utility2d.ts` (4 lệnh mới), tham số mới trong `draw.ts`, `layouts.ts`. System prompt không đổi.

## Giữ đúng luật đã chốt

- **Gói:** cả 6 việc chỉ ở 2D / Full. Test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, trợ lý `tools.test.ts`)
  qua cho 4 lệnh mới.
- **Luật xác nhận không nới:** `find_replace_text` khi thay được tính vào số đối tượng có sẵn bị sửa của lượt
  (`DESTRUCTIVE`), và **luôn** cần nút Đồng ý của thẻ liệt kê đúng các handle đó (chặt hơn luật 20 đối tượng; thẻ chính là câu
  hỏi nên trên 20 vẫn qua sau khi bấm). Các lệnh còn lại chỉ tạo mới (kích thước, hatch, bảng) hoặc chỉ đọc. Không lệnh nào
  đụng khối 3D.
- **Hoàn tác:** mọi lệnh ghi chạy trong transaction + nhóm undo của lượt; U một lần bỏ cả lượt. `plot_pdf` không đổi bản vẽ.
- **Tên lệnh cũ giữ nguyên**, gọi cũ của `draw_hatch` / `plot_pdf` vẫn hợp lệ (macro cũ chạy như trước).
- Kết quả không dùng khóa `handles` cho đối tượng có sẵn (xem trước trả `match_handles`, bảng diện tích trả `object`), để trợ
  lý không coi chúng là "đối tượng mới của lượt".

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **584** test (551 trước). `NangCapCTests.cs` (33): nhãn 6 lệnh (gói 3D bị
  chặn cả trong macro); đăng ký + `readsWhen`; bao ngoài của hatch; kiểm mã nguồn hatch (dò, dọn đối tượng tạm, liên kết);
  kích thước mặt bằng (cửa ở cạnh dưới: 0–1000–1900–10000 + tổng, phía không lỗ chỉ hàng tổng, hàng trục, đỉnh bị che /
  nhìn thấy, lỗi tham số, khoảng mặc định); đếm block theo tên / thuộc tính, thứ tự số, bảng; diện tích theo layer / phòng,
  chữ thuộc vùng trong cùng, hatch trùng polyline tính một lần, vùng bao vùng khác, điểm trong đa giác (hình L), m²; thay chữ
  (hoa thường, nguyên từ, không chồng, xóa bằng ""), mã MTEXT, handle chưa duyệt; kiểm mã nguồn plugin (kiểm thẻ trước khi ghi,
  layer khóa, `match_handles`); tên file an toàn (6 trường hợp), chọn layout (all / thứ tự / hoa thường / trùng / thiếu), tên
  PDF mặc định, một `BeginDocument` nhiều trang.
- Trợ lý: `cd agent && npm ci && npm test` — **270** test (259 trước). `test/nangcapc.test.ts` (11): nhãn 2D, gói 3D không thấy;
  gọi cũ + tham số mới của `draw_hatch` / `plot_pdf`; schema 4 lệnh mới; xem trước không hỏi, không thành "mới tạo"; thay
  không thẻ bị chặn (gõ "đồng ý", nút "Làm" cũng không tính), thẻ thiếu handle bị chặn, thẻ đủ thì qua (cả trên 20), trong
  `batch_execute`; `user_confirmed` chỉ là handle trên thẻ, Claude đặt thì bị bỏ; kích thước / hatch / bảng là đối tượng mới.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, .NET SDK 10 từ kho Ubuntu): xanh,
  0 cảnh báo.
- CI: xem mục "CI" cuối file.

## Token mô tả lệnh + system prompt (cùng cách đo với đợt A / B, ~3,5 ký tự / token)

Đo như plugin gửi (`plan_tools` theo gói, kể cả 5 lệnh thẻ, tên + mô tả + schema JSON + system prompt). Trước = `265b66e`
(ra đúng số "sau" của đợt B).

| Gói | Số lệnh trước → sau | Ký tự trước | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 101 → 105 | 128 024 | 135 288 | ~36,6k → ~38,7k (**+2,1k**) |
| 3D | 87 → 87 | 127 011 | 127 011 | ~36,3k (không đổi) |
| Full | 126 → 130 | 181 726 | 188 990 | ~51,9k → ~54,0k (**+2,1k**) |

Phần thêm: mô tả + schema 4 lệnh mới (~5,8k ký tự), tham số mới của `draw_hatch` / `plot_pdf` (~1,3k). System prompt không
đổi (luật thẻ của tìm & thay nằm trong mô tả lệnh, không tốn token ở gói 3D).

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật. Phần cần AutoCAD (`TraceBoundary`, `RotatedDimension` theo kiểu kích thước, `Table`, ghi
  MTEXT / thuộc tính nhiều dòng / multileader / ô bảng, in nhiều trang) chỉ kiểm bằng build + kiểm mã nguồn.
- Hatch bấm điểm: AutoCAD chỉ dò đối tượng **đang thấy trên màn hình**; điểm ngoài màn hình có thể báo "không kín". Đường bao
  dò theo mặt phẳng XY (cao độ 0).
- Kích thước mặt bằng: chỉ phân tích đoạn thẳng (cung lấy dây cung); tường xiên ghi theo hình chiếu ngang / dọc; nhà có sân
  trong / tường lồi lõm nhiều thì hàng chi tiết có thể dày, Claude nên hỏi bớt phía hoặc chỉ đưa `wall_handles` của tường
  ngoài. Không ghi kích thước bên trong nhà.
- Bảng diện tích: region dùng khung bao để tìm chữ trong vùng; bản vẽ không theo mm thì số m² sai (chưa đọc INSUNITS).
- Tìm & thay: chữ trong block định nghĩa (không phải thuộc tính) và xref không thay; MTEXT có mã định dạng chen giữa chữ cần
  tìm (vd đổi màu một phần chữ) thì không khớp.
- In nhiều layout: mỗi trang theo khổ giấy chung (`media`, mặc định A3), chưa theo khổ riêng của từng layout.
- `CLAUDECHATSELFTEST` chưa có bước của đợt C (các lệnh cần bản vẽ mẫu có tường / block / chữ): để thread con cân nhắc.

## Việc cho thread con Test & sửa đợt C

Rà 6 việc trên, đặc biệt: hatch bấm điểm khi vùng có đảo / chữ bên trong, khi ném lỗi giữa chừng (đối tượng tạm không bị bỏ
lại); kích thước mặt bằng với tường polyline có bề dày, cửa sát góc, trục xiên, phía có tường lồi; block động có thuộc tính,
block lồng; diện tích hatch nhiều vùng, phòng hình L với chữ ở góc lõm; tìm & thay qua `batch_execute`, thẻ có thêm handle khác,
handle của block thay vì thuộc tính, layer khóa, thay xong số chỗ khác xem trước; in "all" khi chỉ có Model, tên layout có ký
tự lạ, `output` tương đối; token (rút gọn mô tả nếu đáng).

## CI

(điền sau khi chạy)
