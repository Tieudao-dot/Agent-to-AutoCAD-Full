# Agent to AutoCAD Full — quy tắc làm việc cho mọi thread

## Dự án

Repo `Tieudao-dot/Agent-to-AutoCAD-Full`: bản sao đầy đủ của plugin ClaudeCAD (chép 12/11/2026) để giữ bản 2D và
phát triển tiếp **vẽ 3D** cùng **gói dịch vụ theo máy (2D / 3D / Full)**.

Plugin AutoCAD 2022–2027: khung chat WPF (C#, `plugin/`) + trợ lý Node/TS chạy Claude Agent SDK (`agent/`),
bộ cài Inno Setup (`installer/`), skill (`skills/`). Đọc trước: `docs/ke-hoach-3d-goi-dich-vu.md` (kế hoạch 3D và
gói), `docs/cho-tro-ve-repo-goc.md` (các chỗ còn trỏ về dự án gốc), `docs/tong-quan-du-an.md` (tổng quan chép từ
dự án gốc), `bo-cai/README.md`.

## Độc lập với dự án gốc (bắt buộc)

- Không push, không sửa gì ở repo gốc `Tieudao-dot/Agent-to-Autocad`.
- Không nhắn tin hay đánh thức thread nào của dự án gốc. Chỉ dùng danh bạ ở cuối file này.
- Không đụng máy chủ kích hoạt (Apps Script), Google Sheet "Thiết bị" hay secret CI của dự án gốc. Bản Full sẽ có
  máy chủ kích hoạt riêng; chủ dự án quyết cách làm.
- Các chỗ trong code, bộ cài, CI còn trỏ về dự án gốc: chỉ sửa khi chủ dự án đã chốt (xem
  `docs/cho-tro-ve-repo-goc.md`).

## Nhánh

- `main` = v0.9.3 (mã đã phát hành của bản gốc).
- `luu-tru/2d-base` = bản 2D mới nhất lúc tách, chưa thử trên AutoCAD thật. Nhánh lưu trữ: **không sửa, không xóa**.
- Các nhánh `claude/*` có sẵn lúc tách là bản chép lưu trữ của dự án gốc: không sửa.
- Proxy chặn push tag, nên lưu trữ bằng nhánh.
- Repo chưa có secret CI nên bước phát hành của CI bị chặn. Giữ nguyên tới khi chủ dự án tự cài.

## Giao tiếp với chủ dự án

- Trả lời tiếng Việt, gọi người dùng là "cậu", xưng "tớ". Ngắn gọn, đi thẳng vào kết quả; nội dung dài
  (báo cáo, hướng dẫn) viết thành file.
- Việc cần xác nhận: hỏi bằng AskUserQuestion có lựa chọn, tách khỏi phần báo trạng thái, mỗi lần một câu.
- **Kiểm kỹ câu hỏi trước khi gửi cậu quyết định (chủ dự án yêu cầu 12/11/2026):**
  - nói bằng ngôn ngữ thông thường, ít thuật ngữ kỹ thuật. Thuật ngữ nào buộc phải dùng thì giải thích ngắn bằng ví dụ
    thực tế: cậu hoặc khách sẽ thấy gì, phải làm gì;
  - nêu rõ vì sao phải quyết, và mỗi lựa chọn dẫn tới kết quả gì, tốn gì;
  - câu hỏi đọc riêng vẫn hiểu; các lựa chọn rõ ràng, không chồng chéo; ghi lựa chọn đề xuất kèm lý do;
  - đọc lại một lượt như người không biết code trước khi gửi. Cậu còn hỏi lại "là sao" nghĩa là câu hỏi chưa đạt:
    giải thích lại đơn giản hơn rồi mới hỏi tiếp;
  - sub-thread nêu điểm cần chốt cũng theo quy tắc này; luồng chính viết lại cho dễ hiểu trước khi hỏi cậu.
- **Tác vụ đang chạy hiện ở thanh bên phải (task list) (chủ dự án yêu cầu 13/11/2026):**
  - luồng chính giữ trên thanh bên phải của thread chính mỗi việc đang chạy là 1 task: tên thread / đợt + việc
    (vd "Đợt A › Test & sửa"), dòng trạng thái là bước hiện tại (vd "CI đang chạy, run …");
  - cập nhật task mỗi khi nhận tin cập nhật / báo cáo; xong thì đánh dấu xong; việc đã lên lịch (các đợt sau, kiểm tra
    cuối, báo cáo) để "chờ"; việc chờ chủ dự án (thử tay, máy chủ kích hoạt…) là task riêng;
  - sub-thread cũng hiện tác vụ của mình ở thanh bên phải của thread đó, kể cả tác vụ chạy nền (chờ CI). Tin giao việc
    và prompt mở thread mới ghi sẵn quy tắc này.
- Push noti khi xong một việc hoặc phải dừng chờ cậu quyết; không push cho từng bước nhỏ.

## Token và thời gian

- Trước việc nặng (đọc nhiều file lớn, nhiều tác vụ con, chờ CI nhiều lần, xem nhiều ảnh): báo 1 dòng việc đó
  tốn ở đâu + cách nhẹ hơn, chờ cậu duyệt.
- Xong việc: ghi ngắn thời gian thực tế và phần tốn nhất. Không thấy số token chính xác thì nói rõ.
- Chỉ 1 tác vụ chờ CI một lúc (Bash chạy nền, vòng `until … sleep 30`); không poll tay, không chạy trùng;
  không xem ảnh chụp trừ khi cần.

## Vai trò luồng chính và giao việc cho sub-thread

- **Luồng chính chỉ làm 3 việc:**
  1. nắm tổng quan dự án (giữ `docs/tong-quan-du-an.md` và danh bạ thread đúng);
  2. trao đổi với chủ dự án: báo trạng thái, hỏi các điểm cần chốt;
  3. điều phối: giao việc cho sub-thread, theo dõi, tổng hợp báo cáo.
- Luồng chính **không tự viết code, sửa mẫu Apps Script, bộ cài, CI hay chạy build / test**. Việc nào như vậy thì
  giao cho sub-thread đúng phạm vi. Chưa có sub-thread nhận thì hỏi cậu mở thread mới, không tự làm thay.
- **Giao việc:** một tin `[Giao việc từ luồng chính] Chủ dự án yêu cầu: …` qua
  `mcp__claude-code-remote__send_message`, đủ để sub-thread làm mà không phải hỏi lại:
  - việc cần làm và kết quả mong muốn ("xong" nghĩa là gì);
  - các điểm cậu đã chốt (kèm ngày), điểm còn chờ chốt;
  - nhánh, commit xuất phát, file liên quan, tài liệu bàn giao trong `docs/ban-giao/`;
  - giới hạn: chi phí, số lượt CI, việc phải hỏi cậu trước;
  - mức gấp.
- **Cây thread (chốt 12/11/2026):**
  - luồng chính mở thread cho từng đợt / phần việc (gói dịch vụ, Vẽ 3D đợt 1, đợt 2, đợt 3, test tổng thể);
  - thread của mỗi đợt, khi luồng chính giao, tự mở **sub-thread con "Test & sửa"** của đợt đó (`create_session`
    từ chính thread đợt, nhánh tách từ nhánh của đợt), chuyển đủ context theo mẫu giao việc ở trên;
  - sub-thread con báo về thread đợt (thread cha); thread đợt tổng hợp rồi báo về luồng chính;
  - đợt sau chỉ bắt đầu khi test & sửa của đợt trước báo xong, và xuất phát từ commit cuối của bước test đó;
  - tin giao việc **đầu tiên** của thread đợt (prompt lúc mở thread) ghi sẵn yêu cầu "viết xong thì tự mở thread con
    Test & sửa" kèm nguyên prompt cho thread con. Yêu cầu gửi sau qua tin nhắn giữa các thread có thể bị chế độ Auto
    chặn; khi đó chủ dự án gõ 1 dòng trong thread đợt;
  - "Test tổng thể" do luồng chính mở sau Test & sửa đợt 3: rà toàn bộ gói + 3 đợt, chạy đủ test + 1 lượt CI, sửa
    lỗi, ra checklist tổng cho chủ dự án thử tay; không mở thread con.
  - **Kiểm tra độc lập (chủ dự án yêu cầu 12/11/2026):** làm xong hết các việc của một đợt lớn thì luồng chính mở một
    thread mới (agent chưa viết hay test phần nào) test lại toàn bộ một lượt. Thread này chỉ ghi lỗi, không tự sửa.
    - Lỗi được chuyển về thread phụ trách phần đó (gói dịch vụ, đợt 1, 2, 3, bộ cài / tài liệu) để sửa.
    - Thread sửa: gộp nhánh tích hợp mới nhất vào nhánh của mình, sửa, chạy test lõi + trợ lý, push, báo commit.
    - Thread giữ nhánh tích hợp gộp các bản sửa và chạy CI; thread kiểm tra độc lập soát lại các lỗi đã sửa.
- **Sub-thread:**
  - chỉ làm đúng tác vụ được giao, trên nhánh riêng của mình;
  - gặp việc ngoài phạm vi thì báo về thread cha 1–2 dòng, không làm thay thread khác; chỉ mở thread con khi được
    giao như trong "Cây thread";
  - việc khó đảo ngược hoặc hướng ra ngoài (merge `main`, phát hành, đổi khóa, xóa dữ liệu, gửi gì ra ngoài) thì
    báo về thread cha để chuyển lên luồng chính hỏi cậu trước;
  - xong mốc lớn hoặc phải dừng chờ quyết: báo 3–6 dòng về thread cha, mở đầu `[Báo cáo từ thread <tên>]`:
    việc đã xong, nhánh + commit, test / CI, việc chờ cậu, thời gian. Một lần mỗi mốc.
- Điểm cần cậu chốt phát sinh ở sub-thread: báo ngược lên tới luồng chính, luồng chính hỏi cậu bằng
  AskUserQuestion rồi chuyển câu trả lời xuống lại.
- **Cập nhật tình hình về luồng chính (chủ dự án yêu cầu 13/11/2026):** để chủ dự án xem tình hình mọi thread ở
  một chỗ là thread chính.
  - Mọi sub-thread, kể cả thread con, gửi tin ngắn về luồng chính (`session_01D56HmFT5bwapmbypmLY5nr`, qua
    `mcp__claude-code-remote__send_message`), mở đầu `[Cập nhật từ thread <tên>]`, 1–3 dòng: vừa xong gì, nhánh +
    commit, việc tiếp theo, chi phí tới lúc đó (`usage.cost_usd` khi gọi `get_session` không tham số).
  - Gửi ở các mốc: nhận việc và bắt đầu; viết / sửa xong, đã push, bắt đầu CI; có kết quả CI (xanh / đỏ, số run);
    mở thread con (kèm session id); xong việc. Bị chặn, gặp lỗi lớn hoặc phải chờ quyết thì gửi ngay.
  - Không gửi cho từng bước nhỏ: ngoài lúc bị chặn, hai tin cách nhau ít nhất khoảng 30 phút.
  - Thread con vẫn báo cáo đầy đủ về thread cha như cũ; tin cập nhật ngắn thì gửi thẳng luồng chính.
  - Luồng chính: mỗi tin cập nhật thì ghi 1–3 dòng tình hình trong thread chính, không push noti (chỉ push khi xong
    việc chủ dự án giao, bị chặn hoặc cần cậu quyết). Xong mỗi đợt thì cập nhật bảng tiến độ trong
    `docs/ke-hoach-nang-cap-lenh.md` và `docs/tong-quan-du-an.md`. Cậu hỏi "tình hình" thì luồng chính kiểm thêm các
    thread đang chạy bằng `get_session`.
  - Thread đang chạy đọc bản `CLAUDE.md` trên nhánh của nó (có thể là bản cũ), nên mọi tin giao việc và prompt mở
    thread mới, kể cả prompt cho thread con, ghi sẵn quy tắc này.
- Mở hay đóng sub-thread: chỉ khi cậu đồng ý (chuỗi 3D: cậu đã đồng ý 12/11/2026). Sub-thread mới ghi vào danh
  bạ ở cuối file; thread con ghi kèm thread cha.
- **Được tự mở thread (chủ dự án cho phép 12/11/2026):** luồng chính và thread mỗi đợt được tự gọi
  `mcp__claude-code-remote__create_session` để mở thread / thread con làm đúng tác vụ chủ dự án đã yêu cầu (theo
  "Cây thread"), không phải hỏi lại.
  - Chỉ mở thread cho việc đã được giao; không mở thread cho việc tự nghĩ ra, không mở trùng.
  - Mỗi thread mới: báo ngay session id về thread cha; thread cha báo lên luồng chính để ghi danh bạ.
  - Giới hạn chi phí, số lượt CI vẫn theo tin giao việc. Đóng / lưu trữ thread vẫn phải hỏi cậu.
  - Quyền chạy lệnh này nằm trong `.claude/settings.json` (`permissions.allow`), do **chủ dự án tự thêm**: thread
    không được tự sửa file quyền. Nhánh mới phải tách từ nhánh đã có file đó thì thread con mới dùng được quyền.

## Git và phát hành

- Mỗi thread làm trên nhánh riêng của mình. Không merge / push vào `main`, không tạo PR, không chạy CI với
  `release: true` khi cậu chưa đồng ý rõ ràng: merge `main` có phiên bản mới là CI tự phát hành bộ cài cho
  người dùng.
- Không bỏ / tắt test để CI xanh. Luôn cập nhật `bo-cai/README.md` và tài liệu trong `docs/` cùng với thay đổi.
- Không đưa tên hay ID model vào commit, PR, code.
- **Gộp bản Full vào `main` (chốt 13/11/2026):** khi chủ dự án đồng ý phát hành, gộp nhánh tích hợp vào `main` thành
  **một commit gọn** (squash), không giữ từng commit. Lý do: 2 commit đầu của dự án (`d8b1b58`, `f570344`, ngày 12/11)
  có tên model trong dòng ghi công; squash giữ lịch sử `main` sạch. Không viết lại lịch sử các nhánh làm việc; lịch sử
  chi tiết xem ở các nhánh đó.

## Bí mật

- Không bao giờ commit `activation/*.gs` đã điền hay `*.pem`.
- Không đưa key, SECRET, khóa riêng, email chủ dự án vào commit, chat, prompt, tin chuyển việc hay báo cáo.
- Không mở / in file chứa khóa; dùng lệnh kín.

## Build và test

- Plugin: build từng target riêng, xóa `obj` / `bin` giữa các lần:
  `dotnet build plugin/ClaudeCAD.Plugin -f net48 -c Release`, rồi `-f net8.0-windows`, rồi `-f net10.0-windows`
  (cần .NET SDK 10; trên Linux thêm `-p:EnableWindowsTargeting=true`).
- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests`. Trợ lý: `cd agent && npm ci && npm test`.
- UI test WPF chỉ chạy trên Windows CI: `.github/workflows/build.yml`, workflow_dispatch với `release: "false"`
  cho nhánh.

## Danh bạ thread (cập nhật khi thêm thread)

| Thread | Session id | Phạm vi |
| --- | --- | --- |
| Luồng chính | `session_01D56HmFT5bwapmbypmLY5nr` | Tổng quan, trao đổi với chủ dự án, điều phối sub-thread. Không tự làm việc kỹ thuật |
| Gói dịch vụ | `session_01D56HmJEU9szFU3rjc38bwT` | Nhánh `full/goi-dich-vu`: gói 2D / 3D / Full trong plugin (đọc gói, nhãn lệnh, chặn lệnh, trợ lý, giao diện), mẫu Apps Script phần gói. Bàn giao: `docs/ban-giao/goi-dich-vu-plugin.md` |
| Vẽ 3D đợt 1 | `session_01D56HmEKLjGrgQjCYBHyRFV` | Nhánh `full/ve-3d` (tách từ `full/goi-dich-vu`): 3D đợt 1 (khối cơ bản, extrude / revolve, cộng trừ khối, góc nhìn, kiểu hiển thị, thông tin khối, chụp ảnh 3D), nhãn gói 3D. Bàn giao: `docs/ban-giao/ve-3d-dot-1.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 1 (con của Vẽ 3D đợt 1) | `session_01D56HmF7xQDkumeCZ4v5kMJ` | Nhánh `full/ve-3d-test-1` (tách từ `full/ve-3d`): rà soát, thêm test tự động, sửa lỗi đợt 1; 1 lượt CI. Báo về thread đợt 1. Bàn giao: `docs/ban-giao/ve-3d-test-1.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Vẽ 3D đợt 2 | `session_01D56HmFnBJGpH2Jp3Gz2Rps` | Nhánh `full/ve-3d-dot-2` (tách từ `full/ve-3d-test-1` @ `9332985`): sweep, loft, cắt / bo / vát / làm rỗng khối, xoay / đối xứng / căn 3D, UCS, va chạm, danh sách cạnh / mặt. Viết xong tự mở thread con Test & sửa đợt 2. Bàn giao: `docs/ban-giao/ve-3d-dot-2.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 2 (con của Vẽ 3D đợt 2) | `session_01D56HmFawNpHoQo4t7E9eQq` | Nhánh `full/ve-3d-test-2` (tách từ `full/ve-3d-dot-2` @ `695909e`): rà soát, thêm test tự động, sửa lỗi đợt 2; 1 lượt CI. Báo về thread đợt 2. Bàn giao: `docs/ban-giao/ve-3d-test-2.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Vẽ 3D đợt 3 | `session_01D56HmJn6B9q2BakrVAExkg` | Nhánh `full/ve-3d-dot-3` (tách từ `full/ve-3d-test-2` @ `a51cd63`): `build_3d_from_plan` (dựng 3D từ mặt bằng 2D), mặt cắt / hình chiếu 2D từ khối 3D, skill mẫu "3D kiến trúc". Viết xong tự mở thread con Test & sửa đợt 3. Bàn giao: `docs/ban-giao/ve-3d-dot-3.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 3 (con của Vẽ 3D đợt 3) | `session_01D56HmJf7EpVVyCj13r68fL` | Nhánh `full/ve-3d-test-3` (tách từ `full/ve-3d-dot-3` @ `e464662`): rà soát, thêm test tự động, sửa lỗi đợt 3; 1 lượt CI. Báo về thread đợt 3. Bàn giao: `docs/ban-giao/ve-3d-test-3.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Test tổng thể | `session_01D56HmGjmEKBFmZETiignkt` | Nhánh `full/test-tong-the` (tách từ `full/ve-3d-test-3` @ `4bf5cce`, gộp tài liệu luồng chính): rà theo chiều ngang gói + 3 đợt 3D, sửa lỗi, đo token theo gói, checklist tổng `docs/checklist-tong-the.md`; 1 lượt CI. Bàn giao: `docs/ban-giao/test-tong-the.md`. Từ 13/11 nhận sửa lỗi phần 3D chủ dự án tìm khi thử thật (các thread đợt đã đóng) |
| Kiểm tra độc lập | `session_01D56HmF7CGr1nonkGWUKkGr` | Nhánh `full/kiem-tra-doc-lap` (tách từ `full/test-tong-the` @ `9ef9db9`): agent mới test lại toàn bộ một lượt, chỉ ghi lỗi (không sửa), phân theo phần phụ trách; 1 lượt CI; sau đó soát lại các lỗi đã sửa. Báo cáo: `docs/ban-giao/kiem-tra-doc-lap.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Bộ cài thử nội bộ | `session_01D56HmEzAMRB7RXVJqoQPnD` | Nhánh `full/ban-thu-noi-bo` (tách từ `full/test-tong-the`, **không bao giờ gộp**): bộ cài thử chỉ cho chủ dự án, bỏ qua kích hoạt, gói Full, hết hạn 10/12/2026, nhãn "BẢN THỬ NỘI BỘ"; 1 lượt CI. Bàn giao: `docs/ban-giao/ban-thu-noi-bo.md` |
| QnA | `session_01D56HmJBDC5zZsEvTcJ4Jbt` | Hỏi đáp về plugin cho chủ dự án, đọc code / tài liệu mới nhất (`full/test-tong-the`). Không sửa code, không commit. Lỗi hoặc việc cần quyết thì chuyển về luồng chính |
| Nâng cấp đợt A | `session_01D56HmEZnPtpeS5RWXPVPYD` | Nhánh `full/nang-cap-a` (tách từ `full/test-tong-the` @ `19944df`): gộp 6 lệnh dựng khối, `build_3d_from_plan` nhận layer tên khác, cột, nhiều tầng, lan can, `array_rect` thêm tầng. Viết xong tự mở thread con Test & sửa (`full/nang-cap-a-test`). Bàn giao: `docs/ban-giao/nang-cap-a.md`. Kế hoạch: `docs/ke-hoach-nang-cap-lenh.md` |
| ↳ Test & sửa đợt A (con của Nâng cấp đợt A) | `session_01D56HmExJjYYvbztLiiRbdx` | Nhánh `full/nang-cap-a-test` (tách từ `full/nang-cap-a` @ `fc68469`): rà soát, thêm test tự động, sửa lỗi đợt A; 1 lượt CI. Báo về thread đợt A, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-a-test.md` |
| Nâng cấp đợt B | `session_01D56HmERJAiGtKizUeoMK7s` | Nhánh `full/nang-cap-b` (tách từ `full/nang-cap-a-test` @ `33acb5f`): STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE, GROUP, MATCHPROP; bo / vát polyline + cung, offset hai phía, nối có dung sai. Viết xong tự mở thread con Test & sửa (`full/nang-cap-b-test`). Bàn giao: `docs/ban-giao/nang-cap-b.md` |
| ↳ Test & sửa đợt B (con của Nâng cấp đợt B) | `session_01D56HmJLL8fUhKMHs8VaPJm` | Nhánh `full/nang-cap-b-test` (tách từ `full/nang-cap-b` @ `81eaf7c`): rà soát, thêm test tự động, sửa lỗi đợt B; 1 lượt CI. Báo về thread đợt B, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-b-test.md` |
| Nâng cấp đợt C | `session_01D56HmJra3trN7XwA1QApZ6` | Nhánh `full/nang-cap-c` (tách từ `full/nang-cap-b-test` @ `265b66e`): hatch bấm một điểm, tự ghi kích thước mặt bằng, thống kê block ra bảng, tổng diện tích phòng / layer, tìm & thay chữ (thẻ xác nhận), in nhiều layout vào một PDF. Viết xong tự mở thread con Test & sửa (`full/nang-cap-c-test`). Bàn giao: `docs/ban-giao/nang-cap-c.md` |
| ↳ Test & sửa đợt C (con của Nâng cấp đợt C) | `session_01D56HmJVjAwoMYD2HCjBwLe` | Nhánh `full/nang-cap-c-test` (tách từ `full/nang-cap-c` @ `1305053`): rà soát, thêm test tự động, sửa lỗi đợt C; 1 lượt CI. Báo về thread đợt C, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-c-test.md` |

Link mở thread: `https://claude.ai/code/<session id>`.

Thread đã đóng vẫn xem lại được và mở lại được (unarchive) khi cần; nhánh và tài liệu bàn giao giữ nguyên. Lỗi thuộc phần
của thread đã đóng chuyển cho thread "Test tổng thể". Kiểm tra độc lập cho chuỗi nâng cấp sẽ mở thread mới (agent chưa
viết hay test phần đó).
