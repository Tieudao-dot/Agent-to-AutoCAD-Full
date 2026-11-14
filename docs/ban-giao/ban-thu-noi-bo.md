# Bộ cài thử nội bộ (BẢN THỬ NỘI BỘ)

> **Nhánh `full/ban-thu-noi-bo` KHÔNG BAO GIỜ được gộp** vào `full/test-tong-the`, `main` hay bất kỳ nhánh nào khác.
> Bộ cài này **chỉ chủ dự án dùng, không gửi cho ai**. Bộ cài thật làm sau, từ nhánh tích hợp, khi đã có máy chủ
> kích hoạt riêng.

*13/11/2026 · thread "Bộ cài thử nội bộ" · xuất phát từ `full/test-tong-the` @ `19944df`. Chủ dự án chốt 13/11/2026.*

> **Bản mới nhất (14/11/2026): `AgentToAutoCADFull-ThuNoiBo-nang-cap-2-0.9.4`** (lượt CI #35, mục "Lượt CI" cuối file).
> Có đủ gói dịch vụ + 3D đợt 1–3 + chuỗi nâng cấp A → E (gộp `full/nang-cap-tong-the` @ `f7f32ad`) **và bản sửa bộ cài
> TT-01** (@ `b0da814`): không còn hộp lỗi tiếng Anh "Setup was unable to automatically close all applications…".
> Bộ cài giờ:
> - AutoCAD đang mở thì hỏi bằng tiếng Việt (Thử lại / Hủy), không tự tắt AutoCAD;
> - tự tắt `node.exe` / `claude.exe` của plugin;
> - file còn bị giữ thì bảo khởi động lại máy rồi chạy lại.
>
> Các bộ cài cũ (`…-0.9.4` lượt #16, `…-nang-cap-0.9.4` lượt #32): **đừng dùng nữa**.

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

1. Mở trang lượt chạy CI mới: <https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/actions/runs/43614348802>. Cần đăng nhập GitHub bằng tài khoản có quyền vào repo.
2. Kéo xuống cuối trang, mục **Artifacts**, bấm **`AgentToAutoCADFull-ThuNoiBo-nang-cap-2-0.9.4`** để tải về file `.zip`.
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

**Lượt mới nhất (dùng lượt này):**

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
