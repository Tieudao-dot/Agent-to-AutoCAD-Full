# Bàn giao: Nâng cấp đợt H8: kết cấu (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người viết). Nhánh `full/nang-cap-h8`:

- gộp `7d023db` = `full/nang-cap-h6-test` @ `e606e52` + `full/nang-cap-h7-test` @ `811a63b`:
  - chỗ chồng `DrawingCheck.IsStandardLayer`: lấy bản H7-test, vì bản này đã có 3 dòng của H6T-4;
  - các file dùng chung khác (`DrawingService`, `ToolLabels`, `index.ts`, csproj test, README) giữ cả hai phần;
  - CI lượt gộp: run 45831302439 xanh, `# pass 450 / # fail 0`, lõi 1657, UI 38 / 38;
- H8 viết tiếp trên đó.

Chưa thử trên AutoCAD thật.

**Số tiêu chuẩn: đã duyệt 26/11/2026** (`docs/dot-h-so-tieu-chuan.md` mục H8 + "Kết quả duyệt"). Tất cả ở `H8Numbers`
(`Core/Rebar.cs`). Mô tả lệnh và skill `ve-ket-cau` nhắc lại các số này; test giữ chúng khớp nhau. Bốn chỗ "chưa chắc"
ghi "số mặc định, kỹ sư đối chiếu TCVN hiện hành":

- móng không lót 70 mm;
- mức tối thiểu neo / nối;
- móc đai ≥ 75 mm;
- tỉ lệ TCVN 5572.

## 1. Khách thấy gì

- **`build_footing`** "Dựng móng" (gói 3D / Full):
  - móng đơn (`pad`) tại các điểm;
  - móng băng (`strip`) theo tuyến, có sườn;
  - bê tông lót dày 100 mm, rộng hơn 100 mm mỗi bên;
  - layer `S-FOOT-3D` / `S-LEAN-3D`;
  - không nói cỡ thì dùng cỡ mẫu (1200 × 1200 đế 350; băng 1000 × 300, sườn 300 × 600) và báo "cỡ mẫu, kỹ sư quyết";
  - khối lượng bê tông lấy bằng `volume_schedule` có sẵn.
- **`draw_rebar`** "Vẽ thép" (gói 2D / Full):
  - `beam`: triển khai dầm (thanh dưới / trên chạy vào gối 30d; thép gân bẻ L 10d ở hai đầu, thép trơn móc 180°; đai theo
    vùng 1/4 nhịp) và mặt cắt bên cạnh;
  - `column`: mặt cắt cột (thanh ở 4 góc rồi chia đều các cạnh, đai), chiều dài thanh = chiều cao + nối 35d;
  - `slab`: ô sàn, thanh tiêu biểu hai phương, số thanh theo khoảng cách;
  - **thép chủ khách phải nhập** (vd "2Ø18"); thiếu thì lời báo nói rõ "plugin không chọn thép";
  - đai / thép sàn không nói thì dùng thép mẫu và báo;
  - mỗi loại thanh có số hiệu trong vòng tròn và chữ "2Ø18 L=6440" (5000 + 2 × 30d + 2 × 10d);
  - thanh không vừa một hàng thì từ chối, không tự dồn;
  - neo / nối dưới mức tối thiểu thì cảnh báo;
  - `start_mark` trùng số hiệu đã có trong bản vẽ thì cảnh báo; thanh dài hơn cây 11,7 m thì báo số mối nối chồng.
- **`rebar_schedule`** "Bảng thống kê thép" (gói 2D / Full):
  - mỗi loại thanh một dòng: cấu kiện, hình dạng, Ø, loại thép, dài 1 thanh (làm tròn 10 mm), số thanh, tổng dài, kg
    (0,00617·d²), số mối nối (cây 11,7 m);
  - thanh dài hơn 11,7 m: dài 1 thanh **cộng đoạn nối chồng** (40d dầm / sàn, 35d cột) và kg tính theo đó; cột hình dạng
    ghi "gồm 1 nối chồng 35d = 700";
  - một số hiệu dùng cho hai loại thanh khác nhau (khác Ø / dài / hình / cấu kiện): **giữ hai dòng riêng, không gộp**, và
    trả `warnings` để Claude bảo khách đánh số lại. Cùng số hiệu, cùng loại thanh thì cộng số thanh;
  - dòng cộng theo đường kính, dòng tổng;
  - `table_position` chèn bảng vào bản vẽ; `excel` ghi file (thẻ F5);
  - hao hụt chỉ có khi khách nói %.
- `check_drawing` không báo layer `S-…` là ngoài chuẩn (như H6T-4 / H7T-06).

## 2. Code

| File | Việc |
| --- | --- |
| `Core/Rebar.cs` (mới) | `H8Numbers`; `RebarLogic`: đọc "2Ø18" / "Ø8a200", loại thép, kg/m, lớp bảo vệ, neo / nối + cảnh báo, móc, chiều dài đai, vị trí đai theo vùng, vị trí thanh (dầm, cột), danh sách thanh của dầm / cột / sàn, bảng thống kê |
| `Drawing/StructureTools.cs` (mới) | `build_footing` (hộp + cổ móng; móng băng dùng lại bố cục dầm `Solid3Logic.Beam` + `UniteOutlines`), `draw_rebar` (vẽ 2D + dấu `CLAUDECAD_REBAR` trên thanh đầu của mỗi loại), `rebar_schedule` (dùng lại `InsertSchedule`, `ExportExcel`) |
| `Core/Plans.cs`, `Core/Ucs.cs`, `UI/ToolLabels.cs`, `Drawing/DrawingService.cs`, `Core/DrawingCheck.cs`, `Core/SkillStore.cs` | mỗi file 1–3 dòng: nhóm gói, UCS Create, nhãn, đăng ký, layer `S-` là chuẩn, skill mẫu |
| `SelfTest.cs` | bước "Nâng cấp đợt H8…": móng đơn + móng băng, thép dầm 2Ø18 / 2Ø16, bảng thép (kg khớp 0,00617·d²·L), xóa hết |
| `agent/src/tools/structure.ts` (mới), `index.ts`, `solids.ts`, `confirmation.ts`, `systemPrompt.ts`, `agent/skills.ts` | 3 lệnh (hoãn nạp), lệnh 3D, thẻ Excel, 2 dòng prompt (2D: thép; 3D: móng), skill mẫu 2D |
| `skills/ve-ket-cau/SKILL.md` (mới) | quy trình, số mặc định, thép mẫu, layer |
| test | `Core.Tests/NangCapH8Tests.cs` (9), `agent/test/nangcaph8.test.ts` (3); cập nhật số lệnh 3D (41 → 42) |

## 3. Con số chờ chủ dự án duyệt (số thường)

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 50 mm | `FirstStirrupMm`: đai đầu tiên cách mép gối | Vị trí đai đầu | Thông lệ |
| 50 – 1000 mm | `MinSpacingMm` / `MaxSpacingMm`: khoảng cách thép nhận được | Ngoài khoảng: báo lỗi | Chặn gõ nhầm (a20, a2000) |
| 40 thanh | `MaxBarsInSection` | Nhiều hơn: báo lỗi | Một mặt cắt hiếm khi quá 20 |
| 500 móng | `MaxFootings`; 400 nét đai vẽ mỗi lần (`MaxStirrupLines`, số đếm vẫn đủ) | Giới hạn | AutoCAD không bị chậm |
| 2,5 mm chữ, bóng số hiệu 8 mm trên giấy | `TextHeightPaper`, `MarkBubblePaper` | Cỡ chữ, vòng số hiệu | Như chữ thường TCVN 7284 |
| màu layer 8 / 9 / 7 / 1 / 7 | `FootingColor`, `LeanColor`, `ConcreteColor`, `RebarColor`, `TextColor` | Màu nét | Thép đỏ cho dễ thấy |
| khoảng cách mặt cắt cạnh triển khai | max(1000, 3 h) mm | Vị trí mặt cắt | Không chồng lên triển khai |

## 4. Quyết định tự chọn

- **Ba lệnh**: móng (3D), vẽ thép (2D), bảng (2D). Theo kế hoạch, móng 3D thuộc gói 3D / Full, thép 2D thuộc gói 2D / Full.
- Dầm vẽ triển khai **và** mặt cắt trong một lần gọi. Dấu thống kê nằm trên triển khai, nên bảng không đếm hai lần.
- Dấu thống kê nằm trên **thanh đầu tiên** của mỗi loại, ghi luôn số thanh. Xóa thanh đó thì loại thanh đó biến khỏi bảng.
  Xóa thanh khác thì bảng vẫn đếm đủ (đếm theo số lúc vẽ).
- Thép gân dầm: neo 30d rồi **bẻ L 10d** ở mỗi đầu (làm theo skill, số đã duyệt; sửa 26/11 sau khi thread F2 đọc trước
  `c01e557` thấy code chưa dùng 10d). Chọn làm theo skill thay vì sửa skill vì 10d là số đã duyệt và cộng thêm thép (thống
  kê không thiếu). Thanh dưới bẻ lên, thanh trên bẻ xuống. Chỉ áp cho dầm: thép cột là thanh nối (không neo), thép sàn mặc
  định Ø8 trơn (móc 180°); thép sàn gân khách nhập vẫn tính thẳng.
- Đoạn nối chồng của thanh dài hơn cây 11,7 m được cộng ở **bảng thép** (`rebar_schedule`), không cộng vào chữ "L=…" trên
  hình (chữ trên hình là chiều dài hình học); `draw_rebar` báo trước số mối nối. Nối làm dài thanh nên số mối nối được đếm
  lại tới khi không đổi.
- Số hiệu trùng: không tự đánh số lại (sẽ lệch chữ trên hình), chỉ tách dòng và báo.
- Móc 180° của thép trơn được tính vào chiều dài, không vẽ hình móc. Đai tính hai móc 135°, đoạn thẳng max(6d, 75).
- Một mặt cắt chỉ một hàng thép dưới / một hàng trên. Không đủ chỗ thì báo, không tự xếp hai hàng.
- Móng băng: lót không kéo dài thêm 100 mm ở hai đầu tuyến (chỉ hai bên).

## 5. Token lúc mở chat

- System prompt: +1 dòng ở gói 2D / Full (khoảng 180 ký tự) và +1 dòng ở gói 3D / Full (khoảng 120 ký tự).
- 3 lệnh hoãn nạp: chỉ nạp khi Claude tìm tới.
- Skill `ve-ket-cau`: chỉ nạp khi bật và cần.
- Đo bằng số ký tự.

## 6. Test

- Lõi: 1666 / 1666 (thêm 9 ở `NangCapH8Tests.cs`; 2 test mới cho số hiệu trùng và nối chồng, test dầm thêm bẻ L 10d).
- Trợ lý: 457 / 457 (thêm 3).
- Build 3 target: qua.

## 7. CI

- Lượt gộp: run 45831302439 trên `7d023db`: xanh, `# fail 0`.
- H8 lượt 1: run 45835715473 trên `c01e557` (bản trước khi sửa 3 lỗi): xanh, `# pass 457 / # fail 0`, lõi 1664, UI 38 / 38.
- H8 lượt 2: run 45837703644 trên `ca89b95` (đã sửa 3 lỗi): xanh, `# pass 457 / # fail 0`, lõi 1666, UI 38 / 38.

## 7b. Sửa sau khi đọc trước (26/11, thread F2 nêu 3 chỗ, cả 3 là lỗi thật)

1. Số hiệu trùng: bảng gộp theo số hiệu, lấy Ø + dài của dòng đầu, khối lượng sai không báo. Sửa: khóa dòng theo (số hiệu,
   Ø, dài, hình, cấu kiện), trùng số hiệu khác loại thì tách dòng + `warnings`; `draw_rebar` báo khi `start_mark` trùng.
2. Thanh > 11,7 m không cộng nối chồng: thêm `RebarLogic.WithLaps`, bảng cộng nối chồng vào dài / kg, ghi trong hình dạng.
3. Bẻ L 10d: làm theo skill (`RebarLogic.AnchorEnd`), vẽ chân L trên triển khai; xem "Quyết định tự chọn".

## 8. Chưa kiểm được

- AutoCAD thật:
  - chấm thép (polyline có độ rộng) hiện đúng;
  - chữ / vòng số hiệu theo tỉ lệ;
  - móng băng góc khép;
  - bảng trong bản vẽ;
  - Excel.
