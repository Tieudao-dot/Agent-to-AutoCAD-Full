# Bàn giao: Sửa lỗi KTP phần B: H7 + H8 + KTP-15 (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5`. Nhánh `full/nang-cap-h-sua-ktp-b`, tách từ `full/nang-cap-h-tong-the-2` @ `6bcd5b5`.
Báo cáo kiểm tra: `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-2.md`. Không chạy CI (thread đợt F2 gộp và chạy). Chưa thử trên
AutoCAD thật.

## 1. Lỗi đã sửa

| Lỗi | Sửa | Test |
| --- | --- | --- |
| KTP-13 ống gió dưới dầm bị báo "sát" chính dầm đó (nửa đường chéo) | `MepLogic.SectionReach` / `SectionBoxGap` / `SectionSectionGap`: khe hở đo theo tiết diện về phía vật kia (dưới dầm: nửa chiều cao; cạnh cột: nửa bề rộng), lấy lớn hơn khe hở hai hộp bao. Bước quyết định có chạy `CheckInterference` vẫn dùng nửa đường chéo, nên không bỏ sót chạm thật | `NangCapHSuaKtpBTests.Ktp13…` (600×300 dưới dầm 100 mm: ra 100, cách cũ ra 0) |
| KTP-14 hai tuyến nối đầu nhau bị báo "sát 0 mm" | `MepLogic.EndToEnd`: hai ống thẳng có đầu trục trùng nhau (≤ 1 mm, `H7Numbers.JoinToleranceMm`) là một mối nối, bỏ qua | `Ktp14…` |
| KTP-16 nhánh nối khác hệ; checklist A3 không ra tê | `draw_mep` từ chối `connect_to` khác hệ; lời báo "không nằm trên ống" nói tim ống chính ở đó cao bao nhiêu. Checklist A3 viết lại: nhánh thoát D60, cao độ 2780 (tim ống chính tại x = 1000) | `Ktp16…` (2780 tính lại từ `Sloped`) |
| KTP-17 không kiểm bán kính co ≤ nửa tiết diện trong mặt phẳng uốn | `MepLogic.HalfInBendPlane`. `bend_radius` khách cho không lớn hơn: từ chối, nói số tối thiểu. Bán kính mặc định của ống gió nhỏ hơn: lấy 1 × cạnh nằm ngang mặt phẳng uốn (vd 200×500 rẽ đứng: 500) và báo | `Ktp17…` |
| KTP-15 macro phát lại nối nhánh vào ống cũ | `MacroLogic.IsHandleKey` thêm `connect_to`; `mep.ts` khai `connect_to` bằng `handle` (test `CrossPlanTests` đòi khóa giống handle phải có kiểu handle) | `Ktp15…` |
| KTP-27 Excel `mep_report` / `rebar_schedule` ra tên "…-bang.xlsx" | `F4Numbers`: `mep_report` → "ong-mep" / sheet "Thống kê ống MEP"; va chạm → file riêng "va-cham-ong-mep" / "Va chạm ống MEP" (khóa `F4Numbers.MepClashKey`, `ExportExcel(…, key)`); `rebar_schedule` → "thong-ke-thep" / "Thống kê thép" | `Ktp27_28_29…` |
| KTP-28 Excel va chạm ghi mã hệ tiếng Anh | Cột "Hệ" ghi `MepLogic.SystemName` ("Cấp nước", "Ống gió cấp"…); JSON cho Claude giữ mã | như trên |
| KTP-29 trộn điểm 2D / 3D; có z mà vẫn cho `elevation` | Từ chối cả hai, nói cách cho điểm | như trên (soát mã nguồn) |
| KTP-30 tài liệu H7 | `nang-cap-h7.md` §1 + §4: đo từ trục ống theo tiết diện; checklist C5 "phẳng nhất"; mục D đánh số lại, thêm D7–D9 | — |
| KTP-18 móng băng `rib: false` luôn lỗi | `rib` là `true` / `false` thì không đọc như mảng số; `true` = sườn mẫu | `NangCapH8Tests.Ktp_…` (soát mã nguồn) |
| KTP-19 đai lệch về trái | `StirrupPositions` đối xứng: đai đầu cách mỗi gối 50, a150 qua mỗi vùng 1/4 nhịp, đoạn giữa chia đều (≤ a200) | `Ktp_…` (4 nhịp: đối xứng, hai đầu bằng nhau) |
| KTP-20 khe hở thép dầm so tâm–tâm | Khe hở **thông thủy** (tâm–tâm trừ một thanh) phải ≥ một thanh: 5Ø20 trong dầm 200 bị từ chối | `Ktp_…` |
| KTP-21 móc 180° thép trơn cộng 8,5d | **Luồng chính chọn 26/11:** cộng **6,25d** mỗi móc (quy ước thường dùng; số đã duyệt uốn 2,5d, đoạn thẳng 3d giữ nguyên). `H8Numbers.PlainHookAddFactor = 6.25`. Sàn 3000 × 4000 Ø8a200: L = 3060 (như tính tay) | `Ktp_…`, test cũ sửa số |
| KTP-31 nhịp < 100 mm ra lỗi AutoCAD thô | `H8Numbers.MinSpanMm = 100` (số luồng chính đưa trong tin giao việc): báo rõ | `Ktp_…` |

## 2. Nghi vấn đã xem

| Nghi vấn | Kết quả |
| --- | --- |
| `IsStructureLayer` nhận quá rộng | Sửa: "S-" chỉ khi đứng đầu tên layer (trước đây "A-WALLS-NEW" cũng là kết cấu). BEAM / COL / DAM / COT giữ như cũ |
| `under_beams` tuyến chữ L | Sửa: chỉ tính dầm nằm trên **một đoạn** của tuyến, không phải dầm trong hộp bao cả tuyến |
| Hộp bao dầm chéo | Để lại: dầm chéo có hộp bao to, `under_beams` / "sát" với co, tê có thể lấy dầm không ở ngay trên. Ống thẳng soát "sát" đã đo theo trục |
| Tê nhánh gần dọc trục; hai nhánh vào cùng điểm | Để lại: vẽ được, tê chồng nhau; khách thấy trên hình |
| Bảng thép thiếu dòng tổng theo loại thép | Sửa: thêm dòng "Cộng CB240-T" / "Cộng CB400-V" sau các dòng "Cộng Ø…" |
| Đai móc 135° chưa cộng phần cong | Để lại: chiều dài đai = chu vi tim + 2 đoạn thẳng max(6d, 75) (số đã duyệt), phần cong 135° không cộng. Cần đổi thì chủ dự án chốt số |

## 3. Quyết định tự chọn

- KTP-13: khe hở báo ra đo theo tiết diện về phía vật kia (hướng từ điểm gần nhất trên trục tới vật); việc có chạy kiểm khối
  chạm (`CheckInterference`) vẫn dùng nửa đường chéo như cũ, để không bỏ sót chạm thật.
- KTP-17: bán kính mặc định của ống gió chỉ tăng khi cần (co rẽ theo cạnh lớn hơn bề rộng), và báo cho khách.
- KTP-21: cách tính +6,25d mỗi móc (luồng chính chọn). Thép trơn đo tới mép lớp bảo vệ rồi cộng 6,25d mỗi đầu.
- KTP-27: thêm luôn tên file cho `rebar_schedule` (cùng lỗi "…-bang.xlsx").

## 4. Test

- Lõi 1695 / 1695 (thêm 8: `NangCapHSuaKtpBTests.cs` 7, `NangCapH8Tests` 1; sửa số ở test H8 cũ: móc 6,25d, đai đối xứng,
  dòng cộng theo loại thép).
- Trợ lý 466 / 466 (`# fail 0`).
- Build `net48` / `net8.0-windows` / `net10.0-windows`: 0 lỗi.
- Chạy lại đủ cả ba sau lần sửa cuối. CI: thread đợt F2 chạy khi gộp.
