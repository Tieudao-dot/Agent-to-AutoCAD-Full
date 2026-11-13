# Bàn giao: Nâng cấp đợt E (cầu thang, mái)

*13/11/2026 · thread Nâng cấp đợt E · nhánh `full/nang-cap-e` (xuất phát `full/nang-cap-d-test` @ `db34801` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`; `full/test-tong-the` @ `19944df` đã có sẵn). **Chưa thử trên AutoCAD thật.**
Thử tay: mục **R** của [`checklist-thu-that.md`](../checklist-thu-that.md).*

## Đã làm (nhãn gói 3D / Full)

| # | Việc | Lệnh | Ở đâu | Ghi chú |
| --- | --- | --- | --- | --- |
| E1 | **Cầu thang** 1 vế / chữ L / chữ U | `build_stair` (mới) | `Drawing/StairRoofTools.cs` (`BuildStair`, `MakePrism`, `UnitePrisms`); `Core/StairRoof.cs` (`StairLogic`, `Prism3`) | `height` = cao tầng (bắt buộc), chia đều thành **số bậc nguyên gần cao bậc mặc định nhất** (`round(height / riser)`, ít nhất 2; vd 3300 → 21 × 157,1; 3000 → 19 × 157,9), báo `risers`, `riser_mm`. Mặc định đã chốt: cao bậc **160**, rộng bậc **280**, vế rộng **1000**, chiếu nghỉ rộng bằng vế. Thêm (tự chọn, ghi rõ): bản thang + chiếu nghỉ dày **150** (`waist`), khe giữa 2 vế chữ U **100** (`gap`). L / U: vế 1 = ⌈n/2⌉ bậc (đổi bằng `first_flight_risers`), chiếu nghỉ ở cao độ đó. Mỗi vế là một khối đùn từ biên dạng đứng (răng cưa bậc + bản thang nghiêng, cắt bằng ở sàn dưới và đáy chiếu nghỉ / sàn trên), các vế ăn 100 mm vào chiếu nghỉ để cộng thành **một khối**; bậc trên cùng ăn 100 mm lên sàn trên (dựng cả cổ bậc cuối). Đặt: `start` (giữa mép dưới bậc đầu) + `direction` (độ) + `turn`; hoặc ô cầu thang `well_handle` / `well_points`: không có `start` thì thử từng góc vuông của ô theo hai cạnh (cạnh dài trước), giữ `turn` nếu khách nói; có `start` thì kiểm vừa ô. **Không vừa → lỗi**, báo kích thước cần và phần nào thò ra, không dựng. Lan can `railing` left / right / both (theo chiều đi lên): tay vịn 50 × 50 cao **900** trên đường mũi bậc + trụ 50 × 50 trên tâm bậc cách ≤ 1500, dọc các vế (chiếu nghỉ chưa có: dùng `build_railing`), khối riêng trên `A-RAIL-3D`. Cảnh báo: 2 × cao + rộng ngoài 550–700, vế quá 18 bậc. Layer `A-STAIR-3D` (màu 6) |
| E2 | **Mái** bằng / 2 dốc / 4 dốc | `build_roof` (mới) | `StairRoofTools.cs` (`BuildRoof`, `WallBoxes`); `Core/StairRoof.cs` (`RoofLogic`) | Đường bao = mặt ngoài tường: `outline_handle` (polyline kín, đoạn thẳng), `outline_points`, hoặc `from_walls: true` (chữ nhật bao các khối tường). Đỉnh tường: `wall_top_z`, mặc định **z cao nhất của khối tường** (`wall_layers`, mặc định `A-WALL-3D`) chồng hộp bao với đường bao → tầng trên cùng; không có tường thì cao độ đường bao (báo `wall_top_from`). Mặc định đã chốt: mái bằng dày **150** (không đua, trừ khi nói `overhang`); mái dốc **30°**, đua **600**; bản mái dốc cũng dày 150 (đo vuông góc mặt mái). Mái dốc = (lăng trụ đường bao đua ∩ các nêm dưới mặt trên) − (∩ các nêm dưới mặt dưới): mặt dưới chạm đỉnh tường đúng ở mép ngoài tường, đầu đua cắt đứng. **2 dốc** chỉ trên hình chữ nhật (nóc theo cạnh dài, `ridge: short` để đổi), hai đầu hồi để hở; **4 dốc** trên đường bao lồi (chữ nhật, đa giác lồi); chữ L / T / U → **lỗi rõ** (chia hình chữ nhật hoặc mái bằng), không dựng sai. Mái bằng nhận mọi đường bao kín (đua theo góc vát). Báo `eave_z_mm`, `top_z_mm`, `ridge_length_m`, `plan_area_m2`, `roof_area_m2` (mặt nghiêng). Layer `A-ROOF-3D` (màu 30, như skill) |

**Gói (phương án A):** 2 lệnh mới nhãn **ThreeD** (`Core/Plans.cs`): không có chữ / kích thước / hatch / bảng. Chỉ dựng
khối mới, chỉ đọc ô cầu thang / đường bao → **không cần thẻ xác nhận**; khối lan can cũng nằm trong `handles` (tính là
"đã tạo trong lượt"). Nhãn tiếng Việt: "Dựng cầu thang", "Dựng mái" (`UI/ToolLabels.cs`).

Kèm theo:

- Thời gian chờ 120 giây cho `build_stair`, `build_roof` (`agent/src/ipc/rpc.ts`).
- System prompt gói 3D / Full: thêm 1 câu (~90 ký tự) "Stairs: build_stair (height = storey height). Roofs: build_roof on the
  top storey walls."
- Skill `ve-3d-kien-truc`: cách dùng cầu thang / mái (số mặc định, ô cầu thang, nhà chữ L), bảng chọn lệnh, layer.
- `CLAUDECHATSELFTEST`: bước "Nâng cấp đợt E: cầu thang 1 vế + chữ U (chia bậc theo cao tầng), mái 4 dốc" (thang 1 vế
  3000 = 19 bậc, thể tích đúng bằng tính tay, đỉnh ở 3000; chữ U một khối + lan can, chiếu nghỉ ở 11 bậc; mái 4 dốc z
  từ mép đua tới đỉnh đúng, thể tích ≈ diện tích mặt mái × 150 ±10 %).

## Giữ đúng luật đã chốt

- Test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, `Every_3d_tool_is_labelled_ThreeD…`, trợ lý
  `crossplan.test.ts` có cả lớp thẻ xác nhận) qua với 2 lệnh mới; số đếm sửa 35 → 37 nhãn 3D, 29 → 31 lệnh 3D Claude thấy.
- Hoàn tác: lệnh ghi chạy trong transaction + nhóm undo của lượt; U một lần bỏ cả lượt.
- Không đổi lệnh / tên cũ; hành vi 3D cũ không đụng (chỉ thêm file và đăng ký).
- Đơn vị: logic tính bằng mm, plugin chia theo INSUNITS (bản vẽ mét: tọa độ mét, số đo vẫn mm).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **702** test (675 trước). `NangCapETests.cs` (27, tính cả từng trường hợp theory): nhãn / đăng ký / nhãn
  tiếng Việt / bước tự kiểm; số bậc (5 trường hợp), số mặc định, số sai bị từ chối, bí danh kiểu; thang 1 vế (từng bậc
  đúng cao, bản thang dày 150 vuông góc, cổ bậc cuối, cảnh báo 21 bậc), chữ L (chiếu nghỉ vuông, vế 2 rẽ, các mảnh chồng
  nhau), chữ U (khe giữa hai vế, chiếu nghỉ phủ cả khe), đặt theo điểm + hướng + rẽ trái / phải (lật hình), thể tích giữ
  nguyên khi lật, ô cầu thang (vừa / không vừa / chạy theo cạnh dài / `start` thò ra), lan can trái / phải / hai bên; mái
  bằng (thể tích, đua), 4 dốc chữ nhật (cao độ mép đua, đỉnh, nóc dài 4000, mặt dưới chạm đỉnh tường, 400 điểm ngẫu nhiên
  so với công thức), 2 dốc (nóc theo cạnh dài / ngắn), 4 dốc lục giác, chữ L bị từ chối (4 dốc, 2 dốc), mái bằng chữ L
  (diện tích đua), dốc 80° và đường bao tự cắt bị từ chối; kiểm mã nguồn phần AutoCAD.
- Trợ lý: `cd agent && npm ci && npm test` — **286** test (281 trước). `test/nangcape.test.ts` (5): nhãn / gói 3D, 2D thấy
  gì, schema 3 kiểu thang + 3 kiểu mái, số mặc định khớp plugin và có trong mô tả, thời gian chờ, câu system prompt, không
  qua thẻ + khối mới là "đã tạo".
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: run **43461186495 xanh** ở `f4a4780` (`release: "false"`).

## Token mô tả lệnh + system prompt (~3,5 ký tự / token)

Cách đo: tên + mô tả + schema JSON (`z.toJSONSchema`) của 2 lệnh mới + câu thêm vào system prompt, cộng vào số "sau" của
đợt D (`nang-cap-d.md`) + ~25 ký tự của Test & sửa đợt D. (Không chạy lại toàn bộ script đo của đợt D vì script không nằm
trong repo; phần thêm được đo đúng cách cũ.)

| Gói | Số lệnh | Ký tự trước | Phần thêm | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- | --- |
| 2D | 105 → 105 | 135 344 | 0 | 135 344 | ~38,7k (không đổi) |
| 3D | 91 → 93 | ~133 585 | `build_stair` 2 843 + `build_roof` 2 011 + prompt ~90 | ~138 530 | ~38,2k → ~39,6k (**+1,4k**) |
| Full | 134 → 136 | ~196 284 | như gói 3D | ~201 230 | ~56,1k → ~57,5k (**+1,4k**) |

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật. Phần cần AutoCAD: hướng đùn của region (code tự dời về z ≥ 0 rồi xoay bằng
  `AlignCoordinateSystem` vào khung thuận tay phải, không dùng ma trận lật), cộng các vế + chiếu nghỉ (chồng 100 mm), giao /
  trừ khối của mái (nêm rất lớn so với nhà), bản vẽ đơn vị mét.
- Cầu thang: vế thẳng, bậc đều; chiếu nghỉ của chữ L vuông `landing` × vế (≥ bề rộng vế); không có thang xoắn, bậc rẻ quạt,
  chiếu tới. Ô cầu thang: chỉ thử các góc vuông của ô (ô hình lạ: đưa `start`). Lan can chỉ dọc các vế, không có song chắn.
  Không tự khoét lỗ sàn tầng trên cho ô cầu thang (dùng `presspull` / `solid_subtract`, qua thẻ).
- Mái: 4 dốc chỉ đường bao lồi, 2 dốc chỉ chữ nhật; không có tường hồi, máng xối, mái thu hồi. `from_walls` lấy hình chữ
  nhật bao tường (nhà không chữ nhật: vẽ đường bao). Đỉnh tường lấy theo z cao nhất của khối tường chồng hộp bao (nhà lệch
  tầng: đưa `wall_top_z`).
- Token: đo phần thêm, không đo lại toàn bộ (xem trên).

## Việc cho thread con Test & sửa đợt E

Rà 2 lệnh: số bậc ở các cao tầng biên (2700, 3600, 4500; cao bậc thực ngoài 100–250), `first_flight_risers` biên, chữ U
vế 2 dài hơn vế 1 (`MinX` âm), ô cầu thang xoay chéo / không chữ nhật, `start` + ô, turn tự chọn theo ô và phía lan can,
bản vẽ mét; mái: đường bao điểm trùng / thẳng hàng, chiều kim đồng hồ, `overhang` 0, dốc 5° / 60°, đỉnh tường khi có nhiều
tầng / không có tường, `from_walls` với tường + cột; nêm quá lớn so với nhà nhỏ; hoàn tác U; gói 2D không thấy lệnh.

## Commit

`f4a4780` (code 2 lệnh + test + tài liệu, CI 43461186495 xanh), sau đó một commit ghi kết quả CI vào file này (chỉ tài liệu; CI của thread con phủ nó).

## Sửa sau kiểm tra độc lập nâng cấp

*13/11/2026 · lỗi từ [`kiem-tra-doc-lap-nang-cap.md`](kiem-tra-doc-lap-nang-cap.md) (nhánh `full/kiem-tra-doc-lap-2` @
`8e6fede`); đã gộp nhánh tích hợp `full/nang-cap-tong-the` @ `f1e7123` trước khi sửa. Không chạy CI (thread "Test tổng thể
nâng cấp" gộp và chạy CI chung).*

| Lỗi | Mức | Sửa | Ở đâu | Test |
| --- | --- | --- | --- | --- |
| KT2-05 | vừa | Kiểm "cầu thang vừa ô" chỉ thử 16 điểm trên cạnh mỗi vế / chiếu nghỉ, bỏ sót chỗ lõm (cột, góc tường) nằm giữa hai điểm thử → dựng xuyên cột. Thêm `WellCutsInto`: đỉnh ô nằm hẳn trong một phần của thang, hoặc cạnh ô cắt cạnh phần đó (so với phần thu nhỏ 1 mm, nên ô trùng mép thang vẫn vừa) → không vừa. Dùng chung cho đặt theo `start` và tự đặt vừa ô | `Core/StairRoof.cs` (`Outside`, `WellCutsInto`; `RoofLogic.SegCross` thành public) | `NangCapEKiemTraTests` (ô có chỗ lõm 200 × 200 giữa hai điểm thử: không vừa; dời ra: vừa; tự đặt không xuyên chỗ lõm; đỉnh trong / trùng mép / lệch 0,5 mm) |
| KT2-08 | nhẹ | Theo lựa chọn của luồng chính (cách 1): không làm mái liền; lời lỗi mái 4 dốc / 2 dốc trên nhà chữ L / T, mô tả lệnh `build_roof` và skill dặn Claude **báo trước** khách: chia hình chữ nhật thì hai mái chồng nhau ở chỗ nối, cần sửa tay, bảng khối lượng tính phần chồng hai lần. Mái liền để đợt F | `Core/StairRoof.cs` (`RoofLogic.Build`), `agent/src/tools/solids3.ts`, `skills/ve-3d-kien-truc/SKILL.md` | `NangCapEKiemTraTests` (lời lỗi cả hai kiểu, skill, mô tả) |
| KT2-09 | nhẹ | (a) Lỗi mái 4 dốc đường bao lõm không còn gợi ý "type gable" (gable cũng đòi chữ nhật): gợi ý mái bằng / chia hình chữ nhật. (b) `landing` thống nhất 600–10000 mm ở plugin (`MinLanding`, `MaxLanding`) và schema trợ lý (trước: schema 500, plugin min(vế, 600)) | `Core/StairRoof.cs`, `agent/src/tools/solids3.ts` | `NangCapEKiemTraTests`; trợ lý `nangcape.test.ts` (550 bị chặn, 600 qua, hằng số khớp) |

Test: lõi **865** (861 sau khi gộp + 4), trợ lý **291**, build net48 / net8.0-windows / net10.0-windows 0 cảnh báo. Token: mô
tả `build_roof` thêm ~70 ký tự (gói 3D / Full).
