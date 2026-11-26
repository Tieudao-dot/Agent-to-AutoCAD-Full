# Bàn giao: Nâng cấp đợt H7: ống MEP 3D (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người viết). Nhánh `full/nang-cap-h7`, tách từ `full/nang-cap-h-tong-the` @
`76ecfae`. Chạy cùng lúc với H6 (hạ tầng, thread đợt F2). Chưa thử trên AutoCAD thật.

> **Số theo tiêu chuẩn: đã duyệt 26/11/2026** (chủ dự án chọn "Dùng hết số đề xuất", `docs/dot-h-so-tieu-chuan.md` mục
> "Kết quả duyệt"). Các số ở `H7Numbers` (`Core/Mep.cs`) và được nhắc lại trong mô tả lệnh, skill `ve-mep` mục 7; test giữ
> các chỗ này khớp nhau. Hai chỗ "chưa chắc" (mức dốc tối thiểu theo TCVN 4474, cỡ gợi ý PCCC) vẫn dùng số đề xuất, ghi
> trong skill: "số mặc định, kỹ sư đối chiếu TCVN hiện hành trước khi dùng".

## 1. Khách thấy gì

- Gói 3D / Full. Hai lệnh mới, hoãn nạp:
  - `draw_mep`: "Vẽ ống MEP 3D";
  - `mep_report`: "Thống kê / soát ống MEP".
- **Đi ống** (`draw_mep`):
  - 7 hệ:
    - cấp nước, nước nóng, thoát nước, thông hơi: ống tròn;
    - PCCC: ống tròn, bắt buộc khách nói cỡ;
    - ống gió cấp / hồi: ống hộp chữ nhật, bắt buộc khách nói rộng × cao.
  - Tuyến là dãy điểm. Có hai cách cho cao độ:
    - điểm mặt bằng (x, y) + cao độ tim ống, hoặc `under_beams` (đi ngay dưới dầm thấp nhất trên tuyến; không có dầm thì
      dưới sàn);
    - điểm 3D (x, y, z).
  - Ống thoát nước **dốc** theo độ dốc %. Dốc thấp hơn mức tối thiểu của cỡ ống thì có cảnh báo.
  - Mỗi góc một **co**, bán kính theo cỡ ống. Điểm thẳng hàng không sinh co. Tuyến quay ngược lại, hoặc đoạn quá ngắn so với
    co, thì bị từ chối kèm lời báo.
  - `connect_to` = một ống thẳng có sẵn mà điểm đầu nằm trên đó: vẽ **tê** ở chỗ nối. Nhánh bắt đầu từ mặt ống chính.
  - Layer theo hệ, thêm hậu tố `-3D` (`P-SUPL-3D`, `P-DRAIN-3D`, `M-DUCT-3D`, `F-SPRK-3D`…), màu theo hệ.
  - Hiểu UCS: điểm theo UCS; cao độ cộng gốc UCS như các lệnh 3D khác.
  - Cỡ ống: chỉ vẽ cỡ khách đưa, hoặc cỡ mặc định của loại ống (`use`: branch / main, floor / wc / stack). **Không tính
    thủy lực / lưu lượng**: lời báo của lệnh nói rõ điều này.
- **Thống kê** (`mep_report`, `action: schedule`):
  - mỗi dòng là một cặp hệ + cỡ ống: chiều dài (m), số đoạn ống, số co, số tê; có dòng Tổng;
  - `excel`: ghi file `.xlsx`; file có sẵn thì hỏi thẻ ghi đè (F5);
  - `waste_percent`: thêm cột "chiều dài + hao hụt %". Mặc định **không có** cột này: chủ dự án còn phải chọn, đổi bằng
    `H7Numbers.WasteColumnByDefault`.
- **Soát va chạm** (`action: clash`):
  - mỗi ống được soát với dầm / cột (khối 3D trên layer có BEAM / COL / DAM / COT / S-, hoặc các khối khách chỉ trong
    `other_handles`) và với ống của tuyến khác;
  - "chạm" = hai khối giao nhau (dùng `CheckInterference` và đo thể tích giao như `check_interference`);
  - "sát" = gần hơn khoảng hở (đo giữa hai hộp bao);
  - ống cùng một tuyến, hoặc nhánh với ống chính của nó, không bị báo;
  - `excel` có danh sách.

## 2. Code

| File | Việc |
| --- | --- |
| `Core/Mep.cs` (mới) | `H7Numbers` (mọi số); `MepLogic`: hệ, cỡ, độ dốc, bán kính co, chia tuyến thành ống + co (`Layout`), mặt cắt ống gió, thống kê, bảng, hao hụt, soát (khoảng cách hộp, loại) |
| `Drawing/MepTools.cs` (mới) | `draw_mep` (ống: `CreateFrustum` / `CreateBox` rồi đặt theo trục; co: `Revolve` mặt cắt quanh trục co; tê), dấu `CLAUDECAD_MEP` trên mỗi khối (hệ, loại, cỡ, chiều dài, tuyến, trục), `mep_report` |
| `Drawing/DrawingService.cs`, `Core/Plans.cs`, `UI/ToolLabels.cs`, `Core/Ucs.cs` | mỗi file thêm 1–3 dòng: đăng ký, nhóm 3D, nhãn, `elevation` là cao độ |
| `SelfTest.cs` | bước "Nâng cấp đợt H7…": dầm hộp, tuyến chữ L thoát nước (1 co), nhánh có tê, thống kê, soát chạm dầm, xóa hết |
| `agent/src/tools/mep.ts` (mới), `index.ts`, `solids.ts`, `confirmation.ts`, `systemPrompt.ts` | hai lệnh (hoãn nạp, mô tả có số mặc định đã duyệt), danh sách lệnh 3D, thẻ Excel, 1 dòng prompt ở phần 3D |
| `skills/ve-mep/SKILL.md` | mục 7 "Ống 3D": cỡ mặc định, dốc, cao độ, co / tê, layer 3D, thống kê / soát (số đã duyệt) |
| test | `Core.Tests/NangCapH7Tests.cs` (14), `agent/test/nangcaph7.test.ts` (3); cập nhật số lệnh 3D trong `SolidTests.cs`, `CrossPlanTests.cs`, `agent/test/crossplan.test.ts` (39 → 41) |

## 3. Số theo tiêu chuẩn: đã duyệt 26/11/2026 (`docs/dot-h-so-tieu-chuan.md`, H7)

Đúng các số đề xuất; tất cả ở `H7Numbers`:

- `SupplyBranchMm` 25, `SupplyMainMm` 32;
- `DrainFloorMm` 60, `DrainWcMm` 110, `VentMm` 60;
- `DrainSlopePercent` 2, `MinDrainSlope` (D50 2,5 %, D100 1,2 %, D125 1 %, D150 0,7 %, D200 0,5 %);
- **chốt thêm 26/11 ~19h50 (đã duyệt 26/11):** "ống nhỏ 2,5 %, ống lớn 2 %": độ dốc mặc định = max(2 %, mức tối thiểu theo cỡ)
  (`MepLogic.DefaultSlope`): D60 dốc 2,5 %, D110 trở lên 2 %. Không ống mặc định nào bị cảnh báo;
- `TopBelowBeamMm` 100, `TopBelowSlabMm` 300;
- `PipeBendFactor` 1,5, `DuctBendFactor` 1;
- `ClearanceMm` 50;
- layer / màu trong `MepLogic.Systems`, `Layer3dSuffix` "-3D";
- `LengthDecimals` 2;
- PCCC và ống gió không có cỡ mặc định.

- Thống kê không cộng hao hụt (chủ dự án chọn 26/11): `WasteColumnByDefault` tắt. Khách nói % hao hụt thì `waste_percent`
  thêm cột.
- Mức dốc tối thiểu theo TCVN 4474 và cỡ gợi ý PCCC: **chưa chắc**, vẫn dùng số đề xuất. Skill ghi "số mặc định, kỹ sư
  đối chiếu TCVN hiện hành trước khi dùng".

## 4. Con số chờ chủ dự án duyệt (số thường, chưa duyệt)

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 200 điểm | `H7Numbers.MaxPoints`: số điểm một tuyến | Tuyến dài hơn: chia làm nhiều lần | Một tuyến trong một tầng hiếm khi quá vài chục góc |
| 5 000 đối tượng | `MaxSolids`: số đối tượng đọc khi thống kê / soát / tìm dầm | Bản vẽ lớn hơn: chỉ đọc phần đầu, có ghi chú | Như các lệnh soát khác |
| 1,15 lần | `FittingOversize`: tê to hơn ống chính | Tê thấy rõ trên mô hình | Giống ống nối thật (đầu nối to hơn ống) |
| 2 lần đường kính nhánh | `TeeLengthFactor`: chiều dài thân tê dọc ống chính | Cỡ tê trên mô hình | Vừa ôm chỗ nối |
| 300 dòng | `MaxClashRows`: số chỗ chạm / sát kể trong kết quả | Nhiều hơn: chỉ đếm | Kết quả ngắn cho Claude |

## 5. Quyết định tự chọn

- **Hai lệnh**, không phải một: vẽ (ghi) và báo cáo (đọc). Lý do: lệnh đọc chạy được ở "Chỉ xem"; mô tả lệnh ngắn hơn.
- Ống và co là **khối 3D riêng**, không gộp. Mỗi khối mang dấu riêng để thống kê đúng sau khi khách xóa / chép một đoạn.
  Chiều dài lấy theo dấu lúc vẽ. Khách kéo giãn ống bằng lệnh AutoCAD thì thống kê không đổi; vẽ lại bằng `draw_mep` thì đúng.
- Chiều dài co tính theo đường tim (bán kính × góc), cộng vào chiều dài của cỡ ống đó.
- Tê đếm vào hệ và cỡ của **nhánh**.
- Soát "sát": ống thẳng đo từ trục (đúng cả khi chéo); co / tê đo giữa hai hộp bao. Ống gió lấy nửa đường chéo mặt cắt
  (về phía an toàn).
- Dầm / cột tìm theo tên layer (BEAM / COL / DAM / COT / S-). Khách dùng tên khác thì chỉ khối qua `other_handles`.
- `under_beams`: dầm tìm theo tên layer BEAM / DAM, sàn theo SLAB / SAN; lấy đáy thấp nhất trong vùng tuyến đi qua.
- Không tính độ dốc cho ống cấp nước / PCCC / ống gió, trừ khi khách cho `slope`.

## 5b. Sửa sau khi thread đợt F2 đọc trước (26/11)

| Chỗ | Lỗi | Sửa | Test (`NangCapH7Tests.cs`) |
| --- | --- | --- | --- |
| Độ dốc mặc định | Ống thoát D60 mặc định dốc 2 %, thấp hơn mức tối thiểu 2,5 % của cỡ, nên ống nào vẽ ra cũng bị cảnh báo | `MepLogic.DefaultSlope` (chủ dự án chốt 26/11) | `A_default_drain_is_never_warned…` |
| Ống gió xoắn ở đoạn đứng | Mặt cắt mỗi đoạn tính riêng: đoạn đứng lấy bề rộng theo trục x, đoạn trước theo y, nên xoắn 90° so với co | Trục bề rộng (`MepPiece.W`) được mang dọc tuyến: xoay theo từng co, chiếu lên mỗi đoạn (`SectionOf`) | `A_duct_keeps_its_width_axis…` |
| `mep_report` bỏ sót ống | Giới hạn 5 000 đếm cả nét 2D, chữ…: bản vẽ nhiều nét 2D thì ống phía sau không được đọc | Chỉ đếm khối 3D (`ObjectClass.IsDerivedFrom(Solid3d)`), cả ở `under_beams` | soi mã nguồn |
| "Sát 0 mm" với ống chéo | Khe hở đo giữa hai hộp bao: hộp của ống chéo rất to, nên chồng lên dầm ở xa | Ống thẳng: đo từ trục ống tới hộp dầm (`PipeBoxGap`), hoặc giữa hai trục ống (`PipePipeGap`), trừ bán kính. Co / tê vẫn đo bằng hộp | `A_slanted_pipe_is_measured…` |

## 6. Token lúc mở chat

- System prompt: +1 dòng (khoảng 190 ký tự, khoảng 55 token) ở phần 3D, chỉ gói 3D và Full.
- Hai lệnh hoãn nạp: mô tả 529 + 355 ký tự, cộng tham số. Chỉ nạp khi Claude tìm tới lệnh, hoặc khi tắt "Nạp gọn lệnh".
- Đo bằng số ký tự, không phải số token chính xác.

## 7. Test

- Lõi: 1624 / 1624 (thêm 17 ở `NangCapH7Tests.cs`).
- Trợ lý: 446 / 446 (thêm 3 ở `nangcaph7.test.ts`).
- Build net48, net8.0-windows, net10.0-windows: qua.
- Test hình học kiểm: co nối liền hai ống (xoay điểm đầu theo trục co đúng tới điểm đầu ống sau), cả khi rẽ lên / xuống và
  góc bất kỳ; ống thẳng hàng không sinh co; độ dốc; cỡ mặc định / bắt nhập; thống kê + cột hao hụt; khoảng cách hộp.

## 8. CI

- Lượt 1: run 45819860494 trên `10ba68a`, **xanh**, log `# pass 446 / # fail 0`, lõi 1621, UI 38 / 38.
- Lượt 2: run 45821183068 trên `03bc792` (số đã duyệt đưa vào mô tả / skill), **xanh**, `# fail 0`.
- Lượt 3 (thêm, vì chốt độ dốc mới và 3 lỗi thread F2 báo, luồng chính giao sau lượt 2): ghi ở dưới.

## 9. Chưa kiểm được

- AutoCAD thật:
  - `Revolve` mặt cắt chữ nhật cho co ống gió;
  - hướng xoay của co (test lõi kiểm bằng công thức, SelfTest kiểm trên AutoCAD);
  - `CheckInterference` của ống với dầm;
  - Excel.
- Chưa thử UCS nghiêng (không phải mặt bằng): tuyến theo mặt bằng khi UCS nghiêng có thể khó hiểu với khách.
