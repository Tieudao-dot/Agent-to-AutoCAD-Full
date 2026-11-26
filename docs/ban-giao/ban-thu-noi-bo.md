# Bộ cài thử nội bộ (BẢN THỬ NỘI BỘ)

> **Nhánh `full/ban-thu-noi-bo` KHÔNG BAO GIỜ được gộp** vào `full/test-tong-the`, `main` hay bất kỳ nhánh nào khác.
> Bộ cài này **chỉ chủ dự án dùng, không gửi cho ai**. Bộ cài thật làm sau, từ nhánh tích hợp, khi đã có máy chủ
> kích hoạt riêng.

*13/11/2026 · thread "Bộ cài thử nội bộ" · xuất phát từ `full/test-tong-the` @ `19944df`. Chủ dự án chốt 13/11/2026.*

> **Bản mới nhất (27/11/2026): bản 7, `AgentToAutoCADFull-ThuNoiBo-nang-cap-7-0.9.4`** (mục "Bản 7" ngay dưới). Đủ
> đợt F + G + H phần 1 + **H phần 2** (hạ tầng, MEP 3D, thép, tiếng Anh). Cài đè lên bản 6 / 5 là được, skill tự thay.
> Bản 3 → 6 bỏ.
>
> **Bản 6 (26/11/2026), `AgentToAutoCADFull-ThuNoiBo-nang-cap-6-0.9.4`** (lượt CI #91, mục "Bản 6" ngay dưới). Đủ
> đợt F + G + **đợt H phần 1**. Cài đè lên bản 5 (hoặc bản cũ hơn): **skill cũ tự được thay, không phải xóa tay.**
> Bản 3, 4, 5 bỏ.
>
> **Bản 5 (26/11/2026), `AgentToAutoCADFull-ThuNoiBo-nang-cap-5-0.9.4`** (lượt CI #73, mục "Bản 5" ngay dưới). Đủ
> đợt F (F1 → F5) + đợt G (G1 → G5). Bản 3, bản 4 bỏ. **Cài đè lên bản 3: phải làm thêm một bước tay cho skill** (xem
> "Bản 5 › Skill").
>
> **Bản 4 (25/11/2026): `AgentToAutoCADFull-ThuNoiBo-nang-cap-4-0.9.4`** (lượt CI #55). Như bản 3, thêm **đợt F (F1 →
> F5)**. Không gửi chủ dự án (chủ dự án chọn nhận một lần ở bản 5).
>
> **Bản 3 (15/11/2026): `AgentToAutoCADFull-ThuNoiBo-nang-cap-3-0.9.4`** (lượt CI #38, mục "Lượt CI" cuối
> file). Như bản 2 dưới đây, thêm **sửa khung chat** (`full/toi-uu-khung-chat` @ `983d4c2`): hết giật khi cuộn / gõ lúc
> đoạn chat dài, icon nét đơn sắc kiểu AutoCAD thay emoji, chế độ thu gọn cân đối hơn. Bản 2 (`…-nang-cap-2-…`) bỏ.
>
> **Bản 2 (14/11/2026): `AgentToAutoCADFull-ThuNoiBo-nang-cap-2-0.9.4`** (lượt CI #35).
> Có đủ gói dịch vụ + 3D đợt 1–3 + chuỗi nâng cấp A → E (gộp `full/nang-cap-tong-the` @ `f7f32ad`) **và bản sửa bộ cài
> TT-01** (@ `b0da814`): không còn hộp lỗi tiếng Anh "Setup was unable to automatically close all applications…".
> Bộ cài giờ:
> - AutoCAD đang mở thì hỏi bằng tiếng Việt (Thử lại / Hủy), không tự tắt AutoCAD;
> - tự tắt `node.exe` / `claude.exe` của plugin;
> - file còn bị giữ thì bảo khởi động lại máy rồi chạy lại.
>
> Các bộ cài cũ (`…-0.9.4` lượt #16, `…-nang-cap-0.9.4` lượt #32): **đừng dùng nữa**.

## Bản 7 (27/11/2026): đợt F + G + H phần 1 + H phần 2

**Gồm gì mới so với bản 6.** Gộp nhánh tích hợp đợt H phần 2 `full/nang-cap-h-tong-the-2` @ `f94e2e2` (code chốt, đã qua
kiểm tra độc lập phần 2):
- **H6 hạ tầng:** nhập điểm khảo sát (hỏi gốc tọa độ thật hay cục bộ), đồng mức, trắc dọc, trắc ngang;
- **H7 MEP 3D:** đi ống / ống gió 3D, soát va chạm ống với dầm / cột, báo cáo;
- **H8 kết cấu thép:** đặt thép dầm / cột / sàn, bảng thống kê thép;
- **H9 tiếng Anh:**
  - khung chat có tiếng Anh, chọn trong ⚙;
  - bộ cài có trang chọn ngôn ngữ (Tiếng Việt / English) ngay đầu; ngôn ngữ chọn ở bộ cài là ngôn ngữ đầu tiên của
    khung chat;
  - chữ vẽ vào bản vẽ và Excel vẫn giữ tiếng Việt.

Thiết lập bản thử giữ nguyên: không kích hoạt, luôn gói Full, **vẫn hết hạn sau 10/12/2026**, không tự cập nhật, không
phát hành được. Tên bản ghi **"nâng cấp 7"**.

**Riêng bản thử, có cả tiếng Anh:**
- trang đầu bộ cài (lời dặn bản thử) có hai thứ tiếng theo ngôn ngữ chọn;
- câu trong hộp thoại thay bản cũ có hai thứ tiếng;
- dòng "Kích hoạt" trong ⚙ / tự kiểm, câu hết hạn, câu "không tự cập nhật" có hai thứ tiếng;
- tên bản ("Agent to AutoCAD Full: BẢN THỬ NỘI BỘ nâng cấp 7 (hết hạn 10/12/2026)") giữ tiếng Việt ở cả hai ngôn ngữ.

**Tải (bản 7 — lượt CI).** Lượt **#111**, run `45876397319`, commit `24f3b0d` (mã + tài liệu bản 7; commit sau chỉ ghi
link này), `release: "false"`: **xanh**. Log: trợ lý `# pass 467 / # fail 0`, lõi 1733/1733 (`Failed: 0`), UI test WPF
41 test qua; bước phát hành bỏ qua.
- Trang run: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45876397319>
- Artifact **`AgentToAutoCADFull-ThuNoiBo-nang-cap-7-0.9.4`** (id `15146375314`, 141 575 382 byte ≈ 142 MB, trong có
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **24/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45876397319/artifacts/15146375314>

### Bản 7 › Cài đè lên bản 6 (hoặc bản cũ hơn)

1. Đóng AutoCAD.
2. Giải nén zip, chạy `AgentToAutoCADFull-ThuNoiBo-Setup.exe`.
3. **Trang đầu tiên mới: chọn ngôn ngữ** (Tiếng Việt / English), bấm OK.
4. Trang tiếp theo ghi "ĐÂY LÀ BẢN THỬ NỘI BỘ (nâng cấp 7 …)" (hoặc "THIS IS AN INTERNAL TEST BUILD …"). Bấm Next tới
   hết.
5. Không có trang Kích hoạt.
6. Skill tự thay như bản 6 (không phải xóa tay).
7. Mở AutoCAD, gõ `CLAUDECHAT`:
   - thanh trên cùng ghi "… nâng cấp 7 …";
   - bấm **Chat mới** một lần.
8. Lịch sử chat, cài đặt, macro, đăng nhập Claude được giữ.

### Bản 7 › Thử gì trước

- Gõ `CLAUDECHATSELFTEST`: thêm các dòng "Nâng cấp đợt H6 … H9" đều ✓.
- Theo `docs/checklist-tong-the.md`: làm khối **"Đợt H phần 2", mục P2-0** trước (~45 phút):
  - nhập file điểm khảo sát thật;
  - đồng mức + trắc dọc / ngang;
  - soát va chạm ống;
  - bảng thống kê thép đưa kỹ sư tính tay;
  - bộ cài chọn English rồi cài lại;
  - gõ tay "yes" / "đồng ý" ở thẻ xác nhận;
  - chữ trên bản vẽ giữ tiếng Việt khi chọn English.
- Rồi phần còn lại của khối H phần 2, sau đó các khối H phần 1 / G / F như bản 6.
- Mục P2-0-5 (chọn English rồi cài lại) làm được ngay với bộ cài này.

### Bản 7 › Kỹ thuật

- **Xung đột gộp ở 2 file, giữ phần bản thử, lấy chữ đã dịch của H9:**
  - `Sidecar/Activation.cs`: `BlockedMessage`, `StatusText`, `Contact`, `ActivateAsync`. Nhánh `#if BAN_THU_NOI_BO` giữ
    nguyên; nhánh `#else` lấy bản `L.Tr(...)`.
  - `installer/ClaudeCAD.iss`: hộp thoại thay bản cũ. Dòng bản thử thành `CustomMessage('BtnbMsg01')` (vi + en); dòng
    xin kích hoạt lại `H9Msg11` chỉ dùng khi không phải bản thử.
- **Thêm cho tiếng Anh của bản thử:**
  - `vietnamese.` / `english.WelcomeLabel2`;
  - 3 dòng trong `Core/LangEn.cs` (câu hết hạn, "không cần kích hoạt", "không tự cập nhật"), bọc `L.Tr`;
  - `InternalBuild.StatusTextEn`;
  - `ExpiredMessage` viết thành một chuỗi liền, cùng nội dung, để làm khóa bảng dịch.
  - Lý do: test H9 `In_the_palette_files_no_vietnamese_text_is_shown_without_Tr` chặn chữ Việt chưa qua `Tr`.
- Bộ cài vẫn ghi `Contents\language.txt` theo ngôn ngữ chọn (`CurStepChanged`, ngoài khối `#ifndef BanThuNoiBo`).
- Test: lõi **1733/1733** (1723 + 10 test bản thử), trợ lý **467/467**, build 3 target xanh. Không loại trừ test nào.
- Chưa kiểm được: chưa chạy bộ cài trên Windows thật / AutoCAD thật. Câu tiếng Anh riêng của bản thử chưa có ai đọc
  soát.

## Bản 6 (26/11/2026): đợt F + G + H phần 1

**Gồm gì mới so với bản 5.** Gộp nhánh tích hợp đợt H phần 1 `full/nang-cap-h-tong-the` @ `1ad6cec` (code `93096dc`, đã
sửa hết 28 lỗi kiểm tra độc lập KTH-01 → 28), không xung đột:
- **H1:** skill tự nâng cấp khi cài bản mới mà vẫn giữ bản khách đã sửa; `trace_image` đọc thẳng PDF scan; soát nhiều
  tầng;
- **H2:** xuất DXF / DWG đời cũ / FBX;
- **H3:** so sánh hai bản vẽ;
- **H4:** học chuẩn công ty từ bản vẽ mẫu (khung tên, layer, style);
- **H5:** làm nhiều file một lúc (có sao lưu bản gốc trước khi sửa);
- sửa CI: bước nhiều lệnh dừng ngay khi một lệnh hỏng (trước đây `npm test` hỏng mà bước vẫn xanh).

Thiết lập bản thử giữ nguyên: không kích hoạt, luôn gói Full, **vẫn hết hạn sau 10/12/2026**, không tự cập nhật, không
phát hành được. Tên bản ghi **"nâng cấp 6"**: "Agent to AutoCAD Full: BẢN THỬ NỘI BỘ nâng cấp 6 (hết hạn 10/12/2026)".

**Tải (bản 6 — lượt CI).** Lượt **#91**, run `45815996979`, commit `bd65f07` (mã + tài liệu bản 6; commit sau chỉ ghi
link này), `release: "false"`: **xanh** (~9 phút). Log: trợ lý `# pass 443 / # fail 0`, lõi 1618/1618, UI test WPF 38
test qua; bước phát hành bỏ qua.
- Trang run: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45815996979>
- Artifact **`AgentToAutoCADFull-ThuNoiBo-nang-cap-6-0.9.4`** (id `15114863910`, 141 457 402 byte ≈ 141 MB, trong có
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **24/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45815996979/artifacts/15114863910>

### Bản 6 › Cài đè lên bản 5 (hoặc bản cũ hơn)

1. Đóng AutoCAD (bộ cài nhắc bằng tiếng Việt nếu AutoCAD còn mở).
2. Giải nén zip, chạy `AgentToAutoCADFull-ThuNoiBo-Setup.exe`, Next tới hết. Không cần gỡ bản cũ.
3. **Không phải xóa skill bằng tay nữa** (khác bản 5).
   - Bộ cài để sẵn bản skill mới trong thư mục `skills-ban-moi`.
   - Lần đầu mở khung chat, plugin tự thay skill cậu **chưa sửa** bằng bản mới.
   - Skill nào cậu **đã sửa** thì giữ nguyên, bản mới đặt cạnh tên `SKILL.ban-moi.md`; khung chat báo một dòng.
   - Plugin nhận ra skill "chưa sửa" bằng cách so với mọi bản skill đã từng phát hành, kể cả của bản thử 3, 4, 5.
4. Mở AutoCAD, gõ `CLAUDECHAT`:
   - thanh trên cùng ghi "… nâng cấp 6 …";
   - bấm **Chat mới** một lần.
5. Lịch sử chat, cài đặt, macro, đăng nhập Claude được giữ.

### Bản 6 › Tự kiểm và thử gì trước

- Gõ `CLAUDECHATSELFTEST`: các dòng "Nâng cấp đợt H2 … H5" đều ✓, dòng G5 có thêm câu "PDF: Windows đọc thẳng trang PDF
  được".
- Theo `docs/checklist-tong-the.md`: làm khối **"Đợt H phần 1", mục H0** trước (~40 phút: xuất FBX mở bằng Blender / 3ds
  Max, áp chuẩn cho cả thư mục bản sao, vẽ lại từ PDF scan, skill khi cài đè, học chuẩn công ty), rồi H1 → H6. Tiếp đó
  khối "Đợt G" (mục G0 trước) và "Đợt F" như bản 5.
- Tiêu đề khối H trong checklist ghi "bộ cài thử bản 5": đúng ra là bản 6 (bản 5 chưa có đợt H).
- Mục H0-5 (skill khi cài đè) thử được ngay khi cài bản 6 lên máy đang có bản 5: sửa một dòng trong
  `skills\ve-kien-truc\SKILL.md` **trước** khi chạy bộ cài.
- Không thử được trên bản thử: mục cần máy gói 2D / 3D hoặc máy chưa kích hoạt.

### Bản 6 › Kỹ thuật

- Gộp `1ad6cec` không xung đột. Thêm: dòng kiểm `$LASTEXITCODE` sau `node bundle/make-bundle.mjs` trong bước bundle riêng
  của bản thử (theo cách sửa CI đợt H); nhãn "nâng cấp 6"; `WelcomeLabel2` ghi "(nâng cấp 6: đợt F + G + H phần 1)";
  artifact `-nang-cap-6`.
- Phần bộ cài của H1 (`[InstallDelete]` và `[Files]` cho `skills-ban-moi`) không đụng phần riêng bản thử.
- `SkillSync` chạy lúc mở khung chat, không phụ thuộc trạng thái kích hoạt.
- Test: lõi **1618/1618** (gồm 10 test bản thử), trợ lý **443/443**, build 3 target xanh. Không loại trừ test nào.
- Chưa kiểm được: chưa chạy bộ cài trên Windows thật, chưa chạy trên AutoCAD thật.

## Bản 5 (26/11/2026): đợt F + đợt G

**Gồm gì.** Gộp nhánh tích hợp đợt G `full/nang-cap-g-tong-the` @ `547d6af` (code cuối `e1455fb`, đã qua test tổng thể và
ba lượt soát của kiểm tra độc lập), không xung đột:
- **đợt F** (như bản 4): F1 UCS + nút chế độ, F2 style chuẩn VN / khung tên / block thư viện / trạng thái layer, F3 mái
  liền khối / vật liệu / góc nhìn, F4 mở chat nhanh + xuất Excel, F5 ô Nạp gọn lệnh / thẻ xác nhận / ghi chú mũi tên;
- **đợt G** (mới): G1 ra bộ hồ sơ một lệnh; G2 soát / sửa bản vẽ trước khi in; G3 bảng dự toán (xuất Excel có công
  thức); G4 render thật; G5 vẽ lại từ ảnh / PDF scan;
- skill cập nhật: `ve-kien-truc`, `ve-3d-kien-truc` (đợt G), `ve-mep`, `ve-ha-tang` (sửa nhỏ từ sau bản 3);
- lệnh tự kiểm `CLAUDECHATSELFTEST` có thêm 5 dòng "Nâng cấp đợt G1 … G5".

Đợt G không thêm file dữ liệu hay thư viện riêng: mọi thứ nằm trong plugin, trợ lý và skill.

**Khác bản 3 / 4.**
- Bản 3 → bản 5: thêm toàn bộ đợt F và đợt G.
- Bản 4 → bản 5: thêm đợt G.
- Tên bản ghi rõ **"nâng cấp 5"**: thanh trên cùng khung chat, ⚙, dòng đầu báo cáo tự kiểm, danh sách ứng dụng Windows
  ("Agent to AutoCAD Full: BẢN THỬ NỘI BỘ nâng cấp 5 (hết hạn 10/12/2026)"), trang đầu bộ cài.
- Thiết lập bản thử giữ nguyên: không kích hoạt, luôn gói Full, **vẫn hết hạn sau 10/12/2026**, không tự cập nhật,
  không phát hành được.

**Tải (bản 5 — lượt CI).** Lượt **#73**, run `45713707097`, commit `c901952` (mã cuối; các commit sau chỉ là tài liệu),
`release: "false"`: **xanh** (~7 phút).
- Trang run: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45713707097>
- Artifact **`AgentToAutoCADFull-ThuNoiBo-nang-cap-5-0.9.4`** (id `15069481207`, ~141 MB zip, trong có
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **23/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45713707097/artifacts/15069481207>

### Bản 5 › Cài đè lên bản 3 (hoặc bản 4)

1. Đóng AutoCAD. Bộ cài nhắc bằng tiếng Việt nếu AutoCAD còn mở: đóng rồi bấm Thử lại.
2. **Làm bước "Skill" ngay dưới trước**, rồi mới chạy bộ cài.
3. Giải nén zip, chạy `AgentToAutoCADFull-ThuNoiBo-Setup.exe`, bấm Next tới hết. Không cần gỡ bản cũ.
4. Mở AutoCAD, gõ `CLAUDECHAT`:
   - thanh trên cùng phải ghi "… nâng cấp 5 …";
   - bấm **Chat mới** một lần trước khi thử.

**Bộ cài cập nhật gì, giữ gì:**
- **Được thay mới:** plugin (cả 3 bản cho AutoCAD 2022–2027), trợ lý, Node.js đi kèm, mẫu lệnh nhanh.
- **Được giữ nguyên:** lịch sử chat, cài đặt, macro, đăng nhập Claude.
- **Skill: không được cập nhật nếu máy đã có.** Bộ cài chỉ chép một skill khi trên máy **chưa có** file đó, để không xóa
  phần cậu tự sửa. Kiểm trong `installer/ClaudeCAD.iss`: dòng chép thư mục `skills` có cờ `onlyifdoesntexist`. Máy đang
  có bản 3 nên đã có đủ 4 skill cũ. Cài đè mà không làm gì thì Claude vẫn dùng skill **cũ**, thiếu hướng dẫn của đợt
  F / G (ví dụ cách ra bộ hồ sơ, render, vẽ lại từ ảnh).

### Bản 5 › Skill: để có skill mới (làm một lần, trước khi chạy bộ cài)

1. Đóng AutoCAD.
2. Bấm phím **Windows + R**, dán dòng sau rồi bấm OK:
   `%AppData%\Autodesk\ApplicationPlugins\ClaudeCAD.bundle\Contents\skills`
3. Cửa sổ thư mục mở ra, có 4 thư mục: `ve-kien-truc`, `ve-3d-kien-truc`, `ve-mep`, `ve-ha-tang`.
4. **Nếu cậu chưa từng tự sửa skill nào:** chọn cả 4 thư mục đó, bấm **Delete**.
5. **Nếu cậu đã tự sửa một skill và muốn giữ:** đổi tên thư mục đó thành tên khác (ví dụ `ve-kien-truc-cua-toi`) thay vì
   xóa. Bộ cài sẽ chép bản mới vào tên gốc, bản của cậu vẫn nằm cạnh.
6. Chạy bộ cài bản 5 (bước 3 ở trên). Bộ cài chép lại 4 skill mới vào đúng chỗ.
7. Kiểm: mở lại thư mục ở bước 2, mở `ve-kien-truc\SKILL.md` bằng Notepad, tìm (Ctrl+F) chữ `create_drawing_set`. Có
   chữ đó là đã có skill mới.

Quên bước này mà đã cài: làm bước 1–5, rồi chạy lại bộ cài bản 5 lần nữa là được.

Skill cậu tự **thêm** (thư mục tên khác 4 tên trên) không bị đụng tới.

### Bản 5 › Chạy lệnh tự kiểm

Mở một bản vẽ bất kỳ, gõ `CLAUDECHATSELFTEST`, chờ vài phút. Plugin tự kiểm trên một bản vẽ tạm, bản vẽ của cậu
không bị đụng tới. Notepad mở báo cáo:
- dòng đầu ghi tên bản "… nâng cấp 5 …";
- các dòng "Nâng cấp đợt F2 … F5" và "Nâng cấp đợt G1 … G5" đều ✓.

Có dòng ✗ thì gửi luồng chính file `selftest-….txt` cùng thư mục thử ghi ở cuối báo cáo.

### Bản 5 › Thử gì trước

Theo `docs/checklist-tong-the.md`:
1. **Khối "Đợt G", mục G0** (~35 phút, làm trước): những điểm rủi ro nhất, chưa ai kiểm được ngoài AutoCAD thật:
   - **G4 render thật** (G0-2, G0-3): tự gõ `-RENDER` chép lại các câu AutoCAD hỏi; rồi nhờ Claude "render góc hiện tại
     chất lượng nhanh": phải ra ảnh render thật, hoặc Claude nói rõ đã dùng ảnh chụp thay và vì sao;
   - **G5 xem ảnh và gắn ảnh** (G0-1, G0-4): ảnh nền được gắn dưới bản vẽ, khóa, không in; Claude thấy được bản scan
     ảnh / PDF; PDF trắng trơn thì Claude phải nói là không thấy;
   - **G1 thẻ chạy lại bộ hồ sơ** (G0-5): "ra bộ hồ sơ" hai lần liền, lần 2 phải hiện thẻ Đồng ý / Hủy; Hủy không đổi gì;
     Đồng ý không nhân đôi tờ hay hình;
   - **G3 mở file dự toán bằng Excel thật** (G0-6): cột tiền có dấu tách hàng nghìn, tổng là công thức, dòng thiếu đơn
     giá để trống.
2. Phần còn lại của khối "Đợt G": G1 → G7 (~3 giờ tổng cả G0).
3. Khối **"Đợt F"** (~2 giờ), như hướng dẫn bản 4 dưới đây. Bỏ F10-1 … F10-3 (cần máy chưa kích hoạt / gói 2D / 3D).

Các mục ghi "Máy khác" (cần máy gói 2D / 3D hoặc máy chưa kích hoạt) không thử được trên bản thử nội bộ.

### Bản 5 › Kỹ thuật

- Gộp `547d6af` không xung đột. Chỉ đổi thêm: nhãn `InternalBuild.Label` và `AppDisplayName` của bộ cài thêm "nâng cấp
  5"; `WelcomeLabel2` ghi "(nâng cấp 5: đợt F + đợt G)"; tên artifact `-nang-cap-5`; test nhãn bản thử theo đó.
- Phần đợt G không hỏi trạng thái kích hoạt / gói ngoài `Activation.Plan` (bản thử: Full), nên chạy như máy Full đã kích
  hoạt.
- Test: lõi **1455/1455** (1445 của nhánh tích hợp + 10 test bản thử), trợ lý **407/407**, build 3 target xanh. Không
  loại trừ test nào.
- **Chưa kiểm được:** chưa chạy bộ cài trên Windows thật (CI chỉ build bộ cài); các mục G0 chỉ kiểm được trên AutoCAD
  thật.

## Bản 4 (25/11/2026): có đợt F

**Có gì mới so với bản 3.** Gộp nhánh tích hợp đợt F `full/nang-cap-f-tong-the` @ `a6da118` (code cuối `7fe215c`):
- F1: UCS + nút chế độ làm việc;
- F2: style chuẩn VN, khung tên, block thư viện, trạng thái layer;
- F3: mái liền khối, vật liệu, góc nhìn;
- F4: mở chat nhanh (Claude khởi động sẵn) + xuất Excel;
- F5: ô **Nạp gọn lệnh** trong ⚙, thẻ xác nhận ghi đè, ghi chú có mũi tên;
- 9 lỗi kiểm tra độc lập đã sửa.

Thiết lập bản thử giữ nguyên: không kích hoạt, luôn gói Full, **vẫn hết hạn sau 10/12/2026**, không tự cập nhật, không
phát hành được.

**Tải.** Lượt CI **#55**, run `45632787564`, commit `5c75b77`, `release: "false"`: **xanh** (~8 phút).
- Trang run: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45632787564>
- Artifact **`AgentToAutoCADFull-ThuNoiBo-nang-cap-4-0.9.4`** (id `15031114800`, ~141 MB zip, trong có
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **23/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/45632787564/artifacts/15031114800>

**Cài đè lên bản 3.** Không cần gỡ.
1. Đóng AutoCAD. Bộ cài nhắc bằng tiếng Việt nếu AutoCAD còn mở: đóng rồi bấm Thử lại.
2. Giải nén zip, chạy `AgentToAutoCADFull-ThuNoiBo-Setup.exe`, Next tới hết.
3. Lịch sử chat, cài đặt, skill, macro và đăng nhập Claude được giữ.
4. Mở AutoCAD, gõ `CLAUDECHAT`, bấm **Chat mới** một lần trước khi thử (cuộc chat cũ vẫn mở được).

**Thử tay.** Khối **"Đợt F (F1 → F5)"** ở cuối `docs/checklist-tong-the.md`, khoảng **2 giờ**, làm theo thứ tự trong đó.
- Làm trước: F0-1 (`CLAUDECHATSELFTEST`, các dòng đợt F2–F5 đều ✓).
- Không thử được trên bản này: các mục cột "Cần" ghi **Máy khác** (F10-1 … F10-3: máy chưa kích hoạt, máy gói 2D / 3D),
  vì bản thử luôn là gói Full và luôn coi là đã kích hoạt.
- Nút chế độ (F1) luôn hiện, vì máy luôn gói Full.

**Công tắc tắt nạp gọn.** Có hai cách, chọn một:
- trong ⚙ ▸ "Cách Claude làm việc" ▸ bỏ chọn ô **Nạp gọn lệnh** (mục F11);
- hoặc tạo file trống `tool-search-off` (không có đuôi `.txt`) trong `%LocalAppData%\ClaudeCAD` (mục F9).

**Bật / tắt công tắc xong thì bấm Chat mới**: thay đổi chỉ áp dụng từ cuộc chat mới. Cuộc chat đang dở vẫn chạy như cũ,
và có thể báo lỗi nếu cứ nhắn tiếp trong đó.

**Kỹ thuật.**
- Xung đột gộp duy nhất ở `ChatController.ShowPlan`: bản thử luôn hiện ô gói `Full` và nút chế độ F1. Bản thật chỉ hiện
  khi có máy chủ kích hoạt và máy đã kích hoạt.
- Các phần đợt F hỏi trạng thái máy: tự mở Claude sẵn hỏi `Activation.IsAllowed` (bản thử: chưa hết hạn), nút chế độ và
  thẻ macro hỏi `Activation.Plan` (bản thử: Full). Cả hai chạy đúng như máy Full đã kích hoạt; hết hạn thì không mở sẵn
  Claude.
- Test: lõi **1227/1227**, trợ lý **364/364**, build 3 target xanh, UI test WPF trên CI xanh. Không loại trừ test nào.

## Bản này là gì

Một bộ cài để cậu thử ngay 2D + 3D trên AutoCAD trong lúc bản Full **chưa có máy chủ kích hoạt riêng**.

| | Bản thử nội bộ | Bản thật (sau này) |
| --- | --- | --- |
| Kích hoạt | Không có. Bộ cài không có trang Kích hoạt, không hỏi key, không gửi gì đi đâu | Có: xin key, chủ plugin duyệt trong Sheet "Thiết bị" |
| Gói | Luôn **Full** (đủ 2D + 3D) | Theo Sheet: 2D / 3D / Full |
| Hết hạn | Dùng được tới **hết ngày 10/12/2026** (giờ Việt Nam). Từ 00:00 ngày 11/12/2026 khung chat không mở trợ lý nữa, hiện: "Bản thử nội bộ đã hết hạn ngày 10/12/2026. Cài bản chính thức để dùng tiếp." | Không hết hạn |
| Cập nhật tự động | Tắt (không tải, không cài đè gì). `CLAUDECHATUPDATE` chỉ báo "Bản thử nội bộ không tự cập nhật" | Có |
| Tên hiện ra | "Agent to AutoCAD Full: BẢN THỬ NỘI BỘ (hết hạn 10/12/2026)": thanh trên cùng khung chat, ⚙, dòng đầu báo cáo `CLAUDECHATSELFTEST`, danh sách ứng dụng Windows, trang đầu bộ cài | "Agent to AutoCAD Full" |
| File bộ cài | `AgentToAutoCADFull-ThuNoiBo-Setup.exe` | `ClaudeCAD-Setup.exe` |

Vặn lùi đồng hồ Windows **không** kéo dài được hạn: plugin nhớ thời điểm muộn nhất máy từng chỉ (cùng cách chống vặn
đồng hồ của bản thật) và tính hạn theo thời điểm đó.

## Cài lên máy đang có ClaudeCAD

Bản này giữ nguyên "danh tính" kỹ thuật của bản Full (cùng mã ứng dụng, cùng thư mục, cùng lệnh `CLAUDECHAT` / `CC`,
cùng thư mục dữ liệu). Nghĩa là:

- máy đang có ClaudeCAD (bản gốc hay bản Full) thì **bản thử sẽ thay nó**. Bộ cài hỏi trước (hai nút **Tiếp tục** /
  **Hủy**); bấm Hủy thì máy giữ nguyên bản đang có;
- **lịch sử chat, cài đặt, skill, macro và đăng nhập Claude được giữ**;
- sau này chạy bộ cài thật là cài đè lên bản thử được, không phải gỡ. Bộ cài thật sẽ hỏi kích hoạt như bình thường.

Gỡ bản thử: Windows ▸ Settings ▸ Apps ▸ "Agent to AutoCAD Full: BẢN THỬ NỘI BỘ (hết hạn 10/12/2026)" ▸ Uninstall.

## Cách tải và cài

1. Mở trang lượt chạy CI mới: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43670758598>. Cần đăng nhập GitHub bằng tài khoản có quyền vào repo.
2. Kéo xuống cuối trang, mục **Artifacts**, bấm **`AgentToAutoCADFull-ThuNoiBo-nang-cap-3-0.9.4`** để tải về file `.zip`.
3. Giải nén zip (chuột phải ▸ Extract All), ra file `AgentToAutoCADFull-ThuNoiBo-Setup.exe`.
4. Đóng AutoCAD (bộ cài sẽ nhắc nếu AutoCAD còn mở), chạy file đó. Trang đầu ghi rõ "ĐÂY LÀ BẢN THỬ NỘI BỘ". Bấm Next tới hết. Windows có thể cảnh báo
   "Windows protected your PC" (file chưa ký): bấm **More info ▸ Run anyway**.
5. Giữ ô **Đăng nhập Claude ngay** rồi bấm Finish (đăng nhập tài khoản Claude Pro/Max một lần).
6. Mở AutoCAD, gõ `CLAUDECHAT`. Thanh trên cùng khung chat có chữ "BẢN THỬ NỘI BỘ"; ⚙ ▸ dòng Kích hoạt ghi "bản thử
   nội bộ — bỏ qua kích hoạt, gói Full, … (còn N ngày)".

GitHub giữ artifact khoảng 90 ngày (tới khoảng 11/02/2027): đủ tới hết hạn bản thử.

## Thử được gì trong `docs/checklist-tong-the.md`

**Thử được** (máy luôn là gói Full):

- **Phần 0** tự kiểm tra (S1, S2);
- **Phần 2–5** (L9, L11; J10–J16; K17–K25; L17–L28);
- **Phần 6** một lượt dùng lệnh cả 3 đợt (X1–X5);
- **Phần 7** J1–J9, K1–K16, L1–L8, L10, L12–L16 (**trừ** J8, K15, L13: cần máy gói 2D);
- **Phần 8** M3–M9;
- **Khối chuỗi nâng cấp A → E** (phần 9–16): S3, **V1–V13**, các mục ★ / ○ của N–R (`checklist-thu-that.md`),
  **trừ** các mục cần máy gói `2D` hoặc `3D`: N3, N16, O14, O15, P12, Q11, Q12, R12; ở V3, V6 chỉ thử nhánh gói `Full`;
- toàn bộ phần 2D có sẵn từ bản gốc.

**Không thử được** (cần máy chủ kích hoạt và Sheet "Thiết bị"):

- **Phần 1** gói dịch vụ: I1–I6, I5b–I5d, T1–T5, M1–M2, M10–M12;
- J8, K15, L13 (máy gói 2D); N3, N16, O14, O15, P12, Q11, Q12, R12 (máy gói 2D / 3D);
- mọi bước có trang Kích hoạt, thẻ 🔒 Kích hoạt, "Kiểm tra lại", đổi gói trong Sheet.

Riêng T5 / I6 (cài đè lên bản gốc): hộp thoại hỏi thay bản gốc vẫn hiện như bản thật, nhưng phần kích hoạt sau đó
không có, nên chưa tính là đã thử T5.

## Đã làm gì (cho thread kỹ thuật)

Hằng biên dịch `BAN_THU_NOI_BO` chỉ đặt ở `plugin/ClaudeCAD.Plugin/ClaudeCAD.Plugin.csproj` trên nhánh này. **Không có**
đường tắt lúc chạy (file, registry, biến môi trường): bản build không có hằng này không thể bị biến thành bản thử.

- `Core/InternalBuild.cs` (mới, hàm thuần, có test): nhãn, câu hết hạn, `ExpiresUtc` = 17:00 UTC ngày 10/12/2026
  (= 00:00 ngày 11/12 giờ Việt Nam), `IsExpired` / `EffectiveUtc` dùng `LicenseLogic.ClockRolledBack` + thời điểm
  muộn nhất đã thấy, `DaysLeft`, `StatusText`.
- `Core/Edition.cs`: `ProductInfo.DisplayName` = nhãn bản thử khi có hằng (mọi chỗ đang dùng tên đều đổi theo).
- `Sidecar/Activation.cs` khi có hằng:
  - `IsActivated` = true; `Plan` = Full; `ServiceUrl` = null;
  - không đọc `license.txt`, `status.txt`, `plan.txt`, không đọc `CLAUDECAD_ACTIVATION_URL`;
  - `DeviceAllowed` = chưa hết hạn (`InternalExpired`: đọc / ghi dấu đồng hồ `clock.txt` + registry như bản thật, đọc
    lại mỗi phút nên để AutoCAD mở qua nửa đêm cũng khóa);
  - `BlockedMessage` = câu hết hạn; `StatusText` = dòng bản thử; `ActivateAsync` không làm gì;
  - `RequestAsync` / `CheckStatusAsync` dừng ngay vì không có `ServiceUrl`: không gọi `?action=` nào.

  Hết hạn thì khung chat hiện thẻ 🔒 với câu hết hạn và không khởi động trợ lý (cùng đường với máy bị khóa ở bản thật).
- `ChatController.cs`: ô "Gói" luôn hiện `Full`.
- `UI/ChatControl.cs`: tên ở ⚙ xuống dòng (tên dài).
- `Sidecar/UpdateService.cs`: `ManifestUrl` = null, `DownloadAndStageAsync` chỉ trả câu "không tự cập nhật".
- `activation/activation.json`: `url` trống.
- `installer/ClaudeCAD.iss` (`#define BanThuNoiBo`):
  - tên hiện ra là nhãn bản thử; file `AgentToAutoCADFull-ThuNoiBo-Setup.exe`;
  - trang đầu có lời dặn (`WelcomeLabel2`); luôn bỏ qua trang Kích hoạt; không có URL máy chủ dù `activation.json` ghi
    gì; cài ngầm `/KEY=` bị bỏ qua;
  - hộp thoại thay bản gốc ghi "không cần kích hoạt, hết hạn sau 10/12/2026";
  - giữ `AppId`, thư mục, lệnh, phát hiện bản gốc.
- `.github/workflows/build.yml`:
  - bundle không có link cập nhật (không `--update-url`, không zip / `latest.json`), kiểm bundle không có
    `update-config.json` và URL kích hoạt;
  - artifact `AgentToAutoCADFull-ThuNoiBo-<phiên bản>` chỉ có file Setup;
  - bước **"Refuse to publish the bản thử nội bộ"** báo lỗi khi có tag, `release: true` hay push mà csproj có
    `BAN_THU_NOI_BO` hoặc chạy trên nhánh này; bước Release còn điều kiện `github.ref_name != 'full/ban-thu-noi-bo'`.

## Test

Lượt đầu (trước khi gộp nâng cấp):


- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **379/379** (thêm `InternalBuildTests`: 10 test hết hạn, nhãn, vặn
  đồng hồ, số ngày còn lại, kiểm mã nguồn plugin / bộ cài / CI).
- Trợ lý `cd agent && npm ci && npm test`: **215/215**.
- Build plugin 3 target trên Linux (`net48`, `net8.0-windows`, `net10.0-windows`): xanh, 0 cảnh báo.
- **Không loại trừ test nào.** Test lõi biên dịch phần Core không có `BAN_THU_NOI_BO` (như nhánh tích hợp), nên các
  test kích hoạt / gói vẫn chạy và vẫn đúng; các kiểm tra mã nguồn của `EditionTests` vẫn khớp vì bộ cài giữ nhánh
  `#else` với tên thật.

Sau khi gộp nâng cấp A → E (commit `29fa991`): lõi **919/919**, trợ lý **298/298**, build plugin 3 target xanh.
Xung đột gộp duy nhất: danh sách file Core trong `ClaudeCAD.Core.Tests.csproj` (giữ cả `InternalBuild.cs` lẫn
`Edit2D.cs`, `Util2D.cs`, `Solids3.cs`). Mã gộp vào không thêm chỗ gọi máy chủ kích hoạt hay cập nhật nào. Không loại trừ
test nào.

## Lượt CI

**Bản 3 — lượt mới nhất (dùng lượt này):**

- Lượt **#38**, run `43670758598`, commit `8e8a429` (gộp `full/toi-uu-khung-chat` @ `983d4c2`, không xung đột),
  workflow_dispatch `release: "false"`: **xanh** (~8 phút).
  Trang: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43670758598>.
- Artifact bộ cài: **`AgentToAutoCADFull-ThuNoiBo-nang-cap-3-0.9.4`** (~141 MB zip, chứa
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **13/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43670758598/artifacts/14143819585>.
- Trước CI: lõi **920/920**, build 3 target xanh; UI test WPF (có test hiệu năng / icon mới) xanh trên CI. Thiết lập bản
  thử giữ nguyên.

**Bản 2 — lượt #35 (chưa có sửa khung chat, đừng dùng):**

- Lượt **#35**, run `43614348802`, commit `71dd03f` (gộp `full/nang-cap-tong-the` @ `b0da814`: nâng cấp A → E + sửa bộ cài
  TT-01), workflow_dispatch `release: "false"`: **xanh** (~7 phút).
  Trang: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43614348802>.
- Artifact bộ cài: **`AgentToAutoCADFull-ThuNoiBo-nang-cap-2-0.9.4`** (~141 MB zip, chứa
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **12/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43614348802/artifacts/14121404198>.
- Trước CI: lõi **920/920**, trợ lý **298/298**, build 3 target xanh. Xung đột gộp duy nhất ở `[Messages]` của bộ cài:
  giữ cả lời dặn bản thử (`WelcomeLabel2`) lẫn chữ Việt của hộp thử lại (TT-01). `PrepareToInstall` mới không đụng
  phần bản thử (bỏ trang Kích hoạt, không URL máy chủ).

**Lượt trước (#32, chưa có sửa TT-01, đừng dùng):**

- Lượt **#32**, run `43481896536`, commit `29fa991` (đã gộp nâng cấp A → E), workflow_dispatch `release: "false"`:
  **xanh** (~6 phút). Trang: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43481896536>.
- Mọi bước build / test qua; "Refuse to publish" và Release **bỏ qua** (không phát hành).
- Artifact bộ cài: **`AgentToAutoCADFull-ThuNoiBo-nang-cap-0.9.4`** (~141 MB zip, chứa
  `AgentToAutoCADFull-ThuNoiBo-Setup.exe`), tải được tới **11/02/2027**. Link tải thẳng (cần đăng nhập GitHub):
  <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43481896536/artifacts/14065019481>.

**Lượt cũ (chưa có nâng cấp A → E, đừng dùng):**

- Lượt **#16**, run `43373318854`, commit `57a55fa`, workflow_dispatch `release: "false"`, nhánh `full/ban-thu-noi-bo`:
  **xanh** (~7 phút). Trang: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43373318854>.
- Bước nào chạy: test trợ lý, test lõi, UI test WPF, build 3 bản plugin, kiểm dll, bundle không có link cập nhật / URL
  kích hoạt, Setup.exe đều qua. "Refuse to publish" và Release **bỏ qua** đúng như mong đợi (không phát hành).
- Artifact bộ cài: **`AgentToAutoCADFull-ThuNoiBo-0.9.4`** (~141 MB zip, chứa `AgentToAutoCADFull-ThuNoiBo-Setup.exe`),
  hết hạn tải 11/02/2027. Artifact `palette-screenshots` là ảnh UI test, không cần tải.
