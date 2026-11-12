# Bàn giao: Kiểm tra độc lập bản Full (lượt 1)

*12/11/2026 · thread "Kiểm tra độc lập" · nhánh `full/kiem-tra-doc-lap` (tách từ `full/test-tong-the` @ `9ef9db9`).
Chỉ ghi lỗi, **không sửa code sản phẩm**. Tài liệu `docs/ban-giao/*` chỉ đọc sau khi rà xong để đối chiếu; không ghi lại
các điểm đã chốt / đang làm / chưa làm theo chủ ý.*

## Kết quả chạy

| Việc | Kết quả |
| --- | --- |
| `dotnet test plugin/ClaudeCAD.Core.Tests` (trước khi thêm test) | 331/331 xanh |
| `cd agent && npm ci && npm test` (trước khi thêm test) | 201/201 xanh |
| Build plugin net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, SDK 8 + 10) | cả 3 xanh, 0 cảnh báo |
| Test mới chứng minh lỗi | lõi **5 đỏ** (`KiemTraDocLapTests.cs`: KT-01, KT-10, KT-11, KT-13 ×2), trợ lý **1 đỏ** (`kiemtradoclap.test.ts`: KT-09). Đỏ có chủ ý |
| CI run `43249502517` (workflow_dispatch, `release: "false"`) | xem cuối file |

**Về test đỏ trên CI:** workflow dừng ở bước đỏ đầu tiên (`npm test` đứng trước test lõi và UI test). Để lượt CI duy nhất
vẫn chạy UI test và build Windows, 6 test mới là *skipped* (xunit, `[KtRedFact]`) / *todo* (node) **chỉ khi**
`GITHUB_ACTIONS=true`. Chạy trên máy dev / Linux thì đỏ. Thread sửa lỗi: sửa xong đổi `[KtRedFact]` về `[Fact]`, bỏ `red`
trong test node.

## Tổng hợp

| Phần phụ trách | Nặng | Vừa | Nhẹ | Mã |
| --- | --- | --- | --- | --- |
| gói dịch vụ | 0 | 2 | 5 | KT-02, 03 · 04, 05, 06, 35, 36 |
| đợt 1 | 0 | 0 | 3 | KT-28, 29, 30 |
| đợt 2 | 0 | 2 | 3 | KT-24, 25 · 13, 26, 27 |
| đợt 3 | 2 | 0 | 9 | KT-11, 14 · 15–23 |
| bộ cài-tài liệu-tích hợp | 1 | 5 | 7 | KT-01 · 07, 09, 10, 12, 31 · 08, 32, 33, 34, 37, 38, 39 |
| **Cộng (39)** | **3** | **9** | **27** | |

Đã kiểm và thấy đúng (không phải lỗi): chặn gói chạy ở plugin (`ToolDispatcher.RequireInPlan`) cho lệnh lẻ, từng lệnh
trong `batch_execute`, macro; sửa file trợ lý / skill / pipe không mở được lệnh ngoài gói; 119 lệnh đều có nhãn, 29 lệnh
`ThreeD`; chữ ký CCL1 / CCS1 / CCF1 không dùng chéo được, sai / trống gói → 2D; Apps Script lọc công thức độc; cập nhật luôn
trỏ repo Full khi build bằng CI; 119 lệnh trợ lý khớp plugin (tên, tham số, giới hạn, enum); `user_confirmed` không giả được
qua tham số / batch; số trong tự kiểm tra và checklist (0,404602; 0,474850; 8,022; J2 2,488; K17 0,040384; L2 14,08…) tính
lại đều đúng; API AutoCAD .NET (đối chiếu `AcDbMgd.xml` 24.1 / 25.0) đúng chữ ký và ý nghĩa tham số; không lộ bí mật
trong repo (không có `*.pem`, `Code.gs` đã điền, key thật); 2D không hỏng so với `main` và `luu-tru/2d-base`.

## Lỗi

### Nặng

**KT-01 · nặng · bộ cài-tài liệu-tích hợp · `installer/ClaudeCAD.iss:589-600` (`Activate`)**
- Tái hiện: máy không mạng; chủ plugin chạy `taoKeyThuCong`, email có **3 dòng** CCL1 / CCS1 / CCF1
  (`Code.gs.template:376`); khách dán cả 3 dòng vào ô key của bộ cài.
- Sai: bộ cài lưu mọi thứ từ `CCS1|` tới hết vào `status.txt` → file thành `CCS1…\r\nCCF1…`. Plugin đọc cả file là một
  dòng (`LicenseLogic.VerifyStatus`, tách `|` ra 9 phần) → "không đúng định dạng"; dòng gói không được lưu. Máy có giấy phép
  nhưng trạng thái hỏng → thẻ 🔒 chỉ có "Kiểm tra lại" (cần mạng): máy offline kẹt. Nếu ô key một dòng của Windows chỉ giữ
  dòng đầu khi dán, cũng chỉ còn CCL1 → kẹt như trên.
- Sửa: bộ cài tách theo khoảng trắng như `Activation.ActivateAsync` (lưu từng dòng vào license / status / plan.txt); plugin
  đọc file khoan dung (lấy token đúng tiền tố); cân nhắc ô dán mã trên thẻ 🔒.
- Test: `KT01_status_file_written_by_installer_from_three_line_offline_paste_is_accepted` (đỏ).

**KT-11 · nặng · đợt 3 · `plugin/ClaudeCAD.Plugin/Core/FloorPlan.cs:737-806` (AssignSymbols bước 2)**
- Tái hiện: phòng 4×6 m tường 220; cửa đi ở tường phải, sát góc (khe y = 110…1010, mép khe trùng mặt trong tường dưới) —
  đúng kiểu "bản lề đặt sát góc phòng" của `ve-kien-truc`. `build_3d_from_plan`.
- Sai: tường phải thành một đoạn từ y = 1010 (không có "khe" để khớp); ký hiệu cửa chạm mặt tường dưới (`pov = 0` vẫn tính)
  → **khoét cửa 900 vào tường dưới** (s 3100–4000), lỗ thật ở tường phải để trống hết chiều cao, không lanh tô; không báo
  `open_gaps` / `skipped`; sửa bằng `openings` tại đúng chỗ cửa thì báo "no recognised wall".
- Sửa: nhận "khe ở đầu tường" (đầu tường có nét bịt, cách mặt tường khác MinOpening–MaxOpening); bước 2 chỉ nhận ký hiệu
  nằm trong dải tường (pov > JoinTol), không thì ưu tiên tường có đầu / nét bịt cạnh ký hiệu, hoặc bỏ qua kèm lý do.
- Test: `KT11_door_at_room_corner_goes_into_its_own_wall` (đỏ).

**KT-14 · nặng · đợt 3 · `Drawing/PlanTo3dTools.cs:367` + `:547`; `skills/ve-3d-kien-truc/SKILL.md:51,91`**
- Tái hiện: làm theo skill: tầng 1 `base_z` 0, `wall_height` 3300; tầng 2 cùng mặt bằng `base_z` 3300, sàn 120.
- Sai: vùng kiểm "đã dựng" bắt đầu `baseZ − slab` = 3180; tường tầng 1 lên tới 3300 → tầng 2 bị chặn "already built". Skill
  bước 5 bảo Claude hiểu lỗi này là "tầng đã dựng" và **hỏi xóa khối cũ** → dễ dẫn tới xóa tầng 1. Ngoài ra skill cho
  `wall_height` = cao sàn-đến-sàn nên sàn tầng 2 (3180–3300) chồng 120 mm vào tường tầng 1.
- Sửa: chỉ coi là dựng trùng khi phần chồng z lớn (vd. > 50 % chiều cao tường mới), hoặc kiểm tường và sàn riêng; câu lỗi
  phân biệt "chạm tầng dưới" với "chạy lại"; skill: `wall_height` = cao tầng − dày sàn (hoặc sàn đặt trên đỉnh tường).
- Test: phần kiểm z nằm ở code AutoCAD; đề xuất tách `FloorPlanLogic.IsRerun(z0, z1, baseZ, wallH, slabT)` vào lõi rồi test
  ca tầng 2. Kiểm thật: thêm vào checklist (xem dưới).

### Vừa

**KT-02 · vừa · gói dịch vụ · `Core/Plans.cs:79`, `Drawing/BlockTools.cs:39,82-99`**
- Tái hiện: máy gói 3D: "tạo block có thuộc tính TEN = 'Phòng khách' cao 300, chèn vào".
- Sai: `define_block` / `insert_block` / `set_block_attributes` thuộc nhóm Chung; `define_block` tạo `AttributeDefinition`
  với chữ, chiều cao, kiểu chữ tùy ý → gói 3D ghi được chữ, trái phương án A (không chữ). Chỉ system prompt ngăn.
- Sửa: từ chối `attributes` của `define_block` và `set_block_attributes` khi gói không có 2D (hoặc chuyển phần đó sang
  TwoD). Xem thêm mục chốt C3.

**KT-03 · vừa · gói dịch vụ · `agent/src/agent/session.ts:266-273`, `agent/src/agent/content.ts:41`**
- Tái hiện: máy gói 2D, bấm **+** chọn `ve-3d-kien-truc`; hoặc đổi Full → 3D giữa cuộc chat.
- Sai: `skills_added` / `skills_removed` dựng từ danh sách khung chat, không qua `skillsForPlan` → Claude được báo "đã thêm
  skill X, nạp bằng Skill tool" cho skill không được nạp ở gói đó (thử gọi, lỗi); đổi gói làm rơi skill 2D mà không có dòng
  "removed", Claude trong cuộc chat cũ có thể vẫn theo quy ước đã đọc. Trái `skills/README.md:66`.
- Sửa: lọc cả hai danh sách qua `skillsForPlan`, thêm skill bị gói loại vào `skills_removed`.

**KT-07 · vừa · bộ cài-tài liệu-tích hợp · `installer/ClaudeCAD.iss:443-455, 749`; `Sidecar/UpdateService.cs:186-193`**
- Tái hiện: máy bản gốc 0.9.x chạy `CLAUDECHATUPDATE` (bản cập nhật tải xong, `apply-update.cmd` đang chờ AutoCAD đóng), để
  AutoCAD mở, chạy bộ cài Full.
- Sai: `CloseApplications=yes` đóng AutoCAD ở trang Preparing, trước `ssInstall`; script chờ của bản gốc chạy `robocopy /E
  /PURGE stage→bundle` đúng lúc bộ cài đang xóa stage và chép file → bundle trộn, hoặc bản gốc đè lại Full, `/PURGE` xóa
  `edition.json`. Độ tin: vừa (phụ thuộc thứ tự Restart Manager).
- Sửa: xóa `updates\stage` và `apply-update.cmd` trong `PrepareToInstall` (trước khi đóng ứng dụng); robocopy thiếu nguồn
  thì lỗi vô hại.

**KT-09 · vừa · bộ cài-tài liệu-tích hợp (cổng xác nhận) · `agent/src/agent/session.ts:485`, `confirmation.ts:203`**
- Tái hiện: lượt trước Claude hỏi một câu bất kỳ ("Tường cao 3 m nhé?"); cậu trả "ok"; trong lượt này Claude cộng / trừ /
  cắt / bo khối có từ lượt trước.
- Sai: mọi dấu "?" trong chữ của Claude đặt `turnAsked`; "ok" lượt sau mở **mọi** lệnh luôn-hỏi của cả lượt, không có thẻ
  xác nhận và không cần câu hỏi nói về khối đó. Nút "Làm" của thẻ kế hoạch cũng tính như "Đồng ý". Chốt 12/11 ghi "luôn qua
  thẻ xác nhận"; bàn giao test tổng thể lại cho câu "đồng ý" trả lời câu hỏi lượt trước → cần chốt (C2).
- Sửa (nếu chọn "chỉ thẻ"): khối 3D có trước lượt chỉ mở bằng nút Đồng ý của `request_confirmation`, và chỉ cho handle đã
  hiện trên thẻ.
- Test: `KT-09` trong `agent/test/kiemtradoclap.test.ts` (đỏ; viết theo lựa chọn "chỉ thẻ", đổi theo chốt).

**KT-10 · vừa · bộ cài-tài liệu-tích hợp (macro) · `Drawing/ToolDispatcher.cs:230,244-246`, `Core/Macros.cs:265-276`**
- Tái hiện: lượt đã đồng ý phá khối 3D cũ (`explode_entities` mang `user_confirmed: true`), "Lưu thành macro", chạy lại
  macro trên bản vẽ có khối cũ khác handle trùng / bản vẽ đó.
- Sai: macro ghi `p.Raw` gồm cả `user_confirmed` → chạy lại phá khối có sẵn không hỏi. Macro cũng không qua cổng của trợ lý,
  nên `solid_union` / `subtract` / `slice` lên handle ngoài macro (`ExternalHandles`) chạy lại không hỏi.
- Sửa: `MacroRecorder.Record` bỏ `SolidLogic.UserConfirmedParam`; với lệnh luôn-hỏi có handle ngoài macro thì từ chối hoặc
  hỏi trước khi chạy.
- Test: `KT10_macro_recorder_drops_user_confirmed` (đỏ).

**KT-12 · vừa · bộ cài-tài liệu-tích hợp (cổng xác nhận) · `agent/src/agent/confirmation.ts` (`ALWAYS_CONFIRM`); `docs/checklist-thu-that.md:211` (L4)**
- Tái hiện: L4 "dựng 3D lại, xóa khối cũ trước" (vài khối tường + sàn, < 20 đối tượng).
- Sai: `erase_entities` lên khối 3D có trước lượt chỉ theo luật > 20 đối tượng → không có thẻ, trong khi L4 ghi "Thẻ xác nhận
  xóa khối cũ". Lách được luật "luôn hỏi": `copy_entities` khối cũ → trừ trên bản sao (bản sao là "đã tạo") → xóa khối gốc
  (1 đối tượng, không hỏi).
- Sửa: tùy chốt C1: thêm `erase_entities` (và `move`/`copy` không cần) vào kiểm ở plugin khi handle là khối 3D (như
  explode, `IsExplodeConfirmEntity`), hoặc sửa kết quả đúng của L4.

**KT-24 · vừa · đợt 2 · `Drawing/Solid2Tools.cs:813-821, 849-853`**
- Sai: `OverlapVolume` nuốt lỗi boolean và trả 0; khi `CheckInterference` = true mà phép giao lỗi (hay gặp khi mặt trùng /
  tiếp xúc), cặp đó vào `touching_pairs`, `interfere: false` → Claude báo "không va chạm" dù có.
- Sửa: trả `double?`; lỗi đo thì vẫn liệt kê là va chạm với `volume: null` + ghi chú; `interfere` = true.
- Kiểm thật: thêm vào checklist (hai khối chồng nhau có chung một mặt phẳng).

**KT-25 · vừa · đợt 2 · `Drawing/Solid2Tools.cs:826-857`**
- Sai: 100 × 100 handle = 10 000 cặp; cặp nào hộp bao chạm (tường giao nhau luôn chạm) đều `CheckInterference` + 2 clone +
  boolean trên luồng chính → AutoCAD đứng lâu. `MaxInterferencePairs` (200) chỉ giới hạn số cặp liệt kê, không giới hạn việc.
- Sửa: giới hạn số cặp qua lọc hộp bao (vd. 2000) và trả `truncated`; ngừng đo thể tích sau 200 cặp.

**KT-31 · vừa · bộ cài-tài liệu-tích hợp · `docs/dung-may-chu-kich-hoat-full.md:91-92`**
- Sai: ghi "Plugin **chưa đọc** dòng `CCF1`… mọi máy vẫn dùng được toàn bộ lệnh". Plugin đã kiểm `CCF1` và chặn theo gói.
  Chủ dự án dựng máy chủ theo file này sẽ hiểu sai (nghĩ chưa cần chọn gói).
- Sửa: bỏ / viết lại đoạn lưu ý.

### Nhẹ

| Mã | Phần | File:dòng | Lỗi → đề xuất |
| --- | --- | --- | --- |
| KT-04 | gói dịch vụ | `ChatController.cs:1451-1455` | Macro ghi ở Full chạy trên 2D / 3D: chạy nửa chừng (lệnh trong gói đã ghi, lệnh ngoài gói lỗi), "Lỗi đầu" hiện câu tiếng Anh dành cho Claude ("Tell the user in Vietnamese…"). → Kiểm `PlanLogic.Allowed` mọi lệnh trước khi chạy, báo tiếng Việt; helper `MacroLogic.BlockedCalls` để test |
| KT-05 | gói dịch vụ | `Core/License.cs:231-238`, `Sidecar/Activation.cs:386-390` | Máy có mã miễn kiểm tra (offline) dán lại dòng `CCF1` Full cũ hơn vẫn được nhận (không so ngày với dòng đang lưu); hạ gói trong ngày + `plan.txt` chỉ đọc giữ Full tới hết ngày UTC. Comment `Code.gs:32` "đổi gói thì chạy lại hàm" làm chủ dự án tưởng hạ được. → `SavePlan` từ chối dòng cũ hơn dòng đang có; ghi rõ trong tài liệu (chốt C4) |
| KT-06 | gói dịch vụ (có từ bản gốc) | `Code.gs.template:138,248` | `markActive(sh, machine, '(kích hoạt bằng key)')` ghi đè tên máy thật ở cột "Thiết bị" cho mọi máy kích hoạt bằng key. → Chỉ ghi tên khi ô trống |
| KT-35 | gói dịch vụ | `agent/src/tools/capture.ts:47-49` | Máy 2D vẫn thấy tham số 3D `view` / `visual_style` của `capture_view` (tốn token, mời góc nhìn 3D). → Bỏ các trường đó khi gói 2D |
| KT-36 | gói dịch vụ | `agent/src/agent/skills.ts` (`skillPlan`) | `plan: 2d # ghi chú` không khớp regex → skill coi là "mọi gói"; `propose_skill` không ghi dòng `plan`, nên skill 2D người dùng tạo vẫn nạp ở gói 3D. → Bỏ phần `#…`; `propose_skill` ghi `plan` theo gói hiện tại |
| KT-08 | bộ cài-tài liệu-tích hợp | `installer/ClaudeCAD.iss:610-627` | Kích hoạt bằng key có mạng trong bộ cài lưu license + status nhưng bỏ `plan` → máy chạy 2D tới lần hỏi trạng thái đầu (vài giây sau khi mở, hoặc khi có mạng lại). → Lưu `JsonValue(Body,'plan')` vào `plan.txt` |
| KT-38 | bộ cài-tài liệu-tích hợp | `bundle/make-bundle.mjs:11`, `installer/ClaudeCAD.iss` (`[InstallDelete]`) | Bộ cài build ngoài CI (không `--update-url`) cài đè bản gốc giữ `update-config.json` của bản gốc → Full nhận bản cập nhật của bản gốc, trình cập nhật (`/PURGE`) đưa bản gốc về. → `[InstallDelete]` file đó khi thay bản gốc, hoặc bắt buộc `--update-url` |
| KT-39 | bộ cài-tài liệu-tích hợp | `.gitignore:20-21`, `activation/README.md:99` | `.gitignore` chỉ chặn `activation/*.gs`; README hướng dẫn `--out ClaudeCAD-Code.gs` (thư mục gốc repo) và `make-apps-script.mjs` mặc định `--out Code.gs` → file có SECRET + khóa riêng không bị ignore, dễ lỡ commit. → Thêm `*.gs` (trừ `*.gs.template`) / `Code.gs` vào `.gitignore`, README hướng ra ngoài repo |
| KT-32 | bộ cài-tài liệu-tích hợp | `docs/checklist-tong-the.md` §1, §6; `checklist-thu-that.md:128-129`; `tong-quan-du-an.md:81`, `ke-hoach…:10` | §1 ghi "~10 phút" + "3 mục rà chéo" nhưng bảng ~20 phút, T1–T5; §6 "~8 phút" vs bảng ~10; đầu phần trong checklist thử thật thiếu T5 / X4; phần L ghi "gói 3D hoặc Full" rồi "Máy ở gói Full"; "89 lệnh" (code: 90 lệnh không phải 3D). → Sửa cho khớp |
| KT-33 | bộ cài-tài liệu-tích hợp | `docs/checklist-thu-that.md` I5 | Đổi ô sang `2D` (cũng là mặc định) thì đúng / sai đều ra 2D → không phân biệt được. → Đặt `plan.txt` chỉ đọc chứa dòng Full cũ, đổi ô sang `3D`; kết quả đúng: về 2D, không bao giờ Full |
| KT-34 | bộ cài-tài liệu-tích hợp | `agent/src/agent/systemPrompt.ts:105-123` | `THREE_D_RULES` không nói phá khối 3D có trước lượt phải hỏi; Claude chỉ biết khi plugin từ chối (thêm một vòng). → Thêm 1 dòng ở nhánh Full |
| KT-37 | bộ cài-tài liệu-tích hợp (có từ bản gốc) | `confirmation.ts` (`DESTRUCTIVE`) | `fillet_entities` / `chamfer_entities` / `define_block` (`replace_with_insert`) / `delete_layout` không tính vào luật > 20 → batch 500 lần bo góc sửa 1000 đường không hỏi. → Thêm vào bảng đếm |
| KT-13 | đợt 2 | `Core/Solids2.cs:191` (`FilterEdges`), `:214` (`FilterFaces`) | Cạnh không đọc được điểm (cạnh kín, `Vertex1` và `Curve` đều lỗi — `ReadEdge` có tính tới ca này) hoặc mặt không có `Center` → `NullReferenceException` = "lỗi nội bộ plugin". → Cạnh / mặt thiếu điểm không khớp bộ lọc mặt bên. Test: `KT13_edge_side_filter…`, `KT13_face_side_filter…` (đỏ) |
| KT-26 | đợt 2 | `Drawing/Solid2Tools.cs:958-962` | `set_ucs` `plan_view` với UCS tùy chọn nghiêng: `ViewTwist = 0` nên trục X màn hình không theo X của UCS. Preset đúng. → Tính ViewTwist từ X UCS chiếu lên mặt nhìn |
| KT-27 | đợt 2 | `Core/Solids2.cs:94` | `topology_id` làm tròn 0,01 mm theo `min`: dời khối lẻ có thể đổi id → báo "đã đổi, gọi lại get_solid_info" (thêm 1 lượt, không sai kết quả). → Làm tròn theo kích thước tương đối, không theo tọa độ tuyệt đối |
| KT-28 | đợt 1 | `Drawing/SolidTools.cs:466-476` | `solid_intersect` ≥ 3 khối: hai khối đầu không giao → lần sau lỗi, câu báo "mặt chỉ chạm… làm khối cắt to hơn" thay vì "các khối không giao nhau". → Kiểm rỗng sau mỗi lần giao |
| KT-29 | đợt 1 | `Drawing/SolidTools.cs:639,646-662` | `set_view_3d` với `handles` không có handle nào tồn tại: trả ok, không phóng; `capture_view` cùng ca thì `ENTITY_NOT_FOUND`. → Báo lỗi khi 0 handle tìm thấy |
| KT-30 | đợt 1 | `Drawing/SolidTools.cs:362`; `Core/Solids.cs:298-314` | Comment nói khối đùn lấy layer / màu biên dạng nhưng code đặt layer hiện hành (cả revolve / sweep) → sửa comment hoặc code. Kiểm trục revolve chỉ xét đỉnh trên mặt XY: cung bulge, đường tròn, biên dạng mặt đứng cắt trục không bắt được (AutoCAD báo lỗi chung) |
| KT-15 | đợt 3 | `Drawing/PlanTo3dTools.cs:20`, `ToolDispatcher.cs:217-224` | `dry_run` là lệnh ghi: lưu bản sao phiên bản (toast "Đã lưu phiên bản…"), mở nhóm hoàn tác ("Hoàn tác lượt này" cho lượt không đổi gì), được ghi vào macro. (Bị chặn ở "Chỉ xem" là đã ghi trong bàn giao đợt 3.) → Dispatcher quyết định ghi theo từng lần gọi (`dry_run` = đọc) |
| KT-16 | đợt 3 | `Drawing/PlanTo3dTools.cs:300,632` | Chiều cao chia theo INSUNITS nhưng `base_z` không; báo cáo lại để `base_z` trong `heights_mm`; skill ghi 3300 (mm). Bản vẽ đơn vị m: tường đúng cao nhưng tầng 2 đặt ở 3300 m. → Thống nhất: đổi `base_z` theo đơn vị, hoặc ghi rõ "đơn vị bản vẽ" trong mô tả lệnh / skill và bỏ khỏi `heights_mm` |
| KT-17 | đợt 3 | `Core/FloorPlan.cs:361-372` | Trụ tường cạnh góc (cửa cách góc 50–200): nét bịt ghép với mặt ngoài tường thành "tường" dày 330 / 420 → `walls_count` 5 thay 4, `thickness_summary` có dòng "330". → Không ghép nét bịt / nét má cửa trừ khi phần chồng ≥ bề dày |
| KT-18 | đợt 3 | `Core/FloorPlan.cs` (tìm khe) | Cửa giữa hai tường thẳng hàng khác dày (220 | khe 900 | 110): bỏ qua "not on or next to any recognised wall", lỗ để trống hết chiều cao (có báo). → Tìm khe giữa tường thẳng hàng khác dày |
| KT-19 | đợt 3 | `Core/FloorPlan.cs:854-857` | `openings` không có `width` tại cửa đã nhận ra (để đổi chiều cao) → báo sai "on a continuous wall: give the opening width". → Tra cả danh sách lỗ đã nhận |
| KT-20 | đợt 3 | `Drawing/PlanTo3dTools.cs:232-236` | Block `CUA-DI` / `CUA-SO` đặt trên layer tường bị bỏ qua là "block trên layer tường". → Xét tên block trước |
| KT-21 | đợt 3 | `skills/ve-3d-kien-truc/SKILL.md:22`; `skills/ve-kien-truc` | Skill 3D nói `A-SLAB` / `A-FLOR` là quy ước của `ve-kien-truc`, nhưng bảng layer `ve-kien-truc` không có → mặt bằng vẽ theo skill 2D không bao giờ tự đổ sàn với `slab: "auto"`. → Thêm layer vào skill 2D hoặc sửa câu skill 3D |
| KT-22 | đợt 3 | `Drawing/PlanTo3dTools.cs:484` | Ba đường bao lồng nhau (sàn – lỗ – đảo trong lỗ): đảo cũng bị trừ như lỗ. → Xét chẵn / lẻ độ lồng |
| KT-23 | đợt 3 | `Drawing/PlanTo3dTools.cs:547-549` | Dựng phần mặt bằng cạnh phần đã dựng (`area_min` / `area_max`): hộp bao chạm nhau tính là chồng → bị chặn trừ khi `on_existing: "add"`. → Chỉ tính chồng thật (diện tích > 0) |

## Điểm chỉ kiểm được trên AutoCAD thật (không tính là lỗi, đề xuất thêm vào checklist)

| Đề xuất | Làm gì | Kết quả đúng |
| --- | --- | --- |
| KT-01 | Bộ cài: dán 3 dòng CCL1 / CCS1 / CCF1 vào ô key (máy rút mạng) | Mở AutoCAD dùng được ngay, hiện đúng gói; không thẻ 🔒 |
| KT-07 | Máy bản gốc: `CLAUDECHATUPDATE`, để AutoCAD mở, chạy bộ cài Full | Sau cài: khung chat "Agent to AutoCAD Full", `edition.json` còn, không quay về bản gốc sau khi mở lại AutoCAD |
| KT-14 | Gói Full: dựng tầng 1 rồi "dựng tầng 2 cùng mặt bằng" | Tầng 2 dựng được, không hỏi xóa tầng 1; sàn tầng 2 không chồng tường tầng 1 |
| KT-24 | Hai hộp chồng một phần, chung một mặt phẳng → `check_interference` | `interfere: true` |
| Pyramid | `draw_pyramid` 4 cạnh, bán kính 500, rotation 0 | Hộp bao ±500 theo X / Y (đáy vuông theo trục, không xoay 45°), đáy ở z của tâm |
| Brep ổn định | Lưu bản vẽ, mở lại / mở bằng đời AutoCAD khác, `get_solid_info` cùng khối | Thứ tự và số cạnh / mặt giữ nguyên (macro bo cạnh theo số dựa vào đây) |
| Chỉ xem + góc nhìn | Bật 🔒 Chỉ xem, `set_view_3d`, đóng bản vẽ | Kiểm AutoCAD có hỏi lưu không (viewport đổi có thể làm bản vẽ "đã sửa") |
| Khóa layer | `rotate_3d` / `mirror_3d` khối trên layer khóa | Câu báo dễ hiểu (hiện có thể là câu "Locked" chung) |
| Ô key một dòng | Dán text nhiều dòng vào ô key của bộ cài | Ghi lại ô giữ gì (cả 3 dòng / dòng đầu) để chốt cách sửa KT-01 |
| Section | L9 / L11 sau khi chạy: kiểm bản vẽ không còn `SectionSettings` mồ côi (PURGE không báo đối tượng lạ) | Không còn đối tượng tạm |

## Cần chủ dự án chốt

**C1 — Xóa khối 3D có từ trước có luôn phải hỏi không? (KT-12)**
Ví dụ: "xóa khối tường cũ rồi dựng lại". Hiện chỉ hỏi khi xóa > 20 đối tượng, nên xóa vài khối tường không có thẻ; nhưng
checklist L4 lại chờ thẻ xác nhận.
- *Luôn hỏi (như phá khối):* an toàn, đóng được đường lách "sao chép → trừ trên bản sao → xóa bản gốc"; thêm một thẻ khi
  cậu đã nói rõ "xóa".
- *Giữ luật > 20:* sửa kết quả đúng của L4; chấp nhận đường lách trên.

**C2 — Câu "ok" có thay được thẻ xác nhận khi sửa khối 3D cũ không? (KT-09)**
Ví dụ: Claude hỏi "Tường cao 3 m nhé?", cậu trả "ok"; cùng lượt đó Claude trừ lỗ cửa khỏi tường cũ mà không hiện thẻ.
- *Chỉ nút Đồng ý trên thẻ:* đúng nghĩa "luôn qua thẻ"; Claude phải hiện thẻ cả khi cậu đã gõ "đồng ý".
- *Giữ như hiện tại (chữ đồng ý cũng được):* nhanh hơn, nhưng "ok" cho một câu hỏi khác cũng mở khóa; nên sửa ít nhất để
  câu hỏi phải là thẻ / câu hỏi về đúng khối đó.

**C3 — Gói 3D có được ghi chữ qua thuộc tính block không? (KT-02)**
Ví dụ: block nhãn phòng có thuộc tính "Phòng khách". Phương án A nói gói 3D không có chữ.
- *Chặn:* đúng phương án A; gói 3D vẫn dùng block không thuộc tính.
- *Cho phép:* coi thuộc tính block là dữ liệu, không phải chữ; ghi rõ trong tài liệu gói.

**C4 — Máy offline có hạ gói được không? (KT-05)**
Ví dụ: máy offline được Full, sau đó cậu đổi xuống 2D và gửi dòng mới; khách vẫn dán lại được dòng Full cũ.
- *Chặn dòng cũ hơn:* plugin chỉ nhận dòng gói mới hơn dòng đang có; vẫn không thu hồi được nếu khách không dán dòng mới.
- *Chấp nhận:* ghi rõ "máy offline không hạ gói được, chỉ cấp Full offline cho máy tin cậy".

**C5 — Gói 3D không có offset / nối / phá đường 2D: có đúng ý? (ghi chú, không phải lỗi)**
Ví dụ: "đùn biên dạng chữ L từ 2 đường đã có" cần nối đường trước; ở gói 3D `join_entities`, `offset_entity`,
`explode_entities` thuộc nhóm 2D nên Claude phải vẽ lại biên dạng bằng polyline.
- *Giữ:* đúng phương án A, đôi khi tốn thêm lượt.
- *Mở 3 lệnh này cho gói 3D:* tiện dựng biên dạng; vẫn không có chữ / kích thước.

## CI

Run `43249502517` trên `full/kiem-tra-doc-lap` @ `219a7bf`: *(điền khi xong)*.

## Thời gian

~1 giờ 15 phút. Tốn nhất: 4 tác vụ rà soát song song (đọc `ChatController.cs`, `FloorPlan.cs`, `PlanTo3dTools.cs`,
`installer/ClaudeCAD.iss`, `agent/src/agent/*`, tải `AcDbMgd.xml`), mỗi tác vụ ~200k token; không thấy số token chính xác
của cả thread.
