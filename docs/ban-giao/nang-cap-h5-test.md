# Bàn giao: Test & sửa đợt H5 — làm nhiều file một lúc (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người rà). Người viết H5 là thread đợt F2.

- Nhánh `full/nang-cap-h5-test`, tách từ `full/nang-cap-h5` @ `5acde3b` (code `a6ac65d`).
- Chưa chạy trên AutoCAD thật.

## 1. Đã soát

| Điểm | Kết quả |
| --- | --- |
| 1. An toàn file khách | Đúng thứ tự: sao lưu → đọc vào bộ nhớ → ghi file tạm **cùng thư mục** → `File.Replace`.<br>• Ghi hỏng giữa chừng (hết chỗ, mất mạng, AutoCAD tắt): file gốc còn nguyên vì chỉ file tạm dở; nếu chính `File.Replace` hỏng thì vẫn còn bản sao lưu, và dòng kết quả ghi đường dẫn sao lưu.<br>• Cùng ổ nên không vướng chuyện khác ổ.<br>• Giữ đời DWG (`OriginalFileVersion`); file đời R14 trở về trước không lưu lại được, báo lỗi, file không đổi.<br>• Bỏ qua đúng: file chỉ đọc, file có `.dwl` / `.dwl2`, file đang mở ở đây.<br>• Đường dẫn quá dài: lỗi xảy ra lúc tạo thư mục sao lưu, trước khi ghi gì.<br>• Thư mục sao lưu không bị quét lại. Hai lượt dùng hai mốc giờ nên không ghi đè sao lưu của nhau.<br>• **1 lỗi** (H5T-02). |
| 2. Thẻ và kế hoạch | Mã thẻ gồm việc, thông số và danh sách file. Thêm / bớt file sau khi lập kế hoạch thì thẻ không khớp, bị từ chối. Thư mục khác thì mã khác. Dừng chỉ xảy ra giữa hai file. Một file lỗi không dừng lượt. **1 lỗi** (H5T-03): bị chặn giữa lượt thì mất danh sách các file đã làm. |
| 3. Áp chuẩn lên database khác | `SetupStandards` / `SetupCompanyStandard` chỉ dùng `ctx.Db` / `ctx.Tr` (không dùng `Doc` / `Ed` / UCS). `WorkingDatabase` được đổi tạm rồi trả lại trong `finally`. Kiểu khác chuẩn được giữ (không có thẻ cho từng file). |
| 4. In PDF | **1 lỗi nặng** (H5T-01). File đang mở sẵn: in ở tab đó. PDF trùng tên: thẻ của lượt nói rõ số file bị thay. Layout không có thiết lập in: theo `plot_pdf`. |
| 5. Gói, Chỉ xem, giới hạn | `RequireToolInPlan` kiểm lệnh con của từng việc. "Chỉ xem" chặn áp chuẩn. `batch_files` không được ghi vào macro (đăng ký là lệnh đọc). Giới hạn 200 file / 5 cấp / 200 MB đúng. |
| 6. Mô tả lệnh / prompt | Chưa có thẻ thì chỉ lập kế hoạch. Không hứa `fix_drawing` / so sánh hàng loạt. |
| 7. `H5Numbers` | Khớp bảng của H5. |

## 2. Lỗi đã sửa

| Mã | Mức | Lỗi | Sửa | Test |
| --- | --- | --- | --- | --- |
| H5T-01 | **Nặng** | In PDF hàng loạt dừng ngay sau file đầu. Plugin mở file và đưa nó lên trước, khung chat tưởng **khách chuyển bản vẽ giữa lượt** nên hủy lượt ("Cậu đã chuyển sang bản vẽ khác…") | `BatchDocSwitches` (`Core/BatchFiles.cs`) nhớ file / bản vẽ mà lượt tự mở, trong 30 giây. `ChatController.OnDocumentActivatedCore` bỏ qua những lần chuyển đó khi đang có lượt chạy. Hết lượt thì mọi lần chuyển lại là của khách | lõi `The_drawings_the_run_brings_…`, `The_chat_ignores_those_switches_…` |
| H5T-02 | Vừa | AutoCAD tắt giữa lúc ghi để lại `.claudecad-tam-….dwg` trong thư mục. Lượt sau lấy nó như bản vẽ của khách (soát, áp chuẩn, in) | `Collect` bỏ file có tên `BatchLogic.TempPrefix` | lõi `A_temporary_copy_left_behind_…` |
| H5T-03 | Vừa | Lượt bị chặn giữa chừng (vd có file mới chép vào thư mục nên thẻ không còn khớp) chỉ trả lỗi. Khách không biết file nào đã bị áp chuẩn | `runBatch` trả kết quả có các file đã làm (kèm nơi sao lưu), lý do dừng, và nói các file còn lại chưa đụng tới. Lỗi ở file đầu vẫn trả lỗi như cũ | trợ lý `refused half way…` |
| H5T-04 | Nhẹ | File lỗi (plugin trả lỗi) không có tên trong báo cáo cuối | Lấy tên từ kế hoạch (30 tên đầu), không có thì ghi "#số" | trợ lý `a failing file keeps its name…` |

- Thêm test giữ an toàn file khách: thứ tự sao lưu → đọc → ghi tạm → thay; hai lượt hai sao lưu; thư mục sao lưu không bị quét.
- File sửa:
  - `Core/BatchFiles.cs` (`BatchDocSwitches`, `TempPrefix`, `IsOwnTemp`, `NameOf`);
  - `Drawing/BatchFilesTools.cs` (đánh dấu khi mở / in, tên file tạm);
  - `ChatController.cs` (1 dòng + hàm `DocPath`);
  - `agent/src/tools/batchFiles.ts`.
- Test mới: `Core.Tests/NangCapH5TestSuaTests.cs` (5), thêm 2 vào `agent/test/nangcaph5.test.ts`.
- Tài liệu: `docs/checklist-dot-h5.md` (B9, B10, C1), bàn giao này.

## 3. Nhận xét, không sửa

- **Chưa kiểm được trên AutoCAD thật:** mở / đóng bản vẽ (`DocumentCollectionExtension.Open`) từ lời gọi lệnh của khung chat. Nếu AutoCAD
  không cho mở trong ngữ cảnh đó, mọi file in PDF sẽ báo lỗi (không hỏng gì). Checklist C1 kiểm điều này.
- File tạm bị bỏ sót không được tự xóa: không đụng file trong thư mục khách ngoài file đang làm. Khách tự xóa `.claudecad-tam-*`.
- In PDF một file đang mở ở **tab khác** (không phải tab đang xem) mà còn thay đổi chưa lưu: không phát hiện được "chưa lưu" (AutoCAD chỉ
  cho đọc biến `DBMOD` của tab đang xem), nên in luôn bản trên màn hình.
- `File.Replace` trên ổ mạng: nếu lỗi thì báo lỗi; bản sao lưu luôn có trước.

## 4. Con số chờ chủ dự án duyệt

Không có số mới khách thấy. `BatchDocSwitches.KeepSeconds` = 30 giây là số bên trong (bao lâu sau khi in xong một file thì việc chuyển sang
bản vẽ đó vẫn coi là của lượt).

## 5. Quyết định tự chọn

- H5T-01: bỏ qua lần chuyển bản vẽ do lượt tự làm **chỉ khi đang có lượt chạy**. Hết lượt, khách chuyển sang bản vẽ đó thì khung chat đổi lịch
  sử như thường.
- H5T-03: bị dừng giữa lượt thì không ghi Excel tổng hợp (bước tổng hợp cũng cần thẻ), chỉ báo trong chat.

## 6. Test

- Lõi: 1576 / 1576 qua (thêm 5).
- Trợ lý: 436 / 436 qua (thêm 2).
- Build 3 target (net48, net8.0-windows, net10.0-windows): qua.

## 7. CI

- Lượt 1: run 45798656330 trên `46b93f2`, **xanh**.
  - Log trợ lý: `# pass 436`, `# fail 0`.
  - Lõi 1576 / 1576, UI test 38 / 38, build 3 target qua.
- Commit này chỉ ghi kết quả CI vào tài liệu, code giữ nguyên như `46b93f2`.
