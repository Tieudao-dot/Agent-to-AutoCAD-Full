# Bàn giao: Nâng cấp đợt H3: so sánh hai bản vẽ (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết). Nhánh `full/nang-cap-h3`.

## 1. Bước gộp

Nhánh `full/nang-cap-h3` tách từ `full/nang-cap-h1-test` @ `24aaa41` và gộp hai nhánh:

- `full/nang-cap-h2-test` @ `e3d395b`: chỉ chồng một dòng bảng trong `bo-cai/README.md`, giữ cả hai dòng;
- `full/nang-cap-h-sua-ci` @ `c29fb9e`: không chồng.

CI của commit gộp: xem mục 9.

## 2. Phần dùng chung: mở một bản vẽ khác chỉ để đọc (cho H3 và H4)

Hai file:

- `Drawing/OtherDrawing.cs`: phần chạy trong AutoCAD.
- `Core/OtherDrawing.cs`: phần không cần AutoCAD, có test trong `Core.Tests/NangCapH3OtherDrawingTests.cs`.

Cách dùng (thay `"ten_lenh"` bằng tên lệnh gọi):

```csharp
using (var od = OtherDrawing.Open(ctx, path, "ten_lenh"))
using (var tr = od.Db.TransactionManager.StartTransaction())
{
    var ents = od.ModelEntities(tr, limit, out int total); // đối tượng model space, tối đa limit
    var info = od.Info();                                  // file, đời DWG, đơn vị, ghi chú khi file cũng đang mở ở đây
    tr.Commit();
}
```

**Đường dẫn:** cùng luật với `trace_image` (G5 và các lượt sửa KFG). `DrawingService.CheckPathAllowed` nay là `internal`, hành
vi không đổi. File được mở khi nằm ở:

- một ổ đĩa của máy;
- file đính kèm trong chat;
- cùng thư mục mạng hoặc cùng ổ mạng với bản vẽ đang mở.

Đường dẫn thiết bị và đường dẫn tương đối bị từ chối.

**Loại file:**

- `.dwg` và `.dxf` được đọc.
- `.dwl` / `.dwl2` / `.bak` / `.sv$` / loại khác bị từ chối kèm gợi ý.
- File rỗng hoặc lớn hơn `OtherDrawingNumbers.MaxFileMb` (200 MB) bị từ chối.

**Không khóa, không sửa:**

- 6 byte đầu đọc bằng `FileShare.ReadWrite | FileShare.Delete`.
- File DWG đọc bằng `ReadDwgFile(…, FileOpenMode.OpenForReadAndAllShare, …)`, rồi `CloseInput(true)` nạp hết vào bộ nhớ và
  nhả file ngay.
- File DXF đọc bằng `DxfIn` vào một database trống.
- Không bao giờ `Save` hay mở `ForWrite`. Test soi mã nguồn kiểm điều này.

**Lỗi báo rõ cho Claude (kèm cách xử lý cho khách):**

| Tình huống | Cách nhận ra | Lời báo |
| --- | --- | --- |
| Đời mới hơn AutoCAD đang chạy | Mã đầu file (`AC1032`…) lớn hơn mã của AutoCAD này (`DwgVersion.Current`), xét **trước** khi AutoCAD đọc | Nhờ khách "Save As" đời cũ hơn |
| Đang bị khóa / không có quyền | Lỗi mở file, hoặc ErrorStatus có `Sharing` / `Locked` / `Access` | Nhờ đóng file ở chỗ kia hoặc chép ra bản khác |
| Không phải DWG | Đầu file không có dạng `AC10xx` | Nhờ mở và lưu lại |
| Hỏng | Mọi lỗi đọc khác | Nhờ RECOVER rồi lưu lại |

File đó cũng đang mở trong AutoCAD này: plugin đọc bản đã lưu, và `Info()` ghi chú điều đó (kể cả khi còn thay đổi chưa lưu,
nếu đó là bản vẽ đang làm).

**SelfTest:** bước "Nâng cấp đợt H3: đọc bản vẽ khác chỉ để đọc".

- Xuất bản vẽ test ra DWG và DXF, đọc lại bằng `OtherDrawing`; số đối tượng của hai file phải bằng nhau.
- Xóa được hai file ngay sau khi đọc, tức là file không bị khóa.
- File không có thì phải báo lỗi.
- Bản vẽ đang mở không đổi.

## 3. So sánh hai bản vẽ

(viết ở bước 2)

## 9. CI

- Commit gộp + phần dùng chung: (ghi sau khi chạy).
