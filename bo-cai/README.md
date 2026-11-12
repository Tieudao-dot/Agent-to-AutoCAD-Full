# Bộ cài ClaudeCAD

> **Bản Agent to AutoCAD Full chưa có bộ cài riêng.** Các link tải bên dưới vẫn là bộ cài ClaudeCAD của dự án gốc
> (bản 2D). Sẽ đổi khi chủ dự án chốt nơi phát hành của bản Full: xem
> [`../docs/cho-tro-ve-repo-goc.md`](../docs/cho-tro-ve-repo-goc.md).

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
3. **Kích hoạt:** bộ cài hiện **mã máy** và dòng *"Liên hệ Zalo owner: 0344644415"*; yêu cầu được gửi tự động cho chủ plugin (email + một dòng **Chờ duyệt** trong bảng thiết bị của chủ plugin). Hai cách: **(a)** chờ chủ plugin duyệt — bấm **Next** cài tiếp không cần key, sau khi được duyệt mở AutoCAD là tự dùng được (hoặc bấm **Kiểm tra lại** trong khung chat để dùng ngay); **(b)** nhận **key** qua Zalo, dán vào ô **Key kích hoạt** rồi bấm **Next**. Máy đã kích hoạt rồi (cài đè, cập nhật) thì không hỏi lại.
4. Trang cuối bộ cài: giữ ô **Đăng nhập Claude ngay** rồi bấm **Finish** → trình duyệt mở trang cấp quyền, bấm **đồng ý** bằng tài khoản Claude Pro/Max (một cửa sổ đen hiện lên và tự đóng khi xong). Không cần mở AutoCAD. Máy đã đăng nhập sẵn thì ô này không hiện. Sau đó mở AutoCAD 2022–2025 là dùng được (2026 và 2027: đang làm, chưa phát hành) (nếu bỏ qua bước này, lần đầu mở AutoCAD plugin tự mở trang đăng nhập).

Sau khi cài: gõ `CLAUDECHATSELFTEST` để tự kiểm tra (báo cáo ✓/✗ mở trong Notepad). Mở khung chat: `CLAUDECHAT`, `CC` hoặc **Ctrl+Shift+Space**.

## Cập nhật

- Khung chat tự kiểm tra bản mới mỗi ngày và báo trong chat. Gõ `CLAUDECHATUPDATE` để tải; bản mới được cài khi đóng AutoCAD.
- Hoặc tải lại `ClaudeCAD-Setup.exe` ở link trên và chạy đè. Skill cậu đã thêm/sửa, macro, mẫu lệnh, lịch sử chat đều được giữ nguyên.

## Cài cho nhiều máy

- Cài ngầm, không hỏi gì: `ClaudeCAD-Setup.exe /VERYSILENT /KEY=XXXX-XXXX-XXXX-XXXX` (key của đúng máy đó; mỗi máy vẫn cần bấm **Đăng nhập Claude** một lần).
- Hoặc bằng PowerShell: giải nén `ClaudeCAD-<version>.zip`, đặt `install.ps1` cạnh thư mục `ClaudeCAD.bundle`, chạy `powershell -ExecutionPolicy Bypass -File install.ps1`.

## Duyệt và quản lý thiết bị (chủ plugin)

- Mỗi máy chạy bộ cài (hoặc mở khung chat khi chưa kích hoạt) tự hiện thành một dòng **Chờ duyệt** trong Google Sheet **"ClaudeCAD – Thiết bị"** của chủ plugin, kèm email **"[ClaudeCAD] Yêu cầu kích hoạt: <tên máy> (<user>)"** có mã máy và **KEY** dự phòng.
- **Cho dùng:** đổi ô **Trạng thái** của dòng đó thành `Active` → lần mở AutoCAD kế tiếp máy đó tự dùng được (hoặc bấm **Kiểm tra lại** để dùng ngay), **không cần key**. Hoặc gửi KEY trong email qua Zalo.
- **Chống spam:** tối đa 30 máy mới mỗi giờ được thêm vào bảng, 10 email mỗi giờ và 40 email mỗi ngày; máy đã `Active` xin lại không gửi email thừa; tên máy gửi lên không chạy được như công thức trong bảng.
- **Không né được lệnh khóa:** link máy chủ nằm sẵn trong plugin (xóa file cấu hình không tắt được kiểm tra); chỉnh lùi đồng hồ máy để kéo dài 7 ngày cũng bị phát hiện.
- ⚠️ **Đừng đổi tên trang "Thiết bị", đừng chèn / đổi thứ tự cột** trước cột "Ghi chú" (máy chủ đọc cột theo vị trí). Chia sẻ bảng tính thì chỉ cho quyền **Người xem**: người có quyền sửa xem được khóa bí mật trong Apps Script.
- **Ngưng cho dùng:** đổi thành `Disabled` → máy bị khóa ở lần kiểm tra kế tiếp (plugin kiểm tra mỗi lần mở AutoCAD và ~6 giờ/lần; máy cố tình ngắt mạng cũng chỉ dùng thêm tối đa 7 ngày). Đổi lại `Active` là máy dùng tiếp ngay, không cần key mới.
- **Gói của máy** (bản Full, chưa phát hành): đổi ô **Gói** thành `2D`, `3D` hoặc `Full`. Máy nhận gói mới ở lần
  kiểm tra kế tiếp (mở AutoCAD, ~6 giờ/lần, hoặc **Kiểm tra lại**), khung chat báo "Gói đã đổi sang …" và hiện
  "Gói: …" trên thanh trên cùng và trong ⚙. Gói 3D có đường cơ bản nhưng không có chữ, kích thước, hatch, layout, in PDF,
  sửa 2D; Claude không thấy và không chạy được lệnh ngoài gói. Máy mới, ô Gói trống hoặc gõ sai nhận `2D` (đã chốt 12/11/2026).
- Cột **Lần cuối dùng** và **Phiên bản** cho biết máy nào còn đang dùng plugin.
- Key chỉ dùng được trên đúng máy có mã đó. Cài lại Windows / thay mainboard thì mã máy đổi, máy xin duyệt lại như máy mới.
- Máy không có mạng, cài máy chủ kích hoạt + bảng thiết bị, tạo key bằng tay: [`activation/README.md`](../activation/README.md).

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
| Full · Test tổng thể *(nhánh `full/test-tong-the`, chưa phát hành)* | Rà chéo gói dịch vụ × 3 đợt 3D: máy gói 3D không còn được Claude gợi ý dùng lệnh 2D không có trong gói (offset, kích thước, kiểu chữ); thêm test tự động cho macro, chặn theo gói, thẻ xác nhận của mọi lệnh 3D. Checklist thử tay một lượt: [`docs/checklist-tong-the.md`](../docs/checklist-tong-the.md). |
| Full · 3D đợt 2 *(nhánh `full/ve-3d-dot-2`, chưa phát hành)* | **Thêm lệnh 3D (máy gói 3D hoặc Full):** quét tiết diện theo đường dẫn (sweep: ống, tay vịn, gờ chỉ) và loft qua nhiều tiết diện (móng vuông lên cột tròn…); cắt khối bằng mặt phẳng (giữ một hoặc cả hai nửa); bo tròn / vát cạnh khối (chọn theo số thứ tự cạnh hoặc "cạnh đứng", "cạnh mặt trên"…); làm rỗng khối thành vỏ có mặt hở (bể, hộp); xoay quanh trục bất kỳ, đối xứng qua mặt phẳng, căn theo 1–3 cặp điểm; đặt / trả UCS (mặt phẳng làm việc của người dùng; Claude vẫn tính tọa độ theo WCS); kiểm tra va chạm giữa các khối (không đổi bản vẽ). Cắt / bo / vát / làm rỗng khối đã có từ trước luôn hỏi xác nhận. AutoCAD từ chối hình (bán kính quá lớn, quét tự cắt…) thì Claude báo rõ, bản vẽ không đổi. `CLAUDECHATSELFTEST` có thêm 3 bước (bo cạnh, cắt đôi, va chạm). Đổi UCS bị chặn khi "Chỉ xem". |
| Full · 3D đợt 3 *(nhánh `full/ve-3d-dot-3`, chưa phát hành)* | **Dựng 3D từ mặt bằng và bản vẽ 2D từ mô hình (máy gói 3D hoặc Full):** "dựng 3D mặt bằng này, tường cao 3,3 m" → Claude đọc tường (hai nét song song trên `A-WALL`, hoặc đường tim + chiều dày, polyline có bề rộng), cửa đi / cửa sổ (`A-DOOR`, `A-WIND`, block `CUA-DI-*` / `CUA-SO-*`) và sàn (đường bao kín trên `A-SLAB`), dựng tường có lỗ cửa (lanh tô, bậu) trên `A-WALL-3D` và sàn trên `A-SLAB-3D`. Claude chạy thử trước, báo phần không hiểu (tường cong, block, nét đơn… kèm handle) và hỏi cậu về khe hở không có ký hiệu cửa; không đoán. Nét 2D của cậu chỉ được đọc, không bị sửa. Dựng lại cùng mặt bằng thì Claude báo và hỏi, không tạo trùng. **Mặt cắt** (đường bao + hatch, có thể kèm nét phía sau) và **mặt đứng / hình chiếu** vẽ thành nét 2D cạnh mô hình, layer riêng `A-SECT`, `A-SECT-PAT`, `A-SECT-BACK`, `A-ELEV`. Skill mẫu mới `ve-3d-kien-truc` (chiều cao tầng, dày sàn, cao cửa, layer khối, quy trình tự kiểm) — máy gói 2D không nạp skill này dù có trong thư viện. `CLAUDECHATSELFTEST` có thêm 2 bước (dựng phòng có cửa, cắt ngang). |
| Full · Test & sửa 3D đợt 3 *(nhánh `full/ve-3d-test-3`, chưa phát hành)* | Sửa lỗi dựng 3D từ mặt bằng: chạy thử giờ báo đủ phần bị bỏ qua và khối của lần dựng trước; khoảng trống giữa hai tường song song (hộp kỹ thuật, khe lún) không còn bị dựng thành tường; polyline có bề rộng làm nét in được đọc như nét mặt tường (không thành tường mỏng); tường đổi dày và góc L hai tường khác dày không còn lồi khối; cửa sổ vẽ bằng nét trên layer tường được báo thay vì mất im; cửa chỉ định ở góc / quá đầu tường bị từ chối có lý do; sàn vẽ trùng chỉ dựng một; dựng lại mặt bằng chỉ có sàn cũng được báo trùng. Mặt cắt: khối AutoCAD không cắt được được nêu tên. |
| Full · Test & sửa 3D đợt 2 *(nhánh `full/ve-3d-test-2`, chưa phát hành)* | Sửa lỗi đợt 2: làm rỗng khối theo đúng chiều tài liệu AutoCAD (vào trong) ngay trên khối, không thử trên bản sao; làm rỗng kín không còn đòi `topology_id`; kiểm tra va chạm không đếm trùng cặp khi hai danh sách chung khối; đường dẫn / tiết diện tạm của sweep / loft nằm trên layer của khối (layer hiện hành khóa không còn chặn) và được xóa ngay; macro bo / vát theo số thứ tự cạnh chạy được giữa AutoCAD 2022–2024 và 2025–2027; `align` tự kiểm ma trận trước khi dời. |
| Full · 3D đợt 1 *(nhánh `full/ve-3d`, chưa phát hành)* | **Vẽ 3D (máy gói 3D hoặc Full):** Claude dựng khối hộp, trụ / nón, cầu, nêm, xuyến, chóp; đùn (extrude, có góc vát) và xoay (revolve) biên dạng 2D thành khối; cộng / trừ / giao khối; đặt góc nhìn (trên, trước, bên, 4 góc iso) và kiểu hiển thị (2D Wireframe, Conceptual, Shaded, Realistic…); đọc thể tích, trọng tâm, hộp bao, số mặt / cạnh. Dựng xong Claude tự xoay góc iso, chụp ảnh và đối chiếu thể tích. Trừ / cộng vào khối đã có từ trước luôn hỏi xác nhận; đổi góc nhìn / kiểu hiển thị không thêm bước U. `CLAUDECHATSELFTEST` có thêm bước dựng hộp trừ trụ. Máy gói 2D không thấy các lệnh này. Bản test đợt 1 (nhánh `full/ve-3d-test-1`): macro có lệnh trừ khối phát lại đúng khối mới, kiểu hiển thị chạy cả trên AutoCAD không phải tiếng Anh, lỗi cộng / trừ khối báo rõ và không đổi bản vẽ. |
| Full *(nhánh `full/goi-dich-vu`, chưa phát hành)* | **Gói dịch vụ theo máy (2D / 3D / Full):** chủ plugin chọn gói ở cột Gói của bảng thiết bị; câu trả lời của máy chủ có chữ ký số; plugin chặn lệnh ngoài gói (cả `batch_execute` và macro), Claude chỉ thấy lệnh của gói và nói rõ gói khi được xin việc ngoài gói; khung chat hiện "Gói: …" và báo khi gói đổi. Gói mặc định cho máy mới là `2D` (đã chốt 12/11/2026). |
| 0.9.5 *(bản thử, chưa phát hành; gồm cả phần từng ghi là 0.9.4)* | **Khóa bằng key theo máy + quản lý thiết bị:** bộ cài hiện mã máy + "Liên hệ Zalo owner: 0344644415", tự gửi email cho chủ plugin (tên máy, user Windows, mã máy, key) và thêm máy vào Google Sheet **"ClaudeCAD – Thiết bị"** (Chờ duyệt / Active / Disabled, lần cuối dùng, phiên bản). Chủ plugin đổi ô Trạng thái thành `Active` là máy tự kích hoạt (nút **Kiểm tra lại**, không cần key); đổi `Disabled` là máy bị khóa từ xa ở lần kiểm tra kế tiếp (mỗi lần mở AutoCAD và ~6 giờ/lần, câu trả lời có chữ ký số; mất mạng dùng tối đa 7 ngày). Key qua Zalo vẫn dùng được làm dự phòng, vĩnh viễn cho máy đó; cập nhật không hỏi lại; plugin kiểm tra giấy phép mỗi lần mở AutoCAD (chép thư mục / cài bằng .zip cũng bị hỏi key), thẻ **🔒 Kích hoạt** trong khung chat. Kèm 30 bản sửa sau đợt rà soát toàn dự án: xóa/sửa hàng loạt luôn hỏi xác nhận, tự gửi lại không nhầm bản vẽ, macro bo góc đúng đường, Claude chỉ đọc thư mục skill, báo cáo lỗi không lẫn nội dung chat, không vẽ trùng khi lệnh nặng, cuộc chat dài mở nhanh, cài ngầm `/VERYSILENT /KEY=` chạy được. **Skill theo từng đoạn chat:** + Add skill chọn nhiều file cùng lúc (vào thư viện và dùng luôn), danh sách Skill có phần *Đang dùng* (nút −) và *Thư viện* (nút +), chat mới dùng skill của chat gần nhất, bỏ ô Chuẩn. Chạy bền hơn: lỗi plugin không làm AutoCAD đóng đột ngột, khởi động lại trợ lý không sót tiến trình, ⚙ mở nhanh hơn, bộ cài không treo ở trang cuối. **Hỗ trợ AutoCAD 2026 và 2027** (vẫn một bộ cài chung 2022–2027): 2026 dùng chung bản .NET 8 với 2025, 2027 có bản .NET 10 riêng; chưa thử trên AutoCAD thật. **Giao diện theo phong cách AutoCAD:** khung chat lấy màu và font từ chính theme palette của AutoCAD (giống Properties, Layer) thay vì màu tự đặt; đổi Color theme trong AutoCAD thì khung chat đổi ngay. Cỡ chữ theo AutoCAD đến khi cậu tự chọn cỡ. **Lớp vỏ kiểu palette AutoCAD:** góc vuông, tin nhắn dạng khối phẳng, thanh công cụ là hàng icon, mọi nút có tooltip giải thích ngắn; ⚙ thành lưới 2 cột với nhóm thu gọn; thanh **hạn mức** dưới thanh công cụ; thẻ xác nhận in to số đối tượng + nút *Xem trên bản vẽ*; thẻ lỗi gọn. **Gửi thêm ý khi Claude đang làm:** gõ rồi Enter, tin tự gửi khi lượt xong (có nút Bỏ). Dòng "đang làm" có thời gian + số thao tác, **Esc** trong ô nhập để dừng. **Thu gọn khung chat (nút ⌄):** palette co lại chỉ còn ô nhập, Enter gửi nhanh; lượt xong hiện một dòng báo, bấm để mở rộng và xem lại nguyên đoạn chat; khi Claude cần cậu đồng ý thì palette tự mở. **Icon ClaudeCAD trên thanh trạng thái AutoCAD** (góc dưới bên phải): chấm xanh khi Claude đang làm, chấm vàng khi cần cậu xem, bấm để mở khung chat; lượt xong lúc khung chat đóng / tự ẩn thì có bong bóng báo. Hàng nút dưới ô nhập (Chọn đối tượng, Lấy điểm, Ảnh, Chụp) cũng thành icon có tooltip. **Thẻ "Bắt đầu" mới:** 3 bước đánh số (Kích hoạt → Đăng nhập Claude → Thử một yêu cầu mẫu), bước nào xong có ✓, bước cần làm có sẵn nút. **Xem Claude đang làm bước nào:** trong lúc chạy, danh sách bước (✓ xong · ● đang chạy · ⚠ lỗi) mở sẵn ngay trên câu trả lời, xong lượt tự thu gọn. **Tiến độ theo kế hoạch:** bấm *Làm* trên thẻ kế hoạch thì thấy "Bước 3/7: Vẽ tường" với ✓ bước đã xong / ● đang làm / ○ chưa làm, cả khi thu gọn khung chat. **Tự kiểm tra mở rộng:** `CLAUDECHATSELFTEST` kiểm thêm bản plugin có đúng với đời AutoCAD không, đọc theme palette của AutoCAD, style giao diện, icon thanh trạng thái, thu gọn / mở lại không mất tin nhắn, hàng chờ gửi, skill của đoạn chat, và xác nhận bản vẽ đang mở không bị đụng tới. **Checklist thử thật** một lượt ~20 phút: [`docs/checklist-thu-that.md`](../docs/checklist-thu-that.md). |
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
