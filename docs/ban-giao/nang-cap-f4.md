# Bàn giao: Nâng cấp đợt F4 — mở chat nhanh hơn, xuất bảng ra Excel

Nhánh `full/nang-cap-f4`: từ `full/nang-cap-f2-test` @ `c9fdc08`, gộp `full/nang-cap-f3-test` @ `53e18b5` (commit gộp
`c67bc79`, CI #46 xanh), rồi việc F4. Người viết: thread "Nâng cấp đợt F1" (`session_01D56HmFAo4dZ7c8sbeNB9eh`), luồng
chính giao 25/11/2026. Kế hoạch: `docs/ke-hoach-nang-cap-lenh.md` mục "Đợt F" (nhánh luồng chính).

Chưa thử trên AutoCAD thật. Thử tay: [`docs/checklist-dot-f4.md`](../checklist-dot-f4.md). Bước tự kiểm
`CLAUDECHATSELFTEST` có thêm 1 bước F4 (xuất Excel).

## Bước 0: gộp F2 + F3

- 5 file xung đột (`systemPrompt.ts`, `bo-cai/README.md`, `Core/Ucs.cs`, `Drawing/DrawingService.cs`, `SelfTest.cs`): cả
  hai bên chỉ thêm phần riêng cạnh nhau → giữ đủ cả hai (phạm vi gói 3D có cả "library blocks / layer states" của F2 lẫn
  "materials and named views" của F3; bước tự kiểm F3 rồi F2). 4 file còn lại gộp tự động.
- Sau gộp: lõi **1087**, trợ lý **328** (đúng số dự kiến: không rơi test), build 3 target, CI **#46** (`45597564054`) xanh.
- Việc nhỏ kèm: màu layer `A-ROOF-3D` thành hằng `RoofLogic.Layer3dColor` (= 30, không đổi giá trị).

## Việc 1: mở chat nhanh hơn

### 1a. Trợ lý khởi động sẵn

- Khi trợ lý (sidecar) báo sẵn sàng, plugin đợi `PrewarmDelayMs` (3 giây, để AutoCAD mở xong trước) rồi gửi tin
  `prewarm` kèm gói / chế độ / skill mà tin nhắn sau sẽ mang. Trợ lý **chỉ khởi động tiến trình Claude**, không gửi gì cho
  Claude → **không tốn token** trước tin đầu của khách. Tin đầu dùng luôn tiến trình đó (test: không khởi động lại, tiến
  trình không nhận gì trước tin đầu).
- Chỉ trên máy đã kích hoạt và được phép (`Activation.IsAllowed`); máy bị khóa không chạy trợ lý nên cũng không khởi động sẵn.
- Gói / chế độ / skill của tin đầu khác lúc khởi động sẵn → khởi động lại như trước (nối tiếp cùng cuộc chat). Đổi nút
  chế độ khi rảnh → khởi động sẵn lại với chế độ mới (vẫn áp dụng từ tin kế tiếp như F1). Gói đổi sau lượt kiểm tra trạng
  thái → khởi động sẵn lại.
- Tự dựng lại: tiến trình Claude đang chờ mà chết → khởi động lại sau `PREWARM.RetryMs` (5 giây), tối đa
  `PREWARM.MaxRestarts` (3) lần liên tiếp không có lượt chat nào (máy cài hỏng thì không lặp mãi); lỗi lúc chờ chỉ ghi log,
  không hiện thẻ lỗi. Sau "Chat mới", chuyển bản vẽ, đổi model / mức suy nghĩ, đổi skill: khởi động sẵn lại. Cả trợ lý
  chết thì plugin vốn tự chạy lại trợ lý; trợ lý báo sẵn sàng → khởi động sẵn lại.
- Plugin cũ không gửi `prewarm` → trợ lý chạy y như trước.
- Code: `agent/src/agent/session.ts` (`prewarm`, `startPrewarmed`, `schedulePrewarm`, `PREWARM`), `ipc/protocol.ts`
  (`PrewarmMessage`), `index.ts`; plugin `ChatController.cs` (`SchedulePrewarm`, `SendPrewarm`, `PlanContext` dùng chung với
  tin nhắn).

### 1b. Chỉ nạp mô tả lệnh cần dùng

- Bật tìm lệnh của Claude Code (`ENABLE_TOOL_SEARCH=true`, thêm công cụ có sẵn `ToolSearch`). 25 lệnh hay dùng
  (`ALWAYS_LOADED_TOOLS` trong `agent/src/tools/index.ts`) + 5 lệnh thẻ được nạp đủ mô tả; **mọi lệnh khác của gói vẫn có**,
  chỉ hiện tên, Claude nạp mô tả bằng ToolSearch khi cần rồi gọi như thường. System prompt có 1 dòng dặn cách nạp và "không
  nói thiếu lệnh khi chưa tìm".
- Test chứng minh không rơi lệnh: với từng gói / chế độ (2D; 3D; Full: Cả hai / 2D / 3D), tập lệnh y như danh sách của gói
  (`Core/Plans.cs`), lệnh nạp đủ = đúng phần `ALWAYS_LOADED_TOOLS` thuộc gói, lệnh thẻ luôn nạp đủ.
- Đánh đổi: lần đầu Claude cần một lệnh chưa nạp, có thêm một bước tìm lệnh (vài trăm – vài nghìn token cho mô tả lệnh đó,
  một lần mỗi cuộc chat).

### Token lúc mở chat (cùng cách đo `test-tong-the-nang-cap.md`: lệnh gửi Claude + 5 lệnh thẻ, tên + mô tả + schema JSON + system prompt, ~3,5 ký tự / token)

| Gói / chế độ | Số lệnh (nạp đủ) | Ký tự trước F4 (`c67bc79`) | Ký tự sau F4 | ≈ token trước → sau | Giảm |
| --- | --- | --- | --- | --- | --- |
| Gói 2D | 109 (24) | 141 765 | 46 944 | 40,5k → 13,4k | **−67 %** |
| Gói 3D | 97 (27) | 146 195 | 65 664 | 41,8k → 18,8k | **−55 %** |
| Full · Cả hai | 142 (30) | 211 824 | 72 269 | 60,5k → 20,6k | **−66 %** |
| Full · chế độ 2D | 109 (24) | 141 988 | 47 167 | 40,6k → 13,5k | **−67 %** |
| Full · chế độ 3D | 97 (27) | 146 418 | 65 887 | 41,8k → 18,8k | **−55 %** |

- "Sau" là **ước tính**: lệnh chưa nạp tính bằng tên đầy đủ `mcp__claudecad__<tên>` + 2 ký tự; định nghĩa của ToolSearch
  (có sẵn trong Claude Code) ước 2 000 ký tự. Số thật do Claude Code quyết, cần đo trên máy thật (dòng mức dùng của lượt đầu).
- Gói 3D giảm ít hơn vì phần quy tắc 3D trong system prompt (~18k ký tự) vẫn nạp đủ.
- Phần F4 thêm (Excel + 3 dòng dặn): +2 157 ký tự ở Full · Cả hai khi nạp đủ (≈ +0,6k token), đã tính trong cột "sau". Dòng dặn Excel nằm ở phần 2D (bảng 2D) và phần 3D (bảng 3D) để gói 2D / 3D không bị nhắc tên lệnh ngoài gói (test `crossplan`).
- Thời gian tới câu trả lời đầu: **chưa đo được, cần AutoCAD thật** (test chỉ chứng minh tin đầu không phải chờ khởi động lại).

## Việc 2: xuất bảng ra Excel

- 4 bảng có sẵn nhận thêm `excel` (true = file `<tên bản vẽ>-<bảng>.xlsx` cạnh bản vẽ; hoặc tên / đường dẫn `.xlsx`) và
  `overwrite`: `block_schedule`, `area_schedule` (đợt C), `volume_schedule`, `check_interference` (đợt D). File có đúng các
  cột của bảng trong bản vẽ (cùng hàm dựng bảng), hàng tiêu đề in đậm.
- Tự viết file .xlsx (`Core/Xlsx.cs`, chuẩn Office Open XML trong file zip, dùng `System.IO.Compression` có sẵn của
  .NET): không thêm thư viện, máy khách không cần Excel. Chữ UTF-8 (tiếng Việt đúng dấu); số (kể cả số dạng chữ "12.50"
  của bảng) ghi thành **số** để Excel cộng được; mã có số 0 đầu ("007"), "1,5", chữ khác giữ là chữ.
- Gói: theo gói của bảng nguồn (bảng 2D trên máy 2D / Full; bảng 3D trên máy 3D / Full — máy 3D không vẽ table được vẫn xuất
  Excel được; test). Chế độ của máy Full theo F1.
- Luật ghi file như `export_stl` / `plot_pdf`: tên phải đuôi `.xlsx`, tên Windows chấp nhận, thư mục phải có; file đã có thì
  **không ghi** và báo Claude hỏi khách rồi gọi lại với `overwrite: true`; ghi qua file tạm rồi mới đổi tên (hỏng giữa chừng
  không để lại nửa file); file đang mở trong Excel → báo nhờ đóng. Bảng vẫn là lệnh đọc (không đổi bản vẽ, dùng được khi bật
  Chỉ xem, như xuất STL). Không có dòng nào → báo, không tạo file (va chạm: "không có va chạm").
- Code: `Core/Xlsx.cs` (`XlsxWriter`, `XlsxLogic`, `F4Numbers`), `Drawing/ExcelExport.cs`, 1 dòng ở mỗi bảng
  (`Util2DTools.cs`, `Solid3Tools.cs`, `Solid2Tools.cs`); trợ lý `tools/common.ts` (`excelParams`), 4 schema, 1 dòng
  system prompt; skill `ve-kien-truc`, `ve-3d-kien-truc` (1 dòng mỗi file).

## Con số chờ chủ dự án duyệt

Excel: tất cả ở `F4Numbers` (`plugin/ClaudeCAD.Plugin/Core/Xlsx.cs`). Mở chat nhanh: `PREWARM`, `TOOL_SEARCH`
(`agent/src/agent/session.ts`), `ALWAYS_LOADED_TOOLS` (`agent/src/tools/index.ts`), `PrewarmDelayMs` (`ChatController.cs`).

| Số | Dùng ở đâu | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Tên file mặc định `<bản vẽ>-thong-ke-block / -dien-tich / -khoi-luong / -va-cham.xlsx` | 4 bảng | tên file cạnh bản vẽ | không dấu cho chắc chắn mở được ở mọi máy / ổ mạng; giống kiểu tên PDF / STL |
| Tên sheet "Thống kê block", "Diện tích", "Khối lượng 3D", "Va chạm khối 3D" | file Excel | tên tab | theo tiêu đề bảng trong bản vẽ, ngắn hơn 31 ký tự (giới hạn Excel) |
| Độ rộng cột = chữ dài nhất + **2**, trong khoảng **6 – 60** ký tự | file Excel | cột vừa chữ | đọc được ngay không phải kéo cột |
| Hàng tiêu đề **in đậm**, font Calibri 11 | file Excel | kiểu chữ | mặc định của Excel |
| Số giữ đúng số lẻ của bảng (vd 12.5 khi bảng ghi 12.50), Excel hiển thị theo định dạng General | file Excel | 12.5 thay 12.50 | để cộng được; khách tự định dạng số lẻ trong Excel |
| Tối đa **100 000** dòng | file Excel | — (các bảng ít hơn nhiều) | chặn danh sách lỗi chạy mãi |
| Đợi **3 giây** sau khi trợ lý sẵn sàng mới khởi động sẵn Claude | mở AutoCAD | AutoCAD mở không chậm thêm | để AutoCAD mở xong trước |
| Tự dựng lại sau **5 giây**, tối đa **3 lần** liền không có lượt chat | trợ lý | — | máy lỗi không lặp mãi |
| **25** lệnh nạp đủ mô tả (+ 5 lệnh thẻ) | mở chat | — | các lệnh hầu như việc nào cũng dùng: đọc bản vẽ, layer, vẽ đường / polyline / chữ nhật / tròn, dời / chép / xóa, chữ, kích thước, offset, khối 3D, đùn, trừ khối, thông tin khối, góc nhìn 3D, dựng 3D từ mặt bằng, chụp hình |

## Quyết định tự chọn

| Tình huống | Đã chọn (rẻ, an toàn, đổi lại được) | Muốn đổi thì |
| --- | --- | --- |
| Cách giảm mô tả lệnh | dùng tính năng tìm lệnh có sẵn của Claude Code (`ToolSearch`, cờ `anthropic/alwaysLoad`) thay vì tự viết "lệnh gọi lệnh": Claude vẫn gọi đúng tên lệnh, plugin không đổi | bỏ dòng `ENABLE_TOOL_SEARCH` + `ToolSearch` trong `session.ts` là về như cũ |
| Bộ lệnh nạp đủ | 25 lệnh chung cho mọi gói (phần ngoài gói tự bỏ) | sửa `ALWAYS_LOADED_TOOLS` |
| Test cũ `session.test.ts` kiểm tập công cụ có sẵn = `['Read', 'Skill']` | sửa đúng dòng đó thành `['Read', 'Skill', 'ToolSearch']` (ToolSearch chỉ nạp mô tả lệnh CAD, không mở file / lệnh hệ thống / web); các kiểm tra cô lập khác giữ nguyên | — |
| Khởi động sẵn khi nào | khi trợ lý báo sẵn sàng (mở AutoCAD / trợ lý tự chạy lại), đổi chế độ, đổi gói, chat mới, chuyển bản vẽ | — |
| Xuất Excel là lệnh mới hay tham số | tham số `excel` của 4 bảng có sẵn (không thêm lệnh, đỡ token, gói tự đúng) | — |
| Ghi đè file Excel | như STL: hỏi khách rồi `overwrite: true` (không dùng thẻ Đồng ý, vì không đổi bản vẽ) | dùng thẻ nếu muốn chặt hơn |
| Số dạng chữ trong bảng | đổi thành số khi là số thường ("12", "12.50", "-3.2"); giữ chữ khi có số 0 đầu, dấu phẩy, đơn vị | — |
| Bản vẽ chưa lưu | file vào Documents (như STL) | — |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1113** (1087 + 26 `NangCapF4Tests.cs`: số / chữ trong ô, tên cột / sheet,
  độ rộng cột, file .xlsx hợp lệ đọc lại được (XML đúng, tiếng Việt, & < >, số có `<v>`, tiêu đề đậm), ghi qua file tạm, giới
  hạn dòng, tham số `excel`, tên mặc định 4 bảng, tên xấu / thư mục thiếu / là thư mục, nối vào 4 bảng, gói 3D xuất được,
  khởi động sẵn ở plugin, hằng màu mái).
- Trợ lý `cd agent && npm ci && npm test`: **347** (328 + 19 `test/nangcapf4.test.ts`: tin `prewarm`, khởi động không gửi gì,
  tin đầu không khởi động lại, khác gói / chế độ thì khởi động lại, đang chạy thì bỏ qua, chết thì dựng lại và dừng sau giới
  hạn, chat mới / chuyển bản vẽ, plugin cũ không đổi; 5 gói × chế độ không rơi lệnh; ToolSearch + cờ bật; 4 bảng có
  `excel`, gói 3D giữ `excel`). Test cũ sửa 1 dòng (xem "Quyết định tự chọn").
- Build 3 target (net48, net8.0-windows, net10.0-windows; xóa obj / bin giữa các lần; `-p:EnableWindowsTargeting=true`).

## Chưa kiểm được (cần AutoCAD / Claude thật)

- Claude Code thật có hoãn đúng các lệnh không nạp và tìm ra lệnh bằng ToolSearch (số token thật của tin đầu); thời gian tới
  câu trả lời đầu; tiến trình `claude.exe` khởi động sẵn không gọi API trước tin đầu (theo thiết kế SDK: chỉ gửi khi có tin);
  AutoCAD mở không chậm hơn; file Excel mở bằng Excel thật (test đọc lại cấu trúc file, chưa mở bằng Excel). Xem
  `docs/checklist-dot-f4.md`.

## CI

- Gộp: run **#46** (`45597564054`, `release=false`) trên `c67bc79`: **xanh**.
- F4: run **#47** (`45601966332`, `release=false`) trên `587f7ce`: **xanh** (build 3 target, lõi 1113, trợ lý 347, UI test
  WPF). Đây là bản code cuối; commit sau chỉ thêm mục này. Dùng 2 / 3 lượt CI cho phép.

## Đã kiểm trước khi báo

- Đọc lại diff F4; sau lần sửa code cuối (dời dòng dặn Excel về phần 2D / 3D của system prompt): build lại 3 target (xóa
  obj / bin), lõi 1113 xanh, trợ lý 347 xanh, UI test build được, rồi CI #47.
