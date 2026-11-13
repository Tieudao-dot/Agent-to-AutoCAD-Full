# Bàn giao: gói dịch vụ trong plugin (2D / 3D / Full)

*12/11/2026 · luồng chính bàn giao cho sub-thread "Gói dịch vụ" (`session_01D56HmJEU9szFU3rjc38bwT`, nhánh
`full/goi-dich-vu`).*

Đọc trước: [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) (Phần 2),
[`../dung-may-chu-kich-hoat-full.md`](../dung-may-chu-kich-hoat-full.md), `activation/README.md`.

## Chủ dự án đã chốt

| Ngày | Điểm | Chốt |
| --- | --- | --- |
| 12/11 | Máy chủ kích hoạt | Riêng cho bản Full: chủ dự án tự dựng Sheet + Apps Script + cặp khóa mới, gửi URL và khóa công khai |
| 12/11 | Chia gói | Phương án A: gói 3D có đường cơ bản (line, polyline, circle, arc, spline, ellipse, rectangle, polygon…), không có chữ, kích thước, hatch, layout, in PDF |
| 12/11 | Phạm vi đợt này | Cả 5 việc bên dưới, kèm **1 lượt** CI Windows để thử giao diện |
| 12/11 | Tên gói | "2D + 3D" gọi là **Full** (mã ký số vẫn `2d3d`) |
| 12/11 | Gói mặc định | `2D` cho máy mới, ô Gói trống và ô gõ sai (hằng `GOI_MAC_DINH` trong mẫu Apps Script, `PlanLogic.DefaultPlan` trong plugin) |

## Đã xong

- **Máy chủ** (commit `f570344`): mẫu `activation/apps-script/Code.gs.template` có cột Gói (cột 10). `status` và
  `activate` trả thêm `plan` = `CCF1|máy|2d / 3d / 2d3d|ngày UTC|chữ ký`, cùng khóa RSA với `CCS1`. `taoKeyThuCong`
  gửi thêm dòng gói. Test trong `agent/test/activation.test.ts`: 18/18 qua.

## Dở dang (commit "WIP" trên nhánh `claude/inspiring-cray-58xsmt`, **chưa build, chưa chạy test**)

- `plugin/ClaudeCAD.Plugin/Core/Plans.cs` (mới): `PlanLogic`
  - bảng nhãn Chung / 2D cho 90 lệnh hiện có theo phương án A; nhóm 3D để trống, lệnh 3D thêm sau;
  - `Allowed(plan, tool)`: lệnh chưa có nhãn chỉ mở cho gói `2d3d`, nên quên gắn nhãn không làm lộ lệnh;
  - `BlockedMessage`: lời nhắn cho Claude khi lệnh nằm ngoài gói.
- `plugin/ClaudeCAD.Plugin/Core/License.cs`:
  - `PlanCheck`, `PlanPrefix = "CCF1"`, `IsPlanText`, `VerifyPlan`;
  - `EffectivePlan(plan, status)`: dòng gói cũ hơn dòng trạng thái thì về gói mặc định. Việc này chặn trường hợp giữ
    lại một dòng `Full` cũ (ví dụ đặt `plan.txt` chỉ đọc) trong khi trạng thái vẫn được làm mới. Máy dùng mã miễn
    kiểm tra (`offline`) giữ dòng gói được cấp;
  - `ParseResponse` trả thêm `plan`. **Đổi kiểu tuple:** kiểm tra lại `LicenseTests.cs` và mọi chỗ gọi.
- `plugin/ClaudeCAD.Plugin/Core/ToolException.cs`: mã lỗi `NOT_IN_PLAN`.
- `plugin/ClaudeCAD.Plugin/Sidecar/Activation.cs`:
  - lưu dòng gói vào `%LocalAppData%\ClaudeCAD\plan.txt`;
  - `Plan`, `PlanLabel`, `StatusText` có thêm gói;
  - nhận dòng `CCF1` khi kiểm tra trạng thái, khi kích hoạt bằng key, và khi dán mã cho máy không có mạng.
  - Build không có máy chủ kích hoạt thì `Plan = 2d3d` (mọi lệnh, như trước).
- `plugin/ClaudeCAD.Core.Tests/ClaudeCAD.Core.Tests.csproj`: đã link `Plans.cs`.

## Kết quả sub-thread "Gói dịch vụ" (12/11/2026, nhánh `full/goi-dich-vu`)

Đã làm cả 6 việc bên dưới. Phần WIP được giữ nguyên (build được, test cũ không vỡ: `ParseResponse` chỉ được gọi qua
tên trường). Khác với gợi ý ban đầu: ngữ cảnh gửi trợ lý dùng tên `context.service_plan` và `context.plan_tools`, vì
`plan` đã là cờ duyệt thẻ kế hoạch trong tin nhắn. Plugin gửi `plan_tools` (danh sách lệnh mở cho gói) trừ khi gói là
`2d3d`; trợ lý lọc `buildTools` theo danh sách đó, nên `capture_view` và `ping` được tính như mọi lệnh.

## Còn phải làm (danh sách gốc)

1. **Build và test lõi:** `dotnet test plugin/ClaudeCAD.Core.Tests`. Máy cloud cài được SDK 8 bằng
   `apt-get update && apt-get install -y dotnet-sdk-8.0`. Viết test:
   - `VerifyPlan`: đúng máy, máy khác, sửa gói, chữ ký hỏng, gói lạ. Tạo cặp khóa RSA ngay trong test để ký;
   - `EffectivePlan`: cũ hơn trạng thái, offline, không có dòng gói;
   - `PlanLogic.Allowed` cho cả 3 gói;
   - **test bắt buộc nhãn:** mọi lệnh đăng ký trong `plugin/ClaudeCAD.Plugin/Drawing/*.cs` (`Register("…"` /
     `new ToolDef("…"`) đều có nhãn, và không có nhãn cho lệnh không tồn tại. Đọc file nguồn từ test, giống
     `PaletteCoreTests.cs` đọc `templates/`.
2. **Chặn ở `ToolDispatcher`:**
   - thêm `string Plan { get; }` vào `ITurnHost` (ChatController trả `Activation.Plan`);
   - kiểm `PlanLogic.Allowed` trong `HandleAsync`, ngay sau khi tìm thấy lệnh, và trong `ExecuteBatch` cho từng lệnh
     con. Macro chạy qua `HandleAsync` nên cũng bị chặn.
3. **Trợ lý (`agent/`):**
   - plugin gửi `context.plan` kèm mỗi tin nhắn (giống `read_only`, `ChatController.cs` quanh dòng 761);
   - `contextPreamble` thêm dòng nói rõ gói;
   - chỉ nạp lệnh thuộc gói: `buildTools` lọc theo danh sách lệnh, nên plugin cần gửi danh sách (hoặc nhóm) lệnh.
     Gói đổi thì khởi động lại phiên Claude, giống khi đổi skill (`session.ts`, `loadedSkillsKey`);
   - system prompt: máy ngoài gói thì trả lời "máy này dùng gói …, liên hệ chủ plugin để mở …";
   - thêm `NOT_IN_PLAN` vào `ERROR_CODES` (`agent/src/ipc/protocol.ts`).
   - Lưu ý: danh sách lệnh của trợ lý (`cadTool(b, '…'`) thiếu `capture_view` và `ping` so với plugin, vì hai lệnh
     này khai báo theo cách khác. Bộ lọc phải tính cả chúng.
4. **Giao diện:**
   - hiện "Gói: Full" ở header hoặc trong ⚙;
   - gói đổi sau một lượt kiểm tra: thông báo nhỏ "Gói đã đổi sang …" (`CheckDeviceStatus` trong
     `ChatController.cs`).
5. **CI:** **1 lượt** `.github/workflows/build.yml`, workflow_dispatch, `release: "false"`, cho nhánh của sub-thread.
6. **Tài liệu:** `activation/README.md`, `bo-cai/README.md`, `docs/checklist-thu-that.md` (thêm phần gói),
   `docs/ke-hoach-3d-goi-dich-vu.md`.

## Sửa sau kiểm tra độc lập (12/11/2026)

Lỗi từ `docs/ban-giao/kiem-tra-doc-lap.md` (nhánh `full/kiem-tra-doc-lap`). Gộp `full/test-tong-the` @ `3f05da7`
(fast-forward), sửa trong commit `457bc98`. Test lõi 334/334, trợ lý 209/209, build net48 + net8.0-windows; chưa chạy CI
(CI chung chạy sau khi thread Test tổng thể gộp).

| Mã | Cách sửa | Test |
| --- | --- | --- |
| KT-02 | Chốt C3 (chặn). `PlanLogic.TextParams` / `BlockedParam`: gói không có 2D bị `ToolDispatcher` từ chối `attributes` (không rỗng) của `define_block` và `insert_block`, mã `NOT_IN_PLAN`, cả lệnh lẻ và trong `batch_execute`. `set_block_attributes` chuyển sang nhóm 2D. Block không thuộc tính vẫn dùng được; chèn block có sẵn định nghĩa thuộc tính thì thuộc tính lấy giá trị mặc định. Trợ lý gói 3D không thấy tham số `attributes`; system prompt gói 3D ghi "blocks without attributes" | `PlanTests.KT02_*`, `plan-fixes.test.ts` |
| KT-03 | `session.ts`: "−" / "+" tính trên skill gói thật sự nạp (`skillsForPlan`); skill bị gói loại khi đổi gói vào `skills_removed`, skill thêm mà gói không nạp không được báo "đã thêm" | `plan-fixes.test.ts` |
| KT-04 | `MacroLogic.BlockedCalls` + `PlanLogic.MacroBlockedText`: kiểm cả macro trước khi chạy; có lệnh (hoặc thuộc tính block) ngoài gói thì không chạy lệnh nào, thẻ lỗi tiếng Việt "Không chạy macro …: máy này dùng gói …" | `PlanTests.KT04_*` |
| KT-05 | Chốt C4. `LicenseLogic.AcceptPlan`: `SavePlan` chỉ nhận dòng gói mới hơn hoặc cùng ngày dòng đang lưu; dán dòng cũ báo lý do. Tài liệu ghi rõ máy offline không thu hồi được gói nếu khách không dán dòng mới, chỉ cấp Full offline cho máy tin cậy | `PlanTests.KT05_*` |
| KT-06 | `upsertDevice` (Apps Script): tên giữ chỗ "(…)" chỉ điền ô Thiết bị trống, không ghi đè tên máy gửi lên | `activation.test.ts` (KT-06) |
| KT-35 | `buildTools(…, plan)`: gói 2D bỏ `view` / `direction` / `visual_style` và câu "3D:" khỏi `capture_view` | `plan-fixes.test.ts` |
| KT-36 | Regex `plan:` bỏ phần `# ghi chú`; `propose_skill` ghi `plan: 2d` / `plan: 3d` theo gói đang dùng (Full: không ghi, dùng ở mọi gói) | `plan-fixes.test.ts` |
| KT-42 | Chấp nhận, không chặn chèn block (đã chốt 13/11/2026, chủ dự án đồng ý). Gói 3D: chèn block có sẵn vẫn hiện chữ mặc định của block; Claude không đặt / sửa được chữ thuộc tính: `insert_block` không truyền được giá trị thuộc tính, `set_block_attributes` thuộc nhóm 2D (đã có từ bản sửa KT-02) | `PlanTests.KT42_*`, `plan-fixes.test.ts` (KT-42) |
| KT-43 | Chặn như C4 (đã chốt 13/11/2026, chủ dự án đồng ý). `LicenseLogic.StatusRefusal`: dòng `CCS1` cũ hơn dòng đang lưu bị từ chối; dòng dán tay cùng ngày không đè `disabled`; trả lời của máy chủ chỉ cần không cũ hơn. Áp ở mọi đường: dán mã (thẻ Kích hoạt, ô "Dùng mã" trên 🔒, ⚙ ▸ Dán mã — cùng đi qua `ActivateAsync`), kích hoạt bằng key, kiểm tra trạng thái. Bộ cài không kiểm được chữ ký nên khi đã có `status.txt` / `plan.txt` thì ghi `status-new.txt` / `plan-new.txt` (`SaveSignedLine`), plugin áp theo cùng luật khi đọc rồi xóa. Sửa kèm test cũ `EditionTests.KT01_*` (kiểm đường lưu plan của bộ cài) cho khớp | `PlanTests.KT43_*` |

Còn biết: cùng ngày UTC, `plan.txt` chỉ đọc vẫn giữ dòng Full cũ tới hết ngày (dòng cùng ngày được tin). Chấp nhận, vì
hạ gói lần nữa trong ngày là hiếm.

## Giới hạn

- Không merge / push `main`, không tạo PR, không chạy CI `release: true`.
- Không sửa URL máy chủ kích hoạt hay khóa công khai: chờ chủ dự án gửi (luồng chính sẽ chuyển).
- Không đưa tên hay ID model vào commit.
