# Bàn giao: Nâng cấp đợt B (sửa hình 2D)

*13/11/2026 · thread Nâng cấp đợt B · nhánh `full/nang-cap-b` (xuất phát `full/nang-cap-a-test` @ `33acb5f` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **O** của
[`checklist-thu-that.md`](../checklist-thu-that.md) (O0 = `CLAUDECHATSELFTEST`, có 1 bước mới của đợt B).*

## Đã làm

Gói theo phương án A (chủ dự án chốt 13/11): GROUP, MATCHPROP ở **mọi gói**; còn lại **2D / Full**.

| # | Việc | Lệnh (tên cho Claude) | Gói | Ở đâu | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| B1 | STRETCH | `stretch_entities` (mới) | 2D | `Drawing/Edit2DTools.cs`; `Edit2DLogic.StretchWindow`, `InPolygon`, `Classify` | Khung cắt `corner1` + `corner2` hoặc `polygon`; dời `from → to` hoặc `displacement`. Dùng điểm kéo giãn của AutoCAD (`GetStretchPoints` / `MoveStretchPointsAt`): đỉnh / đầu mút trong khung thì dời, cả đối tượng trong khung thì dời nguyên. Không truyền `handles`: lấy mọi đối tượng trong khung (bỏ layer khóa / đóng băng), **quá 20 đối tượng thì plugin từ chối và liệt kê handle** để Claude hỏi rồi gọi lại với `handles`. Khối 3D chỉ một phần trong khung: bỏ qua có lý do (cả khối trong khung thì dời như move) |
| B2 | BREAK | `break_entity` (mới) | 2D | `Edit2DLogic.Break` | `point` = bẻ đôi tại điểm (đường kín: từ chối); `point` + `point2` = bỏ đoạn giữa (đường kín: từ `point` tới `point2` theo chiều đường, đường tròn là ngược kim đồng hồ như AutoCAD). Điểm được kéo về đường gần nhất. Các mảnh có handle mới, bản gốc xóa (như `trim_entities`) |
| B3 | LENGTHEN | `lengthen_entity` (mới) | 2D | `Edit2DLogic.NewLength`, `LengthenSegment`, `LengthenArc`, `LengthenBulge` | Đường thẳng, cung, polyline hở (đoạn cuối thẳng hoặc cung). `mode`: `delta` (mặc định, âm = rút ngắn), `total`, `percent`. `end`: `start` / `end` hoặc `near_point`. Giữ handle; cung giữ tâm, bán kính; không cho thành đường tròn kín hay dài ≤ 0. Spline / elip: chưa hỗ trợ (báo rõ) |
| B4 | DIVIDE / MEASURE | `divide_entity` (mới) | 2D | `Edit2DLogic.Stations` | `count` (chia N phần: đường hở N − 1 mốc, đường kín N mốc) hoặc `segment_length` (đặt mốc cách đều, `from` start / end, không đặt mốc ở đầu cuối). Mốc là điểm hoặc `block_name` (block có sẵn, `align_block` mặc định xoay theo đường; thuộc tính block lấy giá trị mặc định). Tối đa 10 000 mốc. PDMODE 0 / 1 thì báo cho Claude nhắc người dùng đổi kiểu điểm |
| B5 | GROUP | `group_entities` (mới) | Mọi gói | `Edit2DTools.cs` | `action` `create` (mặc định; `name` tùy chọn, không có thì `CLAUDE_GROUP_n`; `description`) / `ungroup` (theo `name` hoặc nhóm chứa `handles`). Không đổi đối tượng. Tên trùng, tên sai ký tự, đối tượng ở hai không gian khác nhau: từ chối có lý do |
| B6 | MATCHPROP | `match_properties` (mới) | Mọi gói | `Edit2DLogic.MatchProps`; `PlanLogic.TextValues` | Chép từ `source` sang `handles`: `layer`, `color`, `linetype`, `lineweight`, `linetype_scale`, `transparency`, `thickness`, `text` (kiểu chữ, cao chữ, hệ số rộng, nghiêng: chữ → chữ, TEXT ↔ MTEXT), `dimension` (kiểu kích thước: kích thước → kích thước). Mặc định chép hết. **Gói 3D:** mặc định bỏ `text` / `dimension` (báo trong `skipped`); nêu rõ `text` / `dimension` thì plugin từ chối `NOT_IN_PLAN` (cả trong `batch_execute` và macro: "match_properties (thuộc tính chữ / kích thước)") |
| B7 | Bo / vát polyline và cung | `fillet_entities`, `chamfer_entities` (nâng cấp) | 2D | `ModifyTools.cs` (`CornerObjects`, `PolylineCorners`, `ArcCorner`); `Edit2DLogic.PolylineCorners`, `FilletArcs`, `ChamferArcs` | Hai đường thẳng: **đi đúng đường cũ** (`Geometry.FilletLines` / `ChamferLines`). Thêm: đường + cung, cung + cung (bo: chọn đường tròn bán kính r tiếp xúc cả hai gần góc nhất, tiếp điểm nằm giữa đầu giữ lại và góc; vát: khoảng đo dọc cung). Polyline: chỉ `handle1` (không `handle2`) = mọi góc giữa hai đoạn thẳng, hoặc `vertex` (chỉ số 0); sửa tại chỗ, giữ handle, giữ độ rộng đều; góc cạnh quá ngắn / sát đoạn cung / thẳng hàng bỏ qua có lý do (`skipped`), góc nêu bằng `vertex` mà không làm được thì báo lỗi. Bán kính 0 / khoảng 0 với polyline: báo lỗi (không có gì thay đổi) |
| B8 | Offset hai phía | `offset_entity` (nâng cấp) | 2D | `ModifyTools.cs`; `Edit2DLogic.CheckOffsetSides` | `both_sides: true` = một bản mỗi bên, không cần `side_point` (`side_point` thành tùy chọn). Một bên không offset được (vd. vào trong đường tròn nhỏ) thì làm bên còn lại và ghi chú. Gọi cũ có `side_point` chạy như cũ |
| B9 | Nối có dung sai | `join_entities` (nâng cấp) | 2D | `ModifyTools.cs` (`TryBridge`); `Edit2DLogic.Bridge` | `tolerance` (mm, mặc định 0 = như cũ): khe ≤ dung sai giữa đầu polyline và đầu đối tượng kế tiếp được nối bằng một **đoạn thẳng ngắn** (không xê dịch hình có sẵn); vẫn không nối được thì bỏ đoạn vừa thêm (trả lại bulge cũ). Polyline đã kín thì không nối thêm. Báo `bridged` (handle, khe). Hai đầu polyline cách nhau trong dung sai: để hở, ghi chú gợi ý `edit_polyline closed: true` |

Kèm theo:

- **`CLAUDECHATSELFTEST` thêm 1 bước** (`SelfTest.cs`): "Nâng cấp đợt B: bo 4 góc polyline, kéo dài, bẻ gãy, chia đều,
  nối có dung sai, kéo giãn". Bo R10 hình 100 × 50 (chu vi 220 + 20π ≈ 282,832), kéo dài đường 100 thêm 20, bỏ đoạn 30–50,
  chia mảnh đầu 3 phần (2 điểm), nối 2 đường hở 0,5 với dung sai 1 (dài 200), kéo giãn cạnh phải 50 (chu vi 400). Mỗi
  lệnh chạy trong một transaction riêng (mở lại đối tượng, đúng như dispatcher chạy từng lệnh), trên bản vẽ nháp của
  self-test (đóng không lưu khi xong).
- **Macro:** `source` của `match_properties` được ánh xạ handle khi chạy lại (`MacroLogic.IsHandleKey`). Lệnh nâng cấp
  giữ tên và tham số cũ nên macro cũ chạy như trước.
- `ToolDispatcher.Plan` (đọc gói của máy cho `match_properties`).

## Giữ đúng luật đã chốt

- **Luật xác nhận 2D không nới:** trợ lý đếm đối tượng có sẵn bị đổi cho `stretch_entities` (`handles`), `break_entity`,
  `lengthen_entity` (1), `match_properties` (`handles`), bo / vát polyline (1); quá 20 trong một lượt thì hỏi
  (`confirmation.ts`, `DESTRUCTIVE`). `stretch_entities` không có `handles`: plugin tự chặn trên 20. Lệnh chỉ tạo mới
  (`divide_entity`, `group_entities`, `offset_entity` hai phía) không tính; mốc của `divide_entity` là đối tượng mới của lượt.
- **Thẻ 3D:** không lệnh mới nào cộng / trừ / cắt / phá / xóa khối 3D. `stretch_entities` chỉ dời khối 3D nằm hẳn trong
  khung (như `move_entities`), khối nằm một phần thì bỏ qua; `match_properties` lên khối 3D chỉ đổi layer / màu… như
  `set_entity_props`. Luật thẻ của `explode_entities` / `erase_entities` / phép khối không đổi.
- **Nhãn:** test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, trợ lý `tools.test.ts`) qua cho 6 lệnh mới.
- **Hoàn tác:** mọi lệnh mới là lệnh ghi qua dispatcher (một transaction + nhóm undo của lượt), U một lần bỏ cả lượt.
- Gói 3D không có lệnh 2D mới nào; trợ lý ở gói 3D không thấy 4 lệnh 2D (lọc theo `plan_tools`).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **531** test (485 trước đợt B). `NangCapBTests.cs` (46): nhãn 6 lệnh mới
  và 4 lệnh nâng cấp; `match_properties` chữ / kích thước bị chặn ở gói 3D (danh sách, chuỗi, macro, câu báo), mặc định
  theo gói; khung kéo giãn (trong / một phần / ngoài, cạnh khung, đa giác); bẻ gãy đường hở / kín / tại đầu; độ dài mới 3
  chế độ, kéo dài đường / cung / đoạn cung của polyline (giữ tâm, bán kính, bulge đúng); chia / đặt mốc (hở, kín, từ cuối,
  giới hạn); bo 4 góc hình chữ nhật, một đỉnh, rẽ phải, góc quá ngắn, sát đoạn cung, bo hết cạnh không để đỉnh trùng; vát
  polyline 2 khoảng; bo đường + cung (tâm, tiếp điểm, cung ngắn), bán kính 0, cung + cung, không gặp nhau, bán kính quá
  lớn; vát đường + cung đo dọc cung; nối có dung sai; offset hai phía; `source` trong macro; kiểm mã nguồn các phần cần
  AutoCAD.
- Trợ lý: `cd agent && npm ci && npm test` — **253** test (245 trước). `test/nangcapb.test.ts` (8): 6 lệnh mới đúng nhãn;
  gọi cũ của 4 lệnh nâng cấp vẫn hợp lệ; tham số mới; dạng chính của lệnh mới; đếm đối tượng cho cổng xác nhận (quá 20
  hỏi, đồng ý thì qua, trong `batch_execute`); mốc chia là đối tượng mới.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: run **43408010489 xanh** ở `24c3abe` (`release: "false"`, Windows: trợ lý qua named pipe, test lõi, UI test WPF, build
  3 bản, gói + Setup.exe; bước phát hành bỏ qua đúng). ~9,5 phút. Run 43407328829 (ở `a87741e`) tớ tự hủy để chạy lại
  trên commit sửa self-test, không tính là lượt thứ hai.

## Commit

`07e6a50` (code 9 việc + test), `a87741e` (bàn giao, đo token), `24c3abe` (self-test chạy từng lệnh trong transaction
riêng; nối-khe với polyline kín, CI xanh), sau đó một commit ghi kết quả CI vào file này (chỉ tài liệu; CI của thread con
phủ nó).

## Token mô tả lệnh + system prompt (cùng cách đo với đợt A, ~3,5 ký tự / token)

Đo như plugin gửi (`plan_tools` theo gói, kể cả 5 lệnh thẻ, tên + mô tả + schema JSON + system prompt). Trước = `d87524e`
(đầu nhánh, = `full/nang-cap-a-test` + tài liệu).

| Gói | Số lệnh trước → sau | Ký tự trước | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 95 → 101 | 120 311 | 128 024 | ~34,4k → ~36,6k (**+2,2k**) |
| 3D | 85 → 87 | 125 101 | 127 011 | ~35,7k → ~36,3k (**+0,5k**) |
| Full | 120 → 126 | 174 013 | 181 726 | ~49,7k → ~51,9k (**+2,2k**) |

System prompt không đổi. Phần thêm là mô tả + schema của 6 lệnh mới (~6,3k ký tự) và tham số mới của 4 lệnh nâng cấp
(~1,4k). Có thể rút gọn mô tả thêm ~1–2k ký tự nếu cần (chưa làm, để thread con cân nhắc).

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật (cả 9 việc). Phần cần AutoCAD (điểm kéo giãn của kích thước / block / chữ, `GetSplitCurves`
  của đường kín, `GetOffsetCurves` hai phía, `JoinEntity` sau đoạn nối khe, `Group`) chỉ kiểm bằng build + kiểm mã nguồn
  + bước self-test.
- `stretch_entities` không `handles` chỉ quét **Model**, không quét layout giấy.
- Bẻ gãy đường kín có `point` trùng điểm đầu của đường (vd. góc 0° của đường tròn): phụ thuộc cách AutoCAD tách; cần thử.
- Bo / vát đường + cung: chọn theo góc gần đầu mút nhất; hai đối tượng cắt nhau ở nhiều chỗ thì có thể chọn góc khác ý
  người dùng (chưa có điểm chọn như lệnh FILLET). Polyline + đối tượng khác (đường rời) chưa hỗ trợ: báo dùng polyline
  một mình hoặc `join_entities` trước.
- Bo / vát polyline: độ rộng khác nhau theo đỉnh không giữ được (chỉ giữ độ rộng đều).
- `divide_entity` với block có thuộc tính: thuộc tính nhận giá trị mặc định.
- `match_properties` chưa chép thuộc tính hatch, kiểu bảng, kiểu multileader.

## Sửa sau kiểm tra độc lập nâng cấp

*13/11/2026 · lỗi từ [`kiem-tra-doc-lap-nang-cap.md`](kiem-tra-doc-lap-nang-cap.md) (nhánh `full/kiem-tra-doc-lap-2` @
`8e6fede`) thuộc đợt B. Đã gộp nhánh tích hợp `full/nang-cap-tong-the` @ `f1e7123` vào `full/nang-cap-b` trước khi sửa.
Không chạy CI ở bước này (thread "Test tổng thể nâng cấp" gộp bản sửa của mọi đợt rồi chạy CI chung).*

| Mã | Mức | Lỗi | Sửa | Test |
| --- | --- | --- | --- | --- |
| KT2-03 | vừa | Kéo giãn theo khung sửa cả đối tượng trên **layer đang tắt** (người dùng không thấy) và tính chúng vào giới hạn 20 | Khung chọn bỏ qua layer tắt như layer đóng băng (`Edit2DLogic.WindowLayerRule`), giống khung chọn của AutoCAD; báo `hidden_layer_skipped`. Layer khóa vẫn báo `locked_layer_skipped` như cũ | `NangCapBKt2Tests` (luật layer 6 trường hợp; `StretchEntities` dùng luật có `IsOff`); O21 |
| KT2-14 | nhẹ | Gói 3D: trợ lý vẫn mời `match_properties` `properties` `text` / `dimension` (plugin từ chối) | Gói không 2D: schema chỉ còn 7 thuộc tính chung, bỏ câu mô tả chữ / kích thước (`tools/index.ts` `trimForPlan`; hằng `MATCH_PROPS_*` trong `tools/modify.ts`). Gói 2D / Full giữ nguyên từng chữ | `agent/test/nangcapb-kt2.test.ts` |
| KT2-15 | nhẹ | Kéo giãn theo khung tính cả đối tượng **vừa tạo trong lượt** vào giới hạn 20 (vẽ 25 đường rồi kéo giãn thì bị từ chối) | Plugin chỉ so số đối tượng **có trước lượt** với giới hạn (`Edit2DLogic.CountedForLimit` + `ctx.CreatedThisTurn`), khớp cổng của trợ lý (vốn không tính đối tượng tạo trong lượt) | `NangCapBKt2Tests`; sửa 2 dòng kiểm mã nguồn cũ (`plan.Count > limit` → `existing > limit`, cùng ý); O22 |
| KT2-23 | nhẹ | Bỏ nhóm theo handle xóa **mọi** nhóm chứa các đối tượng đó | Đối tượng ở nhiều nhóm: từ chối trước khi bỏ gì, liệt kê tên nhóm, bảo Claude hỏi người dùng rồi gọi lại với `name` (`Edit2DLogic.UngroupRefusal`). Một nhóm: bỏ như cũ, trả tên nhóm trong `ungrouped` | `NangCapBKt2Tests`; O22 |
| KT2-24 | nhẹ | `lengthen_entity` trên LINE nghiêng / 3D chỉ tính trong mặt XY (sai độ dài; đường thẳng đứng báo "không có độ dài") | `Edit2DLogic.LengthenSegment` tính theo hướng 3D; đoạn polyline (2D) cho kết quả như trước | `NangCapBKt2Tests` (đường nghiêng, thẳng đứng, rút ngắn đầu, đoạn polyline) |
| KT2-25 | nhẹ | Schema `stretch_entities` `polygon` không giới hạn số đỉnh, plugin nhận tối đa 1000 | Thêm `.max(1000)` | `agent/test/nangcapb-kt2.test.ts` |

Test: lõi `dotnet test plugin/ClaudeCAD.Core.Tests` **875** xanh (861 ở `f1e7123` + 14 mới); trợ lý `npm test` **294** xanh
(291 + 3); build net48 / net8.0-windows / net10.0-windows xanh, 0 cảnh báo. Token: gói 3D bớt một ít (mô tả + schema
`match_properties` ngắn hơn ~150 ký tự), gói 2D / Full không đổi.

## Việc cho thread con Test & sửa đợt B

Rà 9 việc trên, đặc biệt: kéo giãn không `handles` (giới hạn 20, layer khóa, đối tượng chỉ chạm khung), khối 3D một phần
trong khung; bẻ gãy đường kín (thứ tự mảnh, điểm ở đầu đường), polyline kín; kéo dài polyline có `Elevation`, cung
`Normal` −Z; bo / vát polyline kín nhiều góc liên tiếp, góc tại đỉnh 0, polyline có đỉnh trùng; bo đường + cung khi cung
quá ngắn phải kéo dài, cung `Normal` −Z; nối có dung sai khi đối tượng kế tiếp là cung / polyline; `match_properties` gói 3D
trong `batch_execute`, `skipped`; nhóm trên layer khóa; macro cũ của 4 lệnh nâng cấp; token (rút gọn mô tả nếu đáng).
