# Bàn giao: Test & sửa đợt H4 — học chuẩn công ty từ bản vẽ mẫu (26/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà). Nhánh `full/nang-cap-h4-test`, tách từ `full/nang-cap-h4` @ `7d0a8a5` (code
`8e72fe6`). Chưa thử trên AutoCAD thật.

**Không sửa `Core/OtherDrawing.cs` hay `Drawing/OtherDrawing.cs`**: bước gộp H5 không phải gỡ chỗ chồng ở phần dùng chung.

## 1. Lỗi đã sửa

| Mã | Lỗi | Khách thấy gì trước khi sửa | Đã sửa |
| --- | --- | --- | --- |
| H4T-1 | `standard.json` hỏng (sửa tay, ổ đầy lúc ghi): `Load` ném lỗi đọc JSON thô; danh sách giấu bộ hỏng; bộ hỏng không xóa được và không được tính vào giới hạn 20 | "plugin bug" khi soát / áp / tạo tờ theo chuẩn đó; không cách nào xóa bộ hỏng qua chat | Lời báo rõ "bộ chuẩn hỏng, học lại hoặc xóa"; `list` ghi `damaged`; `delete` xóa được bộ hỏng (qua thẻ); học lại trùng tên bộ hỏng qua thẻ thay thế; bộ hỏng được tính vào giới hạn |
| H4T-2 | Tên bộ chuẩn "CON", "NUL", "COM1", "LPT1"… thành thư mục cùng tên, là tên thiết bị Windows cấm dùng | Học báo lỗi lạ hoặc ghi vào thiết bị | Thư mục thêm hậu tố: `con-chuan`… |
| H4T-3 | Bản chép file mẫu dùng `File.Copy`, cần cấm người khác ghi; file mẫu đang mở trong AutoCAD khác (thường gặp với mẫu dùng chung trên mạng) thì chép không được | Học xong nhưng không giữ khung tên ("could not be copied") | Chép bằng luồng đọc chung (`FileShare.ReadWrite \| Delete`), như phần đọc chung H3 |
| H4T-4 | Học lại trùng tên từ file mẫu > 50 MB, hoặc chép hỏng: bản chép **cũ** của mẫu trước vẫn nằm trong thư mục | Không sai kết quả (JSON ghi không giữ bản chép), nhưng rác trên đĩa và dễ nhầm | Xóa bản chép cũ khi bản mới không được giữ |
| H4T-5 | Layer khác chuẩn (đã Đồng ý) được thay bằng `WblockCloneObjects` chế độ **Replace** | Thay layer 0, layer hiện hành hoặc layer đang có đối tượng có thể bị AutoCAD từ chối (lỗi thô) hoặc làm đối tượng mất liên kết layer | Đặt thẳng màu / nét / độ dày / in / mô tả lên layer đang có (`ApplyLayer`), chỉ chép kiểu nét còn thiếu từ bản chép mẫu; dùng chung cho đường lui khi mất bản chép |
| H4T-6 | `create_sheet company`: khung tên công ty chèn vào **layer hiện hành** | Khung tên nằm trên A-WALL… (in / tắt theo layer đó) | Đặt trên layer khung `KHUNG` như khung tên VN |
| H4T-7 | `create_sheet company` khổ giấy khác khổ của mẫu (khách nói A1, mẫu A3): khung tên đặt theo tọa độ tờ mẫu, không báo | Khung tên lệch / nằm ngoài khung mà Claude không nói | Kết quả có `company_paper_note` nói rõ, gợi ý dùng khổ của mẫu |
| H4T-8 | Sau `check_drawing company`, gợi ý (`hint`) nhóm `nonstandard_styles` vẫn bảo "fix_drawing nonstandard_styles"; `fix_drawing` không biết là soát theo chuẩn công ty nên sẽ áp kiểu **VN** | Claude có thể đổi chữ của khách sang kiểu VN sau khi soát theo chuẩn công ty | Gợi ý nhóm theo chuẩn khi có `company` chỉ sang `setup_standards company` + `set_entity_props`; `fix_drawing` nhận `company` và **từ chối** nhóm theo chuẩn trước khi quét ("Nothing was changed") |
| H4T-9 | File mẫu đơn vị khác bản vẽ (mét / mm): kiểu kích thước / ghi chú chép sang giữ cỡ theo mét, không báo | Chữ kích thước quá nhỏ / to mà không biết vì sao | `setup_standards company` thêm ghi chú về đơn vị (cỡ chữ chuẩn vẫn đổi đúng) |

## 2. Đã soát, không phải sửa

- **Đọc mẫu:** layer (bỏ layer xref), kiểu chữ (bỏ kiểu shape, font SHX thiếu thì ghi tên file), kiểu kích thước (bỏ kiểu con
  `$`), ghi chú mũi tên, kiểu đang dùng, khổ giấy (quay 90° / 270°, inch → mm), khung nhìn lớn nhất, khung tên (block có thuộc
  tính, bỏ xref / layout), đơn vị. File mẫu đọc qua `OtherDrawing.Open` (không khóa, không lưu).
- **Lưu:** tên có ký tự cấm bị từ chối; khác hoa / thường hoặc có / không dấu là cùng một bộ (thư mục theo tên không dấu); thư mục
  chỉ gồm `a-z0-9-` nên `..` không thoát ra ngoài (có test); xóa chỉ xóa thư mục của bộ đó; tên layer tiếng Việt qua JSON giữ
  nguyên.
- **Thẻ:** học lại trùng tên và xóa đều qua thẻ (`F5_CARD_TOOLS`, mã thẻ theo hành động + thư mục).
- **Áp chuẩn:** chép kiểu / layer còn thiếu với `DuplicateRecordCloning.Ignore`. Kiểu trùng tên giữ nguyên (đúng "Quyết định tự
  chọn" của H4). Clone một record bảng không kéo theo đối tượng bản vẽ, chỉ kéo theo kiểu nét / kiểu chữ / block mũi tên nó trỏ
  tới.
- **Soát:** luật layer / kiểu / cỡ chữ theo bộ công ty, không lẫn với bộ VN (`std`, `LayerOk` / `TextOk` / `DimOk` / `MlOk`).
- **Gói / chế độ:** nhóm 2D (gói 3D không có lệnh, prompt 3D không nhắc); chạy được khi "Chỉ xem"; không ghi vào macro.
- **Bảng `H4Numbers`:** 20, 1 000, 100, 10, 12, 50 MB, 60, 2 mm khớp code và bàn giao H4.

## 3. Test thêm

- Lõi `Core.Tests/NangCapH4TestSuaTests.cs`: 18 test, gồm bộ hỏng, tên thiết bị, thư mục luôn nằm trong kho, xóa chỉ xóa thư mục của
  bộ đó, tên tiếng Việt, khổ giấy ± 2 mm, đổi cỡ chữ mét → mm, và kiểm mã nguồn cho các bản sửa H4T-3…9.
- Trợ lý `agent/test/nangcaph4test.test.ts`: 3 test, gồm tham số plugin đọc có trong schema, các câu của checklist, `fix_drawing company`.
- Sửa 2 test cũ của H4 cho khớp cách làm mới (không nới):
  - `NangCapH4Tests`: mã thẻ xóa theo `shown`, để xóa được cả bộ hỏng;
  - `nangcaph4.test.ts`: `fix_drawing` nay có `company`, nhưng chỉ để từ chối.
- Lõi 1548 / 1548, trợ lý 425 / 425, build net48 / net8.0-windows / net10.0-windows qua (xóa `obj` / `bin` giữa các lần).

## 4. Con số chờ chủ dự án duyệt

Không thêm số nào khách thấy. Hậu tố thư mục `-chuan` cho tên thiết bị là chữ, không phải số.

## 5. Quyết định tự chọn

- Layer khác chuẩn: **đặt lại thuộc tính** thay vì thay bằng clone (an toàn với layer 0 / layer hiện hành / layer có đối tượng).
  Mô tả layer chỉ ghi khi chuẩn có mô tả.
- `fix_drawing` có `company` chỉ để từ chối nhóm theo chuẩn: không tự áp kiểu công ty, đúng quyết định của H4.
- Khung tên công ty ở khổ giấy khác: vẫn đặt theo tọa độ mẫu và **báo**, không tự dời (không biết khung tên công ty neo vào góc
  nào).
- Bộ hỏng: không tự xóa; báo và để khách chọn học lại hoặc xóa qua thẻ.

## 6. Thử tay

Thêm mục F (F1–F8) vào `docs/checklist-dot-h4.md`.

## 7. Chưa kiểm được

- AutoCAD thật: các mục trong mục 7 của bàn giao H4, thêm:
  - chép file mẫu đang mở ở AutoCAD khác (F1);
  - đặt lại layer 0 / layer hiện hành (F2).
- Khung nhìn của layout mẫu chưa từng mở: H4 bỏ khung nhìn đầu tiên, coi đó là khung nhìn của chính layout. Với layout chưa mở
  bao giờ, AutoCAD có thể chưa tạo khung nhìn đó, khi đó khung nhìn thật đầu tiên bị bỏ qua. Ghi lại để thử tay (D1 với file mẫu
  vừa tạo layout mà chưa mở); chưa sửa vì cần AutoCAD thật để biết.

## 8. CI

(ghi sau khi chạy)
