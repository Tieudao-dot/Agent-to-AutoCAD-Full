# Bàn giao: Test & sửa đợt H2 — xuất bản vẽ sang định dạng khác (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người rà; người viết: thread Test & sửa F1). Nhánh `full/nang-cap-h2-test`, tách từ
`full/nang-cap-h2` @ `21c6d90` (code `3d64e75`, CI 45774685657 xanh). Bàn giao người viết: `docs/ban-giao/nang-cap-h2.md`. Thử tay:
`docs/checklist-dot-h2.md` (thêm C.6 – C.8, E.3). Chưa chạy trên AutoCAD / Blender / 3ds Max thật.

## 1. Đã soi

| Điểm | Kết luận |
| --- | --- |
| FBX nhị phân 7.4: đầu file (`Kaydara FBX Binary  \0`, 0x1A 0x00, 7400), bản ghi node (offset cuối tuyệt đối 32 bit, số / độ dài thuộc tính, tên), bản ghi rỗng 13 byte (có con, hoặc không thuộc tính và không phải node cuối), bản ghi rỗng cuối cấp trên cùng, đuôi file (FootId, 4 byte 0, đệm tới bội 16, 7400, 120 byte 0, magic) | **Đúng** từng phần, khớp cách bộ ghi FBX của Blender làm; FileId / CreationTime là cặp hằng của Blender |
| GlobalSettings: UpAxis 2 (+), FrontAxis 1 (−), CoordAxis 0 (+), UnitScaleFactor 0,1 (1 đơn vị = 1 mm) | Đúng: hệ phải, Z lên trên, mm |
| Objects / Connections: Geometry "Mesh" + Model "Mesh" + Material, nối OO Model → gốc, Geometry → Model, Material → Model; tên `tên\0\x01Lớp` | Đúng |
| Chỉ số mặt (mặt cuối `~i` = −(i+1)), mảng đỉnh, màu (DiffuseColor / Diffuse) | Đúng |
| **Pháp tuyến** | **Thiếu** (lỗi 1) |
| Khối rỗng / lưới không ra tam giác nào | **Ghi node rỗng** (lỗi 4) |
| Vượt 2 000 khối | Đúng: từ chối, nhờ chọn layer / handles (như `export_stl`) |
| Wblock / SaveAs: bản vẽ đang mở không đổi file / tên / đời; trùng tên file đang mở bị từ chối (so đường dẫn đầy đủ, không phân biệt hoa thường, cả đường tương đối) | Đúng. File đang mở trong chương trình khác: báo "is it open in another program?" |
| Một phần theo handles / layers: chỉ Model; Wblock chỉ kéo theo những gì đối tượng cần (block, kiểu chữ, layer) | Đúng; **handles + layers cùng lúc âm thầm bỏ layers** (lỗi 5) |
| Chặn theo gói: FBX ngoài gói 3D / Full, kể cả qua `batch_execute` (dispatcher kiểm từng lệnh) và macro (`BlockedCalls`) | Chặn đúng, nhưng **câu báo sai** (lỗi 2) và **Claude vẫn được mời FBX trên gói 2D** (lỗi 3) |
| "Chỉ xem": `export_drawing` là lệnh đọc, như `export_stl`, `plot_pdf` | Đồng bộ |
| Thẻ ghi đè F5, đường dẫn mạng G5, tên file có dấu, thư mục không có | Đúng; **lời khuyên khi đường dẫn bị từ chối còn bảo "kéo file vào chat"** ở dạng đường dẫn thiết bị (lỗi 6) |
| Mô tả lệnh / system prompt: "AutoCAD 2010" → dwg 2010; SketchUp → dwg / dxf 2013 hoặc STL, không hứa; 3ds Max → fbx (DWG là đường lui) | Đúng |
| Bảng "Con số chờ chủ dự án duyệt" (đời mặc định 2013, 6 đời, 2 000 khối, gộp đỉnh 0,0001 mm) | Khớp code |

## 2. Lỗi đã sửa

| # | Mức | Lỗi | Đã sửa | Test |
| --- | --- | --- | --- | --- |
| 1 | Vừa | FBX không có pháp tuyến. Đỉnh trùng đã gộp nên mỗi chương trình tự tính pháp tuyến trung bình: khối hộp có thể bị tô bóng tròn (3ds Max). | `LayerElementNormal` ByPolygonVertex / Direct, pháp tuyến phẳng của từng tam giác (`FbxWriter.FaceNormals`), có trong `Layer` | `Each_triangle_corner_has_the_flat_normal…`, `The_fbx_has_a_normal_layer…` |
| 2 | Nhẹ | Gói 2D gọi FBX: câu báo nói về "thuộc tính block" (nhánh cuối của `BlockedParamMessage`); macro ghi "(số tầng)". | Nhánh riêng cho `ThreeDValues`: nói FBX là phần 3D, gợi ý dwg / dxf; macro ghi "(FBX, phần 3D)" | `Fbx_on_a_plan_without_3d_says_fbx…` |
| 3 | Nhẹ | Gói 2D (và Full chế độ 2D) vẫn thấy `format: fbx` trong schema, Claude có thể đề nghị rồi mới bị từ chối. | `trimForPlan` bỏ fbx khỏi enum ở gói 2D | `nangcaph2.test.ts` "the 2D plan is not offered fbx" |
| 4 | Nhẹ | Khối AutoCAD lưới hóa ra 0 tam giác: ghi Geometry rỗng (có trình nhập báo lỗi cả file). | Bỏ khối đó, kể trong `left_out`; không còn khối nào thì báo lỗi, không ghi file | nguồn (`The_tool_refuses_handles_with_layers…`) |
| 5 | Nhẹ | Có cả `handles` và `layers`: chỉ xuất handles, layers bị bỏ không báo. | Từ chối: "give handles or layers, not both" | như trên |
| 6 | Nhẹ | Đường dẫn ghi bị luật mạng G5 từ chối ở dạng đường dẫn thiết bị: lời khuyên "kéo file vào chat" (vô nghĩa với file xuất); "are opened". | `ExportLogic.WriteRefusal`: thay cả hai lời khuyên bằng "thư mục trên máy hoặc thư mục của bản vẽ", "are used" | `A_refused_network_folder_for_an_export…` |

Kèm: ghi chú FBX nói rõ chỉ lấy khối 3D của Model, không lấy khối nằm trong block; chú thích hai `<summary>` chồng nhau ở `Plans.cs`.

## 3. Con số chờ chủ dự án duyệt

Không có số mới. Ba số của người viết (đời mặc định 2013, 6 đời 2018 … 2000, tối đa 2 000 khối FBX) khớp code.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Pháp tuyến FBX | phẳng theo từng tam giác (hộp, tường sắc cạnh); khối tròn thấy từng mặt nhỏ | pháp tuyến mượt theo góc (vd < 30°) trong `FaceNormals` |
| handles + layers cùng lúc | từ chối, nhờ chọn một | gộp cả hai |
| Khối 3D nằm trong block | không vào FBX (như trước), nay ghi rõ trong kết quả | tách block khi xuất (thêm việc) |

## 5. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1471 / 1471** (thêm 5 ở `NangCapH2TestSuaTests.cs`; không sửa test cũ).
- Trợ lý `cd agent && npm test`: **412 / 412** (thêm 1).
- Build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa các lần): 0 lỗi, 0 cảnh báo.
- CI: ghi ở mục 7.

## 6. Chưa kiểm được

- Mở file FBX bằng Blender / 3ds Max thật (bộ đọc trong test chỉ kiểm cấu trúc); pháp tuyến hiển thị thế nào.
- AutoCAD thật: `Wblock()` giữ đủ layout, `DxfOut` / `SaveAs` các đời, khối lưới hóa ra 0 tam giác.
- SketchUp nhập DWG / DXF 2013.

## 7. CI
