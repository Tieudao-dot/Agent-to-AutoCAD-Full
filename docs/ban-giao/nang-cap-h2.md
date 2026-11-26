# Bàn giao: Nâng cấp đợt H2 — xuất bản vẽ sang định dạng khác (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người viết). Nhánh `full/nang-cap-h2`, tách từ `full/nang-cap-g-tong-the` @ `547d6af`.
Chạy cùng lúc với H1 (thread đợt F1, `full/nang-cap-h1`): file dùng chung chỉ thêm dòng (mục 5). Chưa thử trên AutoCAD thật.

## 1. Khách thấy gì

- "Lưu ra DWG 2010 để gửi đối tác", "xuất DXF", "xuất mô hình ra FBX": Claude gọi `export_drawing`. File **mới** cạnh bản vẽ
  (`<bản vẽ>-acad2013.dwg`, `<bản vẽ>-acad2013.dxf`, `<bản vẽ>.fbx`; một phần: `…-mot-phan…`). Bản vẽ đang mở giữ nguyên file,
  tên, đời; đặt trùng tên file đang mở thì bị từ chối.
- DWG / DXF: đời 2018 / 2013 / 2010 / 2007 / 2004 / 2000 (mặc định 2013; "2016" → định dạng 2013 mà 2013 – 2017 đọc; cũ hơn 2000:
  từ chối). Cả bản vẽ (giữ các layout, kết quả báo `layouts`) hoặc một phần (`handles` / `layers` trong Model, không có layout).
- FBX (gói 3D / Full): các khối 3D thành lưới tam giác, mỗi khối một vật (tên = layer + handle), màu theo màu khối / layer,
  đơn vị mm, trục Z lên. Nét 2D, chữ không có trong FBX (kết quả nói rõ). Gói 2D: FBX bị chặn kèm lời giải thích như mọi tham
  số theo gói; DXF / DWG dùng được ở mọi gói.
- File đã có: thẻ Đồng ý / Hủy (F5). Đường dẫn mạng: luật của G5 (máy này, hoặc cùng thư mục mạng với bản vẽ). "Chỉ xem": xuất được
  (chỉ đọc bản vẽ). Không ghi vào macro.

## 2. Cách làm

- Một lệnh mới `export_drawing` (nhóm Common, **hoãn nạp**), `format` dxf / dwg / fbx; FBX chặn theo gói bằng
  `PlanLogic.ThreeDValues` (giá trị của tham số), cùng cơ chế với các tham số theo gói đã có.
- DWG / DXF: bản sao cơ sở dữ liệu (`Database.Wblock()` cả bản vẽ, `Wblock(ids, gốc)` một phần) rồi `SaveAs(tạm, đời)` /
  `DxfOut(tạm, 16, đời)`, chép vào file đích. Không bao giờ gọi `SaveAs` / `DxfOut` trên bản vẽ đang mở (test kiểm).
- FBX: plugin **tự ghi** FBX 7.4 nhị phân (`Core/Export.cs`, `FbxWriter`): lưới của từng khối lấy như `export_stl` (bản sao dời vào
  góc dương rồi `StlOut`, dời lại trong lưới; bản sao không giữ lại), một Model + Geometry + Material mỗi khối, `GlobalSettings`
  UnitScaleFactor 0,1 (mm), UpAxis Z, đủ phần đầu / chân file theo cách các trình nhập FBX đọc. Không dùng lệnh `FBXEXPORT`.

## 3. Đúng với

- Gói: Common (mọi gói), FBX 3D / Full; chế độ "2D" của máy Full chặn FBX (test). Nhãn: "Xuất DXF / DWG / FBX".
- Thẻ ghi đè: `export_drawing` trong `F5_CARD_TOOLS`; `RequireOverwriteCard` trước khi ghi gì (test vị trí).
- Lệnh đọc (`Writes = false`): chạy khi "Chỉ xem", không ghi macro.

## 4. Tự kiểm

Bước "Nâng cấp đợt H2" trong `SelfTest.cs`: xuất DWG 2010 rồi đọc lại (`OriginalFileVersion` = AC1024), kiểm tên bản vẽ đang mở không
đổi, DXF có `AC1027`, FBX của một khối hộp (đầu file đúng, ≥ 12 tam giác), xóa khối hộp.

## 5. File

- Mới: `plugin/ClaudeCAD.Plugin/Core/Export.cs`, `Drawing/ExportTools.cs`, `agent/src/tools/exportDrawing.ts`,
  `plugin/ClaudeCAD.Core.Tests/NangCapH2Tests.cs` (21), `agent/test/nangcaph2.test.ts` (4), `docs/checklist-dot-h2.md`, file này.
- Thêm dòng (dùng chung): `Core/Plans.cs` (một dòng nhóm + bảng `ThreeDValues` + 3 dòng kiểm), `Drawing/DrawingService.cs` (1),
  `UI/ToolLabels.cs` (1), `SelfTest.cs` (bước H2), `ClaudeCAD.Core.Tests.csproj` (1), `agent/src/tools/index.ts` (2),
  `agent/src/agent/confirmation.ts` (1 từ), `agent/src/agent/systemPrompt.ts` (1 dòng), `bo-cai/README.md` (1 dòng).

## 6. Con số chờ chủ dự án duyệt

| Số | Đề xuất | Ở đâu | Khách thấy | Vì sao |
| --- | --- | --- | --- | --- |
| Đời DXF / DWG mặc định | 2013 (AC1027) | `H2Numbers.DefaultVersion` | file `-acad2013`, mở bằng AutoCAD 2013 trở lên | đời phổ biến nhất mà đa số phần mềm CAD và SketchUp đọc |
| Các đời cho chọn | 2018, 2013, 2010, 2007, 2004, 2000 | `ExportLogic.Versions` | cũ hơn 2000 bị từ chối | các đời AutoCAD .NET lưu được |
| Số khối tối đa trong một FBX | 2 000 | `H2Numbers.MaxFbxSolids` | nhiều hơn thì nhờ chọn layer / khối | như `export_stl` |

Số kỹ thuật (khách không thấy): DXF 16 chữ số thập phân (mặc định của DXFOUT); gộp đỉnh trùng của lưới theo 0,0001 mm.

## 7. Quyết định tự chọn

1. **Một lệnh mới, hoãn nạp** cho cả ba định dạng (không mở rộng `save_drawing` / `export_stl`: hai lệnh đó có luật riêng về ghi
   bản vẽ / STL). Tăng phần luôn nạp chỉ một dòng system prompt.
2. **Xuất cả bản vẽ mặc định; một phần theo `handles` / `layers`** (rẻ: cùng `Wblock`), một phần chỉ lấy Model (không layout).
3. **DWG / DXF từ bản sao `Wblock`**, không `SaveAs` trên bản vẽ đang mở (cách DrawingHistory đã dùng cho bản sao phiên bản).
   Cần AutoCAD thật để chắc `Wblock()` giữ đủ layout: bước tự kiểm báo số layout trong kết quả, checklist A.2 nhờ đếm.
4. **FBX tự ghi**, không gọi `FBXEXPORT` (lệnh có hộp thoại, câu hỏi dòng lệnh chưa rõ ở từng đời: bài học `-RENDER` của G4).
   Đổi lại: chưa mở thử bằng 3ds Max / Blender thật; cấu trúc file được kiểm bằng bộ đọc trong test (đầu file, độ dài từng nút, nút
   kết thúc, chân file, các phần Blender đọc: GlobalSettings, Objects, Connections). Bộ đọc FBX của Blender (Python) không chạy được
   ở đây (chạy code tải từ mạng bị chặn), nên không đối chiếu được bằng trình nhập thật.
5. **FBX chỉ lấy khối 3D** (`Solid3d`); lưới / mặt 3D khác, nét 2D không vào FBX.
6. **SketchUp:** không hứa. Theo hiểu biết (chưa kiểm): SketchUp Pro nhập DWG / DXF (đời 2013 trở về an toàn hơn) lấy được nét và mặt,
   khối ACIS thường không vào; SketchUp 2018+ nhập được STL (đã có `export_stl`). Mô tả lệnh nhờ Claude chỉ gợi ý DWG / DXF 2013 hoặc STL
   và không hứa hơn. Checklist mục D nhờ thử.
7. 3ds Max đọc được DWG trực tiếp: nếu FBX có vấn đề, DWG là đường lui cho 3ds Max (Blender thì không đọc DWG).
8. Đường dẫn ghi ra mạng: dùng luật G5 (`TraceLogic.PathRefusal`), lời khuyên đổi thành "chọn thư mục trên máy hoặc thư mục của bản vẽ".

## 8. Token lúc mở chat

- System prompt: gói 2D 12 775 → 12 868 ký tự (+93, khoảng 27 token); 3D 20 250 → 20 343; Full 20 030 → 20 123.
- Phần lệnh nạp sẵn không đổi (43 359 ký tự); tên lệnh hoãn nạp thêm "export_drawing" (+16 ký tự).
- Mô tả `export_drawing`: 1 983 ký tự (khoảng 570 token), chỉ khi Claude tìm tới lệnh hoặc khi tắt "Nạp gọn lệnh".
- Đo bằng số ký tự (khoảng 3,5 ký tự / token), không phải số token chính xác.

## 9. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: xem mục cập nhật cuối file.
- Trợ lý `cd agent && npm ci && npm test`: xem mục cập nhật cuối file.

## 10. CI

- Xem mục cập nhật cuối file.

## 11. Chưa kiểm được

- AutoCAD thật: `Wblock()` giữ layout, `SaveAs` / `DxfOut` đời cũ trên bản sao, file 2010 / 2000 mở được bằng AutoCAD cũ, bước tự kiểm H2.
- FBX trong Blender / 3ds Max (rủi ro cao: checklist mục C), SketchUp (mục D).
