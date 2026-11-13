# Bàn giao: Test & sửa Nâng cấp đợt B (thread con của "Nâng cấp đợt B")

*13/11/2026 · thread cha "Nâng cấp đợt B" giao. Nhánh `full/nang-cap-b-test`, xuất phát `full/nang-cap-b` @ `81eaf7c`.
**Chưa thử trên AutoCAD thật.** Thử tay: mục **O** của [`checklist-thu-that.md`](../checklist-thu-that.md)
(thêm O17–O20 ở bước này).*

Máy cloud không có AutoCAD: "test" = rà code 9 việc của đợt B, viết test tự động, sửa lỗi, build 3 bản, 1 lượt CI.

## Đã rà

| Phần | File | Kết quả |
| --- | --- | --- |
| B1 STRETCH | `Edit2DTools.cs` (`StretchEntities`), `Edit2DLogic.StretchWindow / InPolygon / Classify`, `confirmation.ts` | **1 lỗi (NB-1)**. Khung hai góc theo thứ tự bất kỳ, đa giác, cạnh khung tính là trong, layer khóa / đóng băng bỏ qua, khối 3D một phần trong khung bỏ qua: đúng. Nêu `handles` trên layer khóa: báo lỗi, cả lượt quay lại (transaction) |
| B2 BREAK | `Edit2DLogic.Break`, `BreakEntity` | Không lỗi. Đường kín: điểm ở chỗ nối đầu / cuối (tham số 0 hay 2π), polyline kín đi qua đỉnh 0: đúng mảnh bị bỏ |
| B3 LENGTHEN | `NewLength`, `LengthenSegment / Arc / Bulge`, `LengthenEntity` | Không lỗi. Cung bulge âm (chiều kim đồng hồ) kéo ở đầu: giữ tâm, bán kính. Cung pháp tuyến −Z: góc OCS và `EndPoint` khớp nhau nên chọn đầu đúng |
| B4 DIVIDE / MEASURE | `Stations`, `DivideEntity` | Không lỗi. Bội số đúng chiều dài không đặt mốc ở đầu cuối; đường kín đặt từ đầu; 10 000 mốc là giới hạn |
| B5 GROUP | `GroupEntities` | Không lỗi. Đối tượng trên layer khóa: AutoCAD báo khóa, dispatcher đổi thành câu báo layer khóa có hướng dẫn (cần thử tay) |
| B6 MATCHPROP | `MatchProps`, `PlanLogic.TextValues / BlockedParam / BlockedCalls`, `MatchProperties` | Không lỗi. Gói 3D: mặc định bỏ chữ / kích thước (báo `skipped`), nêu rõ thì bị chặn, cả trong `batch_execute` (nhãn "thuộc tính chữ / kích thước"), chữ hoa / thường đều bị bắt |
| B7 bo / vát polyline, cung | `PolylineCorners`, `FilletArcs`, `ChamferArcs`, `ModifyTools.cs` (`PrimOf`, `ApplyPrim`) | Không lỗi. Cung ngắn phải kéo dài tới tiếp điểm, đường cắt qua cung, vát đo dọc cung, hình vuông bo hết cạnh (không đỉnh trùng), cạnh ngắn bỏ qua có lý do, đỉnh đầu của polyline hở nêu bằng `vertex` thì báo lỗi: đúng. Cung pháp tuyến −Z: phép đổi góc đúng; **tách ra `Edit2DLogic.WcsArcAngles` để có test** (trước chỉ build được) |
| B8 offset hai phía | `OffsetEntity`, `CheckOffsetSides` | Không lỗi. Gọi cũ có `side_point` đi đúng đường cũ |
| B9 nối có dung sai | `JoinEntities`, `TryBridge`, `Edit2DLogic.Bridge` | **1 lỗi (NB-2)**. Trả lại bulge khi nối khe thất bại, polyline kín không nối thêm: đúng |
| Nhãn gói | `Plans.cs`, test bắt buộc nhãn | GROUP, MATCHPROP mọi gói; 4 lệnh mới 2D / Full; test bắt buộc nhãn (lõi + trợ lý) xanh |
| Luật xác nhận | `confirmation.ts` | 2D: đúng, trừ NB-1. Thẻ 3D: đúng, trừ NB-1 (khối 3D bị kéo giãn theo khung bị coi là "khối mới của lượt") |
| Hoàn tác U | dispatcher | Mọi lệnh mới ghi trong transaction + nhóm undo của lượt: đúng |
| Macro cũ | `Macros.cs` | `fillet_entities`, `chamfer_entities`, `offset_entity`, `join_entities` giữ tên + tham số cũ; `source` của `match_properties` được ánh xạ handle; không lệnh cũ nào có tham số tên `source` (không ánh xạ nhầm) |
| Lệnh 2D cũ | test cũ | Vẫn xanh. Bo / vát hai đường thẳng đi đúng `Geometry.FilletLines / ChamferLines` cũ |

## Lỗi tìm được và cách sửa

| # | Lỗi | Hậu quả với người dùng | Sửa |
| --- | --- | --- | --- |
| NB-1 | `stretch_entities` không nêu `handles` (kéo giãn theo khung) không được trợ lý tính vào số đối tượng có sẵn bị sửa trong lượt; plugin chỉ chặn trên 20 **cho riêng lần gọi đó**. Kết quả của nó còn bị tính là "đối tượng mới tạo trong lượt" | (1) Một lượt kéo giãn hai khung 20 + 20, hoặc kéo giãn 18 rồi xóa 5, **sửa quá 20 đối tượng mà không hỏi** (lách luật đã chốt). (2) Khối 3D có sẵn bị kéo giãn (dời) theo khung thành "khối mới", nên sau đó cộng / trừ / xóa khối đó **không cần thẻ xác nhận** (lách luật thẻ 3D) | Trợ lý gửi kèm `max_unasked` = 20 − số đối tượng lượt đã sửa (chia đều cho các lần kéo giãn khung trong một `batch_execute`; Claude không tự đặt được, macro không ghi). Plugin chặn khi khung sửa nhiều hơn số đó và liệt kê handle để Claude hỏi. Sau lần gọi, các đối tượng khung đã sửa được cộng vào số của lượt và không bao giờ là "mới tạo" (`confirmation.ts` `windowChanged`, `session.ts` `noteResult`, `Edit2DLogic.WindowLimit`) |
| NB-2 | `join_entities` có `tolerance`: khe được nối ngay khi một đối tượng chưa nối khít được, kể cả lúc còn đối tượng khác chạm khít đúng đầu đó | Thứ tự handle khác nhau thì kết quả khác: đoạn nối khe có thể chiếm đầu polyline, đoạn chạm khít bị bỏ sót (`not_joined`) hoặc thêm đoạn nối khe thừa | Chỉ nối khe khi một lượt quét không còn gì nối khít được, mỗi lần một khe, khe nhỏ nhất trước (`ModifyTools.cs` `JoinEntities`, `BridgeGap`) |

Commit: `—` (sửa + test + tài liệu), sau đó một commit ghi kết quả CI vào file này.

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **551** test (531 trước bước này). `NangCapBTestSuaTests.cs` (20): giới hạn
  khung (mặc định 20, kẹp 0–20, câu báo), plugin dùng giới hạn của trợ lý, macro không ghi `max_unasked`; nối khe chỉ sau lượt
  nối khít, khe nhỏ nhất trước; chọn cặp đầu gần nhất; đổi góc cung pháp tuyến −Z (hai chiều) và bo với cung đó ra đúng như
  cung +Z; bo kéo dài cung ngắn; đường cắt qua cung; vát đường + cung; polyline kín bo hết cạnh, cạnh ngắn, đỉnh đầu polyline
  hở; bẻ gãy ở chỗ nối của đường kín; kéo dài bulge âm; 3 chế độ độ dài; mốc với bội số đúng; khung và đa giác; MATCHPROP theo
  gói trong `batch_execute`. Một test cũ (`NangCapBTests`, kiểm mã nguồn kéo giãn) cập nhật theo biểu thức mới, vẫn kiểm giới
  hạn 20.
- Trợ lý: `cd agent && npm ci && npm test` — **259** test (253 trước). `test/nangcapb-testsua.test.ts` (6): giới hạn gửi kèm theo
  số đã sửa, Claude không đặt được, sau khi đồng ý dùng giới hạn của plugin, chia trong `batch_execute`, kết quả khung không
  thành "mới tạo" (thẻ 3D vẫn hỏi), phiên thật: kéo giãn 18 rồi xóa 5 thì hỏi.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, .NET SDK 10 cài từ kho Ubuntu):
  xanh, 0 cảnh báo.
- CI: *(điền sau khi chạy)*.

## Token

Không đổi mô tả lệnh hay system prompt (tham số `max_unasked` không nằm trong schema Claude thấy). Rút gọn mô tả lệnh
đợt B (~1–2k ký tự có thể bớt): **chưa làm**, không đáng rủi ro ở bước sửa lỗi; để đợt sau nếu cần.

## Giới hạn còn lại

- Kéo giãn theo khung đếm cả đối tượng vừa vẽ trong lượt vào giới hạn của plugin (plugin không biết đối tượng nào mới).
  Hậu quả nhỏ: plugin từ chối, Claude gọi lại với `handles`, trợ lý không tính đối tượng mới nên không phải hỏi người dùng.
- Polyline kín có đỉnh cuối trùng đỉnh đầu (một số phần mềm vẽ như vậy): góc ở đỉnh đó bị bỏ qua "zero-length segment"
  khi bo / vát mọi góc. Gợi ý: `edit_polyline` bỏ đỉnh trùng trước.
- Các giới hạn của bàn giao đợt B giữ nguyên (stretch chỉ quét Model, bẻ gãy đường kín ở điểm đầu, bo đường + cung nhiều
  giao điểm, độ rộng polyline không đều, thuộc tính block khi chia, MATCHPROP chưa chép hatch / bảng / multileader).

## Việc còn phải thử tay (mục O của checklist)

Quan trọng nhất: **O0** (self-test), **O1, O4, O6, O10, O11, O14** (đợt B) và **O17** (NB-1: hai lần kéo giãn trong một câu
quá 20 thì hỏi). O18 (khối 3D bị kéo giãn vẫn cần thẻ), O19 (nối khe đúng 1 chỗ), O20 (bo với cung "vẽ ngược").
