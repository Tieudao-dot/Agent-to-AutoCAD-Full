# Bàn giao nâng cấp đợt G1: ra bộ hồ sơ một lệnh

*Thread Nâng cấp đợt G1 (người viết, `session_01D56HmFcYrzsme7GoBm8tNC`), 25/11/2026. Nhánh `full/nang-cap-g1` tách từ
`full/nang-cap-f-tong-the` @ `a6da118` (code `7fe215c`). Chạy cùng lúc với G2. Thử tay: `docs/checklist-dot-g1.md`.
Kết quả CI: xem cuối file.*

## 1. Khách thấy gì

- Nói "ra bộ hồ sơ" → Claude gọi **một** lệnh `create_drawing_set` (nhãn "Ra bộ hồ sơ"), ra các tờ có khung tên của bộ
  chuẩn F2, mỗi hình một tờ, đánh số `KT-01`, `KT-02`… theo thứ tự:
  1. **mặt bằng**: có khối 3D thì mỗi tầng một hình (mặt cắt ngang cao 1,2 m trên sàn tầng đó, tường có hatch, cửa là
     khoảng hở); không có khối 3D thì khung nhìn lên hình 2D có sẵn (một tờ, hoặc mỗi vùng `plan_areas` một tờ);
  2. **mặt đứng** trước / sau / trái / phải (hoặc các hướng khách chọn);
  3. **mặt cắt** A-A (dọc theo cạnh dài, qua giữa nhà) và B-B (ngang), hoặc các đường cắt khách chỉ;
  4. **phối cảnh** nhìn từ góc tây nam (như SW Isometric), tô bóng Realistic khi in.
- Khung tên tự điền: tên bản vẽ (MẶT BẰNG TẦNG 1…), số bản vẽ, tỉ lệ, khổ giấy, ngày; công trình, đơn vị, hạng mục,
  người vẽ, kiểm tra lấy từ khách (`fields`) hoặc để trống.
- `pdf: true`: in cả bộ ra **một file PDF** `<tên bản vẽ>-bo-ho-so.pdf` cạnh bản vẽ (phần in nhiều layout của đợt C).
- Chọn được: loại hình (`kinds`), hướng mặt đứng, đường cắt (`sections`), nguồn mặt bằng (`plan_source` auto / 3d / 2d,
  `plan_areas`), khổ giấy, hướng giấy, một tỉ lệ chung, tiền tố số bản vẽ, có in PDF không.
- Chạy lại: bộ cũ (các tờ và hình do lần trước tạo) **chỉ được thay sau thẻ Đồng ý**; layout trùng tên của khách thì không
  bao giờ đụng tới (báo đổi tiền tố). PDF đã có: cùng thẻ đó hỏi ghi đè.

## 2. Cách làm (kỹ thuật)

- Lõi `plugin/ClaudeCAD.Plugin/Core/DrawingSet.cs`: `G1Numbers` (mọi số / chữ khách gặp), `DrawingSetLogic` (lập danh sách
  tờ, gói nào được phần nào, chọn tỉ lệ, chỗ vẽ từng hình trong Model, khớp phối cảnh vào khung nhìn, đọc tầng từ khối
  tường, bản ghi bộ cũ và mã thẻ). Không cần AutoCAD, có test.
- Plugin `Drawing/DrawingSetTools.cs`: chạy danh sách đó bằng các lệnh có sẵn — `section_view` (mặt bằng cắt, mặt cắt),
  `projection_view` (mặt đứng), `create_sheet` (tờ in + khung tên + khung nhìn khóa), `plot_pdf` (PDF) — rồi chỉnh khung
  nhìn: mặt bằng xoay theo UCS xoay; phối cảnh: hướng nhìn, tâm, tỉ lệ vừa khung, kiểu hiển thị, đóng băng layer không có
  khối trong khung đó; mặt bằng 2D: đóng băng layer khối 3D trong khung đó.
- Hình 2D vẽ thành một hàng bên phải mọi thứ trong Model, mỗi hình một ô rộng bằng phần Model khung nhìn thấy + khoảng hở
  30 mm trên giấy, nên tờ nào cũng chỉ thấy hình của nó.
- **Gói:** lệnh nằm nhóm Common (mọi gói gọi được), nhưng mỗi phần chỉ làm khi gói / chế độ có nhóm của **lệnh làm ra phần
  đó**: tờ in và PDF theo `create_sheet` / `plot_pdf` (2D), hình từ khối theo `section_view` / `projection_view` (3D). Máy
  2D: tờ mặt bằng 2D; máy 3D: các hình trong Model, không tờ; Full: đủ. Phần thiếu ghi trong `missing` (chế độ F1: bảo đổi
  chế độ, không nói mua gói). Không lệnh nào lách gói.
- **UCS:** lệnh lớp Ref; đọc UCS hiện hành (nằm ngang): mặt đứng trước nhìn theo +Y của UCS, đường cắt khách chỉ là điểm
  UCS, mặt bằng xoay theo UCS. Bên trong gọi các lệnh con ở WCS (`UcsScope.Suspend`) để không đổi hệ hai lần. UCS không nằm
  ngang: dùng trục thế giới và ghi chú.
- **Đơn vị:** cao cắt 1,2 m, khoảng hở, tỉ lệ đều tính theo INSUNITS (bản vẽ m / cm / mm như nhau).
- **Chỉ xem:** lệnh ghi → bị chặn như mọi lệnh ghi. **Hoàn tác:** một lượt, như mọi lệnh.
- **Macro:** ghi và chạy lại được; bản vẽ đã có bộ hồ sơ thì macro không thay (từ chối, bảo chạy trong chat để có thẻ).
- **Chạy lại:** bản ghi `CLAUDECAD_DRAWING_SET` (Xrecord trong từ điển bản vẽ) giữ tên các tờ và handle các hình của lần
  trước. Thẻ dùng một mã riêng của bộ (`DrawingSetLogic.ReplaceToken`) + mã file PDF của F5; trợ lý thêm lệnh vào
  `F5_CARD_TOOLS` (chỉ các mã trên thẻ khách đã bấm Đồng ý mới tới plugin).
- Trợ lý: `agent/src/tools/drawingset.ts` (lệnh nạp khi cần, ngoài nhóm nạp sẵn), một dòng system prompt, nhãn "Ra bộ hồ
  sơ", skill `ve-kien-truc` / `ve-3d-kien-truc` thêm một đoạn, bước tự kiểm "Nâng cấp đợt G1" trong `SelfTest`.
- File dùng chung chỉ sửa một dòng mỗi file (để gộp với G2 gọn): `Plans.cs`, `ToolLabels.cs`, `DrawingService.cs`,
  `tools/index.ts`, `confirmation.ts`, `systemPrompt.ts`, `ClaudeCAD.Core.Tests.csproj`.

## 3. Con số chờ chủ dự án duyệt

| Số / chữ | Ở đâu (để đổi) | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Số bản vẽ **`KT-01`, `KT-02`…** (tiền tố KT, 2 chữ số), cũng là tên layout | `G1Numbers.SheetPrefix`, `SheetDigits` | số bản vẽ trong khung tên, tên tab | KT = kiến trúc, cách đánh số hay gặp ở hồ sơ kiến trúc VN |
| Thứ tự tờ: mặt bằng → mặt đứng → mặt cắt → phối cảnh | `DrawingSetLogic.Build` | thứ tự KT-01… và trang PDF | thứ tự đọc hồ sơ thường gặp |
| Tên bản vẽ: "MẶT BẰNG" / "MẶT BẰNG TẦNG n", "MẶT ĐỨNG TRƯỚC / SAU / BÊN TRÁI / BÊN PHẢI", "MẶT CẮT A-A", "PHỐI CẢNH" | `G1Numbers.Title*`, `ElevationTitles` | ô tên bản vẽ | chữ quen dùng; mặt đứng theo hướng nhìn vì chưa biết trục / hướng bắc |
| Tỉ lệ mặc định **1:100** cho mặt bằng, mặt đứng, mặt cắt; to hơn (1:125, 1:150, 1:200…) khi hình không vừa; mọi mặt bằng chung một tỉ lệ, mặt đứng + mặt cắt chung một tỉ lệ | `G1Numbers.DefaultScale`, `ChooseScales` | tỉ lệ trên tờ | 1:100 là tỉ lệ hồ sơ kiến trúc nhà ở phổ biến; chung tỉ lệ để so các hình |
| Phối cảnh không tỉ lệ (ô tỉ lệ ghi **"—"**), chiếm **85 %** khung nhìn | `G1Numbers.NoScaleText`, `PerspectiveFill` | cỡ phối cảnh | phối cảnh không đo được; chừa lề |
| Phối cảnh nhìn từ **góc tây nam** (SW Isometric), kiểu **Realistic** | `DrawingSetLogic.PerspectiveDirection`, `G1Numbers.PerspectiveVisualStyle` | góc nhìn, màu khi in | hướng mặc định AutoCAD hay dùng; Realistic hiện vật liệu F3 |
| Mặt bằng = mặt cắt ngang cao **1200 mm** trên sàn mỗi tầng | `G1Numbers.PlanCutHeightMm` | thấy cửa sổ (bậu 900) là khoảng hở | thông lệ cắt mặt bằng 1–1,5 m |
| Mặt cắt mặc định: **2** (A-A dọc theo cạnh dài, B-B ngang) qua giữa nhà, đường cắt dài hơn nhà 5 % mỗi đầu | `DrawingSetLogic.DefaultSections` | vị trí mặt cắt | theo yêu cầu "một cắt dọc và một cắt ngang qua giữa nhà" |
| Mặt đứng mặc định: **4** hướng | `DrawingSetLogic.Elevations` | số tờ mặt đứng | theo yêu cầu |
| Khoảng hở giữa các hình vẽ trong Model: **30 mm** trên giấy (× tỉ lệ) | `G1Numbers.ViewGapPaperMm` | hàng hình bên phải mô hình | đủ để khung nhìn tờ này không thấy hình tờ kia |
| Tối đa **30** tờ một bộ; tối đa **10** tầng đọc từ mô hình | `G1Numbers.MaxSheets`, `MaxLevels` | bộ quá lớn bị từ chối | chặn trường hợp lỗi; nhà ở thường < 15 tờ |
| Tên file PDF **`<tên bản vẽ>-bo-ho-so.pdf`** cạnh bản vẽ | `G1Numbers.PdfSuffix`, `DrawingSetLogic.PdfName` | tên file | không dấu cho mọi máy / ổ mạng, giống kiểu tên file PDF / Excel trước |
| Khổ giấy mặc định A3 ngang (như F2) | `create_sheet` | khổ tờ | giữ đúng luật F2 |

Số kỹ thuật bên trong (không cần duyệt): đáy tường lệch < 50 mm là cùng một tầng (`LevelTolMm`); khối tường thấp hơn 1500 mm
(mảnh lanh tô khi AutoCAD không gộp được) không tính là một tầng (`LevelMinWallMm`); mặt bằng / mặt đứng dùng tối đa 500
khối (giới hạn có sẵn của `section_view`).

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Ghép các mảnh | **Một lệnh mới điều phối** (`create_drawing_set`) gọi lại lệnh cũ trong plugin: Claude gọi 1 lần thay vì ~20 lần (ít token, ít sai) | — |
| Mỗi tờ mấy hình | **Một hình một tờ** (một khung nhìn, dùng nguyên `create_sheet`) | nhiều hình một tờ: thêm bố cục nhiều khung nhìn (việc vừa) |
| Mặt bằng khi có khối 3D | **Cắt từ khối 3D** mỗi tầng (đúng "mỗi tầng một hình"); mặt bằng 2D có kích thước / chữ thì khách nói `plan_source: "2d"` | mặc định 2D: đổi luật `auto` trong `DrawingSetLogic.AddPlans` |
| Tầng đọc từ đâu | Đáy các khối trên layer tường (A-WALL-3D, tên có WALL / TUONG); không có khối tường: một tầng ở đáy mô hình | — |
| Phối cảnh | **Trục đo (song song) từ góc tây nam**, không phải phối cảnh có điểm tụ (giữ tỉ lệ vừa khung chắc chắn) | bật `PerspectiveOn` của khung nhìn |
| Mặt đứng ghi tên theo hướng nhìn (trước / sau / trái / phải) | chưa biết hướng bắc / trục công trình | ghi theo trục (1-5, A-D) khi có lưới trục |
| Hình 2D của bộ | vẽ trong Model (layer `A-SECT*` / `A-ELEV` như đợt 3), một hàng bên phải mô hình | — |
| Chạy lại | thay cả bộ sau **một thẻ** (mã riêng của bộ + mã file PDF nếu có); khách sửa tay trên tờ / hình cũ sẽ mất (thẻ nói rõ) | cập nhật từng tờ giữ chỗ khách sửa: việc lớn |
| Layout trùng số bản vẽ nhưng không thuộc bộ cũ | **không đụng**, từ chối và gợi ý tiền tố khác | đánh số tiếp sau số lớn nhất |
| Thay bộ cũ khi bản vẽ chỉ có các tờ của bộ đó | AutoCAD phải giữ ít nhất một layout: tạo layout mặc định `Layout1` trước khi xóa tờ cũ | — |
| Macro chạy trên bản vẽ đã có bộ hồ sơ | từ chối (macro không bấm thẻ được) | cho macro thay luôn |
| Máy gói 3D | vẽ các hình trong Model, không tờ in / PDF (tờ và PDF thuộc nhóm 2D) | — |
| Ghi đè PDF trong macro | theo luật `plot_pdf` (F5) | — |

## 5. Token lúc mở chat (ước tính, cùng cách đo đợt F: tên + mô tả + schema + system prompt, ~3,5 ký tự / token)

| Gói / chế độ | Trước G1, nạp gọn | Sau G1, nạp gọn | Trước G1, tắt nạp gọn | Sau G1, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 13,4k | 13,5k | 41,1k | 42,3k |
| Gói 3D | 18,8k | 18,9k | 42,6k | 43,7k |
| Full · Cả hai | 20,7k | 20,8k | 62,0k | 63,1k |
| Full · chế độ 2D | 13,5k | 13,6k | 41,2k | 42,3k |
| Full · chế độ 3D | 18,8k | 18,9k | 42,7k | 43,8k |

Nạp gọn (mặc định): lệnh mới chỉ thêm tên (+ một dòng system prompt), ~0,1k. Lúc khách nhờ ra bộ hồ sơ Claude nạp mô tả
lệnh (~1,1k) một lần; cả bộ là **một** lần gọi thay vì khoảng 20 lần gọi lệnh lẻ.

## 6. Test

- Lõi: `plugin/ClaudeCAD.Core.Tests/NangCapG1Tests.cs` (11 test): thứ tự / số / tên tờ, phần theo từng gói và chế độ, nhà
  không có khối 3D, lựa chọn của khách (loại, hướng, đường cắt, vùng mặt bằng 2D, tiền tố) và lỗi đầu vào, mặt cắt mặc
  định, đọc tầng (kể cả bản vẽ m, mảnh lanh tô), chọn tỉ lệ (1:100, nhà lớn, tỉ lệ cho, bản vẽ m), các hình không lọt sang
  tờ khác, phối cảnh vừa khung, bản ghi bộ cũ + mã thẻ + layout của khách, mã nguồn plugin gọi đúng lệnh cũ / thẻ / gói.
- Trợ lý: `agent/test/nangcapg1.test.ts` (3 test): lệnh nạp khi cần, có ở máy 2D / 3D, thẻ chỉ nhận mã đã Đồng ý, một dòng
  system prompt.
- Sau lần sửa cuối: lõi 1228 / 1228, trợ lý 367 / 367; build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` /
  `bin` giữa các lần) 0 lỗi 0 cảnh báo.

## 7. Chưa kiểm được (cần AutoCAD thật)

- Hình ra đúng trên AutoCAD: mặt bằng cắt, mặt đứng (hidden-line), mặt cắt có nét phía sau, phối cảnh (hướng, tô bóng,
  vừa khung), khung nhìn xoay theo UCS, layer bị đóng băng đúng khung.
- Tốc độ với nhà lớn (nhiều khối: mỗi hình mặt đứng / mặt cắt dùng hidden-line của AutoCAD).
- In PDF cả bộ; thẻ chạy lại; macro; bước tự kiểm G1 (không chạy trên CI).

## 8. CI

Run 45638049687 (#57) trên `28ca6c2` (commit code cuối): **xanh** (build 3 bản, test lõi, test trợ lý, test giao diện WPF
trên Windows). Commit sau đó chỉ ghi kết quả CI vào file này.
