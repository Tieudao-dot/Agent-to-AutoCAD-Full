# Bàn giao: Test & sửa 3D đợt 2 (sub-thread "Vẽ 3D đợt 2 › Test & sửa")

*12/11/2026 · thread cha "Vẽ 3D đợt 2" giao. Nhánh `full/ve-3d-test-2`, xuất phát `full/ve-3d-dot-2` @ `695909e`.
Bước 3 trong chuỗi chủ dự án đặt (test & sửa đợt 1 → viết đợt 2 → **test & sửa đợt 2** → viết đợt 3 → …). Đợt 3
xuất phát từ commit cuối nhánh này.*

Máy cloud không có AutoCAD: "test" = rà code, đối chiếu tài liệu API AutoCAD .NET (`AcDbMgd.xml` trong gói NuGet
`AutoCAD.NET.Model` 24.1 / 25.0 / 26.0, tức AutoCAD 2022–2027), viết test tự động, sửa lỗi. Chỗ chỉ thấy được trên
AutoCAD thật: viết code an toàn nhất có thể và đưa vào checklist phần K (K17–K21).

## Đã rà

| Phần | File | Kết quả |
| --- | --- | --- |
| 12 lệnh đợt 2 | `plugin/ClaudeCAD.Plugin/Drawing/Solid2Tools.cs` | 5 lỗi ở plugin (bảng dưới), đã sửa |
| Tham số API | `CreateSweptSolid` + `SweepOptionsBuilder`, `CreateLoftedSolid` + `LoftOptionsBuilder`, `Slice`, `FilletEdges`, `ChamferEdges`, `ShellBody`, `CheckInterference`, Brep, `Matrix3d`, `Editor.CurrentUserCoordinateSystem` | Khớp chữ ký ở cả 3 đời. `Slice(plane[, true])`: tài liệu ghi khối giữ **nửa phía pháp tuyến dương**, đúng như plugin mô tả. `ShellBody`: tài liệu ghi **dương = ra ngoài, âm = vào trong** (bàn giao đợt 2 ghi nhầm "không có tài liệu") → lỗi 2. `Matrix3d(double[])` chỉ ghi "giá trị trong mảng", không nói hàng hay cột → lỗi 6 (tự kiểm) |
| Số thứ tự cạnh / mặt | `ReadTopology`, `Solid2Logic.TopologyId` | Ổn định giữa `get_solid_info` và lệnh sửa (cùng thứ tự Brep, cùng hộp bao); khối đổi → `INVALID_PARAMS` "solid has changed", không đổi gì. **Lỗi 5** (-0 khác nhau giữa .NET Framework và .NET 8/10) |
| Bộ lọc cạnh / mặt | `FilterEdges`, `FilterFaces` | Đúng nghĩa đã mô tả (song song trục = cạnh thẳng theo trục; top… = nằm trên mặt hộp bao; mặt: phẳng, pháp tuyến theo trục, nằm trên mặt hộp bao). Test có sẵn đủ |
| UCS | `SetUcs`, `ResetUcs`, mọi file `Drawing/` | 6 preset khớp UCS trực giao của AutoCAD; ma trận UCS→WCS đúng (`AlignCoordinateSystem` từ WCS). Không lệnh nào khác đọc UCS; hai chỗ nhận điểm từ người dùng (`Interaction`: chọn điểm, điểm chèn macro) đã đổi UCS→WCS từ trước. Không lệnh nào gửi tọa độ qua dòng lệnh. Bị chặn khi "Chỉ xem", không chạy trong batch, không ghi macro, mở nhóm U |
| Cổng xác nhận | `agent/src/agent/confirmation.ts` | Đúng: slice / fillet / chamfer / shell khối có trước lượt luôn qua thẻ (kể cả trong batch); nửa mới của slice là "đã tạo"; transform 3D theo luật > 20 đối tượng; sweep / loft `erase_profile` tính như xóa |
| Macro | `Core/Macros.cs` | Tham số handle mới đều được ánh xạ; kết quả `handles` của slice `[giữ, nửa mới]` ánh xạ theo vị trí đúng; lệnh dùng khối vừa dựng tách batch. Số thứ tự cạnh: xem lỗi 5 |
| Dispose khi lỗi | cả file | Đúng ở mọi nhánh (khối tạm, region, nửa slice, bản sao giao). Lỗi 2 bỏ luôn bản sao của shell |
| AutoCAD từ chối hình | cả file | Mọi lệnh trả `INVALID_PARAMS` "Nothing was changed"; transaction của lượt (hoặc transaction lồng trong batch) lùi lại cả phần đã đổi (vd. nhóm vát đầu tiên) |
| Giới hạn tham số | `SOLID2_LIMITS` ↔ `Solid2Logic` | Khớp (test có sẵn), cả `angle` ±3600, `guide_handles` 50, điểm 5000, `max_items` 1–300 |
| Chặn theo gói | `PlanLogic`, `ToolDispatcher` | 12 lệnh nhãn `ThreeD`; chặn ở lệnh đơn, từng lệnh trong `batch_execute`, macro. Không sửa logic gói |
| "Chỉ xem" | `ToolDispatcher` | Lệnh sửa và `set_ucs` / `reset_ucs` bị từ chối; `check_interference`, `get_solid_info` là lệnh đọc, vẫn chạy |
| AutoCAD không phải tiếng Anh | cả file | Không dùng tên tiếng Anh nào của AutoCAD (chỉ biến hệ thống `MIRRTEXT`, không phụ thuộc ngôn ngữ) |
| Self-test | `SelfTest.cs` | Không thấy lỗi (dùng chung `FilletSolid`, `SliceSolid`, `OverlapVolume`) |
| Trợ lý | `agent/src/tools/solids.ts`, `systemPrompt.ts` | Đúng; sửa mô tả `shell_solid`, `check_interference` theo lỗi 3, 4 |
| Đợt 1 | test cũ | Vẫn xanh |

## Lỗi tìm được và cách sửa

| # | Mức | Lỗi | Sửa |
| --- | --- | --- | --- |
| 1 | Vừa | `loft` với `sections_points` và `sweep` với `path_points` phẳng đưa đường **chưa vào bản vẽ** cho `CreateLoftedSolid` / `CreateSweptSolid` (chưa kiểm được trên AutoCAD); đường 3D tạm thì vào bản vẽ trên **layer hiện hành**: layer hiện hành khóa thì sweep báo "layer locked" dù khối vẽ trên layer khác | Mọi đường tạm (`AddTemp`) vào bản vẽ trên layer / không gian của khối kết quả, xóa ngay sau lệnh (`DropTemp`); lỗi thì transaction lùi lại |
| 2 | Vừa | `shell_solid` thử trên **bản sao** rồi so `topology_id` của bản sao với khối thật: thứ tự Brep / subentity id của bản sao ngoài bản vẽ không được bảo đảm, khác thì mọi lệnh làm rỗng đều báo "could not shell"; lỗi khác loại AutoCAD khi đọc bản sao thì thành lỗi nội bộ | Dùng dấu theo tài liệu (`Solid2Logic.ShellOffset` = −độ dày), làm thẳng trên khối với đúng id mặt `get_solid_info` liệt kê; kiểm sau (`ShellLooksInward`: thể tích giảm, hộp bao không lớn ra), sai thì báo lỗi, transaction lùi |
| 3 | Nhẹ | Làm rỗng kín bằng `faces: []` (trợ lý cho phép) bị đòi `topology_id` | Danh sách rỗng trả ngay, không cần `topology_id` |
| 4 | Nhẹ | `check_interference` với `handles` và `other_handles` chung khối: cặp A–B kiểm và báo **hai lần** (A–B, B–A), cộng đôi thể tích chồng | `Solid2Logic.InterferencePairs`: mỗi cặp không thứ tự một lần, bỏ A–A |
| 5 | Nhẹ | `topology_id`: tọa độ làm tròn ra −0 in "-0" trên .NET 8/10 nhưng "0" trên .NET Framework → macro bo / vát theo số thứ tự ghi ở AutoCAD 2022–2024 chạy ở 2025–2027 (hoặc ngược lại) báo "solid has changed" dù khối giống hệt | Đổi −0 thành 0 trước khi băm |
| 6 | Nhẹ | `align` dựng `Matrix3d` từ mảng 16 số giả định thứ tự hàng; tài liệu không ghi | `CheckedMatrix`: trước khi dời, kiểm ma trận đưa điểm nguồn đúng chỗ như phép tính trong `Solid2Logic.Apply`; nếu AutoCAD đọc theo cột thì dùng ma trận chuyển vị, khác nữa thì báo lỗi, không dời |

Commit sửa: xem `git log full/ve-3d-dot-2..full/ve-3d-test-2` (commit "Test & sửa 3D đợt 2").

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **261/261** (255 + 6 mới trong `Solid2FixTests.cs`: −0 trong
  `topology_id`, cặp va chạm, dấu / kiểm làm rỗng, `faces: []`, đường tạm vào bản vẽ, kiểm ma trận align).
- Trợ lý `npm test`: **171/171** (169 + 2 trong `test/solids2fix.test.ts`).
- Build Linux (.NET SDK 10): net48, net8.0-windows, net10.0-windows: 0 lỗi.
- CI Windows: xem mục Tiến độ.

## Cần chủ dự án thử tay trên AutoCAD (checklist phần K)

| Mã | Bước thử | Kết quả đúng |
| --- | --- | --- |
| K17 | K6 (bể hở miệng 1000×1000×800, thành 50), rồi "dựng hộp 600x600x600, làm rỗng kín, thành 20" | Ngoài giữ nguyên; 0,1925 m³ và ≈ 0,0404 m³. Báo "did not hollow … inward" → gửi câu lỗi |
| K18 | Khóa layer hiện hành, sweep ống D50 theo chữ L và loft vuông 1000 → vuông 400 trên layer ONG | Hai khối trên ONG, không còn đường tạm, U hoàn tác cả lượt |
| K19 | Macro "dựng hộp, liệt kê cạnh, bo cạnh số 3 R50" ghi ở AutoCAD 2022–2024, chạy ở 2025–2027 (nếu có cả hai) | Bo đúng cạnh 3, không báo "solid has changed" |
| K20 | 3 hộp chồng nhau, "kiểm tra va chạm A, B với A, B, C" | A–B, A–C, B–C mỗi cặp một lần |
| K21 | Căn hộp bằng 3 cặp điểm (bước trong checklist) | Hộp dời + xoay 90° đúng; không có lỗi "internal check of the transform" |

K1–K16 của đợt 2 vẫn cần thử như cũ (K10: U hoàn tác `set_ucs`; K7: tâm tiết diện sweep đặt vào đầu đường dẫn;
K2: `FilletEdges` setback 0).

## Không làm

- Không thêm tính năng (đợt 3: `build_3d_from_plan`, mặt cắt / hình chiếu). Không sửa logic gói, `activation/activation.json`,
  `License.cs`, `.claude/settings.json`.

## Tiến độ

- [x] Rà soát + sửa lỗi + test
- [x] Tài liệu: file này, checklist K17–K21, `bo-cai/README.md`, kế hoạch
- [ ] CI Windows (1 lượt)
