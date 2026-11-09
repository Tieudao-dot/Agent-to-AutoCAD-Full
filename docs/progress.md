# Tiến độ triển khai ClaudeCAD

Nhánh làm việc: `claude/wizardly-heisenberg-ugup5c` (nhánh được cấp cho phiên cloud này; không merge vào `main` khi chưa được đồng ý).

Chú thích: "Cloud" = đã kiểm chứng tự động trên Linux (build + test). "Cần test" = phải chạy trên Windows + AutoCAD 2022 thật (ghi vào `docs/test-log.md`).

## Phase 1a — khung xương ✅ code xong

Đã làm:
- Plugin C# (`plugin/ClaudeCAD.Plugin`): `IExtensionApplication`, lệnh `CLAUDECHAT` / `CLAUDECHATRESTART` / `CLAUDECHATLOG`; palette WPF (lịch sử chat, ô nhập Enter/Shift+Enter, text stream, dòng trạng thái tool, Stop, New); `IpcServer` named pipe `claudecad-{pid}` JSON Lines; `SidecarManager` spawn `node agent/dist/index.js`, tự restart tối đa 3 lần, job object để kill node khi AutoCAD đóng; `ToolDispatcher` (map tool → handler, validate, transaction, error envelope); `UndoScope`; `Logger`; tool `ping` (echo hai chiều).
- Sidecar (`agent/`): Agent SDK `query()` streaming input, in-process MCP server `claudecad`, quyền chỉ bộ tool ClaudeCAD + Read/Skill, stream text/tool_status/turn_end, cancel bằng `interrupt()`, new session, timeout 30 s mỗi request, log file.

Cloud: sidecar build ✅, 32/32 test IPC + session ✅; plugin build net48 ✅; 16/16 unit test lõi C# ✅.

Cần test trên AutoCAD: mục "Phase 1a" trong `docs/test-log.md` (NETLOAD, palette, chat stream, `ping`, Stop, New, kill node.exe).

## Phase 1b — vẽ hình học, layer, batch, undo group ✅ code xong

Đã làm:
- Plugin: handler 7.1 (14 tool vẽ), layer 7.4 (`create_layer` idempotent + tự load linetype, `list_layers`, `set_current_layer`, `set_layer_props`), `batch_execute` (một top transaction, nested transaction cho từng call, tối đa 500 call, lỗi từng call không rollback cả batch), `get_drawing_info` (cần cho quy tắc "luôn gọi trước"). Thuộc tính chung `layer`/`color`/`linetype`/`lineweight` cho mọi tool vẽ; layer thiếu → `LAYER_NOT_FOUND`, layer khóa → `LOCKED`.
- Undo theo lượt: `UndoScope` mở group lười trước tool ghi đầu tiên (COM `StartUndoMark`), đóng ở `turn_end`.
- Hình học thuần (`Core/Geometry.cs`): hình chữ nhật, đa giác đều, cung 3 điểm, revcloud bằng bulge, lineweight — có unit test.
- Sidecar: schema zod + mô tả tiếng Anh cho toàn bộ tool trên.

Cloud: `npm test` 38/38 (có test đối chiếu tên tool sidecar ↔ handler C#), `dotnet test` 29/29, build plugin ✅.

Cần test trên AutoCAD: mục "Phase 1b" trong `docs/test-log.md`, quan trọng nhất là **một lệnh U hoàn tác cả lượt** và **batch 200 line < 2 s**.

## Phase 1c — đọc/truy vấn 7.6, sửa 7.5, chốt xác nhận ✅ code xong

Đã làm:
- Plugin: `list_entities` (lọc types + layers wildcard + màu hiệu dụng + cửa sổ inside/crossing, limit mặc định 200, offset, tóm tắt gọn), `count_entities`, `get_entity` (đầy đủ hình học, attribute, dim), `find_text` (TEXT/MTEXT/attribute/dim override/mleader/ô bảng), `measure_distance`, `measure_area` (m²), `get_bounding_box`.
- Sửa: move/copy (nhiều vị trí, deep clone giữ attribute)/rotate/scale/mirror (giữ text đọc xuôi theo MIRRTEXT)/offset (chọn phía theo side_point)/array_rect/array_polar/fillet & chamfer hai LINE/explode/join (line, arc, polyline)/erase (chốt cứng 1000)/set_entity_props/edit_text (thay toàn bộ hoặc find→replace, đổi chiều cao)/edit_polyline.
- Chốt xác nhận hai tầng: (1) system prompt quy tắc 6; (2) sidecar chặn cứng mọi call xóa/sửa > 20 đối tượng có sẵn (kể cả cộng dồn trong `batch_execute`) nếu tin nhắn hiện tại của người dùng không phải câu xác nhận.

Cloud: `npm test` 42/42, `dotnet test` 34/34, build plugin ✅.

Cần test trên AutoCAD: mục "Phase 1c" trong `docs/test-log.md` và kịch bản nghiệm thu 3, 4, 5.

## Phase 1d — ghi chú 7.2, block 7.3, style, view 7.7 ✅ code xong

Đã làm:
- Ghi chú: TEXT (justify, style, width factor), MTEXT (xuống dòng, wrap), 8 loại dim (linear tự chọn ngang/dọc như DIMLINEAR, aligned, angular 3 điểm / 2 line, radial, diameter, ordinate, continue, baseline theo DIMDLI), multileader, bảng (title tùy chọn, bỏ merge dòng đầu).
- Block: define_block (deep clone vào block, base point = gốc, attribute definition, mặc định thay bằng block reference), insert_block (scale, rotation, attribute, nhiều vị trí), list_block_defs, get/set attributes, set_dynamic_props (đổi đơn vị góc sang độ, liệt kê allowed values).
- Style: create_text_style (TrueType cho tiếng Việt hoặc SHX), create_dim_style (từ biến dim hiện hành + mặc định hệ mét, mũi tên archtick/oblique..., DIMSCALE), set_current_styles, list_styles (thêm).
- View: zoom_extents / zoom_window (đặt view qua ViewTableRecord), regen, save_drawing (QSAVE/SaveAs khi có path), purge_unused (lặp tới khi hết), undo_last_turn (gửi một U, chỉ khi lượt hiện tại chưa sửa gì).

Cloud: 72 tool, `npm test` 42/42 (khớp 1-1 với handler C#), build plugin ✅. Các API AutoCAD dùng ở phase này (dim, mleader, table, dynamic block) chỉ kiểm chứng được ở mức biên dịch.

Cần test trên AutoCAD: mục "Phase 1d" trong `docs/test-log.md`, kịch bản nghiệm thu 2 và 6.

## Phase 1e — hệ thống skill, 3 skill mẫu, system prompt hoàn chỉnh ✅ code xong

Đã làm:
- 3 skill mẫu (`skills/ve-kien-truc`, `ve-mep`, `ve-ha-tang`): frontmatter `name` + `description` viết kỹ để Claude tự nạp đúng lúc, mục "Quy trình chuẩn bị", bảng layer chuẩn, quy cách (tường 220/110, cửa, chữ/dim theo tỷ lệ in, tuyến MEP, block ký hiệu, lý trình, cao độ, đổi trục VN-2000, bảng tọa độ), ví dụ `batch_execute` hoàn chỉnh (kịch bản 2).
- Sidecar nạp skill qua plugin cục bộ được dựng lại mỗi phiên (`skills` chỉ gồm skill ClaudeCAD). Thêm skill = tạo thư mục + bấm New.
- System prompt đủ 10 quy tắc §9 + phần chuẩn vẽ/skill + gợi ý hình học.
- Bundle autoloader: `bundle/PackageContents.xml` (R24.1, Win64) + `bundle/make-bundle.mjs` (chạy được trên Linux, tải node_modules cho win32-x64).
- Công cụ `npm run dry-run`: Claude thật + bản vẽ giả lập để kiểm tra hành vi agent không cần AutoCAD.

Cloud: `npm test` 56/56; Agent SDK thật khởi tạo đúng cấu hình (chỉ Read/Skill + 72 tool, 3 skill); dry run kịch bản 2 và 4 đúng hành vi; bundle tạo được.

Cần test trên AutoCAD: mục "Phase 1e" trong `docs/test-log.md`, kịch bản nghiệm thu 10, và toàn bộ 10 kịch bản §12 trên AutoCAD 2022 thật.

## Phase 2 — layout/paper space, viewport, in PDF, xref, dynamic block nâng cao, trim/extend ✅ code xong

(Ngoài spec v1, làm theo yêu cầu bổ sung; thiết kế ở `docs/phase2-design.md`.)

Đã làm: 17 tool mới (layout ×4, viewport ×2, in PDF ×2, xref ×5, trim/extend ×2, dynamic block ×2), tham số `layout` cho mọi tool tạo đối tượng và bộ lọc, `dynamic_props` khi chèn block.

Cloud: 89 tool; `npm test` 56/56; `dotnet test` 35/35; build plugin ✅. API AutoCAD của phase này mới kiểm chứng ở mức biên dịch.

Cần test trên AutoCAD: mục "Phase 2" trong `docs/test-log.md`.

## Phase 3 — installer kèm kiểm tra Node, cập nhật tự động, AutoCAD 2023–2025 ✅ code xong

(Ngoài spec v1, làm theo yêu cầu bổ sung; thiết kế ở `docs/phase3-design.md`.)

Đã làm:
- Multi-target `net48` (AutoCAD 2022–2024, R24.1–R24.3) + `net8.0-windows` (AutoCAD 2025, R25.0); `PackageContents.xml` hai component chọn dll theo series.
- `installer/install.ps1` (kiểm tra Node >= 18, winget, copy giữ skill người dùng, kiểm tra đăng nhập Claude, gỡ) và `installer/ClaudeCAD.iss` (Setup.exe).
- Cập nhật tự động: manifest `latest.json` (version/url/sha256), kiểm tra mỗi ngày, `CLAUDECHATUPDATE` tải + kiểm SHA-256 + áp dụng sau khi AutoCAD đóng.
- CI GitHub Actions trên Windows: test sidecar với named pipe thật, test lõi C#, build cả hai target, bundle + zip + Setup.exe + latest.json; tag `v*` → GitHub Release.
- Phiên bản 0.3.0 thống nhất (có test).

Cloud: `npm test` 58/58, `dotnet test` 43/43, build hai target ✅, bundle + zip ✅. **CI Windows xanh**: test IPC với named pipe Windows thật, build hai target, Setup.exe; artifact `ClaudeCAD-0.3.0` trên tab Actions của PR.

Cần test: mục "Phase 3" trong `docs/test-log.md` (installer, AutoCAD 2023/2024/2025, luồng cập nhật).

## Khung chat v0.4.0 — 20 tính năng ✅ code xong

Theo đề xuất đã duyệt: chọn đối tượng / lấy điểm, handle bấm được, Xem kết quả, Hoàn tác lượt này, ảnh, bảng Excel, mẫu lệnh `/`, chuẩn + tỷ lệ, ↑↓, thẻ xác nhận, nhật ký thao tác, Thử lại, usage + model/effort, thông báo Windows, Markdown, lịch sử theo bản vẽ (resume phiên), Copy/Xuất, sáng/tối + cỡ chữ, Sửa và gửi lại. Chi tiết và cách dùng: `docs/palette-features.md`.

Cloud: `npm test` 70/70, `dotnet test` 56/56, build ✅; CI Windows chạy thêm test giao diện WPF thật và chụp ảnh palette (artifact `palette-screenshots`).

Cần test trên AutoCAD: mục UI-1 … UI-20 trong `docs/test-log.md`.

## v0.5.0 — thêm và quản lý skill trên khung chat ✅ code xong

- Nút **+ Add skill**: chọn file `.md` (mặc định mở thư mục skill của plugin), kiểm tra phần đầu `name`/`description`, chép vào `Contents\skills\<tên>\SKILL.md`, ghi `Contents\skills\skills.json`, báo trạng thái trên khung chat.
- Nút **Skill (N)**: danh sách với trạng thái, bật/tắt, Sửa (tự nạp lại khi lưu), Xóa, Mở thư mục; ô **Chuẩn** tự liệt kê skill đang bật.
- Trợ lý chỉ nạp skill đang bật; khi skill thay đổi, tin nhắn tiếp theo dùng bộ skill mới mà vẫn tiếp tục cùng cuộc chat.

Cloud: `dotnet test` 68/68, `npm test` 75/75, build ✅. Cần test trên AutoCAD: SK-1 … SK-6 trong `docs/test-log.md`.

## v0.6.0 — 10 tiện ích khung chat ✅ code xong

Theo đề xuất đã duyệt (gói 1, 5, 10, 11, 13, 4 rồi 6, 7, 2, 14):

- **Claude tự nhìn bản vẽ**: tool `capture_view` (ảnh model space vẽ ẩn, không đổi khung nhìn) + nút **Chụp** gửi vùng đang xem.
- **Gợi ý bước tiếp theo**: 1–3 nút dưới câu trả lời (`suggest_next_steps` + `promptSuggestions` của SDK).
- **Macro**: **💾 Lưu macro** sau lượt vẽ; **Macro (N)** chạy lại không cần Claude, tại chỗ cũ hoặc tại điểm chọn; ánh xạ handle giữa các bước.
- **Mẫu lệnh / skill từ cuộc chat**: **☆ Mẫu** trên tin nhắn; **✨ Tạo skill từ cuộc chat này** → thẻ bản nháp → Thêm vào plugin.
- **Chỉ xem** theo bản vẽ: plugin chặn mọi lệnh ghi (`READ_ONLY`).
- **Ghi chú dự án** (file cạnh DWG, dùng chung thư mục) + ghi chú riêng bản vẽ, gửi kèm mọi tin nhắn.
- **`@`** chọn layer / block / style / layout của bản vẽ.
- **Ctrl+Shift+Space** và lệnh **CC** mở khung chat.
- **Thẻ kế hoạch** Làm / Sửa kế hoạch / Bỏ trước việc lớn.
- **Bảng thay đổi** của lượt: Thêm · Sửa · Xóa theo layer, bấm để xem.

Chi tiết: `docs/palette-features.md` mục E. Cloud: `dotnet test` 88/88, `npm test` 89/89, build plugin net48 + net8 ✅. Cần test trên AutoCAD: V6-1 … V6-10 trong `docs/test-log.md`.

## v0.7.0 — Claude biết cậu sửa tay + lịch sử phiên bản ✅ code xong

- **Ghi nhận sửa tay** từ lúc AutoCAD nạp plugin (không cần mở khung chat); tin nhắn tiếp theo gửi kèm `manual_changes` (tóm tắt theo layer + mã đối tượng) để Claude đọc lại trước khi làm; khung chat hiện dòng "✎ Bản vẽ đã được sửa ngoài khung chat…".
- **Biên bản** sau mỗi lượt chat (local, tùy chọn cạnh DWG) → so khi mở lại ở phiên AutoCAD sau / máy khác.
- **Lịch sử phiên bản**: bản sao DWG trước khi chat sửa, tối đa 30 phút một lần, giữ 10 bản; **Phiên bản (N) ▸ Xem** mở tab chỉ xem, **Đóng — về bản hiện tại**.

Cloud: `dotnet test` 96/96, `npm test` 91/91, build plugin net48 + net8 ✅. Cần test trên AutoCAD: V7-1 … V7-6 trong `docs/test-log.md`.

## v0.8.0 — chọn layer rồi chat chỉnh sửa ✅ code xong

Theo lựa chọn của người dùng (cả hai cách chọn; Claude tự hiểu theo câu; giữ xác nhận trừ khi chỉ đổi thuộc tính):

- Chip vùng chọn hiện các layer của phần đã chọn kèm số đối tượng, bỏ tick để loại layer, **→ Cả layer** để dùng toàn bộ layer.
- Nút **Layer**: danh sách layer có lọc, màu, số đối tượng, trạng thái → tick → chip "Layer: …".
- Claude phân biệt sửa thuộc tính layer (làm luôn) với sửa đối tượng trên layer (quá 20 thì thẻ Đồng ý / Hủy).

Cloud: `dotnet test` 98/98, `npm test` 93/93, build plugin net48 + net8 ✅. Cần test trên AutoCAD: L-1 … L-4 trong `docs/test-log.md`.

## v0.9.0 — tối ưu + soát lỗi ✅ code xong

- **`CLAUDECHATSELFTEST`**: tự kiểm tra trong AutoCAD thật trên một bản vẽ tạm (vẽ bằng API, nhóm hoàn tác, sự kiện sửa tay, biên bản, chụp ảnh ẩn, danh sách layer, bản sao + mở tab chỉ xem, kết nối trợ lý, đường dẫn) → báo cáo ✓/✗ trong thư mục log.
- So biên bản lúc AutoCAD rảnh ngay sau khi mở bản vẽ; phiên bản chép thẳng file khi không có thay đổi chưa lưu, chép database trước khi bắt đầu lượt khi có; bản vẽ lớn chỉ cập nhật phần thay đổi; macro chạy theo lô; một chỗ duy nhất nghe sự kiện bản vẽ.
- Lượt review độc lập thứ ba: 14 lỗi (4 lớn, 10 nhỏ) đã sửa — xem `docs/development.md`.

Cloud: `dotnet test` 102/102, `npm test` 95/95, build plugin net48 + net8 ✅.

## v0.9.1 — cài đặt 3 bước ✅ code xong

- **A** Bộ cài kèm sẵn Node.js (`Contents\node\node.exe`, CI tải Node 22 LTS cho Windows) — không cần cài Node.
- **B** Nút **Đăng nhập Claude** (thẻ lỗi khi chưa đăng nhập, thẻ Bắt đầu, ⚙): chạy `claude auth login` bằng Claude Code có sẵn trong Agent SDK, xong thì khởi động lại trợ lý — không cần cài Claude Code hay mở terminal. Sidecar gửi `code` trong `agent_error`.
- **C** Bộ cài (và plugin khi nạp) thêm `…\ClaudeCAD.bundle\Contents\...` vào `TRUSTEDPATHS` của AutoCAD 2022–2025 — hết cảnh báo SECURELOAD.
- **D** Lần đầu mở AutoCAD sau khi cài: khung chat tự mở với thẻ **Bắt đầu** (Node, trợ lý, tài khoản, skill; nút Đăng nhập / Chạy tự kiểm tra).
- **E** Tên bộ cài cố định `ClaudeCAD-Setup.exe` → link `releases/latest/download/ClaudeCAD-Setup.exe`; tag `v*` tạo GitHub Release (repo công khai: tải và tự cập nhật không cần đăng nhập GitHub).

Cần test trên máy thật: I-1 … I-5 trong `docs/test-log.md`.

## v0.9.2 — báo cáo lỗi + tài khoản AI ✅ code xong

- **Gửi báo cáo lỗi** (⚙ và thẻ lỗi): zip trên Desktop với thông tin máy, trạng thái, log 3 ngày, tự kiểm tra, log cài đặt; API key / token được che; không có chat hay bản vẽ.
- Bộ cài luôn ghi log (`SetupLogging=yes`).
- **Tài khoản AI** trong ⚙: trạng thái, Đăng nhập, Đổi tài khoản, Đăng xuất, Dùng API key (DPAPI).
- **Toast** trong khung chat; **giới hạn sử dụng AI**: cảnh báo khi sắp hết, thẻ trong chat khi hết (giờ làm mới, đếm ngược, tự gửi lại, dùng API key).

Cloud: `dotnet test` 106/106, `npm test` 95/95, build ✅. Cần test: T-1, T-2, R-1, A-1, A-2.

## v0.9.3 — đăng nhập ngay trong bộ cài ✅ code xong

- Trang cuối bộ cài: ô **Đăng nhập Claude ngay** (`claude.exe auth login` đi kèm, chạy dưới tài khoản Windows của người dùng, ẩn khi đã đăng nhập, bỏ qua khi cài ngầm) — không cần mở AutoCAD.
- Lần đầu mở AutoCAD mà chưa đăng nhập: plugin tự mở trang đăng nhập.
- Bộ cài bỏ ~2.200 file `.map`; trang phát hành ghi kích thước + SHA-256 của Setup.exe.
- Sửa chữ bị cắt trong ⚙ và tiêu đề bảng chọn layer.

Cần test: I-6, I-7.
