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

## 3. So sánh hai bản vẽ: khách thấy gì

- Gói 2D / Full: khách nói "so bản vẽ này với file D:\…\ban-cu.dwg" (hoặc kéo file vào khung chat). Gói 3D cũng dùng được,
  nhưng chỉ so khối 3D.
- File kia chỉ được đọc, không bao giờ bị sửa hay khóa. Mặc định file kia là **bản cũ**; khách nói "file kia là bản mới" thì
  đảo chiều.
- Claude báo số đối tượng **thêm / bớt / đổi**, và với mỗi chỗ đổi: khác ở đâu.
  - Các kiểu khác: hình học (kèm tâm dời bao nhiêu), layer, màu, nét, độ dày nét, nội dung chữ, kiểu chữ, tên block, thuộc tính
    block, giá trị kích thước, mẫu / diện tích hatch.
  - Khối 3D: hộp bao và thể tích.
- Claude nói rõ cách khớp đã dùng:
  - **theo handle** khi hai file cùng gốc (bản sửa của cùng một file): tối thiểu 50 % đối tượng của file kia có mã đối tượng
    (handle) trong bản đang mở, cùng loại;
  - **theo hình học** khi khác gốc: cùng loại, cùng vị trí và kích thước trong dung sai 1 mm. Đường vẽ ngược chiều vẫn là một.
    Còn lại, đối tượng cùng loại có điểm đầu trùng nhau thì tính là "đổi hình".
  - Khách bắt buộc được một cách (`match`).
- "Khoanh chỗ khác" (`mark`): đám mây quanh từng chỗ khác, trên layer `X-SO-SANH`.
  - Màu: **xanh lá = thêm, đỏ = bớt, vàng = đổi**.
  - Chỗ khác cùng màu sát nhau gộp thành một đám mây.
  - Xóa layer `X-SO-SANH` là bỏ hết dấu. Lần so sau không tính đám mây của lần trước.
  - Quá 50 đám mây: hỏi thẻ xác nhận trước khi vẽ.
  - Quá 500 đám mây: không vẽ, chỉ đưa danh sách / Excel.
  - U một lần xóa hết đám mây của lượt.
- "Xuất Excel" (`excel`): toàn bộ danh sách (thêm / bớt / đổi, loại, layer, handle hai bên, khác ở đâu) ra
  `<bản vẽ>-so-sanh.xlsx`. Kết quả trong chat chỉ liệt kê 100 dòng đầu.
- Bản vẽ lớn: so tối đa 20 000 đối tượng mỗi bên; nhiều hơn thì kết quả ghi rõ "chỉ so phần đầu".
- Hai bản vẽ khác đơn vị (mm / m): số của file kia được đổi về đơn vị bản đang mở, có ghi chú.
- Lỗi file (đang khóa, đời mới hơn, hỏng, đường dẫn mạng khác thư mục): như mục 2. So với chính nó: từ chối.

## 4. Cách làm

- Lệnh mới `compare_drawings`:
  - nhóm Common, tức mọi gói (`Core/Plans.cs`); gói 2D / Full so đối tượng 2D, gói 3D / Full so khối 3D;
  - **hoãn nạp**, mô tả ngắn;
  - thêm một dòng vào system prompt chung.
- Plugin: `Drawing/CompareTools.cs` đọc model space hai bên thành `CmpItem`; `Core/DrawingCompare.cs` khớp và so (có test). Tìm
  ứng viên bằng ô lưới theo dung sai, không so từng cặp: 20 000 đối tượng mỗi bên, cộng 2 000 đường chung một điểm, so dưới 3
  giây trong test.
- Lệnh chỉ đọc khi không `mark` (`readsWhen`), nên chạy được ở chế độ "Chỉ xem". Có `mark` thì là lệnh ghi bình thường (một lần
  hoàn tác).
- Lệnh giữ hệ WCS (`Ucs.cs`): hai bản vẽ so trong WCS của chính nó, đám mây vẽ đúng chỗ đối tượng.
- Thẻ xác nhận theo cơ chế F5 (`F5_CARD_TOOLS`, `RequireCard`). Mã thẻ theo file kia và số đám mây; file Excel đã có cũng hỏi
  thẻ như các bảng khác.
- SelfTest "Nâng cấp đợt H3: so sánh hai bản vẽ":
  - lưu bản sao, vẽ thêm 1 đường, so;
  - cần đúng 1 "thêm" và 0 "bớt";
  - xóa đường, xóa được file ngay.

## 5. File

- Mới:
  - `Core/OtherDrawing.cs`, `Drawing/OtherDrawing.cs` (phần dùng chung);
  - `Core/DrawingCompare.cs`, `Drawing/CompareTools.cs`, `agent/src/tools/compare.ts`;
  - test: `Core.Tests/NangCapH3OtherDrawingTests.cs`, `Core.Tests/NangCapH3CompareTests.cs`, `agent/test/nangcaph3.test.ts`;
  - tài liệu: `docs/checklist-dot-h3.md`.
- Sửa (chỉ thêm dòng):
  - plugin: `DrawingService.cs`, `Core/Plans.cs`, `Core/Ucs.cs`, `Core/Xlsx.cs`, `UI/ToolLabels.cs`, `SelfTest.cs`, `TraceTools.cs`
    (2 hàm thành `internal`);
  - trợ lý: `agent/src/tools/index.ts`, `agent/src/agent/confirmation.ts`, `agent/src/agent/systemPrompt.ts`;
  - còn lại: `Core.Tests.csproj`, `bo-cai/README.md`.

## 6. Con số chờ chủ dự án duyệt

Mọi số ở `H3Numbers` (`Core/DrawingCompare.cs`) và `OtherDrawingNumbers` (`Core/OtherDrawing.cs`).

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| 1 mm | `ToleranceMm`: dung sai khớp vị trí / kích thước | Lệch ≤ 1 mm coi như không đổi | Bản vẽ kiến trúc ghi số tới mm; nhỏ hơn thì sai số làm tròn của AutoCAD cũng thành "đổi" |
| 0,1 % | `VolumeShare`: thể tích khối 3D | Thể tích đổi ≤ 0,1 % coi như không đổi | Bỏ qua sai số tính thể tích của AutoCAD |
| 20 000 | `MaxObjects`: số đối tượng so mỗi bên | Nhiều hơn thì so phần đầu, có ghi chú | Giống soát bản vẽ (G2) |
| 50 % | `SameOriginShare`: ngưỡng coi hai file cùng gốc | Quyết định khớp theo handle hay hình học | Bản sửa của cùng một file giữ phần lớn handle |
| 100 dòng | `MaxRows`: dòng liệt kê trong chat | Hơn thì xem Excel | Giữ kết quả ngắn cho Claude |
| 50 / 500 | `CardAboveClouds` / `MaxClouds`: số đám mây | Quá 50 thì hỏi thẻ; quá 500 thì không vẽ | 50 đám mây đã kín một mặt bằng; 500 vẽ cũng không đọc được |
| 5 %, tối thiểu 100 mm | `CloudMarginShare`, `CloudMinMarginMm`: đám mây cách đối tượng | Khoảng hở giữa mây và chỗ khác | Đủ thấy rõ ở tỉ lệ 1:100 |
| Xanh lá 3 / đỏ 1 / vàng 2 | Màu thêm / bớt / đổi | Màu đám mây | Quen như đèn giao thông, màu ACI cơ bản |
| `X-SO-SANH` | Layer đám mây | Tên layer | Tiền tố `X-` như layer ảnh nền G5 |
| 200 MB | `OtherDrawingNumbers.MaxFileMb`: cỡ file đọc tối đa (dùng chung H4) | File lớn hơn bị từ chối, có lời báo | Bản vẽ thường < 50 MB; đọc file quá lớn làm AutoCAD treo lâu |

## 7. Quyết định tự chọn

- **Không dùng lệnh DWG Compare dựng sẵn (COMPARE).** Không có API .NET công khai. Lệnh chạy bằng hộp thoại; bản dòng lệnh
  không ổn định giữa các đời AutoCAD (bài học `-RENDER` G4). Kết quả của nó nằm trong một bản vẽ so sánh riêng, Claude không đọc
  được danh sách. Tự so bằng side database: rẻ, test được, chạy như nhau từ 2022 tới 2027.
- Bảng liệt kê: **chỉ Excel và danh sách trong kết quả**, không vẽ bảng AutoCAD lên bản vẽ. Danh sách có thể rất dài; bảng trên
  bản vẽ phải chọn chỗ đặt. Muốn có bảng thì thêm sau, dùng lại phần bảng của G2 / G3.
- So **model space**, không so layout. Đối tượng trong block so ở mức block: tên, vị trí, xoay, tỉ lệ, thuộc tính; không mở
  định nghĩa block.
- Lần so sau **không xóa** đám mây cũ. Xóa đối tượng có sẵn sẽ phải hỏi thẻ; khách tự xóa layer.
- Layer `X-SO-SANH` **in ra được**, để khách in bản so sánh gửi đi; muốn không in thì tắt layer.
- Khớp hình học: điểm đầu cùng chỗ thì tính là "đổi". Đối tượng dời xa hơn dung sai thì báo một "bớt" + một "thêm", không đoán
  ghép.
- Bản vẽ đang mở mà file kia cũng đang mở trong AutoCAD: đọc bản đã lưu của file kia, có ghi chú.
- Polyline có cung vẽ ngược chiều ở file kia không được nhận là cùng một hình khi khớp theo hình học (báo một bớt + một thêm). Polyline thẳng và đường thẳng vẽ ngược chiều thì nhận đúng. Khớp theo handle không bị ảnh hưởng.

## 8. Token lúc mở chat

- System prompt: +83 ký tự ở cả ba gói (khoảng 24 token). Gói 2D 12 868 → 12 951 ký tự, 3D 20 343 → 20 426, Full
  20 123 → 20 206.
- Mô tả + schema của `compare_drawings`: khoảng 1 983 ký tự (khoảng 570 token). Chỉ nạp khi Claude tìm tới lệnh, hoặc khi tắt
  "Nạp gọn lệnh".
- Đo bằng số ký tự (khoảng 3,5 ký tự / token), không phải số token chính xác.

## 9. CI

- Commit gộp + phần dùng chung: run 45782279919 trên `98eecf1`, **xanh**, log `# fail 0` (trợ lý 419/419, lõi 1520/1520).
- H3: run 45784702090 trên `d5b74a0`, **xanh**, log `# fail 0` (trợ lý 423/423, lõi 1530/1530, UI test và build 3 target qua). Các commit sau chỉ sửa tài liệu, code giữ nguyên như `d5b74a0`.

## 10. Chưa kiểm được

- Chưa thử trên AutoCAD thật: đọc file khác (khóa, đời mới hơn, DXF), so sánh, đám mây, thẻ, Excel. Các bước SelfTest H3 sẽ chạy
  phần chính trên máy thật.
- Chưa biết bản sao `export_drawing` (Wblock cả bản vẽ) có giữ handle trên mọi đời AutoCAD không. SelfTest ghi ra cách khớp
  đã dùng.
- Mã lỗi (ErrorStatus) khi file bị khóa được nhận theo tên; nếu AutoCAD đặt tên khác, lời báo sẽ là "có thể hỏng" thay vì "đang
  khóa". Checklist mục A.3 kiểm điều này.
