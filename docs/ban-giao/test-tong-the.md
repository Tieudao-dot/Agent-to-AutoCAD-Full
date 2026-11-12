# Bàn giao: Test tổng thể (gói dịch vụ + 3D đợt 1, 2, 3)

*12/11/2026 · luồng chính giao. Nhánh `full/test-tong-the`, xuất phát `full/ve-3d-test-3` @ `4bf5cce` (đã gộp tài liệu
của luồng chính từ `claude/inspiring-cray-58xsmt`, không xung đột). Bước 6, bước cuối trong chuỗi chủ dự án đặt.*

Máy cloud không có AutoCAD: "test" = rà code theo chiều ngang (chỗ gói, macro, cổng xác nhận, self-test và 3 đợt 3D chạm
nhau), viết test tự động quét nguồn để lỗi cùng loại tự đỏ về sau, sửa lỗi. Checklist cho chủ dự án thử tay:
[`../checklist-tong-the.md`](../checklist-tong-the.md).

## Đã rà

| Phần | Kết quả |
| --- | --- |
| Gói × 3D: chặn ở plugin | 29 lệnh `ThreeD` (14 + 12 + 3) bị từ chối ở gói 2D và máy mới (gói mặc định 2D): lệnh lẻ, từng lệnh trong `batch_execute` (kiểm gói **trước** "không chạy trong batch", nên luôn ra `NOT_IN_PLAN`), macro (cả hai đường chạy macro đi qua `HandleAsync`). Gói 3D bị từ chối mọi lệnh nhóm 2D; Full có đủ. Không sửa logic gói |
| Gói × 3D: trợ lý | `plan_tools` lọc đúng: gói 2D không nạp lệnh 3D, gói 3D không nạp lệnh nhóm 2D, Full nạp mọi lệnh. Skill `ve-3d-kien-truc` chỉ nạp ở 3D / Full. Đổi gói giữa chừng: phiên Claude khởi động lại, cùng cuộc chat (test sẵn có trong `session.test.ts`) |
| System prompt theo gói | **Lỗi 1** (bảng dưới) |
| Cổng xác nhận | Đủ 7 lệnh đổi hình khối luôn qua thẻ khi khối có trước lượt (cả trong batch), khối dựng trong lượt thì không; 7 lệnh theo luật > 20; 15 lệnh không bao giờ đổi đối tượng có sẵn. Test mới bắt phân loại mọi lệnh 3D |
| Macro | Mọi tham số handle của mọi lệnh (14 tên ở 3D: `target`, `subtract`, `profile_handles`, `path_handle`, `section_handles`, `guide_handles`, `other_handles`, `wall_handles`, `centerline_handles`, `door_handles`, `window_handles`, `slab_handles`, `handle`, `handles`) được ánh xạ. Không có tham số thường nào trùng dạng tên handle. Test mới quét nguồn |
| Hoàn tác U khi trộn đợt | Mọi lệnh ghi dùng chung một nhóm U của lượt; `set_ucs` (không transaction) cũng mở nhóm đó; `set_view_3d` / `set_visual_style` không mở nhóm. Đúng; thử tay X1 |
| "Chỉ xem" | Chỉ `zoom_*`, `set_view_3d`, `set_visual_style` được đổi; lệnh đọc 3D (`get_solid_info`, `check_interference`) chạy; `set_ucs`, mọi lệnh dựng / sửa, cả `build_3d_from_plan` chạy thử bị chặn. Đúng |
| Giới hạn tham số | Số handle tối đa của mọi lệnh 3D khớp giữa trợ lý và plugin (500 / 200 / 5000 / 50, `MaxSourceEntities` 5000, `MaxSectionSolids` 500); `SOLID_LIMITS` / `SOLID2_LIMITS` / `PLAN3D_LIMITS` đã có test so nguồn |
| Thông báo lỗi | `NOT_IN_PLAN`, `READ_ONLY`, "Confirmation required", `INVALID_PARAMS` "Nothing was changed / built": cùng mẫu ở 3 đợt |
| `CLAUDECHATSELFTEST` | 7 bước 3D chạy nối tiếp trên bản vẽ tạm, khối đặt ở các vùng x tách nhau (6000, 9000, 12000, 15000, 20000) nên không đè; mỗi bước transaction riêng; bản vẽ tạm đóng không lưu, thư mục thử xóa khi mọi bước đạt. Không thấy lỗi |
| Bản 2D | Test 2D cũ xanh; system prompt gói 2D và Full giữ nguyên nội dung 2D (chỉ đổi 1 câu ở mục 4, xem lỗi 1) |

## Lỗi tìm được và cách sửa

| # | Mức | Lỗi | Sửa |
| --- | --- | --- | --- |
| 1 | Vừa | System prompt gói **3D** bảo Claude dùng lệnh gói đó không có: "Practical geometry hints" (tường bằng `offset_entity`, chuỗi kích thước `add_dim_linear` + `add_dim_continue`) và câu "đặt `create_text_style` đầu batch". Claude bị dẫn tới lệnh sẽ trả `NOT_IN_PLAN`, tốn lượt và trái với câu "không bắt chước lệnh thiếu" | Tách `TWO_D_HINTS`, chỉ thêm cho gói 2D / Full (và plugin cũ không gửi gói); câu batch đổi thành "create_layer (và kiểu cần dùng)". Test mới: system prompt của mỗi gói không nhắc tên lệnh nào ngoài gói |

Logic gói: **không sửa**. Không đổi `activation/activation.json`, `License.cs`, `.claude/settings.json`.

## Test thêm (để sau này thêm lệnh mà quên thì đỏ)

- `plugin/ClaudeCAD.Core.Tests/CrossPlanTests.cs` (5 test):
  - quét `agent/src/tools/*.ts` (tham số kiểu `handle` / `handles`) và `Drawing/*.cs` (`p.Handle(s)`, `Explicit`): mọi
    tham số handle phải được macro ánh xạ; plugin không đọc handle từ tham số mà trợ lý không khai là handle; không có
    tham số thường mang tên dạng handle;
  - 29 lệnh 3D bị chặn ở gói 2D / máy mới, lệnh nhóm 2D bị chặn ở gói 3D, Full = mọi lệnh;
  - cổng gói chạy ở lệnh lẻ và từng lệnh batch (trước kiểm "không chạy trong batch"), macro đi qua `HandleAsync`.
- `agent/test/crossplan.test.ts` (6 test): lệnh nạp theo gói; system prompt không nhắc lệnh ngoài gói; skill 3D chỉ ở
  3D / Full; mọi lệnh `ThreeD` thuộc đúng một lớp của cổng xác nhận (luôn hỏi / theo số lượng / không đổi gì).

## Test và build

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **309/309** (304 + 5) ở lượt đầu; **322/322** sau 3 điểm đã chốt (+13);
  **331/331** sau phần thay bản gốc (+9).
- Trợ lý `npm test`: **191/191** (185 + 6) ở lượt đầu; **199/199** (+8) sau 3 điểm đã chốt; **201/201** (+2) sau phần thay
  bản gốc.
- Build Linux (.NET SDK 10, xóa `obj` / `bin` giữa các lần): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows lượt đầu: run 43230375890 (`build.yml`, workflow_dispatch, `release: false`, commit `fa68791`) **xanh** (~10 phút, gồm UI test WPF, build 3 bản, Setup.exe).
- `make-bundle` chạy thử trên Linux (DLL giả, `--skip-npm`): `Contents\edition.json` đúng, `PackageContents.xml` có tên
  mới và phiên bản.
- CI Windows sau phần thay bản gốc: run 43243835815 (`build.yml`, workflow_dispatch, `release: false`, commit `3881429`) **xanh** (~10 phút, gồm UI test WPF, build 3 bản, bundle có `edition.json`, **bộ cài `.iss` mới biên dịch được và Setup.exe build được**; các bước phát hành bỏ qua).
- CI Windows sau 3 điểm đã chốt: run 43236499463 (`build.yml`, workflow_dispatch, `release: false`, commit `6e9193d`) **xanh** (~9 phút, gồm UI test WPF, build 3 bản, Setup.exe; các bước phát hành bỏ qua).

## Chủ dự án đã chốt (12/11/2026) và đã làm

Sau báo cáo đầu, chủ dự án chốt 3 điểm; làm trên cùng nhánh, tiếp từ `f4e81b0` (commit sửa: `41e4b37`).

### 1. `explode_entities` trên khối 3D luôn qua thẻ xác nhận

- **Luật:** phá khối 3D có trước lượt (kể cả khối Claude dựng ở lượt trước) luôn cần cậu đồng ý, như cộng / trừ, cắt,
  bo, vát, làm rỗng. Khối 3D = `Solid3d`, `Body`, mọi loại `Surface` (`PlaneSurface`, `NurbSurface`, `ExtrudedSurface`…),
  `SubDMesh`, `PolyFaceMesh`, `PolygonMesh`. Khối dựng trong lượt này: không hỏi. Đối tượng 2D (block, polyline, region,
  hatch, kích thước…): **giữ luật cũ** (chỉ hỏi khi > 20 đối tượng).
- **Cách làm (chắc nhất: plugin kiểm loại):**
  - plugin (`ModifyTools.cs`, `ExplodeEntities`): trước khi phá bất cứ gì, mở từng đối tượng để đọc, xét loại theo kiểu
    .NET và các kiểu cha trong `Autodesk.AutoCAD.DatabaseServices` (`SolidLogic.ExplodeConfirmTypes`). Khối 3D không
    dựng trong lượt và lệnh không có cờ `user_confirmed` → từ chối cả lệnh (`INVALID_PARAMS`, câu mở đầu
    "Confirmation required", liệt kê handle, bảo Claude gọi `request_confirmation` rồi dừng lượt). Không phá gì;
  - "dựng trong lượt": bộ theo dõi "Thêm · Sửa · Xóa" của lượt có sẵn (`ChangeTracker`, sự kiện `ObjectAppended` của
    bản vẽ). Chạy macro cũng là một lượt, nên khối macro vừa dựng phá được. Quá 100 000 đối tượng thì bộ theo dõi ngừng
    ghi: đối tượng mới bị coi là có sẵn, tức hỏi thêm một lần (phía an toàn);
  - trợ lý (`confirmation.ts`, `applyUserConfirmation`): khi cậu đã đồng ý trong lượt (nút Đồng ý trên thẻ, hoặc câu
    "đồng ý" trả lời câu hỏi của lượt trước), thêm `user_confirmed: true` vào `explode_entities`, cả trong
    `batch_execute`. Claude không tự thêm được: cờ không có trong schema lệnh, và trợ lý xóa cờ ở mọi lệnh không được
    phép. Plugin từ chối thì trợ lý ghi nhận lượt đó đã hỏi (giống cổng của trợ lý), nên "đồng ý" ở tin sau có hiệu lực;
  - mô tả `explode_entities` nói rõ luật này cho Claude.
- **Test:** `ExplodeConfirmTests.cs` (13: loại khối, mọi `Surface`, đối tượng 2D giữ luật cũ, chỉ khối có sẵn chưa đồng ý
  bị từ chối, câu báo, bộ theo dõi, kiểm nguồn: kiểm trước khi phá / xóa, đọc cờ, dùng `CreatedThisTurn`); trợ lý: cờ chỉ
  có sau khi đồng ý, Claude không tự thêm được (lệnh lẻ và trong batch), nhận ra lời từ chối của plugin (lẻ và trong
  batch), lượt sau "đồng ý" có cờ.
- **Thử tay:** X4 trong `docs/checklist-tong-the.md`.

### 2. Skill 2D không nạp ở gói 3D

- `ve-kien-truc`, `ve-mep`, `ve-ha-tang` đều chỉ là chuẩn vẽ 2D (offset, kích thước, mleader, chữ, bảng, hatch): gắn
  `plan: 2d`. `skillPlan` đọc thêm `2d`; `skillsForPlan`: gói 2D bỏ skill `3d`, gói 3D bỏ skill `2d`, Full / plugin cũ
  nạp tất cả; skill không có dòng `plan` (của người dùng) nạp ở mọi gói; giá trị lạ coi như không có.
- `build_3d_from_plan` đọc mặt bằng theo quy ước layer của `ve-kien-truc`. Mặc định trong plugin đã đúng quy ước
  (`A-WALL`, `A-DOOR`, `A-WIND`, `A-SLAB` / `A-FLOR`, block `CUA-DI-*` / `CUA-SO-*`). Skill `ve-3d-kien-truc` thêm **mục 0**:
  bảng layer mặt bằng, cách vẽ tường 220 / 110 (hai nét, đối xứng trục, đóng mép lỗ), ký hiệu cửa đi / cửa sổ, đường bao
  sàn và lỗ sàn, polyline có bề rộng, đường trục + `thickness`, cột không được đọc, ví dụ phòng 4 × 6 m chỉ bằng đường cơ
  bản. Không chép phần chữ / kích thước / dim style (có test).
- Bộ cài không ghi đè skill đã có trên máy (`onlyifdoesntexist`), nên máy nâng cấp từ bản gốc giữ 3 skill 2D cũ không có
  `plan: 2d`. Đã xử lý ở mục "Thay bản gốc như nâng cấp" bên dưới: không phải sửa tay.
- Test cũ đổi kỳ vọng (`plan3d.test.ts`): `skillPlan('ve-kien-truc')` từ `undefined` thành `'2d'`; gói 3D từ "nạp mọi
  skill" thành "chỉ `ve-3d-kien-truc`". Lý do: quyết định 12/11/2026 ở trên.

### 3. Giảm token (đề xuất 1 + 3, không đổi tên lệnh nào)

- **Đề xuất 1:** 10 lệnh dựng khối (`draw_box`, `draw_cylinder`, `draw_sphere`, `draw_wedge`, `draw_torus`,
  `draw_pyramid`, `extrude`, `revolve`, `sweep`, `loft`) chỉ còn `layer` trong 4 thuộc tính chung; plugin vẫn nhận
  `color` / `linetype` / `lineweight` / `layout` (macro cũ không vỡ). Luật 3D thêm 1 dòng: màu / nét của khối lấy theo
  layer, hoặc `set_entity_props` sau khi dựng.
- **Đề xuất 3:** bỏ khỏi mô tả lệnh phần trùng với luật 3D trong system prompt hoặc chỉ là ví dụ: quy trình chạy thử rồi
  hỏi của `build_3d_from_plan` (luật 3D vẫn giữ), câu ví dụ tiếng Việt, "Typical use" của `extrude` / `revolve`, ví dụ
  công dụng của `sweep` / `loft`, gợi ý kiểm `cut_area_m2` (đã có trong luật 3D), câu "so kết quả với kích thước định
  dựng" lặp ở 10 lệnh (rút còn danh sách trường trả về). Ý nghĩa và ràng buộc tham số giữ nguyên; câu "bề rộng dưới
  `min_thickness` là nét in" chuyển vào mô tả tham số `min_thickness`. Công dụng từng lệnh, ví dụ, `area_min` cho mặt
  bằng lớn, `openings` cho khe hở chuyển sang skill `ve-3d-kien-truc` (mục 0, 3, 4, 5, 6).
- Không làm đề xuất 2 (gộp 6 lệnh khối) và 4 (câu mô tả điểm dùng chung).

## Thay bản gốc như nâng cấp (chốt 12/11/2026)

Máy đang có ClaudeCAD bản gốc (chỉ 2D) thì bản Full thay nó như một bản nâng cấp, có cảnh báo. Khách giữ lịch sử chat,
cài đặt, đăng nhập Claude; máy xin kích hoạt lại ở bảng thiết bị mới; sau đó đổi gói chỉ bằng ô Gói. Commit: `b6ad539`.

1. **Giữ danh tính kỹ thuật** (không đổi): `AppId`, thư mục `ClaudeCAD.bundle`, `ProductCode` / `UpgradeCode` và `AppName`
   của 3 thành phần trong `PackageContents.xml` (khóa registry của bộ nạp AutoCAD), lệnh `CLAUDECHAT` / `CC` /
   `CLAUDECHAT*`, thư mục dữ liệu `%LocalAppData%` / `%AppData%\ClaudeCAD`, GUID của palette, tên file `ClaudeCAD-Setup.exe`
   (giữ: link "bản mới nhất" cố định và nhận diện log cài đặt trong "Gửi báo cáo lỗi" dựa vào tên này).
2. **Tên hiển thị "Agent to AutoCAD Full"** (`ProductInfo.DisplayName` trong `Core/Edition.cs`, `AppDisplayName` trong `.iss`):
   - bộ cài: `AppName`, `AppVerName` (= tên + phiên bản), `UninstallDisplayName` (mục trong danh sách ứng dụng), tiêu đề,
     trang Kích hoạt, trang cuối, các hộp thoại; `AppPublisher` giữ `ClaudeCAD`;
   - `PackageContents.xml`: `Name`, `Description`, mô tả thành phần;
   - plugin: tiêu đề palette, dòng đầu ⚙ "Agent to AutoCAD Full 0.9.4", thẻ "Bắt đầu với …", thẻ 🔒 Kích hoạt / tạm khóa,
     icon và bong bóng ở thanh trạng thái, thông báo Windows, dòng lệnh khi nạp và khi tự kiểm tra, dòng đầu báo cáo
     `CLAUDECHATSELFTEST` và "Gửi báo cáo lỗi". Câu trả lời của Claude và log kỹ thuật không đổi.
3. **Dấu nhận diện bản:** `bundle/edition.json` = `{"edition": "full", "name": "Agent to AutoCAD Full", "repo":
   "Tieudao-dot/Agent-to-AutoCAD-Full"}`, `make-bundle` chép vào `Contents\edition.json` (và từ chối nếu `edition` khác
   `full`). Plugin đọc nó (`UpdateService.BundleEdition`) để ghi "edition full" vào dòng phiên bản của tự kiểm tra và báo
   cáo lỗi.
4. **Bộ cài nhận ra bản đang có** (`installer/ClaudeCAD.iss`, `DetectEdition`, `ExistingAppVersion`, `ConfirmReplaceOriginal`),
   cùng luật với `EditionLogic` (C#, có test):
   - `Contents\edition.json` có `"edition":"full"` → Full: nâng cấp bình thường, không hỏi;
   - không có, `Contents\update-config.json` chứa `agent-to-autocad/` (repo gốc; v0.9.3 đã phát hành cũng có file này) →
     bản gốc; chứa `agent-to-autocad-full/` → Full (bản Full build trước khi có `edition.json`);
   - có thư mục bundle mà không đọc được hai file → không rõ, xử lý như bản gốc;
   - so chữ thường, bỏ khoảng trắng / xuống dòng. Phiên bản đọc từ `AppVersion="…"` của `PackageContents.xml`;
   - bản gốc / không rõ: hộp thoại tiếng Việt (bản gì, phiên bản, giữ gì, mất gì) với hai nút **"Tiếp tục" / "Hủy"**
     (chủ dự án chốt 12/11/2026). Dùng `TaskDialogMsgBox` có nhãn nút riêng, vì `MsgBox` của bộ cài chỉ có nút tiếng Anh
     (OK / Cancel). Hủy hoặc đóng hộp thoại: bộ cài thoát trước khi đổi bất cứ gì, bản gốc giữ nguyên. Bản đầu (OK / Cancel,
     commit `b6ad539`) đã qua CI; bản `TaskDialogMsgBox` chưa được biên dịch, sẽ được kiểm ở lượt CI chung sau khi gộp
     các bản sửa của thread kiểm tra độc lập (luồng chính giao, không chạy CI riêng);
   - `/SILENT`, `/VERYSILENT`: không hỏi, cài tiếp, ghi log "existing install: …" và "silent install: replacing …".
5. **Thêm hai việc để nâng cấp thật sự an toàn** (ngoài danh sách giao, cùng mục đích):
   - giấy phép bản gốc (`license.txt`) không còn được tính là "đã kích hoạt" khi thay bản gốc / không rõ, nên bộ cài hiện
     lại **trang Kích hoạt** và gửi yêu cầu tới máy chủ của bản Full ngay lúc cài. Không xóa file nào của giấy phép: key
     mới ghi đè khi kích hoạt. Trước đây bộ cài chỉ xem tiền tố của file nên sẽ bỏ qua trang này, máy chỉ xin kích hoạt
     khi mở khung chat;
   - bản cập nhật bản gốc đã tải sẵn chờ cài (`%LocalAppData%\ClaudeCAD\updates\stage`, do `CLAUDECHATUPDATE` của bản
     gốc tạo) bị xóa lúc bắt đầu cài (`ssInstall`, chỉ khi thay bản gốc / không rõ). Nếu để lại, script cập nhật của bản
     gốc chép nó đè ngược lên bản Full lúc đóng AutoCAD. Đây là thư mục tạm của một bản tải về, không phải dữ liệu người dùng.
6. **Skill mẫu 2D cũ không phải sửa tay:** trợ lý (`skills.ts`, `SAMPLE_2D_SKILLS`) coi `ve-kien-truc`, `ve-mep`,
   `ve-ha-tang` thiếu dòng `plan` là `plan: 2d`. Dòng `plan` trong file luôn thắng (`plan: all` = mọi gói); skill tên khác
   (người dùng tự viết) giữ nguyên "mọi gói". Chọn theo tên vì skill mẫu là file người dùng được sửa: so nội dung sẽ sai
   ngay khi họ sửa. Danh sách khớp `SkillStore.BuiltinNames` của plugin (có test).
7. **Test:** `EditionTests.cs` (9: luật nhận diện full / gốc / không rõ, phiên bản từ `PackageContents.xml`, tên và phiên
   bản, danh tính kỹ thuật không đổi, tên hiển thị ở bộ cài / `PackageContents` / palette / ⚙ / self-test / báo cáo lỗi,
   bộ cài dùng đúng dấu và thứ tự của `EditionLogic`, cài ngầm không hỏi, trang kích hoạt hiện lại, bỏ bản cập nhật chờ);
   trợ lý +2 (skill mẫu cũ thiếu dòng `plan` ở 3 gói, `plan: all` / `plan: 3d` thắng, danh sách khớp skill có `plan: 2d` và
   `SkillStore.BuiltinNames`). Bộ cài `.iss` chỉ biên dịch được trên Windows: CI là lần biên dịch đầu tiên (biên dịch thử
   trên máy cloud bằng Inno Setup tải từ npm bị chế độ Auto chặn vì là mã từ ngoài).
8. **Thử tay:** I6 trong `docs/checklist-thu-that.md`, T5 trong `docs/checklist-tong-the.md`.

## Sửa sau kiểm tra độc lập (12/11/2026)

Nguồn: `docs/ban-giao/kiem-tra-doc-lap.md` (nhánh `full/kiem-tra-doc-lap` @ `02fabf7`), 39 lỗi: 3 nặng, 9 vừa, 27 nhẹ.
Mỗi thread sửa fast-forward lên `full/test-tong-the` @ `3f05da7`, sửa trên nhánh riêng; thread này sửa phần của mình
(`8e163b3`) rồi gộp 4 nhánh bằng merge commit: `full/goi-dich-vu` @ `73aaba6` → `f42fb3f`, `full/ve-3d` @ `97f17a7` →
`54201f9`, `full/ve-3d-dot-2` @ `9d301c9` → `61cc03d`, `full/ve-3d-dot-3` @ `8259828` → `3527374`; ghép nối `601b2f9`.

**Gộp:** chỉ xung đột ở `docs/checklist-thu-that.md` (I5 của thread này với I5b / I5c của gói), giữ cả hai. Các file tự
gộp được đã rà tay: `ChatController.cs` có cả hai chặn macro (ngoài gói KT-04, sửa khối có sẵn KT-10), `ToolDispatcher.cs`
có cả `RequireParamsInPlan` (KT-02) và `WritesFor` (KT-15) ở lệnh lẻ lẫn `batch_execute`, `systemPrompt.ts` có cả luật thẻ
(C2) và luật nhiều tầng (KT-14), `session.ts` có cả theo dõi thẻ (C2) và danh sách skill theo gói (KT-03). Ghép nối
(`601b2f9`): chặn macro sửa khối có sẵn chuyển lên cùng chỗ với chặn ngoài gói (trước khi hỏi chọn điểm, thẻ lỗi như
KT-04); chú thích `EffectivePlan` về đúng chỗ; I5 ghi rõ cần dòng Full từ hôm trước theo UTC (sót nhỏ đã chấp nhận của
KT-05); checklist tổng thêm I5b, I5c, J16, K22–K24, L24–L28, sửa T2 theo KT-04 và X3 theo KT-15.

**Test chứng minh lỗi của nhánh kiểm tra** (6 test, đều đã là `[Fact]` / `it` thường, không còn skipped / todo, xanh):
`PlanTests.KT01_status_file_written_by_installer_from_three_line_offline_paste_is_accepted`,
`crossplan.test.ts` "KT-09 … a plain "ok" in chat is not the confirmation card", `ExplodeConfirmTests.KT10_macro_recorder_drops_user_confirmed`,
`FloorPlanTests.KT11_door_at_room_corner_goes_into_its_own_wall`, `Solid2Tests.KT13_edge_side_filter_does_not_crash_on_edge_without_points`,
`Solid2Tests.KT13_face_side_filter_does_not_crash_on_face_without_points`. Thân test giữ nguyên câu kiểm của nhánh kiểm
tra; KT-11 và KT-13 có thêm câu kiểm.

| Mã | Mức | Thread sửa | Commit | Sửa | Test / thử tay |
| --- | --- | --- | --- | --- | --- |
| KT-01 | nặng | Test tổng thể | `8e163b3` | Bộ cài tách mã không cần mạng (3 dòng CCL1 / CCS1 / CCF1) theo khoảng trắng và xuống dòng, lưu `license.txt` / `status.txt` / `plan.txt` riêng, chỉ lưu dòng của đúng máy; ô key nhiều dòng. Plugin đọc khoan dung file bộ cài cũ đã lưu sai (`LicenseLogic.PickLine`) | `PlanTests.KT01_*` (chép từ nhánh kiểm tra), `EditionTests.KT01_*`; M1 |
| KT-02 | vừa | Gói dịch vụ | `457bc98` | Chốt C3 (chặn): gói không có 2D từ chối `attributes` của `define_block` / `insert_block` (`PlanLogic.BlockedParam`, cả trong `batch_execute`); `set_block_attributes` sang nhóm 2D; trợ lý gói 3D không thấy `attributes` | `PlanTests.KT02_*`, `plan-fixes.test.ts`; I5b |
| KT-03 | vừa | Gói dịch vụ | `457bc98` | Ghi chú "−" / "+" skill tính trên skill gói thật sự nạp (`skillsForPlan`) | `plan-fixes.test.ts` |
| KT-04 | nhẹ | Gói dịch vụ | `457bc98` | Kiểm cả macro trước khi chạy (`MacroLogic.BlockedCalls`): có lệnh / thuộc tính ngoài gói thì không chạy lệnh nào, thẻ lỗi tiếng Việt | `PlanTests.KT04_*`; I3, T2 |
| KT-05 | nhẹ | Gói dịch vụ | `457bc98` | Chốt C4: chỉ nhận dòng gói mới hơn hoặc cùng ngày dòng đang lưu (`LicenseLogic.AcceptPlan`); tài liệu ghi máy offline không thu hồi được gói nếu khách không dán dòng mới. Sót đã chấp nhận: cùng ngày UTC, `plan.txt` chỉ đọc giữ dòng Full tới hết ngày | `PlanTests.KT05_*`; I5c, I5 |
| KT-06 | nhẹ | Gói dịch vụ | `457bc98` | Apps Script: tên giữ chỗ "(…)" chỉ điền ô Thiết bị trống, không ghi đè tên máy | `activation.test.ts` (KT-06) |
| KT-07 | vừa | Test tổng thể | `8e163b3` | Thay bản gốc có bản cập nhật đang chờ: `PrepareToInstall` (trước khi bộ cài đóng AutoCAD) bỏ `updates\stage` và `apply-update.cmd`, bản gốc không chép đè lại sau khi cài | `EditionTests` (nguồn `.iss`); M2 |
| KT-08 | nhẹ | Test tổng thể | `8e163b3` | Kích hoạt có mạng trong bộ cài lưu cả dòng gói (chỉ khi đúng máy) | `EditionTests` (nguồn `.iss`) |
| KT-09 | vừa | Test tổng thể | `8e163b3` | Chốt C2: sửa / xóa / phá khối 3D có trước lượt chỉ mở bằng nút Đồng ý của thẻ, đúng các handle thẻ liệt kê (`confirm_id`; `user_confirmed` mang danh sách handle); gõ "ok" hay nút "Làm" không tính; luật 2D giữ nguyên | `crossplan.test.ts` KT-09 (chép từ nhánh kiểm tra), `session.test.ts`, `solids*.test.ts`; X5, M3 |
| KT-10 | vừa | Test tổng thể | `8e163b3`, `601b2f9` | Macro không lưu `user_confirmed`; macro sửa khối 3D mà nó không tự dựng thì không chạy (`MacroLogic.ExternalSolidEdits`, thẻ lỗi, kiểm trước khi chọn điểm) | `ExplodeConfirmTests.KT10_*` (một chép từ nhánh kiểm tra); M4 |
| KT-11 | nặng | Vẽ 3D đợt 3 | `fddffdb` | Khe ở đầu tường (`FindEndGaps`): cửa sát góc phòng thành cửa của tường có khe; ký hiệu chỉ chạm mặt tường liền thì bỏ qua kèm lý do, không đoán | `FloorPlanTests.KT11_*` (một chép từ nhánh kiểm tra); L25 |
| KT-12 | vừa | Test tổng thể | `8e163b3` | Chốt C1: `erase_entities` khối 3D có trước lượt luôn qua thẻ, plugin tự kiểm như `explode_entities` (đóng lỗ sao chép → trừ → xóa); sửa L4 | `ExplodeConfirmTests`, `crossplan.test.ts`; L4, M3 |
| KT-13 | nhẹ | Vẽ 3D đợt 2 | `9d301c9` | Cạnh / mặt Brep không đọc được điểm không còn `NullReferenceException`, chỉ không khớp bộ lọc | `Solid2Tests.KT13_*` (chép từ nhánh kiểm tra) |
| KT-14 | nặng | Vẽ 3D đợt 3 | `fddffdb` | `FloorPlanLogic.CheckRerun`: chỉ chặn khi khối cũ chồng thật cùng cao độ; tầng dưới chỉ chạm không chặn; câu lỗi không gợi ý xóa tầng khác; skill / prompt: tường = cao tầng − dày sàn | `FloorPlanTests.KT14_*`, `plan3d.test.ts`; L24 |
| KT-15 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | `ToolDef.ReadsWhen`: `dry_run` chạy như lệnh đọc (không bản sao phiên bản, không nhóm U, không ghi macro, chạy được khi "Chỉ xem") | `KT15_dry_run_is_dispatched_as_a_read`; L26, X3 |
| KT-16 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | `base_z` nhập mm, đổi theo đơn vị bản vẽ như chiều cao | `KT16_*`; L27 |
| KT-17 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Cặp nét ngắn hơn chiều dày chỉ nhận khi nét dài nhô ra mỗi đầu tối đa bằng chiều dày | `KT17_*` |
| KT-18 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Khe đầu tường tìm cả tường thẳng hàng khác dày; lanh tô theo tường mỏng hơn | `KT18_*` |
| KT-19 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | `openings` không có `width` lấy bề rộng cửa đã nhận | `KT19_*` |
| KT-20 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Block `CUA-DI-*` / `CUA-SO-*` xét theo tên trước layer (cả trên `A-WALL`) | `KT20_*`; L28 |
| KT-21 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Thêm layer `A-SLAB` (đường bao sàn kín) vào bảng layer `ve-kien-truc`; skill 3D ghi rõ | test skill có sẵn |
| KT-22 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Đường bao sàn lồng nhau theo độ lồng: đảo trong lỗ là sàn riêng | `KT22_*`; L28 |
| KT-23 | nhẹ | Vẽ 3D đợt 3 | `fddffdb` | Kiểm dựng lại: chồng thật cả hai chiều, so từng hộp tường mới | `KT23_*` |
| KT-24 | vừa | Vẽ 3D đợt 2 | `9d301c9` | Va chạm không đo được thể tích vẫn là va chạm (`volume: null`, `unmeasured_clashes`); chỉ thể tích đo được bằng 0 mới là "chạm" | `Solid2Tests`; K22 |
| KT-25 | vừa | Vẽ 3D đợt 2 | `9d301c9` | Lọc hộp bao trước, tối đa 2000 cặp mỗi lần (`truncated`), chỉ đo thể tích 200 va chạm đầu | `Solid2Tests`, trợ lý so khớp giới hạn. Lượt 2: phần "chỉ đo 200 cặp" sửa tiếp ở KT-40 |
| KT-26 | nhẹ | Vẽ 3D đợt 2 | `9d301c9` | `set_ucs` `plan_view`: tính `ViewTwist` để trục X của UCS nằm ngang, chỉ sang phải | `Solid2Tests`; K23 |
| KT-27 | nhẹ | Vẽ 3D đợt 2 | `9d301c9` | `topology_id` không chứa vị trí tuyệt đối: dời / xoay cả khối không đổi id. Hệ quả đã chấp nhận: macro ghi trước bản sửa báo "solid has changed" ở lệnh theo số cạnh (không bo nhầm), ghi lại macro là xong | `Solid2Tests`; K24 |
| KT-28 | nhẹ | Vẽ 3D đợt 1 | `bf38ab3` | `solid_intersect`: kiểm rỗng sau mỗi lần giao, báo "do not overlap", bản vẽ không đổi | `SolidReviewFixTests`; J16 |
| KT-29 | nhẹ | Vẽ 3D đợt 1 | `bf38ab3` | `set_view_3d` với handle không tồn tại → `ENTITY_NOT_FOUND` | `SolidReviewFixTests`; J16 |
| KT-30 | nhẹ | Vẽ 3D đợt 1 | `bf38ab3` | Giữ cách chọn layer (theo `layer` truyền vào, không có thì layer hiện hành), sửa chú thích; kiểm trục revolve theo điểm dọc đường (cả cung, đường tròn) trên mặt phẳng của biên dạng | `SolidReviewFixTests`, `SolidTests`; J16 |
| KT-31 | vừa | Test tổng thể | `8e163b3` | `docs/dung-may-chu-kich-hoat-full.md`: viết lại lưu ý (plugin đã đọc `CCF1` và chặn theo gói, phải chọn gói). Lượt 2: câu "thẻ 🔒 nhận cả ba dòng" làm cho đúng ở KT-41 | tài liệu |
| KT-32 | nhẹ | Test tổng thể | `8e163b3` | Thời gian / số mục / số lệnh trong checklist và kế hoạch cho khớp (T5, X4 ở đầu phần; phần L gói Full; 90 lệnh) | tài liệu |
| KT-33 | nhẹ | Test tổng thể | `8e163b3`, `601b2f9` | I5: `plan.txt` chỉ đọc có dòng Full cũ, đổi ô sang `3D`, đúng là về 2D, không bao giờ Full; sau gộp ghi rõ dòng Full từ hôm trước theo UTC | I5 |
| KT-34 | nhẹ | Test tổng thể | `8e163b3` | System prompt (mục 6 + luật 3D): sửa / xóa / phá khối 3D có trước lượt cần nút Đồng ý của thẻ, gọi `request_confirmation` ngay | `crossplan.test.ts` |
| KT-35 | nhẹ | Gói dịch vụ | `457bc98` | Gói 2D bỏ tham số 3D (`view`, `direction`, `visual_style`) khỏi `capture_view` | `plan-fixes.test.ts` |
| KT-36 | nhẹ | Gói dịch vụ | `457bc98` | `plan: 2d # ghi chú` nhận đúng; `propose_skill` ghi `plan` theo gói đang dùng | `plan-fixes.test.ts` |
| KT-37 | nhẹ | Test tổng thể | `8e163b3` | `fillet_entities`, `chamfer_entities`, `define_block` (thay bằng block), `delete_layout` tính vào luật > 20 đối tượng | `crossplan.test.ts` |
| KT-38 | nhẹ | Test tổng thể | `8e163b3` | `[InstallDelete]` `update-config.json` khi thay bản gốc | `EditionTests` (nguồn `.iss`) |
| KT-39 | nhẹ | Test tổng thể | `8e163b3` | `.gitignore` chặn `*.gs` (giữ `*.gs.template`); `make-apps-script.mjs` từ chối `--out` trong repo; README đổi ví dụ ra ngoài repo | `crossplan.test.ts` (KT-39) |

**Chưa sửa:** không có. Đã chấp nhận, ghi ở bàn giao thread sửa: KT-05 (cùng ngày UTC, `plan.txt` chỉ đọc giữ Full tới hết
ngày), KT-27 (macro cũ dùng `topology_id` công thức cũ phải ghi lại), KT-30 (giữ cách chọn layer, chỉ sửa chú thích),
KT-42 (vòng 2, dưới).

### Vòng 2: kiểm tra độc lập lượt 2

Lượt 2 (`full/kiem-tra-doc-lap` @ `ad3d0a4`, soát trên `efd5792`): 35 / 39 sửa đúng, 3 chấp nhận (KT-05, KT-27, KT-30 phần
layer), KT-25 sửa chưa đủ (thành KT-40); thêm 2 lỗi mới nhẹ (KT-41, KT-42). Thread này sửa KT-41 rồi gộp 2 nhánh bằng merge
commit: `full/goi-dich-vu` @ `d6d8110` → `69e7109`, `full/ve-3d-dot-2` @ `2f5aa58` → `21c54d9`. Không xung đột; checklist
tổng thêm K25.

| Mã | Mức | Thread sửa | Commit | Sửa | Test / thử tay |
| --- | --- | --- | --- | --- | --- |
| KT-40 | nhẹ | Vẽ 3D đợt 2 | `2f5aa58` | Cặp `CheckInterference` đánh dấu sau giới hạn đo 200 cặp là **"chưa xác nhận"** (`Solid2Logic.InterferenceKind`; `unconfirmed_count` / `unconfirmed_pairs` / `unconfirmed_note`): không tính vào `clashes` / `interfere`, cũng không coi là an toàn | `Solid2Tests` (KT-40); K25 |
| KT-41 | nhẹ | Test tổng thể | `2ddbbc1` | Ô key thẻ Kích hoạt nhận nhiều dòng (Enter vẫn là Kích hoạt); thẻ 🔒 có ô **Dùng mã** (dòng CCS1 / CCF1 hoặc cả 3 dòng, đi cùng đường `Activation.ActivateAsync`, dùng được thì mở khóa ngay); ⚙ ▸ Tài khoản AI có **Kiểm tra lại** và **Dán mã của chủ plugin** cho máy đã kích hoạt (máy đang dùng không có thẻ 🔒 nên trước đó không có chỗ dán dòng gói mới, tài liệu lại ghi là có); sửa tài liệu chỗ dán mã | UI test `KT41_code_boxes_take_every_line_of_the_offline_email` (CI Windows; thử dán thật khi máy CI có clipboard), `EditionTests.KT41_*`; M10–M12 |
| KT-42 | nhẹ | Gói dịch vụ | `074abe1`, `d6d8110` | **Chấp nhận** (luồng chính chốt thay chủ dự án, chủ dự án xem lại): gói 3D chèn block có sẵn vẫn hiện chữ mặc định của block; Claude không đặt / sửa được chữ thuộc tính | `PlanTests.KT42_*`, `plan-fixes.test.ts` |

Test sau gộp vòng 2: lõi 367/367, trợ lý 215/215, build net48 / net8.0-windows / net10.0-windows và project UI test 0 lỗi,
0 cảnh báo. CI run `43267347630` (1 lượt): **xanh**, cả UI test WPF của KT-41. Test chỉ dán thật qua clipboard khi máy CI
có clipboard; log không ghi lượt này có dán thật không, nên M10–M12 vẫn là phần kiểm thật.

**Test sau gộp:** lõi 364/364, trợ lý 214/214, build net48 / net8.0-windows / net10.0-windows (xóa `obj` / `bin` giữa các
lần) 0 lỗi, 0 cảnh báo. CI run `43259984843` (1 lượt, workflow_dispatch `release: "false"`): **xanh**, mọi bước gồm UI
test WPF và `Setup.exe`; bước phát hành bỏ qua.

### Vòng 3: kiểm tra độc lập lượt 3

Lượt 3 xác nhận KT-40, KT-41 sửa đúng, KT-42 chấp nhận; thêm 1 lỗi nhẹ KT-43. Gộp `full/goi-dich-vu` @ `a8f0430` (đã gộp
`813ab45`) → `fc7654d`, không xung đột. Test `EditionTests.KT01` (thread gói sửa kỳ vọng theo cách lưu mới của bộ cài):
vẫn kiểm đúng ý, và thêm câu kiểm thư mục `{localappdata}\ClaudeCAD` + tên file khớp chỗ plugin đọc (`status.txt`,
`plan.txt`, `status-new.txt`, `plan-new.txt`). Checklist tổng thêm I5d.

| Mã | Mức | Thread sửa | Commit | Sửa | Test / thử tay |
| --- | --- | --- | --- | --- | --- |
| KT-43 | nhẹ | Gói dịch vụ | `a8f0430` | Chặn như C4 (luồng chính chốt thay chủ dự án, chủ dự án xem lại): `LicenseLogic.StatusRefusal`: dòng `CCS1` cũ hơn dòng đang lưu bị từ chối; dòng dán tay cùng ngày không đè `disabled` (mã miễn kiểm tra cũ không mở lại máy đã khóa); trả lời của máy chủ chỉ cần không cũ hơn. Áp ở mọi đường dán (thẻ Kích hoạt, "Dùng mã" trên 🔒, ⚙ ▸ Dán mã), kích hoạt bằng key, kiểm tra trạng thái. Bộ cài không kiểm được chữ ký nên khi đã có `status.txt` / `plan.txt` thì ghi `status-new.txt` / `plan-new.txt`; plugin áp theo cùng luật rồi xóa | `PlanTests.KT43_*` (2), `EditionTests.KT01_*`; I5d |

### Tổng cả 3 lượt kiểm tra độc lập

**43 mã** (3 nặng, 9 vừa, 31 nhẹ): **39 đã sửa** (gồm KT-25, sửa đủ nhờ KT-40), **4 chấp nhận** (KT-05, KT-27, KT-30 phần
layer, KT-42), **0 chưa sửa**. KT-43 sửa sau lượt 3, chưa có lượt kiểm tra độc lập soát lại. Ba lỗi nặng (KT-01 dán mã
offline ở bộ cài, KT-11 cửa sát góc, KT-14 dựng tầng 2) đã khỏi, có test tự động.

Test sau gộp vòng 3: lõi 369/369, trợ lý 215/215, build net48 / net8.0-windows / net10.0-windows và project UI test 0 lỗi,
0 cảnh báo. CI: xem Tiến độ (lượt này cần để biên dịch phần `.iss` mới của KT-43).

## Số token theo gói

Đo bằng script: dựng danh sách lệnh của từng gói đúng như plugin gửi (`plan_tools`), đổi schema sang JSON như SDK gửi
cho Claude, cộng system prompt. Không có bộ đếm token chính xác trên máy cloud; ước theo ~3,5 ký tự / token (khoảng
3–4). "Trước" = `f4e81b0`, "sau" = commit sửa ở trên.

| Gói | Số lệnh (kể cả 5 lệnh thẻ) | Tổng trước (ký tự) | ≈ token trước | Tổng sau (ký tự) | ≈ token sau | Giảm |
| --- | --- | --- | --- | --- | --- | --- |
| 2D | 95 | 126 300 | ~36,1k | 126 463 | ~36,1k | +0,05k (câu luật phá khối 3D trong mô tả `explode_entities`) |
| 3D | 90 | 139 901 | ~40,0k | 129 911 | ~37,1k | −2,9k (−7 %) |
| Full | 124 | 189 717 | ~54,2k | 179 890 | ~51,4k | −2,8k (−5 %) |
| Riêng phần 3D (29 lệnh + luật 3D) | 29 | 63 742 | ~18,2k | 53 752 | ~15,4k | −2,9k (−16 %) |

- Chi tiết phần 3D: mô tả lệnh 58 290 → 48 128 ký tự; luật 3D 5 452 → 5 624 (thêm dòng `set_entity_props`).
  Đề xuất 1 bớt 8 410 ký tự (≈ 2,4k token, đúng ước tính); đề xuất 3 chỉ bớt ~1 750 ký tự (≈ 0,5k token, ước tính trước
  2–3k): phần lớn mô tả còn lại là ý nghĩa và ràng buộc tham số, phải giữ; quy trình dài vốn đã nằm trong luật 3D.
- Vẫn gấp ~2,5 lần ước tính 5–6k của kế hoạch. Phần này nằm trong prefix được cache: từ lượt thứ hai trong 5 phút trả giá
  đọc cache (~10 %); lượt đầu mỗi phiên trả đủ. Muốn giảm thêm thì còn đề xuất 2 và 4 (chưa làm).
- Nặng nhất sau khi rút: `build_3d_from_plan` 6,6k ký tự (24 tham số), `section_view` 3,2k, `sweep` 2,7k, `loft` 2,2k,
  `revolve` 2,2k, `chamfer_edges` 2,1k.
- Sau khi gộp sửa kiểm tra độc lập (`601b2f9`, cùng cách đo, `capture_view` theo gói như plugin gửi): 2D 95 lệnh
  126 327 ký tự (~36,1k, gần như không đổi: bớt tham số 3D của `capture_view`, thêm câu thẻ ở `erase_entities`); 3D **89**
  lệnh (`set_block_attributes` sang nhóm 2D) 130 320 ký tự (~37,2k, +0,1k: mô tả nhiều tầng, chạy thử, luật thẻ); Full
  124 lệnh 181 470 ký tự (~51,8k, +0,45k).

## Còn lưu ý (không phải lỗi)

- ~~Mô tả `capture_view` ở gói 2D vẫn kể tùy chọn góc nhìn / kiểu hiển thị 3D~~: đã sửa (KT-35).
- ~~Bộ cài lưu dòng gói / trạng thái của mã không cần mạng mà không so ngày~~: từ KT-43, khi máy đã có `status.txt` /
  `plan.txt` bộ cài ghi `*-new.txt`, plugin áp theo cùng luật "không cũ hơn". Còn lại đúng giới hạn của chốt C4: khách tự
  xóa các file đó rồi dán dòng cũ thì máy offline vẫn không thu hồi được gói; chỉ cấp Full offline cho máy tin cậy.

## Còn chỉ thấy trên AutoCAD thật

Như các bàn giao trước: `GenerateSectionGeometry` (L9, L11), cung / spline sau `Explode` (L1, L9, L10), hướng nêm (J10),
dấu góc vát (J11), dấu `ShellBody` (K17), thứ tự ma trận `align` (K21), kiểu hiển thị trên AutoCAD không phải tiếng Anh
(J14). Thêm: lịch sử cuộc chat có lệnh 3D khi máy đổi về gói 2D (T4). Sau kiểm tra độc lập: hộp thoại và ô key nhiều
dòng của bộ cài (M1, M2, I6; CI chỉ kiểm biên dịch), va chạm chung mặt phẳng (K22), `ViewTwist` (K23), `topology_id`
sau khi dời khối (K24), nhiều tầng và cửa sát góc trên bản vẽ thật (L24–L28). Vòng 2: dán thật 3 dòng ở thẻ Kích hoạt,
thẻ 🔒 và ⚙ (M10–M12; UI test chỉ dán được khi máy CI có clipboard), nhiều tường chỉ chạm nhau (K25).

## Việc còn lại trước khi phát hành (không làm, chỉ liệt kê)

1. **Máy chủ kích hoạt riêng:** chủ dự án dựng theo `docs/dung-may-chu-kich-hoat-full.md`, gửi URL + 2 dòng khóa công
   khai; một thread gắn vào `activation/activation.json` và `License.cs` (`PublicModulus`).
2. **Số phiên bản** của bản Full (plugin và trợ lý vẫn ghi 0.9.4 như bản gốc).
3. **Bộ cài thử** build sau bước 1, chủ dự án thử theo `docs/checklist-tong-the.md`.
4. **Secret CI** của repo Full (chủ dự án tự cài); hiện bước phát hành của CI bị chặn.
5. **Merge `main`**: cần chủ dự án đồng ý rõ ràng (merge `main` có phiên bản mới là CI tự phát hành).
6. **Các chỗ trỏ về dự án gốc** trong `docs/cho-tro-ve-repo-goc.md`: nguồn cập nhật / Releases, số Zalo liên hệ, link
   tải trong `README.md` / `bo-cai/README.md`. (`AppId` và thư mục bộ cài: đã chốt 12/11/2026, bản Full thay bản gốc.)
7. Đề xuất giảm token 2 và 4 (tùy chọn, chưa chốt).

## Tiến độ

- [x] Bước 0: gộp tài liệu luồng chính (không xung đột)
- [x] Rà soát + sửa lỗi + test
- [x] Tài liệu: file này, `docs/checklist-tong-the.md`, checklist thử thật, kế hoạch, `bo-cai/README.md`
- [x] CI Windows (1 lượt, xanh: run 43230375890)
- [x] 3 điểm chủ dự án chốt 12/11/2026: phá khối 3D qua thẻ, skill 2D không nạp ở gói 3D, giảm token (commit `41e4b37`)
- [x] CI Windows sau 3 điểm (1 lượt, xanh: run 43236499463)
- [x] Thay bản gốc như nâng cấp (chốt 12/11/2026): tên hiển thị, `edition.json`, bộ cài nhận ra bản gốc, skill mẫu cũ (commit `b6ad539`)
- [x] CI Windows sau phần thay bản gốc (1 lượt, xanh: run 43243835815)
- [x] Hộp thoại cảnh báo bản gốc: hai nút "Tiếp tục" / "Hủy" (`TaskDialogMsgBox`, chốt 12/11/2026)
- [x] Sửa 13 lỗi kiểm tra độc lập phần bộ cài / tài liệu / tích hợp, chốt C1, C2 (commit `8e163b3`)
- [x] Gộp 4 nhánh sửa (`f42fb3f`, `54201f9`, `61cc03d`, `3527374`) + ghép nối (`601b2f9`); lõi 364/364, trợ lý 214/214, build 3 bản
- [x] Lượt CI chung sau khi gộp (1 lượt, xanh: run 43259984843 trên `a62a7b7`, ~10 phút; bước `Setup.exe` biên dịch được `.iss` mới: `TaskDialogMsgBox` hai nút "Tiếp tục" / "Hủy", ô key nhiều dòng, `PrepareToInstall`, `[InstallDelete]`; bước phát hành bỏ qua)
- [x] Vòng 2 (kiểm tra độc lập lượt 2): KT-41 (`2ddbbc1`), gộp KT-42 (`69e7109`) và KT-40 (`21c54d9`); lõi 367/367, trợ lý 215/215, build 3 bản
- [x] CI chung vòng 2 (1 lượt, xanh: run 43267347630 trên `e8b4ede`, ~10 phút; UI test WPF gồm `KT41_code_boxes_take_every_line_of_the_offline_email` xanh, `Setup.exe` xanh; bước phát hành bỏ qua)
- [x] Vòng 3: gộp KT-43 (`fc7654d`), test `EditionTests.KT01` kiểm thêm thư mục / tên file; lõi 369/369, trợ lý 215/215, build 3 bản
- [ ] CI chung vòng 3 (biên dịch `.iss` mới của KT-43)
