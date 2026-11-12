# Bàn giao: gói dịch vụ trong plugin (2D / 3D / 2D + 3D)

*12/11/2026 · luồng chính bàn giao cho sub-thread "Gói dịch vụ".*

Đọc trước: [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) (Phần 2),
[`../dung-may-chu-kich-hoat-full.md`](../dung-may-chu-kich-hoat-full.md), `activation/README.md`.

## Chủ dự án đã chốt

| Ngày | Điểm | Chốt |
| --- | --- | --- |
| 12/11 | Máy chủ kích hoạt | Riêng cho bản Full: chủ dự án tự dựng Sheet + Apps Script + cặp khóa mới, gửi URL và khóa công khai |
| 12/11 | Chia gói | Phương án A: gói 3D có đường cơ bản (line, polyline, circle, arc, spline, ellipse, rectangle, polygon…), không có chữ, kích thước, hatch, layout, in PDF |
| 12/11 | Phạm vi đợt này | Cả 5 việc bên dưới, kèm **1 lượt** CI Windows để thử giao diện |

**Còn chờ chốt:** gói mặc định cho máy mới. Đang đặt tạm `2D` (hằng `GOI_MAC_DINH` trong mẫu Apps Script,
`PlanLogic.DefaultPlan` trong plugin), theo đề xuất trong kế hoạch.

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
    lại một dòng `2D + 3D` cũ (ví dụ đặt `plan.txt` chỉ đọc) trong khi trạng thái vẫn được làm mới. Máy dùng mã miễn
    kiểm tra (`offline`) giữ dòng gói được cấp;
  - `ParseResponse` trả thêm `plan`. **Đổi kiểu tuple:** kiểm tra lại `LicenseTests.cs` và mọi chỗ gọi.
- `plugin/ClaudeCAD.Plugin/Core/ToolException.cs`: mã lỗi `NOT_IN_PLAN`.
- `plugin/ClaudeCAD.Plugin/Sidecar/Activation.cs`:
  - lưu dòng gói vào `%LocalAppData%\ClaudeCAD\plan.txt`;
  - `Plan`, `PlanLabel`, `StatusText` có thêm gói;
  - nhận dòng `CCF1` khi kiểm tra trạng thái, khi kích hoạt bằng key, và khi dán mã cho máy không có mạng.
  - Build không có máy chủ kích hoạt thì `Plan = 2d3d` (mọi lệnh, như trước).
- `plugin/ClaudeCAD.Core.Tests/ClaudeCAD.Core.Tests.csproj`: đã link `Plans.cs`.

## Còn phải làm

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
   - hiện "Gói: 2D + 3D" ở header hoặc trong ⚙;
   - gói đổi sau một lượt kiểm tra: thông báo nhỏ "Gói đã đổi sang …" (`CheckDeviceStatus` trong
     `ChatController.cs`).
5. **CI:** **1 lượt** `.github/workflows/build.yml`, workflow_dispatch, `release: "false"`, cho nhánh của sub-thread.
6. **Tài liệu:** `activation/README.md`, `bo-cai/README.md`, `docs/checklist-thu-that.md` (thêm phần gói),
   `docs/ke-hoach-3d-goi-dich-vu.md`.

## Giới hạn

- Không merge / push `main`, không tạo PR, không chạy CI `release: true`.
- Không sửa URL máy chủ kích hoạt hay khóa công khai: chờ chủ dự án gửi (luồng chính sẽ chuyển).
- Không đưa tên hay ID model vào commit.
