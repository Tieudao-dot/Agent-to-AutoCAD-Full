# Bàn giao: 3D đợt 3 (thread "Vẽ 3D đợt 3")

*12/11/2026 · luồng chính giao. Nhánh `full/ve-3d-dot-3`, xuất phát `full/ve-3d-test-2` @ `a51cd63`
(đợt 2 đã test & sửa; lõi 261/261, trợ lý 171/171, CI run 43212088529 xanh). Bước 4 trong chuỗi chủ dự án đặt:
test & sửa đợt 1 (xong) → viết đợt 2 (xong) → test & sửa đợt 2 (xong) → **viết đợt 3** → test & sửa đợt 3
(thread con) → test tổng thể (luồng chính mở).*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) Phần 1,
[`ve-3d-dot-1.md`](ve-3d-dot-1.md), [`ve-3d-test-1.md`](ve-3d-test-1.md), [`ve-3d-dot-2.md`](ve-3d-dot-2.md),
[`ve-3d-test-2.md`](ve-3d-test-2.md) (13 lỗi đã sửa: không lặp lại).

## Chủ dự án đã chốt (12/11/2026)

- Gói 2D / 3D / Full, gói mặc định 2D, phương án A. **Mọi lệnh mới của đợt 3 thuộc `ToolGroup.ThreeD`.**
  Bên trong một lệnh 3D được tạo đường / hatch 2D làm kết quả (mặt cắt). Không mở lệnh nhóm 2D cho gói 3D.
- Lệnh làm đổi hình khối có trước lượt đó (kể cả khối Claude dựng lượt trước) luôn qua thẻ xác nhận.
- Mọi lệnh dùng tọa độ WCS; `set_ucs` chỉ đặt mặt phẳng làm việc.
- Đợt 3 đúng phạm vi kế hoạch, **không** làm skill "3D cơ khí".

## Việc cần làm

1. **`build_3d_from_plan`**: dựng 3D từ mặt bằng 2D ("dựng 3D mặt bằng này, tường cao 3,3 m").
   - Đọc tường, cửa đi / cửa sổ, sàn theo layer của skill `ve-kien-truc`; thêm chế độ chỉ định rõ (handle đường tim
     hoặc đường bao tường, chiều dày, vùng sàn).
   - Extrude tường, trừ lỗ cửa (cao cửa đi; cao bậu + cao cửa sổ), đổ sàn có độ dày; mỗi loại khối một layer 3D.
   - Mặt bằng lộn xộn: báo cáo rõ phần nhận / bỏ qua (handle, lý do), không đoán bừa, giới hạn số đối tượng, chạy lại
     không tạo trùng mà không báo. Chỉ đọc đối tượng 2D của người dùng.
   - Dựng xong: góc iso Conceptual, kiểm bằng số (thể tích, hộp bao).
2. **Mặt cắt / hình chiếu 2D** từ khối 3D: đường bao vết cắt + hatch (+ phần nhìn thấy phía sau), hình chiếu theo
   hướng nhìn; layer riêng, không đè mô hình. Ưu tiên API .NET (`Section`, `GenerateSectionGeometry`); lệnh AutoCAD
   (FLATSHOT / VIEWBASE) chỉ khi không có API, phải cô lập. Không làm được an toàn thì dừng, báo luồng chính.
3. **Skill mẫu "3D kiến trúc"** (`skills/`): chiều cao tầng, dày sàn, cao cửa đi, cao bậu / cửa sổ, layer khối 3D,
   quy trình dựng + tự kiểm. Máy gói 2D không nạp (nếu cơ chế cho phép).

Kèm theo: trợ lý (định nghĩa lệnh, system prompt gói 3D / Full), nhãn `ThreeD` + test nhãn, `CLAUDECHATSELFTEST`
(phòng chữ nhật + cửa đi → dựng → thể tích; cắt ngang → có đường bao), test lõi + trợ lý (đợt 1, 2 vẫn xanh), build
net48 / net8 / net10, tài liệu (checklist phần L, kế hoạch, `bo-cai/README.md`, `skills/README.md`, file này).

"Viết xong" = đủ 3 phần, gắn nhãn; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`,
workflow_dispatch, `release: "false"`, nhánh `full/ve-3d-dot-3`); docs cập nhật; đã push. Xong thì tự mở thread con
"Vẽ 3D đợt 3 › Test & sửa" (nhánh `full/ve-3d-test-3`) với prompt luồng chính soạn sẵn, báo luồng chính.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính. Chỉ mở đúng 1 thread con.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, `License.cs`, `.claude/settings.json`; logic gói chỉ thêm nhãn.
- Không sửa lệnh 3D đợt 1, 2 trừ khi cần dùng lại (ghi rõ ở đây, test cũ vẫn xanh).

## Tiến độ

- [ ] `build_3d_from_plan`
- [ ] Mặt cắt / hình chiếu
- [ ] Skill 3D kiến trúc
- [ ] Trợ lý, nhãn, self-test, test
- [ ] CI Windows (1 lượt)
- [ ] Tài liệu
- [ ] Mở thread con Test & sửa đợt 3
