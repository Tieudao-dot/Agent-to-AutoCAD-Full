# Bàn giao: Nâng cấp đợt G4 — render thật (25/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người viết). Nhánh `full/nang-cap-g4`, tách từ `full/nang-cap-g3` @ `395c313`
(G1 + G2 đã gộp). Chạy cùng lúc với G3 (dự toán, thread F2): file dùng chung chỉ sửa vài dòng (mục 5). Chưa thử trên
AutoCAD thật.

## 1. Khách thấy gì

- Nói "render", "ra ảnh đẹp" (gói 3D / Full, chế độ "3D" / "Cả hai"): Claude gọi `named_view` `action` `render`. AutoCAD render
  góc nhìn đã lưu (`name`) hoặc góc nhìn đang xem ra file PNG cạnh bản vẽ: `<bản vẽ>-<góc nhìn>-render.png` (không có tên góc:
  `-hien-tai-render.png`). Khách chọn được mức đẹp (nhanh / vừa / đẹp), cỡ ảnh, nền (trắng / trời / của bản vẽ), ánh sáng.
- Khung chat hiện "Góc nhìn / ảnh render: đang render 20 s / khoảng 60 s"; quá ước lượng thì "lâu hơn dự kiến; tự dừng ở … s".
  AutoCAD không bị treo trong lúc chờ.
- Dừng: nút **Dừng** trong khung chat hoặc **Esc** trong AutoCAD. Không có file mới, nền / ánh sáng / góc nhìn trở lại như cũ.
- Đường lui: lệnh render lỗi, AutoCAD không có lệnh render, hoặc quá thời gian chờ tối đa → lưu ảnh chụp kiểu F3 (Realistic,
  cả mô hình, cạnh dài tối đa 1568 px) vào **đúng file đó**, kết quả `method: "capture"` kèm lý do; Claude phải nói rõ "đây là
  ảnh chụp, không phải ảnh render". Render xong: `method: "render"`.
- Nền và ánh sáng chỉ đổi **trong lúc render** (tắt ghi hoàn tác), xong tự trả lại; kết quả liệt kê đã đổi tạm gì
  (`changed_for_render`). Không có gì phải U.
- File đã có: thẻ Đồng ý / Hủy (thẻ ghi đè của F5), xin **trước** khi đổi bất cứ gì. "Chỉ xem": render vẫn chạy (như
  `image`), nhưng dùng nền / ánh sáng của bản vẽ, không đổi gì, có ghi chú. Đang có lệnh AutoCAD dở: từ chối, nhờ bấm Esc.
- Vật liệu: "gạch", "bê tông", "kính", "gỗ", "kim loại", "ngói", "sơn"… Bản vẽ đã có vật liệu thư viện Autodesk hợp tên (vd
  "Masonry - Brick - Red" kéo vào từ Materials Browser) thì dùng nó (`source: "library"`); không thì vật liệu màu đơn giản của
  F3 (`source: "simple"`) và `library_note` nói máy có thư viện hay không, cách lấy bản thư viện (MATBROWSEROPEN, kéo vào).
  Vật liệu khách tự đặt tên không bị đổi. `library: false` = luôn dùng màu đơn giản.

## 2. Cách làm

- **Không thêm lệnh mới**: một `action` mới của `named_view` (đã hoãn nạp từ F4). Hai `action` nội bộ `render_status` /
  `render_stop` chỉ trợ lý dùng, không có trong mô tả cho Claude.
- AutoCAD không có giao diện lập trình để render: plugin gõ `-RENDER` (`RenderLogic.CommandScript`: mức đẹp `_Low` / `_Medium` /
  `_High`, nơi hiện `_R` Render Window, rộng, cao, lưu file `_Y`, tên file; mỗi câu kết thúc bằng Enter nên đường dẫn có dấu cách
  vẫn là một câu; `_` = từ khóa tiếng Anh cho mọi ngôn ngữ AutoCAD), render vào file tạm trong thư mục Temp rồi trả lời ngay.
- Trợ lý (`agent/src/tools/render.ts`, `runRender`) hỏi `render_status` mỗi 2 s, hiện tiến độ, trả kết quả cuối cho Claude
  trong **một** lần gọi. Biết render xong: file tạm có và cỡ file không đổi 1,5 s → chép sang file của khách, xóa file tạm.
- Hủy: sự kiện `CommandCancelled` / `CommandFailed` / `UnknownCommand` của lệnh RENDER; nút Dừng gọi
  `DrawingService.StopRender()` (plugin) và trợ lý gửi `render_stop`; Esc chỉ được gửi khi lệnh đang chạy chính là RENDER.
- Phòng hờ: trợ lý tự dừng nếu quá thời gian chờ tối đa + 60 s mà plugin chưa trả lời xong; render cũ không ai hỏi tới quá thời
  gian chờ tối đa + 60 s thì lần render sau dọn đi.
- Lõi (không cần AutoCAD, có test): `Core/Render.cs` — `G4Numbers`, `RenderLogic` (mức đẹp, cỡ ảnh, ước lượng, câu lệnh,
  trạng thái, tiến độ, lý do đường lui, kế hoạch ánh sáng, tên file), `MaterialLibraryLogic` (từ khóa tên thư viện, chọn vật
  liệu, thư mục thư viện, câu báo). Phần AutoCAD: `Drawing/RenderG4.cs`.

## 3. Đúng với

- Gói 3D / Full; chế độ "3D" / "Cả hai" của máy Full; gói 2D không có (nhóm ThreeD như F3). Nhãn lệnh: "Góc nhìn / ảnh render".
- Hoãn nạp: `named_view` không nằm trong nhóm nạp sẵn (test).
- Macro: render là lệnh đọc → không ghi vào macro (như `image`).
- UCS: không đổi (`named_view` vẫn trong nhóm WCS của F1).

## 4. Tự kiểm

Bước "Nâng cấp đợt G4" trong `SelfTest.cs`: đặt nền trắng + đèn mặc định như lúc render rồi trả lại (nền khung nhìn đúng như cũ,
không sót nền tạm trong bản vẽ), đường lui ghi ảnh chụp, vật liệu thư viện có sẵn ("Masonry - Brick - SelfTest") được dùng cho
"gạch". Không render thật trong tự kiểm (lệnh render chỉ chạy sau khi lệnh tự kiểm xong).

## 5. File

- Mới: `plugin/ClaudeCAD.Plugin/Core/Render.cs`, `plugin/ClaudeCAD.Plugin/Drawing/RenderG4.cs`,
  `plugin/ClaudeCAD.Core.Tests/NangCapG4Tests.cs` (19 test), `agent/test/nangcapg4.test.ts` (9 test), `docs/checklist-dot-g4.md`, file này.
- Sửa ít dòng (dùng chung với G3): `Drawing/RenderTools.cs` (đăng ký action, vật liệu thư viện), `Core/Materials.cs` (tên action,
  tên gọi "sơn", "kim loại"), `Drawing/ToolDispatcher.cs` (`ReadOnlyTurn`), `ChatController.cs` (Dừng dừng render; dòng trạng thái
  hiện tiến độ của lệnh đang chạy), `SelfTest.cs`, `UI/ToolLabels.cs`, `ClaudeCAD.Core.Tests.csproj`, `agent/src/tools/render.ts`,
  `agent/src/agent/systemPrompt.ts` (một câu), `skills/ve-3d-kien-truc/SKILL.md`, `bo-cai/README.md`.

## 6. Con số chờ chủ dự án duyệt

Tất cả ở `Core/Render.cs` (`G4Numbers`, `RenderLogic.Qualities`, `MaterialLibraryLogic.Words`); trợ lý có `RENDER_POLL_MS`.

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Cỡ ảnh mặc định | 1920 × 1080 | cỡ file render | Full HD, vừa màn hình / slide |
| Cỡ ảnh khách chọn được | 64 – 4096 px mỗi cạnh; cho một cạnh thì cạnh kia theo 16:9 | — | giới hạn an toàn bộ nhớ |
| Mức đẹp | nhanh = preset Low, vừa = Medium (mặc định), đẹp = High | chất lượng / thời gian | preset có sẵn của AutoCAD |
| Thời gian ước lượng (Full HD) | nhanh 20 s, vừa 60 s, đẹp 180 s; nhân theo số điểm ảnh, ít nhất 5 s | dòng tiến độ | ước lượng thô, chưa đo trên máy thật |
| Thời gian chờ tối đa | nhanh 120 s, vừa 300 s, đẹp 900 s | quá thì dừng, lưu ảnh chụp | không treo im; đủ cho máy văn phòng |
| Nền mặc định | trắng (255, 255, 255) | nền ảnh | an toàn, dễ in |
| Nền trời | dải màu trên (110, 160, 215), giữa (185, 210, 235), dưới (240, 240, 240) | nền ảnh | trời xanh nhạt, không cần mặt trời |
| Ánh sáng mặc định | "auto": đèn / mặt trời của bản vẽ nếu có, không thì đèn mặc định của AutoCAD | độ sáng ảnh | không đổi ánh sáng khách đã đặt |
| Giờ nắng | **chưa đặt**: "sun" bật mặt trời của bản vẽ với ngày giờ của nó; bản vẽ chưa có mặt trời → đèn mặc định | — | đặt giờ cần vị trí địa lý; để đợt sau nếu cậu muốn |
| Từ khóa vật liệu thư viện | bê tông: concrete; gạch: brick; vữa / sơn: plaster, paint, stucco; kính: glass, glazing; gỗ: wood, oak, walnut, maple, cherry, pine; ngói: roofing, shingles, roof; thép / kim loại: steel, metal, aluminum; đá: stone, granite, marble | vật liệu nào được chọn | khớp nguyên từ trong tên thư viện; nhiều cái thì lấy cái đầu theo tên |
| Tên gọi thêm | "sơn", "son", "paint" → vữa trắng; "kim loại", "kim loai" → thép | — | yêu cầu đợt G4 |

Số kỹ thuật (khách không thấy): hỏi trạng thái mỗi 2 000 ms; file coi là ghi xong khi cỡ không đổi 1,5 s; phòng hờ thêm 60 s.
*(Test & sửa G4 thêm: lệnh RENDER đã xong mà chưa có file sau 20 s → đường lui ngay, `G4Numbers.EndGraceSeconds`; thư mục
file tạm chỉ chữ / số ASCII không dấu cách, xem `docs/ban-giao/nang-cap-g4-test.md`.)*

## 7. Quyết định tự chọn

1. **Một action của `named_view`** thay vì lệnh mới: không thêm mô tả nạp sẵn, cùng chỗ với ảnh F3. Đổi lại được: tách lệnh sau.
2. **Gõ `-RENDER`** (không có giao diện lập trình render). Thứ tự câu hỏi theo AutoCAD 2016+ (chưa kiểm được trên tài liệu: mạng
   chặn help.autodesk.com); nằm ở một hàm `CommandScript` để sửa nhanh. Hỏi sai thứ tự → cùng lắm chờ hết thời gian tối đa
   rồi ra ảnh chụp.
3. **Trợ lý chờ, không phải plugin**: plugin chờ sẽ khóa AutoCAD và render không chạy được.
4. **Nền / ánh sáng / góc nhìn đổi tạm và trả lại**, tắt ghi hoàn tác → không có gì phải U, không rác trong lịch sử U. Rủi ro:
   AutoCAD tắt giữa lúc render thì nền tạm còn lại (khách đổi lại bằng tay, hoặc lần render sau không bị ảnh hưởng).
5. **Vật liệu thư viện chỉ dùng khi đã có trong bản vẽ** (AutoCAD không cho chương trình lấy từ thư viện); báo rõ cách lấy.
6. Render dùng **góc nhìn hiện tại** khi không có tên (khác `image` mặc định sw_iso): "ra ảnh đẹp" là ảnh của cái khách đang nhìn.
7. Đường lui ghi vào **cùng file** (khách đã đồng ý ghi file đó), cạnh dài tối đa 1568 px như ảnh F3.
8. Dừng thì **không** tự ra ảnh chụp (khách chủ động dừng); muốn thì gọi `image`.
9. Dòng trạng thái khung chat: một lần báo "đang chạy" có kèm tiến độ thay dòng đang chạy (không thêm dòng mới mỗi 2 s).

## 8. Token lúc mở chat

- System prompt gói 3D: 19 626 → 19 806 ký tự (+180, khoảng 50 token); Full: 19 515 → 19 695 (+180). Gói 2D không đổi.
- Mô tả `named_view`: 2 227 → 3 343 ký tự (+1 116, khoảng 320 token); `set_material`: 1 898 → 2 302 (+404, khoảng 115 token).
  Hai lệnh hoãn nạp (mặc định) nên tin đầu không thêm; chỉ khi tắt "Nạp gọn lệnh".
- Đo bằng số ký tự (khoảng 3,5 ký tự / token), không phải số token chính xác.

## 9. Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1294** (1275 + 19 `NangCapG4Tests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **385** (376 + 9 `test/nangcapg4.test.ts`). Không sửa / bỏ test cũ.
- Build 3 target (xóa obj / bin giữa các lần): 0 cảnh báo.

## 10. CI

- Run **#62** (`45649202227`, `release=false`) trên `7ddbf12` (commit code cuối): **xanh** (job `windows`: build 3 target, lõi,
  trợ lý, UI test WPF). Dùng 1 / 2 lượt. Commit sau đó chỉ thêm tài liệu bàn giao.

## 11. Chưa kiểm được

- AutoCAD thật: thứ tự câu hỏi của `-RENDER` trên 2022 – 2027; render có chạy nền sau khi lệnh xong không và Esc / Dừng có ngắt
  được không; nền trắng / trời có vào ảnh render không; `DefaultLightingOn` / `Sun.IsOn` của khung nhìn; thời gian render thật
  (để chỉnh số ước lượng); thư mục thư viện vật liệu; bước tự kiểm G4. Thử tay: `docs/checklist-dot-g4.md`.
