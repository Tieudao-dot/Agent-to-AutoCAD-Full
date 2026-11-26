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
- **Con số phải được cậu xác nhận trước khi làm (chủ dự án yêu cầu 25/11/2026):**
  - mọi con số do thread chọn mà khách sẽ gặp hoặc làm đổi kết quả khách nhận được (kích thước và độ dày mặc định,
    chiều cao, khoảng cách, góc, cỡ chữ, tỉ lệ, ngưỡng, giới hạn, giá…) phải hỏi cậu xác nhận **trước** khi đưa vào
    code hay tài liệu cho khách; không tự chốt rồi báo sau;
  - sub-thread gom các số định dùng thành một danh sách (số nào, dùng ở đâu, khách sẽ thấy gì, vì sao đề xuất số đó) và
    gửi luồng chính càng sớm càng tốt; trong lúc chờ thì làm tiếp phần không phụ thuộc số đó;
  - luồng chính hỏi cậu bằng AskUserQuestion, mỗi nhóm số một câu, có số đề xuất kèm lý do; chuyển câu trả lời xuống
    thread; số đã xác nhận ghi vào kế hoạch / bàn giao kèm ngày;
  - còn số chưa được xác nhận thì thread chưa báo "xong" phần đó;
  - không phải hỏi: số kỹ thuật bên trong mà khách không thấy và không làm đổi kết quả (dung sai tính toán, thời gian
    chờ, số trong test). Các số này ghi trong bàn giao;
  - **khi cậu đã dặn "cứ triển khai, không hỏi lại" cho một chuỗi** (đợt F, dặn 25/11/2026; đợt F5 và đợt G, cậu chọn
    "chạy liền như đợt F" 25/11 ~20h): không dừng chuỗi để hỏi.
    Thread dùng số đề xuất, để mọi số ở một chỗ dễ đổi, và lập bảng "Con số chờ chủ dự án duyệt" trong bàn giao; luồng
    chính gom các bảng, đưa cậu duyệt một lần ở báo cáo cuối chuỗi, trước khi báo "hoàn thành". Điểm cần chọn khác thì
    chọn cách rẻ, an toàn, đổi lại được và ghi vào "Quyết định tự chọn" để cậu xem lại. Việc khó đảo ngược hoặc hướng
    ra ngoài (merge `main`, phát hành…) vẫn phải hỏi;
  - tin giao việc và prompt mở thread mới ghi sẵn quy tắc này.
- **Tác vụ đang chạy hiện ở thanh bên phải (task list) (chủ dự án yêu cầu 13/11/2026):**
  - luồng chính giữ trên thanh bên phải của thread chính mỗi việc đang chạy là 1 task: tên thread / đợt + việc
    (vd "Đợt A › Test & sửa"), dòng trạng thái là bước hiện tại (vd "CI đang chạy, run …");
  - cập nhật task mỗi khi nhận tin cập nhật / báo cáo; xong thì đánh dấu xong; việc đã lên lịch (các đợt sau, kiểm tra
    cuối, báo cáo) để "chờ"; việc chờ chủ dự án (thử tay, máy chủ kích hoạt…) là task riêng;
  - sub-thread cũng hiện tác vụ của mình ở thanh bên phải của thread đó, kể cả tác vụ chạy nền (chờ CI). Tin giao việc
    và prompt mở thread mới ghi sẵn quy tắc này.
- Push noti khi xong một việc hoặc phải dừng chờ cậu quyết; không push cho từng bước nhỏ.
- **Kiểm tra trước khi báo hoàn thành (chủ dự án yêu cầu 25/11/2026):**
  - mỗi đợt có thread viết và một thread khác rà lại + sửa (Test & sửa), mỗi thread CI xanh; cuối một chuỗi lớn có test
    tổng thể và kiểm tra độc lập bằng agent mới, lỗi sửa xong và được soát lại;
  - thread chỉ báo "xong" sau khi chạy lại đủ test sau lần sửa cuối và CI xanh; báo cáo ghi rõ đã kiểm gì (số run CI)
    và phần nào chưa kiểm được (vd chưa chạy trên AutoCAD thật);
  - luồng chính tự kiểm lại trước khi báo cậu: CI xanh thật trên đúng commit, nhánh đã push, file bộ cài tải được; rồi
    mới báo "hoàn thành" kèm danh sách mục cậu thử tay;
  - tin giao việc và prompt mở thread mới ghi sẵn quy tắc này.

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
  - **ngoại lệ (chủ dự án chốt 25/11/2026):** hai đợt đụng hai mảng khác nhau được chạy cùng lúc khi chủ dự án đồng ý
    cho đúng cặp đó (đợt F: F2 và F3 cùng xuất phát từ commit cuối của Test & sửa F1; đợt G, đồng ý 25/11 ~20h: G1 với
    G2, G3 với G4). Mỗi đợt vẫn có thread con Test &
    sửa riêng; xong cả hai thì thread của đợt kế tiếp gộp hai nhánh, gỡ chỗ chồng nhau, CI xanh rồi mới làm việc của
    mình. Thread chạy song song sửa file dùng chung càng ít càng tốt và viết phần thử tay vào file riêng của đợt;
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
  - Quyền chạy lệnh nằm trong `.claude/settings.json` (`permissions.allow`), do **chủ dự án tự thêm**: thread
    không được tự sửa file quyền. Nhánh mới phải tách từ nhánh đã có file đó thì thread mới dùng được quyền.
  - **File quyền hiện có (25/11/2026):** trên `full/nang-cap-f-goc` @ `2dbeee2` (= `full/nang-cap-tong-the` @ `012ed29`
    + file quyền; luồng chính thêm hộ, chủ dự án cho phép 25/11, ngoại lệ một lần). File cho thread tự chạy `git add`,
    `git commit`, `git push -u origin full/…` và `gh api repos/Tieudao-dot/Agent-to-AutoCAD-Full/actions/…` (chạy / xem
    CI); không cho đẩy `main`. Thread viết lệnh đúng dạng đó. Khi cài secret CI để phát hành thật: bỏ dòng
    `gh api …/actions/…` trước, vì dòng đó cũng cho chạy CI ở chế độ phát hành.
  - Lý do cần file: từ 14/11 chế độ Auto chặn `git commit` / `git push` ở thread mới (việc giao qua tin nhắn giữa các
    thread không được coi là chủ dự án yêu cầu); thread "Tối ưu khung chat" từng kẹt khoảng 10 giờ vì vậy.
  - **Cách chạy lệnh git để ít bị từ chối (rút ra 25/11):** `git add`, `git commit -m "…" -m "…"`, `git push -u origin
    <nhánh>` là ba lần gọi riêng, dạng đơn giản; không gộp với tạo nhánh hay ghi file, không dùng heredoc hay `$(…)`;
    file tạo bằng công cụ Write. Bị từ chối thì không thử lại, báo luồng chính ngay. Thread F3 lần 1 gộp tất cả vào một
    lệnh nên bị từ chối dù đã có file quyền; luồng chính cho dừng và mở thread thay thế. Prompt mở thread ghi sẵn cách này.
  - **Thực tế 25/11:** ba thread mở mới sau 15h54 (F3 lần 1, F3 lần 2, Test & sửa F2) đều bị từ chối `git commit`, kể
    cả khi chạy lệnh đơn giản và đã có file quyền; các thread đã lưu được trước đó (F1, Test & sửa F1, F2) vẫn lưu được
    khi nhận việc mới qua tin nhắn. Cách đang dùng cho đợt F: **giao việc mới cho thread đã lưu được**, viết và rà vẫn
    là hai thread khác nhau (F3: thread Test & sửa F1 viết, thread F2 rà; rà F2: thread F1). Thread mới bị chặn thì cho
    dừng hẳn để không làm trùng; chủ dự án muốn dùng lại thread đó thì gõ một dòng cho phép trong chính thread đó.

## Git và phát hành

- Mỗi thread làm trên nhánh riêng của mình. Không merge / push vào `main`, không tạo PR, không chạy CI với
  `release: true` khi cậu chưa đồng ý rõ ràng: merge `main` có phiên bản mới là CI tự phát hành bộ cài cho
  người dùng.
- Không bỏ / tắt test để CI xanh. Luôn cập nhật `bo-cai/README.md` và tài liệu trong `docs/` cùng với thay đổi.
- Không đưa tên hay ID model vào commit, PR, code. Dòng ghi công trong commit: `Co-Authored-By: Claude
  <noreply@anthropic.com>` (không kèm tên model). Hai commit ngày 15/11 lỡ ghi tên model (`0c9fc6f` trên nhánh luồng
  chính, `e2bea8d` trên `full/toi-uu-khung-chat`): không viết lại lịch sử, squash khi gộp `main` sẽ bỏ.
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
- Công cụ GitHub MCP (`mcp__github__*`) đã ngừng từ 25/11/2026; `gh pr`, `gh run`… không chạy được. Chạy và xem CI
  bằng `gh api` dạng REST, vd
  `gh api repos/Tieudao-dot/Agent-to-AutoCAD-Full/actions/workflows/build.yml/dispatches -f ref=<nhánh> -f 'inputs[release]=false'`.

## Danh bạ thread (cập nhật khi thêm thread)

| Thread | Session id | Phạm vi |
| --- | --- | --- |
| Luồng chính | `session_01D56HmFT5bwapmbypmLY5nr` | Tổng quan, trao đổi với chủ dự án, điều phối sub-thread. Không tự làm việc kỹ thuật |
| Gói dịch vụ | `session_01D56HmJEU9szFU3rjc38bwT` | Nhánh `full/goi-dich-vu`: gói 2D / 3D / Full trong plugin (đọc gói, nhãn lệnh, chặn lệnh, trợ lý, giao diện), mẫu Apps Script phần gói. Bàn giao: `docs/ban-giao/goi-dich-vu-plugin.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Vẽ 3D đợt 1 | `session_01D56HmEKLjGrgQjCYBHyRFV` | Nhánh `full/ve-3d` (tách từ `full/goi-dich-vu`): 3D đợt 1 (khối cơ bản, extrude / revolve, cộng trừ khối, góc nhìn, kiểu hiển thị, thông tin khối, chụp ảnh 3D), nhãn gói 3D. Bàn giao: `docs/ban-giao/ve-3d-dot-1.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 1 (con của Vẽ 3D đợt 1) | `session_01D56HmF7xQDkumeCZ4v5kMJ` | Nhánh `full/ve-3d-test-1` (tách từ `full/ve-3d`): rà soát, thêm test tự động, sửa lỗi đợt 1; 1 lượt CI. Báo về thread đợt 1. Bàn giao: `docs/ban-giao/ve-3d-test-1.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Vẽ 3D đợt 2 | `session_01D56HmFnBJGpH2Jp3Gz2Rps` | Nhánh `full/ve-3d-dot-2` (tách từ `full/ve-3d-test-1` @ `9332985`): sweep, loft, cắt / bo / vát / làm rỗng khối, xoay / đối xứng / căn 3D, UCS, va chạm, danh sách cạnh / mặt. Viết xong tự mở thread con Test & sửa đợt 2. Bàn giao: `docs/ban-giao/ve-3d-dot-2.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 2 (con của Vẽ 3D đợt 2) | `session_01D56HmFawNpHoQo4t7E9eQq` | Nhánh `full/ve-3d-test-2` (tách từ `full/ve-3d-dot-2` @ `695909e`): rà soát, thêm test tự động, sửa lỗi đợt 2; 1 lượt CI. Báo về thread đợt 2. Bàn giao: `docs/ban-giao/ve-3d-test-2.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Vẽ 3D đợt 3 | `session_01D56HmJn6B9q2BakrVAExkg` | Nhánh `full/ve-3d-dot-3` (tách từ `full/ve-3d-test-2` @ `a51cd63`): `build_3d_from_plan` (dựng 3D từ mặt bằng 2D), mặt cắt / hình chiếu 2D từ khối 3D, skill mẫu "3D kiến trúc". Viết xong tự mở thread con Test & sửa đợt 3. Bàn giao: `docs/ban-giao/ve-3d-dot-3.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| ↳ Test & sửa đợt 3 (con của Vẽ 3D đợt 3) | `session_01D56HmJf7EpVVyCj13r68fL` | Nhánh `full/ve-3d-test-3` (tách từ `full/ve-3d-dot-3` @ `e464662`): rà soát, thêm test tự động, sửa lỗi đợt 3; 1 lượt CI. Báo về thread đợt 3. Bàn giao: `docs/ban-giao/ve-3d-test-3.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Test tổng thể | `session_01D56HmGjmEKBFmZETiignkt` | Nhánh `full/test-tong-the` (tách từ `full/ve-3d-test-3` @ `4bf5cce`, gộp tài liệu luồng chính): rà theo chiều ngang gói + 3 đợt 3D, sửa lỗi, đo token theo gói, checklist tổng `docs/checklist-tong-the.md`; 1 lượt CI. Bàn giao: `docs/ban-giao/test-tong-the.md`. Từ 13/11 nhận sửa lỗi phần 3D chủ dự án tìm khi thử thật (các thread đợt đã đóng) **Đã đóng (lưu trữ) 15/11/2026.** |
| Kiểm tra độc lập | `session_01D56HmF7CGr1nonkGWUKkGr` | Nhánh `full/kiem-tra-doc-lap` (tách từ `full/test-tong-the` @ `9ef9db9`): agent mới test lại toàn bộ một lượt, chỉ ghi lỗi (không sửa), phân theo phần phụ trách; 1 lượt CI; sau đó soát lại các lỗi đã sửa. Báo cáo: `docs/ban-giao/kiem-tra-doc-lap.md` **Đã đóng (lưu trữ) 13/11/2026.** |
| Bộ cài thử nội bộ | `session_01D56HmEzAMRB7RXVJqoQPnD` | Nhánh `full/ban-thu-noi-bo` (tách từ `full/test-tong-the`, **không bao giờ gộp**): bộ cài thử chỉ cho chủ dự án, bỏ qua kích hoạt, gói Full, hết hạn 10/12/2026, nhãn "BẢN THỬ NỘI BỘ"; 1 lượt CI mỗi bản. Bản mới nhất: bản 5 (26/11, `c901952`, CI 45713707097, đủ đợt F + G). Bàn giao: `docs/ban-giao/ban-thu-noi-bo.md` |
| Lưu trữ bộ cài | `session_01D56HmJdn7RASaJtfgMQp5x` | Mở 26/11 05h38 theo yêu cầu của chủ dự án (25/11), đọc nhánh `full/ban-thu-noi-bo`: nơi chủ dự án lấy link bộ cài thử và hỏi cách tải / cài. Giữ danh sách các bản (bản 5 hiện tại, bản 4, bản 3). Không sửa code, không commit / push / build / CI. Bản mới: luồng chính gửi link, thread thêm vào danh sách. Lỗi chủ dự án báo khi thử thì chuyển về luồng chính |
| QnA | `session_01D56HmJBDC5zZsEvTcJ4Jbt` | Hỏi đáp về plugin cho chủ dự án, đọc code / tài liệu mới nhất (`full/test-tong-the`). Không sửa code, không commit. Lỗi hoặc việc cần quyết thì chuyển về luồng chính. Không dùng từ 13/11, đọc code cũ. **Đã đóng (lưu trữ) 26/11/2026.** |
| Nâng cấp đợt A | `session_01D56HmEZnPtpeS5RWXPVPYD` | Nhánh `full/nang-cap-a` (tách từ `full/test-tong-the` @ `19944df`): gộp 6 lệnh dựng khối, `build_3d_from_plan` nhận layer tên khác, cột, nhiều tầng, lan can, `array_rect` thêm tầng. Viết xong tự mở thread con Test & sửa (`full/nang-cap-a-test`). Bàn giao: `docs/ban-giao/nang-cap-a.md`. Kế hoạch: `docs/ke-hoach-nang-cap-lenh.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| ↳ Test & sửa đợt A (con của Nâng cấp đợt A) | `session_01D56HmExJjYYvbztLiiRbdx` | Nhánh `full/nang-cap-a-test` (tách từ `full/nang-cap-a` @ `fc68469`): rà soát, thêm test tự động, sửa lỗi đợt A; 1 lượt CI. Báo về thread đợt A, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-a-test.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Nâng cấp đợt B | `session_01D56HmERJAiGtKizUeoMK7s` | Nhánh `full/nang-cap-b` (tách từ `full/nang-cap-a-test` @ `33acb5f`): STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE, GROUP, MATCHPROP; bo / vát polyline + cung, offset hai phía, nối có dung sai. Viết xong tự mở thread con Test & sửa (`full/nang-cap-b-test`). Bàn giao: `docs/ban-giao/nang-cap-b.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| ↳ Test & sửa đợt B (con của Nâng cấp đợt B) | `session_01D56HmJLL8fUhKMHs8VaPJm` | Nhánh `full/nang-cap-b-test` (tách từ `full/nang-cap-b` @ `81eaf7c`): rà soát, thêm test tự động, sửa lỗi đợt B; 1 lượt CI. Báo về thread đợt B, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-b-test.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Nâng cấp đợt C | `session_01D56HmJra3trN7XwA1QApZ6` | Nhánh `full/nang-cap-c` (tách từ `full/nang-cap-b-test` @ `265b66e`): hatch bấm một điểm, tự ghi kích thước mặt bằng, thống kê block ra bảng, tổng diện tích phòng / layer, tìm & thay chữ (thẻ xác nhận), in nhiều layout vào một PDF. Viết xong tự mở thread con Test & sửa (`full/nang-cap-c-test`). Bàn giao: `docs/ban-giao/nang-cap-c.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| ↳ Test & sửa đợt C (con của Nâng cấp đợt C) | `session_01D56HmJVjAwoMYD2HCjBwLe` | Nhánh `full/nang-cap-c-test` (tách từ `full/nang-cap-c` @ `1305053`): rà soát, thêm test tự động, sửa lỗi đợt C; 1 lượt CI. Báo về thread đợt C, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-c-test.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Nâng cấp đợt D | `session_01D56HmEVecDKB1j1iGf8vNc` | Nhánh `full/nang-cap-d` (tách từ `full/nang-cap-c-test` @ `f4f27fe`): dầm, tường cong, PRESSPULL, xuất STL, bảng khối lượng theo layer, va chạm ra bảng (gói 3D chỉ trả số liệu, không vẽ table). Viết xong tự mở thread con Test & sửa (`full/nang-cap-d-test`). Bàn giao: `docs/ban-giao/nang-cap-d.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| ↳ Test & sửa đợt D (con của Nâng cấp đợt D) | `session_01D56HmFm4aVWGEgb2v8hWTB` | Nhánh `full/nang-cap-d-test` (tách từ `full/nang-cap-d` @ `809ad78`): rà soát, thêm test tự động, sửa lỗi đợt D; 1 lượt CI. Báo về thread đợt D, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-d-test.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Nâng cấp đợt E | `session_01D56HmJ3WbDQRkUMc3aJjvF` | Nhánh `full/nang-cap-e` (tách từ `full/nang-cap-d-test` @ `db34801`): cầu thang 1 vế / chữ L / chữ U, mái bằng / 2 dốc / 4 dốc, số mặc định theo điểm 6 kế hoạch. Viết xong tự mở thread con Test & sửa (`full/nang-cap-e-test`). Bàn giao: `docs/ban-giao/nang-cap-e.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| ↳ Test & sửa đợt E (con của Nâng cấp đợt E) | `session_01D56HmEVcsHK1xiof9qua9w` | Nhánh `full/nang-cap-e-test` (tách từ `full/nang-cap-e` @ `3e8a67c`): rà soát, thêm test tự động, sửa lỗi đợt E; 1 lượt CI. Báo về thread đợt E, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-e-test.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Test tổng thể nâng cấp | `session_01D56HmGm85zfmxQqdNwrF2N` | Nhánh `full/nang-cap-tong-the` (tách từ `full/nang-cap-e-test` @ `cdaa29b`): rà chiều ngang chuỗi nâng cấp A → E cùng gói / 3D cũ, sửa lỗi, đo token trước / sau, cập nhật `docs/checklist-tong-the.md`; 1 lượt CI. Giữ nhánh tích hợp cho bản sửa sau kiểm tra độc lập. Bàn giao: `docs/ban-giao/test-tong-the-nang-cap.md`. Nhánh đã được `full/nang-cap-g-tong-the` thay. **Đã đóng (lưu trữ) 26/11/2026.** |
| Kiểm tra độc lập nâng cấp | `session_01D56HmHmd7iAXreWAFzYLX4` | Nhánh `full/kiem-tra-doc-lap-2` (tách từ `full/nang-cap-tong-the` @ `f1e7123`): agent mới test lại toàn bộ chuỗi nâng cấp A → E một lượt, chỉ ghi lỗi (KT2-xx), phân theo phần phụ trách; 1 lượt CI; sau đó soát lại các lỗi đã sửa. Báo cáo: `docs/ban-giao/kiem-tra-doc-lap-nang-cap.md` **Đã đóng (lưu trữ) 15/11/2026.** |
| Bảo mật | `session_01D56HmJNNATNAVHRZejGSMu` | Trao đổi với chủ dự án về bảo mật, khóa key, máy chủ kích hoạt, quản lý thiết bị / gói, mã máy không có mạng, bộ cài thử nội bộ. Đọc code `full/nang-cap-tong-the`, không sửa code / Apps Script / bộ cài / CI; chỉ viết tài liệu `docs/` trên nhánh `full/bao-mat` khi chủ dự án yêu cầu. Lỗi / điểm yếu / việc cần quyết thì chuyển về luồng chính |
| Tối ưu khung chat | `session_01D56HmJHAPE5osZWP7986Jp` | Nhánh `full/toi-uu-khung-chat` (tách từ `full/nang-cap-tong-the` @ `b0da814`): sửa khung chat giật khi cuộn / gõ lúc chat dài (chủ dự án báo khi thử thật 14/11), không đổi cách dùng; ≤ 2 lượt CI, không mở thread con. Báo về luồng chính. Bàn giao: `docs/ban-giao/toi-uu-khung-chat.md` **Đã đóng (lưu trữ) 26/11/2026.** |
| ↳ Commit & CI (con của Tối ưu khung chat) | `session_01D56HmETMC8GaiKLR7th4ha` | Chủ dự án yêu cầu mở 15/11 (thread cha bị chế độ Auto chặn commit / push): áp bản vá của thread cha lên nhánh `full/toi-uu-khung-chat` (từ `full/nang-cap-tong-the` @ `b0da814`), build, test, commit, push, 1 lượt CI. Báo về thread cha, tin cập nhật ngắn gửi luồng chính. **Đã đóng (lưu trữ) 26/11/2026.** |
| Nâng cấp đợt F1 | `session_01D56HmFAo4dZ7c8sbeNB9eh` | Nhánh `full/nang-cap-f1` (tách từ `full/nang-cap-f-goc` @ `2dbeee2`): mọi lệnh vẽ hiểu UCS; nút chế độ "Cả hai / 2D / 3D" cho máy gói Full. Viết xong tự mở thread con Test & sửa (`full/nang-cap-f1-test`). Bàn giao: `docs/ban-giao/nang-cap-f1.md`. Kế hoạch: `docs/ke-hoach-nang-cap-lenh.md` mục "Đợt F" |
| ↳ Test & sửa đợt F1 (con của Nâng cấp đợt F1) | `session_01D56HmFrL65iF9oJokvqXw5` | Nhánh `full/nang-cap-f1-test` (tách từ `full/nang-cap-f1` @ `8110e45`): rà soát, thêm test tự động, sửa lỗi đợt F1; 1 lượt CI. Báo về thread đợt F1, tin cập nhật ngắn gửi luồng chính. Bàn giao: `docs/ban-giao/nang-cap-f1-test.md` |
| Nâng cấp đợt F2 | `session_01D56HmFcYrzsme7GoBm8tNC` | Nhánh `full/nang-cap-f2` (tách từ `full/nang-cap-f1-test` @ `b2e6431`), chạy cùng lúc với F3: bộ style chuẩn Việt Nam, layout có khung tên, block từ thư viện, trạng thái layer. Viết xong tự mở thread con Test & sửa (`full/nang-cap-f2-test`). Thử tay: `docs/checklist-dot-f2.md`. Bàn giao: `docs/ban-giao/nang-cap-f2.md` |
| ↳ Test & sửa đợt F2 (thread con, đã dừng) | `session_01D56HmJDwuCnKRDvTAFHFXr` | Mở 25/11 16h36, đẩy được nhánh `full/nang-cap-f2-test` (@ `bae8262`) nhưng bị chế độ Auto từ chối `git commit`. Luồng chính cho dừng hẳn 16h38, chưa có commit nào; thread còn một bản vá rà F2 chưa lưu lên GitHub (stash + `scratchpad/f2-test.patch`). Luồng chính lấy bản vá về `docs/ban-giao/f2-test-thread-con-ban-va.md` (không thêm sửa lỗi nào so với nhánh chốt; còn vài test có thể lấy thêm). **Đã đóng (lưu trữ) 26/11/2026.** |
| Test & sửa đợt F2 (người rà) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 25/11 16h39 vì thread này đã lưu được lên GitHub: rà soát, thêm test, sửa lỗi đợt F2, soát bảng con số chờ duyệt trên nhánh `full/nang-cap-f2-test` (từ `full/nang-cap-f2` @ `bae8262`); 1 lượt CI. Báo thẳng luồng chính. Bàn giao: `docs/ban-giao/nang-cap-f2-test.md` |
| Nâng cấp đợt F3 (lần 1, đã dừng) | `session_01D56HmFBfQ8mQ37WLtVFweF` | Mở 25/11 15h53, bị chế độ Auto từ chối `git commit` ngay bước thử quyền (gộp nhiều việc vào một lệnh). Luồng chính cho dừng hẳn, chưa viết gì, chưa đẩy gì. Thay bằng thread "lần 2" ở dòng dưới. **Đã đóng (lưu trữ) 26/11/2026.** |
| Nâng cấp đợt F3 (lần 2, đã dừng) | `session_01D56HmGHrzJYuxkoVZZ8MxA` | Mở 25/11 15h57, cũng bị chế độ Auto từ chối `git commit` dù chạy từng lệnh riêng. Chờ chủ dự án gần 30 phút không được, luồng chính cho dừng hẳn 16h25; chưa viết gì, chưa đẩy gì. **Đã đóng (lưu trữ) 26/11/2026.** |
| Nâng cấp đợt F3 (người viết) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 25/11 16h26 vì thread này đã lưu được lên GitHub và đang nắm code khối 3D trên UCS. Nhánh `full/nang-cap-f3` (tách từ `full/nang-cap-f1-test` @ `b2e6431`), chạy cùng lúc với F2: mái liền khối nhà chữ L / T, vật liệu + render, lưu góc nhìn, bản thang và bản mái dốc về 120 mm. Không mở thread con; viết xong báo luồng chính, luồng chính giao thread đợt F2 (`session_01D56HmFcYrzsme7GoBm8tNC`) rà lại (thread đợt F1 đã nhận rà F2). Thử tay: `docs/checklist-dot-f3.md`. Bàn giao: `docs/ban-giao/nang-cap-f3.md` |
| Test & sửa đợt F3 (người rà) | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 25/11 17h14, xong 17h35: rà soát, thêm test, sửa 2 lỗi đợt F3 trên nhánh `full/nang-cap-f3-test` (từ `full/nang-cap-f3` @ `09ae953`), commit cuối `53e18b5`, CI 45595009744 xanh. Bàn giao: `docs/ban-giao/nang-cap-f3-test.md` |
| Nâng cấp đợt F4 (người viết) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 25/11 17h40: nhánh `full/nang-cap-f4` (từ `full/nang-cap-f2-test` @ `c9fdc08`, gộp `full/nang-cap-f3-test` @ `53e18b5`, CI xanh rồi mới làm tiếp); mở chat nhanh hơn (trợ lý khởi động sẵn, chỉ nạp mô tả lệnh cần dùng), xuất bảng ra Excel. Không mở thread con; viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-f4.md`. Bàn giao: `docs/ban-giao/nang-cap-f4.md` |
| Test & sửa đợt F4 (người rà) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 25/11 18h22: rà soát, thêm test, sửa lỗi đợt F4 trên nhánh `full/nang-cap-f4-test` (từ `full/nang-cap-f4` @ `d6c15d6`): nạp gọn mô tả lệnh (thêm công tắc tắt nếu chưa có), khởi động sẵn, xuất Excel; ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 19h17: commit cuối `1cdf8d4`, CI 45610810220 xanh. Bàn giao: `docs/ban-giao/nang-cap-f4-test.md` |
| Test tổng thể đợt F | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 25/11 19h20: nhánh `full/nang-cap-f-tong-the` (từ `full/nang-cap-f4-test` @ `1cdf8d4`, gộp tài liệu luồng chính): rà chiều ngang F1 → F4 cùng gói / 3D / chuỗi A → E, sửa lỗi, đo token trước / sau, gộp các checklist đợt F vào `docs/checklist-tong-the.md`, soát `docs/dot-f-cho-duyet.md` so với code; ≤ 2 lượt CI. Giữ nhánh tích hợp đợt F cho bản sửa sau kiểm tra độc lập. Xong 19h44: commit cuối `afdaa68`, CI 45614706040 xanh. Sau kiểm tra độc lập: sửa KTF-01 (2 lần), 07, 08, 09, gộp bản sửa của thread F1 và đợt F5; **chốt nhánh 21h15 ở `a6da118` (code `7fe215c`, CI 45630097088 xanh)**. Bàn giao: `docs/ban-giao/test-tong-the-dot-f.md` |
| Nâng cấp đợt F5 (người viết) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 25/11 20h05: nhánh `full/nang-cap-f5` (từ `full/nang-cap-f-tong-the` @ `afdaa68`): ô bật / tắt nạp gọn lệnh trong ⚙, thẻ xác nhận đồng bộ (ghi đè file, góc nhìn, vật liệu cả layer), ghi chú có mũi tên (multileader); ≤ 2 lượt CI. Viết xong báo luồng chính, luồng chính giao thread khác rà rồi gộp vào nhánh tích hợp đợt F. Thử tay: `docs/checklist-dot-f5.md`. Bàn giao: `docs/ban-giao/nang-cap-f5.md` |
| Test & sửa đợt F5 (người rà) + sửa KTF phần F1 / F4 | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | 25/11 20h10: sửa KTF-02…06 trên `full/nang-cap-f-sua-ktf` @ `4399fa0`. 20h35 → 21h00: rà F5 trên `full/nang-cap-f5-test` (gộp nhánh tích hợp @ `03bf163`), sửa 2 lỗi thẻ, commit cuối `c14c8d7`, CI 45627464838 xanh. Bàn giao: `docs/ban-giao/nang-cap-f-sua-ktf.md`, `docs/ban-giao/nang-cap-f5-test.md` |
| Nâng cấp đợt G1 (người viết) | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 25/11 21h25: nhánh `full/nang-cap-g1` (từ `full/nang-cap-f-tong-the` @ `a6da118`), chạy cùng lúc với G2: ra bộ hồ sơ một lệnh (mặt bằng, mặt đứng, mặt cắt, phối cảnh lên các tờ có khung tên, in một PDF); ≤ 2 lượt CI. Viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-g1.md`. Bàn giao: `docs/ban-giao/nang-cap-g1.md` |
| Nâng cấp đợt G2 (người viết) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 25/11 21h27: nhánh `full/nang-cap-g2` (từ `full/nang-cap-f-tong-the` @ `a6da118`), chạy cùng lúc với G1: soát lỗi bản vẽ trước khi xuất (lệnh chỉ đọc + đường sửa qua thẻ xác nhận); ≤ 2 lượt CI. Viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-g2.md`. Bàn giao: `docs/ban-giao/nang-cap-g2.md` |
| Test & sửa đợt G2 (người rà) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 25/11 21h50: rà soát, thêm test, sửa lỗi đợt G2 (`check_drawing`, `fix_drawing`) trên nhánh `full/nang-cap-g2-test` (từ `full/nang-cap-g2` @ `8bbc27c`); ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 22h13: sửa 4 lỗi, commit cuối `f49e717` (code `7b9be8f`), CI 45640452138 xanh. Bàn giao: `docs/ban-giao/nang-cap-g2-test.md` |
| Test & sửa đợt G1 (người rà) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 25/11 22h02: rà soát, thêm test, sửa lỗi đợt G1 (`create_drawing_set`) trên nhánh `full/nang-cap-g1-test` (từ `full/nang-cap-g1` @ `dd29c9b`); ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 22h23: sửa 4 lỗi, commit cuối `99aaab7` (code `9c7c95f`), CI 45642708237 xanh. Bàn giao: `docs/ban-giao/nang-cap-g1-test.md` |
| Nâng cấp đợt G3 (gộp G1 + G2, người viết) | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 25/11 22h17: nhánh `full/nang-cap-g3` (từ `full/nang-cap-g1-test`, gộp `full/nang-cap-g2-test` @ `f49e717`, CI xanh rồi báo commit gộp cho G4 tách nhánh); viết bảng dự toán (khối lượng × đơn giá khách đưa, Excel có công thức), chạy cùng lúc với G4; 1 lượt CI gộp + ≤ 2 lượt G3. Viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-g3.md`. Bàn giao: `docs/ban-giao/nang-cap-g3.md` |
| Nâng cấp đợt G4 (người viết) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 25/11 22h33: nhánh `full/nang-cap-g4` (từ `full/nang-cap-g3` @ `395c313`, commit gộp G1 + G2, CI 45643804481 xanh), chạy cùng lúc với G3: render thật (lệnh RENDER, thư viện vật liệu Autodesk, đường lui về ảnh chụp hiện tại); ≤ 2 lượt CI. Viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-g4.md`. Bàn giao: `docs/ban-giao/nang-cap-g4.md` |
| Test & sửa đợt G3 (người rà) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 25/11 22h48: rà soát, thêm test, sửa lỗi đợt G3 (`cost_estimate`) trên nhánh `full/nang-cap-g3-test` (từ `full/nang-cap-g3` @ `6c7c650`); ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 23h00: sửa 3 lỗi, commit cuối `4061a6c` (code `d6b4a60`), CI 45649649749 xanh. Bàn giao: `docs/ban-giao/nang-cap-g3-test.md` |
| Test & sửa đợt G4 (người rà) | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 25/11 23h59: rà soát, thêm test, sửa lỗi đợt G4 (`named_view` action render, vật liệu thư viện) trên nhánh `full/nang-cap-g4-test` (từ `full/nang-cap-g4` @ `7641203`); soi kỹ chuỗi trả lời lệnh `-RENDER` và đường lui; ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 26/11 00h21: sửa 6 lỗi (1 nặng), commit cuối `29a2e6c` (code `fdeb306`), CI 45661671404 xanh. Bàn giao: `docs/ban-giao/nang-cap-g4-test.md` |
| Nâng cấp đợt G5 (gộp G3 + G4, người viết) | `session_01D56HmFrL65iF9oJokvqXw5` (chính là thread Test & sửa đợt F1) | Giao 26/11 00h11: nhánh `full/nang-cap-g5` (từ `full/nang-cap-g3-test` @ `4061a6c`, gộp `full/nang-cap-g4-test`, CI xanh rồi mới viết); vẽ lại từ ảnh / PDF scan (gắn ảnh làm nền, đặt tỉ lệ theo kích thước thật, Claude xem ảnh có lưới rồi vẽ đè, soát lại, kết quả là bản nháp cần khách kiểm); 1 lượt CI gộp + ≤ 2 lượt G5. Viết xong báo luồng chính, luồng chính giao thread khác rà. Thử tay: `docs/checklist-dot-g5.md`. Bàn giao: `docs/ban-giao/nang-cap-g5.md` |
| Test & sửa đợt G5 (người rà) | `session_01D56HmFAo4dZ7c8sbeNB9eh` (chính là thread Nâng cấp đợt F1) | Giao 26/11 00h42: rà soát, thêm test, sửa lỗi đợt G5 (`trace_image`) trên nhánh `full/nang-cap-g5-test` (từ `full/nang-cap-g5` @ `1cc438f`); soi kỹ đặt tỉ lệ / đổi tọa độ, ảnh xem có nền không, kiểm file, va chạm với G1 / G2; ≤ 2 lượt CI. Báo thẳng luồng chính. Xong 26/11 01h11: sửa 5 lỗi (1 nặng), commit cuối `e3eda7b` (code `464b6e5`), CI 45670027419 xanh. Bàn giao: `docs/ban-giao/nang-cap-g5-test.md` |
| Test tổng thể đợt G | `session_01D56HmFcYrzsme7GoBm8tNC` (chính là thread Nâng cấp đợt F2) | Giao 26/11 00h58: nhánh `full/nang-cap-g-tong-the` (từ `full/nang-cap-g5-test`, gộp tài liệu luồng chính): rà chiều ngang G1 → G5 cùng gói / chuỗi A → E / đợt F, sửa lỗi, đo token cuối đợt F so với cuối đợt G, gộp các checklist đợt G vào `docs/checklist-tong-the.md`, soát `docs/dot-g-cho-duyet.md` so với code; ≤ 2 lượt CI. Giữ nhánh tích hợp đợt G cho bản sửa sau lượt test lại bằng agent khác. Xong 26/11 01h19: sửa 2 lỗi chéo, commit cuối `881b12e` (code `4fbb2a2`), CI 45671817871 xanh. Bàn giao: `docs/ban-giao/test-tong-the-dot-g.md` |
| Kiểm tra độc lập đợt F + G | `session_01D56HmFjDhopvJD3nN6ZMUa` | Mở 26/11 01h20 từ `full/nang-cap-g-tong-the` @ `881b12e` (chủ dự án yêu cầu 25/11: một agent khác test lại toàn bộ tính năng đợt F và đợt G một lượt trước khi build bộ cài): chỉ ghi lỗi (KFG-xx), không sửa, không commit / push / CI; rà sâu F5 và G1 → G5, rà lướt F1 → F4 (đã kiểm độc lập một lần); gửi toàn văn báo cáo về luồng chính, luồng chính lưu vào `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` và chuyển lỗi cho thread giữ nhánh tích hợp đợt G; sau đó soát lại các lỗi đã sửa. Xong 26/11 05h06 (43 lỗi, 42 sửa đúng). **Đã đóng (lưu trữ) 26/11/2026.** |
| Sửa lỗi KFG (sau kiểm tra độc lập F + G) | Ba thread đã lưu được lên GitHub, giao 26/11 01h47 | Lượt kiểm tra độc lập báo 27 lỗi (`docs/ban-giao/kiem-tra-doc-lap-dot-fg.md`). Thread đợt F1 `session_01D56HmFAo4dZ7c8sbeNB9eh`: phần G2 trên `full/nang-cap-g-sua-kfg-a`. Thread Test & sửa F1 `session_01D56HmFrL65iF9oJokvqXw5`: phần G4 / G5 / F5 trên `full/nang-cap-g-sua-kfg-b`. Thread đợt F2 `session_01D56HmFcYrzsme7GoBm8tNC`: phần G1 / G3 / F2 / prompt ngay trên `full/nang-cap-g-tong-the`, rồi gộp hai nhánh kia, cập nhật checklist + `docs/dot-g-cho-duyet.md`, chạy CI (≤ 2 lượt). Thread kiểm tra độc lập soát lại từng lỗi |
| Kiểm tra độc lập đợt F | `session_01D56HmHz4EXXJceBgkmjcap` | Mở 25/11 19h45 từ `full/nang-cap-f-tong-the` @ `afdaa68`: agent mới test lại toàn bộ đợt F một lượt, chỉ ghi lỗi (KTF-xx), không sửa, không commit / push / CI; gửi toàn văn báo cáo về luồng chính, luồng chính lưu vào `docs/ban-giao/kiem-tra-doc-lap-dot-f.md` và chuyển lỗi cho thread giữ nhánh tích hợp đợt F; sau đó soát lại các lỗi đã sửa. **Đã đóng (lưu trữ) 26/11/2026.** |

Link mở thread: `https://claude.ai/code/<session id>`.

Thread đã đóng vẫn xem lại được và mở lại được (unarchive) khi cần; nhánh và tài liệu bàn giao giữ nguyên. Lỗi thuộc phần
của thread đã đóng chuyển cho thread Nâng cấp đợt F2 (`session_01D56HmFcYrzsme7GoBm8tNC`, giữ nhánh tích hợp mới nhất
`full/nang-cap-g-tong-the`); thread đợt F1 hoặc Test & sửa F1 rà lại. Kiểm tra độc lập lần sau mở thread mới (agent chưa
viết hay test phần đó).

**Thread còn mở (26/11/2026):** Luồng chính; Nâng cấp đợt F1, Test & sửa đợt F1, Nâng cấp đợt F2 (ba thread lưu được
code lên GitHub, dùng để sửa lỗi sau khi chủ dự án thử thật); Bộ cài thử nội bộ; Lưu trữ bộ cài; Bảo mật (đang chờ chủ dự
án trả lời về máy chủ kích hoạt).
