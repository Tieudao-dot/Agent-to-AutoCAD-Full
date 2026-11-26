# Checklist thử tay đợt H2: xuất bản vẽ sang định dạng khác

Mọi gói (FBX: gói 3D / Full), AutoCAD 2022 – 2027. Một bản vẽ đã lưu có hình 2D, vài layout và (gói 3D / Full) vài khối 3D.
Khoảng 30 phút. Mở file xuất ra bằng chương trình khác nếu có (AutoCAD đời cũ, TrueView, LibreCAD / DraftSight, Blender,
3ds Max, SketchUp).

## A. DWG đời cũ (~8 phút, quan trọng nhất)

1. Gõ "lưu bản vẽ ra DWG 2010 để gửi đối tác". Cần thấy:
   - file mới `<tên bản vẽ>-acad2010.dwg` cạnh bản vẽ; Claude nói đường dẫn và "mở được bằng AutoCAD 2010 trở lên";
   - **bản vẽ đang mở vẫn là file gốc**: thanh tiêu đề AutoCAD không đổi tên; lệnh `QSAVE` lưu vào file gốc; mở file gốc bằng
     AutoCAD mới vẫn là đời hiện tại.
2. Mở file 2010 (bằng AutoCAD đời cũ / TrueView, hoặc AutoCAD hiện tại): đủ hình, layer, kiểu chữ, kích thước, **các layout**
   (đếm số tab giống bản gốc; kết quả Claude báo `layouts`).
3. Gõ lại cùng câu: file đã có nên hiện thẻ **Đồng ý / Hủy**. Hủy: file cũ không đổi. Đồng ý: file được ghi lại.
4. Thử "DWG 2000": được. Thử "DWG R14": Claude nói AutoCAD chỉ lưu được từ 2000 trở lên.
5. "Lưu đè lên chính bản vẽ này dạng 2010" (đặt tên trùng file đang mở): Claude từ chối, nói bản xuất luôn là file mới.

## B. DXF (~5 phút)

1. "Xuất DXF": file `<tên>-acad2013.dxf`. Mở bằng AutoCAD hoặc chương trình khác (LibreCAD, DraftSight…): hình đúng chỗ, đúng layer.
2. "Xuất DXF chỉ layer A-WALL": file `…-mot-phan.dxf` chỉ có tường (không có layout, Claude nói rõ).
3. Bật "Chỉ xem" (🔒): xuất vẫn chạy (chỉ đọc bản vẽ, ghi file mới).

## C. FBX (gói 3D / Full, ~10 phút, rủi ro cao)

FBX do plugin tự ghi (không dùng lệnh `FBXEXPORT` của AutoCAD). Chưa ai mở thử bằng 3ds Max / Blender thật.

1. "Xuất mô hình ra FBX": file `<tên>.fbx`; Claude báo số khối và số tam giác.
2. **Blender** (File › Import › FBX): mô hình hiện ra, đúng kích thước (mét trong Blender: tường 3 m cao 3 m), đứng thẳng (Z lên trên),
   mỗi khối một vật, màu theo màu khối / layer. Ghi lại nếu báo lỗi hay mô hình nằm ngang / sai cỡ 10 lần.
3. **3ds Max** (Import › FBX): như trên. Ghi lại mọi cảnh báo của hộp thoại nhập FBX.
4. Máy gói 2D: "xuất FBX": Claude nói FBX cần gói 3D / Full; DXF / DWG vẫn dùng được.
5. Bản vẽ chỉ có hình 2D: "xuất FBX": Claude nói FBX chỉ lấy khối 3D, gợi ý DXF / DWG.

## D. SketchUp (~5 phút, nếu có SketchUp)

1. SketchUp Pro: File › Import › chọn file DWG / DXF 2013 vừa xuất: nét 2D vào được. Khối 3D có thể không vào (SketchUp đọc mặt / lưới,
   không đọc khối ACIS): ghi lại.
2. Thử `export_stl` (đã có) rồi nhập STL vào SketchUp (SketchUp 2018+ có sẵn STL importer): ghi lại kết quả.

## E. Đường dẫn

1. "Xuất DWG vào `\\máy-khác\thư-mục\a.dwg`" (máy khác chỗ lưu bản vẽ): Claude từ chối, nhờ chọn thư mục trên máy hoặc thư mục của bản vẽ.
2. Bản vẽ chưa lưu bao giờ: file xuất vào thư mục Documents.

## F. Tự kiểm

Bấm "Tự kiểm": bước "Nâng cấp đợt H2" phải đạt (DWG 2010 đọc lại đúng đời, bản vẽ đang mở không đổi tên, DXF 2013, FBX của một khối hộp).
