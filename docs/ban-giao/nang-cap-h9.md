# Bàn giao: Nâng cấp đợt H9 — giao diện tiếng Anh (26/11/2026)

- **Thread:** `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết).
- **Nhánh:** `full/nang-cap-h9`, tách từ `full/nang-cap-h8-test` @ `886e699` (đã có H6 / H7 / H8 và các bản sửa).
- **Kiểm:** chưa thử trên AutoCAD thật. Thử tay: [`docs/checklist-dot-h9.md`](../checklist-dot-h9.md).

## 1. Khách thấy gì

- **Chọn ngôn ngữ trong ⚙:** ⚙ ▸ Chung — Giao diện ▸ **Ngôn ngữ / Language**: Tiếng Việt (mặc định) hoặc English.
- **Chữ trên khung chat đổi ngay:** nút, gợi ý, ⚙, các bảng, thẻ, dòng trạng thái, nhãn lệnh, lời báo lỗi, thẻ kích hoạt / bắt đầu, bong bóng thanh trạng thái, file xuất chat, tên bước và dòng tổng của SelfTest.
- **Claude trả lời theo ngôn ngữ mới từ tin nhắn tiếp theo,** vẫn trong cùng cuộc chat. Khi chọn English:
  - Claude không xưng "tớ / cậu";
  - thẻ, bước và gợi ý viết bằng tiếng Anh;
  - khách xác nhận được bằng "yes / ok / go ahead" (vẫn nhận "đồng ý").
- **Bộ cài:** hộp đầu tiên chọn ngôn ngữ, Tiếng Việt chọn sẵn. Các trang bộ cài tự viết (kích hoạt, AutoCAD đang mở, file đang dùng, thay bản gốc, trang cuối) theo ngôn ngữ chọn. Lựa chọn này là ngôn ngữ của plugin ở lần mở đầu tiên. Cài ngầm dùng `/LANG=english`.
- **English chỉ đổi giao diện và câu trả lời, chữ trên bản vẽ giữ tiếng Việt** (chủ dự án chốt 26/11/2026 ~20h45). Đã ghi vào `bo-cai/README.md` và `docs/palette-features.md`.
- **Máy đang dùng không thấy gì đổi:** mặc định vẫn Tiếng Việt, plugin không gửi `language` cho trợ lý.

## 2. Cách làm

- **C# — `Core/Lang.cs`, bảng `Core/LangEn.cs`:**
  - `L.Tr("câu tiếng Việt")` / `L.Tr("mẫu {0}", x)`; trong `ChatControl` / `ChatController` gọi `Tr(...)` qua `using static`.
  - Câu tiếng Việt là khóa. Chưa có bản dịch, hoặc bản dịch hỏng chỗ trống `{0}`, thì hiện tiếng Việt, không lỗi.
  - Bảng có 929 câu.
- **TS — `agent/src/i18n.ts`, bảng `agent/src/i18nEn.ts`:**
  - `tr()` cho lời báo lỗi của trợ lý và tóm tắt kết quả lệnh (28 câu).
  - Plugin gửi `context.language = "en"` chỉ khi khách chọn English.
  - Ngôn ngữ đổi thì Claude được khởi động lại với cùng cuộc chat (như khi đổi gói / chế độ), cả tiến trình chờ sẵn (F4).
- **Lời dặn Claude (`systemPrompt.ts`, `ENGLISH_SECTION`):**
  - đặt cuối prompt, thắng mọi câu "in Vietnamese" ở trên;
  - chữ Claude đưa vào bản vẽ giữ tiếng Việt;
  - nút đọc theo tên tiếng Anh (Yes / No, Do it / Edit plan / Skip, Both / 2D / 3D).
- **Đổi ngay:**
  - `ChatControl.Lang.cs` (`Relabel`) đi qua mọi chữ cố định đang hiện, cả popup / menu đang đóng, và đổi nguyên câu Việt ↔ Anh theo bảng. Ô khách gõ không bị đụng.
  - Câu có số (trạng thái, đếm) được code viết lại khi hiện lần sau.
  - Đoạn chat được vẽ lại (`LoadTranscript`); badge gói và dòng kết nối được viết lại.
- **Mã giữ tiếng Việt:**
  - tên bước "Kích hoạt" / "Tài khoản Claude" của thẻ Bắt đầu và câu mẫu `WelcomeExample` dùng làm mã so sánh, nên giữ nguyên trong code và chỉ dịch lúc hiện;
  - dòng nào cố ý giữ tiếng Việt thì ghi `// vi`.
- **Bộ cài (`installer/ClaudeCAD.iss`):**
  - hai `[Languages]` (vietnamese trước), `ShowLanguageDialog=yes`;
  - 55 câu trong phần code chuyển sang `[CustomMessages]` (`vietnamese.H9MsgNN` / `english.H9MsgNN`);
  - `FinishedLabel` và nút "Đăng nhập Claude ngay" có cả hai thứ tiếng;
  - `ssPostInstall` ghi `{app}\Contents\language.txt` (`vi` / `en`);
  - plugin đọc file này khi chưa có `settings.json` (`ChatController.InstallerLanguage`).

## 3. Quyết định tự chọn

| # | Quyết định | Lý do / đổi lại được không |
| --- | --- | --- |
| 1 | **Chữ vẽ vào bản vẽ giữ tiếng Việt khi chọn English** (chủ dự án chốt 26/11/2026 ~20h45). Gồm: tiêu đề bảng, khung tên, tên tờ, tên cọc, mô tả layer. | Hồ sơ nộp ở Việt Nam theo chuẩn Việt Nam. Khách nhờ Claude đổi từng chữ được. Có test kiểm các file vẽ chữ không gọi `Tr` |
| 2 | **File Excel giữ tiếng Việt như bảng trên bản vẽ** (luồng chính chọn 26/11): tiêu đề, "Cộng Ø", "Tổng" | Bảng vừa chèn vào bản vẽ vừa xuất Excel, và `XlsxLogic.DataRows` nhận dòng "Tổng" bằng cách so chữ. Tài liệu khách ghi "(cả bảng xuất Excel)" |
| 3 | Không dịch skill | Claude đọc skill tiếng Việt rồi trả lời tiếng Anh. Rẻ, an toàn, skill khách sửa không phải giữ hai bản |
| 4 | Lời báo plugin gửi cho Claude (lỗi lệnh, NOT_IN_PLAN, "Chỉ xem"…) không dịch | Claude đọc rồi tự nói lại bằng ngôn ngữ khách chọn. Đã dặn trong `ENGLISH_SECTION` |
| 5 | SelfTest: chỉ dịch tên bước và dòng tổng; chi tiết kỹ thuật từng bước giữ tiếng Việt | Phần chi tiết là để người phát triển đọc (khoảng 240 câu kỹ thuật). Dịch thêm thì tốn và dễ sai mà khách không cần |
| 6 | Gói báo cáo lỗi (`DiagnosticsBundle`) giữ tiếng Việt | Gửi cho người hỗ trợ Việt Nam, không phải để khách đọc |
| 7 | Đổi ngôn ngữ: chữ đổi ngay; Claude đổi từ **tin nhắn tiếp theo** | Luồng chính duyệt là "từ chat mới"; làm từ tin nhắn tiếp theo vẫn đúng ý đó và nhanh hơn. Claude được khởi động lại với cùng cuộc chat, không mất ngữ cảnh |
| 8 | Ngôn ngữ chọn ở bộ cài chỉ áp dụng cho máy chưa có cài đặt khung chat | Máy đang dùng không bị đổi ngôn ngữ khi cài đè |
| 9 | Bộ cài dùng `Default.isl` cho cả hai ngôn ngữ | Inno Setup không có sẵn file tiếng Việt chính thức. Nút chuẩn (Next / Back…) vẫn tiếng Anh như bản trước; các câu khách hay vướng thì đã tiếng Việt |
| 10 | Dòng liên hệ mặc định "Liên hệ Zalo owner…" có bản tiếng Anh. Dòng chủ plugin tự đặt (`update-config.json` / `activation.json`) giữ nguyên chữ chủ plugin viết | Không đoán bản dịch chữ của chủ plugin. Trang kích hoạt trong bộ cài English vẫn hiện dòng liên hệ đã cấu hình (mặc định tiếng Việt) |
| 12 | **Xuất chat ra Markdown theo ngôn ngữ giao diện lúc xuất** (luồng chính chọn 26/11) | Đây là chữ giao diện, không phải chữ trên bản vẽ |
| 13 | Câu dặn Claude "tell the user in Vietnamese" đổi thành "in the user's language" / "in the language of the user" | Prompt gốc nói ngôn ngữ của khách là tiếng Việt, trừ khi phần "# Language" ở cuối nói khác. Rẻ, không phải nhánh hai bản prompt |
| 14 | `install.ps1` (cài bằng PowerShell cho người quản trị) giữ tiếng Việt | Dùng cho người cài hộ nhiều máy, không phải khách |
| 11 | Claude nhận thêm "yes, ok, sure, proceed, go ahead, do it…" là đồng ý; "no, not, don't, stop, cancel, wait" là không. **Từ phủ định ở bất kỳ đâu trong câu là không đồng ý** (cả tiếng Việt: "ok, nhưng đừng xóa tường" = không; "không sao" vẫn là đồng ý) | Không chắc thì không đồng ý. Luật khối 3D vẫn chỉ nhận nút trên thẻ |

## 4. Con số chờ chủ dự án duyệt

Không có số mới khách thấy.

## 5. Bảng thuật ngữ (để người biết nghề soát)

| Tiếng Việt | English | Ghi chú |
| --- | --- | --- |
| Khung chat | chat palette / palette | |
| Đoạn chat, cuộc chat | chat | |
| Lượt | turn | một lần Claude làm việc cho một tin nhắn |
| Chủ plugin | plugin owner | |
| Gói (2D / 3D / Full) | plan | |
| Chế độ Cả hai / 2D / 3D | mode Both / 2D / 3D | |
| Chỉ xem / Sửa được | View only / Editable | |
| Kích hoạt, mã máy, key kích hoạt | activate / activation, machine code, activation key | |
| Mã miễn kiểm tra (CCS1) | no-check code | |
| Dòng gói (CCF1), dòng giấy phép (CCL1) | plan line, license line | |
| Trợ lý | assistant | tiến trình Node chạy Claude |
| Hạn mức 5 giờ / tuần | 5-hour limit / weekly limit | |
| Thẻ xác nhận: Đồng ý / Không | confirmation card: Yes / No | |
| Thẻ kế hoạch: Làm / Sửa kế hoạch / Bỏ | plan card: Do it / Edit plan / Skip | "Bỏ" cũng là nút bỏ tin đang chờ gửi |
| Mẫu lệnh | command template | |
| Ghi chú dự án | project notes | |
| Phiên bản (bản sao trước khi sửa) | version | |
| Biên bản thay đổi | change log | |
| Nạp gọn lệnh | Load tools on demand | |
| Tự kiểm tra (SelfTest) | self-test | |
| Mức suy nghĩ | effort | |
| Tỷ lệ in | plot scale | |
| Kiến trúc / Điện nước / Hạ tầng | Architecture / MEP / Infrastructure | |
| Lưới trục | axis grid | câu mẫu "Draw a 5x4 axis grid with 4000 spacing" |
| Đường trục (xline) | construction line | |
| Mây sửa đổi | revision cloud | |
| Thống kê block / bảng diện tích / bảng khối lượng | block schedule / area table / volume table | |
| Bảng thống kê thép | bar schedule | |
| Móng | footing | |
| Mặt cắt / hình chiếu | section / projection | |
| Hạ tầng: điểm đo, đồng mức, trắc | infrastructure: survey points, contours, sections | |
| Ra bộ hồ sơ / Lập dự toán / Soát bản vẽ | make a drawing set / cost estimate / check the drawing | |

## 6. File

- **Mới:**
  - `plugin/ClaudeCAD.Plugin/Core/Lang.cs`, `Core/LangEn.cs`, `UI/ChatControl.Lang.cs`;
  - `agent/src/i18n.ts`, `agent/src/i18nEn.ts`;
  - `plugin/ClaudeCAD.Core.Tests/NangCapH9Tests.cs` (10 test + `InnoMessages.Cm` cho test bộ cài cũ);
  - `agent/test/nangcaph9.test.ts` (7 test);
  - `plugin/ClaudeCAD.UI.Tests/NangCapH9LanguageUiTests.cs` (2 UI test);
  - `docs/checklist-dot-h9.md`, bàn giao này.
- **Sửa (bọc `Tr`):**
  - `UI/ChatControl.cs` (thêm ô Ngôn ngữ), `ChatController.cs` (đổi ngôn ngữ, ngôn ngữ từ bộ cài, `context.language`);
  - `UI/ToolLabels.cs` (dịch lúc tra), `UI/StatusTray.cs`, `UI/MarkdownView.cs`, `UI/ImageAttachment.cs`, `PluginApp.cs`, `SelfTest.cs` (khung báo cáo), `Drawing/Interaction.cs`;
  - `Sidecar/*` (Activation, SidecarManager, UpdateService, ClaudeAuth, ApiKeyStore);
  - `Core/` (License, Plans, SkillStore, SkillSync, ChatStore, ChatExtras, Macros, QuickTemplates, DrawingHistoryCore, F5, PaletteSettings: thêm `Language`).
- **Trợ lý:**
  - `session.ts` (ngôn ngữ, khởi động lại, lời báo lỗi), `systemPrompt.ts` (`ENGLISH_SECTION`), `protocol.ts` (`language`);
  - `content.ts` (tóm tắt lệnh), `confirmation.ts` (chữ đồng ý tiếng Anh).
- **Bộ cài:** `installer/ClaudeCAD.iss`.
- **Tài liệu:** `bo-cai/README.md` (mục *Ngôn ngữ / Language*, bước 2 cài đặt, dòng lịch sử H9), `docs/palette-features.md`.
- **Test cũ sửa theo cách làm mới (không bỏ test nào):**
  - `NangCapF5Tests` (ô Nạp gọn lệnh qua `Tr`);
  - `EditionTests`, `TongTheNangCapTests` (câu bộ cài nằm trong `[CustomMessages]`).

## 7. Chỗ người rà nên soi

1. **Chữ vừa hiện vừa làm mã so sánh.** Tớ đã tìm và giữ các chỗ sau:
   - tên bước thẻ Bắt đầu;
   - `WelcomeExample`;
   - câu "chưa đăng nhập" (khung chat tìm cả bản tiếng Anh "not signed in").

   Còn sót chỗ nào thì nút hoặc thẻ sẽ không phản ứng khi chọn English.
2. **Câu ghép bằng nối chuỗi** dịch từng mảnh (vd "Bong bóng ở icon " + tên + " trên thanh…"). Mỗi mảnh đều có bản dịch, nhưng câu tiếng Anh ghép lại có thể chưa mượt.
3. **`Relabel` chỉ đổi được câu nằm nguyên trong bảng.** Chữ có số (dòng trạng thái, "Skill — 2 in use") chỉ đổi khi code viết lại nó. Chữ trên tin nhắn cũ của khách và câu trả lời cũ của Claude giữ nguyên (cố ý).
4. **Bộ cài chưa được biên dịch trên Linux.** CI Windows biên dịch nó. Pascal Script phải nhận được `[CustomMessage('…'), CustomMessage('…')]` trong mảng nút của `TaskDialogMsgBox`.
5. **Chữ tiếng Việt không dấu** (vd "Theo AutoCAD", "low — nhanh") không bị test bắt. Tớ đã bọc các chỗ thấy được.

## 7b. 9 điểm người rà nêu trước (luồng chính chuyển 26/11 14h50)

| # | Điểm | Đã làm |
| --- | --- | --- |
| 1 | Câu dặn "tell the user in Vietnamese" viết cứng (~15 chỗ C# + TS) | Đổi thành "in the user's language" (C#) / "in the language of the user" (TS) trong mọi lời dặn và lời báo cho Claude: `Plans.cs`, `ToolDispatcher.cs`, `Util2D.cs`, `Solids.cs`, `Solids2.cs`, `Standards2D(Tools).cs`, `BatchFilesTools.cs`, `CompanyStandardTools.cs`, `Util2DTools.cs`, `F5.cs`, `systemPrompt.ts`, `confirmation.ts`, `solids.ts`, `utility2d.ts`, `interaction.ts`, `drawingCheck.ts`. Prompt gốc: "the user's language is Vietnamese unless a # Language section at the end says otherwise". Test trợ lý: không còn "tell the user … in Vietnamese" ngoài câu mặc định |
| 2 | Lời dặn bắt nói nguyên văn tiếng Việt ("cỡ mẫu, kỹ sư quyết", tên nút Đồng ý / Làm / Chỉ xem / Dừng) | Giữ nguyên trong lời dặn (test H8 so khít). `ENGLISH_SECTION` có bảng đổi tên nút: Đồng ý = Yes, Không / Hủy = No, Làm = Do it, Sửa kế hoạch = Edit plan, Bỏ = Skip, Chỉ xem = View only, Dừng = Stop, Chat mới = New chat, Cả hai = Both. Ngoài ra dặn Claude dịch cụm tiếng Việt trong kết quả lệnh (ví dụ "cỡ mẫu, kỹ sư quyết") |
| 3 | Nhận xác nhận gõ tay | Thêm sure / go ahead / do it / proceed…; thêm phủ định don't / not / cancel / stop / wait; **từ phủ định ở bất kỳ đâu trong câu là không đồng ý** (trước đây chỉ xét 4 chữ đầu). Test trợ lý: "yes but don't delete the walls", "ok, but not the doors", "đồng ý, nhưng đừng xóa tường" → không; "ok, không sao" → có |
| 4 | Bảng vừa chèn bản vẽ vừa xuất Excel | Giữ tiếng Việt (luồng chính chọn). Không bọc `Tr` ở Rebar / Estimate / Xlsx / DrawingSet; test H9 kiểm các file này không gọi `Tr`. Tài liệu khách ghi "(cả bảng xuất Excel)" |
| 5 | Chuỗi code so khít để nhận diện | Không đụng: "cột" / "dầm", "TƯỜNG", "cửa" (nằm trong file vẽ chữ, không bọc). `_readyInfo.Contains("·")` so chữ của trợ lý, không dịch. SelfTest / UI test tìm nút theo chữ tiếng Việt vẫn đúng vì mặc định là tiếng Việt. **Thêm UI test English** `NangCapH9LanguageUiTests` (2 test, chạy riêng không song song): khung chat mở bằng English; khung chat đang mở đổi sang English rồi về lại; chữ khách đã gõ không bị đổi |
| 6 | Chữ ghép bằng nối chuỗi | Bọc cả câu có chỗ trống: Activation (tạm khóa / chờ duyệt / chưa có máy chủ / đã gửi yêu cầu / lưu gói), Macros (sửa khối 3D có sẵn), ChatControl (bong bóng icon, ghi chú dự án), ChatController (chưa đăng nhập xong). Plans "Không chạy macro…", ChatExtras "Thêm {0} · Sửa {1} · Xóa {2}", License Reason, SidecarManager "lần {0}/{1}" đã là cả câu. Còn vài câu dài ghép hai câu trọn (vd "✓ Đã lưu macro…" + ", hoặc Tại điểm…"), mỗi mảnh dịch được |
| 7 | Chữ giao diện sinh ở trợ lý | `tr()` cho tóm tắt lệnh (`content.ts`) và lời báo lỗi (`session.ts`); plugin gửi `context.language` trong mỗi tin nhắn và trong lần khởi động sẵn |
| 8 | Bộ cài | Đã làm như mục 2 (`[CustomMessages]` có tiền tố ngôn ngữ, ngôn ngữ ghi vào `{app}\Contents\language.txt`, plugin đọc khi chưa có cài đặt). Bộ cài chạy quyền người dùng (`PrivilegesRequired=lowest`), nhưng vẫn ghi vào `{app}` như người rà đề nghị. `install.ps1` giữ tiếng Việt (Quyết định tự chọn 14) |
| 9 | Khác | H6 / H7 / H8: chỉ lời báo đi qua giao diện được dịch; tên cọc, nhãn ống, bảng thép giữ tiếng Việt (không bọc ở Terrain / Mep / Rebar / StructureTools). Xuất chat Markdown theo ngôn ngữ lúc xuất (quyết định 12). Dòng liên hệ mặc định: C# (`L.Tr(DefaultContact)` → "Contact the owner on Zalo: …") và bộ cài (`ActivationContactEn`, chỉ khi dòng liên hệ là mặc định) cùng bản tiếng Anh |

## 8. Test

- Lõi **1681 / 1681** (thêm 10 test H9).
- Trợ lý **464 / 464** (thêm 7 test H9).
- UI test WPF: thêm 2 test English (chỉ chạy trên CI Windows); project build qua trên Linux.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi; 1 cảnh báo cũ ở `TraceTools.cs`, không thuộc H9.
- CI: ghi ở mục 9.

## 9. CI

(ghi sau khi chạy)
