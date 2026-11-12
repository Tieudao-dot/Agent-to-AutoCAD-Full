# Bàn giao: 3D đợt 1 (sub-thread "Vẽ 3D")

*12/11/2026 · luồng chính giao cho sub-thread "Vẽ 3D". Nhánh `full/ve-3d`, xuất phát `full/goi-dich-vu` @ `b1e9419`.*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) (Phần 1),
[`goi-dich-vu-plugin.md`](goi-dich-vu-plugin.md), `plugin/ClaudeCAD.Plugin/Core/Plans.cs`.

## Chủ dự án đã chốt (12/11/2026)

- 3D đợt 1 giữ đúng phạm vi kế hoạch, không thêm `build_3d_from_plan` (để đợt 3).
- Gói: 2D / 3D / Full. Gói mặc định cho máy mới: 2D.
- Chia gói phương án A: gói 3D có đường cơ bản, không có chữ / kích thước / hatch / in 2D.
- Mọi lệnh 3D mới gắn nhãn `ToolGroup.ThreeD` trong `PlanLogic`: chỉ máy gói 3D và Full dùng được; máy gói 2D không
  nạp các lệnh này.

## Việc cần làm

| Nhóm | Lệnh | API AutoCAD .NET |
| --- | --- | --- |
| Khối cơ bản | `draw_box`, `draw_cylinder` (cả hình nón), `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid` | `Solid3d.CreateBox / CreateFrustum / CreateSphere / CreateWedge / CreateTorus / CreatePyramid` |
| Từ biên dạng 2D | `extrude` (theo chiều cao, có góc vát), `revolve` (quanh trục) | `CreateExtrudedSolid`, `CreateRevolvedSolid`, `Region.CreateFromCurves` |
| Cộng trừ khối | `solid_union`, `solid_subtract`, `solid_intersect` | `Solid3d.BooleanOperation` |
| Góc nhìn | `set_view_3d` (trên, trước, bên, 4 góc iso), `set_visual_style` (2D Wireframe / Conceptual / Shaded / Realistic) | `ViewTableRecord.ViewDirection`, `VisualStyleId` |
| Hỏi khối | `get_solid_info`: thể tích, trọng tâm, hộp bao, số mặt / cạnh | `MassProperties`, `GeometricExtents`, Brep |
| Tự kiểm tra | `capture_view` thêm tùy chọn hướng nhìn + kiểu hiển thị; vẫn thuộc nhóm Chung | Lệnh chụp hiện có |

Kèm theo:

- **Trợ lý:** định nghĩa lệnh 3D (qua bộ lọc theo gói). System prompt thêm quy ước 3D: Z hướng lên, mm; biên dạng trên
  XY rồi extrude; mỗi loại khối một layer; dựng xong đặt góc iso, chụp Conceptual, kiểm bằng số (thể tích, hộp bao).
- **An toàn:** cộng / trừ vào khối có sẵn của người dùng là sửa đối tượng có sẵn → qua thẻ xác nhận
  (`agent/src/agent/confirmation.ts`). Hoàn tác cả lượt bằng một lần U giữ như cũ.
- **`CLAUDECHATSELFTEST`:** thêm bước dựng hộp, trừ trụ, kiểm thể tích.
- **Test:** phần tính toán không cần AutoCAD (hướng nhìn, trục quay, kiểm tham số) trong `plugin/ClaudeCAD.Core.Tests`;
  lệnh trợ lý trong `agent/test`. Test bắt buộc nhãn lệnh phải vẫn xanh.
- **Tương thích:** build net48 / net8.0-windows / net10.0-windows; kiểm trên CI.
- **Tài liệu:** `docs/checklist-thu-that.md` (phần "3D"), `docs/ke-hoach-3d-goi-dich-vu.md` (đánh dấu đợt 1),
  `bo-cai/README.md`, file này.

"Xong" = đủ lệnh đợt 1 có nhãn 3D; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`,
workflow_dispatch, `release: "false"`, nhánh `full/ve-3d`); docs cập nhật; đã push.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính hỏi chủ dự án.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, khóa công khai (`License.cs`), logic gói (`PlanLogic`, `Activation`,
  `ToolDispatcher` phần gói) ngoài việc thêm nhãn lệnh 3D.
- Không làm đợt 2 / 3 (sweep, loft, slice, fillet 3D, UCS, va chạm, `build_3d_from_plan`).
- Mức gấp: khá gấp; chủ dự án muốn thử trên AutoCAD trước khi bản dùng thử hết hạn (~10/12/2026).

## Tiến độ

- [ ] Lệnh plugin + nhãn 3D
- [ ] Trợ lý: định nghĩa lệnh, system prompt, cổng xác nhận
- [ ] Self-test
- [ ] Test lõi + trợ lý
- [ ] CI Windows (1 lượt)
- [ ] Tài liệu
