# Bàn giao: 3D đợt 2 (thread "Vẽ 3D đợt 2")

*12/11/2026 · luồng chính giao. Nhánh `full/ve-3d-dot-2`, xuất phát `full/ve-3d-test-1` @ `9332985`
(đợt 1 đã test & sửa; lõi 236/236, trợ lý 159/159, CI run 43203265912 xanh). Bước 2 trong chuỗi chủ dự án đặt:
test & sửa đợt 1 (xong) → **viết đợt 2** → test & sửa đợt 2 (thread con) → viết đợt 3 → test & sửa đợt 3 → test tổng thể.*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) Phần 1,
[`ve-3d-dot-1.md`](ve-3d-dot-1.md), [`ve-3d-test-1.md`](ve-3d-test-1.md) (7 lỗi đợt 1, không lặp lại).

## Chủ dự án đã chốt (12/11/2026)

- Gói 2D / 3D / Full, gói mặc định 2D, phương án A. **Mọi lệnh 3D mới thuộc `ToolGroup.ThreeD`.**
- Cộng / trừ vào khối có trước lượt đó (kể cả khối Claude dựng ở lượt trước) luôn qua thẻ xác nhận. Luồng chính áp
  cùng luật cho `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid`. `rotate_3d`, `mirror_3d`, `align` theo
  luật sửa đối tượng có sẵn của move / rotate / mirror 2D.
- Đợt 2 giữ đúng phạm vi kế hoạch, không làm việc của đợt 3.

## Việc cần làm

| Nhóm | Lệnh | API AutoCAD .NET |
| --- | --- | --- |
| Biên dạng nâng cao | `sweep`, `loft` | `Solid3d.CreateSweptSolid` + `SweepOptions`, `CreateLoftedSolid` + `LoftOptions` |
| Sửa khối | `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid` | `Solid3d.Slice`, `FilletEdges`, `ChamferEdges`, `ShellBody` |
| Biến đổi 3D | `rotate_3d`, `mirror_3d`, `align` | `Matrix3d.Rotation / Mirroring / AlignCoordinateSystem`, `TransformBy` |
| Mặt phẳng làm việc | `set_ucs`, `reset_ucs` | `Editor.CurrentUserCoordinateSystem` |
| Kiểm tra | `check_interference` | `Solid3d.CheckInterference` |
| Hỏi khối | `get_solid_info` thêm danh sách cạnh / mặt (có giới hạn) | Brep |

Điểm khó:

- Chọn cạnh / mặt: số thứ tự từ `get_solid_info` (ổn định khi khối chưa đổi; khối đã đổi thì báo lỗi rõ) hoặc bộ lọc
  (song song trục, thuộc mặt trên / dưới / trước…, mọi cạnh).
- UCS: mọi lệnh hiện có vẫn hiểu tọa độ theo WCS. Cần đổi quy ước thì dừng, báo luồng chính.
- Macro: tham số handle mới phải được `Macros.IsHandleKey` ánh xạ; số thứ tự cạnh / mặt khi phát lại chọn cách an toàn.
- Bài học đợt 1: Dispose đối tượng tạm khi lỗi; AutoCAD từ chối hình → `INVALID_PARAMS` rõ, bản vẽ không đổi; giới hạn
  tham số chung giữa trợ lý và plugin; AutoCAD không phải tiếng Anh.

Kèm theo: trợ lý (định nghĩa lệnh, system prompt, cổng xác nhận), nhãn `ThreeD`, `CLAUDECHATSELFTEST` thêm bước đợt 2,
test lõi + trợ lý, build net48 / net8 / net10, tài liệu (checklist phần K, kế hoạch, `bo-cai/README.md`, file này).

"Viết xong" = đủ lệnh có nhãn; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`, workflow_dispatch,
`release: "false"`, nhánh `full/ve-3d-dot-2`); docs cập nhật; đã push. Xong thì tự mở thread con
"Vẽ 3D đợt 2 › Test & sửa" (nhánh `full/ve-3d-test-2`) với prompt luồng chính soạn sẵn.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính. Chỉ mở đúng 1 thread con.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, `License.cs`, `.claude/settings.json`; logic gói chỉ thêm nhãn.
- Không làm đợt 3 (`build_3d_from_plan`, mặt cắt / hình chiếu 2D, skill 3D kiến trúc).

## Tiến độ

- [ ] Lệnh plugin + nhãn 3D
- [ ] Trợ lý: định nghĩa lệnh, system prompt, cổng xác nhận
- [ ] Self-test
- [ ] Test lõi + trợ lý
- [ ] CI Windows (1 lượt)
- [ ] Tài liệu
- [ ] Mở thread con Test & sửa đợt 2
