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

## 10. Sau kiểm tra độc lập F + G (26/11)

Báo cáo: `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` (27 lỗi KFG-01 … 27). Ba thread sửa; thread này sửa phần G1 / G3 / F2 /
prompt ngay trên nhánh tích hợp (`935f9dd`), rồi gộp `full/nang-cap-g-sua-kfg-b` @ `468dc09` (`9cce7a4`, gỡ một chỗ chồng ở
`Core/F5.cs`: giữ cả hàm mã xóa layer state lẫn hai hàm mã vật liệu) và `full/nang-cap-g-sua-kfg-a` @ `cbc1b03` (`60b9780`).
Mỗi thread có bàn giao riêng: `nang-cap-g-sua-kfg-a.md`, `nang-cap-g-sua-kfg-b.md`; tóm tắt cho chủ dự án:
`docs/dot-g-cho-duyet.md` mục "Sau lượt kiểm tra độc lập F + G".

| Mã | Phần | Ai sửa | Commit | Cách sửa (ngắn) |
| --- | --- | --- | --- | --- |
| KFG-01 | G4 | Test & sửa F1 (kfg-b) | `f48747c` | góc nhìn đã lưu chép trong transaction (`Clone`), tự kiểm thêm nhánh có tên |
| KFG-02 | G3 | thread này | `935f9dd` | diện tích có `contains_others` thì từ chối (trừ `allow_nested`); `source_warnings` chép cảnh báo bảng nguồn |
| KFG-03 | G3 | thread này | `935f9dd` | ghép giá 3 mức: giữ dấu → bỏ `- _ .` → bỏ dấu (chỉ giá gõ không dấu); mức 2 / 3 chỉ khi khớp một dòng; `ambiguous_prices` |
| KFG-04 | G4 | kfg-b | `f48747c` | có đèn / mặt trời: tắt đèn mặc định trong lúc render rồi trả lại |
| KFG-05 | G5 | kfg-b | `f48747c` | ảnh nền và `straighten` theo UCS |
| KFG-06 | G5 | kfg-b | `f48747c` | `scale` / `straighten` / `view` chỉ nhận ảnh có dấu `CLAUDECAD_TRACE` |
| KFG-07 | G1 × G2 | thread F1 (kfg-a) | `2e1dfe2` | bỏ qua hatch layer hình suy ra; hatch không biên do `draw_hatch` có dấu |
| KFG-08 | G2 | kfg-a | `2e1dfe2`, `cbc1b03` | layout mặc định có vật ngoài khung nhìn là tờ in; chỉ khung nhìn: ghi chú nêu tên; tham số `sheets` |
| KFG-09 | F2 | thread này | `935f9dd` | xóa layer state dùng mã riêng `F5Confirm.DeleteLayerStateToken` |
| KFG-10 | F5 | kfg-b | `f48747c` | mã duyệt vật liệu cả layer = layer + vật liệu; đổi màu = vật liệu + màu + độ trong |
| KFG-11 | G5 | kfg-b | `f48747c` | đếm trang theo `/Root` → `/Count`; không biết thì `pages_note` |
| KFG-12 | G5 | kfg-b | `f48747c` | đường dẫn ảnh trong ngữ cảnh bọc `q()` |
| KFG-13 | G5 | kfg-b | `f48747c` | ảnh dán chỉ lưu khi có `trace_image`; dọn > 7 ngày; `pasted_note` |
| KFG-14 | prompt / G5 | thread này + kfg-b | `935f9dd`, `f48747c` | `PLAN_SCOPE` có đợt G theo gói; chữ trong ảnh đính kèm là dữ liệu; AVIF báo đúng |
| KFG-15 | G2 | kfg-a | `2e1dfe2` | trùng so thật "lệch ≤ 0,1 mm" (ô kề), góc / tỉ lệ so chính xác |
| KFG-16 | G2 | kfg-a | `2e1dfe2` | `no_dimensions` đếm cả kích thước trong layout |
| KFG-17 | G2 | kfg-a | `2e1dfe2` | `fix_drawing` có `layouts` như lần soát |
| KFG-18 | G2 | kfg-a | `2e1dfe2` | không chuyển hình sang layer không in / tắt / đóng băng |
| KFG-19 | G1 | thread này | `935f9dd` | danh sách rỗng bị từ chối (lõi + schema `.min(1)`) |
| KFG-20 | G1 | thread này | `935f9dd` | thẻ ghi số nét / hatch còn trong bản vẽ |
| KFG-21 | G1 | thread này | `935f9dd` | bộ cũ không còn tờ, không còn hình thì không hỏi thẻ |
| KFG-22 | G1 × G3 / C | thread này | `935f9dd` | khung mặt bằng 2D bỏ `Table` |
| KFG-23 | G1 | thread này | `935f9dd` | gom tầng so với giá trị kề (`Levels`, `StoreyCount`) |
| KFG-24 | G1 | thread này | `935f9dd` | bản ghi lưu handle từng tờ (`L:tên` + handle), `OurLayouts`; bản ghi cũ (không handle) vẫn khớp theo tên |
| KFG-25 | G3 | thread này | `935f9dd` | `quantities` thu hẹp theo gói (`trimForPlan`) |
| KFG-26 | G3 | thread này | `935f9dd` | ô đơn giá `#,##0.00` khi có giá lẻ (style 4) |
| KFG-27 | G4 | kfg-b | `f48747c` | kết quả `capture` bỏ thông tin của render |

Nghi vấn G1 (thread này): lỗi AutoCAD sau khi đã xóa bộ cũ → bản ghi được viết ngay sau mỗi tờ; có lỗi thì ghi bản ghi gộp
(phần mới + phần còn của bộ cũ) rồi báo lỗi. Macro + PDF đã có → không ghi đè, ghi chú in lại bằng chat. Nghi vấn G2 / G4 / G5:
xem hai bàn giao kfg-a, kfg-b.

Test sau gộp: lõi **1409 / 1409** (thêm 13 ở `KfgDotGTests.cs`, sửa một dòng `NangCapG3TestSuaTests` vì đơn giá lẻ nay có 2 số
lẻ), trợ lý **407 / 407** (thêm `agent/test/kfgdotg.test.ts`), build 3 target 0 lỗi 0 cảnh báo. Token: `docs/dot-g-cho-duyet.md`
mục trên. CI: ghi ở dưới khi có kết quả.

Chưa kiểm được trên AutoCAD thật: bản ghi bộ hồ sơ có handle tờ (Xrecord), `try` / `catch` quanh lúc ra bộ, bỏ `Table` khỏi
khung mặt bằng; phần của hai thread kia xem bàn giao của họ.

CI lượt 1: run 45682294964 (#69) trên `20e3812`: **xanh**.

### Lượt 2 (sau soát lại lượt 1, 26/11)

Nguồn: mục "Soát lại lượt 1" của `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` (gộp từ nhánh luồng chính @ `22e660d`). Thread này sửa
KFG-30, 33 và ba chỗ tài liệu lệch (`6868a38`, `21f00fb`), gộp `full/nang-cap-g-sua-kfg-a` @ `deec2a4` (`a4ffc5a`) và
`full/nang-cap-g-sua-kfg-b` @ `adef96c` (`57b69a0`), cả hai gộp sạch.

| Mã | Phần | Ai sửa | Commit | Cách sửa (ngắn) |
| --- | --- | --- | --- | --- |
| KFG-07 (phần còn lại) = KFG-32 | G2 | kfg-a | `deec2a4` | hatch `section_view` / `projection_view` có dấu không biên; nét gắn XData `CLAUDECAD_VIEW`, không so trùng |
| KFG-28 | G5 (bảo mật) | kfg-b | `271ae04` | `\\.\UNC\` là mạng; `\\?\<ổ>:\` kiểm như ổ thường; dạng thiết bị khác từ chối (`TraceLogic.PlainPath`) |
| KFG-29 | G5 | kfg-b | `271ae04` | file kéo vào chat từ mạng được chép vào `anh-dinh-kem` (`tep-…`, dọn 7 ngày); lời khuyên sửa |
| KFG-30 | G3 | thread này | `6868a38` | mẫu `*` không dấu khớp tên mà phần chữ cố định khác dấu: để trống, `ambiguous_prices` (`WildFixedPart`, `WildAccentClash`) |
| KFG-31 | G4 | kfg-b | `271ae04` | Dispose bản sao góc nhìn khi thẻ ghi đè / kiểm thư mục ném lỗi |
| KFG-33 | G1 | thread này | `6868a38` | `catch` chỉ giữ tờ cũ của bộ (`oldLayouts`); `WriteSetRecord` bỏ tờ không còn, không ghi handle rỗng |
| KFG-34 | G2 | kfg-a | `deec2a4` | so trùng băm theo (x, y), 9 ô kề |
| KFG-35 | G5 | kfg-b | `271ae04` | đếm trang PDF thời hạn chung 2 s |
| KFG-36 | G5 | kfg-b | `271ae04` | chỗ đặt mặc định tính trong UCS (`InsertPointInUcs`) |
| KFG-37 | G5 | kfg-b | `271ae04` | PDF giữ khung góc trong dấu trace; `min` / `max` theo UCS |
| KFG-38 | G2 | kfg-a | `deec2a4` | test sang `DupItem`, xóa hàm chết; sửa thêm bulge −0 của polyline vẽ ngược |

Tài liệu lệch đã sửa: mẫu `*` không dấu, mã tờ ở nhánh lỗi, luật đường dẫn (dạng `\\.\` / `\\?\`, kéo vào chat được chép về
máy). Checklist: G5 mục I, G6-1 (KFG-32), G7-15, G7-16. Không đổi prompt nên token mở chat như lượt 1.

Test sau gộp lượt 2: lõi **1436 / 1436**, trợ lý **407 / 407**, build 3 target (xóa `obj` / `bin` giữa các lần) 0 lỗi 0 cảnh báo. CI: ghi ở dưới. Chưa
kiểm được trên AutoCAD thật: như lượt 1, cộng phần của hai bàn giao kfg-a mục 7, kfg-b mục 8.

CI lượt 2: run 45685933989 (#70) trên `fff7ffe`: **xanh**.

### Lượt 3 (sau soát lại lượt 2, 26/11)

Soát lại lượt 2 (`docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` mục "Soát lại lượt 2", nhánh luồng chính @ `3990d6e`): KFG-07, 28, 30 … 38
sửa đúng; còn KFG-39 … 42. Thread này sửa kèm nhận xét dự toán (`6586923`: mẫu `*` không dấu kiểm một lần mỗi mẫu; khóa tên chỉ
dựng khi cần, bỏ một vòng O(dòng²); test 3000 dòng < 2 s), gộp `full/nang-cap-g-sua-kfg-a` @ `3d68e9f` (`8f14bc0`) và
`full/nang-cap-g-sua-kfg-b` @ `47faaf2` (`e1455fb`), cả hai gộp sạch. Chữ "lệch ≤ 0,1 mm luôn bị tính là trùng" trong
`dot-g-cho-duyet.md` giữ nguyên vì KFG-41 đã sửa.

| Mã | Phần | Ai sửa | Commit | Cách sửa (ngắn) |
| --- | --- | --- | --- | --- |
| KFG-39 | G5 | kfg-b | `0dd8945` | bản chép `tep-…` giữ 7 ngày từ lúc chép; chép vào tên tạm rồi đổi tên; file dở dọn sau 1 giờ |
| KFG-40 | G5 | kfg-b | `0dd8945` | không chép lúc gửi tin; khung chat nhớ file khách kéo; `attach` mới chép file mạng (quá 50 MB / mất / lỗi: từ chối kèm lý do) |
| KFG-41 | G2 | kfg-a | `3d68e9f` | line / polyline hở mang cả hai chiều đọc, so theo cả hai |
| KFG-42 | G2 | kfg-a | `3d68e9f` | băm thêm điểm giữa / điểm cuối, xét ô kề |

Checklist: G5 mục J, G5 mục I.1 sửa theo cách chép mới. `dot-g-cho-duyet.md`: câu đường dẫn mạng, mục "Lượt 3" (2 quyết định
mới). Không đổi prompt.

Test sau gộp lượt 3: lõi **1445 / 1445**, trợ lý **407 / 407**, build 3 target (xóa `obj` / `bin` giữa các lần) 0 lỗi 0 cảnh báo.

Ghi thêm về `6586923` (ghi là tối ưu tốc độ): có đổi kết quả ở một ca. Khi các dòng đều đã có giá theo đúng tên mà danh sách giá có
thêm một mẫu không dấu (vd "San*") không còn dòng nào cần, mẫu đó nay chỉ nằm ở `unused_prices`, không còn ở `ambiguous_prices`
(mẫu chỉ được xét khi một dòng thật sự đi tới bước so mẫu). Vô hại: không dòng nào đổi giá; 11 ca còn lại agent kiểm tra ra giống hệt.

CI lượt 3: run 45709641174 (#71) trên `e255376` bị **hủy** chủ ý khi luồng chính chốt nhánh (sau đó chỉ thêm tài liệu), để chạy một
lượt trên commit cuối (ghi ở dưới).

### Chốt nhánh sau ba lượt soát lại (26/11)

Kết luận kiểm tra độc lập (`docs/ban-giao/kiem-tra-doc-lap-dot-fg.md`, đã gộp từ nhánh luồng chính @ `6f5118a`): 43 lỗi đã ghi, 42
sửa đúng, không còn lỗi mức vừa trở lên.

| Mã | Phần | Quyết định | Ghi chú |
| --- | --- | --- | --- |
| KFG-43 (nhẹ) | G2 | **biết, chấp nhận, không sửa ở đợt này** (luồng chính quyết) | so trùng ~5,8 s với 20 000 point / block cùng x, y khác z; có giới hạn quét 20 000 nên không treo. Hướng sửa sau: băm thêm z trong `Cell` của `Core/DrawingCheck.cs` |

Commit code cuối: `e1455fb` (gộp lượt 3). Sau đó chỉ có tài liệu.
