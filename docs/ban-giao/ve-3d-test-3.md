# Bàn giao: Test & sửa 3D đợt 3 (sub-thread "Vẽ 3D đợt 3 › Test & sửa")

*12/11/2026 · thread cha "Vẽ 3D đợt 3" giao. Nhánh `full/ve-3d-test-3`, xuất phát `full/ve-3d-dot-3` @ `e464662`.
Bước 5 trong chuỗi chủ dự án đặt (… → viết đợt 3 → **test & sửa đợt 3** → test tổng thể). Test tổng thể xuất phát từ
commit cuối nhánh này.*

Máy cloud không có AutoCAD: "test" = rà code, đối chiếu tài liệu API AutoCAD .NET (`AcDbMgd.xml` trong gói NuGet
`AutoCAD.NET.Model` 24.1 / 25.0 / 26.0, tức AutoCAD 2022–2027), viết test tự động (nhất là bộ nhận tường / cửa với mặt
bằng lộn xộn), sửa lỗi. Chỗ chỉ thấy được trên AutoCAD thật: viết code an toàn nhất có thể và đưa vào checklist phần L
(L17–L23).

## Đã rà

| Phần | File | Kết quả |
| --- | --- | --- |
| Nhận tường / cửa | `Core/FloorPlan.cs` (`FloorPlanLogic`) | 7 lỗi (bảng dưới, số 2, 4–9). Đã thử: nối chữ T / L / chữ thập, tường đổi dày, góc L đường tim khác dày, nét vượt góc 30 mm, tường lệch 0,2°, hai phòng chung vách có cửa, nét trùng / đứt / hở, hai tường song song cách 400, cửa sổ vẽ bằng nét trên layer tường, cửa chỉ định ở góc / quá đầu tường |
| Đọc mặt bằng, dựng khối | `Drawing/PlanTo3dTools.cs` | 6 lỗi (số 1, 3, 4, 10–13). Chỉ đọc: vẫn đúng (không mở ghi / xóa đối tượng 2D nào, test nguồn). Tường cong, block, xref, polyline bề rộng đổi: bỏ qua có lý do (đúng) |
| Đơn vị | cả hai file | `INSUNITS` mm / cm / dm / m / inch / feet, 0 = mm có cảnh báo: đúng. Ngưỡng nhận nét bề rộng mới cũng đổi theo đơn vị (có test mét) |
| Mặt cắt / hình chiếu | `Drawing/SectionTools.cs`, `SectionLogic` | Mặt phẳng / hướng / khung phải-lên: đúng (test sẵn có). Kết quả đặt cạnh mô hình, không đè. `Section` tạm xóa trong cùng transaction, không gọi lệnh AutoCAD, không hộp thoại. Lỗi 12 (AutoCAD từ chối cắt bị nuốt im) |
| API | `Section(Point3dCollection, Vector3d, Vector3d)`, `GenerateSectionGeometry(Entity, out Array ×5)`, `Solid3d.GetSection(Plane)` | Khớp chữ ký và mô tả ở cả 3 đời (`verticalDir` nằm trên mặt phẳng, vuông góc đường cắt: đúng như plugin truyền) |
| Macro | `Core/Macros.cs` | Tham số handle mới đều được ánh xạ (test sẵn có); `openings` chỉ có điểm, không có handle. Không sửa |
| Giới hạn | `PLAN3D_LIMITS` ↔ `FloorPlanLogic` / `SectionLogic` | Khớp (test so nguồn sẵn có) |
| Chặn theo gói, "Chỉ xem" | `PlanLogic`, `ToolDispatcher` | 3 lệnh nhãn `ThreeD`, là lệnh ghi → gói 2D bị từ chối (lệnh đơn, `batch_execute`, macro), "Chỉ xem" bị chặn kể cả `dry_run`. Không sửa logic gói |
| Skill | `skills/ve-3d-kien-truc`, `agent/src/agent/skills.ts` | `plan: 3d` → máy 2D không nạp, 3D / Full nạp: đúng |
| AutoCAD không phải tiếng Anh, net48 / net8 / net10 | cả 3 file | Không dùng tên lệnh / kiểu tiếng Anh nào ngoài mẫu hatch (ANSI31, đã có L15); số trong câu báo in bằng `InvariantCulture` |
| Self-test | `SelfTest.cs` | Không thấy lỗi; phòng mẫu vẫn cho 4 tường, 1 cửa (đường dẫn code sau sửa không đổi với phòng này, có test cùng dạng) |
| Trợ lý | `agent/src/tools/plan3d.ts`, `systemPrompt.ts` | Mô tả lệnh cập nhật theo lỗi 4, 3, 8 |
| Đợt 1, 2 | test cũ | Vẫn xanh |

## Lỗi tìm được và cách sửa (commit `da50e5c`)

| # | Mức | Lỗi | Sửa |
| --- | --- | --- | --- |
| 1 | **Nặng** | `dry_run` trả báo cáo **không có `skipped` / `skipped_count`** (hai trường chỉ được thêm sau khi đã dựng). System prompt bắt Claude chạy thử, đọc `skipped` rồi hỏi cậu: chạy thử không bao giờ thấy phần bị bỏ qua, dựng xong mới biết | `skipped` nằm trong báo cáo chung, cả khi chạy thử |
| 2 | **Nặng** | Hai tường song song cách nhau 50–600 (hộp kỹ thuật, khe lún, ống gen): hai nét mặt đối diện nhau cũng thành một cặp → **khoảng trống giữa hai tường bị dựng thành tường**, không báo gì | Cặp mà cả hai nét đều là mặt của tường khác ở phía bên kia thì bỏ, báo `skipped` loại `LINE PAIR` "space between two walls" |
| 3 | Vừa | Kiểm "đã dựng rồi" chỉ so với vùng tường: mặt bằng chỉ có sàn chạy lại **tạo sàn trùng** không báo; `dry_run` không nói có khối cũ | Kiểm cả đường bao sàn; `dry_run` trả `existing_solids` + ghi chú để Claude hỏi trước |
| 4 | Vừa | Polyline có bề rộng làm nét in (bề rộng 15–30 trên mặt bằng mm, rất hay gặp) bị coi là **tường theo đường tim dày 20** → mỗi mặt tường thành một tường mỏng, tường thật 220 mất | `FloorPlanLogic.WidePolylineRole`: bề rộng < `min_thickness` = nét thường; > `max_thickness` bỏ qua có lý do; ghi chú số polyline đã đọc như nét |
| 5 | Vừa | Tường đổi dày (200 → 100, chung mặt ngoài): phần nhô của nét dài được thêm vào tường dày → **khối lồi 200 × 100** ra ngoài mặt tường mỏng | Đầu nét ngắn có nét đóng về phía nét dài (bậc đổi dày, má cửa) thì không thêm phần nhô |
| 6 | Vừa | Góc L của hai tường đường tim khác dày (100 + 300): mỗi tường kéo thêm nửa chiều dày **của chính nó** → tường dày lồi (300 − 100)/2 ra ngoài mặt tường mỏng; hai đoạn thẳng hàng khác dày cũng bị kéo | Kéo thêm nửa chiều dày của tường gặp (tới mặt ngoài của nó); đoạn thẳng hàng không kéo |
| 7 | Vừa | Cửa sổ vẽ bằng 2 nét giữa hai mặt tường trên layer tường: các nét thành "tường" nằm trong tường thật → **cửa sổ mất không báo** | Tường nằm trọn trong tường dày hơn thì bỏ, báo từng handle "lying inside wall… a window drawn on the wall layer?" |
| 8 | Vừa | `openings` chỉ định: điểm ở góc / chỗ nối (nằm trong 2 tường) thì **tự chọn tường đầu tiên**; rộng quá đầu tường thì **lanh tô / bậu nằm lơ lửng** ngoài tường | Điểm trong 2 tường → báo lỗi, đề nghị dời điểm; quá đầu tường → báo lỗi; cả lượt không dựng gì |
| 9 | Nhẹ | Cửa chỉ định đè lên hai cửa nhận được chỉ thay một cái, cái kia còn chồng lên | Thay mọi cửa nó đè |
| 10 | Nhẹ | Cùng một đường bao sàn hai lần (vẽ trùng, hoặc trên cả `A-SLAB` và `A-FLOR`) → hai sàn chồng nhau | Giữ một sàn, báo `skipped` "the same outline" |
| 11 | Nhẹ | Không có mảnh tường nào và sàn tắt / không dùng được → lệnh báo **thành công với `handles` rỗng** | `INVALID_PARAMS` "nothing to build", kèm 5 dòng bỏ qua đầu |
| 12 | Nhẹ | AutoCAD từ chối: `GetSection` lỗi bị nuốt (báo "mặt phẳng không cắt khối nào" sai lý do); dựng mảnh tường / nổ vùng cắt lỗi → `ACAD_ERROR` chung | Ghi chú / lỗi nêu khối không cắt được; lỗi hình học → `INVALID_PARAMS` "Nothing was built / drawn" (layer khóa vẫn giữ lỗi riêng); transaction của lượt lùi lại |
| 13 | Nhẹ | Tên layer kết quả sai (`wall_layer_3d` có `<`, `:`…) → lỗi `eInvalidInput` khó hiểu | Kiểm tên trước khi tạo layer, câu lỗi rõ |

Logic gói: **không sửa**. Không đổi `activation/activation.json`, `License.cs`, `.claude/settings.json`. Không thêm lệnh
hay tham số mới (chỉ thêm trường báo cáo `existing_solids` khi chạy thử và câu báo lỗi).

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **304/304** (290 + 14 mới trong `FloorPlanFixTests.cs`: hai tường cạnh
  nhau, tường đổi dày, góc L đường tim 100 + 300, cửa sổ vẽ trên layer tường, nét vượt góc, tường lệch 0,2°, hai phòng chung
  vách có cửa, cửa chỉ định ở góc / quá đầu tường / đè hai cửa, nét bề rộng (mm và mét), kiểm nguồn: `skipped` + khối cũ
  khi chạy thử, kiểm trùng có sàn, sàn trùng, "nothing to build", tên layer, từ chối cắt). **7 test logic mới đỏ trên code
  đợt 3 cũ**, xanh sau khi sửa.
- Trợ lý `npm test`: **185/185** (184 + 1 trong `test/plan3d.test.ts`).
- Build Linux (.NET SDK 10, xóa `obj` / `bin` giữa các lần): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: *(điền sau lượt CI)*.

## Cần chủ dự án thử tay trên AutoCAD (checklist phần L, L17–L23)

| Mã | Bước thử | Kết quả đúng |
| --- | --- | --- |
| L17 | Vẽ 2 tường song song dày 200 (2 nét mỗi tường, đóng đầu) cách nhau 400 trên `A-WALL`, "dựng 3D mặt bằng này" | Claude chạy thử, báo 2 tường 200 và 1 mục bỏ qua "space between two walls"; khối không lấp khe 400 |
| L18 | Tường chung mặt ngoài, đổi dày 200 → 100 giữa chừng (có nét bậc); và 2 polyline có bề rộng 100 và 300 gặp nhau thành góc L | Nhìn Top: khối đúng bằng nét vẽ, không lồi ra ở chỗ đổi dày hay ở góc |
| L19 | Vẽ phòng L2 bằng polyline có bề rộng 25 (nét in) thay cho LINE | Vẫn 4 tường 220, 1 cửa; ghi chú "read as plain wall face lines" |
| L20 | Trong một tường liền, vẽ cửa sổ bằng 2 nét song song giữa hai mặt tường, trên `A-WALL` | Claude báo 2 nét đó bị bỏ qua ("inside wall… window drawn on the wall layer?") và hỏi; không âm thầm mất cửa sổ |
| L21 | Đường bao sàn L7 copy tại chỗ sang layer `A-FLOR`, "dựng 3D có sàn" | Một sàn; báo 1 mục "the same outline" |
| L22 | Sau L2: "chạy thử dựng 3D lại mặt bằng này" | Báo cáo chạy thử có `existing_solids`; Claude hỏi trước khi dựng |
| L23 | "thêm cửa sổ rộng 1200 tại góc phòng" (điểm đúng góc trong) | Claude báo điểm nằm ở chỗ hai tường gặp nhau và hỏi lại; không dựng gì |

L1–L16 của đợt 3 vẫn cần thử như cũ (L9, L11: `GenerateSectionGeometry`; L10: hatch để trống trong vòng).

## Còn chỉ thấy trên AutoCAD thật

Như bàn giao đợt 3 (thứ tự mảng / phía "sau" của `GenerateSectionGeometry`, cung / spline sau `Explode`, thời gian cộng
nhiều hộp). Thêm: `Polyline.ConstantWidth` của polyline có bề rộng 0 ở mọi đoạn trả 0 (đọc như nét thường, đúng ý).

## Tiến độ

- [x] Rà soát + sửa lỗi + test (commit `da50e5c`)
- [x] Tài liệu: file này, checklist L17–L23, `bo-cai/README.md`, kế hoạch
- [ ] CI Windows (1 lượt)
