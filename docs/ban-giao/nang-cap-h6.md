# Bàn giao: nâng cấp đợt H6 — hạ tầng (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người viết). Nhánh `full/nang-cap-h6`, tách từ `full/nang-cap-h-tong-the` @ `76ecfae`
(chốt phần 1). Chạy cùng lúc với H7 (MEP 3D). Thử tay: `docs/checklist-dot-h6.md`. Chưa chạy trên AutoCAD thật.

**Số theo tiêu chuẩn đã duyệt 26/11** (chủ dự án chọn "dùng hết số đề xuất", ghi ở `docs/dot-h-so-tieu-chuan.md` mục "Kết quả
duyệt"). Số ở một chỗ: `H6Numbers` (`plugin/ClaudeCAD.Plugin/Core/Terrain.cs`); skill `ve-ha-tang` mục 7 và mô tả lệnh `terrain`
nhắc lại (đổi số thì đổi cả ba chỗ; test `NangCapH6Tests` giữ cho chúng khớp). Khoảng cao đều đồng mức và khoảng cọc chi tiết
20 m: **số mặc định, kỹ sư đối chiếu TCVN hiện hành trước khi dùng** (ghi trong skill và mô tả lệnh).

## 1. Khách thấy gì

Một lệnh `terrain` (gói 2D / Full, hoãn nạp), năm việc:

| Việc | Khách nói | Kết quả |
| --- | --- | --- |
| `import_points` | "nhập điểm khảo sát từ file này" (CSV / TXT / .xlsx) | Mỗi điểm một dấu chấm ở đúng cao độ (layer `C-SURVEY-PT`), chữ cao độ `+12.350` (`C-LEVEL`), tên điểm (`C-COORD`). Đổi trục VN-2000 (X Bắc → y, Y Đông → x, m × 1000). Tọa độ thật (hàng trăm nghìn mét): Claude hỏi vẽ theo tọa độ thật hay gốc cục bộ; gốc cục bộ thì ghi dòng "Gốc tọa độ: X = …, Y = … (VN-2000)". Cột X / Y có vẻ đảo: cảnh báo |
| `contours` | "vẽ đường đồng mức" | Lưới tam giác từ các điểm, đường con `C-TOPO-MINR`, đường cái `C-TOPO-MAJR` nét đậm có ghi số; mỗi đường ở đúng cao độ (Elevation) |
| `stations` | "ghi lý trình tuyến này" | Vạch vuông góc + chữ `Km0+050.00` (`C-STATION`), có cọc đầu / cuối / đỉnh |
| `profile` | "vẽ trắc dọc" (có thể kèm đường thiết kế) | Khung trắc dọc dưới bản vẽ: đường tự nhiên, MSS, bảng (cao độ thiết kế, cao độ tự nhiên, chênh cao, khoảng cách lẻ / cộng dồn, tên cọc), độ dốc thiết kế |
| `cross_sections` | "vẽ trắc ngang tại các cọc" | Mỗi cọc một trắc ngang, xếp thành cột: tim, đường tự nhiên, MSS, bảng cao độ / khoảng cách, tiêu đề cọc + lý trình |

- Mô tả lệnh và prompt nói rõ: bản vẽ hỗ trợ, kỹ sư vẫn kiểm; **không tính khối lượng đào đắp**.
- Không ghi file nào (chỉ vẽ vào bản vẽ, `U` một lần hoàn tác cả lượt), nên **không có thẻ F5**. "Chỉ xem": từ chối.

## 2. Code

| File | Việc |
| --- | --- |
| `Core/Terrain.cs` (mới) | `H6Numbers`; đọc CSV / TXT (phẩy, chấm phẩy, tab, khoảng trắng, dấu phẩy thập phân) và .xlsx (sheet đầu), nhận cột theo tiêu đề tiếng Việt / Anh hoặc vị trí; đổi trục; lưới tam giác Delaunay (Bowyer–Watson quét theo x) + nội suy; đồng mức (nối đoạn thành polyline, đường kín / hở); cọc (Km, H1…H9, C1…, Đ1…, Đầu, Cuối); MSS; bố trí trắc dọc / trắc ngang thành các nét + chữ |
| `Drawing/TerrainTools.cs` (mới) | lệnh `terrain`: đọc file (luật đường dẫn G5), vẽ điểm / chữ / đồng mức / vạch lý trình / khung trắc |
| `Core/Plans.cs`, `Core/Ucs.cs`, `DrawingService.cs`, `UI/ToolLabels.cs`, `SelfTest.cs` | nhóm 2D, giữ WCS (tọa độ khảo sát là tọa độ thật), đăng ký, nhãn "Hạ tầng: điểm đo, đồng mức, trắc", bước tự kiểm (mỗi chỗ 1 – 2 dòng) |
| `skills/ve-ha-tang/SKILL.md` | mục 7 mới: lệnh `terrain` và các số mặc định đã duyệt (máy nâng cấp nhận bản mới nếu khách chưa sửa skill, theo H1) |
| `agent/src/tools/terrain.ts` (mới), `index.ts`, `systemPrompt.ts` (1 dòng ở phần 2D), `ipc/rpc.ts` (`terrain` vào nhóm lệnh chậm 2 phút) | |

File dùng chung đã sửa (để thread gộp H6 + H7 gỡ): `Plans.cs`, `Ucs.cs`, `DrawingService.cs`, `ToolLabels.cs`, `SelfTest.cs`,
`index.ts`, `systemPrompt.ts`, `rpc.ts`, csproj test. Mỗi chỗ thêm 1 – 3 dòng ngay sau dòng của H5.

## 3a. Số theo tiêu chuẩn — đã duyệt 26/11 (bảng H6 của `docs/dot-h-so-tieu-chuan.md`)

Tất cả ở `H6Numbers`, nhóm đầu:

| Số | Đang gán | Ở đâu |
| --- | --- | --- |
| Tỉ lệ trắc dọc | ngang 1:1000, đứng 1:100 | `ProfileScaleH`, `ProfileScaleV` |
| Tỉ lệ trắc ngang | 1:200 | `CrossScale` |
| MSS | mét chẵn, thấp hơn cao độ nhỏ nhất ≥ 2 m | `DatumMarginM` |
| Khoảng cọc | 20 m; H mỗi 100 m; Km mỗi 1 000 m; thêm đầu / cuối / đỉnh | `StakeDetailM`, `Stake100M`, `StakeKmM` |
| Vạch lý trình mặt bằng | 50 m trên mặt bằng; 20 m trên trắc dọc / trắc ngang (cọc chi tiết) | `PlanTickM` |
| Bề rộng trắc ngang | 15 m mỗi bên | `CrossHalfWidthM` |
| Khoảng cao đều | 1:500 → 0,5 m; 1:1000 → 1 m; 1:2000 → 1 m (tỉ lệ khác: lấy tỉ lệ gần nhất) | `ContourIntervals` |
| Đường cái | cứ 5 đường | `MajorEvery` |
| Số lẻ | điểm đo 3; trên trắc 2; khoảng cách 2; độ dốc 2 | `DecPoint`, `DecSection`, `DecDistance`, `DecSlope` |
| Cỡ chữ in | 2,5 mm; tiêu đề 3,5 / 5 mm | `TextMm`, `TitleMm`, `BigTitleMm` |
| VN-2000 | chỉ đổi trục, m × 1000, không đổi múi chiếu | `AxisSwap`, `UnitsPerMetre` |

Hai chỗ "chưa chắc" thuộc H6 (khoảng cao đều theo tỉ lệ, khoảng cọc chi tiết 20 m): dùng số đề xuất, ghi "số mặc định, kỹ sư
đối chiếu TCVN hiện hành trước khi dùng" trong skill và mô tả lệnh. Mỗi chỗ là một số trong `H6Numbers`, đổi một dòng.

## 3b. Con số chờ chủ dự án duyệt (số thường)

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Số điểm mỗi lần | 10 000 | nhiều hơn: báo chia file | lưới tam giác và đồng mức vẫn nhanh; khu đo thường vài trăm – vài nghìn điểm |
| Cỡ file điểm | 20 MB | lớn hơn: báo | 10 000 dòng CSV chỉ khoảng 0,5 MB |
| Số cọc mỗi tuyến | 1 000 | nhiều hơn: nhờ tăng khoảng cọc | 20 km ở 20 m |
| Số trắc ngang mỗi lần | 200 | nhiều hơn: nhờ chọn cọc hoặc tăng khoảng | một lượt vẽ không quá nặng |
| Số mức đồng mức | 2 000 | nhiều hơn: nhờ tăng khoảng cao đều | |
| Tỉ lệ in mặt bằng mặc định | 1:500 | cỡ chữ điểm / đồng mức / lý trình | như skill `ve-ha-tang` |
| Bước lấy mẫu trắc ngang | 1 m, bỏ điểm lệch < 0,01 m so với đường thẳng | số cột trong bảng trắc ngang | đủ mịn, bảng gọn |
| Bỏ tam giác cạnh dài | > 4 × cạnh trung vị (`max_edge` 0 thì giữ hết) | không có đồng mức cắt qua vùng trống | |
| Bố trí khung (mm giấy) | dòng bảng trắc dọc 10, cột nhãn 45; dòng trắc ngang 8, cột nhãn 30; vạch lý trình 4; khoảng cách khung 15; 5 trắc ngang mỗi cột | hình khung | |
| Hai điểm trùng | cách nhau < 1 mm (thật) thì bỏ điểm sau | số điểm "trùng bỏ" | |
| Ngưỡng tọa độ thật | > 100 000 m | Claude hỏi gốc tọa độ | VN-2000 ở Việt Nam luôn lớn hơn |
| Tên / màu layer | `C-SURVEY-PT` 3, `C-LEVEL` 2, `C-COORD` 3, `C-TOPO-MINR` 8, `C-TOPO-MAJR` 33 (0,35), `C-STATION` 2, `C-PROF-GRID` 8, `C-PROF-GRND` 3 (0,35), `C-PROF-DSGN` 1 (0,35), `C-PROF-TEXT` 7, `C-TEXT` 7 | | `C-LEVEL`, `C-COORD`, `C-STATION`, `C-TEXT` giữ như skill |

Số theo tiêu chuẩn (mục 3a): **đã duyệt 26/11**; các số trong bảng 3b vẫn chờ duyệt.

Thời gian chờ lệnh (số kỹ thuật, không phải hỏi): `terrain` vào nhóm lệnh chậm, 2 phút.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Một lệnh hay nhiều | **một lệnh** `terrain` với 5 việc, hoãn nạp | tách lệnh |
| Khối lượng đào đắp | **không làm** (dễ hiểu nhầm là tính toán; cần mặt cắt thiết kế đầy đủ). Trắc dọc chỉ ghi chênh cao TK − TN tại cọc | thêm việc `volumes` theo trắc ngang |
| Điểm khảo sát lưu ở đâu | dấu chấm (POINT) ở đúng cao độ trên layer `C-SURVEY-PT`; đồng mức / trắc đọc lại từ layer đó mỗi lần | block có thuộc tính |
| Tọa độ thật hay gốc cục bộ | **hỏi khách** (như skill); `origin` "auto" = X / Y nhỏ nhất làm tròn xuống 100 m | tự chọn gốc |
| Cột X / Y có vẻ đảo | chỉ cảnh báo, không tự đảo | tự đảo |
| Đồng mức | nối thẳng giữa các cạnh tam giác, **không làm trơn**; mỗi đường ở cao độ của nó | làm trơn (spline) |
| Vẽ lại đồng mức | thêm đường mới, không xóa đường cũ (xóa là việc khách chọn) | tự xóa layer đồng mức cũ (cần thẻ) |
| Cao độ tự nhiên ở cọc | nội suy trên lưới tam giác tại cọc (thẳng giữa hai cọc) | thêm điểm gãy địa hình giữa các cọc |
| Tên cọc | Km0, H1…H9, C1, C2… (cọc chi tiết đánh số liên tục), Đ1… (đỉnh), Đầu / Cuối khi không trùng cọc chẵn | đánh số theo từng 100 m |
| Trái / phải trắc ngang | nhìn theo chiều tăng lý trình, bên trái vẽ bên trái | |
| Đường thiết kế trắc dọc | khách đưa các điểm [lý trình, cao độ]; nối thẳng (không có đường cong đứng) | thêm cong đứng |
| Excel đầu vào | đọc sheet đầu, giá trị đã tính của công thức; .xls cũ: nhờ lưu .xlsx | |
| Mã hóa CSV | UTF-8, rồi Windows-1258, rồi Latin | |
| Hệ tọa độ | giữ WCS (không đổi theo UCS) | |
| Thẻ F5 | không có (không ghi file) | |

## 5. Token lúc mở chat

| Gói / chế độ | Nạp gọn trước → sau | Tắt nạp gọn trước → sau |
| --- | --- | --- |
| 2D | 13,9k → 14,0k (+178 ký tự) | 48,0k → 49,4k |
| 3D | 19,2k → 19,2k (không đổi) | 46,4k → 46,4k |
| Full · Cả hai | 21,2k → 21,3k | 69,3k → 70,7k |

"Trước" là `76ecfae`. Đo bằng số ký tự (khoảng 3,5 ký tự / token).

## 6. Test

- Lõi: **1627 / 1627** (thêm 20 ở `NangCapH6Tests.cs`: đọc CSV tiếng Việt / không tiêu đề / có ngoặc kép, đọc .xlsx, đổi trục,
  cảnh báo đảo cột, điểm trùng, lưới tam giác (đủ tam giác, Delaunay, nội suy mặt phẳng, bỏ cạnh dài), đồng mức mặt phẳng / đồi
  (đường kín), mức và giới hạn, chữ đọc xuôi, lý trình / cao độ, tên cọc, MSS / thiết kế / độ dốc, khung trắc dọc / trắc ngang,
  số tiêu chuẩn không có trong mô tả lệnh / skill, đăng ký).
- Trợ lý: **447 / 447** (thêm 4 ở `agent/test/nangcaph6.test.ts`).
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi; 1 cảnh báo CA2022 ở `TraceTools.cs` có sẵn.
- SelfTest: bước "Nâng cấp đợt H6…": CSV 121 điểm (dấu phẩy thập phân), đồng mức, tuyến 3 đỉnh, lý trình, trắc dọc có thiết kế,
  trắc ngang.
- CI: ghi ở mục 8.

## 7. Chưa kiểm được

- AutoCAD thật: chữ căn giữa / xoay (DBText `AdjustAlignment`), nét đậm, tốc độ với 10 000 điểm, file Excel từ máy toàn đạc thật.
- Đối chiếu bản vẽ với hồ sơ thật của kỹ sư (cách trình bày bảng trắc dọc / trắc ngang theo từng công ty có thể khác).

## 8. CI

*(ghi sau khi chạy)*
