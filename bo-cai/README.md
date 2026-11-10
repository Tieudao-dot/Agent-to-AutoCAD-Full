# Bộ cài ClaudeCAD

## Tải bộ cài (luôn là bản mới nhất)

**[⬇ ClaudeCAD-Setup.exe](https://github.com/Tieudao-dot/Agent-to-Autocad/releases/latest/download/ClaudeCAD-Setup.exe)**

- Link này không đổi: mỗi khi plugin có bản mới, link tự trỏ sang bộ cài mới. Không cần đăng nhập GitHub.
- Các bản trước và file khác (`ClaudeCAD-<version>.zip`, `install.ps1`, `latest.json`): [trang phát hành](https://github.com/Tieudao-dot/Agent-to-Autocad/releases).
- File bộ cài không nằm trực tiếp trong thư mục này vì GitHub không nhận file lớn hơn 100 MB trong repo (bộ cài khoảng 140 MB vì kèm sẵn Node.js và Claude Code). CI tự build và đăng bộ cài mỗi khi có phiên bản mới trên `main`.

## Kiểm tra file tải về

Trang [phát hành](https://github.com/Tieudao-dot/Agent-to-Autocad/releases/latest) ghi kích thước và mã SHA-256 của `ClaudeCAD-Setup.exe`. Nếu bộ cài báo "The source file is corrupted", mở PowerShell trong thư mục chứa file và chạy `Get-FileHash .\ClaudeCAD-Setup.exe` — mã khác với trang phát hành nghĩa là file tải về bị hỏng: tải lại vào ổ máy (không chạy từ ổ mạng / USB), hoặc tạm tắt phần mềm diệt virus khi cài.

## Cài đặt (4 bước)

1. Tải `ClaudeCAD-Setup.exe` ở link trên.
2. Chạy file. Nếu Windows hiện "Windows protected your PC" → **More info → Run anyway** (bộ cài chưa ký số). Không cần quyền admin, không cần cài Node.js hay Claude Code. Nên đóng AutoCAD trước khi cài.
3. **Kích hoạt:** bộ cài hiện **mã máy** và dòng *"Liên hệ Zalo owner: 0344644415"*. Chủ plugin tự nhận email có tên máy và key; nhắn Zalo cho chủ plugin để nhận key, dán vào ô **Key kích hoạt** rồi bấm **Next**. Key dùng vĩnh viễn trên máy đó. Máy đã kích hoạt rồi (cài đè, cập nhật) thì không hỏi lại.
4. Trang cuối bộ cài: giữ ô **Đăng nhập Claude ngay** rồi bấm **Finish** → trình duyệt mở trang cấp quyền, bấm **đồng ý** bằng tài khoản Claude Pro/Max (một cửa sổ đen hiện lên và tự đóng khi xong). Không cần mở AutoCAD. Máy đã đăng nhập sẵn thì ô này không hiện. Sau đó mở AutoCAD 2022–2025 là dùng được (nếu bỏ qua bước này, lần đầu mở AutoCAD plugin tự mở trang đăng nhập).

Sau khi cài: gõ `CLAUDECHATSELFTEST` để tự kiểm tra (báo cáo ✓/✗ mở trong Notepad). Mở khung chat: `CLAUDECHAT`, `CC` hoặc **Ctrl+Shift+Space**.

## Cập nhật

- Khung chat tự kiểm tra bản mới mỗi ngày và báo trong chat. Gõ `CLAUDECHATUPDATE` để tải; bản mới được cài khi đóng AutoCAD.
- Hoặc tải lại `ClaudeCAD-Setup.exe` ở link trên và chạy đè. Skill cậu đã thêm/sửa, macro, mẫu lệnh, lịch sử chat đều được giữ nguyên.

## Cài cho nhiều máy

- Cài ngầm, không hỏi gì: `ClaudeCAD-Setup.exe /VERYSILENT /KEY=XXXX-XXXX-XXXX-XXXX` (key của đúng máy đó; mỗi máy vẫn cần bấm **Đăng nhập Claude** một lần).
- Hoặc bằng PowerShell: giải nén `ClaudeCAD-<version>.zip`, đặt `install.ps1` cạnh thư mục `ClaudeCAD.bundle`, chạy `powershell -ExecutionPolicy Bypass -File install.ps1`.

## Cấp key (chủ plugin)

- Mỗi lần có máy chạy bộ cài (hoặc mở khung chat khi chưa kích hoạt), Gmail của chủ plugin nhận email **"[ClaudeCAD] Yêu cầu kích hoạt: <tên máy> (<user>)"** có mã máy và **KEY**. Đồng ý thì gửi key cho người đó qua Zalo; không đồng ý thì bỏ qua — không có key thì không cài và không dùng được.
- Key chỉ dùng được trên đúng máy có mã đó, vĩnh viễn. Cài lại Windows / thay mainboard thì mã máy đổi, cần key mới.
- Máy không có mạng, cài máy chủ kích hoạt, tạo key bằng tay: [`activation/README.md`](../activation/README.md).

## Khi có lỗi trên máy khác

Người dùng máy đó bấm **⚙ ▸ Gửi báo cáo lỗi** (hoặc nút cùng tên trên thẻ lỗi) → file `ClaudeCAD-baocao-<tên máy>-<ngày giờ>.zip` hiện trên Desktop → gửi file đó cho người hỗ trợ. Nên gõ `CLAUDECHATSELFTEST` trước để báo cáo có kết quả tự kiểm tra mới nhất.

File gồm: thông tin máy / AutoCAD / plugin, trạng thái trợ lý và tài khoản, log 3 ngày gần đây, kết quả tự kiểm tra, log cài đặt. **Không** có lịch sử chat, bản vẽ, ghi chú dự án; API key và token trong log được che.

## Đổi tài khoản AI

**⚙ ▸ Tài khoản AI**: dòng "Đang dùng: …" cho biết tài khoản hiện tại.

- **Đổi tài khoản**: đăng xuất rồi đăng nhập tài khoản Claude khác trên trình duyệt.
- **Đăng xuất**: đăng xuất trên máy này (cả Claude Code nếu máy có dùng).
- **Dùng API key**: dán API key Anthropic (console.anthropic.com, trả theo lượng dùng). Key được lưu mã hóa cho riêng tài khoản Windows đó và được ưu tiên hơn tài khoản Claude; **Xóa key đã lưu** để quay lại tài khoản Claude.

Chỉ dùng được Claude (tài khoản Pro/Max/Team hoặc API key Anthropic); plugin không chạy với ChatGPT hay Gemini.

## Gỡ cài đặt

Windows **Settings → Apps → ClaudeCAD for AutoCAD → Uninstall** (hoặc `install.ps1 -Uninstall`). Dữ liệu của cậu (lịch sử chat, macro, mẫu lệnh) nằm trong `%LocalAppData%\ClaudeCAD` và `%AppData%\ClaudeCAD`, xóa tay nếu không cần nữa.

## Lịch sử phiên bản

| Phiên bản | Nội dung chính |
| --- | --- |
| 0.9.5 *(đang làm, chưa phát hành)* | **Giao diện theo phong cách AutoCAD:** khung chat lấy màu và font từ chính theme palette của AutoCAD (giống Properties, Layer) thay vì màu tự đặt; đổi Color theme trong AutoCAD thì khung chat đổi ngay. Cỡ chữ theo AutoCAD đến khi cậu tự chọn cỡ. **Lớp vỏ kiểu palette AutoCAD:** góc vuông, tin nhắn dạng khối phẳng, thanh công cụ là hàng icon, mọi nút có tooltip giải thích ngắn; ⚙ thành lưới 2 cột với nhóm thu gọn; thanh **hạn mức** dưới thanh công cụ; thẻ xác nhận in to số đối tượng + nút *Xem trên bản vẽ*; thẻ lỗi gọn. **Gửi thêm ý khi Claude đang làm:** gõ rồi Enter, tin tự gửi khi lượt xong (có nút Bỏ). Dòng "đang làm" có thời gian + số thao tác, **Esc** trong ô nhập để dừng. **Thu gọn khung chat (nút ⌄):** palette co lại chỉ còn ô nhập, Enter gửi nhanh; lượt xong hiện một dòng báo, bấm để mở rộng và xem lại nguyên đoạn chat; khi Claude cần cậu đồng ý thì palette tự mở. Đang làm tiếp: icon trạng thái ở thanh dưới AutoCAD. |
| **0.9.4** | **Khóa bằng key theo máy:** bộ cài hiện mã máy + "Liên hệ Zalo owner: 0344644415", tự gửi email cho chủ plugin (tên máy, user Windows, mã máy, key); chỉ cài tiếp khi nhập đúng key. Plugin kiểm tra giấy phép mỗi lần mở AutoCAD (chép thư mục / cài bằng .zip cũng bị hỏi key), thẻ **🔒 Kích hoạt** trong khung chat. Key vĩnh viễn; cập nhật không hỏi lại. Kèm 30 bản sửa sau đợt rà soát toàn dự án: xóa/sửa hàng loạt luôn hỏi xác nhận, tự gửi lại không nhầm bản vẽ, macro bo góc đúng đường, Claude chỉ đọc thư mục skill, báo cáo lỗi không lẫn nội dung chat, không vẽ trùng khi lệnh nặng, cuộc chat dài mở nhanh, cài ngầm `/VERYSILENT /KEY=` chạy được. **Skill theo từng đoạn chat:** + Add skill chọn nhiều file cùng lúc (vào thư viện và dùng luôn), danh sách Skill có phần *Đang dùng* (nút −) và *Thư viện* (nút +), chat mới dùng skill của chat gần nhất, bỏ ô Chuẩn. Chạy bền hơn: lỗi plugin không làm AutoCAD đóng đột ngột, khởi động lại trợ lý không sót tiến trình, ⚙ mở nhanh hơn, bộ cài không treo ở trang cuối. |
| 0.9.3 | **Đăng nhập Claude ngay trong bộ cài** (ô "Đăng nhập Claude ngay" ở trang cuối — trình duyệt mở, bấm đồng ý, không cần mở AutoCAD); lần đầu mở AutoCAD mà chưa đăng nhập thì plugin **tự mở** trang đăng nhập; bộ cài bỏ ~2.200 file `.map` không cần thiết (nhẹ hơn, ít bị diệt virus quét chặn); chữ trong ⚙ và bảng chọn layer không còn bị cắt; trang phát hành ghi kích thước + SHA-256 của bộ cài. |
| 0.9.2 | **Toast** thông báo ngay trong khung chat; **giới hạn sử dụng AI** báo thẳng trong cuộc chat (cảnh báo khi sắp hết, thẻ khi hết với giờ làm mới và nút tự gửi lại); **Gửi báo cáo lỗi** (một file .zip trên Desktop: thông tin máy, log, tự kiểm tra, log cài đặt — không có chat, bản vẽ, API key); bộ cài luôn ghi log cài đặt; mục **Tài khoản AI** trong ⚙: xem đang dùng tài khoản nào, **Đổi tài khoản**, **Đăng xuất**, **Dùng API key** (lưu mã hóa). |
| 0.9.1 | Cài 3 bước: kèm Node.js, nút **Đăng nhập Claude** ngay trong khung chat (không cần cài Claude Code / mở terminal), tự thêm thư mục plugin vào danh sách tin cậy của AutoCAD (hết cảnh báo SECURELOAD), thẻ **Bắt đầu** lần đầu mở AutoCAD, link tải cố định. |
| 0.9.0 | Lệnh `CLAUDECHATSELFTEST`; so biên bản lúc AutoCAD rảnh, lưu phiên bản nhanh, bản vẽ lớn cập nhật dần, macro chạy theo lô; sửa 14 lỗi từ lượt soát code thứ ba. |
| 0.8.0 | Chọn layer rồi chat chỉnh sửa: layer của vùng chọn (bỏ tick, → Cả layer), nút **Layer**. |
| 0.7.0 | Claude biết phần cậu sửa tay (kể cả lần mở sau / máy khác); lịch sử phiên bản 30 phút một lần, giữ 10 bản, mở tab chỉ xem. |
| 0.6.0 | Claude tự nhìn bản vẽ, nút Chụp, gợi ý bước tiếp theo, macro, lưu mẫu lệnh, tạo skill từ cuộc chat, Chỉ xem, ghi chú dự án, gõ `@`, phím tắt, thẻ kế hoạch, bảng Thay đổi. |
| 0.5.0 | **+ Add skill** và quản lý skill trên khung chat. |
| 0.4.0 | Khung chat 20 tính năng (chọn đối tượng/điểm, ảnh, Excel, mẫu `/`, lịch sử theo bản vẽ, hoàn tác lượt, …). |
| 0.3.0 | AutoCAD 2022–2025, bộ cài, cập nhật tự động. |

Chi tiết từng tính năng: [`docs/palette-features.md`](../docs/palette-features.md). Danh sách cần thử trên AutoCAD thật: [`docs/test-log.md`](../docs/test-log.md).
