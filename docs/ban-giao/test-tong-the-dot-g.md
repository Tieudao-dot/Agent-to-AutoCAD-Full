# Bàn giao: Test tổng thể đợt G (G1 → G5)

*Thread `session_01D56HmFcYrzsme7GoBm8tNC`, 25–26/11/2026. Nhánh `full/nang-cap-g-tong-the` tách từ `full/nang-cap-g5-test`
@ `464b6e5` (đã chứa `g1-test` `99aaab7`, `g2-test` `f49e717`, `g3-test` `4061a6c`, `g4-test` `29a2e6c`), gộp 6 file tài liệu
của luồng chính (`claude/inspiring-cray-58xsmt` @ `fdfab46`, giữ bản luồng chính). Thread này **giữ nhánh tích hợp đợt G**:
lỗi của kiểm tra độc lập F + G chuyển về đây gộp và chạy CI. Chưa chạy trên AutoCAD / Claude thật.*

## 1. Đã rà chiều ngang

| Điểm | Kết quả |
| --- | --- |
| Bảng lệnh theo gói (`Core/Plans.cs`) | `create_drawing_set`, `cost_estimate` Common (mỗi phần bên trong theo gói của lệnh làm ra nó); `check_drawing`, `fix_drawing`, `trace_image` 2D; render là `named_view` (3D). Nút chế độ F1: các phần thiếu nói "đổi chế độ", không nói "mua gói". Có test |
| Nhãn lệnh, đăng ký trợ lý, nạp khi cần | 5 lệnh mới đều có một nhãn, một khai báo ở trợ lý, **không** nằm trong nhóm luôn nạp (25 lệnh như F4). Có test (`TongTheDotGTests`) |
| Thẻ xác nhận | Ghi đè file / thay bộ hồ sơ / sửa bản vẽ dùng chung luật F5 (`F5_CARD_TOOLS` có đủ `create_drawing_set`, `cost_estimate`, `check_drawing`, `fix_drawing`, `named_view`); `trace_image` không ghi đè file (ảnh dán chép theo mã nội dung). Có test |
| "Chỉ xem" | Lệnh ghi bị chặn: `create_drawing_set`, `fix_drawing`, `trace_image attach / scale / straighten`, `cost_estimate` khi vẽ bảng. Lệnh đọc chạy được: `check_drawing` (cả Excel), `cost_estimate` (số + Excel), render (nền / ánh sáng của bản vẽ), `trace_image view / info` |
| Macro | Không ghi: `fix_drawing`, `trace_image`, render (lệnh đọc). Ghi: `create_drawing_set` (không thay bộ đã có khi phát lại), `cost_estimate` khi vẽ bảng (phát lại không ghi Excel). Có test |
| Tự kiểm | Một bước mỗi đợt G1 → G5 trong `SelfTest.cs` (không chạy trên CI). Có test đếm |
| System prompt / skill / README | Mỗi lệnh một dòng prompt (G4 một câu trong dòng vật liệu), skill `ve-kien-truc` / `ve-3d-kien-truc` có mục của từng đợt, `bo-cai/README.md` một dòng mỗi đợt. Không mâu thuẫn |
| Tên file xuất | `-bo-ho-so.pdf`, `-du-toan.xlsx`, `-soat-ban-ve.xlsx`, `-<góc>-render.png` / `-hien-tai-render.png`, `-anh-goc-<mã>.<đuôi>` và các file F4: không trùng nhau. Có test |
| UCS / đơn vị / U | G1 Ref (mặt bằng xoay theo UCS), G2 đọc theo WCS, G3 không có điểm (trừ chỗ đặt bảng), G4 WCS (góc nhìn), G5 Ref (sửa ở G5-test). Đơn vị theo INSUNITS ở cả 5 đợt. Lệnh ghi qua dispatcher nên một U hoàn tác cả lượt |

## 2. Lỗi tìm được và đã sửa (chéo giữa các đợt)

| Mã | Lỗi | Sửa | Test |
| --- | --- | --- | --- |
| TTG-1 | `check_drawing` (G2) báo khung nhìn của tờ **PHỐI CẢNH** do `create_drawing_set` (G1) tạo là "Khung nhìn có tỉ lệ không chuẩn" mỗi lần soát: khung nhìn 3D có tỉ lệ vừa khung, không phải tỉ lệ in | Chỉ kiểm tỉ lệ khung nhìn nhìn thẳng xuống (`DrawingCheckLogic.IsPlanView`) | lõi |
| TTG-2 | `area_schedule` (đợt C) và `cost_estimate quantities: "areas"` (G3) khi không chỉ layer / handle đếm cả **hình mặt cắt / mặt đứng** (polyline kín + hatch trên A-SECT, A-SECT-PAT, A-SECT-BACK, A-ELEV) do bộ hồ sơ (G1) hoặc `section_view` vẽ ra: bảng diện tích theo layer có thêm dòng A-SECT…, theo phòng có thêm "(chưa có tên)", dự toán theo diện tích có dòng thiếu giá thừa | Không chỉ layer / handle thì bỏ các layer đó (`Util2DLogic.IsDerivedViewLayer`), kết quả ghi `left_out_layers`; khách nêu các layer đó trong `layers` thì vẫn đếm | lõi |

Đã kiểm, không phải lỗi: `check_drawing` với tờ `KT-xx` (khung + khung tên + khung nhìn khóa, tỉ lệ chuẩn) và layer A-SECT /
A-ELEV / KHUNG / X-ANH-GOC (đều thuộc bảng chuẩn); ô khung tên trống của bộ hồ sơ là gợi ý "cân nhắc" đúng; bảng dự toán vẽ
trong bản vẽ dùng cỡ chữ của bảng đợt C; tờ phối cảnh đóng băng mọi layer không có khối 3D (gồm X-ANH-GOC); hình lạc xa của G2
với hàng hình của G1: mỗi hình có nhiều nét nên nằm trong phần chính.

## 3. Token lúc mở chat (ước tính)

Cách đo như test tổng thể đợt F (tên + mô tả + schema các lệnh nạp đủ, tên các lệnh hoãn nạp, ToolSearch ~2 000 ký tự, system
prompt; ~3,5 ký tự / token). Cuối đợt F = `a6da118`; cuối đợt G = nhánh này.

| Gói / chế độ | Cuối F, nạp gọn | Cuối G, nạp gọn | Cuối F, tắt nạp gọn | Cuối G, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 13,4k | 13,7k | 41,1k | 45,2k |
| Gói 3D | 18,8k | 19,0k | 42,6k | 45,0k |
| Full · Cả hai | 20,7k | 21,0k | 62,0k | 66,5k |
| Full · chế độ 2D | 13,5k | 13,7k | 41,2k | 45,2k |
| Full · chế độ 3D | 18,8k | 19,0k | 42,7k | 45,1k |

Số lệnh: 2D 104 → 109, 3D 92 → 94, Full 137 → 142; lệnh nạp đủ lúc mở chat không đổi (19 / 22 / 25).

## 4. Checklist thử tay

Gộp `checklist-dot-g1.md` … `-g5.md` vào khối **"Đợt G"** cuối `docs/checklist-tong-the.md` (~3 giờ), các file riêng để dòng trỏ
sang. Mục **G0** (làm trước, ~35 phút) gom các điểm rủi ro nhất chưa kiểm được: bước tự kiểm G1 → G5 (dòng G4, G5), thứ tự câu
hỏi của `-RENDER`, render thật / đường lui, ảnh xem G5 (ảnh và PDF), thẻ chạy lại của bộ hồ sơ, mở file dự toán bằng Excel thật.
Thêm mục **G6** (chéo giữa các đợt: TTG-1, TTG-2, vật liệu + phối cảnh + render, tên file, UCS xoay).

## 5. Soát `docs/dot-g-cho-duyet.md`

Các số của G1 → G5 khớp code (`G1Numbers`, `G2Numbers`, `G3Numbers`, `G4Numbers` / `RenderLogic.Qualities`, `G5Numbers` /
`TRACE_MAX_VIEWS`). Đã sửa trong file:
- đầu file: ghi đã soát;
- G4: đoạn "điểm rủi ro nhất" (sau lần rà, thứ tự câu hỏi khác thì phần lớn trường hợp ra ảnh chụp ngay, chỉ lệnh đứng chờ nhập
  mới chờ tới tối đa); tên file khi không nói góc nhìn (`-hien-tai-render.png`);
- G5: đoạn rủi ro (ảnh xem dựng từ file gốc; PDF vẫn qua ảnh chụp), 4 quyết định tự chọn của lần rà, mục "Sau khi rà" (5 lỗi,
  2 số kỹ thuật mới);
- thêm mục "Test tổng thể đợt G" (2 lỗi chéo, 2 quyết định, bảng token).

## 6. Chưa kiểm được (gom cả 5 đợt)

- **G1:** hình mặt bằng / mặt đứng / mặt cắt / phối cảnh ra đúng trên AutoCAD; khung nhìn xoay theo UCS; layer đóng băng đúng
  khung; in PDF cả bộ; thẻ chạy lại; macro; tốc độ nhà lớn; lỗi lạ giữa chừng sau khi bộ cũ đã xóa (một U hoàn tác cả lượt).
- **G2:** trạng thái xref / ảnh, khung nhìn, khung tên trên AutoCAD thật; tốc độ quét 20 000 hình.
- **G3:** bảng vẽ trên AutoCAD; file mở bằng Excel / LibreOffice / Google Sheets (công thức tính lại, dấu tách theo máy); Claude
  thật có giữ luật không tự đặt giá khi khách ép.
- **G4:** thứ tự câu hỏi thật của `-RENDER` (2022 – 2027) và tên mức chất lượng; AutoCAD có trả lời plugin trong lúc render
  không; Dừng / Esc ngắt được render không; nền trắng / trời có vào ảnh; `-RENDER` có ghi cài đặt vào bản vẽ không (kể cả Chỉ
  xem); thời gian render thật; quyền ghi `C:\ProgramData`.
- **G5:** gắn ảnh / PDF (`RasterImageDef.Load`, `PdfDefinition.Load`), PDF scan có hiện trong ảnh chụp không, U hoàn tác ảnh nền,
  đường dẫn có dấu tiếng Việt; Claude thật đọc ảnh tốt tới đâu, số lần xem thực tế, độ lệch kích thước.
- Bước tự kiểm G1 → G5 (không chạy trên CI).

## 7. Chỗ để đổi số

| Đợt | File / chỗ |
| --- | --- |
| G1 | `plugin/ClaudeCAD.Plugin/Core/DrawingSet.cs`: `G1Numbers`, `DrawingSetLogic.Elevations`, `DefaultSections`, `PerspectiveDirection` |
| G2 | `plugin/ClaudeCAD.Plugin/Core/DrawingCheck.cs`: `G2Numbers`, `DrawingCheckLogic.Groups`, `KnownLayers` |
| G3 | `plugin/ClaudeCAD.Plugin/Core/Estimate.cs`: `G3Numbers`, `EstimateLogic.Header` |
| G4 | `plugin/ClaudeCAD.Plugin/Core/Render.cs`: `G4Numbers`, `RenderLogic.Qualities`, `CommandScript` (câu trả lời `-RENDER`), `MaterialLibraryLogic.Words`; trợ lý `agent/src/tools/render.ts` `RENDER_POLL_MS` |
| G5 | `plugin/ClaudeCAD.Plugin/Core/Trace.cs`: `G5Numbers`; trợ lý `agent/src/tools/trace.ts` `TRACE_MAX_VIEWS` |
| Chéo | `Core/Util2D.cs` `IsDerivedViewLayer` (layer bị bỏ khỏi bảng diện tích); `Core/DrawingCheck.cs` `IsPlanView` |

## 8. Test

- Thêm `plugin/ClaudeCAD.Core.Tests/TongTheDotGTests.cs` (4 test): mỗi lệnh G có mặt đúng một lần ở mọi bảng (gói, nhãn, trợ lý,
  không luôn nạp, prompt, thẻ F5, macro, tự kiểm, README); tên file 5 đợt không trùng; tờ phối cảnh không bị báo tỉ lệ; bảng
  diện tích bỏ layer hình vẽ từ mô hình 3D.
- Sau lần sửa cuối: lõi 1368 / 1368, trợ lý 400 / 400; build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa
  các lần) 0 lỗi 0 cảnh báo.

## 9. CI

Run 45671817871 (#68) trên `4fbb2a2` (commit code cuối): **xanh**. Dùng 1 / 2 lượt. Commit sau đó chỉ là tài liệu (gộp
`e3eda7b` bàn giao G5-test, ghi CI vào file này).
