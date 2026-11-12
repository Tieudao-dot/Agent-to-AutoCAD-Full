# Tổng quan dự án ClaudeCAD

*Cập nhật: 12/11/2026 (giờ Việt Nam). Luồng chính cập nhật mục "Tình hình" và "Nhật ký" khi có mốc mới.*

File này là tổng quan duy nhất của dự án. Nó gộp các file cũ `plugin-tong-quan.md` và `ke-hoach-gop-nhanh.md`.

## 1. Tình hình

- **Bản đang phát hành:** v0.9.3 (09/11), nhận AutoCAD 2022–2025.
- **Bản thử:** toàn bộ phần đã làm sau v0.9.3 nằm trên nhánh tích hợp `claude/release-0.9.5`. CI xanh, chưa phát hành.
  - Gồm: khóa key theo máy, giao diện kiểu AutoCAD, AutoCAD 2026 / 2027, checklist thử thật.
  - Nhánh đã gộp cả 5 nhánh, và cặp khóa kích hoạt đã được đổi mới.
- **Bộ cài thử:** tải từ [run 43000757496](https://github.com/Tieudao-dot/Agent-to-Autocad/actions/runs/43000757496/artifacts/13847355266).
  - Tên `ClaudeCAD-0.9.4`, ~340 MB.
  - Bộ cài từ các run trước mang khóa cũ, không dùng.
- **Đang chờ cậu:** thử bộ cài trên AutoCAD 2026 theo [`checklist-thu-that.md`](checklist-thu-that.md), phần A (key) và phần H (2026 / 2027).
  - Hạn: trước khi bản dùng thử hết hạn, khoảng 10/12/2026.
  - Cần hỗ trợ khi cài thì hỏi ở thread cài đặt.
- **Tác vụ nền:** không thread nào đang chạy việc.

## 2. Việc cần cậu quyết (trước khi phát hành)

1. **AutoCAD 2027:**
   - giữ trong bản phát hành;
   - ghi là "thử nghiệm";
   - hoặc tạm bỏ tới khi thử được trên máy thật.
2. **Tên phiên bản và thời điểm phát hành:** 0.9.5 hay 1.0.0. Phải merge `main` mới phát hành, nên chỉ làm khi cậu đồng ý.
3. **Các mục key ngoài 20 phút thử:**
   - mạng: K-5, K-6;
   - chỉnh đồng hồ: K-7;
   - chép giấy phép sang máy khác và các mục còn lại: K-11 … K-16.

   Cậu chọn thử trước khi phát hành hay để sau.

## 3. Các bước còn lại tới phát hành

1. Cậu thử bộ cài theo checklist (~20 phút). Phần A–E lỗi thì chặn phát hành; phần F–H sửa sau được.
2. Thread gộp nhánh sửa theo phản hồi, chạy lại CI cho xanh, ghi kết quả vào `test-log.md`.
3. Cậu đồng ý phát hành. Thread gộp nhánh nâng phiên bản và merge `main`, rồi CI tự phát hành bộ cài.
   - Máy chủ kích hoạt đã gắn sẵn, nên bước chặn phát hành của CI sẽ qua.
   - Lỗi `install.ps1` của v0.9.3 (dòng `"Node.js v$major: OK"`) đã được sửa và hết lỗi ở bản này.
4. Sau phát hành:
   - viết hướng dẫn cho người dùng cuối;
   - tách `ChatControl.cs` cho dễ bảo trì;
   - **vẽ 3D + gói dịch vụ theo máy (2D / 3D / 2D + 3D)**, dự kiến 0.10.0. Plan ở
     [`ke-hoach-3d-goi-dich-vu.md`](ke-hoach-3d-goi-dich-vu.md), đang chờ cậu chốt 5 điểm.

## 4. Các thread

| Thread | Phạm vi | Nhánh | Trạng thái | Chi phí quy đổi* |
| --- | --- | --- | --- | --- |
| [Luồng chính](https://claude.ai/code/session_01D56HmELvEuaLYUjB1TYfNq) | Điều phối, tổng hợp, việc chưa có thread nhận | `claude/wizardly-heisenberg-ugup5c` | Chờ kết quả thử | ~$391 |
| [Gộp nhánh & phát hành](https://claude.ai/code/session_01D56HmErdvF4wWAbxpf9F5t) | Gộp, bộ cài thử, sửa theo phản hồi, nâng phiên bản, phát hành | `claude/release-0.9.5` | Chờ kết quả thử | ~$8,9 |
| [Cài đặt plugin](https://claude.ai/code/session_01D56HmFkjV3poaV54WEEFYJ) | Hướng dẫn cài / kích hoạt, gỡ rối, giúp chạy checklist; không sửa code | — | Đã gửi hướng dẫn, chờ cậu | ~$0,5 |
| [Bảo mật, khóa key & thiết bị](https://claude.ai/code/session_01D56HmEsRHYufkFu48qFmGR) | Key, kích hoạt, Apps Script / Google Sheet | `claude/device-management` (đã gộp) | Xong; máy chủ Google chạy khóa mới | ~$66 |
| [Giao diện & UX](https://claude.ai/code/session_01D56HmHQPBueRAkYDSecN9n) | Khung chat, theme, thu gọn, icon thanh trạng thái | `claude/ux-autocad` (đã gộp) | Xong | ~$10,7 |
| [Kiểm thử](https://claude.ai/code/session_01D56HmG5SsqRGRPe9bHKUad) | Checklist thử thật, tự kiểm tra, test-log | `claude/test-checklist` (đã gộp) | Xong | ~$7,9 |
| [AutoCAD 2026/2027](https://claude.ai/code/session_01D56HmFfmat4UZDMPVZar76) | Bản AutoCAD mới, build .NET 10 | `claude/acad-2026-2027` (đã gộp) | Xong | ~$7,1 |
| [QnA](https://claude.ai/code/session_01D56HmGkaq6YFfr46Xdigci) | Hỏi đáp về plugin, không sửa code | — | Rảnh | ~$5,8 |
| [Dùng thử & Autodesk](https://claude.ai/code/session_01D56HmGdeNRHtH8Vob4cXGr) | Bản dùng thử AutoCAD (từ 10/11, 30 ngày), case Autodesk 26568211 | — | Chờ email Autodesk | ~$1,1 |

\* Số hệ thống ghi nhận cho từng thread, quy đổi theo giá API. Tổng cộng khoảng $500, trong đó luồng chính chiếm
~78% vì ngữ cảnh dài. Việc mới nên làm ở thread ngắn.

Các nhánh đã gộp coi như đóng băng; sửa tiếp làm trên `claude/release-0.9.5`. Quy tắc làm việc và danh bạ thread nằm ở
[`CLAUDE.md`](../CLAUDE.md).

## 5. Plugin làm gì

ClaudeCAD thêm khung chat (palette) vào AutoCAD. Người dùng gõ yêu cầu bằng tiếng Việt, ví dụ "vẽ mặt bằng phòng 4×6 m, tường
220, cửa đi 900". Claude đọc bản vẽ đang mở rồi vẽ, sửa, đo hoặc trả lời ngay trên bản vẽ. Mọi thay đổi của một lượt gộp
thành một bước hoàn tác.

| Nhóm | Chi tiết |
| --- | --- |
| Vẽ và sửa | 89 lệnh cho Claude:<br>• hình học: đường, polyline, cung;<br>• chú thích: chữ, kích thước, hatch;<br>• tổ chức bản vẽ: block, layer, style;<br>• chỉnh sửa: fillet / chamfer, di chuyển / xoay / sao chép / xóa;<br>• zoom…<br><br>Có lệnh gộp nhiều thao tác một lần để vẽ nhanh |
| Hiểu bản vẽ | Đọc layer, đối tượng, đơn vị; đo, đếm; biết người dùng đã sửa tay gì giữa các lượt |
| Theo chuẩn công ty | Skill là file `.md` mô tả quy chuẩn. Có 3 skill mẫu: kiến trúc, MEP, hạ tầng. Mỗi đoạn chat chọn skill riêng |
| Nhập yêu cầu | • `/` chọn mẫu lệnh, `@` gọi tên layer / block;<br>• chọn đối tượng hoặc lấy điểm trên bản vẽ;<br>• đính ảnh phác thảo, chụp vùng bản vẽ;<br>• giới hạn layer được sửa |
| Macro | Lưu một lượt vẽ ưng ý rồi chạy lại mà không cần Claude, không tốn token |
| An toàn | • Hỏi trước khi xóa / sửa nhiều đối tượng (có xem trước); việc nhiều bước thì trình kế hoạch trước.<br>• Hoàn tác cả lượt bằng một lần U.<br>• Lưu bản sao DWG 30 phút một lần, giữ 10 bản.<br>• Chế độ "Chỉ xem".<br>• Dữ liệu bản vẽ không bao giờ được coi là lệnh |
| Giao diện (bản thử) | • Màu và font theo theme AutoCAD.<br>• Hàng nút icon có tooltip.<br>• Thu gọn còn ô nhập; tự mở lại khi cần xác nhận.<br>• Icon và bong bóng thông báo trên thanh trạng thái.<br>• Gửi thêm ý khi Claude đang làm; Esc để dừng.<br>• Hiện tiến độ từng bước |
| Tài khoản AI | Claude Pro / Max hoặc API key Anthropic; xem hạn mức đã dùng |

**Cách chạy:**
1. Plugin gom ngữ cảnh: bản vẽ, đối tượng đang chọn, layer, ghi chú dự án, skill, phần đã sửa tay.
2. Plugin gửi ngữ cảnh qua named pipe cho trợ lý. Trợ lý là một tiến trình Node chạy cạnh AutoCAD, dùng Claude Agent SDK.
3. Trợ lý chuyển từng lệnh Claude chọn về cho plugin.
4. Plugin thực hiện lệnh trong AutoCAD và trả kết quả lại.

| Thành phần | Công nghệ |
| --- | --- |
| Plugin | C# / WPF. Ba bản build:<br>• .NET Framework 4.8: AutoCAD 2022–2024 (R24.1–R24.3);<br>• .NET 8: AutoCAD 2025–2026 (R25.0–R25.1);<br>• .NET 10: AutoCAD 2027 (R26.0) |
| Trợ lý | Node.js + TypeScript, Claude Agent SDK. Node đi kèm bộ cài |
| Bộ cài | `ClaudeCAD-Setup.exe` (Inno Setup), một bộ chung cho 2022–2027. Có trang kích hoạt và trang đăng nhập Claude. Cài ngầm: `/VERYSILENT /KEY=<key>` |
| Kích hoạt | • Mỗi máy có mã `CC-XXXX-XXXX-XXXX`, được cấp giấy phép có chữ ký số.<br>• Chủ plugin duyệt hoặc khóa từng máy trong Google Sheet "Thiết bị" (Active / Disabled / Chờ duyệt) |

**Lệnh:**
- `CLAUDECHAT` (tắt `CC`, hoặc `Ctrl+Shift+Space`): mở khung chat;
- `CLAUDECHATRESTART`: khởi động lại trợ lý;
- `CLAUDECHATUPDATE`: cập nhật;
- `CLAUDECHATSELFTEST`: tự kiểm tra;
- `CLAUDECHATLOG`: mở log.

Khi lỗi, vào ⚙ ▸ *Gửi báo cáo lỗi* để tạo file .zip (không chứa nội dung chat, bản vẽ hay key).

**Dữ liệu:** lịch sử chat, cài đặt, macro và các bản sao lưu nằm trên máy (`%LocalAppData%\ClaudeCAD`). API key được mã hóa
theo tài khoản Windows. Chỉ nội dung yêu cầu và dữ liệu bản vẽ cần cho lượt đó được gửi tới Claude.

**Kiểm thử tự động:**
- 164 test lõi, 136 test trợ lý, UI test WPF trên Windows;
- build cả 3 bản .NET và bộ cài mỗi lần CI chạy.

**Giới hạn:**
- Cần mạng và phụ thuộc hạn mức tài khoản Claude.
- Không chạy trên AutoCAD LT.
- AutoCAD sau 2027 sẽ không tự nạp cho tới khi được khai báo.
- Nhiều mục chỉ kiểm được trên AutoCAD thật (theme, thanh trạng thái, chọn đối tượng, phím tắt, kích hoạt). Các mục này còn
  "chưa chạy" trong `test-log.md`.

## 6. Lịch sử phiên bản

| Phiên bản | Nội dung | Trạng thái |
| --- | --- | --- |
| 0.4 – 0.9.3 | Khung chat 20+ tính năng, skill, macro, phiên bản 30 phút, ghi nhận sửa tay, chọn layer, báo cáo lỗi, tài khoản AI, đăng nhập trong bộ cài | Đã phát hành (v0.9.3 ngày 09/11) |
| 0.9.5 (bản thử, gồm phần từng ghi 0.9.4) | • Khóa key theo máy + quản lý thiết bị.<br>• ~30 bản sửa sau rà soát, rồi 3 đợt sửa tiếp.<br>• Skill theo đoạn chat.<br>• Giao diện kiểu AutoCAD.<br>• AutoCAD 2026 / 2027.<br>• Checklist thử thật + tự kiểm tra mở rộng | Đã gộp, CI xanh, chờ thử trên AutoCAD thật. csproj vẫn ghi 0.9.4 tới khi chốt tên |

## 7. Tài liệu khác

- `bo-cai/README.md`: cài đặt và lịch sử phiên bản cho người dùng.
- `checklist-thu-that.md`: checklist thử trên AutoCAD thật.
- `test-log.md`: kết quả kiểm thử.
- `autocad-2026-2027.md`: chi tiết hỗ trợ AutoCAD 2026 / 2027.
- `ke-hoach-3d-goi-dich-vu.md`: plan vẽ 3D + gói dịch vụ theo máy.
- `ux-autocad.md`: thiết kế giao diện.
- `palette-features.md`: từng tính năng khung chat.
- `skills/README.md`: cách viết skill.
- `trang-thai/*.md`: trạng thái từng nhánh lúc gộp.

## 8. Nhật ký hằng ngày

*Dòng mới thêm lên đầu, giữ 14 ngày gần nhất.*

| Ngày | Tình hình |
| --- | --- |
| 12/11 | Không có commit code. Nghiên cứu xong và viết plan vẽ 3D + gói dịch vụ theo máy, chờ cậu chốt. Vẫn chờ cậu thử bộ cài trên AutoCAD 2026 |
| 11/11 | Gộp xong 5 nhánh vào `claude/release-0.9.5`, đổi cặp khóa kích hoạt, có bộ cài thử cuối (run 43000757496). Mở thread cài đặt plugin. Chờ cậu thử trên AutoCAD 2026 |
