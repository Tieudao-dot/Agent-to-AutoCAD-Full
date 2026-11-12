# Kế hoạch: vẽ 3D và gói dịch vụ theo máy

*12/11/2026 · luồng chính nghiên cứu · chưa code, chờ cậu duyệt*

## Tóm tắt

- **Vẽ 3D làm được.** AutoCAD bản đầy đủ có sẵn bộ lệnh 3D trong .NET API: khối đặc `Solid3d`, extrude, revolve, sweep,
  loft, cộng / trừ / giao khối, cắt khối, tính thể tích. Các lệnh này có ở mọi bản 2022–2027 và ở cả 3 bản build
  .NET 4.8 / 8 / 10, nên không phải đổi gì ở bộ cài.
- Hiện plugin có 89 lệnh, gần như toàn bộ là 2D. Điểm đã nhận tọa độ z nhưng **chưa có lệnh 3D nào**. Phải thêm ~25 lệnh
  mới, chia 3 đợt.
- **Gói dịch vụ làm được, và dựa trên cơ chế có sẵn.**
  - Thêm cột **"Gói"** vào trang "Thiết bị" trong Google Sheet, chọn `2D` / `3D` / `Full`.
  - Máy chủ ký số giá trị gói giống như cách đang ký trạng thái Active / Disabled.
  - Plugin chặn những lệnh không thuộc gói, chặn ngay tại chỗ đã dùng cho chế độ "Chỉ xem". Chỗ này cũng là nơi mọi lệnh,
    batch và macro phải đi qua.
- **Thứ tự đề xuất:**
  1. Gói dịch vụ trước, vì nó nhỏ và là nền để chia lệnh theo gói.
  2. Sau đó tới 3D đợt 1.
  3. Làm sau khi phát hành 0.9.5.
  4. Phát hành chung thành **0.10.0**.

## Phần 1 — Plugin vẽ 3D

> **Đợt 1: đã làm (12/11/2026, nhánh `full/ve-3d`, chưa thử trên AutoCAD thật).** 14 lệnh nhãn 3D: `draw_box`,
> `draw_cylinder` (cả nón), `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid`, `extrude`, `revolve`,
> `solid_union`, `solid_subtract`, `solid_intersect`, `set_view_3d`, `set_visual_style`, `get_solid_info`;
> `capture_view` thêm `view` / `direction` / `visual_style` (vẫn nhóm Chung). Chi tiết và việc còn lại:
> [`ban-giao/ve-3d-dot-1.md`](ban-giao/ve-3d-dot-1.md). Thử thật: phần J của `checklist-thu-that.md`.
>
> **Đợt 2: đã viết (12/11/2026, nhánh `full/ve-3d-dot-2`, chưa thử trên AutoCAD thật, chờ thread Test & sửa đợt 2).**
> 12 lệnh nhãn 3D: `sweep`, `loft`, `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid`, `rotate_3d`,
> `mirror_3d`, `align`, `set_ucs`, `reset_ucs`, `check_interference`; `get_solid_info` thêm danh sách cạnh / mặt có số
> thứ tự và `topology_id`. Chi tiết: [`ban-giao/ve-3d-dot-2.md`](ban-giao/ve-3d-dot-2.md). Thử thật: phần K.
> **Test & sửa đợt 2: xong (12/11/2026, nhánh `full/ve-3d-test-2`)**, 6 lỗi đã sửa: [`ban-giao/ve-3d-test-2.md`](ban-giao/ve-3d-test-2.md).
> Thử thật thêm K17–K21.
>
> **Đợt 3: đã viết (12/11/2026, nhánh `full/ve-3d-dot-3`, chưa thử trên AutoCAD thật, chờ thread Test & sửa đợt 3).**
> 3 lệnh nhãn 3D: `build_3d_from_plan` (dựng tường có lỗ cửa đi / cửa sổ và sàn từ mặt bằng 2D theo layer của
> `ve-kien-truc` hoặc handle chỉ định, báo cáo phần bỏ qua, chạy thử `dry_run`, chặn dựng trùng), `section_view` (mặt
> cắt: đường bao + hatch, tùy chọn nét phía sau), `projection_view` (hình chiếu / mặt đứng); skill mẫu `ve-3d-kien-truc`
> (máy gói 2D không nạp). Toàn bộ bằng API .NET (`Solid3d.GetSection`, `Section.GenerateSectionGeometry`), **không gọi
> lệnh AutoCAD** (FLATSHOT / VIEWBASE). Chi tiết: [`ban-giao/ve-3d-dot-3.md`](ban-giao/ve-3d-dot-3.md). Thử thật: phần L.
>
> **Test & sửa đợt 3: xong (12/11/2026, nhánh `full/ve-3d-test-3`)**, 13 lỗi đã sửa: [`ban-giao/ve-3d-test-3.md`](ban-giao/ve-3d-test-3.md). Thử thật thêm L17–L23.

### Hiện trạng

- Tọa độ điểm đã nhận `[x, y, z]`. Polyline có cao độ (elevation). Plugin đọc được polyline 3D có sẵn trong bản vẽ.
- Plugin chưa tạo được khối đặc hay mặt, không đặt được góc nhìn 3D, không đổi được kiểu hiển thị, không đổi được UCS.
- Lệnh chụp ảnh để Claude tự kiểm tra đã chụp theo hướng nhìn hiện tại. Phần này dùng lại được cho 3D.

### Lệnh mới đề xuất

| Đợt | Nhóm | Lệnh (tên dự kiến) | API AutoCAD .NET |
| --- | --- | --- | --- |
| 1 | Khối cơ bản | `draw_box`, `draw_cylinder` (cả hình nón), `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid` | `Solid3d.CreateBox / CreateFrustum / CreateSphere / CreateWedge / CreateTorus / CreatePyramid` |
| 1 | Từ biên dạng 2D | `extrude` (theo chiều cao, có góc vát), `revolve` (quanh trục) | `CreateExtrudedSolid`, `CreateRevolvedSolid`, `Region.CreateFromCurves` |
| 1 | Cộng trừ khối | `solid_union`, `solid_subtract`, `solid_intersect` | `Solid3d.BooleanOperation` |
| 1 | Góc nhìn | `set_view_3d`: trên, trước, bên, 4 góc iso; `set_visual_style`: 2D Wireframe / Conceptual / Shaded / Realistic | `ViewTableRecord.ViewDirection`, `VisualStyleId` |
| 1 | Hỏi khối | `get_solid_info`: thể tích, trọng tâm, hộp bao, số mặt / cạnh | `MassProperties`, `GeometricExtents`, Brep |
| 1 | Tự kiểm tra | Thêm vào lệnh chụp ảnh có sẵn: tùy chọn hướng nhìn + kiểu hiển thị, để Claude soát khối vừa dựng | Lệnh chụp hiện có |
| 2 | Biên dạng nâng cao | `sweep` (theo đường dẫn), `loft` (qua nhiều tiết diện) | `CreateSweptSolid`, `CreateLoftedSolid` |
| 2 | Sửa khối | `slice_solid` (cắt bằng mặt phẳng), `fillet_edges` / `chamfer_edges`, `shell_solid` | `Slice`, `FilletEdges`, `ChamferEdges` |
| 2 | Biến đổi 3D | `rotate_3d` (quanh trục bất kỳ), `mirror_3d`, `align` | `Matrix3d.Rotation` / `Mirroring` |
| 2 | UCS | `set_ucs` (mặt phẳng làm việc), `reset_ucs` | `Editor.CurrentUserCoordinateSystem` |
| 2 | Kiểm tra | `check_interference` (va chạm giữa các khối) | `Solid3d.CheckInterference` |
| 3 | Ứng dụng | `build_3d_from_plan` (dựng 3D từ mặt bằng 2D), mặt cắt / hình chiếu 2D từ khối 3D | Ghép từ các lệnh trên; `SectionPlane`, lệnh FLATSHOT / VIEWBASE |

### `build_3d_from_plan`

Đây là việc người dùng kiến trúc cần nhất. Ví dụ: "dựng 3D mặt bằng này, tường cao 3,3 m".

1. Plugin đọc tường, cửa và sàn từ layer của skill.
2. Extrude tường lên chiều cao đã cho, rồi trừ đi lỗ cửa.
3. Đổ sàn.
4. Đặt góc nhìn iso và chụp ảnh để Claude soát lại.

Lệnh này dựa trên đợt 1 nên làm sau cùng.

### Khó và cách xử lý

- **Claude khó chỉ ra cạnh / mặt của khối để bo góc hay vát.**
  - `get_solid_info` trả danh sách cạnh kèm số thứ tự và tọa độ.
  - Lệnh bo / vát nhận số thứ tự đó, hoặc nhận "mọi cạnh song song trục Z", "mọi cạnh trên mặt trên".
- **Claude khó tự kiểm tra hình 3D.**
  - Sau mỗi việc dựng lớn, Claude chụp góc iso ở kiểu Conceptual và so với yêu cầu.
  - Claude kiểm thêm bằng số: thể tích, hộp bao. Ví dụ tường 4 m × 0,22 m × 3,3 m thì thể tích phải là ~2,9 m³.
- **Cộng / trừ khối làm mất khối gốc.**
  - Khi trừ khối có sẵn của người dùng, lệnh tính là sửa đối tượng có sẵn, nên phải qua thẻ xác nhận.
  - Hoàn tác cả lượt bằng một lần U vẫn giữ như cũ.
- **Mặt cắt / hình chiếu 2D (đợt 3)**: *(đã làm bằng API .NET, xem bàn giao đợt 3)* vết cắt bằng `Solid3d.GetSection`,
  nét phía sau / hình chiếu bằng `Section.GenerateSectionGeometry` (đối tượng `Section` tạm, xóa trong cùng transaction);
  không gọi FLATSHOT / VIEWBASE. Phần `GenerateSectionGeometry` vẫn cần thử kỹ trên AutoCAD thật (checklist L9, L11).
- **Token:**
  - ~25 lệnh 3D thêm khoảng 5–6k token mô tả lệnh vào mỗi yêu cầu. Phần này được cache nên rẻ, nhưng vẫn tốn.
  - Máy gói 2D sẽ không nạp lệnh 3D, nên không tốn thêm.
- **Kiểm thử:**
  - CI không có AutoCAD. Phần tính toán (hướng nhìn, trục quay, kiểm tham số) được test tự động.
  - Phần dựng khối được thêm vào lệnh tự kiểm tra `CLAUDECHATSELFTEST`: dựng hộp → trừ trụ → kiểm thể tích. Lệnh này
    chạy được trên máy cậu.
  - Thêm phần "3D" vào `checklist-thu-that.md`.

### Hướng dẫn cho Claude

- **System prompt** thêm quy ước 3D:
  - Z hướng lên, đơn vị mm.
  - Vẽ biên dạng trên mặt phẳng XY rồi extrude.
  - Mỗi loại khối đặt một layer riêng.
  - Dựng xong thì đặt góc iso và tự kiểm tra.
- **Skill mẫu "3D kiến trúc":** chiều cao tầng, dày sàn, layer khối (A-WALL-3D, A-SLAB-3D…). Skill mẫu "3D cơ khí" để
  sau, nếu cậu cần.

## Phần 2 — Gói dịch vụ theo máy (2D / 3D / Full)

### Cơ chế có sẵn

- Mỗi máy là một dòng trong trang "Thiết bị", và cột Trạng thái đặt Active / Disabled / Chờ duyệt.
- Máy chủ trả về dòng trạng thái có chữ ký số `CCS1|máy|trạng thái|ngày|chữ ký`. Plugin chỉ tin dòng có chữ ký đúng.
- Plugin kiểm tra trạng thái khi mở AutoCAD và **6 giờ một lần**, hoặc ngay khi bấm "Kiểm tra lại".
- Nếu quá 7 ngày không kiểm tra được với máy chủ, plugin yêu cầu kết nối mạng.

### Thay đổi

| Phần | Việc |
| --- | --- |
| Google Sheet | Thêm cột **"Gói"** có ô chọn `2D` / `3D` / `Full`. Hàm `caiDat()` tự thêm cột vào trang có sẵn. Email "máy mới xin kích hoạt" ghi thêm dòng nhắc chọn gói |
| Máy chủ (Apps Script) | Trả thêm một dòng ký số mới `CCF1\|máy\|gói\|ngày\|chữ ký`. Dòng `CCS1` cũ giữ nguyên, nên máy cài bản cũ không bị ảnh hưởng |
| Plugin (lõi) | • Đọc và kiểm chữ ký dòng gói, lưu cạnh `status.txt`, dùng chung luật 7 ngày.<br>• Máy chủ cũ chưa trả dòng gói thì dùng gói mặc định |
| Plugin (chặn lệnh) | • Mỗi lệnh có một nhãn **Chung / 2D / 3D**.<br>• Trình điều phối lệnh từ chối lệnh ngoài gói với mã lỗi riêng.<br>• Đây cũng là chỗ đang chặn chế độ "Chỉ xem", và lệnh lẻ, `batch_execute` lẫn macro đều đi qua nên không lách được.<br>• Có test bắt buộc: lệnh nào chưa có nhãn thì CI đỏ |
| Trợ lý (Node) | • Plugin gửi gói kèm mỗi tin nhắn, giống cờ "Chỉ xem".<br>• Trợ lý chỉ nạp những lệnh thuộc gói, nên Claude không thấy lệnh 3D trên máy gói 2D.<br>• System prompt nói rõ máy đang dùng gói nào, để khi người dùng xin việc ngoài gói, Claude trả lời "máy này dùng gói 2D, liên hệ chủ plugin để mở 3D" |
| Giao diện | Header hoặc ⚙ hiện "Gói: Full". Đổi gói thì có thông báo nhỏ "Gói đã đổi sang …" ở lượt kiểm tra kế tiếp |
| Bộ cài | Không đổi. Trang kích hoạt vẫn chỉ cần key |

### Trạng thái (12/11/2026)

- **Đã chốt:** phương án A; máy chủ kích hoạt riêng cho bản Full (chủ dự án tự dựng).
- **Đã làm** trên nhánh `full/goi-dich-vu`, chưa phát hành, chưa thử trên AutoCAD thật:
  - máy chủ: cột Gói, dòng `CCF1` (commit `f570344`);
  - plugin: đọc và kiểm chữ ký dòng gói (`plan.txt`), nhãn cho cả 90 lệnh, chặn lệnh ngoài gói ở `ToolDispatcher`
    (`NOT_IN_PLAN`, cả `batch_execute` và macro), test bắt buộc nhãn;
  - trợ lý: chỉ nạp lệnh của gói, system prompt và dòng ngữ cảnh nói rõ gói, đổi gói thì khởi động lại phiên (cùng chat);
  - giao diện: "Gói: …" trên thanh trên cùng và trong ⚙, toast "Gói đã đổi sang …".
- **Đổi tên (12/11/2026):** gói "2D + 3D" gọi là **Full**. Mã trong dòng ký số vẫn là `2d3d`; ô Sheet gõ "2D + 3D"
  vẫn được hiểu là Full.
- **Gói mặc định:** `2D` cho máy mới, ô Gói trống và ô gõ sai (đã chốt 12/11/2026).
- Lệnh 3D mới phải thêm nhãn `ToolGroup.ThreeD` trong `Core/Plans.cs`; quên thì test lõi đỏ, và lệnh chỉ mở cho gói
  `Full`.
- Thử thật: phần I của [`checklist-thu-that.md`](checklist-thu-that.md).

### Chia lệnh theo gói (đề xuất A)

- **Chung (mọi gói):**
  - hỏi bản vẽ, đo, đếm, tìm;
  - layer, zoom, lưu, hoàn tác, chụp ảnh, xác nhận / kế hoạch;
  - di chuyển / sao chép / xoay / tỉ lệ / đối xứng / xóa / mảng / đổi thuộc tính;
  - block, xref;
  - đường cơ bản (line, polyline, circle, arc, spline, ellipse, rectangle, polygon). 3D cần các đường này làm biên dạng và
    đường dẫn.
- **2D:**
  - chữ, kích thước, mleader, bảng;
  - hatch, wipeout, revcloud;
  - layout, viewport, in PDF;
  - kiểu chữ / kiểu kích thước;
  - offset, fillet / chamfer 2D, trim / extend, join, explode, sửa polyline.
- **3D:** toàn bộ lệnh mới ở Phần 1.

Nghĩa là máy **gói 3D** dựng được khối và vẽ được đường làm biên dạng, nhưng không ghi kích thước, không hatch, không ra
bản in 2D. Máy **gói 2D** giữ nguyên như hiện tại. Phương án B chặt hơn: gói 3D không có cả đường cơ bản, biên dạng nhập
thẳng trong lệnh 3D. Đổi lại, gói 3D sẽ khó dùng hơn nhiều.

### An toàn

- Gói nằm trong dòng có chữ ký số, nên sửa file trên máy không mở được 3D.
- Trợ lý Node là file JS sửa được. Vì vậy việc chặn thật nằm ở plugin C#, trợ lý ẩn lệnh chỉ để đỡ tốn token.
- Mức bảo vệ ngang với khóa key hiện tại. Người sửa thẳng DLL thì vẫn lách được, như với key.

### Việc cậu phải làm tay khi triển khai

1. Dán `Code.gs` mới vào Apps Script, chạy `caiDat()` một lần để thêm cột "Gói", rồi Deploy lại bản web app.
2. Chọn gói cho từng máy đang có trong trang "Thiết bị".

## Chia việc và thứ tự

| Bước | Việc | Ai làm |
| --- | --- | --- |
| 0 | Phát hành 0.9.5 như kế hoạch, sau khi cậu thử bộ cài | Thread gộp nhánh & phát hành |
| 1 | Gói dịch vụ:<br>• cột Sheet, dòng ký số;<br>• nhãn lệnh, chặn lệnh, trợ lý nạp lệnh theo gói;<br>• giao diện "Gói" | Thread mới "Vẽ 3D & gói dịch vụ" (đề xuất). Phần Apps Script nhờ thread bảo mật rà lại vì nó thuộc phạm vi thread đó |
| 2 | 3D đợt 1 ✅ *(12/11/2026, nhánh `full/ve-3d`, chờ thử thật)*:<br>• khối cơ bản, extrude / revolve, cộng trừ khối;<br>• góc nhìn, kiểu hiển thị, thông tin khối, chụp ảnh 3D;<br>• system prompt, tự kiểm tra | Thread "Vẽ 3D" |
| 3 | Bộ cài thử 0.10.0 → cậu thử phần gói + 3D đợt 1 trên AutoCAD → phát hành | Thread gộp nhánh & phát hành |
| 4 | 3D đợt 2 (sweep, loft, cắt, bo cạnh, UCS, va chạm) | Thread "Vẽ 3D & gói dịch vụ" |
| 5 | 3D đợt 3 ✅ *(12/11/2026, nhánh `full/ve-3d-dot-3`; test & sửa: `full/ve-3d-test-3`)* (dựng 3D từ mặt bằng, mặt cắt / hình chiếu, skill 3D kiến trúc) | Thread "Vẽ 3D đợt 3" |

- **Chi phí:**
  - Bước 1 và bước 2 mỗi bước cỡ một đợt giao diện trước đây, khoảng $5–15 mỗi đợt ở thread mới.
  - Phần tốn nhất là CI Windows chạy nhiều lần và đọc code plugin.
  - Đây là ước tính; tớ không thấy số token chính xác.
- **Rủi ro chính:** 3D chỉ kiểm được trên AutoCAD thật. Bản dùng thử của cậu hết khoảng 10/12, nên nếu muốn thử 3D đợt 1
  trên bản dùng thử thì phải làm sớm. Nếu không, cần một máy AutoCAD có bản quyền.

## Việc cần cậu chốt

1. **Chia gói:** phương án A (gói 3D vẫn có đường cơ bản, không có kích thước / hatch / in 2D) hay B (chặt hơn).
2. **Gói mặc định:**
   - máy đang có trong Sheet và máy mới xin kích hoạt (đề xuất `2D`, giữ đúng như hiện tại);
   - máy chủ cũ chưa có cột Gói.
3. **Thứ tự:** làm sau khi phát hành 0.9.5 (đề xuất), hay làm song song ngay.
4. **Ai làm:** mở thread mới "Vẽ 3D & gói dịch vụ" (đề xuất), hay giao phần gói cho thread bảo mật.
5. **Phạm vi 3D đợt 1:** như bảng trên, hay thêm / bớt. Ví dụ đưa `build_3d_from_plan` lên sớm nếu cậu cần cho kiến trúc.
